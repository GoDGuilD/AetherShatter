using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using AetherShatter.Core;

namespace AetherShatter.States
{
    /// <summary>
    /// The main menu screen — shown after the splash and after every Game Over or Victory.
    ///
    /// <para>Layout (top to bottom):
    /// <list type="bullet">
    ///   <item>Large neon-cyan "AETHER SHATTER" title with a drop-shadow.</item>
    ///   <item>"GoDGuilD Edition" subtitle in purple.</item>
    ///   <item>Pulsing "CLICK OR PRESS SPACE TO START" prompt.</item>
    ///   <item>Static controls reminder line.</item>
    ///   <item>Semi-transparent "GoDGuilD StudioS" watermark (bottom-right).</item>
    /// </list>
    /// </para>
    ///
    /// <para>A scanline overlay (1-pixel bands every 4 rows) reinforces the CRT /
    /// synthwave aesthetic without any shader requirement.</para>
    /// </summary>
    public class MenuState : GameState
    {
        // ── Animation state ───────────────────────────────────────────────────

        /// <summary>
        /// Continuously accumulates elapsed time (in seconds) to drive the sine-wave
        /// pulse on the "start" prompt text.  Never resets so the pulse continues
        /// smoothly if the player returns from a Game Over or Victory screen.
        /// </summary>
        private float _pulseTimer;

        // ── Constructor ───────────────────────────────────────────────────────

        /// <param name="game">Root game instance.</param>
        public MenuState(AetherShatterGame game) : base(game) { }

        // ── Update ────────────────────────────────────────────────────────────

        /// <summary>
        /// Advance the pulse animation and listen for the action input to start the game.
        /// Any primary action (left click or Space) immediately queues a transition to
        /// the Gameplay state.
        /// </summary>
        public override void Update(GameTime gt)
        {
            _pulseTimer += (float)gt.ElapsedGameTime.TotalSeconds;

            // Any primary action (click or Space) advances to the gameplay state.
            if (Game.Input.ActionPressed)
                Game.StateManager.Change("Gameplay");
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Render the menu: background, scanline overlay, title text, and watermark.
        /// </summary>
        public override void Draw(SpriteBatch sb)
        {
            var vp = Game.GraphicsDevice.Viewport;
            int w  = vp.Width;
            int h  = vp.Height;

            Game.BeginBatch();

            // ── Background ────────────────────────────────────────────────────
            sb.Draw(Game.Pixel, new Rectangle(0, 0, w, h), new Color(5, 0, 12));

            // Horizontal scanlines every 4 pixels — subtle CRT phosphor effect.
            for (int y = 0; y < h; y += 4)
                sb.Draw(Game.Pixel, new Rectangle(0, y, w, 1), Color.Black * 0.18f);

            // ── Content ───────────────────────────────────────────────────────
            DrawText(sb, w, h);
            DrawWatermark(sb, w, h);

            Game.EndBatch();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Draw all text elements: title, subtitle, start prompt, and controls reminder.
        /// Silently returns if no <see cref="SpriteFont"/> is loaded (shapes still render).
        /// </summary>
        /// <param name="sb">Active sprite batch.</param>
        /// <param name="w">Viewport width in pixels.</param>
        /// <param name="h">Viewport height in pixels.</param>
        private void DrawText(SpriteBatch sb, int w, int h)
        {
            var sf = Game.UiFont;
            if (sf == null) return;

            // Sine pulse in [0.6, 1.0] for the start-prompt alpha (slower = 2.5 rad/s).
            float pulse = 0.6f + 0.4f * MathF.Sin(_pulseTimer * 2.5f);

            // ── Main title ────────────────────────────────────────────────────
            string title = "AETHER SHATTER";
            Vector2 ts   = sf.MeasureString(title);
            float tScale = 3.2f;                            // 3.2× the base font size
            Color titleC = new Color(0, 220, 255);          // neon cyan

            // Drop shadow drawn 3 px right and 3 px down in a dark teal.
            sb.DrawString(sf, title,
                new Vector2(w / 2f - ts.X * tScale / 2f + 3f, h * 0.28f + 3f),
                new Color(0, 80, 120), 0f, Vector2.Zero, tScale, SpriteEffects.None, 0f);

            // Main title layer drawn on top of the shadow.
            sb.DrawString(sf, title,
                new Vector2(w / 2f - ts.X * tScale / 2f, h * 0.28f),
                titleC, 0f, Vector2.Zero, tScale, SpriteEffects.None, 0f);

            // ── Subtitle ──────────────────────────────────────────────────────
            string sub   = "GoDGuilD Edition";
            Vector2 ss   = sf.MeasureString(sub);
            float sScale = 1.4f;
            sb.DrawString(sf, sub,
                new Vector2(w / 2f - ss.X * sScale / 2f,
                            h * 0.28f + ts.Y * tScale + 8f),   // directly below the title
                new Color(180, 50, 255), 0f, Vector2.Zero, sScale, SpriteEffects.None, 0f);

            // ── Start prompt (pulsing) ─────────────────────────────────────────
            string prompt = "CLICK OR PRESS SPACE TO START";
            Vector2 ps    = sf.MeasureString(prompt);
            sb.DrawString(sf, prompt,
                new Vector2(w / 2f - ps.X / 2f, h * 0.62f),
                new Color(200, 200, 255) * pulse,
                0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            // ── Controls reminder (static, dimmed) ────────────────────────────
            string ctrl = "Mouse / Arrow Keys to move   |   Space / Click to launch";
            Vector2 cs  = sf.MeasureString(ctrl);
            sb.DrawString(sf, ctrl,
                new Vector2(w / 2f - cs.X / 2f, h * 0.72f),
                new Color(100, 100, 140),   // dimmed so it doesn't compete with the prompt
                0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Draw the "GoDGuilD StudioS" watermark in the bottom-right corner at low opacity.
        /// </summary>
        private void DrawWatermark(SpriteBatch sb, int w, int h)
        {
            var sf = Game.UiFont;
            if (sf == null) return;

            string wm   = "GoDGuilD StudioS";
            Vector2 wms = sf.MeasureString(wm);

            // 8 px right margin, 6 px bottom margin; 55% opacity for subtlety.
            sb.DrawString(sf, wm,
                new Vector2(w - wms.X - 8f, h - wms.Y - 6f),
                new Color(80, 40, 120) * 0.55f,
                0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }
    }
}
