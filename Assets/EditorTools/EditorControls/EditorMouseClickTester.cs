// EditorTools-Unity editor controls: https://github.com/aje0827/EditorTools-Unity
// Copied into a project? Change the original in this repo, not the copy.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor-only controller for testing an XR scene without a headset. A mouse click does what the
/// controller's trigger does: it asks the XR Interaction Manager to select the interactable you point at,
/// or presses the UI under the pointer. Interactables need nothing added; any XR interactable works.
/// </summary>
public class EditorMouseClickTester : MonoBehaviour
{
    [Header("Settings")]
    public bool enableMouseTesting = true;

    [Tooltip("Degrees a held object turns per pixel of mouse movement while Command and the right mouse button are held. Left and right spin it. Up and down tilt it.")]
    public float grabTurnSpeed = 0.3f;

    [Tooltip("How much a held object grows or shrinks per scroll notch while Command is held. 0.1 is 10 percent. It stays inside the min and max on the object's XR General Grab Transformer.")]
    public float grabScaleSpeed = 0.1f;
    
    [Header("Visualization")]
    public bool visualizeRaycast = true;
    public Color raycastNormalColor = Color.gray;      // Default ray color
    public Color raycastHoverColor = Color.white;      // Hover over clickable
    public Color raycastClickColor = Color.green;      // Click on clickable
    public Color raycastMissColor = Color.red;         // Click on non-clickable
    public float raycastLineTime = 0.5f;
    public bool useGizmoLines = true;
    public float hoverUpdateRate = 0.05f;              // How often to update hover check

    [Header("References")]
    public Camera mainCamera;
    public EventSystem eventSystem;
    [Tooltip("Leave empty to auto-find a non-teleport ray on the XR Origin.")]
    public XRBaseInteractor clickRayInteractor;
    XRBaseInteractor mouseActiveInteractor;
    
    [Header("Debug")]
    [Tooltip("Write each world click, grab and teleport to the Console.")]
    public bool logWorldHits = false;
    [Tooltip("Write each UI press to the Console.")]
    public bool logUIHits = false;
    
    private GraphicRaycaster[] graphicRaycasters;
    private XRInteractionManager xrManager;
    private IXRSelectInteractable selectedByMouse;
    IXRSelectInteractable held;

    /// True while Command is still holding this object. A right-drag turn drops the
    /// toolkit grab for a moment so the turn is not overwritten. That is not a let-go.
    public bool IsHolding(IXRSelectInteractable target)
    {
        return target != null && held == target;
    }
    bool turningHeld;
    bool suppressTeleportRelease;
    // Size when Play started, per object, so the scroll limits match the grab transformer's.
    Dictionary<int, Vector3> scaleBasis = new Dictionary<int, Vector3>();
    float pendingScroll;

    // A two-finger lift sends a scroll as the right click ends. Ignore that scroll briefly,
    // or the model changes size at the end of a turn.
    const float ScrollQuietAfterTurn = 0.5f;
    float scrollQuietUntil;
    private Vector3 lastRayStart;
    private Vector3 lastRayEnd;

    [Header("Laser (visible in the Game view)")]
    [Tooltip("Draw a laser from a 'hand' near the camera to where the mouse points, like a controller ray.")]
    public bool showLaser = true;
    public float laserWidth = 0.004f;
    public Vector3 laserHandOffset = new Vector3(0.12f, -0.12f, 0.15f); // right, down, forward from the camera
    [Tooltip("Points in the curved arc shown over teleport spots, like the controller's teleport ray.")]
    public int arcSegments = 24;
    [Tooltip("How high the teleport arc bows, as a share of its length.")]
    public float arcHeight = 0.25f;
    private LineRenderer laser;
    private Transform laserDot;
    private Color currentRayColor;
    private float rayVisibleUntil;
    private float nextHoverCheck;
    private bool isHoveringClickable;
    private bool isMouseDown = false;
    // A press on a button is not a click yet. Moving the pointer down toward
    // another button must not fire the first one.
    private GameObject pendingUiPress;
    private Vector2 pendingUiPressPosition;
    private float pendingUiPressTime;
    private bool pendingDuringLook;
    private bool pendingUiCancelled;

    /// The last control the mouse click actually ran. Empty until a click lands.
    public static string lastUiClick;
    // A trackpad tap while looking is a press and release in the same instant.
    // A real click is held down briefly, then released on the same button.
    const float MinClickHoldWhileLooking = 0.1f;
    bool gazeLookClickStopped;
    float gazeLookClickGiveUp;

#if UNITY_EDITOR
    static bool scriptedHooked;

    void OnEnable()
    {
        Application.onBeforeRender += ApplyPendingScale;
        if (!scriptedHooked)
        {
            InputSystem.onBeforeUpdate += DriveScriptedMouse;
            scriptedHooked = true;
        }
    }

    void OnDisable()
    {
        Application.onBeforeRender -= ApplyPendingScale;
        if (scriptedHooked)
        {
            InputSystem.onBeforeUpdate -= DriveScriptedMouse;
            scriptedHooked = false;
        }
    }

    // One mouse click at a screen point, through the same press and release this tester
    // already uses for a real cursor. Two input updates: button down, then button up.
    static int scriptedStep;
    static Vector2 scriptedPos;
    static double scriptedTime;

    public static void QueueScreenClick(Vector2 screenPos)
    {
        scriptedPos = screenPos;
        scriptedStep = 1;
        scriptedTime = 0;
    }

