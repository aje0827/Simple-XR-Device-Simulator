// EditorTools-Unity editor controls: https://github.com/aje0827/EditorTools-Unity
// Copied into a project? Change the original in this repo, not the copy.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace XREditorControls
{
    /// <summary>
    /// Editor-only controller for testing an XR scene without a headset. The mouse is the controller's
    /// pointer: a left click is the trigger, Command is the grip, the right button is the thumbstick.
    /// Everything goes through the XR Interaction Toolkit, so interactables need nothing added.
    ///
    /// This component holds the settings and runs the parts in order; each part does one job:
    /// <see cref="MousePointer"/> (the ray and interactors), <see cref="MouseUiClick"/> (UI panels),
    /// <see cref="MouseWorldClick"/> (3D objects), <see cref="MouseGrab"/> (grab, turn, scale),
    /// <see cref="MouseLocomotion"/> (teleport and snap turn) and <see cref="MouseLaser"/> (what you see).
    /// </summary>
    public class EditorMouseClickTester : MonoBehaviour
    {
        [Header("Settings")]
        public bool enableMouseTesting = true;

        [Tooltip("Degrees a held object turns per pixel of mouse movement while Command and the right mouse button are held. Left and right spin it. Up and down tilt it.")]
        public float grabTurnSpeed = 0.3f;

        [Tooltip("How much a held object grows or shrinks per scroll notch while Command is held. 0.1 is 10 percent. It stays inside the min and max on the object's XR General Grab Transformer.")]
        public float grabScaleSpeed = 0.1f;

        [Header("Laser colors")]
        [Tooltip("Color the laser by what it points at.")]
        public bool visualizeRaycast = true;
        public Color raycastNormalColor = Color.gray;
        [Tooltip("Pointing at something clickable.")]
        public Color raycastHoverColor = Color.white;
        [Tooltip("Pressing on something clickable.")]
        public Color raycastClickColor = Color.green;
        [Tooltip("Pressing on nothing clickable, or no teleport target.")]
        public Color raycastMissColor = Color.red;
        [Tooltip("Seconds between hover checks for the laser color.")]
        public float hoverUpdateRate = 0.05f;

        [Header("Laser (visible in the Game view)")]
        [Tooltip("Draw a laser from a 'hand' near the camera to where the mouse points, like a controller ray.")]
        public bool showLaser = true;
        public float laserWidth = 0.004f;
        [Tooltip("Where the laser starts: right, down and forward from the camera.")]
        public Vector3 laserHandOffset = new Vector3(0.12f, -0.12f, 0.15f);
        [Tooltip("Points in the curved arc shown while aiming a teleport.")]
        public int arcSegments = 24;
        [Tooltip("How high the teleport arc bows, as a share of its length.")]
        public float arcHeight = 0.25f;

        [Header("References (found automatically when empty)")]
        public Camera mainCamera;
        public EventSystem eventSystem;
        [Tooltip("Leave empty to use the rig's controller ray, or the controls' own pointer when the controllers are off.")]
        public XRBaseInteractor clickRayInteractor;

        [Header("Debug")]
        [Tooltip("Write each world click, grab and teleport to the Console.")]
        public bool logWorldHits = false;
        [Tooltip("Write each UI press to the Console.")]
        public bool logUIHits = false;

        /// <summary>The last UI control a mouse click ran. Empty until a click lands.</summary>
        public static string lastUiClick;

#if UNITY_EDITOR
        MousePointer pointer;
        MouseUiClick ui;
        MouseWorldClick world;
        MouseGrab grab;
        MouseLocomotion locomotion;
        MouseLaser laser;

        bool buttonDown;
        bool gazeSelectStopped;
        float gazeSelectGiveUpAt;
#endif

        /// <summary>One real mouse click at a screen point, for automated checks.</summary>
        public static void QueueScreenClick(Vector2 screenPosition)
        {
#if UNITY_EDITOR
            ScriptedMouseClick.Queue(screenPosition);
#endif
        }

        /// <summary>True while Command is still holding this object, including while the right button turns it.</summary>
        public bool IsHolding(IXRSelectInteractable target)
        {
#if UNITY_EDITOR
            return target != null && grab != null && grab.Held == target;
#else
            return false;
#endif
        }

        /// <summary>Lets go of the editor grip, for example when the object is put back while Command is still down.</summary>
        public void EndEditorGrab()
        {
#if UNITY_EDITOR
            grab?.End();
#endif
        }

        internal void LogWorld(string message)
        {
            if (logWorldHits)
                Debug.Log("[EditorMouseClickTester] " + message);
        }

        internal void LogUi(string message)
        {
            if (logUIHits)
                Debug.Log("[EditorMouseClickTester] " + message);
        }

#if UNITY_EDITOR
        void OnEnable()
        {
            ScriptedMouseClick.Hook();
            Application.onBeforeRender += ApplyPendingScale;
        }

        void OnDisable()
        {
            ScriptedMouseClick.Unhook();
            Application.onBeforeRender -= ApplyPendingScale;
        }

        // Runs after the toolkit's before-render pass (order 100); see MouseGrab.ApplyPendingScale.
        [BeforeRenderOrder(200)]
        void ApplyPendingScale() => grab?.ApplyPendingScale();

        void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
            if (eventSystem == null)
                eventSystem = FindAnyObjectByType<EventSystem>();

            pointer = new MousePointer(mainCamera, clickRayInteractor);
            clickRayInteractor = pointer.ClickInteractor;
            ui = new MouseUiClick(this, eventSystem, mainCamera);
            locomotion = new MouseLocomotion(this, pointer);
            world = new MouseWorldClick(this, pointer, locomotion);
            grab = new MouseGrab(this, pointer);
            laser = new MouseLaser(this, pointer, ui, locomotion, grab);

            ui.TakeMouseOffUiModules();
            EditorScriptedPointer.Clear();
            gazeSelectGiveUpAt = Time.unscaledTime + 30f;
            StopGazeSelect();
        }

        void Update()
        {
            if (!enableMouseTesting || mainCamera == null)
                return;

            StopGazeSelect();

            // A press on UI waits for the release to count. While looking around, a press on a 3D object is ignored.
            if (EditorControlsInput.LeftPressedThisFrame)
            {
                buttonDown = true;
                bool looking = EditorMouseRotationTester.SuppressClicks;
                if (!ui.Press(looking) && !looking)
                    world.Click();
            }

            if (visualizeRaycast)
                laser.UpdateColor(buttonDown);

            // The controls' own pointer follows the mouse, so things react to hover as they do with a controller.
            // While the right button turns a held object, it stays put so the mouse doesn't also slide the object.
            if (grab.Held == null || !EditorControlsInput.RightHeld)
                pointer.Aim();
            ui.Update();

            grab.Update();
            locomotion.Update(grab.TakeRightClickWasTurn());

            if (EditorControlsInput.LeftReleasedThisFrame)
            {
                ui.Release();
                world.Release();
                buttonDown = false;
                if (visualizeRaycast)
                    laser.UpdateColor(false, now: true);
            }
        }

        void LateUpdate()
        {
            if (laser == null)
                return;
            if (!enableMouseTesting || !showLaser || mainCamera == null)
            {
                laser.Hide();
                locomotion.ClearArc();
                return;
            }
            laser.Draw();
        }

        void OnDestroy()
        {
            pointer?.Destroy();
        }

        // Some scenes turn on gaze hover-to-select. With no headset the gaze is the middle of the Game view, so
        // resting on a button and moving away would run it. A connected headset keeps the setting.
        void StopGazeSelect()
        {
            if (gazeSelectStopped)
                return;
            if (UnityEngine.XR.XRSettings.isDeviceActive || Time.unscaledTime > gazeSelectGiveUpAt)
            {
                gazeSelectStopped = true;
                return;
            }
            XRGazeInteractor[] gazes = FindObjectsByType<XRGazeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (gazes.Length == 0)
                return;
            foreach (XRGazeInteractor gaze in gazes)
                gaze.hoverToSelect = false;
            gazeSelectStopped = true;
        }
#endif
    }
}
