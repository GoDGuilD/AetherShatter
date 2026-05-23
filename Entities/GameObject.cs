using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AetherShatter.Entities
{
    /// <summary>
    /// Abstract base class for every visible, updatable object in the game world
    /// (paddle, ball, brick, power-up).  Provides shared position / size data and
    /// enforces the Update / Draw contract on all subclasses.
    /// </summary>
    public abstract class GameObject
    {
        /// <summary>Top-left world-space position of the object, in pixels.</summary>
        public Vector2 Position;

        /// <summary>Width and height of the object's bounding rectangle, in pixels.</summary>
        public Vector2 Size;

        /// <summary>
        /// Whether the object should still participate in update / draw / collision.
        /// Set to <c>false</c> to mark the object for removal at the end of the frame.
        /// </summary>
        public bool IsAlive = true;

        /// <summary>
        /// Axis-aligned bounding rectangle derived from <see cref="Position"/> and
        /// <see cref="Size"/>.  Used for collision detection and drawing.
        /// </summary>
        public Rectangle Bounds => new Rectangle(
            (int)Position.X, (int)Position.Y,
            (int)Size.X,     (int)Size.Y);

        /// <summary>
        /// The centre point of the object in world space.
        /// Convenience shortcut for <c>Position + Size * 0.5f</c>.
        /// </summary>
        public Vector2 Center => Position + Size * 0.5f;

        /// <summary>
        /// Called once per frame to advance the object's internal state
        /// (movement, timers, animations, etc.).
        /// </summary>
        /// <param name="gt">MonoGame GameTime snapshot for the current frame.</param>
        /// <param name="dt">Pre-computed delta time in seconds (<c>gt.Elapsed.TotalSeconds</c>)
        /// passed in so callers avoid repeated boxing allocations.</param>
        public abstract void Update(GameTime gt, float dt);

        /// <summary>
        /// Called once per frame to render the object using a stretched 1×1 white
        /// <paramref name="pixel"/> texture tinted with the desired colour.
        /// All drawing must occur between an active <see cref="SpriteBatch.Begin"/> /
        /// <see cref="SpriteBatch.End"/> pair opened by the caller.
        /// </summary>
        /// <param name="sb">The active sprite batch to draw into.</param>
        /// <param name="pixel">Shared 1×1 white texture used for all primitive drawing.</param>
        public abstract void Draw(SpriteBatch sb, Texture2D pixel);
    }
}
