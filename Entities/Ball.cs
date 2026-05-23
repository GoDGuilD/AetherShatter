using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AetherShatter.Core;

namespace AetherShatter.Entities
{
    /// <summary>
    /// The glowing energy sphere — the core projectile of the game.
    ///
    /// <para><b>Physics model:</b> the ball is treated as an axis-aligned bounding box
    /// (AABB) for collision purposes.  On each potential collision the penetration depth
    /// on the X and Y axes is measured via <see cref="GetOverlap"/>; the ball is then
    /// pushed out and its velocity reflected along the axis of <em>least</em> penetration.
    /// This "minimum-overlap" approach eliminates the "sticky corner" bug where a ball
    /// moving at 45° would tunnel through the join of two bricks.</para>
    ///
    /// <para><b>Paddle deflection:</b> after bouncing off the paddle the outgoing angle
    /// is recalculated from scratch using <see cref="DeflectFromPaddle"/> so the player
    /// has meaningful control over the ball's trajectory.</para>
    ///
    /// <para><b>Trail effect:</b> <see cref="EmitTrail"/> is called every frame and
    /// emits a trail dot into the shared <see cref="ParticleSystem"/> at a fixed
    /// interval, producing the glowing comet-tail look.</para>
    /// </summary>
    public class Ball : GameObject
    {
        // ── Public state ──────────────────────────────────────────────────────

        /// <summary>Current velocity in pixels per second.  Zero while the ball is attached.</summary>
        public Vector2 Velocity;

        /// <summary>
        /// <c>true</c> once the player launches the ball; <c>false</c> while it sits on
        /// the paddle waiting for the launch input.
        /// </summary>
        public bool IsLaunched;

        /// <summary>Half the width of the ball's bounding square, used for position offset maths.</summary>
        public float Radius => Size.X * 0.5f;

        // ── Private state ─────────────────────────────────────────────────────

        /// <summary>Accumulator for trail dot emission; resets when it reaches <see cref="TRAIL_INTERVAL"/>.</summary>
        private float _trailTimer;

        // ── Constants ─────────────────────────────────────────────────────────

        /// <summary>
        /// Seconds between trail dot emissions.  Lower = denser trail, higher CPU cost.
        /// 0.025 s produces a smooth comet tail at 60 fps without flooding the particle pool.
        /// </summary>
        private const float TRAIL_INTERVAL = 0.025f;

        /// <summary>
        /// Ball speed in pixels per second.  Constant for all balls (multiball clones
        /// launch at the same speed from different angles).
        /// </summary>
        private const float SPEED = 420f;

        // ── Neon colour palette ────────────────────────────────────────────────

        /// <summary>Main body fill colour — warm pale yellow suggesting hot plasma.</summary>
        private static readonly Color BallColor  = new Color(255, 255, 180);

        /// <summary>Semi-transparent outer halo drawn ~1.8× larger than the body.</summary>
        private static readonly Color GlowColor  = new Color(255, 220, 60, 100);

        /// <summary>Colour emitted as trail dots behind the ball.</summary>
        private static readonly Color TrailColor = new Color(255, 200, 50);

        // ── Constructor ───────────────────────────────────────────────────────

        /// <summary>
        /// Create a new ball.  Position must be set separately via <see cref="Attach"/>
        /// or direct assignment before the ball becomes visible.
        /// </summary>
        public Ball()
        {
            Size     = new Vector2(14f, 14f);   // 14×14 px square bounding box
            Velocity = Vector2.Zero;
        }

        // ── Launch / attach ───────────────────────────────────────────────────

        /// <summary>
        /// Set the ball's velocity and mark it as launched.
        /// Called on player input (Space / click) or when spawned as a multiball clone.
        /// </summary>
        /// <param name="angleDeg">
        /// Launch angle in degrees measured from the positive X axis.
        /// Default of <c>-70°</c> fires the ball steeply upward-left; use values
        /// between roughly <c>-80°</c> and <c>-50°</c> for multiball spread shots.
        /// </param>
        public void Launch(float angleDeg = -70f)
        {
            float rad  = MathHelper.ToRadians(angleDeg);
            Velocity   = new Vector2(MathF.Cos(rad), MathF.Sin(rad)) * SPEED;
            IsLaunched = true;
        }

