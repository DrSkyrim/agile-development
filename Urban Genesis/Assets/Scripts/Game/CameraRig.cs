using UnityEngine;
using UnityEngine.InputSystem;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// City-builder camera that orbits a focus point on the ground.
    ///   WASD / arrow keys  move (Shift = faster)
    ///   Q / E              rotate
    ///   Right mouse drag   rotate (and tilt with up/down movement)
    ///   Middle mouse drag  pan
    ///   Mouse wheel        zoom
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        private const float MinDistance = 30f;
        private const float MaxDistance = 4000f;
        private const float MinPitch = 25f;
        private const float MaxPitch = 85f;

        private Camera cam;
        private HudToolbar hud;

        private Vector3 focus;
        private float yaw;
        private float pitch = 55f;
        private float distance = 260f;
        private float targetDistance = 260f;
        private float halfExtent;

        public Camera Camera { get { return cam; } }

        /// <summary>Which way the camera is turned, in degrees. 0 means north is straight up on screen.</summary>
        public float YawDegrees { get { return yaw; } }

        /// <summary>How many screen pixels one metre of ground is wide, where the camera looks. Used for the scale bar.</summary>
        public float PixelsPerMetre()
        {
            if (cam == null)
            {
                return 1f;
            }

            Vector3 a = cam.WorldToScreenPoint(focus);
            Vector3 b = cam.WorldToScreenPoint(focus + cam.transform.right * 10f);
            float dx = b.x - a.x;
            float dy = b.y - a.y;
            return Mathf.Max(0.01f, Mathf.Sqrt(dx * dx + dy * dy) / 10f);
        }

        /// <summary>Zooms one step in (positive) or out (negative), like one notch of the mouse wheel.</summary>
        public void ZoomStep(int direction)
        {
            targetDistance = Mathf.Clamp(targetDistance * Mathf.Pow(0.8f, direction), MinDistance, MaxDistance);
        }

        /// <summary>Turns the camera so north is straight up on screen.</summary>
        public void FaceNorth()
        {
            yaw = 0f;
        }

        /// <summary>Moves the view back to the middle of the map.</summary>
        public void Recenter()
        {
            focus = Vector3.zero;
        }

        /// <summary>Starts looking at the middle of the map. <paramref name="mapHalfExtent"/> is half the map's side length in metres.</summary>
        public void Init(HudToolbar toolbar, float mapHalfExtent)
        {
            hud = toolbar;
            halfExtent = mapHalfExtent;
            focus = Vector3.zero;

            cam = Camera.main;
            if (cam == null)
            {
                cam = FindFirstObjectByType<Camera>();
            }

            if (cam == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cam = cameraObject.AddComponent<Camera>();
            }

            if (FindFirstObjectByType<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }

            // The default clip planes are tuned for small scenes; a 4 km map needs longer ones.
            cam.nearClipPlane = 5f;
            cam.farClipPlane = 12000f;

            ApplyToCamera();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (cam == null || keyboard == null || mouse == null)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            bool pointerFree = hud == null || !hud.ContainsScreenPoint(mouse.position.ReadValue());

            // Rotate with the keyboard.
            if (keyboard.qKey.isPressed) { yaw -= 90f * dt; }
            if (keyboard.eKey.isPressed) { yaw += 90f * dt; }

            // Move with the keyboard, relative to where the camera is facing.
            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) { move.y += 1f; }
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) { move.y -= 1f; }
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) { move.x += 1f; }
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) { move.x -= 1f; }

            Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
            float speed = distance * 0.8f * (keyboard.leftShiftKey.isPressed ? 2.5f : 1f);
            focus += yawRotation * new Vector3(move.x, 0f, move.y) * (speed * dt);

            // Mouse drags (only if the drag is not on top of the toolbar).
            Vector2 delta = mouse.delta.ReadValue();
            if (mouse.rightButton.isPressed && pointerFree)
            {
                yaw += delta.x * 0.2f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.2f, MinPitch, MaxPitch);
            }

            if (mouse.middleButton.isPressed && pointerFree)
            {
                Vector3 drag = yawRotation * new Vector3(-delta.x, 0f, -delta.y);
                focus += drag * (distance * 0.0016f);
            }

            // Zoom with the wheel. The raw value differs per platform, so only its sign is used.
            float scroll = mouse.scroll.ReadValue().y;
            if (pointerFree && Mathf.Abs(scroll) > 0.01f)
            {
                targetDistance = Mathf.Clamp(targetDistance * Mathf.Pow(0.88f, Mathf.Sign(scroll)), MinDistance, MaxDistance);
            }

            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-12f * dt));

            focus.x = Mathf.Clamp(focus.x, -halfExtent, halfExtent);
            focus.z = Mathf.Clamp(focus.z, -halfExtent, halfExtent);

            ApplyToCamera();
        }

        private void ApplyToCamera()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.rotation = rotation;
            cam.transform.position = focus + rotation * new Vector3(0f, 0f, -distance);
        }
    }
}
