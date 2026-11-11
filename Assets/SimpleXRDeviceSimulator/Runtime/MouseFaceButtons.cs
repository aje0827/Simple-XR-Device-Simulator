// Simple XR Device Simulator: https://github.com/aje0827/Simple-XR-Device-Simulator
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR && ENABLE_VR
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace SimpleXRDeviceSimulator
{
    /// <summary>
    /// The right controller's A and B buttons: hold 1 for A, 2 for B. They are pressed on a stand-in right
    /// controller in the Input System, so anything bound to <c>&lt;XRController&gt;{RightHand}/primaryButton</c> or
    /// <c>secondaryButton</c> works unchanged. The stand-in reports itself as not tracked, so the rig doesn't switch
    /// its controllers on. With a headset connected, its real controller is used and nothing is added.
    /// </summary>
    internal sealed class MouseFaceButtons
    {
        XRSimulatedController controller;
        XRSimulatedControllerState state;
        bool checkedHeadset;

        public void Update()
        {
            bool a = EditorControlsInput.PrimaryButtonHeld && !TypingInTextField();
            bool b = EditorControlsInput.SecondaryButtonHeld && !TypingInTextField();
            if (controller == null)
            {
                if (!a && !b)
                    return;
                if (!checkedHeadset)
                {
                    checkedHeadset = true;
                    if (UnityEngine.XR.XRSettings.isDeviceActive)
                        return;
                    Add();
                }
                if (controller == null)
                    return;
            }

            bool wasA = state.HasButton(ControllerButton.PrimaryButton);
            bool wasB = state.HasButton(ControllerButton.SecondaryButton);
            if (a == wasA && b == wasB)
                return;
            state = state.WithButton(ControllerButton.PrimaryButton, a).WithButton(ControllerButton.SecondaryButton, b);
            InputSystem.QueueStateEvent(controller, state);
        }

        public void Remove()
        {
            if (controller != null && controller.added)
                InputSystem.RemoveDevice(controller);
            controller = null;
        }

        void Add()
        {
            controller = InputSystem.AddDevice<XRSimulatedController>("Simple XR Device Simulator Right Controller");
            InputSystem.SetDeviceUsage(controller, UnityEngine.InputSystem.CommonUsages.RightHand);
            state = new XRSimulatedControllerState();
            state.Reset();
        }

        // Typing a 1 or a 2 into a text field isn't pressing a button.
        static bool TypingInTextField()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null)
                return false;
            InputField field = selected.GetComponent<InputField>();
            if (field != null)
                return field.isFocused;
            foreach (Component component in selected.GetComponents<Component>())
            {
                if (component == null || component.GetType().Name != "TMP_InputField")
                    continue;
                var focused = component.GetType().GetProperty("isFocused");
                return focused != null && (bool)focused.GetValue(component);
            }
            return false;
        }
    }
}
#endif
