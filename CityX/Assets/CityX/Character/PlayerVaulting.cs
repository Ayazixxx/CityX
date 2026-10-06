using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The vault: running (Shift + W) at a waist-to-chest-high obstacle with a flat, thin top takes the player over it in one motion, hands on
    /// the top, and lands them on the far side. Like <see cref="PlayerClimbing"/>'s mantle it is MOTION-WARPED: the body follows a path
    /// computed from the real obstacle (<see cref="VaultProbe"/>) rather than playing a canned clip that only fits one height.
    ///
    /// Only automatic for a RUNNING player facing the obstacle (the Assassin's Creed rule): walking into a fence is just walking into a fence,
    /// and holding Space is still the way to ask for a climb.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerVaulting : MonoBehaviour
    {
        /// <summary>The whole vault takes this long, in seconds.</summary>
        public const float VaultSeconds = 0.7f;

        /// <summary>Slowest running speed (m/s) at which an obstacle is vaulted rather than bumped into.</summary>
        public const float MinRunSpeed = 0.0f;

        /// <summary>The body clears the top by this much at the highest point of the path, in metres.</summary>
        public const float ClearanceMetres = 0.12f;

        /// <summary>How far the body leans forward at the middle of the vault, in degrees.</summary>
        public const float LeanDegrees = 35.0f;

        private CharacterController _controller;
        private PlayerLocomotion _locomotion;
        private VaultProbe.Vault _vault;
        private Vector3 _start;
        private float _elapsed;
        private float _cooldown;
        private Quaternion _yaw;

        public bool IsVaulting { get; private set; }

        /// <summary>Where the vault is, 0 (start) to 1 (landed). Meaningless outside a vault.</summary>
        public float Progress => Mathf.Clamp01(_elapsed / _seconds);

        private float _seconds = VaultSeconds;

        private void Awake()
        {
            Bind();
        }

        public void Bind()
        {
            _controller = GetComponent<CharacterController>();
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        /// <summary>Checked every frame the player is on foot and free: starts a vault when running at a vaultable obstacle.</summary>
        public bool TryStart(bool forwardHeld, bool runHeld, bool grounded, float speed, float deltaTime, Vector3 direction = default)
        {
            _cooldown = Mathf.Max(0.0f, _cooldown - deltaTime);
            if (IsVaulting || _cooldown > 0.0f || !forwardHeld || !runHeld || !grounded)
            {
                return false;
            }
            if (speed < MinRunSpeed)   // no gate by default: holding run and forward at an obstacle IS the request, even when pressed against it
            {
                return false;
            }
            float height = _locomotion != null ? _locomotion.StandingHeight : BodyRig.StandingHeight;
            float radius = _controller != null ? _controller.radius : 0.3f;
            Vector3 run = direction.sqrMagnitude > 0.0001f ? direction : transform.forward;   // the way the player is trying to go, not the way the body happens to face
            if (!VaultProbe.TryFind(transform.position, run, height, radius, transform, out VaultProbe.Vault vault))
            {
                return false;
            }
            Begin(vault);
            return true;
        }

        /// <summary>Starts a vault over a known obstacle (public so a test can drive it directly).</summary>
        public void Begin(VaultProbe.Vault vault)
        {
            IsVaulting = true;
            _vault = vault;
            _start = transform.position;
            _yaw = transform.rotation;
            _elapsed = 0.0f;
            _seconds = VaultSeconds + 0.3f * Mathf.Clamp01((vault.Height - _start.y - 1.2f) / 1.0f);   // a taller obstacle takes a little longer
            _locomotion?.SetVerticalVelocity(0.0f);
            if (_controller != null)
            {
                _controller.enabled = false;   // the path is driven directly; the validated landing spot keeps it out of the geometry
            }
        }

        /// <summary>
        /// The body's position along the vault at <paramref name="u"/> (0 to 1): up first, over the top, down onto the far side. The horizontal
        /// travel starts after the rise has begun, so the feet are above the top for the whole time they are above the obstacle.
        /// </summary>
        public static Vector3 Path(Vector3 start, VaultProbe.Vault vault, float u)
        {
            u = Mathf.Clamp01(u);
            float apex = vault.Height + ClearanceMetres;
            float up = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(u / 0.3f));
            float down = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01((u - 0.72f) / 0.28f));
            float y = Mathf.Lerp(Mathf.Lerp(start.y, apex, up), vault.Land.y, down);
            float along = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01((u - 0.15f) / 0.7f));
            return new Vector3(Mathf.Lerp(start.x, vault.Land.x, along), y, Mathf.Lerp(start.z, vault.Land.z, along));
        }

        /// <summary>Called every frame by PlayerInputReader while IsVaulting.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsVaulting)
            {
                return;
            }
            _elapsed += deltaTime;
            float u = Progress;
            Vector3 face = -_vault.Normal;
            if (face.sqrMagnitude > 0.0001f)
            {
                _yaw = Quaternion.RotateTowards(_yaw, Quaternion.LookRotation(face, Vector3.up), 720.0f * deltaTime);
            }
            // The body leans into the vault (hips stay on the path, head and chest go forward, legs trail), easing in and out; flat again before the landing.
            float lean = Mathf.Sin(Mathf.PI * Mathf.Clamp01((u - 0.1f) / 0.6f)) * LeanDegrees;
            Quaternion rotation = Quaternion.AngleAxis(lean, Vector3.Cross(Vector3.up, face)) * _yaw;
            float height = _locomotion != null ? _locomotion.StandingHeight : BodyRig.StandingHeight;
            Vector3 hip = Vector3.up * (height * 0.5f);
            transform.rotation = rotation;
            transform.position = Path(_start, _vault, u) + hip - rotation * hip;
            // Hands reach for the top as the body rises, push off it while passing over, and let go on the way down.
            float hands = u < 0.2f ? u / 0.2f : (u < 0.55f ? 1.0f : Mathf.Clamp01(1.0f - (u - 0.55f) / 0.2f));
            _locomotion?.SetLedgeHands(hands, Vector3.Lerp(_vault.NearEdge, _vault.FarEdge, 0.4f), _vault.Normal);
            if (u >= 1.0f)
            {
                Complete();
            }
        }

        private void Complete()
        {
            IsVaulting = false;
            _cooldown = 0.25f;
            Vector3 land = _vault.Land;
            if (Physics.Raycast(land + Vector3.up * 0.6f, Vector3.down, out RaycastHit hit, 1.6f + VaultProbe.MaxDropMetres, ~0, QueryTriggerInteraction.Ignore))
            {
                land.y = hit.point.y;
            }
            transform.rotation = _yaw;
            transform.position = land;
            if (_controller != null)
            {
                _controller.enabled = true;
            }
            Physics.SyncTransforms();
            _locomotion?.SetLedgeHands(0.0f, Vector3.zero, Vector3.zero);
            _locomotion?.SetVerticalVelocity(0.0f);
        }
    }
}
