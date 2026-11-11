// Simple XR Device Simulator: https://github.com/aje0827/Simple-XR-Device-Simulator
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR && ENABLE_VR
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace SimpleXRDeviceSimulator
{
    /// <summary>
    /// The controller's face buttons: hold 1 for A, 2 for B on the right hand, or X and Y on the left hand. They are
    /// pressed on a stand-in controller in the Input System, so anything bound to
    /// <c>&lt;XRController&gt;{RightHand}/primaryButton</c>, <c>{LeftHand}/primaryButton</c> or the secondary buttons
    /// works unchanged. The stand-in reports itself as not tracked, so the rig doesn't switch its controllers on.
    /// With a headset connected, its real controllers are used and nothing is added.
    /// </summary>
    internal sealed class MouseFaceButtons
    {
        readonly Hand right = new Hand("Simple XR Device Simulator Right Controller", CommonUsages.RightHand);
        readonly Hand left = new Hand("Simple XR Device Simulator Left Controller", CommonUsages.LeftHand);
        bool checkedHeadset;
        bool headset;

        public void Update(bool leftHand)
        {
            bool primary = EditorControlsInput.PrimaryButtonHeld && !EditorControlsInput.TypingInTextField();
            bool secondary = EditorControlsInput.SecondaryButtonHeld && !EditorControlsInput.TypingInTextField();
            if ((primary || secondary) && !checkedHeadset)
            {
                checkedHeadset = true;
                headset = UnityEngine.XR.XRSettings.isDeviceActive;
            }
            if (headset)
                return;

            // Switching hands lets go of whatever the other hand was pressing.
            Hand active = leftHand ? left : right;
            Hand other = leftHand ? right : left;
            other.Set(false, false);
            active.Set(primary, secondary);
        }

        public void Remove()
        {
            right.Remove();
            left.Remove();
        }

        sealed class Hand
        {
            readonly string name;
            readonly InternedString usage;
            XRSimulatedController controller;
            XRSimulatedControllerState state;

            public Hand(string name, InternedString usage)
            {
                this.name = name;
                this.usage = usage;
            }

            public void Set(bool primary, bool secondary)
            {
                if (controller == null)
                {
                    if (!primary && !secondary)
                        return;
                    controller = InputSystem.AddDevice<XRSimulatedController>(name);
                    InputSystem.SetDeviceUsage(controller, usage);
                    state = new XRSimulatedControllerState();
                    state.Reset();
                }

                bool wasPrimary = state.HasButton(ControllerButton.PrimaryButton);
                bool wasSecondary = state.HasButton(ControllerButton.SecondaryButton);
                if (primary == wasPrimary && secondary == wasSecondary)
                    return;
                state = state.WithButton(ControllerButton.PrimaryButton, primary).WithButton(ControllerButton.SecondaryButton, secondary);
                InputSystem.QueueStateEvent(controller, state);
            }

            public void Remove()
            {
                if (controller != null && controller.added)
                    InputSystem.RemoveDevice(controller);
                controller = null;
            }
        }
    }
}
#endif