    static void DriveScriptedMouse()
    {
        if (scriptedStep == 0)
            return;
        if (InputState.currentUpdateType != InputUpdateType.Dynamic)
            return;
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        MouseState state = new MouseState();
        state.position = scriptedPos;
        state.buttons = (ushort)(scriptedStep == 1 ? 1 : 0);
        double now = InputState.currentTime;
        // A queued time ahead of the clock gets dropped. Wait until the clock catches up.
        if (scriptedTime >= now - 0.0002)
            return;
        double t = now - 0.0001;
        scriptedTime = t;
        InputSystem.QueueStateEvent(mouse, state, t);
        scriptedStep = scriptedStep == 1 ? 2 : 0;
    }
#endif

    void Start()
    {
        // Find the camera if none is assigned
        if (mainCamera == null)
            mainCamera = Camera.main;
            
        // Find event system if none is assigned
        if (eventSystem == null)
            eventSystem = FindAnyObjectByType<EventSystem>();
            
        // Include panels that are off at the start. The choice panel turns on later,
        // and a list taken only from what is on now never sees it, so the laser misses it.
        graphicRaycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        // Initialize ray visualization
        currentRayColor = raycastNormalColor;
        
        if (clickRayInteractor == null)
            clickRayInteractor = FindClickRayInteractor();

        if (clickRayInteractor != null)
            xrManager = clickRayInteractor.interactionManager;

        if (xrManager == null)
            xrManager = FindAnyObjectByType<XRInteractionManager>();

#if UNITY_EDITOR
        // This tester runs the mouse click. The UI modules see that same press and run the
        // button again, so a toggle turns on and straight back off. The headset still uses
        // the ray and the poke. Only the laptop mouse is taken off those modules.
        UnityEngine.InputSystem.UI.InputSystemUIInputModule mouseModule = FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (mouseModule != null)
            mouseModule.enabled = false;
        UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule xrUi = FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
        if (xrUi != null)
            xrUi.enableMouseInput = false;
#endif

        EditorScriptedPointer.Clear();
        gazeLookClickGiveUp = Time.unscaledTime + 30f;
        StopGazeLookClick();
    }

    // Some scenes turn on gaze hover-to-select. With no headset that look is the middle of the
    // Game view, so resting on a button and moving away runs it. A headset keeps the setting.
    void StopGazeLookClick()
    {
        if (gazeLookClickStopped)
            return;
        if (UnityEngine.XR.XRSettings.isDeviceActive)
        {
            gazeLookClickStopped = true;
            return;
        }
        if (Time.unscaledTime > gazeLookClickGiveUp && gazeLookClickGiveUp > 0f)
        {
            gazeLookClickStopped = true;
            return;
        }
        var gazes = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRGazeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (gazes == null || gazes.Length == 0)
            return;
        for (int i = 0; i < gazes.Length; i++)
            gazes[i].hoverToSelect = false;
        gazeLookClickStopped = true;
    }
    
