// EditorTools-Unity editor controls: https://github.com/aje0827/EditorTools-Unity
// Copied into a project? Change the original in this repo, not the copy.
using UnityEngine;

namespace XREditorControls
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
        public static bool CommandPressed;
        public static bool CommandReleased;

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
            CommandPressed = false;
            CommandReleased = false;
        }
    }
}
