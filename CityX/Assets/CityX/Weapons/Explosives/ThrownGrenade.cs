using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A GRENADE IN FLIGHT with its fuse lit (docs/GRENADE.md; the Godot game's weapons/thrown_grenade.gd): a real rigid body that bounces off
    /// walls and rolls (<see cref="GrenadeRules.Bounce"/>), its fuse light blinking faster as it burns down, and when the fuse is out, the
    /// explosion. Only the thrower's grenade blasts (<see cref="Explosion.Blast"/>); another player's copy of it flies the same way, waits for
    /// its owner's word of where it went off (GrenadeThrower) and shows the explosion there, or goes off by itself if the word is late.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThrownGrenade : MonoBehaviour
    {
        /// <summary>For the first moment of its flight it does not touch its thrower, seconds.</summary>
        public const float IgnoreThrowerSeconds = 0.3f;

        /// <summary>A copy waits this long past its fuse for its owner's word before it goes off by itself, seconds.</summary>
        public const float CopyGraceSeconds = 0.5f;

        public static readonly Color BlinkColour = new Color(1.0f, 0.25f, 0.1f);

        /// <summary>The fuse light's little red light on what is round it: its reach, metres, and its brightness at the top of a blink.</summary>
        public const float FuseLightRange = 1.0f;

        public const float FuseLightIntensity = 0.6f;

        [Tooltip("The fuse light on top. Set by GrenadeBuilder.")]
        public Renderer Blinker;

        [Tooltip("Semtex: sticks to whatever it first touches, a body included, and goes off there. Set by GrenadeBuilder.")]
        public bool Sticky;

        [Tooltip("How far its middle stands off the surface it sticks to, metres (half its thickness). Set by GrenadeBuilder.")]
        public float StickOffset = 0.03f;

        /// <summary>Stuck where it first touched (Semtex).</summary>
        public bool IsStuck { get; private set; }

        /// <summary>What it is stuck to: a body's bone, a moving thing, a wall's piece of the city.</summary>
        public Transform StuckTo { get; private set; }

        private static PhysicsMaterial _bounce;

        private Rigidbody _body;
        private Collider[] _colliders;
        private Collider[] _thrower;
        private GameObject _thrownBy;
        private MaterialPropertyBlock _blink;
        private Light _light;
        private float _phase;
        private float _since;
        private bool _ignoring;

        /// <summary>The fuse left, seconds.</summary>
        public float FuseLeft { get; private set; } = GrenadeRules.FuseSeconds;

        /// <summary>Another player's grenade, shown here: it hurts no one.</summary>
        public bool IsCopy { get; private set; }

        /// <summary>It has gone off (it is taken away then).</summary>
        public bool HasExploded { get; private set; }

        /// <summary>Raised on the thrower's grenade as it goes off, with where.</summary>
        public event System.Action<Vector3> Exploded;

        private MaterialPropertyBlock Blink => _blink ??= new MaterialPropertyBlock();

        /// <summary>A grenade leaving the hand at <paramref name="from"/> with <paramref name="velocity"/>, thrown by <paramref name="thrower"/>.</summary>
        public static ThrownGrenade Spawn(GameObject prefab, Vector3 from, Vector3 velocity, GameObject thrower, bool copy)
        {
            if (prefab == null)
            {
                return null;
            }
            GameObject made = Instantiate(prefab, from, Random.rotation);
            var grenade = made.GetComponent<ThrownGrenade>();
            grenade.Launch(velocity, thrower, copy);
            return grenade;
        }

        private void Launch(Vector3 velocity, GameObject thrower, bool copy)
        {
            _body = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>();
            _thrownBy = thrower;
            IsCopy = copy;
            _body.mass = GrenadeRules.Mass;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.linearVelocity = velocity;
            _body.angularVelocity = Random.insideUnitSphere * 12.0f;
            foreach (Collider c in _colliders)
            {
                c.sharedMaterial = BounceMaterial();
            }
            _thrower = thrower != null ? thrower.GetComponentsInChildren<Collider>() : null;
            Ignore(true);
            var lightObject = new GameObject("FuseLight");
            lightObject.transform.SetParent(Blinker != null ? Blinker.transform : transform, false);
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = BlinkColour;
            _light.range = FuseLightRange;
            _light.shadows = LightShadows.None;
        }

        private void Update()
        {
            if (HasExploded)
            {
                return;
            }
            float deltaTime = Time.deltaTime;
            _since += deltaTime;
            FuseLeft -= deltaTime;
            if (_ignoring && _since > IgnoreThrowerSeconds)
            {
                Ignore(false);
            }
            // The blink speeds up as the fuse burns: driven by its phase, not a sine of the time, so it never jumps back as it quickens.
            _phase += deltaTime * GrenadeRules.BlinkHz(FuseLeft) * 2.0f * Mathf.PI;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_phase);
            if (Blinker != null)
            {
                Blinker.GetPropertyBlock(Blink);
                Blink.SetColor("_BaseColor", BlinkColour * Mathf.Lerp(0.4f, 3.0f, pulse));
                Blinker.SetPropertyBlock(Blink);
            }
            if (_light != null)
            {
                _light.intensity = Mathf.Lerp(0.0f, FuseLightIntensity, pulse);
            }
            if (FuseLeft <= 0.0f && (!IsCopy || FuseLeft <= -CopyGraceSeconds))
            {
                GoOff(transform.position);
            }
        }

        /// <summary>
        /// SEMTEX STICKS (owner, 2026-11-11: "a semtex sticky grenade that is just the grenade that just sticks to whatever surface it touches,
        /// it sticks to players and enemies too wherever it contacts them"): on its first touch it stops dead, flat against what it hit, and is
        /// carried by it: a body's nearest bone to where it landed (so it rides an arm or a back as they move), a moving thing, or the city.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (!Sticky || IsStuck || HasExploded || collision.contactCount == 0)
            {
                return;
            }
            ContactPoint contact = collision.GetContact(0);
            Stick(collision.collider, contact.point, contact.normal);
        }

        /// <summary>Sticks it at <paramref name="point"/> on <paramref name="surface"/>, its flat side against it (<paramref name="normal"/> out of the surface).</summary>
        public void Stick(Collider surface, Vector3 point, Vector3 normal)
        {
            if (IsStuck || surface == null)
            {
                return;
            }
            IsStuck = true;
            StuckTo = AnchorFor(surface, point);
            if (normal.sqrMagnitude < 1e-6f)
            {
                normal = Vector3.up;
            }
            normal.Normalize();
            if (_body != null)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.interpolation = RigidbodyInterpolation.None;   // carried by its parent now: interpolated, it trailed a frame behind it
                _body.isKinematic = true;
            }
            // Its colliders go: stuck to a body it must not push it about, nor be what the next shot hits.
            foreach (Collider mine in _colliders ?? GetComponentsInChildren<Collider>())
            {
                if (mine != null) mine.enabled = false;
            }
            Vector3 across = Vector3.ProjectOnPlane(transform.forward, normal);
            if (across.sqrMagnitude < 1e-4f) across = Vector3.ProjectOnPlane(Vector3.forward, normal);
            transform.SetPositionAndRotation(point + normal * StickOffset, Quaternion.LookRotation(across.normalized, normal));
            if (StuckTo != null)
            {
                transform.SetParent(StuckTo, true);
            }
        }

        /// <summary>What a stuck grenade rides: a body's bone nearest the point (a body's capsule is its whole self), else what it hit.</summary>
        private static Transform AnchorFor(Collider surface, Vector3 point)
        {
            Animator animator = surface.GetComponentInParent<Animator>();
            if (animator == null && surface.GetComponent<CharacterController>() != null)
            {
                animator = surface.GetComponentInChildren<Animator>();
            }
            if (animator != null && animator.isHuman)
            {
                Transform nearest = null;
                float best = float.MaxValue;
                foreach (HumanBodyBones bone in Carriers)
                {
                    Transform t = animator.GetBoneTransform(bone);
                    if (t == null) continue;
                    float d = (t.position - point).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        nearest = t;
                    }
                }
                if (nearest != null)
                {
                    return nearest;
                }
            }
            return surface.attachedRigidbody != null ? surface.attachedRigidbody.transform : surface.transform;
        }

        /// <summary>The bones a stuck grenade can ride on a body.</summary>
        private static readonly HumanBodyBones[] Carriers =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
        };

        /// <summary>It goes off at <paramref name="at"/>: the thrower's blasts; a copy (told where by its owner, or late) only shows it.</summary>
        public void GoOff(Vector3 at)
        {
            if (HasExploded)
            {
                return;
            }
            HasExploded = true;
            if (IsCopy)
            {
                Explosion.Show(at);
            }
            else
            {
                Explosion.Blast(at, _thrownBy);
                Exploded?.Invoke(at);
            }
            Destroy(gameObject);
        }

        private void Ignore(bool ignore)
        {
            _ignoring = ignore;
            if (_thrower == null)
            {
                return;
            }
            foreach (Collider mine in _colliders)
            {
                foreach (Collider theirs in _thrower)
                {
                    if (mine != null && theirs != null)
                    {
                        Physics.IgnoreCollision(mine, theirs, ignore);
                    }
                }
            }
        }

        private static PhysicsMaterial BounceMaterial()
        {
            if (_bounce == null)
            {
                _bounce = new PhysicsMaterial("Grenade")
                {
                    bounciness = GrenadeRules.Bounce,
                    dynamicFriction = GrenadeRules.Friction,
                    staticFriction = GrenadeRules.Friction,
                    bounceCombine = PhysicsMaterialCombine.Maximum,
                };
            }
            return _bounce;
        }
    }
}
