using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AetherShatter.Core;
using AetherShatter.Entities;
using AetherShatter.Levels;

namespace AetherShatter.States
{
    /// <summary>
    /// The core gameplay state — owns the full game loop while the player is actively
    /// playing (paddle movement, ball physics, brick collision, power-ups, scoring).
    ///
    /// <para><b>Update order each frame:</b>
    /// <list type="number">
    ///   <item>Check pause input → return to menu if pressed.</item>
    ///   <item>Move and update the paddle.</item>
    ///   <item>Launch the attached ball on action input.</item>
    ///   <item>For each ball: integrate position, emit trail, bounce walls,
    ///         resolve paddle collision, resolve brick collisions.</item>
    ///   <item>Handle ball loss (all balls gone → lose a life or Game Over).</item>
    ///   <item>Update and collect power-ups.</item>
    ///   <item>Check level clear → advance or trigger Victory.</item>
    ///   <item>Tick brick hit-flash timers and screen shake.</item>
    /// </list>
    /// </para>
    ///
    /// <para><b>Draw order:</b>
    /// <list type="number">
    ///   <item>World pass (with optional shake matrix): background grid, bricks,
    ///         power-ups, particles, balls, paddle.</item>
    ///   <item>HUD pass (no shake matrix): score, lives, level, expand bar, watermark.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class GameplayState : GameState
    {
        // ── Brick layout constants ─────────────────────────────────────────────

        /// <summary>Number of brick columns in the grid (must match LevelData column count).</summary>
        private const int BRICK_COLS     = 10;

        /// <summary>Width of each brick in pixels.</summary>
        private const int BRICK_W        = 64;

        /// <summary>Height of each brick in pixels.</summary>
        private const int BRICK_H        = 22;

        /// <summary>Horizontal gap between adjacent bricks in pixels.</summary>
        private const int BRICK_PAD_X    = 4;

        /// <summary>Vertical gap between adjacent brick rows in pixels.</summary>
        private const int BRICK_PAD_Y    = 4;

        /// <summary>Left margin before column 0 starts, in pixels.</summary>
        private const int BRICK_OFFSET_X = 16;

        /// <summary>Top margin before row 0 starts, in pixels.  Leaves room for the HUD.</summary>
        private const int BRICK_OFFSET_Y = 60;

        // ── Tuning constants ──────────────────────────────────────────────────

        /// <summary>
        /// Probability (0–1) that any individual brick will drop a power-up when destroyed.
        /// 0.15 means roughly 1-in-7 bricks carries a power-up token.
        /// </summary>
        private const float POWERUP_CHANCE = 0.15f;

        // ── Game entities ─────────────────────────────────────────────────────

        /// <summary>
        /// The player's paddle.  Recreated each time <see cref="LoadLevel"/> is called
        /// so its position and expand state reset between levels.
        /// </summary>
        private Paddle _paddle = null!;

        /// <summary>
        /// All active balls.  Populated with one ball per level load; the Multiball
        /// power-up appends additional entries.  Balls whose <c>IsAlive</c> is false
        /// are removed each frame.
        /// </summary>
        private List<Ball> _balls = new();

        /// <summary>
        /// All bricks on the current level.  Destroyed bricks are removed from this
        /// list immediately; when it reaches empty the level is cleared.
        /// </summary>
        private List<Brick> _bricks = new();

        /// <summary>
        /// Falling power-up tokens currently in play.  Tokens that reach the bottom
        /// of the screen or are collected by the paddle are removed each frame.
        /// </summary>
        private List<PowerUp> _powerUps = new();

        // ── Persistent game progress ──────────────────────────────────────────

        /// <summary>
        /// Player's current score.  Accumulated across all levels in a single run;
        /// reset to 0 in <see cref="Enter"/> when a new game starts.
        /// </summary>
        private int _score;

        /// <summary>
        /// Remaining lives.  Starts at 3; decremented each time all balls are lost.
        /// Reaching 0 triggers Game Over.
        /// </summary>
        private int _lives = 3;

        /// <summary>
        /// Zero-based index into <see cref="LevelData.All"/> for the current level.
        /// Incremented by <see cref="Update"/> when all bricks are cleared.
        /// </summary>
        private int _levelIndex = 0;

        /// <summary>
        /// True while the ball is sitting on the paddle waiting for a launch input.
        /// False once the ball has been launched; reset to true when a new ball is
        /// attached after a life is lost.
        /// </summary>
        private bool _ballAttached = true;

        // ── Screen shake ──────────────────────────────────────────────────────

        /// <summary>
        /// Remaining duration of the current screen-shake in seconds.
        /// Zero when no shake is active.  Decremented by <see cref="UpdateShake"/>.
        /// </summary>
        private float _shakeTimer;

        /// <summary>
        /// Maximum pixel displacement applied per-axis during the shake.
        /// Larger values = more violent shake.
        /// </summary>
        private float _shakeMag;

        /// <summary>
        /// Per-frame random offset (pixels) applied as a SpriteBatch transform matrix
        /// to shift the entire world pass, creating the screen-shake effect.
        /// Zero when no shake is active.
        /// </summary>
        private Vector2 _shakeOffset;

        // ── HUD animation timer ───────────────────────────────────────────────

        /// <summary>
        /// Total elapsed gameplay time in seconds (never resets mid-run).
        /// Used to drive the sine-wave pulse on the "launch ball" HUD prompt.
        /// </summary>
        private float _totalTime;

        // ── Constructor ───────────────────────────────────────────────────────

        /// <param name="game">Root game instance.</param>
        public GameplayState(AetherShatterGame game) : base(game) { }

        // ── GameState lifecycle ───────────────────────────────────────────────

        /// <summary>
        /// Reset all game progress and load the first level.
        /// Called each time the player starts a new game from the main menu.
        /// </summary>
        public override void Enter()
        {
            _levelIndex = 0;
            _score      = 0;
            _lives      = 3;
            LoadLevel(_levelIndex);
        }

        /// <summary>
        /// Clear all entity lists when leaving gameplay (going to menu, Game Over, or Victory).
        /// Prevents stale references from persisting into subsequent game sessions.
        /// </summary>
        public override void Exit()
        {
            _balls.Clear();
            _bricks.Clear();
            _powerUps.Clear();
        }

        // ── Level loading ─────────────────────────────────────────────────────

        /// <summary>
        /// Populate the entity lists from the level layout at <paramref name="idx"/> in
        /// <see cref="LevelData.All"/>.  Creates the paddle, spawns all bricks, and
        /// attaches a fresh ball to the paddle.
        /// </summary>
        /// <param name="idx">Zero-based level index (0 = Level 1, 1 = Level 2, ...).</param>
        private void LoadLevel(int idx)
        {
            // Wipe any entities from the previous level or the previous attempt.
            _balls.Clear();
            _bricks.Clear();
            _powerUps.Clear();
            _ballAttached = true;

            var vp  = Game.GraphicsDevice.Viewport;
            _paddle = new Paddle(vp.Width, vp.Height);

            var grid = LevelData.All[idx];
            int rows = grid.GetLength(0);
            int cols = grid.GetLength(1);
            var rng  = AetherShatterGame.Rng;

            // Iterate every cell in the level grid.
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int hp = grid[r, c];
                if (hp == 0) continue;   // 0 = empty cell — skip

                // Compute pixel position from column/row index + margins/gaps.
                int x = BRICK_OFFSET_X + c * (BRICK_W + BRICK_PAD_X);
                int y = BRICK_OFFSET_Y + r * (BRICK_H + BRICK_PAD_Y);

                var brick = new Brick(x, y, BRICK_W, BRICK_H, hp);

                // Randomly mark ~15% of bricks to drop a power-up on destruction.
                brick.ShouldDropPowerUp = rng.NextDouble() < POWERUP_CHANCE;

                _bricks.Add(brick);
            }

            // Attach a new ball to the paddle centre, waiting for the launch input.
            var ball = new Ball();
            ball.Attach(_paddle.Center);
            _balls.Add(ball);
        }

