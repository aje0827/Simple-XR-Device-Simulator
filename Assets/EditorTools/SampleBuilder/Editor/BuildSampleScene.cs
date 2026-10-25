// Builds the package's "Basic Scene" sample. Not part of the package: it lives outside EditorControls.
// Run Tools > XR Editor Controls > Build Sample Scene, then move Assets/XREditorControlsSample into
// Assets/EditorTools/EditorControls/Samples~/Basic Scene (with its .meta files).
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
using XREditorControls;
using XREditorControls.Sample;

static class BuildSampleScene
{
    const string Folder = "Assets/XREditorControlsSample";
    const string ControlsPrefab = "Assets/EditorTools/EditorControls/~EditorControls.prefab";

    static Material defaultMaterial;

    [MenuItem("Tools/XR Editor Controls/Build Sample Scene")]
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
        cube.AddComponent<XRGrabInteractable>();
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
        rect.sizeDelta = new Vector2(600f, 320f);
        rect.position = new Vector3(0f, 1.55f, 2.5f);
        rect.localScale = Vector3.one * 0.002f;
        panel.AddComponent<Image>().color = new Color(0.1f, 0.11f, 0.14f, 0.92f);

        Label(panel.transform, "Title", "XR Editor Controls", 40, new Vector2(0f, 100f), new Vector2(560f, 60f));
        Label(panel.transform, "Hint", "Grab the cube with Command, then click Reset", 26, new Vector2(0f, 40f), new Vector2(560f, 50f));

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
        r.anchoredPosition = new Vector2(0f, -70f);
        Label(buttonObject.transform, "Label", "Reset", 34, Vector2.zero, new Vector2(220f, 90f));
        UnityEventTools.AddPersistentListener(button.onClick, actions.ResetCube);

        var controls = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ControlsPrefab));
        controls.GetComponent<EditorMouseClickTester>().showInputLabel = true;

        EditorSceneManager.SaveScene(scene, Folder + "/Basic Scene.unity");
        Debug.Log("Saved " + Folder + "/Basic Scene.unity");
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
