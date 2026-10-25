// EditorTools-Unity editor controls: https://github.com/aje0827/EditorTools-Unity
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;

namespace XREditorControls
{
    /// <summary>
    /// The mouse as a controller pointer: the ray from the camera through the cursor, what it hits, and the
    /// interactors that act for the controller. When the rig's controllers are off (no headset), the
    /// controls make their own invisible ray interactor and steer it with the mouse.
    /// </summary>
    internal sealed class MousePointer
    {
        public Camera Camera { get; }

        /// <summary>Where the last <see cref="Raycast"/> landed. A click on a teleport floor lands there.</summary>
        public Vector3 LastHitPoint { get; private set; }

        public XRBaseInteractor ClickInteractor { get; private set; }
        public XRInteractionManager Manager { get; private set; }

        XRRayInteractor ownMouseInteractor;
        XRRayInteractor ownArcInteractor;

        readonly RaycastHit[] hits = new RaycastHit[32];

        public MousePointer(Camera camera, XRBaseInteractor clickInteractor)
        {
            Camera = camera;
            ClickInteractor = clickInteractor != null ? clickInteractor : FindInteractor(false);
            if (ClickInteractor != null)
                Manager = ClickInteractor.interactionManager;
            if (Manager == null)
                Manager = Object.FindAnyObjectByType<XRInteractionManager>();
        }

        public Ray Ray => Camera.ScreenPointToRay(EditorControlsInput.MousePosition);

        /// <summary>
        /// Nearest solid hit, or a trigger that belongs to an XR interactable (a teleport spot).
        /// Plain trigger zones are skipped so they don't block clicks.
        /// </summary>
        public bool Raycast(Ray ray, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(ray, hits, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            float bestDistance = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider.isTrigger && hit.collider.GetComponentInParent<XRBaseInteractable>() == null)
                    continue;
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = hit;
                    found = true;
                }
            }
            if (found)
                LastHitPoint = best.point;
            return found;
        }

        public static bool IsClickable(GameObject target)
        {
            return target.GetComponentInParent<XRBaseInteractable>() != null || target.GetComponent<IClickable>() != null;
        }

        /// <summary>Controllers can be switched off after Play starts (no headset), so this is checked on every click.</summary>
        public bool RefreshClickInteractor()
        {
            if (ClickInteractor == null || !ClickInteractor.isActiveAndEnabled)
            {
                ClickInteractor = FindInteractor(false);
                Manager = null;
            }
            if (Manager == null && ClickInteractor != null)
                Manager = ClickInteractor.interactionManager;
            if (Manager == null)
                Manager = Object.FindAnyObjectByType<XRInteractionManager>();
            return ClickInteractor != null && Manager != null;
        }

        /// <summary>Any active controller interactor (ray or near-far). wantTeleport picks the teleport one.</summary>
        public XRBaseInteractor FindInteractor(bool wantTeleport)
        {
            foreach (XRBaseInteractor interactor in Object.FindObjectsByType<XRBaseInteractor>(FindObjectsSortMode.None))
            {
                if (interactor == null || !interactor.isActiveAndEnabled || interactor is XRSocketInteractor)
                    continue;
                if (IsTeleportInteractor(interactor) == wantTeleport)
                    return interactor;
            }
            // No live controller pointer (the rig turns its controllers off with no headset connected).
            return MouseInteractor;
        }

        /// <summary>The controls' own invisible pointer, steered by the mouse.</summary>
        public XRRayInteractor MouseInteractor
        {
            get
            {
                if (ownMouseInteractor != null)
                    return ownMouseInteractor;
                // Not parented, so no scale above it can stretch what it grabs.
                ownMouseInteractor = new GameObject("~EditorMouseInteractor").AddComponent<XRRayInteractor>();
                ownMouseInteractor.useForceGrab = false; // grabbed things stay where they are, like a far grab
                // Grabbing only. UI clicks go through the UI clicker; a 3D select here could block later panel clicks.
                ownMouseInteractor.enableUIInteraction = false;
                // Reach what the controller's pointer reaches, so teleport spots don't light up under the mouse.
                foreach (XRBaseInteractor interactor in Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (interactor == ownMouseInteractor || interactor is XRSocketInteractor || IsTeleportInteractor(interactor))
                        continue;
                    if (interactor.GetType().Name == "NearFarInteractor" || (interactor is XRRayInteractor && !(interactor is XRGazeInteractor)))
                    {
                        ownMouseInteractor.interactionLayers = interactor.interactionLayers;
                        break;
                    }
                }
                return ownMouseInteractor;
            }
        }

        /// <summary>Holds teleport spots for the arc when the rig has no live teleport ray. Points nowhere, so it never hovers.</summary>
        public XRRayInteractor ArcInteractor
        {
            get
            {
                if (ownArcInteractor == null)
                {
                    ownArcInteractor = new GameObject("~EditorArcInteractor").AddComponent<XRRayInteractor>();
                    ownArcInteractor.raycastMask = 0;
                }
                return ownArcInteractor;
            }
        }

        public bool IsOwnMouseInteractor(XRBaseInteractor interactor) => interactor != null && interactor == ownMouseInteractor;

        /// <summary>Where the "hand" sits, right, down and forward from the camera. Set from the laser settings.</summary>
        public Vector3 HandOffset { get; set; }

        /// <summary>While something is held, how far out the pointer aims, so it doesn't aim at the held object itself.</summary>
        public float HoldDistance { get; set; }

        /// <summary>
        /// Points the controls' own pointer from the hand to what the mouse is on, so things react to hover as they
        /// do with a controller. Starting at the hand, not the camera, matters for objects that fly to the hand
        /// when grabbed: from the camera they'd land in your face.
        /// </summary>
        public void Aim()
        {
            Ray ray = Ray;
            Vector3 target;
            if (HoldDistance > 0f)
                target = ray.GetPoint(HoldDistance);
            else if (Raycast(ray, out RaycastHit hit))
                target = hit.point;
            else
                target = ray.GetPoint(10f);

            Vector3 hand = Camera.transform.TransformPoint(HandOffset);
            Vector3 direction = target - hand;
            if (direction.sqrMagnitude < 0.0001f)
                direction = ray.direction;
            MouseInteractor.transform.SetPositionAndRotation(hand, Quaternion.LookRotation(direction));
        }

        public void Destroy()
        {
            if (ownMouseInteractor != null)
                Object.Destroy(ownMouseInteractor.gameObject);
            if (ownArcInteractor != null)
                Object.Destroy(ownArcInteractor.gameObject);
        }

        static bool IsTeleportInteractor(XRBaseInteractor interactor)
        {
            return interactor.name.IndexOf("Teleport", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
