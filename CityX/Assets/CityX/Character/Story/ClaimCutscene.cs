using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The pure part of the claim cutscene: framing a building in a fixed
    /// field of view, and placing a camera on an orbit cone around it.
    /// Ported from the maths inside player/claim_cutscene.gd.
    ///
    /// Godot and Unity are BOTH Y-up here, so unlike a mirrored model this
    /// formula carries over unchanged -- no Z-flip needed. It also traces a
    /// full 360 degree circle, so orbit direction cannot make it read
    /// backwards the way an asymmetric mesh could.
    /// </summary>
    public static class ClaimCutsceneMath
    {
        /// <summary>
        /// How far back the camera must sit to keep a box this size inside
        /// the given field of view, with a safety margin, never closer than
        /// a minimum distance.
        /// </summary>
        public static float FrameDistance(Vector3 buildingSize, float fovDegrees, float margin, float minDistance)
        {
            float halfFov = fovDegrees * 0.5f * Mathf.Deg2Rad;
            float halfDiagonal = new Vector2(buildingSize.x, buildingSize.z).magnitude * 0.5f;
            float radius = new Vector2(halfDiagonal, buildingSize.y * 0.5f).magnitude;
            float sinHalfFov = Mathf.Max(Mathf.Sin(halfFov), 0.001f);
            return Mathf.Max(minDistance, radius / sinHalfFov * margin);
        }

        /// <summary>
        /// A point on a cone around <paramref name="focus"/>: constant
        /// distance and elevation, swept by <paramref name="angle"/> radians
        /// from <paramref name="startAngle"/>.
        /// </summary>
        public static Vector3 PlaceOnOrbit(Vector3 focus, float startAngle, float angle, float elevationDegrees, float distance)
        {
            float a = startAngle + angle;
            float elevation = elevationDegrees * Mathf.Deg2Rad;
            var offset = new Vector3(
                Mathf.Cos(a) * Mathf.Cos(elevation),
                Mathf.Sin(elevation),
                Mathf.Sin(a) * Mathf.Cos(elevation)) * distance;
            return focus + offset;
        }

        /// <summary>
        /// The angle (radians, atan2 convention) from the building's centre
        /// to the player at the moment the cutscene begins, so the orbit
        /// starts from wherever the player is actually standing rather than
        /// snapping the view around first.
        /// </summary>
        public static float StartAngle(Vector3 buildingCentre, Vector3 playerPosition)
        {
            Vector3 fromCentre = playerPosition - buildingCentre;
            var flat = new Vector2(fromCentre.x, fromCentre.z);
            return flat.sqrMagnitude > 0.0001f ? Mathf.Atan2(flat.y, flat.x) : 0.0f;
        }

        /// <summary>Smoothstep easing, matching Godot's smoothstep(0, 1, t).</summary>
        public static float Ease(float t)
        {
            float clamped = Mathf.Clamp01(t);
            return clamped * clamped * (3.0f - 2.0f * clamped);
        }
    }

    /// <summary>
    /// Drives a 360-degree orbit of the scene's single camera around a
    /// freshly claimed building, then hands control back. Ported from
    /// player/claim_cutscene.gd.
    ///
    /// DRIVES THE EXISTING CAMERA RATHER THAN SPAWNING A SECOND ONE. Godot's
    /// version creates its own free Camera3D and swaps `.current`; this
    /// project's camera rig is a single Camera shared by fly-cam and
    /// third-person modes, switched only by enabling/disabling the script
    /// that positions it each frame (see ThirdPersonCamera). A second Camera
    /// here would need its own AudioListener too, and Unity only tolerates
    /// one enabled listener at a time. Disabling ThirdPersonCamera and
    /// writing straight to the shared camera's transform reaches the same
    /// visual result without either problem, and matches how this codebase
    /// already manages "the camera" everywhere else.
    /// </summary>
    public sealed class ClaimCutscene : MonoBehaviour
    {
        public const float OrbitSeconds = 6.0f;
        public const float ElevationDegrees = 14.0f;
        public const float FrameMargin = 1.18f;
        public const float FrameMinDistance = 16.0f;
        public const float CameraFovDegrees = 75.0f;

        public event System.Action Finished;

        private Transform _camera;
        private Vector3 _focus;
        private float _distance;
        private float _startAngle;
        private float _elapsed;
        private bool _finished;

        /// <summary>
        /// Starts the orbit. <paramref name="groundCentre"/> and
        /// <paramref name="size"/> are the building's footprint in world
        /// space (ground-level centre, full size) -- exactly what
        /// ClaimSystem already reads off BuildingInteriorVolume's collider.
        /// </summary>
        public void Begin(Transform cameraTransform, Vector3 playerPosition, Vector3 groundCentre, Vector3 size)
        {
            _camera = cameraTransform;
            _focus = groundCentre + Vector3.up * (size.y * 0.5f);
            _distance = ClaimCutsceneMath.FrameDistance(size, CameraFovDegrees, FrameMargin, FrameMinDistance);
            _startAngle = ClaimCutsceneMath.StartAngle(groundCentre, playerPosition);
            _elapsed = 0.0f;
            _finished = false;
            PlaceCamera(0.0f);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// The orbit advanced by one step. Public, and taking its own
        /// deltaTime rather than reading Time.deltaTime, so a test can
        /// fast-forward through the whole 6-second orbit without waiting on
        /// real wall-clock seconds -- same reasoning as
        /// PlayerVitals.TickStamina.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_finished)
            {
                return;
            }
            _elapsed += deltaTime;
            float t = Mathf.Clamp01(_elapsed / OrbitSeconds);
            PlaceCamera(ClaimCutsceneMath.Ease(t) * Mathf.PI * 2.0f);

            if (t >= 1.0f)
            {
                _finished = true;
                Finished?.Invoke();
            }
        }

        private void PlaceCamera(float angle)
        {
            if (_camera == null)
            {
                return;
            }
            Vector3 position = ClaimCutsceneMath.PlaceOnOrbit(_focus, _startAngle, angle, ElevationDegrees, _distance);
            _camera.position = position;
            Vector3 toFocus = _focus - position;
            if (toFocus.sqrMagnitude > 0.0001f)
            {
                _camera.rotation = Quaternion.LookRotation(toFocus.normalized, Vector3.up);
            }
        }
    }
}
