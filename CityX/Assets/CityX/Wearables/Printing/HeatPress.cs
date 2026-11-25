using CityX.Character;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// THE HEAT PRESS, A PIECE OF FURNITURE (docs/HEAT_PRESS.md; owner, 2026-11-24: "the heat press is furniture", "I want parts of it to move ...
    /// when initiated, have it clamp down for 3 seconds and then steam puffs out of it and the shirt is done"). Moves its lid and drawer as
    /// <see cref="HeatPressCycle"/> says for the seconds since its pressing began, and puffs steam from round the plates as the lid lifts. It only
    /// shows a pressing: what starts one (and prints the shirt) is told the start time, so every machine shows the same moment.
    /// </summary>
    public sealed class HeatPress : MonoBehaviour
    {
        [Tooltip("Turns the lid about the pivot (HeatPressShape.Pivot); the lid's mesh is its child.")]
        public Transform LidPivot;

        [Tooltip("The lower platen, slid out along +z.")]
        public Transform Drawer;

        /// <summary>How many puffs the steam is.</summary>
        public const int SteamPuffs = 40;

        public static readonly Color SteamColour = new Color(0.95f, 0.95f, 0.97f, 0.7f);

        private static Material _steamMaterial;

        private ParticleSystem _steam;
        private float _startedAt = float.NegativeInfinity;
        private bool _steamed = true;

        /// <summary>Whether a pressing is under way.</summary>
        public bool Pressing => Seconds < HeatPressCycle.TotalSeconds;

        /// <summary>Seconds into the pressing under way (a large number when none).</summary>
        public float Seconds => Time.time - _startedAt;

        /// <summary>A pressing begins at <paramref name="startedAt"/> (Time.time; in the past when told late).</summary>
        public void Begin(float startedAt)
        {
            _startedAt = startedAt;
            _steamed = Seconds > HeatPressCycle.SteamAt + 1.0f;   // a pressing heard of long after its steam shows none
        }

        public void Begin() => Begin(Time.time);

        private void Awake()
        {
            Show(HeatPressCycle.At(float.PositiveInfinity));
        }

        private void Update()
        {
            float seconds = Seconds;
            Show(HeatPressCycle.At(seconds));
            if (!_steamed && seconds >= HeatPressCycle.SteamAt)
            {
                _steamed = true;
                Puff();
            }
        }

        private void Show(HeatPressCycle.Pose pose)
        {
            if (LidPivot != null) LidPivot.localRotation = HeatPressShape.LidTurn(pose.LidDegrees);
            if (Drawer != null) Drawer.localPosition = Vector3.forward * pose.DrawerOut;
        }

        /// <summary>Steam out of the gap between the plates, all round, rising and spreading.</summary>
        private void Puff()
        {
            if (_steam == null)
            {
                _steam = MakeSteam(transform);
            }
            var emit = new ParticleSystem.EmitParams();
            Vector2 half = HeatPressShape.PadHalf;
            for (int i = 0; i < SteamPuffs; i++)
            {
                // Round the plates' edge: the front and the two sides.
                float along = Random.value * 2.0f - 1.0f;
                Vector3 local;
                Vector3 outward;
                switch (i % 3)
                {
                    case 0: local = new Vector3(along * half.x, 0.0f, half.y); outward = Vector3.forward; break;
                    case 1: local = new Vector3(half.x, 0.0f, along * half.y); outward = Vector3.right; break;
                    default: local = new Vector3(-half.x, 0.0f, along * half.y); outward = Vector3.left; break;
                }
                local.y = HeatPressShape.PadTop + 0.03f;
                emit.position = transform.TransformPoint(local);
                emit.velocity = transform.TransformDirection(outward * Random.Range(0.1f, 0.35f) + Vector3.up * Random.Range(0.35f, 0.8f));
                emit.startSize = Random.Range(0.12f, 0.24f);
                emit.startLifetime = Random.Range(1.0f, 1.8f);
                _steam.Emit(emit, 1);
            }
        }

        private static ParticleSystem MakeSteam(Transform press)
        {
            var go = new GameObject("Steam");
            go.transform.SetParent(press, false);
            ParticleSystem steam = go.AddComponent<ParticleSystem>();
            steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = steam.main;
            main.loop = true;   // always playing, emitting nothing by itself: each pressing's puff is emitted by hand
            main.startColor = SteamColour;
            main.gravityModifier = -0.02f;   // warm: it rises on its own
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = SteamPuffs * 3;
            ParticleSystem.EmissionModule emission = steam.emission;
            emission.enabled = false;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = steam.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.5f;
            ParticleSystem.ColorOverLifetimeModule fade = steam.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.9f, 0.0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0.0f, 1.0f) });
            fade.color = alpha;
            ParticleSystem.SizeOverLifetimeModule swell = steam.sizeOverLifetime;
            swell.enabled = true;
            swell.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 0.6f, 1.0f, 2.2f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _steamMaterial != null ? _steamMaterial : (_steamMaterial = SoftGlow.Make("HeatPressSteam", SteamColour, false));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            steam.Play();
            return steam;
        }
    }
}
