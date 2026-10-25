// Simple XR Device Simulator: https://github.com/aje0827/Simple-XR-Device-Simulator
// Copied into a project? Change the original in this repo, not the copy.
namespace XREditorControls
{
    /// <summary>No longer called: a click is the trigger now, so it does only what the trigger does in the headset.</summary>
    [System.Obsolete("Simple XR Device Simulator no longer calls OnClick. A click is the controller's trigger; listen for the XR interactable's select or activate instead, as the headset does.")]
    public interface IClickable
    {
        void OnClick();
    }

    /// <summary>No longer called: UI is pressed through the XR UI Input Module now, as in the headset.</summary>
    [System.Obsolete("Simple XR Device Simulator no longer calls OnUIClick. UI is pressed through the XR UI Input Module; use the control's own onClick instead.")]
    public interface IUIClickable
    {
        void OnUIClick();
    }
}