        // ── Update ────────────────────────────────────────────────────────────

        /// <summary>
        /// Full per-frame game logic tick.  See class-level documentation for the
        /// exact order of operations.
        /// </summary>
        public override void Update(GameTime gt)
        {
            float dt    = (float)gt.ElapsedGameTime.TotalSeconds;
            _totalTime += dt;
            var vp      = Game.GraphicsDevice.Viewport;

            // ── Pause ─────────────────────────────────────────────────────────
            if (Game.Input.PausePressed)
            {
                Game.StateManager.Change("Menu");
                return;
            }

            // ── Paddle ────────────────────────────────────────────────────────
            // Move first (position must be correct before collision resolution).
            _paddle.MoveTo(Game.Input.PaddleTargetNormX, dt);
            _paddle.Update(gt, dt);   // ticks the expand power-up countdown

            // ── Ball launch ───────────────────────────────────────────────────
            if (_ballAttached && Game.Input.ActionPressed)
            {
                // Random launch angle in [-80°, -60°] (slightly left or right of straight up)
                // so every serve starts differently.
                _balls[0].Launch(AetherShatterGame.Rng.Next(-80, -60));
                _ballAttached = false;
            }

            // While the ball is still attached, keep it glued to the paddle's centre.
            if (_ballAttached && _balls.Count > 0)
                _balls[0].Attach(_paddle.Center);

            // ── Ball physics loop ─────────────────────────────────────────────
            // Iterate backwards so we can safely RemoveAt while iterating.
            for (int i = _balls.Count - 1; i >= 0; i--)
            {
                var ball = _balls[i];

                // Integrate velocity → new position.
                ball.Update(gt, dt);

                // Emit a trail dot into the particle system at fixed intervals.
                ball.EmitTrail(Game.Particles, dt);

                // Bounce off left/right/top walls; detect bottom exit (life lost).
                bool fellOff = ball.BounceWalls(vp.Width, vp.Height);
                if (fellOff || !ball.IsAlive)
                {
                    _balls.RemoveAt(i);
                    continue;   // this ball is gone — move on to the next
                }

                // ── Paddle collision ───────────────────────────────────────────
                if (ball.IsLaunched)
                {
                    char axis = ball.ResolveCollision(_paddle.Bounds);

                    // Only deflect if the ball was travelling downward (positive Y).
                    // This prevents the ball from getting pushed through the paddle
                    // when it approaches from below (rare but possible at steep angles).
                    if (axis != '\0' && ball.Velocity.Y > 0)
                    {
                        float hf = _paddle.GetHitFactor(ball.Center.X);
                        ball.DeflectFromPaddle(hf);
                    }
                }

                // ── Brick collision (one brick per ball per frame) ─────────────
                // Testing from the back of the list so RemoveAt stays O(1) (swap
                // with last element) and we don't miss any newly shifted entries.
                for (int b = _bricks.Count - 1; b >= 0; b--)
                {
                    var brick = _bricks[b];
                    if (!brick.IsAlive) continue;

                    char axis = ball.ResolveCollision(brick.Bounds);
                    if (axis == '\0') continue;   // no overlap — check next brick

                    bool destroyed = brick.Hit();

                    if (destroyed)
                    {
                        // Score: Glass = 100, Neon = 200, Core = 300 (tier value × 100).
                        _score += (int)brick.Tier * 100;

                        // Particle burst matching the brick's colour.
                        Game.Particles.Burst(brick.Center, brick.FaceColor, 18, 200f);

                        // Core brick destruction is the most dramatic event — add shake.
                        if (brick.Tier == BrickTier.Core)
                            TriggerShake(0.25f, 7f);

                        // Conditionally spawn a power-up token.
                        if (brick.ShouldDropPowerUp)
                            SpawnPowerUp(brick.Center);

                        _bricks.RemoveAt(b);
                    }

                    // Stop checking this ball against further bricks this frame.
                    // One collision per ball per frame prevents multi-brick penetration
                    // at high speed and is standard in Breakout-style games.
                    break;
                }
            }

            // ── Life loss check ───────────────────────────────────────────────
            // All balls fell off the bottom while none are marked as attached.
            if (_balls.Count == 0 && !_ballAttached)
            {
                _lives--;

                // Shake feedback even on life loss.
                TriggerShake(0.35f, 9f);

                if (_lives <= 0)
                {
                    // No lives remaining — pass the final score and go to Game Over.
                    Game.FinalScore = _score;
                    Game.StateManager.Change("GameOver");
                    return;
                }

                // Still have lives — reattach a fresh ball so the player can serve again.
                var newBall = new Ball();
                newBall.Attach(_paddle.Center);
                _balls.Add(newBall);
                _ballAttached = true;
            }

            // ── Power-up tokens ───────────────────────────────────────────────
            for (int i = _powerUps.Count - 1; i >= 0; i--)
            {
                var pu = _powerUps[i];
                pu.Update(gt, dt);

                // Expired (fell off bottom).
                if (!pu.IsAlive) { _powerUps.RemoveAt(i); continue; }

                // Collected by the paddle.
                if (pu.Bounds.Intersects(_paddle.Bounds))
                {
                    ApplyPowerUp(pu);
                    _powerUps.RemoveAt(i);
                }
            }

            // ── Level clear check ─────────────────────────────────────────────
            if (_bricks.Count == 0)
            {
                _levelIndex++;

                if (_levelIndex >= LevelData.All.Length)
                {
                    // All levels beaten — Victory!
                    Game.FinalScore = _score;
                    Game.StateManager.Change("Victory");
                }
                else
                {
                    // Load the next level; preserve score and lives.
                    LoadLevel(_levelIndex);
                }
                return;
            }

            // ── Brick flash timers & screen shake ─────────────────────────────
            foreach (var brick in _bricks)
                brick.Update(gt, dt);

            UpdateShake(dt);
        }

