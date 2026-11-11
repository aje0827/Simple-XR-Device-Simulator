// Simple XR Device Simulator: https://github.com/aje0827/Simple-XR-Device-Simulator
// Copied into a project? Change the original in this repo, not the copy.
using UnityEngine;

namespace SimpleXRDeviceSimulator
{
    /// <summary>
    /// One frame of the editor pointer, set by a script (for example an automated UI check).
    /// Inactive during normal Play, so the real mouse is what the controls read.
    /// </summary>
    public static class EditorScriptedPointer
    {
        public static bool Active;
        public static Vector2 Position;
        public static Vector2 Delta;
        public static float Scroll;
        public static bool LeftHeld;
        public static bool LeftPressed;
        public static bool LeftReleased;
        public static bool RightHeld;
        public static bool RightPressed;
        public static bool RightReleased;
        public static bool CommandHeld;
        public static bool ControlHeld;
        public static bool CommandPressed;
        public static bool CommandReleased;
        public static bool PrimaryHeld;
        public static bool SecondaryHeld;
        public static bool SwitchHandPressed;

        public static void Clear()
        {
            Active = false;
            Delta = Vector2.zero;
            Scroll = 0f;
            LeftHeld = false;
            LeftPressed = false;
            LeftReleased = false;
            RightHeld = false;
            RightPressed = false;
            RightReleased = false;
            CommandHeld = false;
            ControlHeld = false;
            CommandPressed = false;
            CommandReleased = false;
            PrimaryHeld = false;
            SecondaryHeld = false;
            SwitchHandPressed = false;
        }
    }
}
