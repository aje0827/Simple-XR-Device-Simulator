using UnityEngine;
using UnityEngine.Rendering;

namespace XREditorControls.Sample
{
    /// <summary>
    /// Gives an object the render pipeline's own default material and a color, so the sample looks right in the
    /// Built-In pipeline, URP and HDRP without shipping a material for each.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public class SampleColors : MonoBehaviour
    {
        [SerializeField] Color color = Color.white;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");

        MaterialPropertyBlock block;

        void OnEnable()
        {
            var r = GetComponent<Renderer>();
            Material standard = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.defaultMaterial
                : null;
            if (standard != null)
                r.sharedMaterial = standard;
            Apply();
        }

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
