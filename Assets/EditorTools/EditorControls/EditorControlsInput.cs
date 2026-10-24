// EditorTools-Unity editor controls: https://github.com/aje0827/EditorTools-Unity
// Copied into a project? Change the original in this repo, not the copy.
using UnityEngine;

/// Reads mouse and keyboard for the editor controls in any project.
/// Uses the new Input System when the project has it turned on,
/// otherwise the old Input Manager. Nothing else in the controls touches input directly.
public static class EditorControlsInput
{
    public static Vector2 MousePosition
    {
        get
        {
#if UNITY_EDITOR
            if (EditorScriptedPointer.Active)
                return EditorScriptedPointer.Position;
#endif
            // While looking, the pointer stays in the middle of the view. The real cursor
            // does not lock until the first click, and that click was jumping the laser off the button.
            if (EditorMouseRotationTester.IsLooking)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null ? m.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
    }

    public static bool LeftPressedThisFrame
    {
        get
        {
#if UNITY_EDITOR
            if (EditorScriptedPointer.Active)
                return EditorScriptedPointer.LeftPressed;
#endif
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
    }

    public static bool LeftReleasedThisFrame
    {
        get
        {
#if UNITY_EDITOR
            if (EditorScriptedPointer.Active)
                return EditorScriptedPointer.LeftReleased;
#endif
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(0);
#endif
        }
    }

    public static bool LeftHeld
    {
        get
        {
#if UNITY_EDITOR
            if (EditorScriptedPointer.Active)
                return EditorScriptedPointer.LeftHeld;
#endif
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.isPressed;
#else
            return Input.GetMouseButton(0);
#endif
        }
    }

    public static bool RightHeld
    {
        get
        {
#if UNITY_EDITOR
            if (EditorScriptedPointer.Active)
                return EditorScriptedPointer.RightHeld;
#endif
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.rightButton.isPressed;
#else
            return Input.GetMouseButton(1);
#endif
        }
    }

    public static bool RightReleasedThisFrame
    {
        get
        {
#if UNITY_EDITOR
            if (EditorScriptedPointer.Active)
                return EditorScriptedPointer.RightReleased;
#endif
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.rightButton.wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(1);
#endif
        }
    }

    public static bool RightPressedThisFrame
    {
        get
        {
#if UNITY_EDITOR
            if (EditorScriptedPointer.Active)
                return EditorScriptedPointer.RightPressed;
#endif
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.rightButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(1);
#endif
        }
    }

    // Command (either side) stands in for the controller's grip.
    public static bool CommandHeld
    {
        get
        {
#if UNITY_EDITOR
            if (EditorScriptedPointer.Active)
                return EditorScriptedPointer.CommandHeld;
#endif
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
#endif
        }
    }

    public static bool CommandPressedThisFrame
    {
        get
        {
#if UNITY_EDITOR
            if (EditorScriptedPointer.Active)
                return EditorScriptedPointer.CommandPressed;
#endif
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.leftCommandKey.wasPressedThisFrame || kb.rightCommandKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.LeftCommand) || Input.GetKeyDown(KeyCode.RightCommand);
#endif
        }
    }

    public static bool CommandReleasedThisFrame
    {
        get
        {
#if UNITY_EDITOR
            if (EditorScriptedPointer.Active)
                return EditorScriptedPointer.CommandReleased;
#endif
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.leftCommandKey.wasReleasedThisFrame || kb.rightCommandKey.wasReleasedThisFrame);
#else
            return Input.GetKeyUp(KeyCode.LeftCommand) || Input.GetKeyUp(KeyCode.RightCommand);
#endif
        }
    }

    // Arrow keys stand in for the controller's thumbstick (left and right snap turn).
#if ENABLE_INPUT_SYSTEM
    static UnityEngine.InputSystem.Controls.KeyControl Arrow(KeyCode key)
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return null;
        switch (key)
        {
            case KeyCode.UpArrow: return kb.upArrowKey;
            case KeyCode.DownArrow: return kb.downArrowKey;
            case KeyCode.LeftArrow: return kb.leftArrowKey;
            case KeyCode.RightArrow: return kb.rightArrowKey;
            default: return null;
        }
    }
#endif

    public static bool ArrowHeld(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Arrow(key);
        return k != null && k.isPressed;
#else
        return Input.GetKey(key);
#endif
    }

    public static bool ArrowPressedThisFrame(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Arrow(key);
        return k != null && k.wasPressedThisFrame;
#else
        return Input.GetKeyDown(key);
#endif
    }

    public static bool ArrowReleasedThisFrame(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Arrow(key);
        return k != null && k.wasReleasedThisFrame;
#else
        return Input.GetKeyUp(key);
#endif
    }

    /// Mouse movement this frame, in the same units as the old "Mouse X"/"Mouse Y" axes.
    public static Vector2 MouseDelta
    {
        get
        {
#if UNITY_EDITOR
            if (EditorScriptedPointer.Active)
                return EditorScriptedPointer.Delta;
#endif
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null ? m.delta.ReadValue() * 0.1f : Vector2.zero;
#else
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
        }
    }

    /// Wheel or trackpad scroll this frame. One notch is about 1. Positive is up.
    public static float ScrollNotches
    {
        get
        {
#if UNITY_EDITOR
            if (EditorScriptedPointer.Active)
                return EditorScriptedPointer.Scroll;
#endif
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m == null) return 0f;
            float y = m.scroll.ReadValue().y;
#else
            // The old axis reports about 0.1 for one wheel notch.
            float y = Input.GetAxis("Mouse ScrollWheel") * 10f;
#endif
            // Some mice report 120 for one notch.
            if (Mathf.Abs(y) >= 10f)
                y /= 120f;
            return y;
        }
    }

    public static bool KeyHeld(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        switch (key)
        {
            case KeyCode.LeftAlt: return kb.leftAltKey.isPressed;
            case KeyCode.RightAlt: return kb.rightAltKey.isPressed;
            case KeyCode.LeftShift: return kb.leftShiftKey.isPressed;
            case KeyCode.RightShift: return kb.rightShiftKey.isPressed;
            case KeyCode.LeftControl: return kb.leftCtrlKey.isPressed;
            case KeyCode.LeftCommand: return kb.leftCommandKey.isPressed;
            case KeyCode.Space: return kb.spaceKey.isPressed;
            default: return false;
        }
#else
        return Input.GetKey(key);
#endif
    }
}
