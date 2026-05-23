using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AetherShatter.Core
{
    /// <summary>
    /// Abstract base for all game screens / states (splash, menu, gameplay, etc.).
    /// Each concrete state owns a full Update / Draw cycle and is given a reference
    /// to the main <see cref="AetherShatterGame"/> so it can access shared resources
    /// (pixel texture, font, particles, input) without tight coupling.
    /// </summary>
    public abstract class GameState
    {
        /// <summary>Reference to the root game object; grants access to shared resources.</summary>
        protected AetherShatterGame Game { get; }

        /// <param name="game">The single <see cref="AetherShatterGame"/> instance.</param>
        protected GameState(AetherShatterGame game) => Game = game;

        /// <summary>
        /// Called by <see cref="GameStateManager"/> immediately after this state becomes
        /// active.  Override to reset per-state data (timers, entity lists, etc.).
        /// </summary>
        public virtual void Enter()  { }

        /// <summary>
        /// Called by <see cref="GameStateManager"/> just before this state is replaced.
        /// Override to release per-state resources or unsubscribe from events.
        /// </summary>
        public virtual void Exit()   { }

        /// <summary>Advance the state's logic by one frame.</summary>
        /// <param name="gt">MonoGame frame timing snapshot.</param>
        public abstract void Update(GameTime gt);

        /// <summary>
        /// Render the state.  Each state is responsible for calling
        /// <c>Game.BeginBatch()</c> / <c>Game.EndBatch()</c> itself so states can
        /// use different transform matrices (e.g. screen-shake).
        /// </summary>
        /// <param name="sb">The game's shared <see cref="SpriteBatch"/>.</param>
        public abstract void Draw(SpriteBatch sb);
    }

    /// <summary>
    /// Lightweight finite-state machine that manages named <see cref="GameState"/>
    /// instances.  Transitions are <em>deferred</em>: a call to <see cref="Change"/>
    /// queues the next state, and the swap only happens when
    /// <see cref="FlushTransition"/> is called at the top of the next Update tick.
    /// This prevents any mid-frame state change from corrupting the current frame's
    /// Update or Draw pass.
    /// </summary>
    public class GameStateManager
    {
        /// <summary>Lookup table of all registered states, keyed by name string.</summary>
        private readonly Dictionary<string, GameState> _states = new();

        /// <summary>The currently active state.  Null until the first transition is flushed.</summary>
        private GameState? _current;

        /// <summary>
        /// The state queued by the most recent <see cref="Change"/> call.
        /// Null when no transition is pending.
        /// </summary>
        private GameState? _pending;

        /// <summary>String name of the currently active state, or null before first activation.</summary>
        public string? CurrentName { get; private set; }

        /// <summary>
        /// Register a state so it can later be activated by name via <see cref="Change"/>.
        /// Registering the same name twice overwrites the previous entry.
        /// </summary>
        /// <param name="name">Unique string key (e.g. "Menu", "Gameplay").</param>
        /// <param name="state">The <see cref="GameState"/> instance to associate.</param>
        public void Register(string name, GameState state) => _states[name] = state;

        /// <summary>
        /// Flush any pending state transition.  Must be called at the very start of
        /// <see cref="AetherShatterGame.Update"/> before any other logic runs.
        /// <para>If a transition is pending this method calls <see cref="GameState.Exit"/>
        /// on the outgoing state and <see cref="GameState.Enter"/> on the incoming one.</para>
        /// </summary>
        public void FlushTransition()
        {
            // Nothing to do if no transition was requested.
            if (_pending == null) return;

            // Let the outgoing state clean up.
            _current?.Exit();

            // Swap in the new state and clear the pending slot.
            _current = _pending;
            _pending = null;

            // Notify the new state that it is now active.
            _current.Enter();
        }

        /// <summary>
        /// Queue a transition to the named state.  The transition is not applied
        /// immediately; call <see cref="FlushTransition"/> at the start of the next
        /// frame to complete it.
        /// </summary>
        /// <param name="name">The key of a previously registered state.</param>
        public void Change(string name)
        {
            _pending    = _states[name];
            CurrentName = name;
        }

        /// <summary>Forward the Update tick to the active state.</summary>
        /// <param name="gt">MonoGame frame timing snapshot.</param>
        public void Update(GameTime gt) => _current?.Update(gt);

        /// <summary>Forward the Draw call to the active state.</summary>
        /// <param name="sb">The game's shared <see cref="SpriteBatch"/>.</param>
        public void Draw(SpriteBatch sb) => _current?.Draw(sb);
    }
}
