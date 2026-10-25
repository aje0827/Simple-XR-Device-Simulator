// EditorTools-Unity editor controls: https://github.com/aje0827/EditorTools-Unity
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using Object = UnityEngine.Object;

namespace XREditorControls
{
    /// <summary>
    /// The thumbstick: hold the right mouse button to aim a teleport arc and let go to jump, and the left and
    /// right arrow keys snap turn. Teleports go through the rig's own Teleportation Provider.
    /// </summary>
    internal sealed class MouseLocomotion
    {
        readonly EditorMouseClickTester owner;
        readonly MousePointer pointer;

        // The teleport spot the arc rests on. With a controller, holding the stick over a spot makes the
        // teleport ray select it until you let go, and the spot plays whatever it is set up to (an arrow
        // rising, a glow). The arc selects it the same way, through the toolkit's manual select.
        GameObject arcTarget;
        XRBaseInteractor arcHolder;

        public MouseLocomotion(EditorMouseClickTester owner, MousePointer pointer)
        {
            this.owner = owner;
            this.pointer = pointer;
        }

        /// <param name="skipTeleport">True while an object is held: the right click doesn't teleport then.</param>
        public void Update(bool skipTeleport)
        {
            if (EditorControlsInput.ArrowPressedThisFrame(KeyCode.LeftArrow))
                SnapTurn(-1f);
            if (EditorControlsInput.ArrowPressedThisFrame(KeyCode.RightArrow))
                SnapTurn(1f);

            if (!EditorControlsInput.RightReleasedThisFrame || skipTeleport)
                return;

            if (arcTarget != null && arcTarget.activeInHierarchy)
            {
                TeleportTo(arcTarget.GetComponent<BaseTeleportationInteractable>());
                return;
            }

            if (TryGetTarget(pointer.Ray, out BaseTeleportationInteractable target, out _))
                TeleportTo(target);
        }

        /// <summary>
        /// Where the arc ends for this ray, and whether that is somewhere to land. Keeps hold of a spot while the
        /// mouse is still near it on the floor: the spot's arrow rises out from under the mouse, and letting go of
        /// the spot then would make the arrow bob every frame.
        /// </summary>
        public bool AimArc(Ray ray, out Vector3 end)
        {
            end = ray.origin + ray.direction * 10f;
            bool valid = TryGetTarget(ray, out BaseTeleportationInteractable target, out Vector3 landing);
            if (valid)
                end = landing;
            else if (Physics.Raycast(ray, out RaycastHit any, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                end = any.point;

            GameObject spot = valid && target is TeleportationAnchor ? target.gameObject : null;
            if (spot == null && arcTarget != null && arcTarget.activeInHierarchy
                && Physics.Raycast(ray, out RaycastHit floor, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                Vector3 offset = floor.point - arcTarget.transform.position;
                offset.y = 0f;
                if (offset.magnitude < 0.6f)
                {
                    spot = arcTarget;
                    valid = true;
                    TeleportationAnchor anchor = arcTarget.GetComponent<TeleportationAnchor>();
                    end = anchor != null && anchor.teleportAnchorTransform != null ? anchor.teleportAnchorTransform.position : arcTarget.transform.position;
                }
            }

            SetArcTarget(spot);
            return valid;
        }

        public void ClearArc()
        {
            SetArcTarget(null);
        }

        /// <summary>What a teleport ray would land on: a teleport spot (snapped to its landing point) or a teleport floor.</summary>
        public bool TryGetTarget(Ray ray, out BaseTeleportationInteractable target, out Vector3 landing)
        {
            target = null;
            landing = Vector3.zero;
            if (!pointer.Raycast(ray, out RaycastHit hit))
                return false;
            target = hit.collider.GetComponentInParent<BaseTeleportationInteractable>();
            if (target == null)
                return false;
            landing = hit.point;
            if (target is TeleportationAnchor anchor && anchor.teleportAnchorTransform != null)
                landing = anchor.teleportAnchorTransform.position;
            return true;
        }

        public bool TeleportTo(BaseTeleportationInteractable target)
        {
            TeleportationProvider provider = target.teleportationProvider != null
                ? target.teleportationProvider
                : Object.FindAnyObjectByType<TeleportationProvider>();
            if (provider == null)
            {
                Debug.LogWarning("[EditorMouseClickTester] Clicked a teleport spot but there is no Teleportation Provider in the scene.");
                return false;
            }

            var request = new TeleportRequest();
            if (target is TeleportationAnchor anchor && anchor.teleportAnchorTransform != null)
            {
                request.destinationPosition = anchor.teleportAnchorTransform.position;
                request.destinationRotation = anchor.teleportAnchorTransform.rotation;
                request.matchOrientation = anchor.matchOrientation;
            }
            else
            {
                request.destinationPosition = pointer.LastHitPoint;
                request.destinationRotation = Quaternion.identity;
                request.matchOrientation = MatchOrientation.None;
            }

            bool queued = provider.QueueTeleportRequest(request);
            owner.LogWorld("Teleport to " + target.name + (queued ? "" : " (provider refused)"));
            return queued;
        }

        void SetArcTarget(GameObject target)
        {
            if (target == arcTarget)
                return;
            if (arcHolder != null && arcHolder.isPerformingManualInteraction)
                arcHolder.EndManualInteraction();
            arcHolder = null;
            arcTarget = target;

            XRBaseInteractable spot = arcTarget != null ? arcTarget.GetComponent<XRBaseInteractable>() : null;
            if (spot == null)
                return;
            arcHolder = pointer.FindInteractor(true);
            if (pointer.IsOwnMouseInteractor(arcHolder))
                arcHolder = pointer.ArcInteractor;
            if (arcHolder.interactionManager == null)
                arcHolder.interactionManager = spot.interactionManager;
            arcHolder.StartManualInteraction((IXRSelectInteractable)spot);
        }

        // Turns the rig by the snap turn amount, around the head, the way the snap turn does.
        static void SnapTurn(float direction)
        {
            XROrigin origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
                return;
            SnapTurnProvider snap = Object.FindAnyObjectByType<SnapTurnProvider>();
            float amount = snap != null ? snap.turnAmount : 45f;
            origin.RotateAroundCameraUsingOriginUp(direction * amount);
        }
    }
}
#endif
