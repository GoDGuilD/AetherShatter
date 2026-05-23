using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace AetherShatter.Core
{
    /// <summary>
    /// Platform-agnostic input abstraction layer.  On desktop it reads keyboard and
    /// mouse state each frame; on Android the caller invokes <see cref="SetTouchInput"/>
    /// instead, keeping all game logic identical across platforms.
    ///
    /// <para>Design rule: game states query the three public properties
    /// (<see cref="PaddleTargetNormX"/>, <see cref="ActionPressed"/>,
    /// <see cref="PausePressed"/>) and never read raw MonoGame input APIs directly.
    /// Swapping the input source therefore requires only this one file to change.</para>
    /// </summary>
    public class InputManager
    {
        // ── Raw state snapshots (keyboard) ────────────────────────────────────
        /// <summary>Keyboard state captured on the previous frame.</summary>
        private KeyboardState _prevKeys;

        /// <summary>Keyboard state captured on the current frame.</summary>
        private KeyboardState _currKeys;

        // ── Raw state snapshots (mouse) ────────────────────────────────────────
        /// <summary>Mouse state captured on the previous frame.</summary>
        private MouseState _prevMouse;

        /// <summary>Mouse state captured on the current frame.</summary>
        private MouseState _currMouse;

        // ── Processed / normalised outputs ────────────────────────────────────

        /// <summary>
        /// Desired horizontal paddle position as a normalised value in [0, 1]
        /// where 0 = left edge and 1 = right edge of the viewport.
        /// Updated every frame by <see cref="Update"/> or <see cref="SetTouchInput"/>.
        /// </summary>
        public float PaddleTargetNormX { get; private set; }

        /// <summary>
        /// <c>true</c> only on the single frame the player presses the primary action
        /// (left mouse button down-edge or Space key down-edge).  Used to launch the
        /// ball and to advance menus.
        /// </summary>
        public bool ActionPressed { get; private set; }

        /// <summary>
        /// <c>true</c> only on the single frame the player presses Escape / Back.
        /// Used to return to the main menu from gameplay.
        /// </summary>
        public bool PausePressed { get; private set; }

        /// <summary>
        /// Poll MonoGame's keyboard and mouse APIs, compute normalised paddle position,
        /// and detect single-frame button presses.  Call once at the start of each
        /// <c>Game.Update()</c> tick, before any state logic runs.
        /// </summary>
        /// <param name="viewportWidth">
        /// Current viewport width in pixels, used to convert the raw mouse X pixel
        /// coordinate into a [0, 1] normalised value.
        /// </param>
        public void Update(int viewportWidth)
        {
            // Rotate current → previous before polling new state.
            _prevKeys  = _currKeys;
            _prevMouse = _currMouse;
            _currKeys  = Keyboard.GetState();
            _currMouse = Mouse.GetState();

            // ── Paddle position ───────────────────────────────────────────────
            // Mouse position normalised to [0, 1] across the viewport width.
            float mouseNorm = MathHelper.Clamp(_currMouse.X / (float)viewportWidth, 0f, 1f);

            // Accumulate a directional nudge from held arrow/WASD keys.
            float keyboardDelta = 0f;
            if (_currKeys.IsKeyDown(Keys.Left)  || _currKeys.IsKeyDown(Keys.A)) keyboardDelta -= 1f;
            if (_currKeys.IsKeyDown(Keys.Right) || _currKeys.IsKeyDown(Keys.D)) keyboardDelta += 1f;

            // Mouse movement takes priority over keyboard so the two inputs don't fight.
            // When the mouse is stationary, keyboard nudges the target incrementally.
            if (_currMouse.X != _prevMouse.X)
                PaddleTargetNormX = mouseNorm;
            else if (keyboardDelta != 0f)
                PaddleTargetNormX = MathHelper.Clamp(PaddleTargetNormX + keyboardDelta * 0.02f, 0f, 1f);

            // ── Action button (launch ball / confirm) ──────────────────────────
            // Detect the leading edge (press) only — not held state.
            bool mouseClick = _currMouse.LeftButton == ButtonState.Pressed &&
                              _prevMouse.LeftButton == ButtonState.Released;
            bool keyPress   = _currKeys.IsKeyDown(Keys.Space) && !_prevKeys.IsKeyDown(Keys.Space);
            ActionPressed   = mouseClick || keyPress;

            // ── Pause / back ───────────────────────────────────────────────────
            // Leading-edge detection so one tap doesn't fire multiple times.
            PausePressed = _currKeys.IsKeyDown(Keys.Escape) && !_prevKeys.IsKeyDown(Keys.Escape);
        }

        /// <summary>
        /// Android / touch-screen bridge.  Call this instead of <see cref="Update"/>
        /// when running on a touch device.  The MonoGame touch API feeds normalised
        /// finger X and a tap flag, both of which map directly onto the same contract
        /// that <see cref="Update"/> produces for desktop.
        /// </summary>
        /// <param name="normalizedX">
        /// Horizontal touch position normalised to [0, 1] across the screen width.
        /// </param>
        /// <param name="tap">
        /// <c>true</c> on the frame the player lifts their finger (equivalent to a
        /// left-click release → press transition).
        /// </param>
        public void SetTouchInput(float normalizedX, bool tap)
        {
            PaddleTargetNormX = MathHelper.Clamp(normalizedX, 0f, 1f);
            ActionPressed     = tap;
            PausePressed      = false;  // Android back button handled separately via the Activity
        }
    }
}
