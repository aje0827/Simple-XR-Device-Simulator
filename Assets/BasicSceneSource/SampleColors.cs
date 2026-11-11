using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleXRDeviceSimulator.Sample
{
    /// <summary>
    /// Gives an object the render pipeline's own default material and a color, so the sample looks right in the
    /// Built-In pipeline, URP and HDRP from one scene. The material is set every time the object is enabled, so a
    /// material saved from another pipeline is replaced.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public class SampleColors : MonoBehaviour
    {
        [SerializeField] Color color = Color.white;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");

        static Material builtInMaterial;

        MaterialPropertyBlock block;

        void OnEnable()
        {
            GetComponent<Renderer>().sharedMaterial = DefaultMaterial();
            Apply();
        }

        static Material DefaultMaterial()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
                return GraphicsSettings.currentRenderPipeline.defaultMaterial;
            if (builtInMaterial == null)
                builtInMaterial = new Material(Shader.Find("Standard")) { hideFlags = HideFlags.DontSave };
            return builtInMaterial;
        }

        public Color Color => color;

        public void SetColor(Color next)
        {
            color = next;
            Apply();
        }

        void OnValidate() => Apply();

        void Apply()
        {
            var r = GetComponent<Renderer>();
            if (r == null)
                return;
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(LegacyColor, color);
            r.SetPropertyBlock(block);
        }
    }
}