        // ── Screen shake ──────────────────────────────────────────────────────

        /// <summary>
        /// Start (or restart) a screen-shake effect.
        /// The shake offsets are applied as a SpriteBatch world-transform in
        /// <see cref="Draw"/> and automatically decay over <paramref name="duration"/> seconds.
        /// </summary>
        /// <param name="duration">How long the shake lasts in seconds.</param>
        /// <param name="magnitude">Maximum pixel displacement per axis (e.g. 7 for a medium shake).</param>
        private void TriggerShake(float duration, float magnitude)
        {
            _shakeTimer = duration;
            _shakeMag   = magnitude;
        }

        /// <summary>
        /// Decay the active screen-shake each frame.
        /// While the timer is positive, a random offset is picked each frame and
        /// multiplied by a linear ramp that approaches zero as the timer expires —
        /// producing a shake that starts strong and fades out smoothly.
        /// </summary>
        /// <param name="dt">Delta time in seconds.</param>
        private void UpdateShake(float dt)
        {
            // No active shake — clear offset and exit early.
            if (_shakeTimer <= 0f) { _shakeOffset = Vector2.Zero; return; }

            _shakeTimer -= dt;

            var   rng = AetherShatterGame.Rng;

            // Random direction each frame for a jittery, organic feel.
            float ox = (float)(rng.NextDouble() * 2 - 1) * _shakeMag;
            float oy = (float)(rng.NextDouble() * 2 - 1) * _shakeMag;

            // Multiply by a 0→1 ramp: full magnitude when timer is high, near-zero at expiry.
            _shakeOffset = new Vector2(ox, oy) * MathHelper.Clamp(_shakeTimer * 4f, 0f, 1f);
        }

