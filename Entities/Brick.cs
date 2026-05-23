using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AetherShatter.Entities
{
    /// <summary>
    /// Defines the three visual and gameplay tiers of bricks.
    /// The integer value of each member equals that tier's starting HP, which
    /// lets <see cref="Brick"/> cast directly: <c>(BrickTier)hp</c>.
    /// </summary>
    public enum BrickTier
    {
        /// <summary>Weakest tier.  1 hit to destroy.  Rendered in neon blue.</summary>
        Glass = 1,

        /// <summary>Medium tier.  2 hits to destroy.  Rendered in hot pink.</summary>
        Neon  = 2,

        /// <summary>Strongest tier.  3 hits to destroy.  Rendered in core yellow.
        /// Destroying one triggers a screen shake.</summary>
        Core  = 3,
    }

    /// <summary>
    /// A single destructible brick on the play field.
    ///
    /// <para><b>Tiered damage:</b> each <see cref="Hit"/> call decrements HP and
    /// downgrades the brick's colour so the player can see its remaining health at
    /// a glance.  At HP = 0 the brick sets <see cref="GameObject.IsAlive"/> = false
    /// and signals the caller to spawn particles and optionally drop a power-up.</para>
    ///
    /// <para><b>Hit flash:</b> on every hit the brick turns solid white for 0.12 s
    /// before returning to its current-HP colour, giving tactile visual feedback.</para>
    /// </summary>
    public class Brick : GameObject
    {
        // ── Public fields ─────────────────────────────────────────────────────

        /// <summary>Remaining hit points.  Decremented by <see cref="Hit"/>.</summary>
        public int Hp;

        /// <summary>The tier the brick was <em>spawned</em> at (not updated on damage).</summary>
        public BrickTier Tier;

        /// <summary>
        /// Current face fill colour, updated each time the brick takes a hit.
        /// Exposed publicly so the particle system can match burst colours to the brick.
        /// </summary>
        public Color FaceColor;

        /// <summary>
        /// Current glow halo colour (semi-transparent version of <see cref="FaceColor"/>).
        /// Updated alongside <see cref="FaceColor"/>.
        /// </summary>
        public Color GlowColor;

        /// <summary>
        /// Whether a power-up should be spawned when this brick is destroyed.
        /// Set once at level load based on a random roll in <c>GameplayState.LoadLevel</c>.
        /// </summary>
        public bool ShouldDropPowerUp;

        // ── Private fields ────────────────────────────────────────────────────

        /// <summary>
        /// Countdown timer for the white hit-flash effect.  Positive while flashing,
        /// zero otherwise.  Decremented each <see cref="Update"/> tick.
        /// </summary>
        private float _hitFlash;

        // ── Colour tables ─────────────────────────────────────────────────────
        // Indexed by HP value (0 is unused; indices 1-3 map to Glass/Neon/Core).
        // Static so they are allocated once and shared by all Brick instances.

        /// <summary>
        /// Solid face colour per HP level.  Index 0 is a placeholder so the HP value
        /// can be used directly as an array index without an offset.
        /// </summary>
        private static readonly Color[] FaceColors =
        {
            Color.Transparent,            // [0] unused
            new Color( 80, 180, 255),     // [1] Glass — neon blue
            new Color(255,  50, 200),     // [2] Neon  — hot pink
            new Color(255, 220,  30),     // [3] Core  — bright yellow
        };

        /// <summary>
        /// Semi-transparent glow colours corresponding to each HP level.
        /// Alpha of ~60 produces a soft halo without overwhelming the fill.
        /// </summary>
        private static readonly Color[] GlowColors =
        {
            Color.Transparent,
            new Color( 80, 180, 255, 60),
            new Color(255,  50, 200, 60),
            new Color(255, 220,  30, 60),
        };

        // ── Constructor ───────────────────────────────────────────────────────

        /// <summary>
        /// Create a brick at the given pixel coordinates with the specified dimensions
        /// and starting HP.  HP must be in the range [1, 3].
        /// </summary>
        /// <param name="x">Left edge of the brick in world pixels.</param>
        /// <param name="y">Top edge of the brick in world pixels.</param>
        /// <param name="w">Width of the brick in pixels.</param>
        /// <param name="h">Height of the brick in pixels.</param>
        /// <param name="hp">Starting hit points (1 = Glass, 2 = Neon, 3 = Core).</param>
        public Brick(int x, int y, int w, int h, int hp)
        {
            Position  = new Vector2(x, y);
            Size      = new Vector2(w, h);
            Hp        = hp;
            Tier      = (BrickTier)hp;      // cast works because enum values == HP values
            FaceColor = FaceColors[hp];
            GlowColor = GlowColors[hp];
        }

        // ── Damage ────────────────────────────────────────────────────────────

        /// <summary>
        /// Register one hit on the brick.  Decrements HP, triggers the white flash
        /// animation, downgrades the colour if still alive, and marks the brick dead
        /// when HP reaches zero.
        /// </summary>
        /// <returns>
        /// <c>true</c> if this hit destroyed the brick (HP reached 0);
        /// <c>false</c> if the brick survived with remaining HP.
        /// </returns>
        public bool Hit()
        {
            Hp--;

            // Start the white flash timer regardless of whether the brick survived.
            _hitFlash = 0.12f;

            if (Hp <= 0)
            {
                // Brick is destroyed — caller will handle particles, score, power-up.
                IsAlive = false;
                return true;
            }

            // Still alive — downgrade colour to reflect the new (lower) HP.
            FaceColor = FaceColors[Hp];
            GlowColor = GlowColors[Hp];
            return false;
        }

        // ── Update ────────────────────────────────────────────────────────────

        /// <summary>Count down the hit-flash timer each frame.</summary>
        public override void Update(GameTime gt, float dt)
        {
            if (_hitFlash > 0f) _hitFlash -= dt;
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw the brick as:
        /// <list type="number">
        ///   <item>A semi-transparent glow halo 3 px larger on each side.</item>
        ///   <item>A semi-opaque fill rectangle (white during hit-flash, tier colour otherwise).</item>
        ///   <item>A 1-pixel bright border on all four inner edges.</item>
        /// </list>
        /// Skips rendering entirely when <see cref="GameObject.IsAlive"/> is false.
        /// </summary>
        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            // Dead bricks are removed from the list so this guard is a safety net only.
            if (!IsAlive) return;

            var b = Bounds;

            // Layer 1 — soft glow halo slightly larger than the brick body.
            sb.Draw(pixel,
                new Rectangle(b.X - 3, b.Y - 3, b.Width + 6, b.Height + 6),
                GlowColor);

            // Layer 2 — fill rectangle.  White while flashing, tier colour normally.
            Color fill = _hitFlash > 0f ? Color.White : FaceColor;
            sb.Draw(pixel, b, fill * 0.85f);

            // Layer 3 — 1-pixel inner border to give the brick a crisp neon edge.
            // White border during flash, tier colour normally.
            Color border = _hitFlash > 0f ? Color.White : FaceColor;

            sb.Draw(pixel, new Rectangle(b.X,          b.Y,          b.Width, 1),  border);  // top
            sb.Draw(pixel, new Rectangle(b.X,          b.Bottom - 1, b.Width, 1),  border);  // bottom
            sb.Draw(pixel, new Rectangle(b.X,          b.Y,          1, b.Height), border);  // left
            sb.Draw(pixel, new Rectangle(b.Right - 1,  b.Y,          1, b.Height), border);  // right
        }
    }
}
