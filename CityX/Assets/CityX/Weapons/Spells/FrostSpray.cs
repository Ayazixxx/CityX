using CityX.Character;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Weapons
{
    /// <summary>
    /// The Frost Staff's spray, drawn in code (docs/FROST.md; owner: "you hold click to spray a cloud of frost"): from the staff's head along
    /// the crosshair, a cloud of white-blue frost billowing out and widening as it goes, glittering ice crystals through it, gone by the end of
    /// its reach; a hiss with it. The same on another player's copy of the staff.
    /// </summary>
    public sealed class FrostSpray : MonoBehaviour
    {
        public static readonly Color Frost = new Color(0.86f, 0.95f, 1.0f, 0.55f);
        public static readonly Color Crystal = new Color(0.65f, 0.88f, 1.0f, 1.0f);

        /// <summary>How fast the frost leaves the staff, metres a second, so it lives as long as it takes to cross its reach.</summary>
        public const float Speed = 14.0f;

        /// <summary>Puffs and crystals a second.</summary>
        public const float PuffsPerSecond = 70.0f;

        public const float CrystalsPerSecond = 90.0f;

        public const float HissVolume = 0.35f;

        /// <summary>The water jet's hiss, played higher: a cold rush.</summary>
        public const float HissPitch = 1.35f;

        private static Material _puffMaterial;
        private static Material _crystalMaterial;
        private ParticleSystem _puffs;
        private ParticleSystem _crystals;
        private AudioSource _hiss;
        private float _puffDebt;
        private float _crystalDebt;

        public bool IsRunning { get; private set; }

        /// <summary>A spray on <paramref name="tip"/> (the staff's head), stopped until <see cref="Run"/>.</summary>
        public static FrostSpray Make(Transform tip)
        {
            var go = new GameObject("FrostSpray");
            go.transform.SetParent(tip, false);
            var spray = go.AddComponent<FrostSpray>();
            spray.Build();
            return spray;
        }

        private void Build()
        {
            _puffs = Particles("Puffs", PuffMaterial(), 0.25f, 0.5f, 3.2f, 0.75f);
            _crystals = Particles("Crystals", CrystalMaterial(), 0.04f, 0.1f, 1.0f, 1.0f);
            if (Application.isPlaying)
            {
                _hiss = gameObject.AddComponent<AudioSource>();
                _hiss.clip = WaterSounds.Hiss;
                _hiss.loop = true;
                _hiss.pitch = HissPitch;
                _hiss.volume = HissVolume;
                _hiss.spatialBlend = 1.0f;
                _hiss.playOnAwake = false;
            }
        }

        /// <summary>Particles thrown along the spray: <paramref name="grow"/> times their size by the end of their life.</summary>
        private ParticleSystem Particles(string name, Material material, float leastSize, float mostSize, float grow, float speedShare)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            float life = FrostRules.Range / Speed;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(Speed * 0.8f * speedShare, Speed * speedShare);
            main.startSize = new ParticleSystem.MinMaxCurve(leastSize, mostSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, 2.0f * Mathf.PI);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0.0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 1.0f, 1.0f, grow));
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.12f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0.0f, 1.0f) });
            fade.color = alpha;
            ParticleSystem.VelocityOverLifetimeModule drag = particles.velocityOverLifetime;
            drag.enabled = true;
            drag.speedModifier = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.35f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        public void Run()
        {
            if (IsRunning)
            {
                return;
            }
            IsRunning = true;
            if (_hiss != null)
            {
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
            if (_hiss != null)
            {
                _hiss.Stop();
            }
        }

        /// <summary>One frame of spray from <paramref name="from"/> along <paramref name="direction"/>: frost and crystals thrown out in its cone.</summary>
        public void Spray(Vector3 from, Vector3 direction, float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }
            _puffDebt += PuffsPerSecond * deltaTime;
            _crystalDebt += CrystalsPerSecond * deltaTime;
            Throw(_puffs, ref _puffDebt, from, direction, Speed * 0.9f);
            Throw(_crystals, ref _crystalDebt, from, direction, Speed);
        }

        private static void Throw(ParticleSystem particles, ref float debt, Vector3 from, Vector3 direction, float speed)
        {
            var emit = new ParticleSystem.EmitParams();
            while (debt >= 1.0f)
            {
                debt -= 1.0f;
                Vector3 spread = Quaternion.AngleAxis(Random.Range(0.0f, 360.0f), direction)
                    * Quaternion.AngleAxis(Random.Range(0.0f, FrostRules.ConeHalfAngleDegrees), Perpendicular(direction)) * direction;
                emit.position = from;
                emit.velocity = spread * speed * Random.Range(0.8f, 1.0f);
                particles.Emit(emit, 1);
            }
        }

        private static Vector3 Perpendicular(Vector3 direction)
        {
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            return side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.right;
        }

        /// <summary>A puff of frost: blended, not glowing (mist hides what is behind it), soft round.</summary>
        private static Material PuffMaterial() => _puffMaterial != null ? _puffMaterial : (_puffMaterial = SoftGlow.Make("FrostPuff", Frost, false));

        private static Material CrystalMaterial() => _crystalMaterial != null ? _crystalMaterial : (_crystalMaterial = SoftGlow.Make("FrostCrystal", Crystal, true));
    }
}
