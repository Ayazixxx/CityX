using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.View
{
    /// <summary>
    /// WHAT BODIES LEAVE IN THE SNOW (docs/SNOW.md): a print where a foot comes down, which fills back in, with a crunch; and their breath showing in the cold. Red Dead's
    /// fresh snow keeps footprints, and Batman Arkham Origins' deforms under them, but its height maps cover a small area and this city is not one. So a print here is a dark
    /// mark on the ground, a thousand of them in one draw. Every machine makes its own from the bodies it can see, so other players' tracks show with nothing sent.
    /// </summary>
    public sealed class SnowTracks : MonoBehaviour
    {
        public const int MaxPrints = 1000;

        /// <summary>How far a print is held off the ground, metres.</summary>
        public const float PrintLift = 0.025f;

        /// <summary>A print's size, metres, along and across the foot.</summary>
        public const float PrintLength = 0.52f;

        public const float PrintWidth = 0.25f;

        /// <summary>How far off a body makes tracks, and shows its breath, and is heard, metres.</summary>
        public const float TrackRange = 45.0f;

        public const float BreathRange = 16.0f;

        public const float HeardRange = 12.0f;

        /// <summary>A body's breath, seconds apart.</summary>
        public const float BreathEverySeconds = 2.4f;

        private const float RefreshBodiesSeconds = 1.0f;
        private const float FadeEverySeconds = 0.5f;
        private const float EverywhereMetres = 100000.0f;

        /// <summary>A print is whole for this share of its time, then fades.</summary>
        private const float WholeUntilShare = 0.6f;
        private const float GroundCheckAbove = 0.35f;
        private const float GroundCheckBelow = 0.9f;
        private const float CrunchVolume = 0.28f;

        private static readonly Color PrintShade = new Color(0.58f, 0.63f, 0.74f, 0.85f);

        [Tooltip("A print's material: multiplied onto what is under it (an alpha-modulating Particles/Unlit). Set by the scene builder.")]
        public Material PrintMaterial;

        [Tooltip("A puff's material: a soft dot. Set by the scene builder.")]
        public Material BreathMaterial;

        private sealed class Walker
        {
            public PlayerLocomotion Body;
            public Transform LeftFoot;
            public Transform RightFoot;
            public Transform Head;
            public readonly FootprintStepper Left = new FootprintStepper();
            public readonly FootprintStepper Right = new FootprintStepper();
            public float NextBreath;
        }

        private readonly List<Walker> _walkers = new List<Walker>();
        private readonly RaycastHit[] _ground = new RaycastHit[8];
        /// <summary>The prints are one mesh of this component's own, a quad each, laid along the way the body walks: a particle's turn in three axes came out sideways.</summary>
        private readonly Vector3[] _printVertices = new Vector3[MaxPrints * 4];
        private readonly Color32[] _printColours = new Color32[MaxPrints * 4];
        private readonly float[] _printBorn = new float[MaxPrints];
        private Mesh _printMesh;
        private int _nextPrint;
        private float _fadeIn;
        private ParticleSystem _breath;
        private AudioSource _crunch;
        private Texture2D _printShape;
        private Material _printMaterial;
        private float _refreshIn;

        /// <summary>Prints made since it began (for tests).</summary>
        public int PrintsMade { get; private set; }

        public int PrintsLying
        {
            get
            {
                int lying = 0;
                foreach (float born in _printBorn)
                {
                    if (Time.time - born < SnowRules.PrintSeconds)
                    {
                        lying++;
                    }
                }
                return lying;
            }
        }

        private void Awake()
        {
            BuildPrints();
            BuildBreath();
            var crunchHost = new GameObject("Crunch");
            crunchHost.transform.SetParent(transform, false);
            _crunch = crunchHost.AddComponent<AudioSource>();
            _crunch.playOnAwake = false;
            _crunch.spatialBlend = 1.0f;
            _crunch.maxDistance = HeardRange * 2.0f;
        }

        private void OnDestroy()
        {
            if (_printShape != null)
            {
                Destroy(_printShape);
            }
            if (_printMaterial != null)
            {
                Destroy(_printMaterial);
            }
            if (_printMesh != null)
            {
                Destroy(_printMesh);
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            FadePrints(deltaTime);
            if (CurrentWeather.SnowCover < SnowRules.TracksFromCover && CurrentWeather.SnowFalling <= 0.0f)
            {
                return;
            }
            _refreshIn -= deltaTime;
            if (_refreshIn <= 0.0f)
            {
                _refreshIn = RefreshBodiesSeconds;
                RefreshWalkers();
            }
            Camera camera = Camera.main;
            Vector3 eye = camera != null ? camera.transform.position : transform.position;
            for (int i = _walkers.Count - 1; i >= 0; i--)
            {
                Walker walker = _walkers[i];
                if (walker.Body == null || !walker.Body.isActiveAndEnabled)
                {
                    _walkers.RemoveAt(i);
                    continue;
                }
                float away = (walker.Body.transform.position - eye).sqrMagnitude;
                if (away > TrackRange * TrackRange)
                {
                    continue;
                }
                bool heard = away < HeardRange * HeardRange;
                Foot(walker, walker.LeftFoot, walker.Left, deltaTime, heard);
                Foot(walker, walker.RightFoot, walker.Right, deltaTime, heard);
                if (away < BreathRange * BreathRange)
                {
                    Breathe(walker);
                }
            }
        }

        private void RefreshWalkers()
        {
            PlayerLocomotion[] bodies = FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None);
            foreach (PlayerLocomotion body in bodies)
            {
                if (_walkers.Exists(walker => walker.Body == body))
                {
                    continue;
                }
                Animator animator = body.GetComponentInChildren<Animator>();
                if (animator == null || animator.avatar == null || !animator.isHuman)
                {
                    continue;
                }
                _walkers.Add(new Walker
                {
                    Body = body,
                    LeftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                    RightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot),
                    Head = animator.GetBoneTransform(HumanBodyBones.Head),
                    NextBreath = Time.time + Random.value * BreathEverySeconds,
                });
            }
        }

        private void Foot(Walker walker, Transform foot, FootprintStepper stepper, float deltaTime, bool heard)
        {
            if (foot == null || !stepper.Step(foot.position.y - walker.Body.transform.position.y, deltaTime))
            {
                return;
            }
            if (!GroundUnder(foot.position, out Vector3 point, out Vector3 normal) || CurrentWeather.SnowUnderfoot(point) < SnowRules.TracksFromCover)
            {
                return;
            }
            Vector3 along = Vector3.ProjectOnPlane(walker.Body.transform.forward, normal);
            if (along.sqrMagnitude < 1e-4f)
            {
                return;
            }
            AddPrint(point + normal * PrintLift, along.normalized, normal);
            PrintsMade++;
            if (heard && Application.isPlaying)
            {
                _crunch.transform.position = point;
                _crunch.pitch = Random.Range(0.85f, 1.2f);
                _crunch.PlayOneShot(SnowSounds.Crunch, CrunchVolume);
            }
        }

        /// <summary>The ground under a foot: the first thing that is not a body.</summary>
        private bool GroundUnder(Vector3 foot, out Vector3 point, out Vector3 normal)
        {
            point = foot;
            normal = Vector3.up;
            int count = Physics.RaycastNonAlloc(foot + Vector3.up * GroundCheckAbove, Vector3.down, _ground, GroundCheckAbove + GroundCheckBelow, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (_ground[i].distance < nearest && _ground[i].collider.GetComponentInParent<IDamageable>() == null)
                {
                    nearest = _ground[i].distance;
                    point = _ground[i].point;
                    normal = _ground[i].normal;
                    found = true;
                }
            }
            return found;
        }

        private void Breathe(Walker walker)
        {
            if (walker.Head == null || Time.time < walker.NextBreath)
            {
                return;
            }
            walker.NextBreath = Time.time + BreathEverySeconds;
            Vector3 forward = walker.Body.transform.forward;
            float scale = walker.Body.WorldHeight / BodyRig.StandingHeight;
            Vector3 mouth = walker.Head.position + forward * (FusedBodyShape.HeadRadius * scale * 1.05f) + Vector3.down * (FusedBodyShape.HeadRadius * scale * 0.3f);
            var puff = new ParticleSystem.EmitParams { position = mouth, velocity = forward * 0.45f + Vector3.up * 0.12f };
            _breath.Emit(puff, 3);
        }

        private void BuildPrints()
        {
            var host = new GameObject("Prints");
            host.transform.SetParent(transform, false);
            // The prints are in the world's own space, so the mesh stands at the world's middle.
            host.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var uv = new Vector2[MaxPrints * 4];
            var triangles = new int[MaxPrints * 6];
            for (int i = 0; i < MaxPrints; i++)
            {
                int at = i * 4, t = i * 6;
                uv[at] = new Vector2(0.0f, 0.0f);
                uv[at + 1] = new Vector2(1.0f, 0.0f);
                uv[at + 2] = new Vector2(1.0f, 1.0f);
                uv[at + 3] = new Vector2(0.0f, 1.0f);
                triangles[t] = at;
                triangles[t + 1] = at + 2;
                triangles[t + 2] = at + 1;
                triangles[t + 3] = at;
                triangles[t + 4] = at + 3;
                triangles[t + 5] = at + 2;
                _printBorn[i] = float.NegativeInfinity;
            }
            _printMesh = new Mesh { name = "SnowPrints", hideFlags = HideFlags.DontSave };
            _printMesh.MarkDynamic();
            _printMesh.SetVertices(_printVertices);
            _printMesh.uv = uv;
            _printMesh.SetColors(_printColours);
            _printMesh.triangles = triangles;
            // The prints are wherever the bodies walk: never culled as a mesh at the world's middle, which is where its bounds were when its triangles were set.
            _printMesh.bounds = new Bounds(Vector3.zero, Vector3.one * EverywhereMetres);
            host.AddComponent<MeshFilter>().sharedMesh = _printMesh;
            var renderer = host.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (PrintMaterial != null)
            {
                _printShape = MakePrintShape();
                _printMaterial = new Material(PrintMaterial) { hideFlags = HideFlags.DontSave };
                _printMaterial.SetTexture("_BaseMap", _printShape);
                renderer.sharedMaterial = _printMaterial;
            }
        }

        /// <summary>A print at <paramref name="centre"/>, heel to toe along <paramref name="along"/>, in place of the oldest.</summary>
        private void AddPrint(Vector3 centre, Vector3 along, Vector3 normal)
        {
            int at = _nextPrint * 4;
            Vector3 across = Vector3.Cross(normal, along).normalized * (PrintWidth * 0.5f);
            Vector3 ahead = along * (PrintLength * 0.5f);
            _printVertices[at] = centre - across - ahead;
            _printVertices[at + 1] = centre + across - ahead;
            _printVertices[at + 2] = centre + across + ahead;
            _printVertices[at + 3] = centre - across + ahead;
            _printBorn[_nextPrint] = Time.time;
            Shade(_nextPrint, 1.0f);
            _nextPrint = (_nextPrint + 1) % MaxPrints;
            _printMesh.SetVertices(_printVertices, 0, _printVertices.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            _printMesh.SetColors(_printColours);
        }

        private void Shade(int print, float strength)
        {
            Color32 shade = PrintShade;
            shade.a = (byte)(shade.a * Mathf.Clamp01(strength));
            for (int corner = 0; corner < 4; corner++)
            {
                _printColours[print * 4 + corner] = shade;
            }
        }

        /// <summary>The prints fill back in: whole for the first part of their time, then fading, looked at twice a second.</summary>
        private void FadePrints(float deltaTime)
        {
            _fadeIn -= deltaTime;
            if (_fadeIn > 0.0f)
            {
                return;
            }
            _fadeIn = FadeEverySeconds;
            bool changed = false;
            for (int i = 0; i < MaxPrints; i++)
            {
                if (float.IsNegativeInfinity(_printBorn[i]))
                {
                    continue;
                }
                float share = (Time.time - _printBorn[i]) / SnowRules.PrintSeconds;
                Shade(i, Mathf.InverseLerp(1.0f, WholeUntilShare, share));
                if (share >= 1.0f)
                {
                    _printBorn[i] = float.NegativeInfinity;
                }
                changed = true;
            }
            if (changed)
            {
                _printMesh.SetColors(_printColours);
            }
        }

        private void BuildBreath()
        {
            var host = new GameObject("Breath");
            host.transform.SetParent(transform, false);
            _breath = host.AddComponent<ParticleSystem>();
            _breath.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _breath.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 120;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSpeed = 0.0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.11f);
            main.startColor = new Color(0.92f, 0.95f, 1.0f, 0.3f);
            main.gravityModifier = 0.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = _breath.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _breath.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule grow = _breath.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 4.0f));
            ParticleSystem.ColorOverLifetimeModule fade = _breath.colorOverLifetime;
            fade.enabled = true;
            var thinning = new Gradient();
            thinning.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
            fade.color = thinning;

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = BreathMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _breath.Play();
        }

        /// <summary>A shoe's mark, up the texture from the heel: the sole a long oval, the heel a smaller one.</summary>
        private static Texture2D MakePrintShape()
        {
            const int width = 32, height = 64;
            var shape = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "SnowPrint", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width * 2.0f - 1.0f;
                    float v = (y + 0.5f) / height;
                    float sole = Oval(u, v, 0.66f, 0.3f, 0.88f);
                    float heel = Oval(u, v, 0.17f, 0.15f, 0.72f);
                    float alpha = Mathf.Max(sole, heel);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255.0f));
                }
            }
            shape.SetPixels32(pixels);
            shape.Apply(false, false);
            return shape;
        }

        private static float Oval(float u, float v, float middle, float halfLength, float halfWidth)
        {
            float along = (v - middle) / halfLength;
            float across = u / halfWidth;
            return Mathf.Clamp01((1.0f - (along * along + across * across)) * 5.0f);
        }
    }
}
