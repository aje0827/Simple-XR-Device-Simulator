// EditorTools-Unity editor controls: https://github.com/aje0827/EditorTools-Unity
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace XREditorControls
{
    /// <summary>
    /// The trigger on a 3D object: a left click selects the XR interactable under the pointer (and activates it,
    /// as the trigger does) until the button is let go. A teleport spot teleports, and an object with
    /// <see cref="IClickable"/> gets its OnClick.
    /// </summary>
    internal sealed class MouseWorldClick
    {
        readonly EditorMouseClickTester owner;
        readonly MousePointer pointer;
        readonly MouseLocomotion locomotion;

        IXRSelectInteractable selected;
        XRBaseInteractor selectedBy;

        public MouseWorldClick(EditorMouseClickTester owner, MousePointer pointer, MouseLocomotion locomotion)
        {
            this.owner = owner;
            this.pointer = pointer;
            this.locomotion = locomotion;
        }

        public void Click()
        {
            if (!pointer.Raycast(pointer.Ray, out RaycastHit hit))
                return;

            GameObject target = hit.collider.gameObject;
            bool clickable = MousePointer.IsClickable(target);
            owner.LogWorld("Hit world object: " + target.name + (clickable ? " (clickable)" : ""));
            if (!clickable)
                return;

            if (TrySelect(target))
                return;

            IClickable clickHandler = target.GetComponent<IClickable>();
            if (clickHandler != null)
                clickHandler.OnClick();
        }

        /// <summary>Lets go of what the click selected, as releasing the trigger does.</summary>
        public void Release()
        {
            if (selected == null || pointer.ClickInteractor == null || pointer.Manager == null)
            {
                selected = null;
                return;
            }

            XRBaseInteractor who = selectedBy != null ? selectedBy : pointer.ClickInteractor;
            if (selected is IXRActivateInteractable activatable && who is IXRActivateInteractor activator)
                activatable.OnDeactivated(new DeactivateEventArgs { interactorObject = activator, interactableObject = activatable });
            if (who.interactablesSelected.Contains(selected))
                pointer.Manager.SelectExit((IXRSelectInteractor)who, selected);
            selected = null;
            selectedBy = null;
        }

        bool TrySelect(GameObject target)
        {
            XRBaseInteractable interactable = target.GetComponentInParent<XRBaseInteractable>();
            if (interactable == null)
                return false;

            // A mouse has no controller ray for a teleport spot to check, so ask the rig to move there directly.
            if (interactable is BaseTeleportationInteractable teleport)
                return locomotion.TeleportTo(teleport);

            // The trigger doesn't pick things up; the grip (Command) does.
            if (interactable is XRGrabInteractable)
                return false;

            if (!pointer.RefreshClickInteractor())
            {
                Debug.LogWarning("[EditorMouseClickTester] Hit an XR interactable but there is no Ray Interactor or Interaction Manager in the scene.");
                return false;
            }

            Release();
            selectedBy = pointer.ClickInteractor;
            pointer.Manager.SelectEnter((IXRSelectInteractor)selectedBy, interactable);
            if (interactable is IXRActivateInteractable activatable && selectedBy is IXRActivateInteractor activator)
                activatable.OnActivated(new ActivateEventArgs { interactorObject = activator, interactableObject = activatable });
            selected = interactable;

            owner.LogWorld("XR SelectEnter on " + interactable.gameObject.name);
            return true;
        }
    }
}
#endif
