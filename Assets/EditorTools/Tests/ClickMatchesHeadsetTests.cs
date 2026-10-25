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
}
