using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AetherShatter.Entities
{
    /// <summary>
    /// The player-controlled paddle — a sleek, neon energy bar that slides
    /// horizontally across the bottom of the play field.
    ///
    /// <para>Movement uses frame-rate-independent exponential smoothing
    /// (<c>Lerp</c> with a decay factor) so the paddle glides rather than
    /// snapping.  The same <see cref="MoveTo"/> API works identically for mouse,
    /// keyboard, and future touch input — callers only supply a normalised [0,1] X.</para>
    ///
    /// <para>The <b>Paddle Expand</b> power-up is handled entirely here:
    /// <see cref="ApplyExpand"/> widens the paddle and <see cref="Update"/> auto-resets
    /// it after the timer expires.</para>
    /// </summary>
    public class Paddle : GameObject
    {
        // ── Properties ────────────────────────────────────────────────────────

        /// <summary>
        /// Width of the paddle in its normal (unexpanded) state, in pixels.
        /// Stored so <see cref="Update"/> can restore it after an expand power-up expires.
        /// </summary>
        public float DefaultWidth { get; private set; }

        /// <summary>
        /// Seconds remaining on the active Paddle Expand power-up.
        /// Zero when no power-up is active.  Read by the HUD to draw the expand timer bar.
        /// </summary>
        public float ExpandTimer { get; private set; }

        // ── Private fields ────────────────────────────────────────────────────

        /// <summary>Pixel width of the viewport; used to clamp position and compute MoveTo targets.</summary>
        private readonly int _viewportWidth;

        // ── Neon colour palette ────────────────────────────────────────────────
        // Colours are static so they are shared across all instances without allocation.

        /// <summary>Primary fill colour of the paddle body (neon cyan).</summary>
        private static readonly Color BodyColor = new Color(0, 220, 255);

        /// <summary>Semi-transparent outer glow halo drawn slightly larger than the body.</summary>
        private static readonly Color GlowColor = new Color(0, 180, 255, 80);

        /// <summary>Bright highlight used for the centre spine and end-cap nubs.</summary>
        private static readonly Color CoreColor = new Color(200, 255, 255);

        // ── Constructor ───────────────────────────────────────────────────────

        /// <summary>
        /// Create a paddle centred horizontally and positioned near the bottom
        /// of the viewport.
        /// </summary>
        /// <param name="viewportWidth">Viewport width in pixels.</param>
        /// <param name="viewportHeight">Viewport height in pixels.</param>
        public Paddle(int viewportWidth, int viewportHeight)
        {
            _viewportWidth = viewportWidth;
            DefaultWidth   = 110f;
            Size           = new Vector2(DefaultWidth, 14f);

            // Start centred horizontally, 48px from the bottom edge.
            Position = new Vector2(
                (viewportWidth - Size.X) * 0.5f,
                viewportHeight - 48f);
        }

        // ── Power-up API ──────────────────────────────────────────────────────

        /// <summary>
        /// Activate the Paddle Expand power-up: widen the paddle to 150% of its
        /// default width and start a 10-second countdown.  Calling this while an
        /// expand is already active simply resets the timer to 10 s.
        /// </summary>
        public void ApplyExpand()
        {
            ExpandTimer = 10f;
            Size        = new Vector2(DefaultWidth * 1.5f, Size.Y);
        }

        // ── Update ────────────────────────────────────────────────────────────

        /// <summary>
        /// Tick the expand power-up timer and restore the default paddle width
        /// when it expires.
        /// </summary>
        public override void Update(GameTime gt, float dt)
        {
            if (ExpandTimer > 0f)
            {
                ExpandTimer -= dt;

                // Timer has run out — snap back to default width.
                if (ExpandTimer <= 0f)
                {
                    ExpandTimer = 0f;
                    Size        = new Vector2(DefaultWidth, Size.Y);
                }
            }
        }

        // ── Movement ──────────────────────────────────────────────────────────

        /// <summary>
        /// Smoothly move the paddle's centre toward the target normalised X position.
        /// Uses frame-rate-independent exponential decay:
        /// <c>lerp(current, target, 1 - 0.001^dt)</c> which converges at the same
        /// rate regardless of frame rate.
        /// </summary>
        /// <param name="normalizedX">
        /// Target horizontal position as a fraction of the viewport width [0, 1].
        /// Supplied by <see cref="AetherShatter.Core.InputManager.PaddleTargetNormX"/>.
        /// </param>
        /// <param name="dt">Delta time in seconds for this frame.</param>
        public void MoveTo(float normalizedX, float dt)
        {
            // Convert normalised [0,1] to a target pixel centre-X.
            float targetCenterX  = normalizedX * _viewportWidth;
            float currentCenterX = Position.X + Size.X * 0.5f;

            // Exponential smoothing: 0.001^dt gives frame-rate-independent decay.
            float newCenterX = MathHelper.Lerp(currentCenterX, targetCenterX,
                                               1f - MathF.Pow(0.001f, dt));

            // Keep paddle fully inside the viewport at all times.
            Position.X = MathHelper.Clamp(newCenterX - Size.X * 0.5f,
                                           0f, _viewportWidth - Size.X);
        }

        // ── Collision helper ──────────────────────────────────────────────────

        /// <summary>
        /// Compute where on the paddle the ball made contact, normalised to [-1, 1].
        /// <list type="bullet">
        ///   <item>-1 = ball hit the far left edge (bounces hard left).</item>
        ///   <item> 0 = ball hit the centre (bounces straight up).</item>
        ///   <item>+1 = ball hit the far right edge (bounces hard right).</item>
        /// </list>
        /// This value is passed to <see cref="Ball.DeflectFromPaddle"/> to apply
        /// the angle-of-departure calculation.
        /// </summary>
        /// <param name="ballCenterX">Horizontal centre of the ball in world pixels.</param>
        /// <returns>Hit factor in the range [-1, 1].</returns>
        public float GetHitFactor(float ballCenterX) =>
            ((ballCenterX - Position.X) / Size.X) * 2f - 1f;

        // ── Draw ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw the paddle as three layered primitives:
        /// <list type="number">
        ///   <item>A semi-transparent outer glow halo (4 px larger on each side).</item>
        ///   <item>The solid neon-cyan body rectangle.</item>
        ///   <item>A bright centre-spine stripe and two bright end-cap nubs.</item>
        /// </list>
        /// </summary>
        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            var bounds = Bounds;

            // Layer 1 — soft outer glow, extending 4 px outward horizontally and 2 px vertically.
            sb.Draw(pixel,
                new Rectangle(bounds.X - 4, bounds.Y - 2, bounds.Width + 8, bounds.Height + 4),
                GlowColor);

            // Layer 2 — solid neon body fill.
            sb.Draw(pixel, bounds, BodyColor);

            // Layer 3a — 2-pixel bright spine running the length of the paddle's centre.
            sb.Draw(pixel,
                new Rectangle(bounds.X + 4, bounds.Y + bounds.Height / 2 - 1, bounds.Width - 8, 2),
                CoreColor);

            // Layer 3b — bright 4-pixel end-cap nubs on left and right ends.
            sb.Draw(pixel, new Rectangle(bounds.X,          bounds.Y, 4, bounds.Height), CoreColor);
            sb.Draw(pixel, new Rectangle(bounds.Right - 4,  bounds.Y, 4, bounds.Height), CoreColor);
        }
    }
}
