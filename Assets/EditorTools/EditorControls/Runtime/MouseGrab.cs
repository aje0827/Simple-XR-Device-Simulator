// Simple XR Device Simulator: https://github.com/aje0827/Simple-XR-Device-Simulator
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace XREditorControls
{
    /// <summary>
    /// The grip: hold Command to grab what the pointer is on. While holding, Control turns the
    /// object and the scroll wheel scales it. The grab is the toolkit's own held select, so the object behaves
    /// as it does in the controller's hand.
    /// </summary>
    internal sealed class MouseGrab
    {
        readonly EditorMouseClickTester owner;
        readonly MousePointer pointer;

        // Size when first grabbed, per object, so the scroll limits match the grab transformer's.
        readonly Dictionary<int, Vector3> scaleBasis = new Dictionary<int, Vector3>();

        bool turning;

        // The hold point turns while Control is down; it goes back to how it was on letting go.
        Vector3 attachStartPosition;
        Quaternion attachStartRotation;
        float pendingScroll;

        // The mouse moves to turn the object, so the cursor is locked while turning and the pointer stays pinned
        // where the turn started. After the turn the cursor is put back there before the pin comes off, or the
        // object would jump to wherever the cursor ended up.
        const int FramesToSettle = 3;
        Vector2 turnStartedAt;
        int aimHeldUntilFrame;
        int turnFramesToIgnore;

        public IXRSelectInteractable Held { get; private set; }

        /// <summary>True while the pointer should stay put: during a turn and the moment after it.</summary>
        public bool HoldsAim => Held != null && (turning || Time.frameCount <= aimHeldUntilFrame);

        // The cursor has had time to land back where the turn started; the pointer can follow it again.
        void ReleasePinWhenSettled()
        {
            if (!turning && EditorControlsInput.PinnedPosition.HasValue && Time.frameCount > aimHeldUntilFrame)
                EditorControlsInput.PinnedPosition = null;
        }

        public MouseGrab(EditorMouseClickTester owner, MousePointer pointer)
        {
            this.owner = owner;
            this.pointer = pointer;
        }

        public void Update()
        {
            ReleasePinWhenSettled();
            if (EditorControlsInput.CommandPressedThisFrame)
                Start();
            if (EditorControlsInput.CommandReleasedThisFrame && !heldByTrigger)
                End();

            // Control while holding: the mouse turns the object instead of moving it. A key, not a click, because a
            // trackpad's two-finger press slips into a scroll, and the scroll scales.
            if (Held != null && EditorControlsInput.ControlHeld)
            {
                if (!turning)
                    BeginTurn();
                Turn();
            }
            else if (turning)
            {
                EndTurn();
            }

            // Scroll, or a two-finger swipe, scales the held object.
            if (Held != null && !turning)
                pendingScroll += EditorControlsInput.ScrollNotches;
        }

        bool heldByTrigger;

        /// <summary>The trigger grabs, for rigs that grab with the trigger. Lets go with the trigger, not with Command.</summary>
        public void StartFromTrigger()
        {
            if (Held != null)
                return;
            Start();
            heldByTrigger = Held != null;
        }

        public void EndFromTrigger()
        {
            if (heldByTrigger)
                End();
        }

        public void End()
        {
            heldByTrigger = false;
            if (turning)
                UnlockCursor();
            turning = false;
            EditorControlsInput.PinnedPosition = null;
            pendingScroll = 0f;
            Held = null;
            pointer.HoldDistance = 0f;
            XRRayInteractor mouse = pointer.MouseInteractor;
            if (mouse.isPerformingManualInteraction)
                mouse.EndManualInteraction();
            Transform attach = mouse.attachTransform;
            if (attach != null)
                attach.SetLocalPositionAndRotation(attachStartPosition, attachStartRotation);
        }

        // Grabs what the pointer is on, through the toolkit's held select (the same as the grip).
        void Start()
        {
            End();
            XRRayInteractor mouse = pointer.MouseInteractor;
            IXRSelectInteractable target = null;
            foreach (IXRHoverInteractable hovered in mouse.interactablesHovered)
            {
                if (hovered is IXRSelectInteractable selectable && !(hovered is BaseTeleportationInteractable))
                {
                    target = selectable;
                    break;
                }
            }
            // Only what the ray hovers, as with the grip: something on a layer the controller can't reach stays put.
            if (target == null)
                return;

            RememberScaleBasis(target.transform);
            Vector3 cameraPosition = pointer.Camera.transform.position;
            pointer.HoldDistance = pointer.Raycast(pointer.Ray, out RaycastHit grabHit)
                ? grabHit.distance
                : Vector3.Distance(cameraPosition, target.transform.position);
            if (mouse.attachTransform != null)
                mouse.attachTransform.GetLocalPositionAndRotation(out attachStartPosition, out attachStartRotation);
            mouse.StartManualInteraction(target);
            Held = target;
            owner.LogWorld("Grab " + target.transform.name);
        }

        void BeginTurn()
        {
            turning = true;
            pendingScroll = 0f;
            turnStartedAt = EditorControlsInput.MousePosition;
            EditorControlsInput.PinnedPosition = turnStartedAt;
            Cursor.lockState = CursorLockMode.Locked;
            // Locking the cursor reports one big jump; that isn't the hand turning.
            turnFramesToIgnore = 2;
        }

        void EndTurn()
        {
            turning = false;
            pendingScroll = 0f;
            UnlockCursor();
            aimHeldUntilFrame = Time.frameCount + FramesToSettle;
        }

        void UnlockCursor()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.None;
            EditorControlsInput.WarpMouse(turnStartedAt);
        }

        // Turns the hold point, like turning a wrist, so the grab stays on and the object follows it the way it
        // follows a controller. Left and right spin around the vertical, up and down tilt toward you, and with
        // Shift held left and right roll it around the line you're looking along. The turn is around the middle
        // of the object, not its root, which often sits well below the middle.
        void Turn()
        {
            if (turnFramesToIgnore > 0)
            {
                turnFramesToIgnore--;
                return;
            }
            Vector2 pixels = EditorControlsInput.MouseDelta * 10f;
            if (pixels.sqrMagnitude < 0.0001f)
                return;
            Transform attach = pointer.MouseInteractor.attachTransform;
            if (attach == null)
                return;

            Transform view = pointer.Camera.transform;
            bool roll = EditorControlsInput.KeyHeld(KeyCode.LeftShift) || EditorControlsInput.KeyHeld(KeyCode.RightShift);
            Quaternion spin = roll
                ? Quaternion.AngleAxis(-pixels.x * owner.grabTurnSpeed, view.forward)
                : Quaternion.AngleAxis(-pixels.x * owner.grabTurnSpeed, Vector3.up) * Quaternion.AngleAxis(pixels.y * owner.grabTurnSpeed, view.right);

            Transform t = Held.transform;
            Vector3 middle = t.TransformPoint(Center(t));
            attach.SetPositionAndRotation(middle + spin * (attach.position - middle), spin * attach.rotation);
        }

        /// <summary>
        /// Applies the scroll after the toolkit's before-render pass (order 100) has written the grab pose,
        /// so the new size doesn't push the object toward or away from the pointer.
        /// </summary>
        [BeforeRenderOrder(200)]
        public void ApplyPendingScale()
        {
            if (Held == null || Mathf.Abs(pendingScroll) < 0.0001f)
                return;

            float notches = pendingScroll;
            pendingScroll = 0f;

            Transform t = Held.transform;
            RememberScaleBasis(t);
            float factor = Mathf.Pow(1f + Mathf.Max(0f, owner.grabScaleSpeed), notches);
            Vector3 next = t.localScale * factor;

            XRGeneralGrabTransformer transformer = t.GetComponent<XRGeneralGrabTransformer>();
            if (transformer != null && transformer.clampScaling)
            {
                scaleBasis.TryGetValue(t.GetInstanceID(), out Vector3 basis);
                Vector3 min = basis * transformer.minimumScaleRatio;
                Vector3 max = basis * transformer.maximumScaleRatio;
                next = new Vector3(
                    Mathf.Clamp(next.x, Mathf.Min(min.x, max.x), Mathf.Max(min.x, max.x)),
                    Mathf.Clamp(next.y, Mathf.Min(min.y, max.y), Mathf.Max(min.y, max.y)),
                    Mathf.Clamp(next.z, Mathf.Min(min.z, max.z), Mathf.Max(min.z, max.z)));
            }

            Vector3 localCenter = Center(t);
            Vector3 worldCenter = t.TransformPoint(localCenter);
            t.localScale = next;
            t.position += worldCenter - t.TransformPoint(localCenter);

            XRGrabInteractable grab = t.GetComponent<XRGrabInteractable>();
            if (grab == null || !grab.isSelected)
                return;

            // The hold point is a child, so a new size swings it toward or away from the pointer and the grab
            // follows. Park that point on the pointer, then let the grab learn this pose. The object stays put.
            var interactor = grab.interactorsSelecting[0];
            Transform holdPoint = grab.GetAttachTransform(interactor);
            Transform pointerPoint = interactor.GetAttachTransform(grab);
            if (holdPoint != null && holdPoint != t && pointerPoint != null)
                holdPoint.SetPositionAndRotation(pointerPoint.position, pointerPoint.rotation);

            grab.SetTargetLocalScale(t.localScale);
            grab.SetTargetPose(new Pose(t.position, t.rotation));
            if (transformer != null)
                transformer.OnGrab(grab);

            Rigidbody body = t.GetComponent<Rigidbody>();
            if (body != null)
                body.position = t.position;
        }

        void RememberScaleBasis(Transform t)
        {
            int id = t.GetInstanceID();
            if (!scaleBasis.ContainsKey(id))
                scaleBasis[id] = t.localScale;
        }

        static Vector3 Center(Transform t)
        {
            BoxCollider box = t.GetComponent<BoxCollider>();
            return box != null ? box.center : Vector3.zero;
        }
    }
}
#endif
