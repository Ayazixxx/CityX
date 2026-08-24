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

        [Tooltip(
            "Metres to the right of the spine the camera sits, Fortnite/GTA-style, so the body " +
            "is off to one side of frame instead of dead centre. Reported directly: standing " +
            "square behind the head put anything small and close in front of the player -- a " +
            "dropped pistol on the ground being the concrete case -- right behind their own " +
            "silhouette, with nothing on screen to actually judge alignment against.")]
        public float ShoulderOffset = 0.85f;

        [Tooltip("Layers the spring arm collides with. Should include the city.")]
        public LayerMask CollisionLayers = ~0;

        [Tooltip(
            "Field of view while aiming (see IsAiming). Narrower than the ordinary field of " +
            "view, which is read from whatever the Camera component already starts at rather " +
            "than assumed here, so this only ever has to describe how much CLOSER aiming zooms.")]
        public float AimFieldOfView = 40.0f;

        [Tooltip("Degrees of field of view crossed per second when the aim state changes.")]
        public float AimZoomDegreesPerSecond = 90.0f;

        /// <summary>
        /// Set every frame by PlayerInputReader -- see its own TickCombat,
        /// the one place that already knows whether a gun is out and right
        /// is held, or a melee weapon's own WeaponAimInput.IsAiming says so.
        /// This component stays a plain camera rig with no opinion on which
        /// input or which equipped item means "aiming"; it only zooms.
        /// </summary>
        public bool IsAiming;

        private float _yaw;
        private float _pitch = 12.0f;
        private PlayerLocomotion _player;
        private Camera _camera;
        private float _restFieldOfView;

        /// <summary>
        /// Settable so IntroCutscene can hand the camera back on the same
        /// yaw/pitch it was left on when it re-enables this component --
        /// otherwise the very next LateUpdate would snap to whatever
        /// leftover _yaw/_pitch this component last held from before the
        /// cutscene disabled it, popping the view. Degrees, same convention
        /// LateUpdate's own Quaternion.Euler(pitch, yaw, 0) already uses.
        /// </summary>
        public float Yaw
        {
            get => _yaw;
            set => _yaw = value;
        }

        public float Pitch
        {
            get => _pitch;
            set => _pitch = value;
        }

        private void Start()
        {
            if (Target != null)
            {
                _player = Target.GetComponent<PlayerLocomotion>();
            }
            _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                _restFieldOfView = _camera.fieldOfView;
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
                _pitch = Mathf.Clamp(
                    _pitch - delta.y * LookDegreesPerPixel, MinPitch, MaxPitch);
            }

            // NO ESCAPE HANDLING HERE. There used to be a bare "Escape frees the
            // cursor" fallback, from before there was any menu to open with it.
            // PauseMenuView owns Escape exclusively now, and it matters that
            // this component does not also react to the same key: re-reading
            // Escape here would race PauseMenuView's own Close(), which also
            // sets Cursor.lockState in the same frame -- whichever ran last
            // would win by accident, sometimes leaving the cursor free right
            // after the menu closed.

            float height = _player != null ? _player.StandingHeight : 1.8f;
            Vector3 pivot = Target.position + Vector3.up * (height * PivotHeightFraction);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0.0f);

            // OFFSET THE DESTINATION, NOT THE COLLISION ORIGIN. The spherecast
            // below still starts from the centred chest pivot -- see its own
            // comment on why that specifically has to be inside the capsule --
            // and simply aims at the shifted point instead, so a wall directly
            // behind the player's own centreline still pulls the shoulder-shot
            // camera in correctly rather than missing it because the cast
            // started somewhere already past the collider.
            Vector3 shoulderPivot = pivot + rotation * Vector3.right * ShoulderOffset;
            Vector3 wanted = shoulderPivot - rotation * Vector3.forward * Distance;

            // Pull in if a wall is in the way. The sphere radius keeps the near
            // plane out of the surface rather than flush against it, which is
            // what causes see-through corners.
            //
            // SphereCastAll rather than SphereCast because the cast STARTS
            // INSIDE THE PLAYER'S OWN CAPSULE -- the pivot is at chest height on
            // the body. A plain cast reports that capsule as the nearest hit and
            // pins the camera at minimum distance forever, which looks like the
            // camera being broken rather than like a collision bug.
            // The shoulder offset makes pivot-to-wanted slightly longer than
            // Distance itself (the hypotenuse of Distance and ShoulderOffset)
            // -- casting only as far as the flat Distance would leave the
            // last few centimetres in front of the actual camera unchecked.
            float wantedDistance = Vector3.Distance(pivot, wanted);
            Vector3 direction = (wanted - pivot).normalized;
            RaycastHit[] hits = Physics.SphereCastAll(
                pivot, 0.25f, direction, wantedDistance, CollisionLayers, QueryTriggerInteraction.Ignore);

            float nearest = wantedDistance;
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

            if (nearest < wantedDistance)
            {
                wanted = pivot + direction * Mathf.Max(0.4f, nearest - 0.1f);
            }

            transform.position = wanted;
            transform.rotation = rotation;

            if (_camera != null)
            {
                float target = IsAiming ? AimFieldOfView : _restFieldOfView;
                _camera.fieldOfView = Mathf.MoveTowards(
                    _camera.fieldOfView, target, AimZoomDegreesPerSecond * Time.deltaTime);
            }
        }
    }
}
