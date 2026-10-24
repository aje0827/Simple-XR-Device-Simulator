// EditorTools-Unity editor controls: https://github.com/aje0827/EditorTools-Unity
// Copied into a project? Change the original in this repo, not the copy.
namespace XREditorControls
{
    /// <summary>Optional: a world object with no XR interactable can implement this to receive mouse clicks.</summary>
    public interface IClickable
    {
        void OnClick();
    }

    /// <summary>Optional: a UI element can implement this to hear when the mouse clicks it.</summary>
    public interface IUIClickable
    {
        void OnUIClick();
    }
}
