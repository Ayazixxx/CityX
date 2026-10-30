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
