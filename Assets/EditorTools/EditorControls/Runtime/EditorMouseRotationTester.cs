// Simple XR Device Simulator: https://github.com/aje0827/Simple-XR-Device-Simulator
// Copied into a project? Change the original in this repo, not the copy.
using UnityEngine;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XREditorControls
{
    /// <summary>
    /// Hold Alt and move the mouse to look around.
    /// Locking the cursor makes the mouse jump to the middle of the screen. Unity can report that jump
    /// as a click (pressing whatever button is in the middle, like Start) and as a big mouse movement
    /// (the camera jolts). On a Mac trackpad, a tap while the cursor is locked is also reported as a
    /// click. While Alt is held, a trackpad tap is not a click. A real click still is: press and release
    /// on the same button. UI input stays off during the hold so the tap cannot sneak through.
    /// </summary>
    [DefaultExecutionOrder(-2000)] // before the EventSystem, so UI is off before the jump is processed
    public class EditorMouseRotationTester : MonoBehaviour
    {
        /// True while Alt is held. The laser and clicks use the middle of the view, not the moving cursor.
        public static bool IsLooking { get; private set; }

        /// True while Alt is held, and for a moment after it is let go. UI input is off. A tap is ignored. A held click is still delivered by the click tester.
        public static bool SuppressClicks { get; private set; }

        [Header("Settings")]
        public bool enableMouseRotation = true;
        public KeyCode activationKey = KeyCode.LeftAlt;  // Hold this key to rotate the camera/view
        public bool enableCursorLocking = true;  // Locks cursor to prevent hitting screen edges

        [Header("Rotation Controls")]
        public float rotationSpeed = 100.0f;
        public bool invertY = false;
        public float smoothing = 5.0f;

        [Header("Constraints")]
        public bool limitVerticalRotation = true;
        public float minVerticalAngle = -80f;
        public float maxVerticalAngle = 80f;

        [Header("References")]
        public Camera targetCamera;      // Camera to rotate
        public Transform pivotTransform; // Optional pivot for orbit-style rotation

        // Internal variables
        private Quaternion targetRotation;
        private Quaternion currentRotation;
        private Quaternion initialRotation;
        private bool isRotating = false;
        private int ignoreMovementFrames = 0;
        private float suppressUntil = -1f;
        // The UI modules this tool switched off, and only those. Turning every module back on would undo
        // the click tester switching off the mouse module, and each click would press buttons twice.
        private readonly System.Collections.Generic.List<BaseInputModule> uiModulesTurnedOff = new System.Collections.Generic.List<BaseInputModule>();
        // Look angles kept as numbers, so the view never picks up a roll on the way to where it is going.
        private float pitch, yaw, targetPitch, targetYaw;

        [Header("Debug")]
        [Tooltip("Writes the mouse movement of the first frames after Alt to the Console, tagged [AltLook].")]
        public bool logLook = false;
        private int framesSinceLock;
        private float lastPixels;

        const int MovementFramesToIgnore = 3;
        // A real mouse move is at most a hundred or so pixels in one frame. More than this is the cursor
        // jumping to the middle of the screen, not the hand moving, so that frame is thrown away.
        const float JumpPixels = 150f;
        // On a Mac the cursor's jump to the middle can wait until the first click, and arrive as one
        // sudden move of 30 to 80 pixels out of a still mouse. A real hand speeds up over a few frames,
        // so a big move straight out of stillness is dropped for the whole time Alt is held.
        const float LateJumpPixels = 20f;
        const float StillPixels = 3f;
        // rotationSpeed 100 = 0.15 degrees per pixel of mouse movement.
        const float DegreesPerPixelAtSpeed100 = 0.15f;
        const float SecondsToIgnoreClicksAfterRelease = 0.35f;

        private void Start()
        {
            // Find the camera if none is assigned
            if (targetCamera == null)
                targetCamera = Camera.main;

            // Do not rotate the camera itself. XR Origin's Tracked Pose Driver
            // writes that transform every frame (headset pose, or "look forward"
            // when there is no headset), so Alt-drag looks like it fights and snaps back.
            // Rotate Camera Offset instead — that is the parent XR uses for height.
            if (pivotTransform == null)
                pivotTransform = FindLookPivot(targetCamera);

            // Store initial rotation values
            if (pivotTransform != null)
            {
                initialRotation = pivotTransform.localRotation;
                currentRotation = initialRotation;
                targetRotation = initialRotation;
                ReadAngles();
            }

        }

        private void Update()
        {
            #if UNITY_EDITOR
            if (!enableMouseRotation || pivotTransform == null)
                return;

            bool keyPressed = EditorControlsInput.KeyHeld(activationKey);
            IsLooking = keyPressed;

            // Start looking: clicks and UI off first, then lock the cursor.
            // Unity will not actually lock the cursor until the Game view has been clicked once,
            // so the first Alt still has to aim as if it were locked.
            if (keyPressed && !isRotating)
            {
                isRotating = true;
                ReadAngles();
                ignoreMovementFrames = MovementFramesToIgnore;
                framesSinceLock = 0;
                lastPixels = 0f;
                FocusGameView();

                if (enableCursorLocking)
                    Cursor.lockState = CursorLockMode.Locked;
            }

            // Stop looking: unlock, and keep clicks off a moment longer (the unlock can click too).
            if (!keyPressed && isRotating)
            {
                isRotating = false;
                if (Cursor.lockState == CursorLockMode.Locked)
                    Cursor.lockState = CursorLockMode.None;
                suppressUntil = Time.unscaledTime + SecondsToIgnoreClicksAfterRelease;
            }

            // A trackpad tap while Alt is held comes through as a left click. Keep clicks off
            // until Alt is up and the short settle after release has passed.
            if (keyPressed)
            {
                SuppressClicks = true;
                SetUiInputEnabled(false);
            }
            else if (SuppressClicks && Time.unscaledTime >= suppressUntil && !EditorControlsInput.LeftHeld)
            {
                SuppressClicks = false;
                SetUiInputEnabled(true);
            }

            // A click while looking must not turn the view. The press moves the mouse a little,
            // and the first click also delivers the cursor's jump to the middle of the screen.
            if (EditorControlsInput.LeftReleasedThisFrame)
                ignoreMovementFrames = Mathf.Max(ignoreMovementFrames, 2);
            if (isRotating && !EditorControlsInput.LeftHeld)
            {
                // MouseDelta is pixels * 0.1 with the new input system.
                Vector2 pixels = EditorControlsInput.MouseDelta * 10f;
                string what;
                if (ignoreMovementFrames > 0)
                {
                    ignoreMovementFrames--;
                    what = "ignored (click or just locked)";
                }
                else if (pixels.magnitude > JumpPixels)
                    what = "ignored (cursor jump)";
                else if (lastPixels < StillPixels && pixels.magnitude > LateJumpPixels)
                    what = "ignored (cursor jump)";
                else
                {
                    ApplyRotationFromDelta(pixels);
                    what = "used";
                }

                framesSinceLock++;
                lastPixels = pixels.magnitude;
                if (logLook && framesSinceLock <= 30)
                    Debug.Log($"[AltLook] frame {framesSinceLock}: mouse moved {pixels.x:F0},{pixels.y:F0} px, {what}. Look target pitch {targetPitch:F1}, yaw {targetYaw:F1}");
            }

            if (smoothing > 0)
            {
                float t = Time.deltaTime * smoothing;
                pitch = Mathf.LerpAngle(pitch, targetPitch, t);
                yaw = Mathf.LerpAngle(yaw, targetYaw, t);
            }
            else
            {
                pitch = targetPitch;
                yaw = targetYaw;
            }

            currentRotation = Quaternion.Euler(pitch, yaw, 0f);
            targetRotation = Quaternion.Euler(targetPitch, targetYaw, 0f);
            pivotTransform.localRotation = currentRotation;
            #endif
        }

        // Takes the look angles from where the view is now (another script may have turned it).
        private void ReadAngles()
        {
            Vector3 e = pivotTransform.localEulerAngles;
            pitch = targetPitch = e.x > 180f ? e.x - 360f : e.x;
            yaw = targetYaw = e.y;
        }

        // Unity ignores a cursor lock until the Game view is the focused window. The first click
        // was doing that job, and the cursor jumped to the middle as it locked.
        static void FocusGameView()
        {
    #if UNITY_EDITOR
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (type == null)
                return;
            if (UnityEditor.EditorWindow.focusedWindow != null && type.IsInstanceOfType(UnityEditor.EditorWindow.focusedWindow))
                return;
            UnityEditor.EditorWindow.FocusWindowIfItsOpen(type);
    #endif
        }

        private void SetUiInputEnabled(bool enabled)
        {
            if (!enabled)
            {
                foreach (BaseInputModule module in FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (module != null && module.enabled)
                    {
                        module.enabled = false;
                        uiModulesTurnedOff.Add(module);
                    }
                }
                return;
            }

            foreach (BaseInputModule module in uiModulesTurnedOff)
            {
                if (module != null)
                    module.enabled = true;
            }
            uiModulesTurnedOff.Clear();
        }


        private void OnDisable()
        {
            if (isRotating && Cursor.lockState == CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.None;
            isRotating = false;
            IsLooking = false;
            if (SuppressClicks)
                SetUiInputEnabled(true);
            SuppressClicks = false;
        }

        private void ApplyRotationFromDelta(Vector2 pixels)
        {
            float degreesPerPixel = DegreesPerPixelAtSpeed100 * rotationSpeed / 100f;
            targetYaw += pixels.x * degreesPerPixel;
            targetPitch += pixels.y * degreesPerPixel * (invertY ? 1 : -1);

            if (limitVerticalRotation)
                targetPitch = Mathf.Clamp(targetPitch, minVerticalAngle, maxVerticalAngle);
        }

        static Transform FindLookPivot(Camera cam)
        {
            // XR Origin puts Tracked Pose Driver on the camera. Rotating that
            // camera fights the driver (snap-back to "forward" with no headset).
            // The camera's parent is Camera Offset — rotate that instead.
            // A normal 3D Main Camera has no parent, so we rotate the camera.
            if (cam == null)
                return null;
            if (cam.transform.parent != null)
                return cam.transform.parent;
            return cam.transform;
        }

        // Reset rotation to initial values
        public void ResetRotation()
        {
            if (pivotTransform != null)
            {
                targetRotation = initialRotation;
                currentRotation = initialRotation;
                pivotTransform.localRotation = initialRotation;
                ReadAngles();
            }
        }

        #if UNITY_EDITOR
        [MenuItem("Tools/Reset Camera Rotation")]
        static void ResetCameraRotationMenuItem()
        {
            EditorMouseRotationTester rotationTester = FindAnyObjectByType<EditorMouseRotationTester>();
            if (rotationTester != null)
            {
                rotationTester.ResetRotation();
                Debug.Log("Camera rotation has been reset to initial values.");
            }
            else
            {
                Debug.LogWarning("No EditorMouseRotationTester found in the scene.");
            }
        }
        #endif
    }
}
