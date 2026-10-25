// Simple XR Device Simulator: https://github.com/aje0827/Simple-XR-Device-Simulator
// Copied into a project? Change the original in this repo, not the copy.
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace XREditorControls
{
    /// <summary>
    /// Editor-only controller for testing an XR scene without a headset. The mouse is the controller's
    /// pointer: a left click is the trigger, Command is the grip, the right button is the thumbstick.
    /// The left button does only what the trigger does and Command only what the grip does, through the XR
    /// Interaction Toolkit's own ray, so what works here works in the headset and what is broken there is
    /// broken here. Interactables need nothing added.
    ///
    /// This component holds the settings and runs the parts in order; each part does one job:
    /// <see cref="MousePointer"/> (the ray, its trigger, and the interactors), <see cref="MouseUiClick"/> (UI panels),
    /// <see cref="MouseGrab"/> (grab, turn, scale),
    /// <see cref="MouseLocomotion"/> (teleport and snap turn) and <see cref="MouseLaser"/> (what you see).
    /// </summary>
    public class EditorMouseClickTester : MonoBehaviour
    {
        [Header("Settings")]
        public bool enableMouseTesting = true;

        [Tooltip("Degrees a held object turns per pixel of mouse movement while Command and Control are held. Left and right spin it. Up and down tilt it. Add Shift to roll it.")]
        public float grabTurnSpeed = 0.3f;

        [Tooltip("How much a held object grows or shrinks per scroll notch while Command is held. 0.1 is 10 percent. It stays inside the min and max on the object's XR General Grab Transformer.")]
        public float grabScaleSpeed = 0.1f;

        [Header("Laser colors")]
        [Tooltip("Color the laser by what it points at.")]
        public bool visualizeRaycast = true;
        public Color raycastNormalColor = Color.gray;
        [Tooltip("Pointing at something the ray can act on.")]
        public Color raycastHoverColor = Color.white;
        [Tooltip("Pressing the trigger on something the ray can act on.")]
        public Color raycastClickColor = Color.green;
        [Tooltip("Pressing the trigger on nothing, or no teleport target.")]
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

        [Header("Recording")]
        [Tooltip("Show which control is in use, like \"⌘ Grab\", in the corner of the Game view. For demos and screen recordings.")]
        public bool showInputLabel = false;

        [Header("Debug")]
        [Tooltip("Write each grab and teleport to the Console.")]
        public bool logWorldHits = false;
        [Tooltip("Write each UI click to the Console.")]
        public bool logUIHits = false;

        /// <summary>The last UI control the XR UI Input Module clicked. Empty until a click lands.</summary>
        public static string lastUiClick;

#if UNITY_EDITOR
        MousePointer pointer;
        MouseUiClick ui;
        MouseGrab grab;
        MouseLocomotion locomotion;
        MouseLaser laser;
        MouseInputLabel inputLabel;

        bool buttonDown;
        bool triggerDown;
        float lookPressAt = -1f;
        int triggerPressedFrame = -1;
        bool releaseNextFrame;
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

        /// <summary>True while Command is still holding this object, including while Control turns it.</summary>
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

            pointer = new MousePointer(mainCamera) { HandOffset = laserHandOffset };
            ui = new MouseUiClick(this);
            locomotion = new MouseLocomotion(this, pointer);
            grab = new MouseGrab(this, pointer);
            laser = new MouseLaser(this, pointer, locomotion, grab);
            inputLabel = new MouseInputLabel(grab);

            ui.TakeMouseOffUiModules();
            ui.CheckScene();
            EditorScriptedPointer.Clear();
            gazeSelectGiveUpAt = Time.unscaledTime + 30f;
            StopGazeSelect();
        }

        void Update()
        {
            if (!enableMouseTesting || mainCamera == null)
                return;

            StopGazeSelect();

            // The toolkit reads the trigger a frame after it's set, so a press and release in the same frame
            // (a scripted click) lets go one frame later, or the press would never be seen.
            if (releaseNextFrame && Time.frameCount > triggerPressedFrame)
                ReleaseTrigger();

            // The left button is the trigger. While looking around, a trackpad tap is a press and release in the
            // same instant, so there it only counts once it's held briefly, the way a real click is.
            if (EditorControlsInput.LeftPressedThisFrame)
            {
                buttonDown = true;
                if (EditorMouseRotationTester.SuppressClicks)
                    lookPressAt = Time.unscaledTime;
                else
                    PressTrigger();
            }
            if (lookPressAt >= 0f && EditorControlsInput.LeftHeld && Time.unscaledTime - lookPressAt >= 0.1f)
                PressTrigger();

            if (visualizeRaycast)
                laser.UpdateColor(buttonDown);

            // The controls' own pointer follows the mouse, so things react to hover as they do with a controller.
            // While Control turns a held object, it stays put so the mouse doesn't also slide the object.
            if (!grab.HoldsAim)
                pointer.Aim();

            grab.Update();
            locomotion.Update(grab.Held != null);

            if (showInputLabel)
                inputLabel.Update();

            if (EditorControlsInput.LeftReleasedThisFrame)
            {
                lookPressAt = -1f;
                if (Time.frameCount > triggerPressedFrame)
                    ReleaseTrigger();
                else if (triggerDown)
                    releaseNextFrame = true;
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

        void PressTrigger()
        {
            lookPressAt = -1f;
            if (triggerDown)
                return;
            triggerDown = true;
            triggerPressedFrame = Time.frameCount;
            pointer.SetTrigger(true);
        }

        void ReleaseTrigger()
        {
            releaseNextFrame = false;
            if (!triggerDown)
                return;
            triggerDown = false;
            pointer.SetTrigger(false);
        }

        void OnGUI()
        {
            if (!enableMouseTesting)
                return;
            if (showInputLabel)
                inputLabel?.Draw();
            if (ui != null && ui.Problem.Length > 0)
                DrawProblem(ui.Problem);
        }

        // Shown in the Game view as well as the Console, because a click that does nothing looks like a broken simulator.
        static void DrawProblem(string problem)
        {
            var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true, fontSize = 13 };
            style.normal.textColor = Color.white;
            float width = Mathf.Min(520f, Screen.width - 20f);
            float height = style.CalcHeight(new GUIContent(problem), width) + 8f;
            Color before = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.8f, 0.1f, 0.1f, 1f);
            GUI.Box(new Rect(10f, 10f, width, height), problem, style);
            GUI.backgroundColor = before;
        }

        void OnDestroy()
        {
            ui?.Unhook();
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
