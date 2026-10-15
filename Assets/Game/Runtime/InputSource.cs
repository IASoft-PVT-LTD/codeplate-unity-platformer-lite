using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime
{
    /// <summary>
    /// Turns the Input System actions (keyboard, gamepad) and the on-screen touch buttons into
    /// one InputFrame bitmask per tick. The bindings live in
    /// Assets/Resources/Content/controls.inputactions (JSON: edit it to change keys).
    /// The simulation only ever sees the bitmask, so replays and bots use the same path.
    /// </summary>
    public sealed class InputSource
    {
        public readonly InputActionAsset Actions;
        readonly InputAction _move, _jump, _dash, _pause, _back;

        /// <summary>Bits held by the touch buttons (TouchControls sets them).</summary>
        public int TouchBits;
        /// <summary>Set by TouchControls' pause button; read and cleared by PausePressed.</summary>
        public bool TouchPause;

        public InputSource()
        {
            Actions = Object.Instantiate(Resources.Load<InputActionAsset>("Content/controls"));
            var play = Actions.FindActionMap("Gameplay", true);
            _move = play.FindAction("Move", true);
            _jump = play.FindAction("Jump", true);
            _dash = play.FindAction("Dash", true);
            _pause = play.FindAction("Pause", true);
            _back = Actions.FindActionMap("Menus", true).FindAction("Back", true);
            Actions.Enable();
        }

        /// <summary>This tick's input as InputFrame bits.</summary>
        public int Read()
        {
            int bits = TouchBits;
            var m = _move.ReadValue<Vector2>();
            if (m.x < -0.4f) bits |= InputFrame.Left;
            if (m.x > 0.4f) bits |= InputFrame.Right;
            if (m.y > 0.5f) bits |= InputFrame.Up;
            if (m.y < -0.5f) bits |= InputFrame.Down;
            if (_jump.IsPressed()) bits |= InputFrame.Jump;
            if (_dash.IsPressed()) bits |= InputFrame.Dash;
            return bits;
        }

        /// <summary>True once when pause was pressed (Esc, P, gamepad Start, or the touch button).</summary>
        public bool PausePressed()
        {
            bool touch = TouchPause;
            TouchPause = false;
            return _pause.WasPressedThisFrame() || touch;
        }

        /// <summary>True once when "back" was pressed in a menu (Esc, gamepad B/East).</summary>
        public bool BackPressed() => _back.WasPressedThisFrame();

        /// <summary>A short "key or button" label for a gameplay action, for menus ("Space", "X").</summary>
        public string Label(string action)
        {
            var a = Actions.FindAction(action);
            return a == null ? action : a.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard"));
        }
    }
}