    void Update()
    {
        #if UNITY_EDITOR
        if (!enableMouseTesting)
            return;

        StopGazeLookClick();
        
        // A press is not a click yet. It counts on release, on that same button, if the pointer did not slide off.
        // While Alt is held, a tap is ignored. A click that is held down briefly still counts.
        if (EditorControlsInput.LeftPressedThisFrame)
        {
            isMouseDown = true;
            bool looking = EditorMouseRotationTester.SuppressClicks;
            GameObject ui = RaycastTopUi(true);
            if (ui != null)
            {
                pendingUiPress = ui;
                pendingUiPressPosition = EditorControlsInput.MousePosition;
                pendingUiPressTime = Time.unscaledTime;
                pendingDuringLook = looking;
                pendingUiCancelled = false;
                PointerEventData pressData = new PointerEventData(eventSystem);
                pressData.button = PointerEventData.InputButton.Left;
                pressData.position = EditorControlsInput.MousePosition;
                ExecuteEvents.ExecuteHierarchy(ui, pressData, ExecuteEvents.pointerDownHandler);
            }
            else
            {
                pendingUiPress = null;
                if (!looking)
                    HandleMouseClick();
            }
        }

        if (pendingUiPress != null)
        {
            // A jump this large is the cursor warping, not a hand sliding off the button.
            float moved = (EditorControlsInput.MousePosition - pendingUiPressPosition).magnitude;
            if (moved > 150f)
                pendingUiPressPosition = EditorControlsInput.MousePosition;
        }
        
        // Update visualization based on mouse state
        if (visualizeRaycast)
        {
            if (isMouseDown)
            {
                // While mouse is down, constantly check if we're over a clickable
                // and update the ray color accordingly (green/red)
                UpdateClickRaycast();
                rayVisibleUntil = Time.time + 0.1f; // Keep extending visibility
            }
            else if (Time.time >= nextHoverCheck)
            {
                // When mouse is not down, do normal hover state (white/gray)
                nextHoverCheck = Time.time + hoverUpdateRate;
                UpdateHoverRaycast();
            }
        }
        
        // The controls' own controller pointer follows the mouse, so things react to hovering
        // (outlines, highlights) exactly as they do when the controller points at them.
        // While the right button turns a held object, leave the pointer where it is so the
        // mouse does not also slide the object.
        if (held == null || !EditorControlsInput.RightHeld)
            AimPointer();
        UpdateUiHover();

        // Command key: grab (like the grip). Hold to keep holding; the grabbed thing follows the mouse.
        if (EditorControlsInput.CommandPressedThisFrame)
            StartGrab();
        if (EditorControlsInput.CommandReleasedThisFrame)
            EndGrab();

        // Right button while holding something: the mouse turns it instead of moving it.
        if (held != null && EditorControlsInput.RightHeld)
        {
            if (!turningHeld)
                BeginTurn();
            TurnHeld();
        }
        else if (turningHeld)
        {
            turningHeld = false;
            scrollQuietUntil = Time.unscaledTime + ScrollQuietAfterTurn;
            pendingScroll = 0f;
            if (held != null)
                ResumeMove();
        }

        // Scroll wheel, or a two-finger swipe with Command held and the right button up, scales
        // what Command is holding. A two-finger press is the right click, and the trackpad also
        // reports that press as a scroll. Ignore it while turning, and for a moment after,
        // so letting go does not change the size.
        if (held != null && !EditorControlsInput.RightHeld && Time.unscaledTime >= scrollQuietUntil)
            ScaleHeld();

        // Left and right arrows: snap turn (like the stick pushed left or right).
        if (EditorControlsInput.ArrowPressedThisFrame(KeyCode.LeftArrow))
            SnapTurn(-1f);
        if (EditorControlsInput.ArrowPressedThisFrame(KeyCode.RightArrow))
            SnapTurn(1f);

        // Right mouse: hold to aim the teleport arc, release to jump (like the stick pushed forward).
        // A right click that was turning a held object does not teleport.
        if (EditorControlsInput.RightReleasedThisFrame && suppressTeleportRelease)
            suppressTeleportRelease = false;
        else if (EditorControlsInput.RightReleasedThisFrame && mainCamera != null)
        {
            UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.BaseTeleportationInteractable tp;
            Vector3 landing;
            if (arcTarget != null && arcTarget.activeInHierarchy)
                TeleportTo(arcTarget.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.BaseTeleportationInteractable>());
            else if (TryGetTeleportTarget(mainCamera.ScreenPointToRay(EditorControlsInput.MousePosition), out tp, out landing))
                TeleportTo(tp);
        }

        // Handle mouse button release. Sliding off the button you pressed is not a click.
        // Staying on it counts, even if the pointer moved. The old 28px cutoff was dropping
        // these presses: the press hit the button and the release was still on it.
        if (EditorControlsInput.LeftReleasedThisFrame)
        {
            if (pendingUiPress != null && !pendingUiCancelled)
            {
                bool heldLongEnough = !pendingDuringLook || (Time.unscaledTime - pendingUiPressTime) >= MinClickHoldWhileLooking;
                GameObject releasedOn = RaycastTopUi(false);
                bool onControl = SameUiControl(pendingUiPress, releasedOn) || PointerStillOn(pendingUiPress);
                PointerEventData releaseData = new PointerEventData(eventSystem);
                releaseData.button = PointerEventData.InputButton.Left;
                releaseData.position = EditorControlsInput.MousePosition;
                ExecuteEvents.ExecuteHierarchy(pendingUiPress, releaseData, ExecuteEvents.pointerUpHandler);
                if (heldLongEnough && onControl)
                {
                    ExecuteEvents.ExecuteHierarchy(pendingUiPress, releaseData, ExecuteEvents.pointerClickHandler);
                    lastUiClick = GetUIPath(pendingUiPress);
                    IUIClickable uiClickable = pendingUiPress.GetComponent<IUIClickable>();
                    if (uiClickable != null)
                        uiClickable.OnUIClick();
                }
            }
            pendingUiPress = null;
            pendingUiCancelled = false;
            pendingDuringLook = false;
            isMouseDown = false;
            EndMouseXrSelect();
            
            // After mouse up, immediately switch back to hover state
            if (visualizeRaycast)
            {
                UpdateHoverRaycast();
            }
        }
        #endif
    }
    
    // The teleport spot the arc is resting on. With the controller, holding the stick over a spot makes the
    // teleport ray select it until you let go, and the spot plays whatever it is set up to play (an arrow rising,
    // a glow). The arc makes the teleport interactor select it the same way, through the toolkit's own
    // manual select, so spots in any project react exactly as they do in the headset.
    GameObject arcTarget;
    XRBaseInteractor arcHolder;

    void SetArcTarget(GameObject target)
    {
        if (target == arcTarget) return;
        if (arcHolder != null && arcHolder.isPerformingManualInteraction)
            arcHolder.EndManualInteraction();
        arcHolder = null;
        arcTarget = target;
        var spot = arcTarget != null ? arcTarget.GetComponent<XRBaseInteractable>() : null;
        if (spot == null) return;
        arcHolder = FindInteractor(true);
        if (arcHolder == ownMouseInteractor)
            arcHolder = OwnArcInteractor();
        if (arcHolder.interactionManager == null)
            arcHolder.interactionManager = spot.interactionManager;
        arcHolder.StartManualInteraction((IXRSelectInteractable)spot);
    }

