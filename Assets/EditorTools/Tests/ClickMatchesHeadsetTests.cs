// Play-mode checks that a laptop click behaves like the headset trigger. Run them in Window > General > Test Runner > PlayMode.
// They live outside the package, so they never install into anyone else's project.
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XREditorControls;

public class ClickMatchesHeadsetTests
{
    const string ScenePath = "Assets/XREditorControlsSample/Basic Scene.unity";

    IEnumerator Load()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 30; i++) yield return null;
    }

    static Button FindReset()
    {
        foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            if (b.name.ToLower().Contains("reset") || b.GetComponentInChildren<Text>()?.text.ToLower().Contains("reset") == true
                || b.GetComponentInChildren<TMPro.TMP_Text>()?.text.ToLower().Contains("reset") == true)
                return b;
        return null;
    }

    static IEnumerator ClickAt(Vector2 screen, int holdFrames = 3)
    {
        EditorScriptedPointer.Active = true;
        EditorScriptedPointer.Position = screen;
        for (int i = 0; i < 5; i++) yield return null;   // let the ray aim and hover
        EditorScriptedPointer.LeftPressed = true; EditorScriptedPointer.LeftHeld = true;
        yield return null;
        EditorScriptedPointer.LeftPressed = false;
        for (int i = 0; i < holdFrames; i++) yield return null;
        EditorScriptedPointer.LeftHeld = false; EditorScriptedPointer.LeftReleased = true;
        yield return null;
        EditorScriptedPointer.LeftReleased = false;
        for (int i = 0; i < 5; i++) yield return null;
        EditorScriptedPointer.Clear();
    }

    static Vector2 ScreenPoint(Button b)
    {
        var rt = (RectTransform)b.transform;
        Vector3 center = rt.TransformPoint(rt.rect.center);
        return Camera.main.WorldToScreenPoint(center);
    }

    int clicks;

    [UnityTest]
    public IEnumerator ResetClicksWithXRUIInputModule()
    {
        yield return Load();
        Button reset = FindReset();
        Assert.IsNotNull(reset, "no Reset button");
        clicks = 0; reset.onClick.AddListener(() => clicks++);
        Vector2 p = ScreenPoint(reset);
        Debug.Log($"[ZTEST] reset at {p}, screen {Screen.width}x{Screen.height}");
        yield return ClickAt(p);
        Assert.AreEqual(1, clicks, "one click should press Reset exactly once");
        yield return ClickAt(p, 0);   // same-frame-ish quick click
        Assert.AreEqual(2, clicks, "a quick click should also press it once");
    }

    [UnityTest]
    public IEnumerator ResetDoesNothingWithoutXRUIInputModule()
    {
        yield return Load();
        foreach (XRUIInputModule m in Object.FindObjectsByType<XRUIInputModule>(FindObjectsSortMode.None))
            Object.Destroy(m);
        yield return null;
        Button reset = FindReset();
        clicks = 0; reset.onClick.AddListener(() => clicks++);
        yield return ClickAt(ScreenPoint(reset));
        Assert.AreEqual(0, clicks, "without the XR UI Input Module the headset can't press it, so the laptop mustn't either");
    }

    [UnityTest]
    public IEnumerator ClickOnEmptySpaceDoesNothing()
    {
        yield return Load();
        Button reset = FindReset();
        clicks = 0; reset.onClick.AddListener(() => clicks++);
        yield return ClickAt(new Vector2(5, 5));
        Assert.AreEqual(0, clicks);
    }

    [UnityTest]
    public IEnumerator CommandGrabsTheCube()
    {
        yield return Load();
        var grab = Object.FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        Assert.IsNotNull(grab, "no grabbable in the sample");
        Vector2 p = Camera.main.WorldToScreenPoint(grab.GetComponent<Collider>().bounds.center);
        EditorScriptedPointer.Active = true; EditorScriptedPointer.Position = p;
        for (int i = 0; i < 5; i++) yield return null;
        EditorScriptedPointer.CommandPressed = true; EditorScriptedPointer.CommandHeld = true;
        yield return null;
        EditorScriptedPointer.CommandPressed = false;
        for (int i = 0; i < 3; i++) yield return null;
        bool held = grab.isSelected;
        EditorScriptedPointer.CommandHeld = false; EditorScriptedPointer.CommandReleased = true;
        yield return null;
        EditorScriptedPointer.Clear();
        yield return null;
        Assert.IsTrue(held, "Command on the cube should grab it");
        Assert.IsFalse(grab.isSelected, "letting go of Command should drop it");
    }

    [UnityTest]
    public IEnumerator OneAndTwoPressTheRightControllersAAndB()
    {
        yield return Load();
        var a = new UnityEngine.InputSystem.InputAction("A", UnityEngine.InputSystem.InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        var b = new UnityEngine.InputSystem.InputAction("B", UnityEngine.InputSystem.InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        a.Enable(); b.Enable();
        var cube = GameObject.Find("Grab Cube").GetComponent<Renderer>();
        Assert.IsTrue(cube.enabled);

        EditorScriptedPointer.Active = true;
        EditorScriptedPointer.Position = new Vector2(5, 5);
        EditorScriptedPointer.PrimaryHeld = true;
        for (int i = 0; i < 3; i++) yield return null;
        Assert.IsTrue(a.IsPressed(), "holding 1 should press A");
        Assert.IsFalse(b.IsPressed());
        EditorScriptedPointer.PrimaryHeld = false;
        for (int i = 0; i < 3; i++) yield return null;
        Assert.IsFalse(a.IsPressed(), "letting go of 1 should release A");

        EditorScriptedPointer.SecondaryHeld = true;
        for (int i = 0; i < 3; i++) yield return null;
        Assert.IsTrue(b.IsPressed(), "holding 2 should press B");
        EditorScriptedPointer.SecondaryHeld = false;
        for (int i = 0; i < 3; i++) yield return null;
        Assert.IsFalse(cube.enabled, "B should hide the sample cube");

        // The stand-in controller must not get in the way of the trigger.
        Button reset = FindReset();
        clicks = 0; reset.onClick.AddListener(() => clicks++);
        yield return ClickAt(ScreenPoint(reset));
        Assert.AreEqual(1, clicks, "Reset still clicks after A and B were used");
        a.Disable(); b.Disable();
    }

    // Found in a real app: a click on an item you're pointing at must fire its trigger (activate) event,
    // because the headset's rig lets the trigger work on hovered items.
    [UnityTest]
    public IEnumerator ClickFiresTheTriggerOnAPointedAtItem()
    {
        yield return Load();
        foreach (var rig in Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            rig.allowHoveredActivate = true;
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = "Wearable Item";
        item.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 1.5f + Vector3.left * 0.6f;
        item.transform.localScale = Vector3.one * 0.3f;
        var simple = item.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        int activated = 0;
        simple.activated.AddListener(_ => activated++);
        // Wait until the pointer is on the item, so this checks the trigger, not how fast hovering starts.
        Vector2 at = Camera.main.WorldToScreenPoint(item.transform.position);
        EditorScriptedPointer.Active = true; EditorScriptedPointer.Position = at;
        for (int i = 0; i < 120 && !simple.isHovered; i++) yield return null;
        Assert.IsTrue(simple.isHovered, "the pointer never reached the item");
        yield return ClickAt(at);
        Assert.AreEqual(1, activated, "a click on a pointed-at item should pull the trigger on it once, like the headset");
    }

    // Found in a real app: one click pressed Next twice. The scene's XR UI Input Module had UI actions assigned
    // (as Starter Assets does), so it read the real mouse as a second pointer on top of the controller ray.
    [UnityTest]
    public IEnumerator OneClickPressesOnceWhenTheModuleHasMouseActions()
    {
        yield return Load();
        // Headless test runs have no window focus, and by default the Input System drops a mouse without focus.
        var settings = UnityEngine.InputSystem.InputSystem.settings;
        var oldBackground = settings.backgroundBehavior;
        var oldEditor = settings.editorInputBehaviorInPlayMode;
        settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        var asset = ScriptableObject.CreateInstance<UnityEngine.InputSystem.InputActionAsset>();
        var map = UnityEngine.InputSystem.InputActionSetupExtensions.AddActionMap(asset, "UI");
        var point = UnityEngine.InputSystem.InputActionSetupExtensions.AddAction(map, "Point", UnityEngine.InputSystem.InputActionType.PassThrough, "<Mouse>/position");
        var click = UnityEngine.InputSystem.InputActionSetupExtensions.AddAction(map, "Click", UnityEngine.InputSystem.InputActionType.PassThrough, "<Mouse>/leftButton");
        var module = Object.FindAnyObjectByType<XRUIInputModule>();
        module.pointAction = UnityEngine.InputSystem.InputActionReference.Create(point);
        module.leftClickAction = UnityEngine.InputSystem.InputActionReference.Create(click);
        // The module only switches to its assigned actions when it turns on, as it does when a real scene loads.
        module.enabled = false; module.enabled = true;
        yield return null;

        Button reset = FindReset();
        // Some panels have a plain Graphic Raycaster next to the tracked-device one, so a mouse pointer can hit it too.
        Canvas canvas = reset.GetComponentInParent<Canvas>().rootCanvas;
        if (canvas.GetComponent<GraphicRaycaster>() == null || canvas.GetComponent<GraphicRaycaster>() is TrackedDeviceGraphicRaycaster)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        canvas.worldCamera = Camera.main;
        yield return null;
        clicks = 0; reset.onClick.AddListener(() => clicks++);
        Vector2 p = ScreenPoint(reset);
        // The real mouse goes down with the click, as it does with a real click.
        UnityEngine.InputSystem.LowLevel.InputState.Change(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = p }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
        yield return null;
        yield return ClickAt(p);
        UnityEngine.InputSystem.LowLevel.InputState.Change(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = p });
        for (int i = 0; i < 5; i++) yield return null;
        UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
        Object.Destroy(asset);
        settings.backgroundBehavior = oldBackground;
        settings.editorInputBehaviorInPlayMode = oldEditor;
        Assert.AreEqual(1, clicks, "one click must press the button once, not once for the ray and again for the mouse");
    }

    // Found in a real app: Some rigs grab with the trigger (Select wired to Activate), so their menus move with the
    // trigger in the headset. A left click must then grab and move them too, and let go on release.
    [UnityTest]
    public IEnumerator ClickGrabsWhenTheRigGrabsWithTheTrigger()
    {
        yield return Load();
        var asset = ScriptableObject.CreateInstance<UnityEngine.InputSystem.InputActionAsset>();
        var map = UnityEngine.InputSystem.InputActionSetupExtensions.AddActionMap(asset, "Right Interaction");
        UnityEngine.InputSystem.InputActionSetupExtensions.AddAction(map, "Activate", UnityEngine.InputSystem.InputActionType.Button, "<XRController>{RightHand}/trigger");
        var triggerRef = UnityEngine.InputSystem.InputActionReference.Create(map.FindAction("Activate"));
        // A hand pointer, switched off as the rig's are with no headset connected.
        var hand = new GameObject("Trigger-Grab Hand");
        hand.SetActive(false);
        var rig = hand.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
        var reader = UnityEngine.XR.Interaction.Toolkit.Inputs.Readers.XRInputButtonReader.InputSourceMode.InputActionReference;
        rig.selectInput.inputSourceMode = reader;
        rig.selectInput.inputActionReferencePerformed = triggerRef;
        rig.activateInput.inputSourceMode = reader;
        rig.activateInput.inputActionReferencePerformed = triggerRef;

        var grab = Object.FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        Vector2 at = Camera.main.WorldToScreenPoint(grab.GetComponent<Collider>().bounds.center);
        EditorScriptedPointer.Active = true; EditorScriptedPointer.Position = at;
        for (int i = 0; i < 120 && !grab.isHovered; i++) yield return null;
        Assert.IsTrue(grab.isHovered, "the pointer never reached the cube");
        Vector3 start = grab.transform.position;
        EditorScriptedPointer.LeftPressed = true; EditorScriptedPointer.LeftHeld = true;
        yield return null;
        EditorScriptedPointer.LeftPressed = false;
        for (int i = 0; i < 3; i++) yield return null;
        bool held = grab.isSelected;
        EditorScriptedPointer.Position = at + new Vector2(80, 0);
        for (int i = 0; i < 20; i++) yield return null;
        bool moved = Vector3.Distance(start, grab.transform.position) > 0.05f;
        EditorScriptedPointer.LeftHeld = false; EditorScriptedPointer.LeftReleased = true;
        yield return null;
        EditorScriptedPointer.LeftReleased = false;
        for (int i = 0; i < 3; i++) yield return null;
        EditorScriptedPointer.Clear();
        Object.Destroy(asset);
        Object.Destroy(hand);
        Assert.IsTrue(held, "a left click should grab when the rig grabs with the trigger");
        Assert.IsTrue(moved, "dragging with the click held should move it");
        Assert.IsFalse(grab.isSelected, "letting go of the click should drop it");
    }

    [UnityTest]
    public IEnumerator ClickDoesNotGrabWhenTheRigGrabsWithTheGrip()
    {
        yield return Load();
        var grab = Object.FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        Vector2 at = Camera.main.WorldToScreenPoint(grab.GetComponent<Collider>().bounds.center);
        EditorScriptedPointer.Active = true; EditorScriptedPointer.Position = at;
        for (int i = 0; i < 120 && !grab.isHovered; i++) yield return null;
        EditorScriptedPointer.LeftPressed = true; EditorScriptedPointer.LeftHeld = true;
        yield return null;
        EditorScriptedPointer.LeftPressed = false;
        for (int i = 0; i < 3; i++) yield return null;
        bool held = grab.isSelected;
        EditorScriptedPointer.LeftHeld = false; EditorScriptedPointer.LeftReleased = true;
        yield return null;
        EditorScriptedPointer.Clear();
        Assert.IsFalse(held, "on a rig that grabs with the grip, a click must not grab (Command does)");
    }

    // Found in a real app: a label-only panel without a Tracked Device Graphic Raycaster is not a problem; a panel with a button is.
    [UnityTest]
    public IEnumerator PanelWarningOnlyForPanelsWithButtons()
    {
        yield return Load();
        GameObject MakePanel(string name, bool withButton)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var child = new GameObject("Content", typeof(RectTransform), typeof(Text));
            child.transform.SetParent(go.transform, false);
            if (withButton) child.AddComponent<Button>();
            return go;
        }
        MakePanel("Label Only Panel", false);
        MakePanel("Button Panel", true);
        yield return null;

        var tester = Object.FindAnyObjectByType<EditorMouseClickTester>();
        Assert.IsNotNull(tester, "no EditorMouseClickTester in the sample");
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        object ui = typeof(EditorMouseClickTester).GetField("ui", flags).GetValue(tester);
        ui.GetType().GetMethod("CheckScene").Invoke(ui, null);
        string problem = (string)ui.GetType().GetProperty("Problem").GetValue(ui);
        Assert.IsTrue(problem.Contains("Button Panel"), "a panel with a button and no tracked-device raycaster should be named");
        Assert.IsFalse(problem.Contains("Label Only Panel"), "a panel with nothing to press should not be named");
    }

    [UnityTest]
    public IEnumerator CameraStaysAtHeadHeightWithoutAHeadset()
    {
        yield return Load();
        Assert.AreEqual(1.6f, Camera.main.transform.position.y, 0.01f, "the head tracking must not pull the camera to the floor with no headset");
    }
}
