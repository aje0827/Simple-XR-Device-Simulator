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
    /// The menu item that opens this package's Basic Scene sample, importing it first if it isn't imported yet.
    /// </summary>
    internal static class OpenDemoScene
    {
        const string ThisPackage = "com.albertelias.simple-xr-device-simulator";

        [MenuItem("Tools/Simple XR Device Simulator/Open Basic Scene")]
        static void OpenBasicScene()
        {
            foreach (Sample sample in Sample.FindByPackage(ThisPackage, null))
            {
                if (sample.displayName != "Basic Scene")
                    continue;
                if (!sample.isImported)
                {
                    sample.Import();
                    AssetDatabase.Refresh();
                }
                string scenePath = ToProjectPath(Path.Combine(sample.importPath, "Basic Scene.unity"));
                if (File.Exists(scenePath) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(scenePath);
                return;
            }
            EditorUtility.DisplayDialog("Simple XR Device Simulator", "The Basic Scene sample is only there when the package is installed through Package Manager.", "OK");
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
