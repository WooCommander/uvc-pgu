using UnityEngine;

namespace Uvc.Prototype
{
    /// <summary>Fixed-angle orthographic camera: follows the hero, pans, zooms, clamps to the floor.</summary>
    public sealed class CameraRig
    {
        public static readonly Quaternion Rotation = Quaternion.Euler(45f, 35f, 0f);
        private const float Distance = 40f;
        private const float MinSize = 4f;
        private const float MaxSize = 13f;

        private readonly Camera camera;
        private Vector3 focus;

        public CameraRig(Camera camera)
        {
            this.camera = camera;
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 120f;
            camera.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.rotation = Rotation;
        }

        public bool Following { get; set; } = true;
        public Camera Camera => camera;

        public void SnapTo(Vector3 point)
        {
            focus = Clamp(point);
            Apply();
        }

        public void Update(Vector3 heroPosition, float deltaTime)
        {
            if (Following)
            {
                focus = Vector3.Lerp(focus, Clamp(heroPosition), 1f - Mathf.Exp(-6f * deltaTime));
            }

            Apply();
        }

        /// <summary>Pans by a screen-space delta in pixels; stops following the hero.</summary>
        public void Pan(Vector2 screenDelta)
        {
            Following = false;
            float unitsPerPixel = camera.orthographicSize * 2f / Screen.height;
            Vector3 right = camera.transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.up, Vector3.up).normalized;
            // A vertical screen move covers more ground on a tilted camera.
            float tiltScale = 1f / Mathf.Max(0.3f, Mathf.Sin(Rotation.eulerAngles.x * Mathf.Deg2Rad));
            focus = Clamp(focus - (right * screenDelta.x + forward * screenDelta.y * tiltScale) * unitsPerPixel);
        }

        public void Zoom(float factor)
        {
            camera.orthographicSize = Mathf.Clamp(camera.orthographicSize * factor, MinSize, MaxSize);
        }

        private void Apply()
        {
            camera.transform.SetPositionAndRotation(focus - Rotation * Vector3.forward * Distance, Rotation);
        }

        private static Vector3 Clamp(Vector3 point)
        {
            Rect bounds = FloorLayout.CameraBounds;
            return new Vector3(Mathf.Clamp(point.x, bounds.xMin, bounds.xMax), 0f, Mathf.Clamp(point.z, bounds.yMin, bounds.yMax));
        }
    }
}
