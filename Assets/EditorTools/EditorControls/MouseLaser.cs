// Simple XR Device Simulator: https://github.com/aje0827/Simple-XR-Device-Simulator
// Copied into a project? Change the original in this repo, not the copy.
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace XREditorControls
{
    /// <summary>
    /// The laser you see in the Game view: a line from a "hand" near the camera to where the mouse points,
    /// or a curved arc while aiming a teleport. Its color says whether the pointer is on something clickable.
    /// </summary>
    internal sealed class MouseLaser
    {
        readonly EditorMouseClickTester owner;
        readonly MousePointer pointer;
        readonly MouseUiClick ui;
        readonly MouseLocomotion locomotion;
        readonly MouseGrab grab;

        LineRenderer line;
        Transform dot;
        Renderer dotRenderer;
        Color color;
        float nextColorCheck;

        public MouseLaser(EditorMouseClickTester owner, MousePointer pointer, MouseUiClick ui, MouseLocomotion locomotion, MouseGrab grab)
        {
            this.owner = owner;
            this.pointer = pointer;
            this.ui = ui;
            this.locomotion = locomotion;
            this.grab = grab;
            color = owner.raycastNormalColor;
        }

        /// <summary>
        /// Picks the color: while the button is down, click or miss; otherwise hover or normal.
        /// Hover is checked a few times a second, a press every frame.
        /// </summary>
        public void UpdateColor(bool buttonDown, bool now = false)
        {
            if (!buttonDown && !now && Time.time < nextColorCheck)
                return;
            nextColorCheck = Time.time + owner.hoverUpdateRate;

            bool overClickable = ui.IsOverUi()
                || (pointer.Raycast(pointer.Ray, out RaycastHit hit) && MousePointer.IsClickable(hit.collider.gameObject));
            if (buttonDown)
                color = overClickable ? owner.raycastClickColor : owner.raycastMissColor;
            else
                color = overClickable ? owner.raycastHoverColor : owner.raycastNormalColor;
        }

        public void Draw()
        {
            if (line == null)
                Build();

            Ray ray = pointer.Ray;
            Transform cam = pointer.Camera.transform;
            Vector3 offset = owner.laserHandOffset;
            Vector3 start = cam.position + cam.right * offset.x + cam.up * offset.y + cam.forward * offset.z;
            Vector3 end;
            Color drawColor = color;

            if (EditorControlsInput.RightHeld && grab.Held == null)
            {
                // Aiming a teleport (the stick pushed forward): a curved arc to where you would land.
                bool valid = locomotion.AimArc(ray, out end);
                drawColor = valid ? owner.raycastHoverColor : owner.raycastMissColor;
                DrawArc(start, end);
            }
            else
            {
                locomotion.ClearArc();
                end = ray.origin + ray.direction * 10f;
                if (ui.TryGetHitPoint(out Vector3 uiPoint))
                    end = uiPoint;
                else if (pointer.Raycast(ray, out RaycastHit hit))
                    end = hit.point;
                line.positionCount = 2;
                line.SetPosition(0, start);
                line.SetPosition(1, end);
            }

            line.enabled = true;
            line.startColor = line.endColor = drawColor;
            dot.gameObject.SetActive(true);
            dot.position = end;
            dot.localScale = Vector3.one * Mathf.Max(0.01f, Vector3.Distance(cam.position, end) * 0.008f);
            dotRenderer.material.color = drawColor;
        }

        public void Hide()
        {
            if (line == null)
                return;
            line.enabled = false;
            dot.gameObject.SetActive(false);
        }

        void DrawArc(Vector3 start, Vector3 end)
        {
            int points = Mathf.Max(3, owner.arcSegments);
            line.positionCount = points;
            Vector3 middle = (start + end) * 0.5f + Vector3.up * Vector3.Distance(start, end) * owner.arcHeight;
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                line.SetPosition(i, (1 - t) * (1 - t) * start + 2 * (1 - t) * t * middle + t * t * end);
            }
        }

        void Build()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            var material = new Material(shader);

            var laserObject = new GameObject("~EditorLaser");
            laserObject.transform.SetParent(owner.transform, false);
            line = laserObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = owner.laserWidth;
            line.endWidth = owner.laserWidth * 0.5f;
            line.material = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "~EditorLaserDot";
            Object.Destroy(sphere.GetComponent<Collider>()); // never blocks clicks
            sphere.transform.SetParent(owner.transform, false);
            dotRenderer = sphere.GetComponent<Renderer>();
            dotRenderer.material = new Material(material);
            dotRenderer.shadowCastingMode = ShadowCastingMode.Off;
            dot = sphere.transform;
        }
    }
}
#endif
