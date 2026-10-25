// Simple XR Device Simulator: https://github.com/aje0827/Simple-XR-Device-Simulator
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace XREditorControls
{
    /// <summary>
    /// The trigger on UI panels. A press is not a click yet: it counts on release, on the same control, so
    /// sliding off a button cancels it as it would with a controller. Hover enter and exit are sent too, so
    /// buttons highlight under the mouse.
    /// </summary>
    internal sealed class MouseUiClick
    {
        // A trackpad tap while looking around is a press and release in the same instant.
        // A real click is held down briefly, then released on the same button.
        const float MinClickHoldWhileLooking = 0.1f;

        // Panels that switch on later in the scene are picked up on the next refresh.
        const float RaycasterRefreshSeconds = 0.5f;

        readonly EditorMouseClickTester owner;
        readonly EventSystem eventSystem;
        readonly Camera camera;
        readonly List<RaycastResult> results = new List<RaycastResult>();

        GraphicRaycaster[] raycasters = new GraphicRaycaster[0];
        float nextRaycasterRefresh;

        GameObject pressed;
        Vector2 pressedAt;
        float pressedTime;
        bool pressedWhileLooking;
        GameObject hovered;

        public MouseUiClick(EditorMouseClickTester owner, EventSystem eventSystem, Camera camera)
        {
            this.owner = owner;
            this.eventSystem = eventSystem;
            this.camera = camera;
        }

        /// <summary>
        /// The controls click UI themselves. If Unity's own mouse UI input also saw the press, every click would
        /// run twice (a toggle would turn on and straight back off), so the laptop mouse is taken off it.
        /// A headset's ray and poke still work.
        /// </summary>
        public void TakeMouseOffUiModules()
        {
            InputSystemUIInputModule mouseModule = Object.FindAnyObjectByType<InputSystemUIInputModule>();
            if (mouseModule != null)
                mouseModule.enabled = false;
            XRUIInputModule xrModule = Object.FindAnyObjectByType<XRUIInputModule>();
            if (xrModule != null)
                xrModule.enableMouseInput = false;
        }

        /// <summary>Starts a press if the pointer is on UI. Returns false when there's no UI under it.</summary>
        public bool Press(bool looking)
        {
            GameObject target = TopUi();
            if (target == null)
            {
                pressed = null;
                return false;
            }

            owner.LogUi("Press on UI: " + Path(target));
            pressed = target;
            pressedAt = EditorControlsInput.MousePosition;
            pressedTime = Time.unscaledTime;
            pressedWhileLooking = looking;
            ExecuteEvents.ExecuteHierarchy(target, PointerData(), ExecuteEvents.pointerDownHandler);
            return true;
        }

        public void Update()
        {
            // A jump this large is the cursor warping, not a hand sliding off the button.
            if (pressed != null && (EditorControlsInput.MousePosition - pressedAt).magnitude > 150f)
                pressedAt = EditorControlsInput.MousePosition;

            UpdateHover();
        }

        /// <summary>Ends the press. Releasing on the same control counts as a click, even if the pointer moved within it.</summary>
        public void Release()
        {
            if (pressed == null)
                return;

            bool heldLongEnough = !pressedWhileLooking || Time.unscaledTime - pressedTime >= MinClickHoldWhileLooking;
            bool onControl = SameControl(pressed, TopUi()) || PointerStillOn(pressed);
            PointerEventData data = PointerData();
            ExecuteEvents.ExecuteHierarchy(pressed, data, ExecuteEvents.pointerUpHandler);
            if (heldLongEnough && onControl)
            {
                ExecuteEvents.ExecuteHierarchy(pressed, data, ExecuteEvents.pointerClickHandler);
                EditorMouseClickTester.lastUiClick = Path(pressed);
                IUIClickable clickHandler = pressed.GetComponent<IUIClickable>();
                if (clickHandler != null)
                    clickHandler.OnUIClick();
            }
            pressed = null;
        }

        public bool IsOverUi() => TopUi() != null;

        /// <summary>Where the pointer meets the nearest world-space panel, for the laser to end on.</summary>
        public bool TryGetHitPoint(out Vector3 point)
        {
            point = Vector3.zero;
            float best = float.MaxValue;
            bool found = false;
            foreach (RaycastResult result in RaycastAll())
            {
                if (result.worldPosition == Vector3.zero || result.distance >= best)
                    continue;
                best = result.distance;
                point = result.worldPosition;
                found = true;
            }
            return found;
        }

        // The editor's own ray doesn't hover buttons, so this keeps the highlight on the control under the mouse.
        void UpdateHover()
        {
            GameObject next = TopUi();
            if (next == hovered)
                return;
            PointerEventData data = PointerData();
            if (hovered != null)
                ExecuteEvents.ExecuteHierarchy(hovered, data, ExecuteEvents.pointerExitHandler);
            hovered = next;
            if (next != null)
                ExecuteEvents.ExecuteHierarchy(next, data, ExecuteEvents.pointerEnterHandler);
        }

        // The nearest UI graphic under the pointer, across every panel, or null.
        GameObject TopUi()
        {
            GameObject best = null;
            float bestDistance = float.MaxValue;
            foreach (GraphicRaycaster raycaster in Raycasters())
            {
                results.Clear();
                raycaster.Raycast(PointerData(), results);
                if (results.Count > 0 && results[0].distance < bestDistance)
                {
                    bestDistance = results[0].distance;
                    best = results[0].gameObject;
                }
            }
            return best;
        }

        IEnumerable<RaycastResult> RaycastAll()
        {
            var all = new List<RaycastResult>();
            foreach (GraphicRaycaster raycaster in Raycasters())
            {
                results.Clear();
                raycaster.Raycast(PointerData(), results);
                all.AddRange(results);
            }
            return all;
        }

        IEnumerable<GraphicRaycaster> Raycasters()
        {
            if (eventSystem == null)
                yield break;
            if (Time.unscaledTime >= nextRaycasterRefresh)
            {
                // A panel built for the headset may only have the controller's raycaster, which the mouse can't use.
                // Give it the standard one for this Play session; the headset still uses its own.
                foreach (TrackedDeviceGraphicRaycaster tracked in Object.FindObjectsByType<TrackedDeviceGraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (tracked.GetComponent<GraphicRaycaster>() == null)
                        tracked.gameObject.AddComponent<GraphicRaycaster>();
                }
                // Include panels that are off right now; a choice panel that switches on later must still be clickable.
                raycasters = Object.FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                nextRaycasterRefresh = Time.unscaledTime + RaycasterRefreshSeconds;
            }
            foreach (GraphicRaycaster raycaster in raycasters)
            {
                if (raycaster != null && raycaster.isActiveAndEnabled)
                    yield return raycaster;
            }
        }

        PointerEventData PointerData()
        {
            return new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = EditorControlsInput.MousePosition
            };
        }

        // The pointer is still inside the pressed control, even if the ray now hits one of its children.
        bool PointerStillOn(GameObject target)
        {
            Selectable control = target.GetComponentInParent<Selectable>();
            RectTransform rect = control != null ? control.GetComponent<RectTransform>() : null;
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, EditorControlsInput.MousePosition, camera);
        }

        // A button's label and its background are one control.
        static bool SameControl(GameObject first, GameObject second)
        {
            if (first == null || second == null)
                return false;
            Selectable firstControl = first.GetComponentInParent<Selectable>();
            Selectable secondControl = second.GetComponentInParent<Selectable>();
            if (firstControl != null || secondControl != null)
                return firstControl == secondControl;
            return first == second;
        }

        static string Path(GameObject target)
        {
            string path = target.name;
            for (Transform parent = target.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }
    }
}
#endif
