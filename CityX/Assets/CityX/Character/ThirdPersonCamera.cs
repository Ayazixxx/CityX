using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// Orbit camera behind the player, with a spring arm that pulls in when
    /// something is between the camera and the head.
    ///
    /// The collision arm is not polish. This city is narrow canyons and 14m
    /// streets; without it the camera spends most of its time inside a wall,
    /// and you would be judging the buildings from the inside.
    /// </summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        public Transform Target;

        [Tooltip(
            "Where the camera pivots on the body, as a fraction of standing height. " +
            "0.73 puts it at 1.6m on a 2.2m body, matching player.gd's own " +
            "BASE_CAMERA_PIVOT_HEIGHT -- a fraction rather than a metre value so it stays " +
            "sensible when the height is changed.")]
        public float PivotHeightFraction = 0.73f;

        public float Distance = 4.5f;
        public float LookDegreesPerPixel = 0.12f;
        public float MinPitch = -35.0f;
        public float MaxPitch = 70.0f;

        [Tooltip("Layers the spring arm collides with. Should include the city.")]
        public LayerMask CollisionLayers = ~0;

        private float _yaw;
        private float _pitch = 12.0f;
        private PlayerLocomotion _player;

        private void Start()
        {
            if (Target != null)
            {
                _player = Target.GetComponent<PlayerLocomotion>();
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// LateUpdate, not Update: the player moves in Update, and a camera that
        /// reads its position in the same phase is a frame behind, which reads
        /// as the body swimming against the camera.
        /// </summary>
        private void LateUpdate()
        {
            if (Target == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * LookDegreesPerPixel;
                _pitch = Mathf.Clamp(_pitch - delta.y * LookDegreesPerPixel, MinPitch, MaxPitch);
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // Otherwise there is no way out of the play window.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            float height = _player != null ? _player.StandingHeight : 1.8f;
            Vector3 pivot = Target.position + Vector3.up * (height * PivotHeightFraction);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0.0f);
            Vector3 wanted = pivot - rotation * Vector3.forward * Distance;

            // Pull in if a wall is in the way. The sphere radius keeps the near
            // plane out of the surface rather than flush against it, which is
            // what causes see-through corners.
            //
            // SphereCastAll rather than SphereCast because the cast STARTS
            // INSIDE THE PLAYER'S OWN CAPSULE -- the pivot is at chest height on
            // the body. A plain cast reports that capsule as the nearest hit and
            // pins the camera at minimum distance forever, which looks like the
            // camera being broken rather than like a collision bug.
            Vector3 direction = (wanted - pivot).normalized;
            RaycastHit[] hits = Physics.SphereCastAll(
                pivot, 0.25f, direction, Distance, CollisionLayers, QueryTriggerInteraction.Ignore);

            float nearest = Distance;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(Target) || hit.transform == Target)
                {
                    continue;
                }
                // A zero distance means the sphere began already overlapping,
                // which carries no useful direction information.
                if (hit.distance > 0.01f && hit.distance < nearest)
                {
                    nearest = hit.distance;
                }
            }

            if (nearest < Distance)
            {
                wanted = pivot + direction * Mathf.Max(0.4f, nearest - 0.1f);
            }

            transform.position = wanted;
            transform.rotation = rotation;
        }
    }
}