        /// <summary>
        /// Attach (glue) the ball to the paddle surface so it moves with the paddle
        /// until the player presses launch.  Clears velocity and resets <see cref="IsLaunched"/>.
        /// </summary>
        /// <param name="paddleCenter">Centre of the paddle in world space (pixels).</param>
        public void Attach(Vector2 paddleCenter)
        {
            // Sit the ball's centre directly above the paddle's centre, with 2 px breathing room.
            Position   = paddleCenter - new Vector2(Radius, Radius + 2f);
            IsLaunched = false;
            Velocity   = Vector2.Zero;
        }

        // ── Update ────────────────────────────────────────────────────────────

        /// <summary>
        /// Integrate the ball's position by its velocity.
        /// Does nothing while the ball is still attached to the paddle.
        /// </summary>
        public override void Update(GameTime gt, float dt)
        {
            if (!IsLaunched) return;

            // Simple Euler integration — at 420 px/s the ball moves ~7 px per frame
            // at 60 fps, well under any brick's 22 px height, so tunnelling is unlikely.
            Position += Velocity * dt;
        }

        // ── Trail emission ────────────────────────────────────────────────────

        /// <summary>
        /// Emit a trail dot into the particle system at fixed time intervals.
        /// Should be called every frame from the gameplay update loop.
        /// Has no effect while the ball is attached (not yet launched).
        /// </summary>
        /// <param name="ps">The shared particle system to emit into.</param>
        /// <param name="dt">Delta time in seconds for this frame.</param>
        public void EmitTrail(ParticleSystem ps, float dt)
        {
            if (!IsLaunched) return;

            _trailTimer += dt;
            if (_trailTimer >= TRAIL_INTERVAL)
            {
                _trailTimer = 0f;

                // Emit at the ball's centre, scaled down slightly so the dot is
                // smaller than the ball and reads as a trailing ghost.
                ps.TrailDot(Center, TrailColor, Radius * 0.7f);
            }
        }

        // ── Wall bouncing ─────────────────────────────────────────────────────

        /// <summary>
        /// Test the ball against the four viewport boundaries and reflect its velocity
        /// if it hits the left, right, or top walls.  If the ball passes below the
        /// bottom edge it is marked dead and the method returns <c>true</c>.
        /// </summary>
        /// <param name="viewportWidth">Play-field width in pixels.</param>
        /// <param name="viewportHeight">Play-field height in pixels.</param>
        /// <returns>
        /// <c>true</c> if the ball fell off the bottom (life lost); <c>false</c> otherwise.
        /// </returns>
        public bool BounceWalls(int viewportWidth, int viewportHeight)
        {
            // ── Left wall ──────────────────────────────────────────────────────
            if (Position.X < 0f)
            {
                Position.X = 0f;                      // push the ball back inside
                Velocity.X = MathF.Abs(Velocity.X);  // ensure velocity is rightward
            }
            // ── Right wall ─────────────────────────────────────────────────────
            else if (Position.X + Size.X > viewportWidth)
            {
                Position.X = viewportWidth - Size.X;
                Velocity.X = -MathF.Abs(Velocity.X); // ensure velocity is leftward
            }

            // ── Top wall ───────────────────────────────────────────────────────
            if (Position.Y < 0f)
            {
                Position.Y = 0f;
                Velocity.Y = MathF.Abs(Velocity.Y);  // ensure velocity is downward
            }
            // ── Bottom edge — ball lost ────────────────────────────────────────
            // 20 px grace zone below the visible area so the ball fully exits before
            // triggering a life loss (avoids an abrupt pop at the very bottom pixel).
            else if (Position.Y > viewportHeight + 20f)
            {
                IsAlive = false;
                return true;
            }

            return false;
        }

        // ── AABB collision maths ──────────────────────────────────────────────

        /// <summary>
        /// Compute the axis-aligned overlap between this ball's bounding box and
        /// a target rectangle.  Both axes must overlap for a collision to exist.
        /// </summary>
        /// <param name="rect">The rectangle to test against (e.g. a brick's <c>Bounds</c>).</param>
        /// <returns>
        /// A <see cref="Vector2"/> whose X and Y components are the penetration depths
        /// on each axis.  Returns <see cref="Vector2.Zero"/> when there is no overlap.
        /// </returns>
        public Vector2 GetOverlap(Rectangle rect)
        {
            float ballLeft  = Position.X;
            float ballRight = Position.X + Size.X;
            float ballTop   = Position.Y;
            float ballBot   = Position.Y + Size.Y;

            // Overlap on each axis = the length of the intersection segment.
            float overlapX = MathF.Min(ballRight, rect.Right)  - MathF.Max(ballLeft, rect.Left);
            float overlapY = MathF.Min(ballBot,   rect.Bottom) - MathF.Max(ballTop,  rect.Top);

            // Both must be positive for the rectangles to actually intersect.
            if (overlapX <= 0f || overlapY <= 0f) return Vector2.Zero;

            return new Vector2(overlapX, overlapY);
        }

