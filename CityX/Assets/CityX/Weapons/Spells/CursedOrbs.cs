using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE CURSED ORBS one caster has put on one target (docs/ARCANE_ORB.md): dark spheres in a magenta glow, flying out from where the ball
    /// landed to circle the target's chest, bobbing, trailing light. Their clock is an <see cref="ArcaneCurse"/>. For the last
    /// <see cref="TellSeconds"/> they spin faster, draw in and pulse (the warning a fair enemy gives: a guard or a dash meets them); then they
    /// dive in one after another, each a blow from where it was (it can be blocked like any other) and a burst. If the target dies first they
    /// fade away unspent.
    ///
    /// The caster's machine decides the blows. Every other machine shows a cosmetic copy, placed by the caster's word on how many orbs and how
    /// long they have left (<see cref="Show"/>): it looks and sounds the same, and hurts nothing.
    /// </summary>
    public sealed class CursedOrbs : MonoBehaviour
    {
        /// <summary>An orb's body and its glow, metres across at the 2.6 m body (larger targets get larger orbs).</summary>
        public const float OrbDiameter = 0.32f;

        public const float GlowSize = 0.9f;

        /// <summary>The ring's radius and height, as shares of the target's height.</summary>
        public const float RingShare = 0.38f;

        public const float HeightShare = 0.6f;

        public const float TurnsPerSecond = 0.4f;

        /// <summary>How far each orb rises and falls about the ring, metres at the 2.6 m body.</summary>
        public const float BobMetres = 0.12f;

        /// <summary>The warning before the strike, seconds: the spin quickens by <see cref="TellSpin"/> and the ring draws in to <see cref="TellRingShare"/>.</summary>
        public const float TellSeconds = 0.6f;

        public const float TellSpin = 2.5f;

        public const float TellRingShare = 0.8f;

        /// <summary>A new orb's flight from the ball's landing to its place on the ring, seconds.</summary>
        public const float ArriveSeconds = 0.25f;

        /// <summary>The strike: each orb dives this long after the one before, and takes <see cref="DiveSeconds"/> to reach the body.</summary>
        public const float StrikeStaggerSeconds = 0.12f;

        public const float DiveSeconds = 0.14f;

        /// <summary>Where a diving orb meets the body and bursts, as a share of the way out from the middle to the ring: its skin, not inside it.</summary>
        public const float StrikeShare = 0.4f;

        /// <summary>Orbs whose target died fade over this long.</summary>
        public const float DissolveSeconds = 0.3f;

        /// <summary>The magenta light the orbs throw on whoever they circle (twice as bright in the warning).</summary>
        public const float LightIntensity = 0.9f;

        private const float BuiltHeight = 2.6f;

        private sealed class Orb
        {
            public Transform Body;
            public MeshRenderer Glow;
            public float Damage;
            public Vector3 From;
            public float Age;
            public float Slot;
            public float Scale;
            public bool Struck;
        }

        private static readonly List<CursedOrbs> Live = new List<CursedOrbs>();

        private readonly ArcaneCurse _curse = new ArcaneCurse();
        private readonly List<Orb> _orbs = new List<Orb>();
        private MaterialPropertyBlock _block;

        /// <summary>Made on first use: a property block may not be made while the component is (a field initializer).</summary>
        private MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();
        private IDamageable _damageable;
        private CharacterController _controller;
        private Collider _collider;
        private Light _light;
        private float _penetration;
        private float _spin;
        private float _strikeClock;
        private float _dissolve = -1.0f;

        public Transform Target { get; private set; }

        public GameObject Caster { get; private set; }

        /// <summary>A copy shown for another machine's caster: no blows.</summary>
        public bool Cosmetic { get; private set; }

        public ArcaneCurse Curse => _curse;

        /// <summary>Every curse in the world right now.</summary>
        public static IReadOnlyList<CursedOrbs> All => Live;

        public static CursedOrbs Find(Transform target, GameObject caster)
        {
            foreach (CursedOrbs group in Live)
            {
                if (group != null && group.Target == target && group.Caster == caster)
                {
                    return group;
                }
            }
            return null;
        }

        /// <summary>
        /// A ball carrying <paramref name="orbsInShot"/> hit <paramref name="target"/>: its orbs go on the caster's curse there (as many as fit),
        /// each to strike for <paramref name="orbDamage"/>, flying out from <paramref name="from"/>. Null when the target is not a thing in the world.
        /// </summary>
        public static CursedOrbs Hit(IDamageable target, GameObject caster, int orbsInShot, float orbDamage, float penetration, Vector3 from)
        {
            if (!(target is Component component) || component == null)
            {
                return null;
            }
            CursedOrbs group = Find(component.transform, caster) ?? Create(component.transform, target, caster, false);
            int placed = group._curse.Hit(orbsInShot);
            group._penetration = penetration;
            for (int i = 0; i < placed; i++)
            {
                group.AddOrb(orbDamage, from);
            }
            if (placed > 0 && Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(ArcaneOrbSounds.Curse, group.transform.position);
            }
            return group;
        }

        /// <summary>On another machine: shows the caster's curse on <paramref name="target"/> as their machine has it (orbs, seconds left).</summary>
        public static CursedOrbs Show(IDamageable target, GameObject caster, int orbs, float secondsLeft, Vector3 from)
        {
            if (!(target is Component component) || component == null)
            {
                return null;
            }
            CursedOrbs group = Find(component.transform, caster) ?? Create(component.transform, target, caster, true);
            if (!group.Cosmetic)
            {
                return group;
            }
            int before = group._orbs.Count;
            group._curse.Match(orbs, secondsLeft);
            while (group._orbs.Count < group._curse.Orbs)
            {
                group.AddOrb(0.0f, from);
            }
            if (group._orbs.Count > before && Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(ArcaneOrbSounds.Curse, group.transform.position);
            }
            return group;
        }

        private static CursedOrbs Create(Transform target, IDamageable damageable, GameObject caster, bool cosmetic)
        {
            var go = new GameObject("CursedOrbs");
            var group = go.AddComponent<CursedOrbs>();
            group.Target = target;
            group._damageable = damageable;
            group.Caster = caster;
            group.Cosmetic = cosmetic;
            group._controller = target.GetComponent<CharacterController>();
            group._collider = group._controller == null ? target.GetComponentInChildren<Collider>() : null;
            go.transform.position = group.RingCentre(out _);
            group._light = ArcaneVisuals.Light(go.transform, LightIntensity, 4.0f);
            Live.Add(group);
            return group;
        }

        private void OnDestroy()
        {
            Live.Remove(this);
        }

        /// <summary>The middle of the ring (the target's chest) and the target's height.</summary>
        private Vector3 RingCentre(out float height)
        {
            Vector3 middle;
            if (_controller != null)
            {
                height = _controller.height * Target.lossyScale.y;
                middle = Target.TransformPoint(_controller.center);
            }
            else if (_collider != null)
            {
                Bounds bounds = _collider.bounds;
                height = bounds.size.y;
                middle = bounds.center;
            }
            else
            {
                height = 2.0f;
                middle = Target.position + Vector3.up;
            }
            return middle + Vector3.up * (height * (HeightShare - 0.5f));
        }

        private void AddOrb(float damage, Vector3 from)
        {
            RingCentre(out float height);
            float scale = Mathf.Clamp(height / BuiltHeight, 0.4f, 10.0f);
            var go = new GameObject("Orb");
            go.transform.SetParent(transform, false);
            go.transform.position = from;
            go.transform.localScale = Vector3.one * scale;
            ArcaneVisuals.Sphere(go.transform, ArcaneVisuals.CoreMaterial, OrbDiameter);
            var orb = new Orb
            {
                Body = go.transform,
                Glow = ArcaneVisuals.Sprite(go.transform, ArcaneVisuals.Glow, GlowSize),
                Damage = damage,
                From = from,
                Slot = _orbs.Count * 360.0f / Mathf.Max(1, _curse.Orbs),
                Scale = scale,
            };
            ArcaneVisuals.Trail(go, 0.35f, OrbDiameter * 0.8f * scale);
            _orbs.Add(orb);
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>Public so a test can run the clock without waiting for frames.</summary>
        public void Step(float deltaTime)
        {
            if (Target == null)
            {
                Destroy(gameObject);
                return;
            }
            Vector3 centre = RingCentre(out float height);
            transform.position = centre;
            float scale = Mathf.Clamp(height / BuiltHeight, 0.4f, 10.0f);

            if (_dissolve < 0.0f && !_curse.Striking && (_damageable == null || !_damageable.IsAlive))
            {
                _dissolve = 0.0f;
            }
            if (_dissolve >= 0.0f)
            {
                Dissolve(deltaTime);
                return;
            }

            _curse.Tick(deltaTime);
            float tell = _curse.Striking ? 1.0f : Mathf.Clamp01(1.0f - _curse.SecondsLeft / TellSeconds);
            _spin += deltaTime * TurnsPerSecond * 360.0f * Mathf.Lerp(1.0f, TellSpin, tell);
            float radius = height * RingShare * Mathf.Lerp(1.0f, TellRingShare, tell);
            float pulse = tell > 0.0f ? 0.65f + 0.35f * Mathf.Sin(Time.time * 30.0f) : 1.0f;

            int count = Mathf.Max(1, _orbs.Count);
            bool allStruck = true;
            for (int i = 0; i < _orbs.Count; i++)
            {
                Orb orb = _orbs[i];
                if (orb.Struck)
                {
                    continue;
                }
                allStruck = false;
                orb.Age += deltaTime;
                orb.Slot = Mathf.MoveTowardsAngle(orb.Slot, i * 360.0f / count, 360.0f * deltaTime);
                float angle = (_spin + orb.Slot) * Mathf.Deg2Rad;
                Vector3 ring = centre + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * radius
                               + Vector3.up * (Mathf.Sin(orb.Age * 4.0f + i * 1.7f) * BobMetres * scale);
                float arrive = Mathf.Clamp01(orb.Age / ArriveSeconds);
                Vector3 at = Vector3.Lerp(orb.From, ring, arrive * arrive * (3.0f - 2.0f * arrive));

                if (_curse.Striking)
                {
                    float dive = (_strikeClock - i * StrikeStaggerSeconds) / DiveSeconds;
                    if (dive > 0.0f)
                    {
                        at = Vector3.Lerp(ring, centre + (ring - centre) * StrikeShare, Mathf.Clamp01(dive * dive));
                        if (dive >= 1.0f)
                        {
                            Strike(orb, ring, centre, scale);
                            continue;
                        }
                    }
                }
                orb.Body.position = at;
                ArcaneVisuals.Fade(orb.Glow, ArcaneVisuals.Magenta, pulse, Block);
            }

            if (_light != null)
            {
                _light.intensity = LightIntensity * (1.0f + tell) * pulse;
                _light.range = 4.0f * scale;
            }
            if (_curse.Striking)
            {
                _strikeClock += deltaTime;
                if (allStruck)
                {
                    Destroy(gameObject);
                }
            }
        }

        private void Strike(Orb orb, Vector3 ring, Vector3 centre, float scale)
        {
            orb.Struck = true;
            orb.Body.gameObject.SetActive(false);
            if (!Cosmetic && _damageable != null && _damageable.IsAlive)
            {
                DamageContext.Strike(_damageable, orb.Damage, ring, CombatTuning.HitstunSeconds, DamageKind.Magic, _penetration, centre, Caster);
            }
            ArcaneBurst.Spawn(centre + (ring - centre) * StrikeShare, 1.2f * scale, ArcaneOrbSounds.Strike, 0.8f);
        }

        private void Dissolve(float deltaTime)
        {
            _dissolve += deltaTime;
            float left = Mathf.Clamp01(1.0f - _dissolve / DissolveSeconds);
            foreach (Orb orb in _orbs)
            {
                if (orb.Body != null)
                {
                    orb.Body.localScale = Vector3.one * (orb.Scale * left);
                    ArcaneVisuals.Fade(orb.Glow, ArcaneVisuals.Magenta, left, Block);
                }
            }
            if (_light != null)
            {
                _light.intensity *= left;
            }
            if (left <= 0.0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
