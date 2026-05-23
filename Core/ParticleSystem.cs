using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AetherShatter.Core
{
    /// <summary>
    /// Lightweight CPU particle system that renders tiny coloured squares.
    ///
    /// <para><b>Zero-GC design:</b> all particles live in a fixed-size struct array
    /// (<see cref="MAX_PARTICLES"/> slots).  Emission writes into the first inactive
    /// slot found; expiry simply flips <c>Active = false</c>.  No heap allocations
    /// occur during <see cref="Update"/> or <see cref="Draw"/>.</para>
    ///
    /// <para>Two emission modes are provided:
    /// <list type="bullet">
    ///   <item><see cref="Burst"/> — radial explosion of many particles (brick destroy).</item>
    ///   <item><see cref="TrailDot"/> — single stationary dot (ball motion trail).</item>
    /// </list>
    /// </para>
    /// </summary>
    public class ParticleSystem
    {
        // ── Pool configuration ────────────────────────────────────────────────

        /// <summary>
        /// Maximum number of simultaneously active particles.  1024 comfortably covers
        /// 3 simultaneous balls × burst of 18 + continuous trail dots with margin to spare.
        /// </summary>
        private const int MAX_PARTICLES = 1024;

        // ── Internal particle struct ──────────────────────────────────────────

        /// <summary>
        /// Value type representing a single particle.  Kept as a struct so the pool
        /// is a single contiguous array with no per-element heap allocation.
        /// </summary>
        private struct Particle
        {
            /// <summary>Current world-space position in pixels.</summary>
            public Vector2 Position;

            /// <summary>Movement per second in pixels, decayed each frame by drag.</summary>
            public Vector2 Velocity;

            /// <summary>Tint colour applied to the white pixel texture.</summary>
            public Color   Color;

            /// <summary>Remaining lifetime in seconds.  Particle expires when this reaches 0.</summary>
            public float   Life;

            /// <summary>Lifetime at spawn, used to compute the alpha fade ratio.</summary>
            public float   MaxLife;

            /// <summary>Pixel size of the square at full alpha; shrinks as the particle fades.</summary>
            public float   Size;

            /// <summary>Whether this slot is currently in use.  False = available for reuse.</summary>
            public bool    Active;
        }

        // ── Pool and rendering ────────────────────────────────────────────────

        /// <summary>Pre-allocated flat array of all particle slots.</summary>
        private readonly Particle[] _pool = new Particle[MAX_PARTICLES];

        /// <summary>
        /// 1×1 white <see cref="Texture2D"/> stretched and tinted to draw each particle
        /// square.  Created once in the constructor; never reallocated.
        /// </summary>
        private readonly Texture2D _pixel;

        // ── Constructor ───────────────────────────────────────────────────────

        /// <summary>
        /// Initialise the particle system and allocate the shared pixel texture.
        /// </summary>
        /// <param name="gd">The <see cref="GraphicsDevice"/> used to create the texture.</param>
        public ParticleSystem(GraphicsDevice gd)
        {
            _pixel = new Texture2D(gd, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        // ── Emission ──────────────────────────────────────────────────────────

        /// <summary>
        /// Emit a radial burst of particles centred on <paramref name="origin"/>.
        /// Called when a brick is destroyed; the colour matches the brick's tier.
        /// </summary>
        /// <param name="origin">World-space centre of the burst, in pixels.</param>
        /// <param name="color">Base colour; each particle gets a small random brightness variation.</param>
        /// <param name="count">Number of particles to spawn.</param>
        /// <param name="speed">Maximum outward speed in pixels/second.  Each particle
        /// gets a random magnitude in the range [speed×0.4, speed].</param>
        public void Burst(Vector2 origin, Color color, int count, float speed = 180f)
        {
            var rng = AetherShatterGame.Rng;

            for (int i = 0; i < count; i++)
            {
                ref Particle p = ref FindSlot();

                // Random direction uniformly distributed across the full circle.
                float angle = (float)(rng.NextDouble() * MathHelper.TwoPi);

                // Random magnitude between 40 % and 100 % of the max speed for variety.
                float mag = speed * (0.4f + (float)rng.NextDouble() * 0.6f);

                p.Position = origin;
                p.Velocity = new Vector2(MathF.Cos(angle) * mag, MathF.Sin(angle) * mag);

                // Slight brightness variation so particles don't look identical.
                p.Color    = color * (0.7f + (float)rng.NextDouble() * 0.3f);

                // Random lifetime in [0.35, 0.70] seconds.
                p.MaxLife  = 0.35f + (float)rng.NextDouble() * 0.35f;
                p.Life     = p.MaxLife;

                // Random size in [3, 7] pixels.
                p.Size     = 3f + (float)rng.NextDouble() * 4f;
                p.Active   = true;
            }
        }

        /// <summary>
        /// Emit a single stationary dot at <paramref name="pos"/>.
        /// Called every <c>TRAIL_INTERVAL</c> seconds by the ball to produce a
        /// motion-trail effect.  Trail dots have zero velocity and fade out quickly.
        /// </summary>
        /// <param name="pos">World-space position of the dot, in pixels.</param>
        /// <param name="color">Colour of the trail dot.</param>
        /// <param name="size">Pixel size of the dot at full opacity.</param>
        public void TrailDot(Vector2 pos, Color color, float size = 4f)
        {
            ref Particle p = ref FindSlot();
            p.Position = pos;
            p.Velocity = Vector2.Zero;   // stationary — just fades in place
            p.Color    = color;
            p.MaxLife  = 0.18f;          // short lifespan so the trail doesn't linger
            p.Life     = p.MaxLife;
            p.Size     = size;
            p.Active   = true;
        }

        // ── Per-frame update ──────────────────────────────────────────────────

        /// <summary>
        /// Advance all active particles by one frame: decrement lifetime, apply
        /// velocity, and apply velocity drag.  Expired particles are deactivated
        /// (freeing their slot for future emission).
        /// </summary>
        /// <param name="dt">Delta time in seconds for this frame.</param>
        public void Update(float dt)
        {
            for (int i = 0; i < MAX_PARTICLES; i++)
            {
                ref Particle p = ref _pool[i];
                if (!p.Active) continue;

                // Age the particle; expire it if its lifetime is exhausted.
                p.Life -= dt;
                if (p.Life <= 0f) { p.Active = false; continue; }

                // Move by velocity, then slow down slightly each frame (air resistance).
                p.Position += p.Velocity * dt;
                p.Velocity *= 0.92f;   // multiplicative drag coefficient per frame
            }
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw all active particles into the currently open <see cref="SpriteBatch"/>.
        /// Each particle is a square whose side length and alpha both shrink linearly
        /// with its remaining lifetime to produce a natural fade-and-shrink effect.
        /// </summary>
        /// <param name="sb">An active sprite batch (Begin must already have been called).</param>
        public void Draw(SpriteBatch sb)
        {
            for (int i = 0; i < MAX_PARTICLES; i++)
            {
                ref Particle p = ref _pool[i];
                if (!p.Active) continue;

                // Normalised lifetime [0=expired, 1=just spawned]; drives alpha and size.
                float alpha = p.Life / p.MaxLife;

                // Pixel size shrinks toward 1 as the particle ages.
                int s = (int)(p.Size * alpha + 1f);

                sb.Draw(_pixel,
                    new Rectangle(
                        (int)(p.Position.X - s * 0.5f),  // centre the square on Position
                        (int)(p.Position.Y - s * 0.5f),
                        s, s),
                    p.Color * alpha);   // fade alpha matches size reduction
            }
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Find and return a reference to the first inactive particle slot.
        /// If all slots are occupied (only happens under extreme burst conditions),
        /// slot 0 is recycled rather than skipping the emission — this sacrifices one
        /// old particle rather than silently dropping the new one.
        /// </summary>
        private ref Particle FindSlot()
        {
            for (int i = 0; i < MAX_PARTICLES; i++)
                if (!_pool[i].Active) return ref _pool[i];

            // Pool exhausted — recycle the oldest slot to avoid silent drops.
            return ref _pool[0];
        }
    }
}
