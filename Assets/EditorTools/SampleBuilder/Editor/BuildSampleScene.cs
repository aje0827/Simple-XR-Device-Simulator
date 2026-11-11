// Builds the package's "Basic Scene" sample. Not part of the package: it lives outside the package folder.
// Run Tools > Simple XR Device Simulator > Build Sample Scene. It saves the scene in Assets/BasicSceneSource and
// copies that folder, .meta files included, into the package's Samples~/Basic Scene for the Samples tab.
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using SimpleXRDeviceSimulator;
using SimpleXRDeviceSimulator.Sample;

static class BuildSampleScene
{
    const string Folder = "Assets/BasicSceneSource";
    const string PackageSample = "Assets/SimpleXRDeviceSimulator/Samples~/Basic Scene";
    const string ControlsPrefab = "Assets/SimpleXRDeviceSimulator/Simple XR Device Simulator.prefab";

    static Material defaultMaterial;

    [MenuItem("Tools/Simple XR Device Simulator/Build Sample Scene")]
    static void Build()
    {
        defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Shape(PrimitiveType.Plane, "Floor", Vector3.zero, new Vector3(2f, 1f, 2f), new Color(0.55f, 0.58f, 0.62f));

        new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<XRUIInputModule>();

        // A plain XR Origin: camera, one controller with a ray, teleport and snap turn.
        var rig = new GameObject("XR Origin");
        var origin = rig.AddComponent<XROrigin>();
        var offset = new GameObject("Camera Offset");
        offset.transform.SetParent(rig.transform, false);
        offset.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        cameraObject.transform.SetParent(offset.transform, false);
        var camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.01f;
        cameraObject.AddComponent<AudioListener>();
        // Moves the camera with the headset. Without it a headset can't look around, and the XR Origin warns on Play.
        var head = cameraObject.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        head.positionInput = new UnityEngine.InputSystem.InputActionProperty(new UnityEngine.InputSystem.InputAction("Head Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
        head.rotationInput = new UnityEngine.InputSystem.InputActionProperty(new UnityEngine.InputSystem.InputAction("Head Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
        head.trackingStateInput = new UnityEngine.InputSystem.InputActionProperty(new UnityEngine.InputSystem.InputAction("Head Tracking State", binding: "<XRHMD>/trackingState", expectedControlType: "Integer"));
        origin.Camera = camera;
        origin.CameraFloorOffsetObject = offset;
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
        origin.CameraYOffset = 1.6f;

        var controller = new GameObject("Right Controller");
        controller.transform.SetParent(offset.transform, false);
        controller.transform.localPosition = new Vector3(0.2f, -0.4f, 0.3f);
        controller.AddComponent<XRRayInteractor>();

        var locomotion = new GameObject("Locomotion");
        locomotion.transform.SetParent(rig.transform, false);
        locomotion.AddComponent<LocomotionMediator>();
        locomotion.AddComponent<XRBodyTransformer>();
        Child(locomotion, "Teleportation").AddComponent<TeleportationProvider>();
        Child(locomotion, "Snap Turn").AddComponent<SnapTurnProvider>();

        // A table with a cube to grab.
        Shape(PrimitiveType.Cube, "Table", new Vector3(0f, 0.375f, 1.9f), new Vector3(1.4f, 0.75f, 0.7f), new Color(0.42f, 0.33f, 0.25f));
        var cube = Shape(PrimitiveType.Cube, "Grab Cube", new Vector3(0f, 0.85f, 1.9f), Vector3.one * 0.2f, new Color(0.95f, 0.6f, 0.2f));
        var body = cube.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        // A kinematic cube can't be thrown; without this, every drop warns in the Console.
        cube.AddComponent<XRGrabInteractable>().throwOnDetach = false;
        cube.AddComponent<SampleFaceButtons>();
        var transformer = cube.AddComponent<XRGeneralGrabTransformer>();
        transformer.clampScaling = true;
        transformer.minimumScaleRatio = 0.25f;
        transformer.maximumScaleRatio = 4f;

        // Two spots beside the table, and one where you start so you can come back.
        Pad("Teleport Spot Left", new Vector3(-1.4f, 0.01f, 2.1f), 90f);
        Pad("Teleport Spot Right", new Vector3(1.4f, 0.01f, 2.1f), -90f);
        Pad("Teleport Spot Start", new Vector3(0f, 0.01f, 0f), 0f);

        // A world-space panel, the way XR apps show UI.
        var panel = new GameObject("Button Panel", typeof(RectTransform));
        var canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        panel.AddComponent<CanvasScaler>();
        panel.AddComponent<GraphicRaycaster>();
        panel.AddComponent<TrackedDeviceGraphicRaycaster>();
        var rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(900f, 600f);
        rect.position = new Vector3(0f, 1.55f, 2.5f);
        rect.localScale = Vector3.one * 0.002f;
        panel.AddComponent<Image>().color = new Color(0.1f, 0.11f, 0.14f, 0.92f);

        Label(panel.transform, "Title", "Simple XR Device Simulator", 40, new Vector2(0f, 250f), new Vector2(720f, 60f));

        // The controls: what it does on the left, the keys on the right, both left aligned. Mac key first, then Windows.
        string[,] help =
        {
            { "Look around", "Hold Option / Alt + move" },
            { "Trigger", "Left click" },
            { "Grab what you point at", "Hold Command / Windows" },
            { "Rotate it (add Shift to roll)", "Command / Windows + Control + move" },
            { "Scale it", "Command / Windows + scroll" },
            { "Teleport", "Hold right click, release" },
            { "Snap turn", "Left / Right arrows" },
            { "A button (lights the cube)", "1" },
            { "B button (hides the cube)", "2" },
        };
        for (int i = 0; i < help.GetLength(0); i++)
        {
            float y = 200f - 34f * i;
            Label(panel.transform, "Does " + i, help[i, 0], 24, new Vector2(-220f, y), new Vector2(290f, 36f)).alignment = TextAnchor.MiddleLeft;
            var keys = Label(panel.transform, "Keys " + i, help[i, 1], 24, new Vector2(175f, y), new Vector2(440f, 36f));
            keys.alignment = TextAnchor.MiddleLeft;
            keys.color = new Color(0.75f, 0.78f, 0.85f);
        }

        Label(panel.transform, "Hint", "Grab the cube, then click Reset to put it back", 26, new Vector2(0f, -150f), new Vector2(720f, 50f));

        var actions = panel.AddComponent<SampleActions>();
        var so = new SerializedObject(actions);
        so.FindProperty("cube").objectReferenceValue = cube.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        var buttonObject = new GameObject("Reset Button", typeof(RectTransform));
        buttonObject.transform.SetParent(panel.transform, false);
        buttonObject.AddComponent<Image>().color = new Color(0.3f, 0.6f, 0.95f);
        var button = buttonObject.AddComponent<Button>();
        var r = buttonObject.GetComponent<RectTransform>();
        r.sizeDelta = new Vector2(220f, 90f);
        r.anchoredPosition = new Vector2(0f, -225f);
        Label(buttonObject.transform, "Label", "Reset", 34, Vector2.zero, new Vector2(220f, 90f));
        UnityEventTools.AddPersistentListener(button.onClick, actions.ResetCube);

        var controls = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ControlsPrefab));
        controls.GetComponent<EditorMouseClickTester>().showInputLabel = true;

        EditorSceneManager.SaveScene(scene, Folder + "/Basic Scene.unity");
        CopyToPackage();
        Debug.Log("Saved " + Folder + "/Basic Scene.unity and copied it to " + PackageSample);
    }

    static void CopyToPackage()
    {
        if (System.IO.Directory.Exists(PackageSample))
            System.IO.Directory.Delete(PackageSample, true);
        System.IO.Directory.CreateDirectory(PackageSample);
        foreach (var file in System.IO.Directory.GetFiles(Folder))
            System.IO.File.Copy(file, System.IO.Path.Combine(PackageSample, System.IO.Path.GetFileName(file)));
    }

    static GameObject Child(GameObject parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    static GameObject Shape(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
    {
        var shape = GameObject.CreatePrimitive(type);
        shape.name = name;
        shape.transform.position = position;
        shape.transform.localScale = scale;
        shape.GetComponent<Renderer>().sharedMaterial = defaultMaterial;
        var colors = shape.AddComponent<SampleColors>();
        var so = new SerializedObject(colors);
        so.FindProperty("color").colorValue = color;
        so.ApplyModifiedPropertiesWithoutUndo();
        return shape;
    }

    static void Pad(string name, Vector3 position, float yaw)
    {
        var pad = Shape(PrimitiveType.Cylinder, name, position, new Vector3(0.8f, 0.02f, 0.8f), new Color(0.3f, 0.6f, 0.95f));
        pad.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        pad.AddComponent<TeleportationAnchor>().matchOrientation = MatchOrientation.TargetUpAndForward;
    }

    static Text Label(Transform parent, string name, string text, int size, Vector2 position, Vector2 sizeDelta)
    {
        var labelObject = new GameObject(name, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        var label = labelObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        var r = labelObject.GetComponent<RectTransform>();
        r.anchoredPosition = position;
        r.sizeDelta = sizeDelta;
        return label;
    }
}
