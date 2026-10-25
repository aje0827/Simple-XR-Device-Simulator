// EditorTools-Unity editor controls: https://github.com/aje0827/EditorTools-Unity
// Copied into a project? Change the original in this repo, not the copy.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace XREditorControls
{
    /// <summary>
    /// The one place the editor controls read the mouse and keyboard (Unity's Input System, which the
    /// XR Interaction Toolkit needs anyway). While a script drives <see cref="EditorScriptedPointer"/>,
    /// its values are read instead of the real mouse.
    /// </summary>
    public static class EditorControlsInput
    {
        static Mouse Mouse => Mouse.current;
        static Keyboard Keys => Keyboard.current;
        static bool Scripted => EditorScriptedPointer.Active;

        public static Vector2 MousePosition
        {
            get
            {
                if (Scripted)
                    return EditorScriptedPointer.Position;
                // While a held object turns, the cursor is locked and the pointer stays where the turn started.
                if (PinnedPosition.HasValue)
                    return PinnedPosition.Value;
                // While looking, the pointer stays in the middle of the view. The real cursor does not
                // lock until the first click, and that click was jumping the laser off the button.
                if (EditorMouseRotationTester.IsLooking)
                    return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                return Mouse != null ? Mouse.position.ReadValue() : Vector2.zero;
            }
        }

        /// <summary>When set, the pointer reads this point instead of the cursor.</summary>
        public static Vector2? PinnedPosition { get; set; }

        /// <summary>Moves the cursor to a point in the Game view, or the scripted pointer while a script drives it.</summary>
        public static void WarpMouse(Vector2 position)
        {
            if (Scripted)
                EditorScriptedPointer.Position = position;
            else
                Mouse?.WarpCursorPosition(position);
        }

        public static bool LeftPressedThisFrame => Scripted ? EditorScriptedPointer.LeftPressed : Pressed(Mouse?.leftButton);
        public static bool LeftReleasedThisFrame => Scripted ? EditorScriptedPointer.LeftReleased : Released(Mouse?.leftButton);
        public static bool LeftHeld => Scripted ? EditorScriptedPointer.LeftHeld : Held(Mouse?.leftButton);

        public static bool RightPressedThisFrame => Scripted ? EditorScriptedPointer.RightPressed : Pressed(Mouse?.rightButton);
        public static bool RightReleasedThisFrame => Scripted ? EditorScriptedPointer.RightReleased : Released(Mouse?.rightButton);
        public static bool RightHeld => Scripted ? EditorScriptedPointer.RightHeld : Held(Mouse?.rightButton);

        // Command (either side) stands in for the controller's grip.
        public static bool CommandPressedThisFrame => Scripted ? EditorScriptedPointer.CommandPressed : Pressed(Keys?.leftCommandKey) || Pressed(Keys?.rightCommandKey);
        public static bool CommandReleasedThisFrame => Scripted ? EditorScriptedPointer.CommandReleased : Released(Keys?.leftCommandKey) || Released(Keys?.rightCommandKey);
        public static bool CommandHeld => Scripted ? EditorScriptedPointer.CommandHeld : Held(Keys?.leftCommandKey) || Held(Keys?.rightCommandKey);

        // Control, added to the grip, turns the held object.
        public static bool ControlHeld => Scripted ? EditorScriptedPointer.ControlHeld : Held(Keys?.leftCtrlKey) || Held(Keys?.rightCtrlKey);

        // Arrow keys stand in for the controller's thumbstick (left and right snap turn).
        public static bool ArrowPressedThisFrame(KeyCode key) => Pressed(Arrow(key));
        public static bool ArrowReleasedThisFrame(KeyCode key) => Released(Arrow(key));
        public static bool ArrowHeld(KeyCode key) => Held(Arrow(key));

        /// <summary>Mouse movement this frame, in the same units as the old "Mouse X" and "Mouse Y" axes (pixels × 0.1).</summary>
        public static Vector2 MouseDelta
        {
            get
            {
                if (Scripted)
                    return EditorScriptedPointer.Delta;
                return Mouse != null ? Mouse.delta.ReadValue() * 0.1f : Vector2.zero;
            }
        }

        /// <summary>Wheel or trackpad scroll this frame. One notch is about 1. Positive is up.</summary>
        public static float ScrollNotches
        {
            get
            {
                if (Scripted)
                    return EditorScriptedPointer.Scroll;
                if (Mouse == null)
                    return 0f;
                float y = Mouse.scroll.ReadValue().y;
                // Some mice report 120 for one notch.
                if (Mathf.Abs(y) >= 10f)
                    y /= 120f;
                return y;
            }
        }

        public static bool KeyHeld(KeyCode key)
        {
            if (Keys == null)
                return false;
            switch (key)
            {
                case KeyCode.LeftAlt: return Keys.leftAltKey.isPressed;
                case KeyCode.RightAlt: return Keys.rightAltKey.isPressed;
                case KeyCode.LeftShift: return Keys.leftShiftKey.isPressed;
                case KeyCode.RightShift: return Keys.rightShiftKey.isPressed;
                case KeyCode.LeftControl: return Keys.leftCtrlKey.isPressed;
                case KeyCode.LeftCommand: return Keys.leftCommandKey.isPressed;
                case KeyCode.Space: return Keys.spaceKey.isPressed;
                default: return false;
            }
        }

        static KeyControl Arrow(KeyCode key)
        {
            if (Keys == null)
                return null;
            switch (key)
            {
                case KeyCode.UpArrow: return Keys.upArrowKey;
                case KeyCode.DownArrow: return Keys.downArrowKey;
                case KeyCode.LeftArrow: return Keys.leftArrowKey;
                case KeyCode.RightArrow: return Keys.rightArrowKey;
                default: return null;
            }
        }

        static bool Pressed(ButtonControl button) => button != null && button.wasPressedThisFrame;
        static bool Released(ButtonControl button) => button != null && button.wasReleasedThisFrame;
        static bool Held(ButtonControl button) => button != null && button.isPressed;
    }
}
