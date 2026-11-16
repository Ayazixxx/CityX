using CityX.Character;
using CityX.World;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A fireball in flight, spawned fresh on every cast -- same shape as
    /// ThrownBottle (a separate physics-driven object, not the caster
    /// itself), just for a spell instead of a thrown item. No Godot
    /// equivalent to port -- an explicit new-mechanic ask, same footing as
    /// Jetpack.cs.
    ///
    /// Its look (docs/FIRE.md, "The fire spells"): a molten core that tumbles and heats as it flies (<see cref="Core"/>), a wake of burning gas
    /// left behind it, embers, sparks and smoke shed off it (FireScene.Wake), and its burst where it lands (<see cref="FireBurst"/>). All of it
    /// is the ball's own, so another player's copy of a fireball looks the same as the caster's.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FireballProjectile : MonoBehaviour
    {
        public const float BaseDamage = 25.0f;

        public GameObject BurstPrefab;
        public AudioClip ImpactSound;

        [Tooltip("The molten core (CityX/FireballCore): its seams prise open and brighten as it flies. Set by FireballProjectileBuilder.")]
        public MeshRenderer Core;

        private static readonly int ChargeId = Shader.PropertyToID("_Charge");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int HeadingId = Shader.PropertyToID("_Heading");
        private static MaterialPropertyBlock _coreBlock;

        private Rigidbody _rigidbody;
        /// <summary>Points in the caster's Spell Power (2% a point, CombatTuning.DamageFor), and how much of the target's armour and block it ignores.</summary>
        private float _casterStrength;

        /// <summary>Who cast it: named with its blows, so the caster sees their damage (DamageDealt).</summary>
        private GameObject _striker;
        private float _penetration;
        private bool _consumed;

        /// <summary>A charged ball's size and damage, as a multiple of a plain one's (the Fireball Staff's charge, FireballCaster).</summary>
        private float _size = 1.0f;

        /// <summary>How far round its landing a charged ball's burst also burns, metres (0: only what it hits).</summary>
        private float _blastRadius;

        /// <summary>At the blast's edge, the share of the hit's damage still dealt (all of it at the middle).</summary>
        public const float BlastEdgeShare = 0.3f;

        public float Size => _size;

        public float BlastRadius => _blastRadius;

        /// <summary>How long it has flown, seconds (its core heats over it: SpellFire.CoreHeatAt).</summary>
        public float Age => _age;

        /// <summary>
        /// True for the copy another player's machine shows of a fireball they cast: it flies, bursts and sounds exactly as the real one
        /// does, but hurts nothing (the caster's own machine decides what the real fireball hits).
        /// </summary>
        public bool Cosmetic { get; private set; }

        private float _age;
        private Vector3 _wakeFrom;
        private float _wakeDebt;
        private Vector3 _spinAxis = Vector3.up;
        private float _seed;

        public static FireballProjectile Spawn(GameObject prefab, Vector3 origin, Vector3 velocity,
            float casterStrength, Collider[] ignoreColliders, bool cosmetic = false, float penetration = 0.0f, GameObject striker = null,
            float size = 1.0f, float blastRadius = 0.0f)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, origin, Quaternion.LookRotation(velocity.normalized));
            var fireball = instance.GetComponent<FireballProjectile>();
            if (fireball == null)
            {
                return null;
            }

            fireball._casterStrength = casterStrength;
            fireball._striker = striker;
            fireball._penetration = Mathf.Clamp01(penetration);
            fireball.Cosmetic = cosmetic;
            fireball._size = Mathf.Max(0.1f, size);
            fireball._blastRadius = Mathf.Max(0.0f, blastRadius);
            fireball._wakeFrom = origin;
            if (!Mathf.Approximately(fireball._size, 1.0f))
            {
                Grow(instance, fireball._size);
            }
            // GetComponent RATHER THAN THE CACHED FIELD -- see ThrownBottle.
            // Spawn's own comment: Awake has not necessarily run yet on a
            // clone made from an inactive prefab template.
            if (fireball._rigidbody == null)
            {
                fireball._rigidbody = instance.GetComponent<Rigidbody>();
            }
            fireball._rigidbody.linearVelocity = velocity;

            if (ignoreColliders != null)
            {
                Collider ownCollider = instance.GetComponent<Collider>();
                if (ownCollider != null)
                {
                    foreach (Collider ignore in ignoreColliders)
                    {
                        if (ignore != null)
                        {
                            Physics.IgnoreCollision(ownCollider, ignore);
                        }
                    }
                }
            }

            return fireball;
        }

        /// <summary>
        /// The fireball's own light, so it glows in the dark and lights the street and walls it flies past (owner, 2026-10-10: "make the
        /// fireball have a light effect so it glows in the dark"). Warm fire orange, no shadow (a moving point light's shadow is six renders a
        /// frame), flickering a little like a flame. On the prefab, so every player's copy of a fireball glows.
        /// </summary>
        public static readonly Color GlowColour = new Color(1.0f, 0.55f, 0.18f);
        public const float GlowIntensity = 6.0f;
        public const float GlowRange = 10.0f;

        /// <summary>How far the flicker takes the glow either side of its brightness, and how fast.</summary>
        public const float GlowFlicker = 0.25f;
        public const float GlowFlickerSpeed = 14.0f;

        private Light _glow;
        private float _flickerSeed;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _glow = GetComponentInChildren<Light>();
            _flickerSeed = Random.value * 100.0f;
            _seed = Random.value * 10.0f;
            _spinAxis = Random.onUnitSphere;
        }

        /// <summary>
        /// Keeps facing wherever it is CURRENTLY flying, not just where it was thrown from (a reflect ability will change its velocity; its
        /// wake and its core's hot face go by this), flickers its light, turns and heats its core, and leaves its wake for the stretch it flew.
        /// </summary>
        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _age += deltaTime;
            if (_glow != null)
            {
                float noise = Mathf.PerlinNoise(Time.time * GlowFlickerSpeed, _flickerSeed) * 2.0f - 1.0f;
                _glow.intensity = GlowIntensity * (1.0f + GlowFlicker * noise);
            }

            Vector3 velocity = _rigidbody.linearVelocity;
            Vector3 heading = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(heading);
            HeatCore(heading);
            Vector3 at = transform.position;
            FireScene.Wake(_wakeFrom, at, heading, _size, ref _wakeDebt, deltaTime);
            _wakeFrom = at;
        }

        private void HeatCore(Vector3 heading)
        {
            if (Core == null)
            {
                return;
            }
            Core.transform.localRotation = Quaternion.AngleAxis(_age * SpellFire.CoreSpinDegreesPerSecond, _spinAxis);
            _coreBlock ??= new MaterialPropertyBlock();
            _coreBlock.SetFloat(ChargeId, SpellFire.CoreHeatAt(_age));
            _coreBlock.SetFloat(SeedId, _seed);
            _coreBlock.SetVector(HeadingId, heading);
            Core.SetPropertyBlock(_coreBlock);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_consumed)
            {
                return;
            }
            _consumed = true;

            Vector3 point = transform.position;
            Vector3 normal = Vector3.up;
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                point = contact.point;
                normal = contact.normal;
            }

            var target = Cosmetic ? null : collision.collider.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                float damage = CombatTuning.DamageFor(BaseDamage * _size, _casterStrength);
                DamageContext.Strike(target, damage, transform.position, CombatTuning.HitstunSeconds, DamageKind.Magic, _penetration, null, _striker);
            }

            if (!Cosmetic && _blastRadius > 0.0f)
            {
                Blast(point, target);
            }
            if (!Cosmetic)
            {
                LightGasoline(point, target);
            }
            FireScene.Wake(_wakeFrom, point, transform.forward, _size, ref _wakeDebt, 0.0f);   // the wake runs on into the burst
            FireBurst.Spawn(BurstPrefab, point, normal, _size);
            if (ImpactSound != null)
            {
                AudioSource.PlayClipAtPoint(ImpactSound, point);
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Gasoline it bursts in goes up (owner, 2026-11-08: "make the gasoline ignite from gunshot and fire spells"), the floor under a body it
        /// struck included; bare ground does not catch (2026-10-29: "fireball doesnt need to light the ground on fire"). Other players are told by
        /// the fire itself (FireSync).
        /// </summary>
        private void LightGasoline(Vector3 point, IDamageable struck)
        {
            Vector3 ground = struck is Component body ? new Vector3(point.x, body.transform.position.y, point.z) : point;
            FireScene.Ignite(ground, Mathf.Max(FireRules.FireballIgniteRadius * _size, _blastRadius), true, _striker);
        }

        /// <summary>
        /// A charged ball's burst burns everyone within <see cref="_blastRadius"/> of where it landed but the one it hit (who took the whole hit)
        /// and its caster: the full hit's damage at the middle, <see cref="BlastEdgeShare"/> of it at the edge.
        /// </summary>
        private void Blast(Vector3 point, IDamageable alreadyHit)
        {
            float damage = CombatTuning.DamageFor(BaseDamage * _size, _casterStrength);
            IDamageable caster = _striker != null ? _striker.GetComponent<IDamageable>() : null;
            var burnt = new System.Collections.Generic.List<IDamageable>();
            foreach (Collider collider in Physics.OverlapSphere(point, _blastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || ReferenceEquals(target, alreadyHit) || ReferenceEquals(target, caster) || burnt.Contains(target))
                {
                    continue;
                }
                burnt.Add(target);
                float distance = Vector3.Distance(point, collider.ClosestPoint(point));
                float share = Mathf.Lerp(1.0f, BlastEdgeShare, Mathf.Clamp01(distance / _blastRadius));
                DamageContext.Strike(target, damage * share, point, CombatTuning.HitstunSeconds, DamageKind.Magic, _penetration, null, _striker);
            }
        }

        /// <summary>A charged ball, <paramref name="size"/> times a plain one: its core, its reach and its light grow with it (its wake by its size).</summary>
        private static void Grow(GameObject ball, float size)
        {
            ball.transform.localScale = Vector3.one * size;
            foreach (Light light in ball.GetComponentsInChildren<Light>(true))
            {
                light.range *= size;
            }
        }
    }
}
