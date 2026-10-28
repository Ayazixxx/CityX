using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE JET OF WATER ITSELF, drawn in code (docs/STAFFS.md; owner: "try to make it look like the water from the hose on the firetruck in
    /// gta"; then "make it less of a beam and more of a wider splash like gta firetruck hose, there will be other beam attacks i make later").
    /// GTA V's cannon is a dense WHITE spray: a short solid core at the nozzle that falls in an arc and breaks up into a widening cone of
    /// froth and mist, and a billowing white cloud where it hits. So, three parts, rebuilt every frame from the staff's head along the crosshair:
    /// <list type="bullet">
    /// <item>THE CORE: a tube along the falling arc (<see cref="GravityShare"/> of gravity), bright at the nozzle and gone by
    /// <see cref="CoreFadeShare"/> of the reach, streaks of foam scrolling down it, cut where it meets the world.</item>
    /// <item>THE SPRAY: the body of it -- white drops shed all along the arc with its flow, fanning out the further they have flown, and a
    /// mist of soft puffs drifting off its second half.</item>
    /// <item>THE IMPACT: where the arc lands, drops thrown out and up and a cloud of mist rolling up off the ground.</item>
    /// </list>
    /// Plus the hiss of it. The same on a copy of the staff on another player.
    /// </summary>
    public sealed class WaterJet : MonoBehaviour
    {
        /// <summary>How fast the water leaves the nozzle, metres a second, and how far it reaches along its arc.</summary>
        public const float Speed = 26.0f;

        public const float Reach = WaterJetRules.Range;

        /// <summary>The arc's fall, as a share of gravity: a cannon's jet sags, it does not drop like a thrown ball.</summary>
        public const float GravityShare = 0.3f;

        /// <summary>The core's radius at the nozzle and at the end of its reach, metres.</summary>
        public const float CoreRadiusStart = 0.06f;

        public const float CoreRadiusEnd = 0.14f;

        /// <summary>How solid the core is at the nozzle, and how far along the reach it has broken up into spray entirely (owner: "less of a beam").</summary>
        public const float CoreAlphaStart = 0.92f;

        public const float CoreFadeShare = 0.4f;

        /// <summary>The foam streaks' length along the core, metres, and how fast they scroll down it.</summary>
        public const float StreakMetres = 0.9f;

        public const float StreakScrollPerSecond = 3.5f;

        public const int Segments = 18;

        public const int Sides = 8;

        /// <summary>Drops a second peeling off the arc, and mist puffs a second drifting off its end.</summary>
        public const float SprayPerSecond = 900.0f;

        public const float MistPerSecond = 120.0f;

        /// <summary>How often the landing throws up a burst, seconds, and what one holds.</summary>
        public const float SplashSeconds = 0.05f;

        public const int SplashDrops = 16;

        public const int SplashPuffs = 4;

        public const float HissVolume = 0.5f;

        private static readonly Color Foam = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        private static readonly Color Water = new Color(0.8f, 0.92f, 1.0f, 1.0f);
        private static readonly Color MistColour = new Color(0.95f, 0.98f, 1.0f, 0.45f);
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];
        private static Material _coreMaterial;
        private static Material _dropMaterial;
        private static Material _mistMaterial;

        private MeshRenderer _core;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Color[] _colours;
        private Vector2[] _uvs;
        private ParticleSystem _spray;
        private ParticleSystem _mist;
        private ParticleSystem _splash;
        private ParticleSystem _cloud;
        private AudioSource _hiss;
        private float _splashIn;
        private float _sprayDebt;
        private float _mistDebt;
        private readonly Vector3[] _arc = new Vector3[Segments + 1];
        private readonly Vector3[] _flow = new Vector3[Segments + 1];

        public bool IsRunning { get; private set; }

        /// <summary>Where the arc lands this frame, if it lands on anything within reach.</summary>
        public bool Lands { get; private set; }

        public Vector3 Landing { get; private set; }

        /// <summary>A jet on <paramref name="tip"/> (the staff's head), stopped until <see cref="Run"/>.</summary>
        public static WaterJet Make(Transform tip)
        {
            var go = new GameObject("WaterJet");
            go.transform.SetParent(tip, false);
            var jet = go.AddComponent<WaterJet>();
            jet.Build();
            return jet;
        }

        private void Build()
        {
            // The core, a mesh of its own in the world (the arc is in world space, whatever the staff does).
            var core = new GameObject("WaterCore");
            core.transform.SetParent(null, false);
            core.AddComponent<MeshFilter>().sharedMesh = _mesh = new Mesh { name = "WaterCore" };
            _mesh.MarkDynamic();
            _core = core.AddComponent<MeshRenderer>();
            _core.sharedMaterial = CoreMaterial;
            _core.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _core.receiveShadows = false;
            _core.enabled = false;
            int count = (Segments + 1) * (Sides + 1);
            _vertices = new Vector3[count];
            _colours = new Color[count];
            _uvs = new Vector2[count];
            var triangles = new int[Segments * Sides * 6];
            int t = 0;
            for (int i = 0; i < Segments; i++)
            {
                for (int j = 0; j < Sides; j++)
                {
                    int a = i * (Sides + 1) + j, b = a + 1, c = a + Sides + 1, d = c + 1;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            }
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.colors = _colours;
            _mesh.triangles = triangles;

            _spray = Particles("WaterSpray", DropMaterial, 0.1f, 0.3f, 0.35f, 0.6f, 1.0f, true);
            _mist = Particles("WaterMist", MistMaterial, 0.6f, 1.2f, 0.5f, 0.9f, 0.05f, false);
            _splash = Particles("WaterSplash", DropMaterial, 0.1f, 0.22f, 0.4f, 0.7f, 1.0f, true);
            _cloud = Particles("WaterCloud", MistMaterial, 1.0f, 2.0f, 0.5f, 0.9f, -0.03f, false);
            if (Application.isPlaying)
            {
                _hiss = gameObject.AddComponent<AudioSource>();
                _hiss.clip = WaterSounds.Hiss;
                _hiss.loop = true;
                _hiss.volume = 0.0f;
                _hiss.spatialBlend = 1.0f;
                _hiss.playOnAwake = false;
            }
        }

        private static Material CoreMaterial => _coreMaterial != null ? _coreMaterial : (_coreMaterial = Painted("WaterCore", StreakTexture()));

        private static Material DropMaterial => _dropMaterial != null ? _dropMaterial : (_dropMaterial = SoftGlow.Make("WaterDrop", Foam, false));

        private static Material MistMaterial => _mistMaterial != null ? _mistMaterial : (_mistMaterial = SoftGlow.Make("WaterMist", MistColour, false));

        /// <summary>A see-through, unlit material painted with <paramref name="texture"/>, white (the mesh's own colours tint it).</summary>
        private static Material Painted(string name, Texture2D texture)
        {
            Material material = SoftGlow.Make(name, Color.white, false);
            material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            return material;
        }

        /// <summary>Foam streaking down the core: bright bands running along it, a little bluer between them; repeats along its length.</summary>
        private static Texture2D StreakTexture()
        {
            const int width = 256, height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "WaterStreaks", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width, v = y / (float)height;
                    float streak = 0.5f + 0.5f * Mathf.Sin(2.0f * Mathf.PI * (3.0f * u + 0.6f * Mathf.Sin(2.0f * Mathf.PI * (v * 2.0f + 0.4f * u))));
                    float fine = 0.5f + 0.5f * Mathf.Sin(2.0f * Mathf.PI * (11.0f * u + 1.5f * Mathf.Sin(2.0f * Mathf.PI * v * 3.0f)));
                    float foam = Mathf.Clamp01(0.55f * streak + 0.45f * fine);
                    Color colour = Color.Lerp(Water, Foam, foam);
                    colour.a = 0.8f + 0.2f * foam;
                    pixels[y * width + x] = colour;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static ParticleSystem Particles(string name, Material material, float leastSize, float mostSize, float leastLife, float mostLife,
            float gravity, bool stretched)
        {
            var go = new GameObject(name);
            go.transform.SetParent(null, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(leastLife, mostLife);
            main.startSpeed = 0.0f;
            main.startSize = new ParticleSystem.MinMaxCurve(leastSize, mostSize);
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 800;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0.0f;
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0.0f, 1.0f) });
            fade.color = alpha;
            if (!stretched)
            {
                // Mist swells as it thins.
                ParticleSystem.SizeOverLifetimeModule swell = particles.sizeOverLifetime;
                swell.enabled = true;
                swell.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 0.6f, 1.0f, 1.6f));
            }
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            if (stretched)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.02f;
                renderer.lengthScale = 1.4f;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
            return particles;
        }

        public void Run()
        {
            if (IsRunning)
            {
                return;
            }
            IsRunning = true;
            _core.enabled = true;
            if (_hiss != null)
            {
                _hiss.volume = HissVolume;
                _hiss.Play();
            }
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }
            IsRunning = false;
            _core.enabled = false;
            Lands = false;
            if (_hiss != null)
            {
                _hiss.Stop();
            }
        }

        /// <summary>
        /// Points the jet from <paramref name="from"/> along <paramref name="direction"/>: the arc is traced, cut where it first meets the
        /// world (nothing of <paramref name="self"/>, no trigger), the core rebuilt along it, spray and mist shed off it and the landing hit.
        /// </summary>
        public void Aim(Vector3 from, Vector3 direction, Transform self, float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }
            Vector3 velocity = direction.normalized * Speed;
            float gravity = Physics.gravity.magnitude * GravityShare;
            float flight = Reach / Speed;
            int last = Segments;
            Lands = false;
            Vector3 normal = Vector3.up;
            _arc[0] = from;
            _flow[0] = velocity;
            for (int i = 1; i <= Segments; i++)
            {
                float t = flight * i / Segments;
                _arc[i] = from + velocity * t + Vector3.down * (0.5f * gravity * t * t);
                _flow[i] = velocity + Vector3.down * (gravity * t);
                if (!Lands && Hits(_arc[i - 1], _arc[i], self, out Vector3 point, out Vector3 hitNormal))
                {
                    Lands = true;
                    Landing = point;
                    normal = hitNormal;
                    _arc[i] = point;
                    last = i;
                    for (int j = i + 1; j <= Segments; j++)
                    {
                        _arc[j] = point;
                        _flow[j] = _flow[i];
                    }
                    break;
                }
            }
            BuildCore(last);
            Shed(last, deltaTime);
            if (Lands)
            {
                Splash(normal, deltaTime);
            }
        }

        /// <summary>The first thing between <paramref name="a"/> and <paramref name="b"/> that is not <paramref name="self"/>'s or a trigger.</summary>
        private static bool Hits(Vector3 a, Vector3 b, Transform self, out Vector3 point, out Vector3 normal)
        {
            Vector3 step = b - a;
            float length = step.magnitude;
            point = b;
            normal = Vector3.up;
            if (length < 1e-4f)
            {
                return false;
            }
            int count = Physics.RaycastNonAlloc(a, step / length, HitBuffer, length, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = HitBuffer[i];
                if (hit.collider == null || (self != null && hit.collider.transform.IsChildOf(self)) || hit.distance >= best)
                {
                    continue;
                }
                best = hit.distance;
                point = hit.point;
                normal = hit.normal;
                found = true;
            }
            return found;
        }

        /// <summary>The tube along the arc up to ring <paramref name="last"/>: a ring of <see cref="Sides"/> round each sample, foam scrolling down it.</summary>
        private void BuildCore(int last)
        {
            float scroll = Time.time * StreakScrollPerSecond;
            float wobble = Time.time * 23.0f;
            float along = 0.0f;
            Vector3 lastSide = Vector3.right;
            for (int i = 0; i <= Segments; i++)
            {
                int k = Mathf.Min(i, last);
                if (i > 0)
                {
                    along += Vector3.Distance(_arc[k], _arc[Mathf.Min(i - 1, last)]);
                }
                float s = k / (float)Segments;
                Vector3 tangent = _flow[k].sqrMagnitude > 1e-6f ? _flow[k].normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(tangent, Vector3.up);
                side = side.sqrMagnitude > 1e-4f ? side.normalized : lastSide;
                lastSide = side;
                Vector3 up = Vector3.Cross(side, tangent).normalized;
                float radius = i > last ? 0.0f : Mathf.Lerp(CoreRadiusStart, CoreRadiusEnd, s) * (1.0f + 0.08f * Mathf.Sin(wobble + s * 31.0f));
                float alpha = i > last ? 0.0f : CoreAlphaStart * (1.0f - Mathf.SmoothStep(0.0f, 1.0f, s / CoreFadeShare));
                for (int j = 0; j <= Sides; j++)
                {
                    float angle = 2.0f * Mathf.PI * j / Sides;
                    int v = i * (Sides + 1) + j;
                    _vertices[v] = _arc[k] + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
                    _uvs[v] = new Vector2(along / StreakMetres - scroll, j / (float)Sides);
                    _colours[v] = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
            }
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.colors = _colours;
            _mesh.RecalculateBounds();
        }

        /// <summary>Drops peel off the arc's far half with its flow, and mist drifts off its last third.</summary>
        private void Shed(int last, float deltaTime)
        {
            _sprayDebt += SprayPerSecond * deltaTime;
            while (_sprayDebt >= 1.0f)
            {
                _sprayDebt -= 1.0f;
                float s = Random.Range(0.05f, 1.0f);
                Sample(s, last, out Vector3 at, out Vector3 flow);
                // The spray fans out the further it has flown: a cone, not a beam.
                Vector3 velocity = flow * Random.Range(0.65f, 1.0f) + Random.insideUnitSphere * Mathf.Lerp(0.4f, 3.5f, s);
                _spray.Emit(new ParticleSystem.EmitParams { position = at + Random.insideUnitSphere * CoreRadiusEnd, velocity = velocity }, 1);
            }
            _mistDebt += MistPerSecond * deltaTime;
            while (_mistDebt >= 1.0f)
            {
                _mistDebt -= 1.0f;
                float s = Random.Range(0.3f, 1.0f);
                Sample(s, last, out Vector3 at, out Vector3 flow);
                Vector3 velocity = flow * Random.Range(0.2f, 0.5f) + Random.insideUnitSphere * 1.5f + Vector3.up * 0.3f;
                _mist.Emit(new ParticleSystem.EmitParams { position = at + Random.insideUnitSphere * 0.25f, velocity = velocity }, 1);
            }
        }

        /// <summary>A point <paramref name="s"/> (0..1) along the arc as it stands, and the water's flow there.</summary>
        private void Sample(float s, int last, out Vector3 at, out Vector3 flow)
        {
            float f = s * last;
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, Mathf.Max(0, last - 1));
            float u = f - i;
            at = Vector3.Lerp(_arc[i], _arc[Mathf.Min(i + 1, last)], u);
            flow = Vector3.Lerp(_flow[i], _flow[Mathf.Min(i + 1, last)], u);
        }

        /// <summary>Where the arc lands: drops thrown out and up off the surface, and a cloud of mist rolling up.</summary>
        private void Splash(Vector3 normal, float deltaTime)
        {
            _splashIn -= deltaTime;
            if (_splashIn > 0.0f)
            {
                return;
            }
            _splashIn = SplashSeconds;
            Vector3 flow = _flow[Segments];
            Vector3 bounce = Vector3.Reflect(flow.normalized, normal);
            for (int i = 0; i < SplashDrops; i++)
            {
                Vector3 velocity = (bounce * 0.35f + normal * 0.6f + Random.insideUnitSphere * 0.7f).normalized * Random.Range(3.0f, 7.0f);
                _splash.Emit(new ParticleSystem.EmitParams { position = Landing + normal * 0.05f, velocity = velocity }, 1);
            }
            for (int i = 0; i < SplashPuffs; i++)
            {
                Vector3 velocity = (normal * 0.8f + bounce * 0.3f + Random.insideUnitSphere * 0.5f).normalized * Random.Range(1.0f, 2.5f);
                _cloud.Emit(new ParticleSystem.EmitParams { position = Landing + normal * 0.2f + Random.insideUnitSphere * 0.3f, velocity = velocity }, 1);
            }
        }

        private void OnDestroy()
        {
            foreach (Object o in new Object[] { _core != null ? _core.gameObject : null, _spray, _mist, _splash, _cloud, _mesh })
            {
                if (o is ParticleSystem p)
                {
                    Destroy(p.gameObject);
                }
                else if (o != null)
                {
                    Destroy(o);
                }
            }
        }
    }
}
