using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A background NPC that flies around the city with a jetpack --
    /// EXPLICIT ASK: "make an npc that is a normal white playermodel and he
    /// flys around with the jetpack in the sky. make him go around walls and
    /// stuff and not get stuck." Composes FlyingNpcBrain (which way it wants
    /// to head) with FlyingNpcSteering (the real raycasts that keep it clear
    /// of buildings and at a sane altitude), the exact same two-layer split
    /// Civilian.Update already keeps between CivilianBrain and NpcSteering.
    ///
    /// NO CharacterController -- this is free 3D flight, not ground
    /// movement, so the transform is moved directly rather than through a
    /// controller built for slopes and step-offsets it will never encounter.
    /// No Ragdoll either -- this NPC only ever flies; it was not asked to be
    /// punchable or knocked down, and both would be scope nobody asked for.
    /// </summary>
    public sealed class FlyingNpc : MonoBehaviour
    {
        [Tooltip("Seeds the wander. Different values give different routes from the same spot.")]
        public int Seed;

        [Tooltip("How fast the body turns to face its own heading, in degrees per second.")]
        public float TurnDegreesPerSecond = 70.0f;

        /// <summary>
        /// How far the cosmetic jetpack lean swings per m/s of vertical
        /// speed -- fed to the real Jetpack as a synthesized "camera pitch"
        /// (see Jetpack.TiltTarget's own doc comment for the real meaning of
        /// that value: negative = looking up = leans the body toward
        /// standing/climbing, positive = looking down = leans it toward a
        /// dive). Purely visual; it never touches how the NPC actually
        /// moves, which FlyingNpcSteering already decided.
        /// </summary>
        public const float VisualPitchDegreesPerVerticalSpeed = 4.0f;

        private FlyingNpcBrain _brain;
        private readonly FlyingNpcSteering _steering = new FlyingNpcSteering();
        private IJetpack _jetpack;

        public bool IsAvoiding => _steering.IsAvoiding;
        public float HeightAboveSurface => _steering.HeightAboveSurface;

        private void Awake()
        {
            _brain = new FlyingNpcBrain(Seed, transform.position);
            // Found live rather than wired as a serialized field. Unity does
            // not serialize a bare interface-typed field into a prefab, so a
            // reference set once by the editor builder would silently come
            // back null on load -- the real Jetpack the builder actually
            // parented onto this body is still right here in the hierarchy
            // either way, the same reasoning every bone/Animator lookup in
            // this codebase already uses GetComponentInChildren for instead
            // of a wired field.
            _jetpack = GetComponentInChildren<IJetpack>();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            Vector3 heading = _brain.Tick(transform.position, deltaTime);
            Vector3 velocity = _steering.Steer(transform, heading, deltaTime);

            transform.position += velocity * deltaTime;

            Vector3 flatVelocity = new Vector3(velocity.x, 0.0f, velocity.z);
            if (flatVelocity.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(flatVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, TurnDegreesPerSecond * deltaTime);
            }

            if (_jetpack != null)
            {
                // thrustHeld true and moveInput (0,1) always -- this body is
                // always "flying forward" under thrust, unlike a player who
                // can let go. See IJetpack.Tick's own doc comment for why
                // OnEquip/Tick both tolerate having no PlayerLocomotion,
                // PlayerVitals or PlayerInputReader at all: only the
                // cosmetic flame/audio/lean actually run here, since
                // SetVerticalVelocity's real caller (Jetpack.Tick's own
                // `_locomotion?.SetVerticalVelocity`) is a safe no-op with
                // no PlayerLocomotion on this body -- real movement is
                // entirely FlyingNpcSteering's own velocity above.
                float syntheticPitch = -velocity.y * VisualPitchDegreesPerVerticalSpeed;
                _jetpack.Tick(true, new Vector2(0.0f, 1.0f), false, deltaTime, syntheticPitch);
            }
        }
    }
}
