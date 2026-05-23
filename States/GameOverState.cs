using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AetherShatter.Core;

namespace AetherShatter.States
{
    /// <summary>
    /// Shown when the player exhausts all lives.
    ///
    /// <para>Visual treatment:
    /// <list type="bullet">
    ///   <item>Deep-purple background tinted with a red vignette overlay.</item>
    ///   <item>Large "GAME OVER" text with a dark drop shadow, fading in over ~0.8 s.</item>
    ///   <item>Final score centred below the title.</item>
    ///   <item>Pulsing "return to menu" prompt.</item>
    /// </list>
    /// </para>
    ///
    /// <para>A 1-second cooldown after entering the state prevents the player from
    /// accidentally skipping past the screen on the same input that lost the last ball.</para>
    /// </summary>
    public class GameOverState : GameState
    {
        // ── Timers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Total seconds since this state was entered.
        /// Used for the 1-second input cooldown and the fade-in alpha ramp.
        /// </summary>
        private float _timer;

        /// <summary>
        /// Separate continuously-running timer for the sine-wave pulse on the
        /// "return to menu" prompt, independent of the fade-in timing.
        /// </summary>
        private float _pulseTimer;

        // ── Constructor ───────────────────────────────────────────────────────

        /// <param name="game">Root game instance.</param>
        public GameOverState(AetherShatterGame game) : base(game) { }

        // ── GameState lifecycle ───────────────────────────────────────────────

        /// <summary>Reset both timers each time this state is entered.</summary>
        public override void Enter()
        {
            _timer      = 0f;
            _pulseTimer = 0f;
        }

        // ── Update ────────────────────────────────────────────────────────────

        /// <summary>
        /// Advance timers and listen for the return-to-menu input.
        /// The 1-second cooldown (guarded by <c>_timer &gt; 1.0f</c>) ensures the
        /// player reads the score before accidentally bouncing to the main menu.
        /// </summary>
        public override void Update(GameTime gt)
        {
            float dt     = (float)gt.ElapsedGameTime.TotalSeconds;
            _timer      += dt;
            _pulseTimer += dt;

            // Only accept input after the 1-second grace period has elapsed.
            if (Game.Input.ActionPressed && _timer > 1.0f)
                Game.StateManager.Change("Menu");
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Render the Game Over screen: background, red tint, title, score, and prompt.
        /// Everything fades in during the first ~0.8 s via a clamped alpha ramp.
        /// </summary>
        public override void Draw(SpriteBatch sb)
        {
            var vp = Game.GraphicsDevice.Viewport;
            int w  = vp.Width;
            int h  = vp.Height;
            var sf = Game.UiFont;

            Game.BeginBatch();

            // ── Background ─────────────────────────────────────────────────────
            // Base deep-purple fill.
            sb.Draw(Game.Pixel, new Rectangle(0, 0, w, h), new Color(5, 0, 12));

            // Red overlay at 30% opacity — creates a sombre "danger" atmosphere.
            sb.Draw(Game.Pixel, new Rectangle(0, 0, w, h), new Color(80, 0, 0) * 0.3f);

            if (sf == null) { Game.EndBatch(); return; }

            // ── Fade-in alpha ramp ─────────────────────────────────────────────
            // Linearly ramps from 0 to 1 over the first 0.8 seconds after entering.
            float alpha = MathHelper.Clamp(_timer / 0.8f, 0f, 1f);

            // ── "GAME OVER" title ──────────────────────────────────────────────
            string go     = "GAME OVER";
            Vector2 gos   = sf.MeasureString(go);
            float goScale = 4f;   // large and dominant

            // Drop shadow: dark red, offset 4 px right and 4 px down.
            sb.DrawString(sf, go,
                new Vector2(w / 2f - gos.X * goScale / 2f + 4f, h * 0.3f + 4f),
                new Color(120, 0, 0) * alpha,
                0f, Vector2.Zero, goScale, SpriteEffects.None, 0f);

            // Main title layer: bright red on top of the shadow.
            sb.DrawString(sf, go,
                new Vector2(w / 2f - gos.X * goScale / 2f, h * 0.3f),
                new Color(255, 40, 40) * alpha,
                0f, Vector2.Zero, goScale, SpriteEffects.None, 0f);

            // ── Final score ────────────────────────────────────────────────────
            // 6-digit zero-padded format (e.g. "001500") is consistent with arcade style.
            string sc   = $"FINAL SCORE :  {Game.FinalScore:D6}";
            Vector2 scs = sf.MeasureString(sc);
            sb.DrawString(sf, sc,
                new Vector2(w / 2f - scs.X / 2f, h * 0.52f),
                new Color(200, 200, 255) * alpha);

            // ── Return prompt (pulsing) ────────────────────────────────────────
            float pulse   = 0.5f + 0.5f * MathF.Sin(_pulseTimer * 2.8f);
            string prompt = "CLICK OR PRESS SPACE TO RETURN";
            Vector2 ps    = sf.MeasureString(prompt);
            sb.DrawString(sf, prompt,
                new Vector2(w / 2f - ps.X / 2f, h * 0.66f),
                new Color(200, 200, 255) * pulse * alpha);

            Game.EndBatch();
        }
    }
}
