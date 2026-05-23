using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AetherShatter.Entities
{
    /// <summary>
    /// Identifies the two power-up varieties that can drop from destroyed bricks.
    /// </summary>
    public enum PowerUpType
    {
        /// <summary>
        /// Splits the first active ball into three by spawning two additional balls
        /// at ±35° from the current ball's direction.  +50 score bonus.
        /// </summary>
        Multiball,

        /// <summary>
        /// Widens the paddle to 150% of its default size for 10 seconds.  +25 score bonus.
        /// </summary>
        PaddleExpand,
    }

    /// <summary>
    /// A collectible power-up token that falls downward from a destroyed brick.
    ///
    /// <para>Power-ups fall at a constant speed and self-expire if they reach the
    /// bottom of the screen without being caught.  They pulse in size each frame to
    /// attract the player's attention.</para>
    ///
    /// <para>Collection is handled externally by <c>GameplayState</c>; this class only
    /// owns its own movement, animation, and draw logic.  The power-up's effect is
    /// applied via <see cref="Paddle.ApplyExpand"/> or ball-spawning logic in
    /// <c>GameplayState.ApplyPowerUp</c>.</para>
    /// </summary>
    public class PowerUp : GameObject
    {
        // ── Public fields ─────────────────────────────────────────────────────

        /// <summary>Which power-up this token delivers when caught by the paddle.</summary>
        public PowerUpType Kind;

        // ── Private fields ────────────────────────────────────────────────────

        /// <summary>
        /// Ever-increasing angle (radians) used to drive the pulsing scale animation
        /// via <c>Sin(_pulse)</c>.  Incremented by <c>4 × dt</c> each frame.
        /// </summary>
        private float _pulse;

        // ── Colour palette ────────────────────────────────────────────────────

        /// <summary>Bright green used for the Multiball token.</summary>
        private static readonly Color MultiBallColor = new Color(100, 255, 100);

        /// <summary>Orange used for the Paddle Expand token.</summary>
        private static readonly Color ExpandColor    = new Color(255, 160,  30);

        // ── Constructor ───────────────────────────────────────────────────────

        /// <summary>
        /// Spawn a power-up token centred on <paramref name="origin"/> (typically the
        /// centre of the destroyed brick that dropped it).
        /// </summary>
        /// <param name="origin">World-space spawn point in pixels.</param>
        /// <param name="kind">The power-up type to deliver on collection.</param>
        public PowerUp(Vector2 origin, PowerUpType kind)
        {
            Kind     = kind;
            Size     = new Vector2(20f, 20f);

            // Centre the 20×20 token on the origin point.
            Position = origin - Size * 0.5f;
        }

        // ── Update ────────────────────────────────────────────────────────────

        /// <summary>
        /// Move the token downward, advance the pulse animation, and expire it if
        /// it falls off the bottom of the play area (y > 900 px).
        /// </summary>
        public override void Update(GameTime gt, float dt)
        {
            // Fall at 90 px/s — slow enough to be catchable but fast enough to add tension.
            Position.Y += 90f * dt;

            // Advance the sine-wave pulse at 4 radians/s for a ~0.6 Hz oscillation.
            _pulse += dt * 4f;

            // Expire if it falls well past the bottom edge (600+ px screen + margin).
            if (Position.Y > 900f) IsAlive = false;
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw the token as three concentric layers:
        /// <list type="number">
        ///   <item>A semi-transparent pulsing glow halo 5 px larger on each side.</item>
        ///   <item>A pulsing square body (scales between 80% and 100% of Size).</item>
        ///   <item>A fixed 4×4 white core dot for a jewel-like highlight.</item>
        /// </list>
        /// The colour is determined by <see cref="Kind"/>: green for Multiball, orange
        /// for Paddle Expand.
        /// </summary>
        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            // Choose colour based on power-up type.
            Color c = Kind == PowerUpType.Multiball ? MultiBallColor : ExpandColor;

            // Sine wave in [0, 1] that drives both the halo alpha and the body scale.
            float glow = 0.5f + 0.5f * MathF.Sin(_pulse);

            // Layer 1 — pulsing glow halo (alpha modulated by the sine wave).
            sb.Draw(pixel,
                new Rectangle(
                    (int)Position.X - 5,
                    (int)Position.Y - 5,
                    (int)Size.X + 10,
                    (int)Size.Y + 10),
                c * glow * 0.4f);

            // Layer 2 — square body that breathes in size between 80% and 100%.
            int s  = (int)(Size.X * (0.8f + glow * 0.2f));
            int cx = (int)Center.X;
            int cy = (int)Center.Y;
            sb.Draw(pixel,
                new Rectangle(cx - s / 2, cy - s / 2, s, s),
                c);

            // Layer 3 — small bright core dot gives a gem / energy-crystal look.
            sb.Draw(pixel,
                new Rectangle(cx - 2, cy - 2, 4, 4),
                Color.White * 0.9f);
        }
    }
}