    // Updates both the endpoint and color of the ray based on whether we're over a clickable
    void LateUpdate()
    {
        #if UNITY_EDITOR
        if (!enableMouseTesting || !showLaser || mainCamera == null)
        {
            if (laser != null) { laser.enabled = false; laserDot.gameObject.SetActive(false); }
            SetArcTarget(null);
            return;
        }
        if (laser == null)
            BuildLaser();

        Ray ray = mainCamera.ScreenPointToRay(EditorControlsInput.MousePosition);
        Vector3 end = ray.origin + ray.direction * 10f;
        Transform cam = mainCamera.transform;
        Vector3 start = cam.position + cam.right * laserHandOffset.x + cam.up * laserHandOffset.y + cam.forward * laserHandOffset.z;
        laser.enabled = true;
        Color color = currentRayColor;

        if (EditorControlsInput.RightHeld && held == null)
        {
            // Teleport mode (like pushing the stick forward): curved arc, snapped to where you would land.
            UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.BaseTeleportationInteractable tp;
            Vector3 landing;
            bool valid = TryGetTeleportTarget(ray, out tp, out landing);
            RaycastHit any;
            if (valid) end = landing;
            else if (Physics.Raycast(ray, out any, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) end = any.point;
            color = valid ? raycastHoverColor : raycastMissColor;

            // Stay on a spot while the mouse is still over it on the floor. Its arrow rises out from under
            // the mouse, and dropping the spot then would make the arrow bob up and down every frame.
            GameObject spot = valid && tp is UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor ? tp.gameObject : null;
            if (spot == null && arcTarget != null && arcTarget.activeInHierarchy)
            {
                RaycastHit floor;
                if (Physics.Raycast(ray, out floor, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    Vector3 offset = floor.point - arcTarget.transform.position;
                    offset.y = 0f;
                    if (offset.magnitude < 0.6f)
                    {
                        spot = arcTarget;
                        valid = true;
                        color = raycastHoverColor;
                        var keep = arcTarget.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>();
                        end = keep != null && keep.teleportAnchorTransform != null ? keep.teleportAnchorTransform.position : arcTarget.transform.position;
                    }
                }
            }
            SetArcTarget(spot);

            int n = Mathf.Max(3, arcSegments);
            laser.positionCount = n;
            Vector3 mid = (start + end) * 0.5f + Vector3.up * Vector3.Distance(start, end) * arcHeight;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                laser.SetPosition(i, (1 - t) * (1 - t) * start + 2 * (1 - t) * t * mid + t * t * end);
            }
        }
        else
        {
            SetArcTarget(null);
            Vector3 uiPoint;
            RaycastHit hit;
            if (TryGetUIHitPoint(out uiPoint))
                end = uiPoint;
            else if (RaycastForClick(ray, out hit))
                end = hit.point;
            laser.positionCount = 2;
            laser.SetPosition(0, start);
            laser.SetPosition(1, end);
        }
        laser.startColor = laser.endColor = color;
        laserDot.gameObject.SetActive(true);
        laserDot.position = end;
        laserDot.localScale = Vector3.one * Mathf.Max(0.01f, Vector3.Distance(cam.position, end) * 0.008f);
        laserDot.GetComponent<Renderer>().material.color = color;
        #endif
    }

    // What a teleport ray would land on: a teleport spot (snapped to its landing point) or a teleport floor.
    bool TryGetTeleportTarget(Ray ray, out UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.BaseTeleportationInteractable tp, out Vector3 landing)
    {
        tp = null; landing = Vector3.zero;
        RaycastHit hit;
        if (!RaycastForClick(ray, out hit)) return false;
        tp = hit.collider.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.BaseTeleportationInteractable>();
        if (tp == null) return false;
        landing = hit.point;
        if (tp is UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor anchor && anchor.teleportAnchorTransform != null)
            landing = anchor.teleportAnchorTransform.position;
        return true;
    }

    void BuildLaser()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);

