using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// WATER ANSWERING THE BODIES IN IT (docs/WATER.md): a ring spreading from each stride of a body wading, drops kicked up when it runs
    /// through deeper water, a ring round a body standing or floating in it now and then, and a burst where one comes down into it. Every
    /// humanoid body in the scene, this player's, other players' copies and the people of the city alike, so everyone sees everyone's
    /// splashes from where the bodies are (nothing is sent). Local and faked, the way games do the water round a character: two pooled
    /// particle systems, no ripple simulation. Asleep while the world has no water.
    ///
    /// Also holds the water's materials, for whatever makes water at run time (the console, a spell later): <see cref="Current"/>.
    /// </summary>
    public sealed class WaterSplashes : MonoBehaviour
    {
        [Tooltip("The water's surface (CityX/Water).")]
        public Material WaterMaterial;

        [Tooltip("The rings (the rain's own ring material).")]
        public Material RingMaterial;

        [Tooltip("The drops kicked up (the rain's own splash material).")]
        public Material DropMaterial;

        /// <summary>The scene's, for whatever makes water at run time.</summary>
        public static WaterSplashes Current { get; private set; }

        /// <summary>How often the bodies in the scene are looked for again, seconds.</summary>
        public const float FindBodiesSeconds = 1.0f;

        /// <summary>A ring for every this much a body wades, metres (a stride).</summary>
        public const float StrideMetres = 0.6f;

        /// <summary>Water shallower than this makes no rings, metres (wet ground is the rain's).</summary>
        public const float ShallowestRings = 0.04f;

        /// <summary>A ring round a body standing still in water this often, seconds.</summary>
        public const float StillRingSeconds = 1.6f;

        /// <summary>Wading faster than this kicks up drops, metres a second (a run, not a walk).</summary>
        public const float DropsAboveSpeed = 3.0f;

        /// <summary>Coming down into water faster than this is a splash, metres a second.</summary>
        public const float PlungeSpeed = 3.5f;

        private const float RingLift = 0.015f;
        private const float RingSize = 0.9f;

        private static readonly Color RingColour = new Color(0.86f, 0.92f, 1.0f, 0.55f);
        private static readonly Color DropColour = new Color(0.86f, 0.92f, 1.0f, 0.75f);

        private struct Wader
        {
            public Animator Body;
            public Vector3 Last;
            public float Travelled;
            public float Still;
            public bool LeftNext;
            public bool WasIn;
        }

        private readonly List<Wader> _waders = new List<Wader>();
        private ParticleSystem _rings;
        private ParticleSystem _drops;
        private float _findTimer;

        /// <summary>Rings and drops ever started, for tests.</summary>
        public int RingsEmitted { get; private set; }

        public int DropsEmitted { get; private set; }

        private void OnEnable()
        {
            Current = this;
            if (_rings == null)
            {
                _rings = RippleRings.Build(transform, "WadingRings", RingMaterial, 300, 0.75f, RingSize, RingColour);
                _drops = BuildDrops(transform, DropMaterial);
            }
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void Update()
        {
            if (!CurrentWater.Any)
            {
                _waders.Clear();
                _findTimer = 0.0f;
                return;
            }
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0.0f)
            {
                return;
            }
            _findTimer -= deltaTime;
            if (_findTimer <= 0.0f)
            {
                FindBodies();
                _findTimer = FindBodiesSeconds;
            }
            for (int i = 0; i < _waders.Count; i++)
            {
                _waders[i] = Step(_waders[i], deltaTime);
            }
        }

        /// <summary>Every humanoid body in the scene, kept with what was known of each already.</summary>
        private void FindBodies()
        {
            var known = new Dictionary<Animator, Wader>();
            foreach (Wader wader in _waders)
            {
                if (wader.Body != null)
                {
                    known[wader.Body] = wader;
                }
            }
            _waders.Clear();
            foreach (Animator body in FindObjectsByType<Animator>(FindObjectsSortMode.None))
            {
                if (body == null || !body.isActiveAndEnabled || !body.isHuman)
                {
                    continue;
                }
                _waders.Add(known.TryGetValue(body, out Wader was) ? was : new Wader { Body = body, Last = body.transform.position });
            }
        }

        private Wader Step(Wader wader, float deltaTime)
        {
            if (wader.Body == null)
            {
                return wader;
            }
            Vector3 at = wader.Body.transform.position;
            Vector3 moved = at - wader.Last;
            wader.Last = at;
            float fall = -moved.y / deltaTime;
            moved.y = 0.0f;
            float distance = moved.magnitude;
            float speed = distance / deltaTime;

            float depth = CurrentWater.DepthAt(at, out WaterBody water);
            bool inWater = water != null && depth >= ShallowestRings;
            if (!inWater)
            {
                wader.WasIn = false;
                return wader;
            }
            float surface = water.SurfaceY;
            if (!wader.WasIn && fall >= PlungeSpeed)
            {
                Plunge(new Vector3(at.x, surface, at.z), fall);
            }
            wader.WasIn = true;

            // A ring a stride from alternate feet, bigger the deeper the water; drops kicked up running through it past the ankles.
            wader.Travelled += distance;
            if (wader.Travelled >= StrideMetres && distance > 0.0f)
            {
                wader.Travelled = 0.0f;
                Transform foot = wader.Body.GetBoneTransform(wader.LeftNext ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                wader.LeftNext = !wader.LeftNext;
                Vector3 from = foot != null ? foot.position : at;
                Ring(new Vector3(from.x, surface, from.z), RingSize * Mathf.Lerp(0.8f, 1.5f, Mathf.Clamp01(depth / 1.2f)));
                if (speed >= DropsAboveSpeed && depth >= 0.15f)
                {
                    Drops(new Vector3(from.x, surface, from.z), moved / Mathf.Max(distance, 1e-4f), 4, speed);
                }
            }

            // Standing (or floating) still: a slow ring round the body now and then.
            wader.Still = speed < 0.3f ? wader.Still + deltaTime : 0.0f;
            if (wader.Still >= StillRingSeconds)
            {
                wader.Still = 0.0f;
                Ring(new Vector3(at.x, surface, at.z), RingSize * 1.4f);
            }
            return wader;
        }

        private void Ring(Vector3 onSurface, float size)
        {
            _rings.Emit(new ParticleSystem.EmitParams { position = onSurface + Vector3.up * RingLift, startSize = size, applyShapeToPosition = false }, 1);
            RingsEmitted++;
        }

        /// <summary>Drops thrown up and forward from a stride, falling back.</summary>
        private void Drops(Vector3 onSurface, Vector3 forward, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 kick = forward * Random.Range(0.4f, 1.0f) * Mathf.Min(speed, 8.0f) * 0.35f
                               + Random.insideUnitSphere * 0.6f + Vector3.up * Random.Range(1.6f, 2.8f);
                _drops.Emit(new ParticleSystem.EmitParams { position = onSurface + Vector3.up * 0.05f, velocity = kick }, 1);
            }
            DropsEmitted += count;
        }

        /// <summary>Coming down into water: a crown of drops and a wide ring, bigger the harder.</summary>
        private void Plunge(Vector3 onSurface, float fall)
        {
            float strength = Mathf.Clamp01(fall / 15.0f);
            Ring(onSurface, RingSize * Mathf.Lerp(1.8f, 3.5f, strength));
            int count = Mathf.RoundToInt(Mathf.Lerp(10.0f, 30.0f, strength));
            for (int i = 0; i < count; i++)
            {
                Vector2 round = Random.insideUnitCircle.normalized * Random.Range(0.6f, 2.2f);
                var kick = new Vector3(round.x, Random.Range(2.5f, 4.5f) * Mathf.Lerp(1.0f, 1.6f, strength), round.y);
                _drops.Emit(new ParticleSystem.EmitParams { position = onSurface + Vector3.up * 0.05f, velocity = kick }, 1);
            }
            DropsEmitted += count;
        }

        /// <summary>Drops: small, thrown, falling under gravity, gone in under a second.</summary>
        private static ParticleSystem BuildDrops(Transform parent, Material material)
        {
            var host = new GameObject("WadingDrops");
            host.transform.SetParent(parent, false);
            var drops = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = drops.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 400;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = 0.0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = DropColour;
            main.gravityModifier = 1.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = drops.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = drops.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule fade = drops.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.7f), new GradientAlphaKey(0.0f, 1.0f) });
            fade.color = alpha;
            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return drops;
        }
    }
}
