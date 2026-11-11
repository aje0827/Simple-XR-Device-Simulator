// Simple XR Device Simulator: https://github.com/aje0827/Simple-XR-Device-Simulator
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SimpleXRDeviceSimulator
{
    /// <summary>
    /// One real mouse click at a screen point, fed into the Input System as two updates (button down, then up),
    /// so a script can press UI exactly as a person would. Used by automated checks.
    /// </summary>
    internal static class ScriptedMouseClick
    {
        static int step;
        static Vector2 position;
        static double lastTime;
        static bool hooked;

        public static void Queue(Vector2 screenPosition)
        {
            position = screenPosition;
            step = 1;
            lastTime = 0;
        }

        public static void Hook()
        {
            if (hooked)
                return;
            InputSystem.onBeforeUpdate += Drive;
            hooked = true;
        }

        public static void Unhook()
        {
            if (!hooked)
                return;
            InputSystem.onBeforeUpdate -= Drive;
            hooked = false;
        }

        static void Drive()
        {
            if (step == 0 || InputState.currentUpdateType != InputUpdateType.Dynamic)
                return;
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            double now = InputState.currentTime;
            // An event stamped ahead of the input clock is dropped, so wait until the clock catches up.
            if (lastTime >= now - 0.0002)
                return;
            lastTime = now - 0.0001;

            var state = new MouseState { position = position, buttons = (ushort)(step == 1 ? 1 : 0) };
            InputSystem.QueueStateEvent(mouse, state, lastTime);
            step = step == 1 ? 2 : 0;
        }
    }
}
#endif
