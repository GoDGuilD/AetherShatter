using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AetherShatter.Core;
using AetherShatter.States;

namespace AetherShatter
{
    /// <summary>
    /// Root MonoGame <see cref="Game"/> class for AetherShatter: GoDGuilD Edition.
    ///
    /// <para>Responsibilities of this class (kept deliberately minimal):
    /// <list type="bullet">
    ///   <item>Own the <see cref="GraphicsDeviceManager"/> and the single <see cref="SpriteBatch"/>.</item>
    ///   <item>Create and expose shared resources: <see cref="Pixel"/>, <see cref="UiFont"/>,
    ///         <see cref="Particles"/>, <see cref="Input"/>.</item>
    ///   <item>Drive the <see cref="StateManager"/> FSM each frame.</item>
    ///   <item>Provide <see cref="BeginBatch"/> / <see cref="EndBatch"/> helpers so states
    ///         can open a batch with a custom transform matrix (screen-shake) without
    ///         needing direct access to the private <c>_spriteBatch</c>.</item>
    /// </list>
    /// All gameplay logic lives in the state classes, keeping this class thin and testable.
    /// </para>
    /// </summary>
    public class AetherShatterGame : Game
    {
        // ── Shared random number generator ────────────────────────────────────

        /// <summary>
        /// Single <see cref="Random"/> instance shared across all systems.
        /// Static so subsystems (ParticleSystem, GameplayState, VictoryState) can access
        /// it directly without passing it through constructors, and without each allocating
        /// their own instance (which would produce correlated sequences if seeded close together).
        /// </summary>
        public static readonly Random Rng = new Random();

        // ── Core subsystems ───────────────────────────────────────────────────

        /// <summary>
        /// Finite-state machine that owns the active game screen.
        /// States are registered by name during <see cref="LoadContent"/> and
        /// transitioned via string keys (e.g. "Menu", "Gameplay", "GameOver").
        /// </summary>
        public GameStateManager StateManager { get; } = new GameStateManager();

        /// <summary>
        /// Platform-agnostic input abstraction.  Updated at the top of every
        /// <see cref="Update"/> tick before the active state runs, so the state
        /// always sees the latest input values for that frame.
        /// </summary>
        public InputManager Input { get; } = new InputManager();

        /// <summary>
        /// Pre-allocated particle pool shared by gameplay and the victory screen.
        /// Ticked once per frame in <see cref="Update"/>.
        /// </summary>
        public ParticleSystem Particles { get; private set; } = null!;

        // ── Shared rendering resources ─────────────────────────────────────────

        /// <summary>
        /// A 1×1 white <see cref="Texture2D"/> used by every entity and state for all
        /// primitive shape drawing (rectangles, borders, glows).  Coloured by passing a
        /// tint <see cref="Color"/> to <see cref="SpriteBatch.Draw"/>.
        /// Allocated once in <see cref="LoadContent"/>; never deallocated.
        /// </summary>
        public Texture2D Pixel { get; private set; } = null!;

        /// <summary>
        /// Arial 14pt <see cref="SpriteFont"/> loaded from the MonoGame Content Pipeline.
        /// <c>null</c> when the content has not been compiled (e.g. first checkout without
        /// running <c>mgcb</c>).  All text-drawing code checks for null and degrades
        /// gracefully so the game remains playable as shapes-only.
        /// </summary>
        public SpriteFont? UiFont { get; private set; }

        // ── Cross-state data ──────────────────────────────────────────────────

        /// <summary>
        /// Score carried from <c>GameplayState</c> into <c>GameOverState</c> /
        /// <c>VictoryState</c> for display.  Written by GameplayState before queuing
        /// a transition; read by the result screens.
        /// </summary>
        public int FinalScore { get; set; }

        // ── Private graphics handles ──────────────────────────────────────────

        /// <summary>Controls back-buffer size and full-screen mode.</summary>
        private readonly GraphicsDeviceManager _graphics;

        /// <summary>
        /// The single shared <see cref="SpriteBatch"/> used by all states.
        /// States must not create their own — they call <see cref="BeginBatch"/> /
        /// <see cref="EndBatch"/> to open and close draw passes on this instance.
        /// </summary>
        private SpriteBatch _spriteBatch = null!;

        // ── Constructor ───────────────────────────────────────────────────────

        /// <summary>
        /// Configure the window dimensions and content root.
        /// Width of 672 px = 10 brick columns × (64 px wide + 4 px gap) + 2 × 16 px side margin.
        /// Height of 820 px gives comfortable vertical play space on a standard 1080p monitor.
        /// </summary>
        public AetherShatterGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth  = 672;
            _graphics.PreferredBackBufferHeight = 820;
            _graphics.ApplyChanges();

            Content.RootDirectory = "Content";
            IsMouseVisible        = true;
            Window.Title          = "AetherShatter — GoDGuilD Edition";
        }

        // ── MonoGame lifecycle ────────────────────────────────────────────────

