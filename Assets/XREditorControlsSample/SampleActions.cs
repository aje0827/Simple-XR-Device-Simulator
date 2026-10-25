using UnityEngine;
using UnityEngine.UI;

namespace XREditorControls.Sample
{
    /// <summary>
    /// What the sample's buttons and hotspot do, so each click shows on screen.
    /// </summary>
    public class SampleActions : MonoBehaviour
    {
        [SerializeField] SampleColors cube;
        [SerializeField] SampleColors hotspot;
        [SerializeField] Text status;

        static readonly Color[] Colors = { new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.6f, 0.95f), new Color(0.35f, 0.8f, 0.45f) };
        static readonly string[] Names = { "Red", "Blue", "Green" };

        int clicks;
        bool hotspotOn;

        public void SetCubeColor(int index)
        {
            cube.SetColor(Colors[index]);
            Show(Names[index] + " pressed");
        }

        public void ToggleHotspot()
        {
            hotspotOn = !hotspotOn;
            hotspot.SetColor(hotspotOn ? new Color(1f, 0.8f, 0.2f) : Color.white);
            Show("Hotspot " + (hotspotOn ? "on" : "off"));
        }

        void Show(string message)
        {
            clicks++;
            if (status != null)
                status.text = message + "  (" + clicks + ")";
        }
    }
}
