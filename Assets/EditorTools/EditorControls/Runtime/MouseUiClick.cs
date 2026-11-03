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
    /// UI panels. The controls don't press UI themselves: the left button is the pointer's trigger, and the
    /// scene's XR UI Input Module presses the button the ray is on, as it does in the headset. This part
    /// takes the plain mouse off the UI, says loudly when the scene can't be pressed by a controller ray,
    /// and reports each click for the Console and automated checks.
    /// </summary>
    internal sealed class MouseUiClick
    {
        readonly EditorMouseClickTester owner;
        XRUIInputModule xrModule;

        /// <summary>What stops a controller ray pressing this scene's UI, or empty when nothing does.</summary>
        public string Problem { get; private set; } = "";

        public MouseUiClick(EditorMouseClickTester owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// If Unity's own mouse UI input also saw the press, every click would run twice (a toggle would turn on
        /// and straight back off), and a scene with no XR UI Input Module would still click on the laptop while
        /// the headset could press nothing. So the plain mouse is taken off the UI. A headset's ray and poke still work.
        /// </summary>
        public void TakeMouseOffUiModules()
        {
            foreach (InputSystemUIInputModule mouseModule in Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None))
                mouseModule.enabled = false;
            foreach (StandaloneInputModule oldModule in Object.FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None))
                oldModule.enabled = false;

            KeepMouseOffXrModule();
        }

        /// <summary>
        /// Runs every frame: the scene with the EventSystem can load after the controls (an additive controls scene).
        /// The XR UI Input Module has two mouse paths. Enable Mouse Input covers its built-in one; when the module has
        /// UI actions assigned (Starter Assets assigns XRI UI/Click, bound to the mouse), it reads those instead and
        /// ignores that switch. Either way the real mouse pressed UI as a second pointer on top of the controller ray,
        /// so one click pressed a button twice. Both are turned off; the ray is the only thing that presses.
        /// </summary>
        public void KeepMouseOffXrModule()
        {
            if (xrModule == null || !xrModule.isActiveAndEnabled)
            {
                XRUIInputModule found = Object.FindAnyObjectByType<XRUIInputModule>();
                if (found == null)
                    return;
                if (found != xrModule)
                {
                    if (xrModule != null)
                        xrModule.pointerClick -= OnClick;
                    xrModule = found;
                    xrModule.pointerClick += OnClick;
                }
            }
            if (xrModule.enableMouseInput)
                xrModule.enableMouseInput = false;
            if (xrModule.enableTouchInput)
                xrModule.enableTouchInput = false;
            if (xrModule.leftClickAction != null)
                xrModule.leftClickAction = null;
            if (xrModule.rightClickAction != null)
                xrModule.rightClickAction = null;
            if (xrModule.middleClickAction != null)
                xrModule.middleClickAction = null;
        }

        /// <summary>Warns in the Console, once on Play, about anything that stops a controller ray pressing UI.</summary>
        public void CheckScene()
        {
            var problems = new List<string>();
            if (xrModule == null || !xrModule.isActiveAndEnabled)
            {
                problems.Add("The EventSystem has no XR UI Input Module, so controller rays can't press UI, in the headset or here.");
            }
            else if (!xrModule.enableXRInput)
            {
                problems.Add("The XR UI Input Module has Enable XR Input off, so controller rays can't press UI, in the headset or here.");
            }

            var missing = new List<string>();
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.renderMode == RenderMode.WorldSpace && canvas.isRootCanvas
                    && canvas.GetComponent<GraphicRaycaster>() != null && canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null
                    && canvas.GetComponentInChildren<Selectable>(true) != null) // a panel with nothing to press doesn't need it
                    missing.Add(canvas.name);
            }
            if (missing.Count > 0)
                problems.Add("These panels have no Tracked Device Graphic Raycaster, so controller rays can't press them: " + string.Join(", ", missing) + ".");

            Problem = string.Join("\n", problems);
            foreach (string problem in problems)
                Debug.LogWarning("[Simple XR Device Simulator] " + problem);
        }

        public void Unhook()
        {
            if (xrModule != null)
                xrModule.pointerClick -= OnClick;
        }

        void OnClick(GameObject target, PointerEventData data)
        {
            if (target == null)
                return;
            string path = Path(target);
            EditorMouseClickTester.lastUiClick = path;
            owner.LogUi("UI click: " + path);
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