        var go = new GameObject("~EditorLaser");
        go.transform.SetParent(transform, false);
        laser = go.AddComponent<LineRenderer>();
        laser.useWorldSpace = true;
        laser.positionCount = 2;
        laser.startWidth = laserWidth;
        laser.endWidth = laserWidth * 0.5f;
        laser.material = mat;
        laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laser.receiveShadows = false;

        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "~EditorLaserDot";
        Destroy(dot.GetComponent<Collider>()); // never block clicks
        dot.transform.SetParent(transform, false);
        var r = dot.GetComponent<Renderer>();
        r.material = new Material(mat);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laserDot = dot.transform;
    }

    // Where the mouse ray meets a world-space UI panel, if it does.
    bool TryGetUIHitPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (eventSystem == null || graphicRaycasters == null) return false;
        var pointerData = new PointerEventData(eventSystem) { position = EditorControlsInput.MousePosition };
        float best = float.MaxValue;
        bool found = false;
        var results = new List<RaycastResult>();
        foreach (GraphicRaycaster raycaster in graphicRaycasters)
        {
            if (raycaster == null || !raycaster.isActiveAndEnabled) continue;
            results.Clear();
            raycaster.Raycast(pointerData, results);
            foreach (var r in results)
            {
                if (r.worldPosition == Vector3.zero) continue;
                if (r.distance < best) { best = r.distance; point = r.worldPosition; found = true; }
            }
        }
        return found;
    }

    private void UpdateClickRaycast()
    {
        // Check if we're clicking on UI first
        bool isOverClickable = IsHoveringUI();
        Vector3 rayEnd = Vector3.zero;
        
        // If not UI, check for world objects
        if (!isOverClickable)
        {
            Ray ray = mainCamera.ScreenPointToRay(EditorControlsInput.MousePosition);
            lastRayStart = mainCamera.transform.position;
            
            RaycastHit hit;
            if (RaycastForClick(ray, out hit))
            {
                rayEnd = hit.point;
                // Check if this is a clickable object
                isOverClickable = IsObjectClickable(hit.collider.gameObject);
            }
            else
            {
                // Not hitting anything
                rayEnd = ray.origin + ray.direction * 100;
            }
        }
        else
        {
            // If we're over UI, set a reasonable rayEnd
            Ray ray = mainCamera.ScreenPointToRay(EditorControlsInput.MousePosition);
            lastRayStart = mainCamera.transform.position;
            rayEnd = ray.origin + ray.direction * 10; // Shorter distance for UI
        }
        
        // Set the color based on whether we're over a clickable
        currentRayColor = isOverClickable ? raycastClickColor : raycastMissColor;
        
        // Store ray info for visualization
        lastRayEnd = rayEnd;
    }
    
    private void UpdateHoverRaycast()
    {
        // For Debug Visualization
        Vector3 rayOrigin = Vector3.zero;
        Vector3 rayEnd = Vector3.zero;
        
        // First check if we're hovering over UI
        bool hoveringUI = IsHoveringUI();
        isHoveringClickable = hoveringUI;
        
        // If UI wasn't hit, check for world objects
        if (!hoveringUI)
        {
            Ray ray = mainCamera.ScreenPointToRay(EditorControlsInput.MousePosition);
            rayOrigin = ray.origin;
            
            RaycastHit hit;
            if (RaycastForClick(ray, out hit))
            {
                rayEnd = hit.point;
                
                // Check if we're hovering over a clickable object
                isHoveringClickable = IsObjectClickable(hit.collider.gameObject);
            }
            else
            {
                // Not hovering over anything
                rayEnd = ray.origin + ray.direction * 100;
                isHoveringClickable = false;
            }
        }
        
        // Set the appropriate color
        currentRayColor = isHoveringClickable ? raycastHoverColor : raycastNormalColor;
        
        // Store the ray info for OnDrawGizmos
        if (useGizmoLines)
        {
            lastRayStart = mainCamera.transform.position;
            lastRayEnd = rayEnd;
            rayVisibleUntil = Time.time + 0.1f; // Keep visible until next update
        }
        else
        {
            // Use Debug.DrawLine (only visible in Scene view)
            Debug.DrawLine(rayOrigin, rayEnd, currentRayColor, hoverUpdateRate);
        }
    }
    
    // Processes a mouse click on a world object. UI presses are handled on release.
    private void HandleMouseClick()
    {
        Ray ray = mainCamera.ScreenPointToRay(EditorControlsInput.MousePosition);
        RaycastHit hit;

        if (RaycastForClick(ray, out hit))
        {
            // Check if we hit a clickable object
            bool isClickable = IsObjectClickable(hit.collider.gameObject);

            if (logWorldHits)
            {
                string clickableStatus = isClickable ? " (Clickable)" : "";
                Debug.Log($"[EditorMouseClickTester] Hit world object: {hit.collider.gameObject.name}{clickableStatus}");
            }

            // Process the hit if it's clickable
            if (isClickable)
            {
                HandleWorldObjectClick(hit);
            }
        }
    }
    
    GameObject uiHover;

    // The editor ray no longer hovers buttons. This keeps the highlight on the control under the mouse.
    void UpdateUiHover()
    {
        if (eventSystem == null)
            return;
        GameObject next = RaycastTopUi(false);
        if (next == uiHover)
            return;
        PointerEventData data = new PointerEventData(eventSystem);
        data.position = EditorControlsInput.MousePosition;
        if (uiHover != null)
            ExecuteEvents.ExecuteHierarchy(uiHover, data, ExecuteEvents.pointerExitHandler);
        uiHover = next;
        if (next != null)
            ExecuteEvents.ExecuteHierarchy(next, data, ExecuteEvents.pointerEnterHandler);
    }

    // Closest UI graphic under the pointer, or null. Does not press it.
    // The first canvas in the list used to win even when a nearer panel was the one on screen.
    private GameObject RaycastTopUi(bool logHit)
    {
        graphicRaycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (eventSystem == null || graphicRaycasters == null || graphicRaycasters.Length == 0)
            return null;

        PointerEventData pointerData = new PointerEventData(eventSystem);
        pointerData.position = EditorControlsInput.MousePosition;

        float bestDistance = float.MaxValue;
        GameObject best = null;
        foreach (GraphicRaycaster raycaster in graphicRaycasters)
        {
            if (raycaster == null || !raycaster.isActiveAndEnabled) continue;

            List<RaycastResult> results = new List<RaycastResult>();
            raycaster.Raycast(pointerData, results);
            if (results.Count == 0)
                continue;
            if (results[0].distance < bestDistance)
            {
                bestDistance = results[0].distance;
                best = results[0].gameObject;
            }
        }

        if (best != null && logHit && logUIHits)
            Debug.Log($"[EditorMouseClickTester] Press on UI: {GetUIPath(best)}");
        return best;
    }

    // The pointer is still inside the control that was pressed, even if the ray now hits a child.
    bool PointerStillOn(GameObject pressed)
    {
        if (pressed == null || mainCamera == null)
            return false;
        Selectable control = pressed.GetComponentInParent<Selectable>();
        if (control == null)
            return false;
        RectTransform rect = control.GetComponent<RectTransform>();
        if (rect == null)
            return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, EditorControlsInput.MousePosition, mainCamera);
    }

    // The label and the button background are one control.
    private static bool SameUiControl(GameObject pressed, GameObject released)
    {
        if (pressed == null || released == null)
            return false;
        Selectable pressedControl = pressed.GetComponentInParent<Selectable>();
        Selectable releasedControl = released.GetComponentInParent<Selectable>();
        if (pressedControl != null || releasedControl != null)
            return pressedControl == releasedControl;
        return pressed == released;
    }
    
    private bool IsHoveringUI()
    {
        if (eventSystem == null || graphicRaycasters == null || graphicRaycasters.Length == 0)
            return false;
            
        PointerEventData pointerData = new PointerEventData(eventSystem);
        pointerData.position = EditorControlsInput.MousePosition;
        
        foreach (GraphicRaycaster raycaster in graphicRaycasters)
        {
            if (raycaster == null || !raycaster.isActiveAndEnabled) continue;
            
            List<RaycastResult> results = new List<RaycastResult>();
            raycaster.Raycast(pointerData, results);
            
            if (results.Count > 0)
            {
                return true;
            }
        }
        
        return false;
    }
    
    private bool IsObjectClickable(GameObject obj)
    {
        if (obj.GetComponentInParent<XRBaseInteractable>() != null)
            return true;

        return obj.GetComponent<IClickable>() != null;
    }
    
    private void HandleWorldObjectClick(RaycastHit hit)
    {
        GameObject hitObject = hit.collider.gameObject;

        if (TrySelectXrInteractable(hitObject))
            return;

        IClickable clickableInterface = hitObject.GetComponent<IClickable>();
        if (clickableInterface != null)
        {
            clickableInterface.OnClick();
        }
    }

    bool TrySelectXrInteractable(GameObject hitObject)
    {
        XRBaseInteractable interactable = hitObject.GetComponentInParent<XRBaseInteractable>();
        if (interactable == null)
            return false;

        // Teleport spots: ask the rig's teleport system to move us there directly,
        // since a mouse has no controller ray for the spot to check.
        if (interactable is UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.BaseTeleportationInteractable tp)
            return TeleportTo(tp);

        // The trigger doesn't pick things up; right click (the grip) does.
        if (interactable is XRGrabInteractable)
            return false;

        // Controllers can be switched off after Start (no headset), so re-check every click.
        if (clickRayInteractor == null || !clickRayInteractor.isActiveAndEnabled)
        {
            clickRayInteractor = FindClickRayInteractor();
            xrManager = null;
        }

        if (xrManager == null && clickRayInteractor != null)
            xrManager = clickRayInteractor.interactionManager;

        if (xrManager == null)
            xrManager = FindAnyObjectByType<XRInteractionManager>();

        if (clickRayInteractor == null || xrManager == null)
        {
            Debug.LogWarning("[EditorMouseClickTester] Hit an XR interactable but there is no Ray Interactor / Interaction Manager in the scene.");
            return false;
        }

        EndMouseXrSelect();
        bool isTeleportTarget = interactable is UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.BaseTeleportationInteractable;
        mouseActiveInteractor = isTeleportTarget ? (FindInteractor(true) ?? clickRayInteractor) : clickRayInteractor;
        xrManager.SelectEnter((IXRSelectInteractor)mouseActiveInteractor, interactable);
        if (interactable is IXRActivateInteractable act && mouseActiveInteractor is IXRActivateInteractor actor)
            act.OnActivated(new ActivateEventArgs { interactorObject = actor, interactableObject = act });
        selectedByMouse = interactable;

        if (logWorldHits)
            Debug.Log("[EditorMouseClickTester] XR SelectEnter on " + interactable.gameObject.name);

        return true;
    }

    bool TeleportTo(UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.BaseTeleportationInteractable tp)
    {
        var provider = tp.teleportationProvider != null ? tp.teleportationProvider
            : FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();
        if (provider == null)
        {
            Debug.LogWarning("[EditorMouseClickTester] Clicked a teleport spot but there is no Teleportation Provider in the scene.");
            return false;
        }
        var request = new UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportRequest();
        if (tp is UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor anchor && anchor.teleportAnchorTransform != null)
        {
            request.destinationPosition = anchor.teleportAnchorTransform.position;
            request.destinationRotation = anchor.teleportAnchorTransform.rotation;
            request.matchOrientation = anchor.matchOrientation;
        }
        else
        {
            request.destinationPosition = lastClickPoint;
            request.destinationRotation = Quaternion.identity;
            request.matchOrientation = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.MatchOrientation.None;
        }
        bool queued = provider.QueueTeleportRequest(request);
        if (logWorldHits)
            Debug.Log("[EditorMouseClickTester] Teleport to " + tp.name + (queued ? "" : " (provider refused)"));
        return queued;
    }

    void EndMouseXrSelect()
    {
        if (selectedByMouse == null || clickRayInteractor == null || xrManager == null)
        {
            selectedByMouse = null;
            return;
        }

        XRBaseInteractor who = mouseActiveInteractor != null ? mouseActiveInteractor : clickRayInteractor;
        if (selectedByMouse is IXRActivateInteractable act && who is IXRActivateInteractor actor)
            act.OnDeactivated(new DeactivateEventArgs { interactorObject = actor, interactableObject = act });
        if (who.interactablesSelected.Contains(selectedByMouse))
            xrManager.SelectExit((IXRSelectInteractor)who, selectedByMouse);
        selectedByMouse = null;
        mouseActiveInteractor = null;
    }

    // Nearest solid hit, or a trigger collider that belongs to a VR interactable
    // (e.g. a teleport spot). Plain trigger zones are ignored so they don't block clicks.
    static readonly RaycastHit[] clickHits = new RaycastHit[32];
    static Vector3 lastClickPoint;

    static bool RaycastForClick(Ray ray, out RaycastHit best)
    {
        best = default;
        int n = Physics.RaycastNonAlloc(ray, clickHits, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        float bestDist = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var h = clickHits[i];
            if (h.collider.isTrigger && h.collider.GetComponentInParent<XRBaseInteractable>() == null)
                continue;
            if (h.distance < bestDist) { bestDist = h.distance; best = h; found = true; }
        }
        if (found) lastClickPoint = best.point;
        return found;
    }

    XRBaseInteractor FindClickRayInteractor() { return FindInteractor(false); }

    // Any active controller interactor (ray or near-far). wantTeleport picks the teleport one.
    XRBaseInteractor FindInteractor(bool wantTeleport)
    {
        XRBaseInteractor[] all = FindObjectsOfType<XRBaseInteractor>();
        for (int i = 0; i < all.Length; i++)
        {
            XRBaseInteractor it = all[i];
            if (it == null || !it.isActiveAndEnabled || it is XRSocketInteractor)
                continue;
            bool isTeleport = it.name.IndexOf("Teleport", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (isTeleport == wantTeleport)
                return it;
        }
        // No live controller pointer (e.g. the XR rig turns controllers off when no headset
        // is connected). Use the editor controls' own invisible pointer instead.
        return OwnMouseInteractor();
    }

    XRRayInteractor ownMouseInteractor;

    XRRayInteractor OwnMouseInteractor()
    {
        if (ownMouseInteractor != null)
            return ownMouseInteractor;
        // Not parented, so no scale above it can stretch what it grabs. Removed with the controls (OnDestroy).
        var go = new GameObject("~EditorMouseInteractor");
        ownMouseInteractor = go.AddComponent<XRRayInteractor>();
        ownMouseInteractor.useForceGrab = false; // grabbed things stay where they are, like a far grab
        // This ray is for grabbing the model. UI clicks belong to the mouse tester.
        // Leaving UI on let a 3D select block later panel clicks.
        ownMouseInteractor.enableUIInteraction = false;
        // Reach the same things the controller's pointer reaches (so teleport spots don't light up under the mouse).
        foreach (var it in FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (it != ownMouseInteractor && !(it is XRSocketInteractor) && it.name.IndexOf("Teleport", System.StringComparison.OrdinalIgnoreCase) < 0
                && (it.GetType().Name == "NearFarInteractor" || (it is XRRayInteractor && !(it is XRGazeInteractor))))
            { ownMouseInteractor.interactionLayers = it.interactionLayers; break; }
        return ownMouseInteractor;
    }

    // Holds teleport spots for the arc when the rig has no live teleport ray. Points nowhere, so it never hovers.
    XRRayInteractor ownArcInteractor;

    XRRayInteractor OwnArcInteractor()
    {
        if (ownArcInteractor != null)
            return ownArcInteractor;
        var go = new GameObject("~EditorArcInteractor");
        ownArcInteractor = go.AddComponent<XRRayInteractor>();
        ownArcInteractor.raycastMask = 0;
        return ownArcInteractor;
    }

    void OnDestroy()
    {
        if (ownMouseInteractor != null) Destroy(ownMouseInteractor.gameObject);
        if (ownArcInteractor != null) Destroy(ownArcInteractor.gameObject);
    }

    // Point the controls' own pointer along the mouse.
    void AimPointer()
    {
        if (mainCamera == null) return;
        Ray ray = mainCamera.ScreenPointToRay(EditorControlsInput.MousePosition);
        var pointer = OwnMouseInteractor();
        pointer.transform.SetPositionAndRotation(ray.origin, Quaternion.LookRotation(ray.direction));
    }

    // Command key: grab what the pointer is on, through the toolkit's own held select (the same as the grip).
    void StartGrab()
    {
        EndGrab();
        var pointer = OwnMouseInteractor();
        IXRSelectInteractable target = null;
        foreach (var hovered in pointer.interactablesHovered)
            if (hovered is IXRSelectInteractable sel && !(hovered is UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.BaseTeleportationInteractable)) { target = sel; break; }
        if (target == null && mainCamera != null)
        {
            RaycastHit hit;
            if (RaycastForClick(mainCamera.ScreenPointToRay(EditorControlsInput.MousePosition), out hit))
            {
                var found = hit.collider.GetComponentInParent<XRBaseInteractable>();
                if (found != null && !(found is UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.BaseTeleportationInteractable))
                    target = found;
            }
        }
        if (target == null) return;
        RememberScaleBasis(target.transform);
        pointer.StartManualInteraction(target);
        held = target;
        if (logWorldHits)
            Debug.Log("[EditorMouseClickTester] Grab " + target.transform.name);
    }

    void EndGrab()
    {
        turningHeld = false;
        pendingScroll = 0f;
        held = null;
        if (ownMouseInteractor != null && ownMouseInteractor.isPerformingManualInteraction)
            ownMouseInteractor.EndManualInteraction();
    }

    /// Lets go of the editor grip. Put back uses this when Command is still down.
    public void EndEditorGrab()
    {
        EndGrab();
    }

    // Drop the toolkit's grab so it stops writing the pose, and remember this right click
    // is a turn, not a teleport aim.
    void BeginTurn()
    {
        turningHeld = true;
        suppressTeleportRelease = true;
        pendingScroll = 0f;
        if (ownMouseInteractor != null && ownMouseInteractor.isPerformingManualInteraction)
            ownMouseInteractor.EndManualInteraction();
    }

    // Left/right spins around the vertical. Up/down tilts around the view's sideways axis.
    // The spin is around the middle of the model. The root sits well below that middle,
    // and spinning around the root swings the head toward and away from you.
    void TurnHeld()
    {
        if (held == null || mainCamera == null)
            return;

        Vector2 pixels = EditorControlsInput.MouseDelta * 10f;
        if (pixels.sqrMagnitude < 0.0001f)
            return;

        float yaw = -pixels.x * grabTurnSpeed;
        float pitch = pixels.y * grabTurnSpeed;
        Quaternion spin = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, mainCamera.transform.right);
        Transform t = held.transform;
        Vector3 localCenter = ScaleCenter(t);
        Vector3 worldCenter = t.TransformPoint(localCenter);
        t.rotation = spin * t.rotation;
        t.position += worldCenter - t.TransformPoint(localCenter);
    }

    // One notch up grows it, one notch down shrinks it. Applied after the grab
    // writes its pose, so the new size does not push the model toward or away.
    void ScaleHeld()
    {
        float notches = EditorControlsInput.ScrollNotches;
        if (Mathf.Abs(notches) < 0.0001f)
            return;

        pendingScroll += notches;
    }

    // Runs after the toolkit's before-render pass (order 100).
    [BeforeRenderOrder(200)]
    void ApplyPendingScale()
    {
        if (held == null || Mathf.Abs(pendingScroll) < 0.0001f)
            return;

        float notches = pendingScroll;
        pendingScroll = 0f;

        Transform t = held.transform;
        RememberScaleBasis(t);
        float speed = Mathf.Max(0f, grabScaleSpeed);
        float factor = Mathf.Pow(1f + speed, notches);
        Vector3 next = t.localScale * factor;

        XRGeneralGrabTransformer transformer = t.GetComponent<XRGeneralGrabTransformer>();
        if (transformer != null && transformer.clampScaling)
        {
            Vector3 basis;
            scaleBasis.TryGetValue(t.GetInstanceID(), out basis);
            Vector3 min = basis * transformer.minimumScaleRatio;
            Vector3 max = basis * transformer.maximumScaleRatio;
            next = new Vector3(
                Mathf.Clamp(next.x, Smaller(min.x, max.x), Larger(min.x, max.x)),
                Mathf.Clamp(next.y, Smaller(min.y, max.y), Larger(min.y, max.y)),
                Mathf.Clamp(next.z, Smaller(min.z, max.z), Larger(min.z, max.z)));
        }

        Vector3 localCenter = ScaleCenter(t);
        Vector3 worldCenter = t.TransformPoint(localCenter);
        t.localScale = next;
        t.position += worldCenter - t.TransformPoint(localCenter);

        var grab = t.GetComponent<XRGrabInteractable>();
        if (grab == null || !grab.isSelected)
            return;

        // The hold point is a child, so a new size swings it toward or away from
        // the pointer and the grab follows. Park that point on the pointer, then
        // let the grab learn this pose. The model stays where it is.
        var interactor = grab.interactorsSelecting[0];
        var holdPoint = grab.GetAttachTransform(interactor);
        var pointerPoint = interactor.GetAttachTransform(grab);
        if (holdPoint != null && holdPoint != t && pointerPoint != null)
            holdPoint.SetPositionAndRotation(pointerPoint.position, pointerPoint.rotation);

        grab.SetTargetLocalScale(t.localScale);
        grab.SetTargetPose(new Pose(t.position, t.rotation));
        if (transformer != null)
            transformer.OnGrab(grab);

        var body = t.GetComponent<Rigidbody>();
        if (body != null)
            body.position = t.position;
    }

    static float Smaller(float a, float b)
    {
        return a < b ? a : b;
    }

    static float Larger(float a, float b)
    {
        return a > b ? a : b;
    }

    void RememberScaleBasis(Transform t)
    {
        int id = t.GetInstanceID();
        if (!scaleBasis.ContainsKey(id))
            scaleBasis[id] = t.localScale;
    }

    static Vector3 ScaleCenter(Transform t)
    {
        BoxCollider box = t.GetComponent<BoxCollider>();
        if (box != null)
            return box.center;
        return Vector3.zero;
    }

    // Right button is up, Command is still down: take hold again so the mouse moves it.
    void ResumeMove()
    {
        if (held == null)
            return;

        var pointer = OwnMouseInteractor();
        if (pointer.isPerformingManualInteraction)
            pointer.EndManualInteraction();
        if (pointer.interactionManager == null)
            pointer.interactionManager = xrManager != null ? xrManager : FindAnyObjectByType<XRInteractionManager>();
        pointer.StartManualInteraction(held);
    }

    // Arrow left/right: turn the rig by the snap turn amount, around the head, as the snap turn does.
    void SnapTurn(float direction)
    {
        var origin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin == null) return;
        var snap = FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider>();
        float amount = snap != null ? snap.turnAmount : 45f;
        origin.RotateAroundCameraUsingOriginUp(direction * amount);
    }
    
    private void HandleUIElementClick(GameObject uiElement)
    {
        if (uiElement == null || eventSystem == null)
            return;

        // A trackpad click has to run the button. Hitting the label and only logging it
        // leaves Start unpressed. Walk up from the text to the button.
        var pointerData = new PointerEventData(eventSystem);
        pointerData.button = PointerEventData.InputButton.Left;
        pointerData.position = EditorControlsInput.MousePosition;
        ExecuteEvents.ExecuteHierarchy(uiElement, pointerData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(uiElement, pointerData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(uiElement, pointerData, ExecuteEvents.pointerClickHandler);
        lastUiClick = GetUIPath(uiElement);

        IUIClickable uiClickable = uiElement.GetComponent<IUIClickable>();
        if (uiClickable != null)
            uiClickable.OnUIClick();
    }
    
    // Helper method to get the full UI path for better debugging
    private string GetUIPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
    
    void OnDrawGizmos()
    {
        #if UNITY_EDITOR
        // Draw if we're using gizmo lines and the line is still within its visible time
        // Or if we're holding mouse down
        bool shouldDraw = useGizmoLines && visualizeRaycast && 
                          (Time.time < rayVisibleUntil || isMouseDown);
                         
        if (shouldDraw)
        {
            // Draw the ray with the current color
            Gizmos.color = currentRayColor;
            Gizmos.DrawLine(lastRayStart, lastRayEnd);
            
            // Draw a small sphere at the hit point for better visibility
            Gizmos.DrawSphere(lastRayEnd, 0.02f);
        }
        #endif
    }
}

// Optional: a world object without an XR interactable can implement this to receive mouse clicks.
public interface IClickable
{
    void OnClick();
}

// Optional: a UI element can implement this to hear about mouse clicks.
public interface IUIClickable
{
    void OnUIClick();
}