using UnityEngine;
using UnityEngine.InputSystem;

namespace SimpleXRDeviceSimulator.Sample
{
    /// <summary>
    /// The sample's A and B buttons, read from the right controller the way an app reads them: the cube lights up
    /// while A is held, and B hides it or brings it back. In the Editor, 1 is A and 2 is B.
    /// </summary>
    [RequireComponent(typeof(SampleColors))]
    public class SampleFaceButtons : MonoBehaviour
    {
        [SerializeField] Color litColor = Color.white;

        readonly InputAction aButton = new InputAction("A", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        readonly InputAction bButton = new InputAction("B", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");

        SampleColors colors;
        Renderer body;
        Color startColor;

        void Awake()
        {
            colors = GetComponent<SampleColors>();
            body = GetComponent<Renderer>();
            startColor = colors.Color;
        }

        void OnEnable()
        {
            aButton.Enable();
            bButton.Enable();
        }

        void OnDisable()
        {
            aButton.Disable();
            bButton.Disable();
        }

        void Update()
        {
            if (aButton.WasPressedThisFrame())
                colors.SetColor(litColor);
            if (aButton.WasReleasedThisFrame())
                colors.SetColor(startColor);
            if (bButton.WasPressedThisFrame())
                body.enabled = !body.enabled;
        }
    }
}
