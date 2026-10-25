// EditorTools-Unity editor controls: https://github.com/aje0827/EditorTools-Unity
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR
using UnityEngine;

namespace XREditorControls
{
    /// <summary>
    /// A label in the corner of the Game view naming the control in use, like "⌘ Grab" or "⌘ Ctrl Turn", plus a
    /// drawn cursor and a ring on each click, so a recording shows what the hands are doing. Unity's recorder
    /// doesn't capture the real cursor. Off by default; turn it on for demos and recordings.
    /// </summary>
    internal sealed class MouseInputLabel
    {
        // Long enough to read a quick click, short enough not to hang over the next action.
        const float LingerSeconds = 0.7f;
        const float RingSeconds = 0.5f;

        readonly MouseGrab grab;

        string text;
        float shownUntil;
        GUIStyle style;
        Texture2D background;
        Texture2D dot;
        Texture2D ring;

        Vector2 ringAt;
        float ringStarted = -1f;
        Color ringColor;

        public MouseInputLabel(MouseGrab grab)
        {
            this.grab = grab;
        }

        public void Update()
        {
            if (EditorControlsInput.LeftPressedThisFrame)
                StartRing(Color.white);
            else if (EditorControlsInput.RightPressedThisFrame)
                StartRing(new Color(0.4f, 0.75f, 1f));

            string now = Current();
            if (now == null)
                return;
            text = now;
            shownUntil = Time.unscaledTime + LingerSeconds;
        }

        public void Draw()
        {
            if (style == null)
                Build();
            DrawPointer();
            if (text == null || Time.unscaledTime > shownUntil)
                return;

            style.fontSize = Mathf.Max(14, Screen.height / 18);
            int pad = style.fontSize / 2;
            style.padding = new RectOffset(pad, pad, pad / 2, pad / 2);
            var content = new GUIContent(text);
            Vector2 size = style.CalcSize(content);
            var rect = new Rect(pad, Screen.height - size.y - pad, size.x, size.y);
            GUI.Box(rect, content, style);
        }

        void StartRing(Color color)
        {
            ringAt = EditorControlsInput.MousePosition;
            ringStarted = Time.unscaledTime;
            ringColor = color;
        }

        // The cursor, and a ring that grows and fades from where a click landed. GUI y runs down the screen.
        void DrawPointer()
        {
            float unit = Mathf.Max(6f, Screen.height / 90f);
            Color before = GUI.color;

            float age = Time.unscaledTime - ringStarted;
            if (ringStarted >= 0f && age < RingSeconds)
            {
                float t = age / RingSeconds;
                float size = unit * Mathf.Lerp(3f, 9f, t);
                GUI.color = new Color(ringColor.r, ringColor.g, ringColor.b, 1f - t);
                GUI.DrawTexture(Centered(ringAt, size), ring);
            }

            if (!EditorMouseRotationTester.IsLooking && !grab.HoldsAim)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(Centered(EditorControlsInput.MousePosition, unit * 2.5f), dot);
            }
            GUI.color = before;
        }

        static Rect Centered(Vector2 screenPoint, float size)
        {
            return new Rect(screenPoint.x - size * 0.5f, Screen.height - screenPoint.y - size * 0.5f, size, size);
        }

        // A white disc with a dark edge, or a white ring, drawn once.
        static Texture2D Circle(int size, bool filled)
        {
            var texture = new Texture2D(size, size) { hideFlags = HideFlags.HideAndDontSave };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float edge = Mathf.Clamp01(r - d);
                    Color c;
                    if (filled)
                        c = d < r * 0.7f ? new Color(1f, 1f, 1f, edge) : new Color(0f, 0f, 0f, edge * 0.8f);
                    else
                        c = new Color(1f, 1f, 1f, d > r * 0.8f ? edge : 0f);
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            return texture;
        }

        // What the hands are doing this frame, or null when nothing is pressed.
        string Current()
        {
            bool shift = EditorControlsInput.KeyHeld(KeyCode.LeftShift) || EditorControlsInput.KeyHeld(KeyCode.RightShift);

            if (EditorMouseRotationTester.IsLooking)
                return "⌥  Look around";
            if (grab.Held != null)
            {
                if (EditorControlsInput.ControlHeld)
                    return shift ? "⌘ Ctrl Shift  Roll" : "⌘ Ctrl  Turn";
                if (Mathf.Abs(EditorControlsInput.ScrollNotches) > 0.01f)
                    return "⌘ Scroll  Scale";
                return "⌘  Grab";
            }
            if (EditorControlsInput.RightHeld || EditorControlsInput.RightReleasedThisFrame)
                return "Right click  Teleport";
            if (EditorControlsInput.LeftHeld || EditorControlsInput.LeftReleasedThisFrame)
                return "Click  Trigger";
            if (EditorControlsInput.ArrowHeld(KeyCode.LeftArrow))
                return "←  Turn left";
            if (EditorControlsInput.ArrowHeld(KeyCode.RightArrow))
                return "→  Turn right";
            return null;
        }

        void Build()
        {
            background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
            background.Apply();
            style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };
            style.normal.background = background;
            dot = Circle(32, true);
            ring = Circle(64, false);
            style.normal.textColor = Color.white;
        }
    }
}
#endif
