using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The numbers of Acid Rain and the rules that use them, with no scene, so they can be tested on their own.
    ///
    /// SIZED FOR A BIG FIGHT, not the whole world (owner, 2026-10-10: "a large area but not the entire world ... reasonable for a large fight"):
    /// a 60 m radius covers a couple of blocks and the streets between them (owner, 2026-10-11: "cover a larger area"), and lasts 20 s. 6 health a second burns a 100-health body down in
    /// about 17 s if it never takes cover: it is a storm you must get out of, not one that kills on contact. Damage starts after a 2 s warning, as
    /// the clouds gather; the clouds hang 70 m up, storm grey ("higher up in the sky", "grey instead of green"). A roof is full shelter for now (owner: "make it so roofs are a full shelter"); melting roofs waits for building damage.
    /// </summary>
    public static class AcidRain
    {
        public const float Radius = 60.0f;

        public const float Seconds = 20.0f;

        public const float DamagePerSecond = 6.0f;

        /// <summary>From the cast until the first burn: the clouds gather and everyone sees it coming.</summary>
        public const float WarningSeconds = 2.0f;

        /// <summary>How often everyone under it is checked and burned.</summary>
        public const float CheckSeconds = 0.5f;

        /// <summary>How high above the centre the cloud ceiling sits. Anything higher than the clouds is above the rain.</summary>
        public const float CloudHeight = 70.0f;

        /// <summary>How far below the centre the storm still reaches (a street running downhill from where it was cast).</summary>
        public const float Depth = 40.0f;

        /// <summary>The clouds clear and the last drops fall after the storm stops burning.</summary>
        public const float ClearSeconds = 3.0f;

        /// <summary>Whether a point is under the storm: within the radius across, between its depth and its clouds up and down.</summary>
        public static bool IsUnder(Vector3 centre, Vector3 point)
        {
            Vector2 across = new Vector2(point.x - centre.x, point.z - centre.z);
            return across.sqrMagnitude <= Radius * Radius && point.y >= centre.y - Depth && point.y <= centre.y + CloudHeight;
        }

        /// <summary>Whether the storm burns at this age: after the warning, until it stops.</summary>
        public static bool IsBurning(float age)
        {
            return age >= WarningSeconds && age < Seconds;
        }

        /// <summary>The burn for one check, before the caster's strength.</summary>
        public static float BurnPerCheck => DamagePerSecond * CheckSeconds;
    }

    /// <summary>
    /// One Acid Rain storm: a ceiling of sick green cloud over the area, green rain falling out of it that stops on roofs, and every body out in the
    /// open under it burned every half second (AcidRain). Never the caster. A COSMETIC storm (another player's, on this machine) looks and sounds
    /// the same and burns nothing -- the caster's own machine decides the damage, which reaches other players through their copies, as every hit does.
    /// </summary>
    public sealed class AcidRainStorm : MonoBehaviour
    {
        public bool Cosmetic;

        public float Age { get; private set; }

        /// <summary>How many burns it has dealt, for tests.</summary>
        public int Burns { get; private set; }

        private int _strength;
        private Collider[] _ignore;
        private float _sinceCheck = AcidRain.CheckSeconds;   // the first burn lands the moment the warning ends
        private ParticleSystem[] _particles = new ParticleSystem[0];
        private Light _glow;
        private float _glowBase;
        private AudioSource _audio;
        private float _audioBase;

        private static readonly Collider[] Overlaps = new Collider[256];
        private static readonly RaycastHit[] Above = new RaycastHit[16];
        private readonly HashSet<IDamageable> _seen = new HashSet<IDamageable>();

        public static AcidRainStorm Spawn(GameObject prefab, Vector3 centre, int strength, Collider[] ignore, bool cosmetic = false)
        {
            GameObject go = prefab != null ? Instantiate(prefab, centre, Quaternion.identity) : new GameObject("AcidRainStorm");
            go.transform.position = centre;
            AcidRainStorm storm = go.GetComponent<AcidRainStorm>();
            if (storm == null)
            {
                storm = go.AddComponent<AcidRainStorm>();
            }
            storm._strength = strength;
            storm._ignore = ignore;
            storm.Cosmetic = cosmetic;
            storm.Begin();
            return storm;
        }

        private void Begin()
        {
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particles in _particles)
            {
                particles.Play();
            }
            _glow = GetComponentInChildren<Light>();
            _glowBase = _glow != null ? _glow.intensity : 0.0f;
            _audio = GetComponentInChildren<AudioSource>();
            _audioBase = _audio != null ? _audio.volume : 0.0f;
            if (_audio != null && _audio.clip != null && Application.isPlaying)
            {
                _audio.Play();
            }
            Shade();
        }

        private void Update()
        {
            Tick(Mathf.Min(Time.deltaTime, 0.1f));
        }

        /// <summary>Advances the storm: burns what is under it on time, gathers and clears the clouds. Public so a test can step it.</summary>
        public void Tick(float deltaTime)
        {
            Age += deltaTime;
            if (Age >= AcidRain.Seconds + AcidRain.ClearSeconds)
            {
                Destroy(gameObject);
                return;
            }
            Shade();
            if (Cosmetic || !AcidRain.IsBurning(Age))
            {
                return;
            }
            _sinceCheck += deltaTime;
            while (_sinceCheck >= AcidRain.CheckSeconds)
            {
                _sinceCheck -= AcidRain.CheckSeconds;
                Burn();
            }
        }

        /// <summary>The clouds gather over the warning, hang, and clear after: rain stops falling when the burning stops; the glow and the hiss follow.</summary>
        private void Shade()
        {
            float gather = Mathf.Clamp01(Age / AcidRain.WarningSeconds);
            float clear = Mathf.Clamp01((AcidRain.Seconds + AcidRain.ClearSeconds - Age) / AcidRain.ClearSeconds);
            float strength = gather * clear;
            bool raining = Age < AcidRain.Seconds;
            foreach (ParticleSystem particles in _particles)
            {
                if (particles == null)
                {
                    continue;
                }
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = raining;
            }
            if (_glow != null)
            {
                _glow.intensity = _glowBase * strength;
            }
            if (_audio != null)
            {
                _audio.volume = _audioBase * strength;
            }
        }

        private void Burn()
        {
            Vector3 centre = transform.position;
            Vector3 bottom = centre + Vector3.down * AcidRain.Depth;
            Vector3 top = centre + Vector3.up * AcidRain.CloudHeight;
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, AcidRain.Radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            _seen.Clear();
            float amount = CombatTuning.DamageFor(AcidRain.BurnPerCheck, _strength);
            for (int i = 0; i < count; i++)
            {
                Collider collider = Overlaps[i];
                if (collider == null || (_ignore != null && System.Array.IndexOf(_ignore, collider) >= 0))
                {
                    continue;
                }
                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || !_seen.Add(target))
                {
                    continue;
                }
                Bounds body = collider.bounds;
                Vector3 head = new Vector3(body.center.x, body.max.y, body.center.z);
                if (!AcidRain.IsUnder(centre, head) || IsSheltered(head, target, top.y))
                {
                    continue;
                }
                if (target is IPoisonable quiet)
                {
                    quiet.TakePoison(amount);   // a burn, not a blow: no flinch, no block
                }
                else
                {
                    DamageContext.Strike(target, amount, head + Vector3.up, 0.0f, DamageKind.Magic, 0.0f);
                }
                Burns++;
            }
        }

        /// <summary>
        /// Whether anything solid is between the top of a body and the clouds: a roof, a deck, a balcony. Other bodies do not shelter (the acid
        /// burns the one standing on top), nor does the body itself.
        /// </summary>
        public static bool IsSheltered(Vector3 head, IDamageable target, float cloudsY)
        {
            float reach = cloudsY - head.y;
            if (reach <= 0.0f)
            {
                return false;
            }
            int count = Physics.RaycastNonAlloc(head + Vector3.up * 0.05f, Vector3.up, Above, reach, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Above[i].collider;
                if (hit == null)
                {
                    continue;
                }
                IDamageable owner = hit.GetComponentInParent<IDamageable>();
                if (owner == target || owner != null)
                {
                    continue;
                }
                return true;
            }
            return false;
        }
    }
}
