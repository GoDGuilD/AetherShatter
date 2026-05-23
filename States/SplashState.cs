using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AetherShatter.Core;

namespace AetherShatter.States
{
    /// <summary>
    /// The opening splash screen that displays "GoDGuilD StudioS Presents" before
    /// auto-transitioning to the main menu.
    ///
    /// <para><b>Animation phases:</b>
    /// <list type="number">
    ///   <item><b>FadeIn</b>  (1.2 s) — studio text fades from invisible to full opacity.</item>
    ///   <item><b>Hold</b>    (1.8 s) — text held at full opacity so the player can read it.</item>
    ///   <item><b>FadeOut</b> (1.0 s) — text fades back to black, then Menu state loads.</item>
    /// </list>
    /// </para>
    ///
    /// <para><b>Font fallback:</b> if the MonoGame Content Pipeline hasn't been run yet
    /// (no compiled SpriteFont), the state gracefully substitutes coloured rectangles
    /// for the studio name so the game is still playable from a plain source checkout.</para>
    /// </summary>
    public class SplashState : GameState
    {
        // ── Internal phase FSM ────────────────────────────────────────────────

        /// <summary>The three sequential animation phases of the splash screen.</summary>
        private enum Phase { FadeIn, Hold, FadeOut }

        /// <summary>Which animation phase is currently running.</summary>
        private Phase _phase = Phase.FadeIn;

        /// <summary>Elapsed seconds within the current phase.  Resets on each phase transition.</summary>
        private float _timer = 0f;

        // ── Phase durations ───────────────────────────────────────────────────

        /// <summary>Duration of the fade-in phase in seconds.</summary>
        private const float FADE_IN_DUR  = 1.2f;

        /// <summary>Duration of the hold phase in seconds (text fully visible).</summary>
        private const float HOLD_DUR     = 1.8f;

        /// <summary>Duration of the fade-out phase in seconds.</summary>
        private const float FADE_OUT_DUR = 1.0f;

        // ── Computed alpha ────────────────────────────────────────────────────

        /// <summary>
        /// Current opacity of the studio text in [0, 1], computed from the active phase
        /// and elapsed time within that phase.
        /// <list type="bullet">
        ///   <item>FadeIn:  linear ramp 0 → 1 over <see cref="FADE_IN_DUR"/> seconds.</item>
        ///   <item>Hold:    constant 1.</item>
        ///   <item>FadeOut: linear ramp 1 → 0 over <see cref="FADE_OUT_DUR"/> seconds.</item>
        /// </list>
        /// </summary>
        private float Alpha => _phase switch
        {
            Phase.FadeIn  => _timer / FADE_IN_DUR,
            Phase.Hold    => 1f,
            Phase.FadeOut => 1f - _timer / FADE_OUT_DUR,
            _             => 0f
        };

        // ── Constructor ───────────────────────────────────────────────────────

        /// <param name="game">Root game instance.</param>
        public SplashState(AetherShatterGame game) : base(game) { }

        // ── GameState lifecycle ───────────────────────────────────────────────

        /// <summary>Reset to the beginning of the FadeIn phase each time the splash is entered.</summary>
        public override void Enter()
        {
            _phase = Phase.FadeIn;
            _timer = 0f;
        }

        // ── Update ────────────────────────────────────────────────────────────

        /// <summary>
        /// Advance the per-phase timer and trigger phase transitions.
        /// When FadeOut completes, queues a transition to the "Menu" state.
        /// </summary>
        public override void Update(GameTime gt)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            _timer += dt;

            // Duration threshold for the current phase.
            float limit = _phase switch
            {
                Phase.FadeIn  => FADE_IN_DUR,
                Phase.Hold    => HOLD_DUR,
                Phase.FadeOut => FADE_OUT_DUR,
                _             => 0f
            };

            if (_timer >= limit)
            {
                // Reset the per-phase timer for the next phase.
                _timer = 0f;

                switch (_phase)
                {
                    case Phase.FadeIn:
                        _phase = Phase.Hold;
                        break;

                    case Phase.Hold:
                        _phase = Phase.FadeOut;
                        break;

                    case Phase.FadeOut:
                        // Splash is complete — queue transition to the main menu.
                        Game.StateManager.Change("Menu");
                        return;
                }
            }
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Render the splash screen: full-screen dark background plus the studio
        /// name text (or a colour-bar fallback) at the computed <see cref="Alpha"/>.
        /// </summary>
        public override void Draw(SpriteBatch sb)
        {
            var vp  = Game.GraphicsDevice.Viewport;
            float a = MathHelper.Clamp(Alpha, 0f, 1f);

            Game.BeginBatch();

            // Deep purple-black background fills the screen every frame.
            sb.Draw(Game.Pixel,
                new Rectangle(0, 0, vp.Width, vp.Height),
                new Color(5, 0, 12));

            // Delegate text drawing so the font fallback is isolated to one method.
            DrawStudioText(sb, vp.Width, vp.Height, a);

            Game.EndBatch();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Draw the studio name and "PRESENTS" line at the given alpha.
        /// Falls back to coloured rectangle bars when no <see cref="SpriteFont"/> is available.
        /// </summary>
        /// <param name="sb">Active sprite batch.</param>
        /// <param name="w">Viewport width in pixels.</param>
        /// <param name="h">Viewport height in pixels.</param>
        /// <param name="a">Opacity in [0, 1].</param>
        private void DrawStudioText(SpriteBatch sb, int w, int h, float a)
        {
            var sf = Game.UiFont;

            if (sf == null)
            {
                // ── Font not available — draw placeholder bars ─────────────────
                // Two horizontal blocks centred on screen suggest the studio name
                // without requiring the content pipeline to have been run first.
                sb.Draw(Game.Pixel,
                    new Rectangle(w / 2 - 120, h / 2 - 8, 240, 8),
                    new Color(180, 50, 255) * a);
                sb.Draw(Game.Pixel,
                    new Rectangle(w / 2 - 80, h / 2 + 8, 160, 5),
                    new Color(120, 30, 200) * a);
                return;
            }

            // ── Font available — draw actual text ──────────────────────────────

            // Vivid purple for the studio name; slightly dimmer for the "PRESENTS" line.
            Color studioColor   = new Color(200, 80, 255) * a;
            Color presentsColor = new Color(140, 100, 200) * a;

            string studioLine   = "GoDGuilD StudioS";
            string presentsLine = "P R E S E N T S";

            // Measure both strings so we can centre them horizontally.
            Vector2 studioSize   = sf.MeasureString(studioLine);
            Vector2 presentsSize = sf.MeasureString(presentsLine);

            // Studio name drawn at 2.4× scale — large and dominant.
            float scale = 2.4f;
            sb.DrawString(sf, studioLine,
                new Vector2(w / 2f - studioSize.X * scale / 2f,
                            h / 2f - studioSize.Y * scale - 10f),
                studioColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // "P R E S E N T S" drawn at 1× scale, centred below the studio name.
            sb.DrawString(sf, presentsLine,
                new Vector2(w / 2f - presentsSize.X / 2f, h / 2f + 10f),
                presentsColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }
    }
}