        /// <summary>
        /// Called once by MonoGame before <see cref="LoadContent"/>.
        /// Currently delegates entirely to the base class; kept as an explicit
        /// override for Android subclassing convenience.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
        }

        /// <summary>
        /// Allocate all managed GPU resources, load content, and register game states.
        /// Only called once at startup (MonoGame guarantee).
        /// </summary>
        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            Particles    = new ParticleSystem(GraphicsDevice);

            // 1×1 white pixel texture — the single texture used for every drawn rectangle.
            Pixel = new Texture2D(GraphicsDevice, 1, 1);
            Pixel.SetData(new[] { Color.White });

            // Load the compiled SpriteFont.  Wrapped in try/catch so the game boots
            // cleanly even when Content.mgcb hasn't been built yet (raw source checkout).
            try
            {
                UiFont = Content.Load<SpriteFont>("Font");
            }
            catch
            {
                // Null font triggers the rectangle fallback in SplashState and causes
                // all other text draws to be silently skipped.
                UiFont = null;
            }

            // ── Register all screens with the state machine ───────────────────
            StateManager.Register("Splash",   new SplashState(this));
            StateManager.Register("Menu",     new MenuState(this));
            StateManager.Register("Gameplay", new GameplayState(this));
            StateManager.Register("GameOver", new GameOverState(this));
            StateManager.Register("Victory",  new VictoryState(this));

            // Queue and immediately flush the Splash state so its Enter() runs
            // before the first Update tick, giving it a valid initial state.
            StateManager.Change("Splash");
            StateManager.FlushTransition();
        }

        // ── Update ────────────────────────────────────────────────────────────

        /// <summary>
        /// Main update loop — called once per frame by MonoGame.
        /// Order of operations is important:
        /// <list type="number">
        ///   <item>Flush any deferred state transition from the previous frame.</item>
        ///   <item>Poll input so the active state sees fresh values.</item>
        ///   <item>Tick the particle system (all states share it).</item>
        ///   <item>Tick the active game state.</item>
        /// </list>
        /// </summary>
        protected override void Update(GameTime gameTime)
        {
            // Step 1: Commit any state change queued during the previous frame.
            // Must happen before input and state update so the correct state receives
            // the first frame of input after a transition.
            StateManager.FlushTransition();

            // Step 2: Poll keyboard/mouse and compute normalised input values.
            Input.Update(GraphicsDevice.Viewport.Width);

            // Step 3: Advance the particle simulation.  States that need particles
            // (GameplayState, VictoryState) do NOT call this themselves to avoid
            // double-ticking.
            Particles.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

            // Step 4: Run the active state's own Update logic.
            StateManager.Update(gameTime);

            base.Update(gameTime);
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Main draw call — called once per frame by MonoGame.
        /// Each <see cref="GameState"/> is responsible for calling
        /// <see cref="BeginBatch"/> and <see cref="EndBatch"/> itself, allowing
        /// states such as <c>GameplayState</c> to use a shake transform matrix on
        /// one pass and an identity matrix on the HUD pass.
        /// </summary>
        protected override void Draw(GameTime gameTime)
        {
            // Clear to the background colour as a safety net in case a state's
            // BeginBatch/EndBatch is mismatched and leaves part of the screen undrawn.
            GraphicsDevice.Clear(new Color(5, 0, 12));

            // Delegate entirely to the active state; it opens and closes the batch.
            StateManager.Draw(_spriteBatch);

            base.Draw(gameTime);
        }

        // ── SpriteBatch helpers ────────────────────────────────────────────────

        /// <summary>
        /// Open a new <see cref="SpriteBatch"/> pass with the standard render settings
        /// (deferred sort, alpha blend, point-clamp sampling).
        /// <para>
        /// States call this instead of accessing <c>_spriteBatch</c> directly so the
        /// settings are consistent across all states and only need to be changed in
        /// one place if the rendering pipeline evolves.
        /// </para>
        /// </summary>
        /// <param name="transform">
        /// Optional world-transform matrix applied to all subsequent draw calls.
        /// Pass <see cref="Matrix.CreateTranslation"/> with a shake offset to implement
        /// screen-shake without a render target.  Pass <c>null</c> for identity (no transform).
        /// </param>
        public void BeginBatch(Matrix? transform = null)
        {
            _spriteBatch.Begin(
                SpriteSortMode.Deferred,    // draw order matches call order — predictable layering
                BlendState.AlphaBlend,      // standard alpha compositing for glow effects
                SamplerState.PointClamp,    // nearest-neighbour scaling — keeps pixel art crisp
                null,                       // no depth stencil
                null,                       // no rasterizer state override
                null,                       // no custom shader effect
                transform);                 // world-space transform (shake / null)
        }

        /// <summary>
        /// Close the currently open <see cref="SpriteBatch"/> pass and submit all
        /// queued draw calls to the GPU.
        /// </summary>
        public void EndBatch() => _spriteBatch.End();
    }
}
