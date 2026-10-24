// EditorTools-Unity editor controls: https://github.com/aje0827/EditorTools-Unity
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using Object = UnityEngine.Object;

namespace XREditorControls
{
    /// <summary>
    /// The grip: hold Command to grab what the pointer is on. While holding, the right mouse button turns the
    /// object and the scroll wheel scales it. The grab is the toolkit's own held select, so the object behaves
    /// as it does in the controller's hand.
    /// </summary>
    internal sealed class MouseGrab
    {
        // A two-finger lift sends a scroll as the right click ends. Ignore it briefly,
        // or the object changes size at the end of a turn.
        const float ScrollQuietAfterTurn = 0.5f;

        readonly EditorMouseClickTester owner;
        readonly MousePointer pointer;

        // Size when first grabbed, per object, so the scroll limits match the grab transformer's.
        readonly Dictionary<int, Vector3> scaleBasis = new Dictionary<int, Vector3>();

        bool turning;
        bool rightClickWasTurn;
        float pendingScroll;
        float scrollQuietUntil;

        public IXRSelectInteractable Held { get; private set; }

        public MouseGrab(EditorMouseClickTester owner, MousePointer pointer)
        {
            this.owner = owner;
            this.pointer = pointer;
        }

        public void Update()
        {
            if (EditorControlsInput.CommandPressedThisFrame)
                Start();
            if (EditorControlsInput.CommandReleasedThisFrame)
                End();

            // Right button while holding: the mouse turns the object instead of moving it.
            if (Held != null && EditorControlsInput.RightHeld)
            {
                if (!turning)
                    BeginTurn();
                Turn();
            }
            else if (turning)
            {
                turning = false;
                scrollQuietUntil = Time.unscaledTime + ScrollQuietAfterTurn;
                pendingScroll = 0f;
                if (Held != null)
                    ResumeMove();
            }

            // Scroll, or a two-finger swipe with the right button up, scales the held object. A two-finger press is
            // the right click and the trackpad also reports it as a scroll, so it's ignored while turning and just after.
            if (Held != null && !EditorControlsInput.RightHeld && Time.unscaledTime >= scrollQuietUntil)
                pendingScroll += EditorControlsInput.ScrollNotches;
        }

        /// <summary>True once per right click that turned a held object; that release must not teleport.</summary>
        public bool TakeRightClickWasTurn()
        {
            if (!EditorControlsInput.RightReleasedThisFrame || !rightClickWasTurn)
                return false;
            rightClickWasTurn = false;
            return true;
        }

        public void End()
        {
            turning = false;
            pendingScroll = 0f;
            Held = null;
            XRRayInteractor mouse = pointer.MouseInteractor;
            if (mouse.isPerformingManualInteraction)
                mouse.EndManualInteraction();
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
            if (target == null && pointer.Raycast(pointer.Ray, out RaycastHit hit))
            {
                XRBaseInteractable found = hit.collider.GetComponentInParent<XRBaseInteractable>();
                if (found != null && !(found is BaseTeleportationInteractable))
                    target = found;
            }
            if (target == null)
                return;

            RememberScaleBasis(target.transform);
            mouse.StartManualInteraction(target);
            Held = target;
            owner.LogWorld("Grab " + target.transform.name);
        }

        // Drops the toolkit's grab so it stops writing the pose while the mouse turns the object.
        void BeginTurn()
        {
            turning = true;
            rightClickWasTurn = true;
            pendingScroll = 0f;
            XRRayInteractor mouse = pointer.MouseInteractor;
            if (mouse.isPerformingManualInteraction)
                mouse.EndManualInteraction();
        }

        // Left and right spin around the vertical, up and down tilt around the view's sideways axis.
        // Both turn around the middle of the object, not its root, which often sits well below the middle.
        void Turn()
        {
            Vector2 pixels = EditorControlsInput.MouseDelta * 10f;
            if (pixels.sqrMagnitude < 0.0001f)
                return;

            float yaw = -pixels.x * owner.grabTurnSpeed;
            float pitch = pixels.y * owner.grabTurnSpeed;
            Quaternion spin = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, pointer.Camera.transform.right);
            Transform t = Held.transform;
            Vector3 localCenter = Center(t);
            Vector3 worldCenter = t.TransformPoint(localCenter);
            t.rotation = spin * t.rotation;
            t.position += worldCenter - t.TransformPoint(localCenter);
        }

        // Right button up, Command still down: take hold again so the mouse moves the object.
        void ResumeMove()
        {
            XRRayInteractor mouse = pointer.MouseInteractor;
            if (mouse.isPerformingManualInteraction)
                mouse.EndManualInteraction();
            if (mouse.interactionManager == null)
                mouse.interactionManager = pointer.Manager != null ? pointer.Manager : Object.FindAnyObjectByType<XRInteractionManager>();
            mouse.StartManualInteraction(Held);
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
