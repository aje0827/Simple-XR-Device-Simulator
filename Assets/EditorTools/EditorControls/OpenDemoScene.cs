// Simple XR Device Simulator: https://github.com/aje0827/Simple-XR-Device-Simulator
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace XREditorControls
{
    /// <summary>
    /// One menu item that opens the scene from the README's GIFs: the XR Interaction Toolkit's own demo scene
    /// (from its Starter Assets sample, imported if it isn't yet) with the editor controls added.
    /// </summary>
    internal static class OpenDemoScene
    {
        const string ToolkitPackage = "com.unity.xr.interaction.toolkit";
        const string ControlsPrefab = "Packages/com.albertelias.simple-xr-device-simulator/~EditorControls.prefab";
        const string ControlsPrefabCopied = "Assets/EditorTools/EditorControls/~EditorControls.prefab";

        [MenuItem("Tools/Simple XR Device Simulator/Open Demo Scene")]
        static void Open()
        {
            Sample starterAssets = default;
            bool found = false;
            foreach (Sample sample in Sample.FindByPackage(ToolkitPackage, null))
            {
                if (sample.displayName == "Starter Assets")
                {
                    starterAssets = sample;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                EditorUtility.DisplayDialog("Simple XR Device Simulator", "Couldn't find the XR Interaction Toolkit's Starter Assets sample.", "OK");
                return;
            }

            if (!starterAssets.isImported)
            {
                if (!EditorUtility.DisplayDialog("Simple XR Device Simulator",
                        "The demo is the XR Interaction Toolkit's own DemoScene, from its Starter Assets sample. Import Starter Assets now?",
                        "Import", "Cancel"))
                    return;
                starterAssets.Import();
                AssetDatabase.Refresh();
            }

            string scenePath = ToProjectPath(Path.Combine(starterAssets.importPath, "DemoScene.unity"));
            if (!File.Exists(scenePath))
            {
                EditorUtility.DisplayDialog("Simple XR Device Simulator", "Starter Assets is imported, but DemoScene wasn't found at " + scenePath, "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(scenePath);
            if (Object.FindAnyObjectByType<EditorMouseClickTester>() == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ControlsPrefab)
                    ?? AssetDatabase.LoadAssetAtPath<GameObject>(ControlsPrefabCopied);
                if (prefab != null)
                {
                    var controls = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    controls.GetComponent<EditorMouseClickTester>().showInputLabel = true;
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }

        static string ToProjectPath(string absolute)
        {
            string root = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/') + "/";
            string path = absolute.Replace('\\', '/');
            return path.StartsWith(root) ? path.Substring(root.Length) : path;
        }
    }
}
#endif
