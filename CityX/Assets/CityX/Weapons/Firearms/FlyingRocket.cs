using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE RPG'S ROCKET IN FLIGHT (docs/RPG.md): the launcher's own round (a copy of its Rocket part) flying as RocketRules says, a flame at its
    /// tail and a trail of smoke behind it once the motor lights, a hiss, and on whatever it meets the grenade's explosion (Explosion), a body
    /// struck square taking a blow of its own. Only the shooter's rocket hurts; another player's copy of it flies the same way, waits for its
    /// owner's word of where it burst (RocketLauncher) and shows the explosion there, or bursts by itself where it meets something.
    /// </summary>
    public sealed class FlyingRocket : MonoBehaviour
    {
        public static readonly Color FlameColour = new Color(1.0f, 0.62f, 0.22f);
        public static readonly Color SmokeColour = new Color(0.88f, 0.87f, 0.85f, 0.6f);   // a rocket motor's smoke is near white

        /// <summary>The smoke trail: how long it hangs, its width at the rocket and at its end, metres.</summary>
        public const float TrailSeconds = 1.6f;
        public const float TrailStartWidth = 0.22f;
        public const float TrailEndWidth = 1.1f;

        /// <summary>The motor's light on what it passes: reach, metres, and brightness.</summary>
        public const float FlameLightRange = 6.0f;
        public const float FlameLightIntensity = 3.0f;

        /// <summary>A copy waits this long past where it should have met something for its owner's word, seconds.</summary>
        public const float CopyGraceSeconds = 0.4f;

        private static Material _flameMaterial;
        private static Material _smokeMaterial;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private GameObject _shooter;
        private Vector3 _velocity;
        private float _since;
        private float _travelled;
        private Transform _tail;
        private TrailRenderer _trail;
        private Light _light;
        private GameObject _flame;
        private AudioSource _hiss;
        private ParticleSystem _puffs;

        /// <summary>Another player's rocket, shown here: it hurts no one.</summary>
        public bool IsCopy { get; private set; }

        public bool HasExploded { get; private set; }

        /// <summary>Raised as it bursts, with where (the shooter's: other players are told; a copy's: its own burst, matched to the word).</summary>
        public event System.Action<Vector3> Exploded;

        /// <summary>
        /// A rocket leaving <paramref name="from"/> along <paramref name="direction"/>, looking like <paramref name="look"/> (the launcher's loaded
        /// round, copied; null: a plain stand-in), fired by <paramref name="shooter"/>.
        /// </summary>
        public static FlyingRocket Launch(GameObject look, Vector3 from, Vector3 direction, GameObject shooter, bool copy)
        {
            if (direction.sqrMagnitude < 1e-6f)
            {
                return null;
            }
            direction.Normalize();
            var made = new GameObject("FlyingRocket");
            made.transform.SetPositionAndRotation(from, Quaternion.LookRotation(direction));
            var rocket = made.AddComponent<FlyingRocket>();
            rocket.Build(look);
            rocket._shooter = shooter;
            rocket._velocity = direction * RocketRules.LaunchSpeed;
            rocket.IsCopy = copy;
            return rocket;
        }

        /// <summary>The round's look, and at its tail the motor's flame, light, smoke and hiss, lit when the motor lights.</summary>
        private void Build(GameObject look)
        {
            if (look != null)
            {
                GameObject body = Instantiate(look, transform, false);
                body.name = "Round";
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;
                body.SetActive(true);
                CityX.Items.InertModel.Strip(body);
            }
            var tail = new GameObject("Tail");
            tail.transform.SetParent(transform, false);
            tail.transform.localPosition = Vector3.back * 0.05f;
            _tail = tail.transform;

            _flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_flame.GetComponent<Collider>());
            _flame.name = "Flame";
            _flame.transform.SetParent(_tail, false);
            _flame.transform.localScale = new Vector3(0.14f, 0.14f, 0.42f);
            _flame.transform.localPosition = Vector3.back * 0.2f;
            _flame.GetComponent<Renderer>().sharedMaterial = FlameMaterial();
            _flame.SetActive(false);

            _light = tail.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = FlameColour;
            _light.range = FlameLightRange;
            _light.intensity = 0.0f;
            _light.shadows = LightShadows.None;

            _trail = tail.AddComponent<TrailRenderer>();
            _trail.time = TrailSeconds;
            _trail.minVertexDistance = 0.25f;
            _trail.widthCurve = new AnimationCurve(new Keyframe(0.0f, TrailStartWidth), new Keyframe(1.0f, TrailEndWidth));
            _trail.colorGradient = SmokeFade();
            _trail.sharedMaterial = SmokeMaterial();
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.emitting = false;

            _puffs = Puffs(_tail);

            _hiss = tail.AddComponent<AudioSource>();
            _hiss.clip = RocketSounds.MotorHiss;
            _hiss.loop = true;
            _hiss.spatialBlend = 1.0f;
            _hiss.minDistance = 4.0f;
            _hiss.maxDistance = 120.0f;
            _hiss.volume = 0.0f;
            if (Application.isPlaying)
            {
                _hiss.Play();
            }
        }

        private void Update()
        {
            if (HasExploded)
            {
                return;
            }
            float deltaTime = Time.deltaTime;
            bool lit = _since + deltaTime >= RocketRules.IgniteSeconds;
            if (lit && !_trail.emitting)
            {
                _trail.emitting = true;   // the motor lights: flame, light, smoke and the full hiss
                _flame.SetActive(true);
                ParticleSystem.EmissionModule emission = _puffs.emission;
                emission.enabled = true;
            }
            _light.intensity = lit ? FlameLightIntensity * (0.8f + 0.2f * Mathf.PerlinNoise(_since * 30.0f, 0.0f)) : 0.0f;
            _hiss.volume = lit ? 0.9f : 0.3f;

            Vector3 from = transform.position;
            Vector3 position = from;
            RocketRules.Step(ref position, ref _velocity, _since, deltaTime);
            _since += deltaTime;
            Vector3 step = position - from;
            if (Meets(from, step, out RaycastHit hit))
            {
                transform.position = hit.point;
                Burst(hit.point, hit.collider);
                return;
            }
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(_velocity));
            _travelled += step.magnitude;
            if (_travelled >= RocketRules.Range + (IsCopy ? RocketRules.TopSpeed * CopyGraceSeconds : 0.0f))
            {
                Burst(position, null);   // the fuze's own end, in the air
            }
        }

        /// <summary>The first thing along this step it meets: not its shooter's own body, nor anything only a trigger.</summary>
        private bool Meets(Vector3 from, Vector3 step, out RaycastHit nearest)
        {
            nearest = default;
            float distance = step.magnitude;
            if (distance < 1e-5f)
            {
                return false;
            }
            int count = Physics.SphereCastNonAlloc(from, RocketRules.Radius, step / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.collider == null || (_shooter != null && hit.collider.transform.IsChildOf(_shooter.transform)) || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }
                float at = hit.distance;
                if (hit.point == Vector3.zero && at <= 0.0f)
                {
                    hit.point = from;   // it began inside: it bursts where it is
                }
                if (at < best)
                {
                    best = at;
                    nearest = hit;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>It bursts at <paramref name="at"/>: the shooter's hurts (a body struck square first, then the blast); a copy only shows it.</summary>
        public void Burst(Vector3 at, Collider struck)
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
                IDamageable body = struck != null ? struck.GetComponentInParent<IDamageable>() : null;
                if (body != null && body.IsAlive)
                {
                    Vector3 from = at - _velocity.normalized;
                    DamageContext.Strike(body, RocketRules.DirectHitDamage, from, 0.6f, DamageKind.Physical, 0.0f, at, _shooter);
                }
                Explosion.Blast(at, _shooter);
            }
            Exploded?.Invoke(at);
            // The smoke hangs on where it was drawn; the rest goes with the round.
            if (_puffs != null)
            {
                _puffs.transform.SetParent(null, true);
                ParticleSystem.MainModule main = _puffs.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
                _puffs.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            if (_trail != null)
            {
                _trail.transform.SetParent(null, true);
                _trail.emitting = false;
                _trail.autodestruct = true;
                Light light = _trail.GetComponent<Light>();
                if (light != null) light.enabled = false;   // switched off, not destroyed: the pipeline's own light data depends on it
                AudioSource hiss = _trail.GetComponent<AudioSource>();
                if (hiss != null) Destroy(hiss);
                if (_flame != null) Destroy(_flame);
            }
            Destroy(gameObject);
        }

        /// <summary>The smoke's PUFFS: a trail seen end on (a rocket flying straight away from the camera over the shoulder) is a line with no
        /// width and cannot be seen at all, so the smoke is mostly puffs, each turned to the camera, left along the way and swelling as they drift up
        /// and thin out: lit warm at the motor, grey after.</summary>
        private static ParticleSystem Puffs(Transform tail)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(tail, false);
            ParticleSystem puffs = go.AddComponent<ParticleSystem>();
            puffs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = puffs.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
            main.startColor = SmokeColour;
            main.gravityModifier = -0.03f;
            ParticleSystem.EmissionModule emission = puffs.emission;
            emission.rateOverTime = 0.0f;
            emission.rateOverDistance = PuffsPerMetre;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = puffs.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            ParticleSystem.SizeOverLifetimeModule size = puffs.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.EaseInOut(0.0f, 1.0f, 1.0f, 3.5f));
            ParticleSystem.ColorOverLifetimeModule colour = puffs.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(new Color(1.0f, 0.8f, 0.55f), 0.0f), new GradientColorKey(Color.white, 0.12f) },
                new[] { new GradientAlphaKey(0.9f, 0.0f), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0.0f, 1.0f) });
            colour.color = new ParticleSystem.MinMaxGradient(fade);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = SmokeMaterial();   // the soft puff DustBurst draws with (the fire's own smoke wants the fire shader's streams)
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (Application.isPlaying)
            {
                puffs.Play();
            }
            return puffs;
        }

        /// <summary>How thick the puffs are laid along the way, per metre.</summary>
        public const float PuffsPerMetre = 7.0f;

        private static Gradient SmokeFade()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1.0f, 0.85f, 0.6f), 0.0f), new GradientColorKey(SmokeColour, 0.08f), new GradientColorKey(SmokeColour, 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(SmokeColour.a, 0.06f), new GradientAlphaKey(SmokeColour.a * 0.6f, 0.5f), new GradientAlphaKey(0.0f, 1.0f) });
            return gradient;
        }

        private static Material FlameMaterial() => _flameMaterial != null ? _flameMaterial : (_flameMaterial = SoftGlow.Make("RocketFlame", FlameColour * 2.5f, additive: true));

        private static Material SmokeMaterial() => _smokeMaterial != null ? _smokeMaterial : (_smokeMaterial = SoftGlow.Make("RocketSmoke", Color.white, additive: false));
    }
}