        // ── Power-up helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Spawn a power-up token at the given world position, randomly choosing between
        /// Multiball and PaddleExpand with equal probability.
        /// </summary>
        /// <param name="origin">World-space spawn position (brick centre), in pixels.</param>
        private void SpawnPowerUp(Vector2 origin)
        {
            var kind = AetherShatterGame.Rng.Next(2) == 0
                     ? PowerUpType.Multiball
                     : PowerUpType.PaddleExpand;
            _powerUps.Add(new PowerUp(origin, kind));
        }

        /// <summary>
        /// Apply the effect of a collected power-up token to the current game state.
        /// Also awards a small score bonus for the collection.
        /// </summary>
        /// <param name="pu">The power-up token that was caught by the paddle.</param>
        private void ApplyPowerUp(PowerUp pu)
        {
            switch (pu.Kind)
            {
                case PowerUpType.Multiball:
                    // Spawn two extra balls diverging at ±35° from straight up,
                    // using the first active ball's current position as the origin.
                    if (_balls.Count > 0)
                    {
                        var src = _balls[0];
                        for (int i = 0; i < 2; i++)
                        {
                            var nb = new Ball();
                            nb.Position = src.Position;

                            // Left clone at -125° (−90° − 35°), right clone at −55° (−90° + 35°).
                            float angle = MathHelper.ToRadians(-90f + (i == 0 ? -35f : 35f));
                            nb.Velocity   = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 420f;
                            nb.IsLaunched = true;
                            _balls.Add(nb);
                        }
                    }
                    _score += 50;   // collection bonus
                    break;

                case PowerUpType.PaddleExpand:
                    // Delegate entirely to the paddle; it manages the timer and auto-revert.
                    _paddle.ApplyExpand();
                    _score += 25;   // collection bonus
                    break;
            }
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Render the gameplay scene in two SpriteBatch passes:
        /// <list type="number">
        ///   <item><b>World pass</b> — opened with a shake transform matrix so all
        ///         game entities shift together during a screen-shake event.</item>
        ///   <item><b>HUD pass</b> — opened with identity matrix so the score, lives,
        ///         and watermark are always rock-steady regardless of shake.</item>
        /// </list>
        /// </summary>
        public override void Draw(SpriteBatch sb)
        {
            var gd  = Game.GraphicsDevice;
            var vp  = gd.Viewport;
            int w   = vp.Width;
            int h   = vp.Height;
            var pix = Game.Pixel;

            // ── World pass (with optional screen-shake transform) ──────────────
            var shake = Matrix.CreateTranslation(_shakeOffset.X, _shakeOffset.Y, 0f);
            Game.BeginBatch(shake);

            // Deep purple-black background — slightly different shade from other states
            // so entities read clearly against it.
            sb.Draw(pix, new Rectangle(0, 0, w, h), new Color(6, 0, 14));

            // Horizontal scanlines every 4 px — subtle CRT phosphor effect.
            for (int y = 0; y < h; y += 4)
                sb.Draw(pix, new Rectangle(0, y, w, 1), Color.Black * 0.15f);

            // Faint vertical grid lines every 40 px — give the play field depth.
            for (int x = 0; x < w; x += 40)
                sb.Draw(pix, new Rectangle(x, 0, 1, h), new Color(20, 0, 40) * 0.4f);

            // Draw entities back-to-front: bricks → power-ups → particles → balls → paddle.
            // Particles are drawn between power-ups and balls so they sit behind the ball
            // but in front of falling tokens (keeps the glow visible).
            foreach (var brick in _bricks)   brick.Draw(sb, pix);
            foreach (var pu    in _powerUps)  pu.Draw(sb, pix);

            Game.Particles.Draw(sb);   // burst particles + ball trail dots

            foreach (var ball  in _balls)    ball.Draw(sb, pix);
            _paddle.Draw(sb, pix);

            // ── HUD pass (no shake transform) ─────────────────────────────────
            // End the world pass before starting the HUD pass so the matrix is reset.
            Game.EndBatch();
            Game.BeginBatch();   // identity matrix — HUD stays fixed on screen

            DrawHud(sb, w, h);
            DrawWatermark(sb, w, h);
            Game.EndBatch();
        }

        // ── HUD drawing helpers ───────────────────────────────────────────────

        /// <summary>
        /// Draw the in-game heads-up display: score (top-left), level indicator (top-centre),
        /// lives (top-right), expand power-up timer bar (below score), and the launch
        /// prompt when the ball is attached.
        /// </summary>
        /// <param name="sb">Active sprite batch (HUD pass, no shake matrix).</param>
        /// <param name="w">Viewport width in pixels.</param>
        /// <param name="h">Viewport height in pixels.</param>
        private void DrawHud(SpriteBatch sb, int w, int h)
        {
            var sf = Game.UiFont;
            if (sf == null) return;   // no font loaded — skip all text silently

            // ── Score (top-left) ───────────────────────────────────────────────
            // 6-digit zero-padded format matches classic arcade score displays.
            string scoreStr = $"SCORE  {_score:D6}";
            sb.DrawString(sf, scoreStr, new Vector2(14f, 10f), new Color(0, 220, 255));

            // ── Lives indicator (top-right) ────────────────────────────────────
            sb.DrawString(sf, "LIVES", new Vector2(w - 140f, 10f), new Color(0, 220, 255));

            // Draw one 14×14 yellow square per remaining life.
            for (int i = 0; i < _lives; i++)
                sb.Draw(Game.Pixel,
                    new Rectangle(w - 100 + i * 22, 12, 14, 14),
                    new Color(255, 220, 60));

            // ── Level indicator (top-centre) ───────────────────────────────────
            string lvlStr = $"LEVEL {_levelIndex + 1}";
            Vector2 ls    = sf.MeasureString(lvlStr);
            sb.DrawString(sf, lvlStr,
                new Vector2(w / 2f - ls.X / 2f, 10f),
                new Color(180, 50, 255));

            // ── Paddle Expand timer bar (below score, only when active) ────────
            if (_paddle.ExpandTimer > 0f)
            {
                // Ratio of remaining time to full 10-second duration.
                float ratio = _paddle.ExpandTimer / 10f;

                // Background track.
                sb.Draw(Game.Pixel, new Rectangle(14, 34, 160, 6), new Color(30, 30, 60));

                // Fill bar — shrinks left-to-right as the power-up expires.
                sb.Draw(Game.Pixel, new Rectangle(14, 34, (int)(160 * ratio), 6), new Color(255, 160, 30));

                // Label below the bar.
                sb.DrawString(sf, "EXPAND", new Vector2(14f, 40f), new Color(255, 160, 30) * 0.8f);
            }

            // ── Launch prompt (below paddle, only while ball is attached) ──────
            if (_ballAttached)
            {
                string msg  = "CLICK / SPACE  to launch";
                Vector2 ms  = sf.MeasureString(msg);

                // Sine-wave pulse in [0.6, 1.0] draws the player's eye to the prompt.
                float pulse = 0.5f + 0.5f * MathF.Sin(_totalTime * 3.5f);

                sb.DrawString(sf, msg,
                    new Vector2(w / 2f - ms.X / 2f, h - 80f),
                    new Color(200, 200, 255) * (0.6f + 0.4f * pulse));
            }
        }

        /// <summary>
        /// Draw the "GoDGuilD StudioS" branding watermark in the bottom-right corner
        /// at 50% opacity so it is visible but does not distract from gameplay.
        /// </summary>
        private void DrawWatermark(SpriteBatch sb, int w, int h)
        {
            var sf = Game.UiFont;
            if (sf == null) return;

            string wm   = "GoDGuilD StudioS";
            Vector2 wms = sf.MeasureString(wm);

            sb.DrawString(sf, wm,
                new Vector2(w - wms.X - 8f, h - wms.Y - 6f),
                new Color(80, 40, 120) * 0.5f,
                0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }
    }
}