        /// <summary>
        /// Resolve a collision with an axis-aligned rectangle.  Pushes the ball out
        /// of the rectangle and reflects its velocity along the axis of least penetration.
        ///
        /// <para>Resolving along the axis with the <em>smaller</em> overlap is the
        /// minimum-translation-vector (MTV) approach.  It correctly handles corner hits
        /// that naive "just flip Y" solvers misclassify as side hits, preventing the
        /// "sticky corner" bug.</para>
        /// </summary>
        /// <param name="rect">The rectangle to resolve against.</param>
        /// <returns>
        /// <c>'x'</c> if the ball was pushed out horizontally,
        /// <c>'y'</c> if vertically, or <c>'\0'</c> if no collision existed.
        /// The caller uses this to decide whether to play a sound or call hit logic.
        /// </returns>
        public char ResolveCollision(Rectangle rect)
        {
            Vector2 overlap = GetOverlap(rect);
            if (overlap == Vector2.Zero) return '\0';

            if (overlap.X < overlap.Y)
            {
                // Horizontal axis had less penetration — push out left or right.
                if (Center.X < rect.Center.X)
                    Position.X -= overlap.X;   // ball came from the left
                else
                    Position.X += overlap.X;   // ball came from the right

                Velocity.X = -Velocity.X;
                return 'x';
            }
            else
            {
                // Vertical axis had less penetration — push out up or down.
                if (Center.Y < rect.Center.Y)
                    Position.Y -= overlap.Y;   // ball came from above
                else
                    Position.Y += overlap.Y;   // ball came from below

                Velocity.Y = -Velocity.Y;
                return 'y';
            }
        }

        // ── Paddle deflection ─────────────────────────────────────────────────

        /// <summary>
        /// Override the ball's outgoing angle after it bounces off the paddle.
        /// The speed is preserved but the direction is recomputed from
        /// <paramref name="hitFactor"/> so the player can steer the ball.
        ///
        /// <para>At hitFactor = 0 (centre hit) the ball leaves straight up (−90°).
        /// At ±1 (edge hit) it leaves at up to ±65° from vertical, giving a max
        /// angle of 25° from horizontal — steep enough to be fun, shallow enough
        /// to avoid infinitely flat shots that never reach the top bricks.</para>
        /// </summary>
        /// <param name="hitFactor">
        /// Where on the paddle the ball made contact, in the range [-1, 1].
        /// Supplied by <see cref="Paddle.GetHitFactor"/>.
        /// </param>
        public void DeflectFromPaddle(float hitFactor)
        {
            // Map hit factor to a departure angle offset from straight-up.
            float angle = hitFactor * MathHelper.ToRadians(65f);
            float speed = Velocity.Length();   // preserve current speed

            // Build the new velocity: horizontal component from sin(angle),
            // vertical component always upward (negative Y in screen space).
            Velocity = new Vector2(
                MathF.Sin(angle),
                -MathF.Abs(MathF.Cos(angle))) * speed;
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw the ball as three concentric layers to create the glowing energy-sphere look:
        /// <list type="number">
        ///   <item>Large semi-transparent outer glow halo (~1.8× the ball size).</item>
        ///   <item>Solid warm-yellow body square.</item>
        ///   <item>Small bright-white specular highlight offset slightly toward the top-left.</item>
        /// </list>
        /// </summary>
        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            // Layer 1 — outer glow halo, centred on the ball.
            int gs = (int)(Size.X * 1.8f);
            sb.Draw(pixel,
                new Rectangle(
                    (int)(Center.X - gs * 0.5f),
                    (int)(Center.Y - gs * 0.5f),
                    gs, gs),
                GlowColor);

            // Layer 2 — solid body.
            sb.Draw(pixel, Bounds, BallColor);

            // Layer 3 — small bright specular dot (top-left offset by 2 px for depth).
            int cs = (int)(Size.X * 0.35f);
            sb.Draw(pixel,
                new Rectangle(
                    (int)(Center.X - cs * 0.5f) - 2,
                    (int)(Center.Y - cs * 0.5f) - 2,
                    cs, cs),
                Color.White);
        }
    }
}
