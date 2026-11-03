// Simple XR Device Simulator: https://github.com/aje0827/Simple-XR-Device-Simulator
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace XREditorControls
{
    /// <summary>
    /// The mouse as a controller pointer: the ray from the camera through the cursor, what it hits, and the
    /// interactors that act for the controller. The controls make their own invisible ray interactor, steer it
    /// with the mouse and press its trigger, so the toolkit does the hovering, pressing and grabbing.
    /// </summary>
    internal sealed class MousePointer
    {
        public Camera Camera { get; }

        /// <summary>Where the last <see cref="Raycast"/> landed. A click on a teleport floor lands there.</summary>
        public Vector3 LastHitPoint { get; private set; }

        XRRayInteractor ownMouseInteractor;
        XRRayInteractor ownArcInteractor;

        readonly RaycastHit[] hits = new RaycastHit[32];

        // Panels a controller ray can press. Panels that switch on later are picked up on the next refresh.
        const float PanelRefreshSeconds = 0.5f;
        TrackedDeviceGraphicRaycaster[] panels = new TrackedDeviceGraphicRaycaster[0];
        float nextPanelRefresh;

        /// <summary>Where the pointer meets a panel a controller ray can press, when <see cref="OverUi"/>.</summary>
        public Vector3 UiPoint { get; private set; }

        /// <summary>True when the last <see cref="Aim"/> landed on a panel a controller ray can press.</summary>
        public bool OverUi { get; private set; }

        public MousePointer(Camera camera)
        {
            Camera = camera;
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
                ownMouseInteractor = new GameObject("Simple XR Device Simulator Pointer").AddComponent<XRRayInteractor>();
                ownMouseInteractor.useForceGrab = false; // grabbed things stay where they are, like a far grab
                // The left button is this pointer's trigger, so UI is pressed the way a controller presses it:
                // through the scene's XR UI Input Module. A scene the headset can't press can't be pressed here either.
                ownMouseInteractor.enableUIInteraction = true;
                UseManualInput(ownMouseInteractor.activateInput);
                UseManualInput(ownMouseInteractor.uiPressInput);
                // The trigger works on something you're only pointing at, as it does on the Starter Assets rig.
                // Overwritten below by the rig's own setting when there is one.
                ownMouseInteractor.allowHoveredActivate = true;
                // Reach what the controller's pointer reaches, so teleport spots don't light up under the mouse.
                // Also take its trigger rule, so a click activates exactly what the headset's trigger activates.
                foreach (XRBaseInteractor interactor in Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (interactor == ownMouseInteractor || interactor is XRSocketInteractor || IsTeleportInteractor(interactor))
                        continue;
                    if (interactor.GetType().Name == "NearFarInteractor" || (interactor is XRRayInteractor && !(interactor is XRGazeInteractor)))
                    {
                        ownMouseInteractor.interactionLayers = interactor.interactionLayers;
                        if (interactor is XRRayInteractor ray)
                            ownMouseInteractor.raycastMask = ray.raycastMask;
                        if (interactor is XRBaseInputInteractor input)
                            ownMouseInteractor.allowHoveredActivate = input.allowHoveredActivate;
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
                    ownArcInteractor = new GameObject("Simple XR Device Simulator Teleport Arc").AddComponent<XRRayInteractor>();
                    ownArcInteractor.raycastMask = 0;
                }
                return ownArcInteractor;
            }
        }

        public bool IsOwnMouseInteractor(XRBaseInteractor interactor) => interactor != null && interactor == ownMouseInteractor;

        /// <summary>Pulls or lets go of the trigger: the toolkit reads it next frame, as it reads a controller's.</summary>
        public void SetTrigger(bool down)
        {
            float value = down ? 1f : 0f;
            MouseInteractor.activateInput.QueueManualState(down, value);
            MouseInteractor.uiPressInput.QueueManualState(down, value);
        }

        /// <summary>True while the pointer hovers something the toolkit lets it act on, or a panel it can press.</summary>
        public bool HoversSomething => OverUi || (ownMouseInteractor != null && ownMouseInteractor.interactablesHovered.Count > 0);

        static void UseManualInput(XRInputButtonReader input)
        {
            input.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
        }

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
            bool hitSolid = Raycast(ray, out RaycastHit hit);
            Vector3 uiPoint = Vector3.zero;
            OverUi = HoldDistance <= 0f && RaycastUi(ray, hitSolid ? hit.distance : float.MaxValue, out uiPoint);
            if (OverUi)
                UiPoint = uiPoint;

            if (HoldDistance > 0f)
                target = ray.GetPoint(HoldDistance);
            else if (OverUi)
                target = uiPoint;
            else if (hitSolid)
                target = hit.point;
            else
                target = ray.GetPoint(10f);

            Vector3 hand = Camera.transform.TransformPoint(HandOffset);
            Vector3 direction = target - hand;
            if (direction.sqrMagnitude < 0.0001f)
                direction = ray.direction;
            MouseInteractor.transform.SetPositionAndRotation(hand, Quaternion.LookRotation(direction));
        }

        // The nearest UI under the cursor on a panel a controller ray can press, closer than maxDistance. The
        // pointer aims there, so its ray from the hand lands on the same spot the cursor is on.
        bool RaycastUi(Ray ray, float maxDistance, out Vector3 point)
        {
            point = Vector3.zero;
            if (Time.unscaledTime >= nextPanelRefresh)
            {
                panels = Object.FindObjectsByType<TrackedDeviceGraphicRaycaster>(FindObjectsSortMode.None);
                nextPanelRefresh = Time.unscaledTime + PanelRefreshSeconds;
            }

            Vector2 screen = EditorControlsInput.MousePosition;
            float best = maxDistance;
            bool found = false;
            foreach (TrackedDeviceGraphicRaycaster panel in panels)
            {
                if (panel == null || !panel.isActiveAndEnabled)
                    continue;
                Canvas canvas = panel.GetComponent<Canvas>();
                if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                    continue;
                var plane = new Plane(canvas.transform.forward, canvas.transform.position);
                if (!plane.Raycast(ray, out float distance) || distance >= best || !OnGraphic(canvas, screen))
                    continue;
                best = distance;
                point = ray.GetPoint(distance);
                found = true;
            }
            return found;
        }

        bool OnGraphic(Canvas canvas, Vector2 screen)
        {
            IList<Graphic> graphics = GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas);
            for (int i = 0; i < graphics.Count; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic.depth != -1 && graphic.raycastTarget && !graphic.canvasRenderer.cull
                    && RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, screen, Camera))
                    return true;
            }
            return false;
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
