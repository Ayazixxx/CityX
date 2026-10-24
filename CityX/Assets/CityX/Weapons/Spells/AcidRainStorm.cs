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
    /// the clouds gather; storm grey ("higher up in the sky", "grey instead of green"), and since 2026-10-15 at the cloud layer, 1.5 km up, like
    /// Self Smite's (owner: "same with acid rain make that appear from the height of the clouds"). A roof is full shelter for now (owner: "make it
    /// so roofs are a full shelter"); melting roofs waits for building damage.
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

        /// <summary>
        /// How high above the centre the cloud ceiling sits: the cloud layer (CloudLayer), 1.5 km. Anything higher than the clouds is above the
        /// rain; anything under them in the storm's circle is in it, a jetpack's height included.
        /// </summary>
        public const float CloudHeight = CloudLayer.HeightMetres;

        /// <summary>How wide the cloud deck is, metres from the centre: far wider than the rain, as a storm cloud is over its shower.</summary>
        public const float CloudRadius = 900.0f;

        /// <summary>How fast the drops fall, metres per second.</summary>
        public const float DropSpeed = 30.0f;

        /// <summary>
        /// The lowest the drops start, above the centre (a ten-storey roof): from the street, rain from here reads the same as rain from the clouds,
        /// and over a 60 m radius it keeps the drops alive (and colliding with the world) affordable.
        /// </summary>
        public const float RainFromHeight = 36.0f;

        /// <summary>How far above whoever is watching the drops start when they are higher up than that (flying through the storm).</summary>
        public const float RainAboveViewer = 30.0f;

        /// <summary>The faint glow's height above the centre, metres: over the street, where it lights something.</summary>
        public const float GlowHeight = 30.0f;

        /// <summary>Drops a second over the whole storm, and the most dropped in one frame (a long frame does not dump a sheet of rain).</summary>
        public const float DropsPerSecond = 3000.0f;

        public const int MaxDropsPerFrame = 200;

        /// <summary>The longest a drop falls, metres: from just above the viewer to the deepest street the storm reaches.</summary>
        public const float MaxDropFall = RainFromHeight + Depth;

        /// <summary>Splashes are thrown only where they could be seen: within this of this machine's camera, metres.</summary>
        public const float SplashViewRange = 45.0f;

        /// <summary>The droplets one landing throws up.</summary>
        public const int SplashDroplets = 3;

        /// <summary>The drops' greens, from the sickly acid green to a paler one; each drop (and its splash) is one of them.</summary>
        public static readonly Color DropGreen = new Color(0.55f, 1.0f, 0.15f, 0.55f);

        public static readonly Color DropPale = new Color(0.8f, 1.0f, 0.4f, 0.75f);

        /// <summary>A drop's colour, <paramref name="t"/> of the way from <see cref="DropGreen"/> to <see cref="DropPale"/>.</summary>
        public static Color DropColour(float t) => Color.Lerp(DropGreen, DropPale, Mathf.Clamp01(t));

        /// <summary>
        /// Where the drops start for somebody watching from <paramref name="viewerY"/>: just above them, but never lower than
        /// <see cref="RainFromHeight"/> over the centre nor up in the clouds. Drops falling the whole 1.5 km would be a column of particles nobody
        /// could see; started above the viewer they come down past them from the clouds' direction wherever they are, on the street or flying.
        /// </summary>
        public static float RainStartY(float centreY, float viewerY)
        {
            return Mathf.Clamp(viewerY + RainAboveViewer, centreY + RainFromHeight, centreY + CloudHeight - 50.0f);
        }

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

        /// <summary>Who cast it: named with its blows, so the caster sees their damage (DamageDealt).</summary>
        private GameObject _striker;
        private float _sinceCheck = AcidRain.CheckSeconds;   // the first burn lands the moment the warning ends
        private ParticleSystem[] _particles = new ParticleSystem[0];
        private ParticleSystem _rain;

        /// <summary>The names of the drops' and the splashes' particle systems in the prefab (AcidRainBuilder).</summary>
        public const string RainName = "Rain";

        public const string SplashesName = "Splashes";

        /// <summary>The green splashes where drops land, for tests.</summary>
        public ParticleSystem Splashes { get; private set; }

        /// <summary>How many drops have splashed, all told (for tests).</summary>
        public int SplashCount { get; private set; }

        private struct Landing
        {
            public float Time;
            public Vector3 Position;
            public Color Colour;
        }

        private readonly List<Landing> _landings = new List<Landing>();
        private float _dropDebt;
        private static readonly RaycastHit[] Below = new RaycastHit[8];

        /// <summary>The drops' particle system, for tests.</summary>
        public ParticleSystem Rain => _rain;
        private Light _glow;
        private float _glowBase;
        private AudioSource _audio;
        private float _audioBase;

        /// <summary>Room for every collider in a 60 m column 1.5 km tall: the city's own colliders fill a good part of it before any body does.</summary>
        private static readonly Collider[] Overlaps = new Collider[1024];
        private static readonly RaycastHit[] Above = new RaycastHit[16];
        private readonly HashSet<IDamageable> _seen = new HashSet<IDamageable>();

        public static AcidRainStorm Spawn(GameObject prefab, Vector3 centre, int strength, Collider[] ignore, bool cosmetic = false, GameObject striker = null)
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
            storm._striker = striker;
            storm.Cosmetic = cosmetic;
            storm.Begin();
            return storm;
        }

        private void Begin()
        {
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particles in _particles)
            {
                if (particles.name == RainName)
                {
                    _rain = particles;
                }
                else if (particles.name == SplashesName)
                {
                    Splashes = particles;
                }
                particles.Play();
            }
            FollowTheViewer();
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

        private void LateUpdate()
        {
            FollowTheViewer();
            float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
            RainDrops(deltaTime);
            Splash();
        }

        /// <summary>
        /// THE DROPS ARE PLACED BY SCRIPT, as the city's own rain's are (RainEffect.EmitOneDrop): each one is dropped from just above the viewer at
        /// a random spot in the storm's circle, and a ray straight down finds the first thing it will hit -- a roof, a deck, the street, a head --
        /// so it lives exactly long enough to fall onto it, and splashes there. (The particle system's own collision, which this used to rely
        /// on, is approximate at this speed: in a test a quarter of the drops in the air were under the ground, and some fell through roofs.)
        /// A drop with nothing within its fall ends in the air, unseen, with no splash.
        /// </summary>
        private void RainDrops(float deltaTime)
        {
            if (_rain == null || Age >= AcidRain.Seconds)
            {
                return;
            }
            _dropDebt += AcidRain.DropsPerSecond * deltaTime;
            int count = Mathf.Min((int)_dropDebt, AcidRain.MaxDropsPerFrame);
            _dropDebt -= (int)_dropDebt;
            if (count <= 0)
            {
                return;
            }
            Vector3 centre = transform.position;
            float startY = _rain.transform.position.y;
            Camera viewer = Camera.main;
            Vector3 eye = viewer != null ? viewer.transform.position : centre;
            float now = Time.time;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false, velocity = Vector3.down * AcidRain.DropSpeed };
            for (int i = 0; i < count; i++)
            {
                Vector2 disc = Random.insideUnitCircle * AcidRain.Radius;
                var start = new Vector3(centre.x + disc.x, startY, centre.z + disc.y);
                float fall = FirstSurfaceBelow(start, AcidRain.MaxDropFall, out bool lands);
                Color colour = AcidRain.DropColour(Random.value);
                emit.position = start;
                emit.startLifetime = Mathf.Max(0.02f, fall / AcidRain.DropSpeed);
                emit.startColor = colour;
                _rain.Emit(emit, 1);
                Vector3 landing = start + Vector3.down * fall;
                if (lands && (landing - eye).sqrMagnitude <= AcidRain.SplashViewRange * AcidRain.SplashViewRange)
                {
                    _landings.Add(new Landing { Time = now + fall / AcidRain.DropSpeed, Position = landing, Colour = colour });
                }
            }
        }

        /// <summary>How far down a drop from <paramref name="start"/> falls before it hits something, at most <paramref name="most"/>.</summary>
        private static float FirstSurfaceBelow(Vector3 start, float most, out bool lands)
        {
            int count = Physics.RaycastNonAlloc(start, Vector3.down, Below, most, ~0, QueryTriggerInteraction.Ignore);
            float nearest = most;
            lands = false;
            for (int i = 0; i < count; i++)
            {
                if (Below[i].collider != null && Below[i].distance < nearest)
                {
                    nearest = Below[i].distance;
                    lands = true;
                }
            }
            return nearest;
        }

        /// <summary>
        /// GREEN SPLASHES WHERE THE DROPS LAND (owner, 2026-10-15: "make a green version of that droplet effect on the floor ... make the green the
        /// same green as the acid rain droplets in the air"): the city rain's splash -- a few tiny droplets thrown up, gone in 0.3 s -- at the
        /// moment and the place each drop near the viewer lands, in that drop's own colour.
        /// </summary>
        private void Splash()
        {
            if (Splashes == null || _landings.Count == 0)
            {
                return;
            }
            float now = Time.time;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = _landings.Count - 1; i >= 0; i--)
            {
                Landing landing = _landings[i];
                if (landing.Time > now)
                {
                    continue;
                }
                _landings[i] = _landings[_landings.Count - 1];
                _landings.RemoveAt(_landings.Count - 1);
                Color colour = landing.Colour;
                colour.a = Mathf.Min(1.0f, colour.a + 0.2f);   // a splash is a little brighter than the drop, as the city rain's is
                emit.startColor = colour;
                emit.position = landing.Position + Vector3.up * 0.03f;
                for (int spray = 0; spray < AcidRain.SplashDroplets; spray++)
                {
                    Vector3 direction = Random.insideUnitSphere;
                    direction.y = Mathf.Abs(direction.y) + 0.6f;
                    emit.velocity = direction.normalized * Random.Range(0.4f, 1.0f);
                    Splashes.Emit(emit, 1);
                }
                SplashCount++;
            }
        }

        /// <summary>
        /// The drops start just above whoever is watching this machine's screen (AcidRain.RainStartY): the clouds are 1.5 km up, and a flier
        /// inside the storm should be rained on too. The drops already falling keep falling where they are (the rain is simulated in the world).
        /// </summary>
        private void FollowTheViewer()
        {
            if (_rain == null)
            {
                return;
            }
            Camera viewer = Camera.main;
            float centreY = transform.position.y;
            float viewerY = viewer != null ? viewer.transform.position.y : centreY;
            Vector3 at = _rain.transform.position;
            at.y = AcidRain.RainStartY(centreY, viewerY);
            _rain.transform.position = at;
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
                    DamageContext.Strike(target, amount, head + Vector3.up, 0.0f, DamageKind.Magic, 0.0f, null, _striker);
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
