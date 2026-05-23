using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AetherShatter.Core;

namespace AetherShatter.States
{
    /// <summary>
    /// Shown when the player clears all three levels.
    ///
    /// <para>Celebration effects:
    /// <list type="bullet">
    ///   <item>Continuous particle firework bursts in all four game colours every 0.3 s.</item>
    ///   <item>Animated title whose colour cycles through cyan↔blue via a sine wave.</item>
    ///   <item>Final score centred below the title.</item>
    ///   <item>Pulsing "return to menu" prompt.</item>
    ///   <item>"GoDGuilD StudioS" watermark bottom-right.</item>
    /// </list>
    /// </para>
    ///
    /// <para>Firework bursts are emitted into the shared <see cref="ParticleSystem"/>
    /// which is ticked by <c>AetherShatterGame.Update</c> — no double-update needed here.</para>
    ///
    /// <para>Same 1-second input cooldown as <see cref="GameOverState"/> to prevent
    /// accidental skips on the transition frame.</para>
    /// </summary>
    public class VictoryState : GameState
    {
        // ── Timers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Total seconds elapsed since this state was entered.
        /// Drives: input cooldown, text fade-in, title colour cycling.
        /// </summary>
        private float _timer;

        /// <summary>
        /// Absolute time threshold for the next firework burst emission.
        /// When <c>_timer &gt; _burst</c>, a burst fires and <c>_burst</c> advances
        /// by 0.3 seconds, scheduling the next one.
        /// </summary>
        private float _burst;

        // ── Firework colour palette ────────────────────────────────────────────

        // Same four colours used throughout the game's neon aesthetic.
        // Defined inline in Update since they are only used there — see method.

        // ── Constructor ───────────────────────────────────────────────────────

        /// <param name="game">Root game instance.</param>
        public VictoryState(AetherShatterGame game) : base(game) { }

        // ── GameState lifecycle ───────────────────────────────────────────────

        /// <summary>Reset timers when the state is first entered so fireworks start immediately.</summary>
        public override void Enter()
        {
            _timer = 0f;
            _burst = 0f;   // zero ensures the first burst fires on the very first Update tick
        }

        // ── Update ────────────────────────────────────────────────────────────

        /// <summary>
        /// Advance the scene timer, emit scheduled firework bursts, and check for the
        /// return-to-menu input after the 1-second cooldown.
        /// </summary>
        public override void Update(GameTime gt)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            _timer  += dt;

            // ── Firework scheduling ────────────────────────────────────────────
            if (_timer > _burst)
            {
                // Schedule the next burst 0.3 s from now.
                _burst = _timer + 0.3f;

                var vp  = Game.GraphicsDevice.Viewport;
                var rng = AetherShatterGame.Rng;

                // Random position within the upper 60 % of the screen, 80 px from each side.
                float x = 80f + (float)rng.NextDouble() * (vp.Width - 160f);
                float y = 60f + (float)rng.NextDouble() * (vp.Height * 0.6f);

                // Pick a random colour from the four neon game colours.
                Color[] cols =
                {
                    new Color(0,   220, 255),   // neon cyan  (ball / Glass brick)
                    new Color(255,  50, 200),   // hot pink   (Neon brick)
                    new Color(255, 220,  30),   // core yellow (Core brick)
                    new Color(100, 255, 100),   // bright green (Multiball power-up)
                };

                // Emit 28 particles at 260 px/s — more dramatic than gameplay bursts.
                Game.Particles.Burst(new Vector2(x, y), cols[rng.Next(cols.Length)], 28, 260f);
            }

            // Note: Particles.Update() is already called by AetherShatterGame.Update
            // every frame — no need to call it again here.

            // ── Return to menu ────────────────────────────────────────────────
            // 1-second cooldown prevents accidental input on the transition frame.
            if (Game.Input.ActionPressed && _timer > 1.0f)
                Game.StateManager.Change("Menu");
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Render the victory screen: background, particle fireworks, animated title,
        /// final score, return prompt, and watermark.
        /// </summary>
        public override void Draw(SpriteBatch sb)
        {
            var vp = Game.GraphicsDevice.Viewport;
            int w  = vp.Width;
            int h  = vp.Height;
            var sf = Game.UiFont;

            Game.BeginBatch();

            // ── Background ─────────────────────────────────────────────────────
            sb.Draw(Game.Pixel, new Rectangle(0, 0, w, h), new Color(5, 0, 12));

            // ── Particle fireworks ─────────────────────────────────────────────
            // Drawn before text so bursts appear behind the UI elements.
            Game.Particles.Draw(sb);

            if (sf == null) { Game.EndBatch(); return; }

            // ── Fade-in alpha ──────────────────────────────────────────────────
            // Ramps from 0 to 1 over the first 0.9 seconds after entering the state.
            float alpha = MathHelper.Clamp(_timer / 0.9f, 0f, 1f);

            // ── Animated title colour ──────────────────────────────────────────
            // A sine wave cycles the R and G channels, shifting the title from pale
            // cyan toward a saturated electric blue over a ~2 s period.
            float hue    = 0.5f + 0.5f * MathF.Sin(_timer * 3f);   // [0, 1]
            Color titleC = new Color(
                (byte)(hue * 80),           // red channel: 0–80
                (byte)(200 + hue * 55),     // green channel: 200–255
                (byte)255);                 // blue channel: constant full

            // ── "AETHER SHATTERED" title ───────────────────────────────────────
            string title  = "AETHER SHATTERED";
            Vector2 ts    = sf.MeasureString(title);
            float tScale  = 3.2f;

            // Drop shadow: dark navy, offset 3 px right and 3 px down.
            sb.DrawString(sf, title,
                new Vector2(w / 2f - ts.X * tScale / 2f + 3f, h * 0.28f + 3f),
                new Color(0, 60, 90) * alpha,
                0f, Vector2.Zero, tScale, SpriteEffects.None, 0f);

            // Main title layer with animated colour.
            sb.DrawString(sf, title,
                new Vector2(w / 2f - ts.X * tScale / 2f, h * 0.28f),
                titleC * alpha,
                0f, Vector2.Zero, tScale, SpriteEffects.None, 0f);

            // ── Final score ────────────────────────────────────────────────────
            string sc   = $"FINAL SCORE :  {Game.FinalScore:D6}";
            Vector2 scs = sf.MeasureString(sc);
            sb.DrawString(sf, sc,
                new Vector2(w / 2f - scs.X / 2f, h * 0.52f),
                new Color(200, 220, 255) * alpha);

            // ── Return prompt (pulsing) ────────────────────────────────────────
            float pulse   = 0.5f + 0.5f * MathF.Sin(_timer * 2.5f);
            string prompt = "CLICK OR PRESS SPACE TO RETURN";
            Vector2 ps    = sf.MeasureString(prompt);
            sb.DrawString(sf, prompt,
                new Vector2(w / 2f - ps.X / 2f, h * 0.66f),
                new Color(200, 200, 255) * pulse * alpha);

            // ── Watermark ─────────────────────────────────────────────────────
            string wm   = "GoDGuilD StudioS";
            Vector2 wms = sf.MeasureString(wm);
            sb.DrawString(sf, wm,
                new Vector2(w - wms.X - 8f, h - wms.Y - 6f),
                new Color(80, 40, 120) * 0.5f);

            Game.EndBatch();
        }
    }
}
