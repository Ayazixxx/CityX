using System.Collections.Generic;
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

        /// <summary>What is currently locked on, or null.</summary>
        public Transform LockedTarget { get; private set; }

        private int _lockVisits;
        private float _sightLost;

        public bool IsLockedOn => LockedTarget != null;

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

            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            {
                ToggleLock();
            }

            // WHILE LOCKED THE MOUSE IS IGNORED ENTIRELY. Blending a lock with
            // live look input means two things fighting over the same yaw, and
            // the visible result is a camera that will not settle. Godot takes
            // the same line.
            if (IsLockedOn)
            {
                TrackLock();
            }
            else
            {
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    _yaw += delta.x * LookDegreesPerPixel;
                    _pitch = Mathf.Clamp(
                        _pitch - delta.y * LookDegreesPerPixel, MinPitch, MaxPitch);
                }
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

        /// <summary>
        /// C acquires, cycles, and eventually lets go -- one button for all
        /// three, which is what makes it usable mid-fight.
        /// </summary>
        private void ToggleLock()
        {
            MonoBehaviour[] everything =
                Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            List<Transform> candidates =
                LockOn.Candidates(Target.position, Lockable(everything));

            if (!IsLockedOn)
            {
                LockedTarget = LockOn.Acquire(Target.position, transform.forward, candidates);
                _lockVisits = LockedTarget != null ? 1 : 0;
                _sightLost = 0.0f;
                return;
            }

            _lockVisits++;
            LockedTarget = LockOn.Cycle(LockedTarget, candidates, _lockVisits);
            if (LockedTarget == null)
            {
                _lockVisits = 0;
            }
            _sightLost = 0.0f;
        }

        /// <summary>
        /// Everything damageable EXCEPT the body doing the looking. Without
        /// that exclusion, pressing lock-on picks yourself -- you are, after
        /// all, the nearest thing to the crosshair that can be hit.
        /// </summary>
        private IEnumerable<IDamageable> Lockable(IEnumerable<MonoBehaviour> all)
        {
            foreach (MonoBehaviour behaviour in all)
            {
                if (behaviour is IDamageable damageable &&
                    !behaviour.transform.IsChildOf(Target) && behaviour.transform != Target)
                {
                    yield return damageable;
                }
            }
        }

        /// <summary>
        /// Swings toward the target, and decides whether the lock survives.
        ///
        /// The yaw is LERPED rather than snapped, so acquiring reads as the
        /// camera turning to look rather than as a cut. And because the body
        /// takes its facing from the camera, driving the yaw here turns WASD
        /// into strafing around the target for free -- no movement code knows
        /// this feature exists.
        /// </summary>
        private void TrackLock()
        {
            var damageable = LockedTarget.GetComponentInParent<IDamageable>();
            float distance = Vector3.Distance(Target.position, LockedTarget.position);

            if (damageable == null || !damageable.IsAlive ||
                distance > LockOn.Range * LockOn.ReleaseSlack)
            {
                LockedTarget = null;
                _lockVisits = 0;
                return;
            }

            // Out of sight is forgiven briefly -- somebody ducking behind a
            // corner mid-fight should not cost the lock, but standing behind a
            // building should.
            Vector3 eye = Target.position + Vector3.up * 1.2f;
            Vector3 toTarget = LockedTarget.position + Vector3.up * 0.8f - eye;
            bool blocked = Physics.Raycast(eye, toTarget.normalized, out RaycastHit hit,
                                toTarget.magnitude, CollisionLayers,
                                QueryTriggerInteraction.Ignore)
                           && !hit.transform.IsChildOf(LockedTarget)
                           && hit.transform != LockedTarget;

            _sightLost = blocked ? _sightLost + Time.deltaTime : 0.0f;
            if (_sightLost > LockOn.SightGraceSeconds)
            {
                LockedTarget = null;
                _lockVisits = 0;
                return;
            }

            LockOn.Bearing(Target.position, LockedTarget.position,
                out float wantedYaw, out float wantedPitch);

            float step = LockOn.CameraLerpSpeed * Time.deltaTime;
            _yaw = Mathf.LerpAngle(_yaw, wantedYaw, step);
            _pitch = Mathf.Lerp(_pitch, wantedPitch, step);
        }
    }
}
