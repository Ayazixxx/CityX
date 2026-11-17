using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE GUN'S AIM WHEN WHAT THE CROSSHAIR IS ON JUMPS IN DEPTH. Reported: "the pistol glitches around a bunch
    /// and starts pointing in other directions than intended, and the bullet trails are equally spazzy".
    ///
    /// The arm aims at where the crosshair ray ENDS -- and that end point is not continuous: sweep the crosshair
    /// across a facade with shot-out windows and it flicks between the glass at 3m and the room behind at 30m;
    /// a lamp post or a flying shard crossing the ray is a one-frame hit at 2m. The camera is three metres behind
    /// the gun and to one side, so a 3m point and a 30m point along the same ray are ~20 degrees apart as seen
    /// from the shoulder, and the arm was following every one of them instantly.
    ///
    /// This flickers the aim point between near and far along ONE steady camera ray -- what a sweep across bad
    /// geometry looks like to the arm -- and records how fast the bones and the muzzle turn.
    /// </summary>
    public sealed class PistolAimDepthJumpPlayTests
    {
        private static readonly HumanBodyBones[] Tracked =
        {
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand,
        };

        private GameObject _ground;
        private GameObject _player;
        private GameObject _pistolObject;
        private GameObject _light;
        private PlayerLocomotion _locomotion;
        private Animator _animator;
        private PistolWeapon _pistol;
        private WeaponGripSettings _realSettings;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_pistolObject != null) Object.Destroy(_pistolObject);
            if (_ground != null) Object.Destroy(_ground);
            if (_light != null) Object.Destroy(_light);
            // The real holds back, or every test after this one holds its weapons in the untuned hold.
            if (_realSettings != null)
            {
                Object.Destroy(WeaponGripSettings.Current);
                WeaponGripSettings.Current = _realSettings;
                _realSettings = null;
            }
        }

        private void Build()
        {
#if UNITY_EDITOR
            _realSettings ??= WeaponGripSettings.Current;
            WeaponGripSettings.Current = ScriptableObject.CreateInstance<WeaponGripSettings>();
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.1f, 0.0f), Quaternion.identity);
            var reader = _player.GetComponent<PlayerInputReader>();
            if (reader != null) reader.enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            _light = new GameObject("Light");
            _light.AddComponent<Light>().type = LightType.Directional;
            var pistolPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Pistol.prefab");
            _pistolObject = Object.Instantiate(pistolPrefab, new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
            _pistol = _pistolObject.GetComponent<PistolWeapon>();
            _pistol.OnEquip(_player, CityX.Items.ItemInstanceData.None);
#endif
        }

        private struct Worst
        {
            public float BoneDegreesPerSecond;
            public float MuzzleDegreesPerSecond;
            public string Where;
        }

        /// <summary>
        /// Holds the camera dead steady and flicks the reported aim point between <paramref name="near"/> and
        /// <paramref name="far"/> metres along its ray every <paramref name="stepSeconds"/> seconds.
        /// </summary>
        private IEnumerator Flicker(bool aiming, float near, float far, float stepSeconds, float seconds, float measureAfter, System.Action<Worst> done)
        {
            Vector3 chest = _player.transform.position + Vector3.up * 1.4f;
            Vector3 cameraPosition = chest + Vector3.right * 0.65f - Vector3.forward * 4.0f;
            Vector3 cameraForward = Vector3.forward;

            var history = new List<Sample>();
            int oldest = 0;
            var worst = new Worst();
            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                float depth = (((int)(elapsed / stepSeconds)) % 2 == 0) ? near : far;
                _locomotion.Tick(new LocomotionInput
                {
                    Aiming = aiming,
                    GunEquipped = true,
                    AimCameraPosition = cameraPosition,
                    AimCameraForward = cameraForward,
                    GunAimPoint = cameraPosition + cameraForward * depth,
                    HasGunAimPoint = true,
                }, dt);
                yield return null;
                elapsed += dt;

                var sample = new Sample { Time = elapsed, Bones = new Quaternion[Tracked.Length] };
                for (int b = 0; b < Tracked.Length; b++)
                {
                    sample.Bones[b] = _animator.GetBoneTransform(Tracked[b]).rotation;
                }
                sample.Muzzle = _locomotion.GunMuzzle != null ? _locomotion.GunMuzzle.forward : Vector3.forward;
                history.Add(sample);

                // Rates are taken over at least RateWindowSeconds, so a run of sub-millisecond batch frames
                // (where half a degree of settling is "1000 deg/s") is judged the way it would look on screen.
                while (oldest + 1 < history.Count && elapsed - history[oldest + 1].Time >= RateWindowSeconds)
                {
                    oldest++;
                }
                Sample past = history[oldest];
                float window = elapsed - past.Time;
                if (window >= RateWindowSeconds && elapsed > measureAfter)
                {
                    for (int b = 0; b < Tracked.Length; b++)
                    {
                        float rate = Quaternion.Angle(past.Bones[b], sample.Bones[b]) / window;
                        if (rate > worst.BoneDegreesPerSecond)
                        {
                            worst.BoneDegreesPerSecond = rate;
                            worst.Where = $"{Tracked[b]} {Quaternion.Angle(past.Bones[b], sample.Bones[b]):0.0} deg over {window * 1000:0.0}ms @{elapsed:0.00}s";
                        }
                    }
                    worst.MuzzleDegreesPerSecond = Mathf.Max(worst.MuzzleDegreesPerSecond, Vector3.Angle(past.Muzzle, sample.Muzzle) / window);
                }
            }
            done(worst);
        }

        private struct Sample
        {
            public float Time;
            public Quaternion[] Bones;
            public Vector3 Muzzle;
        }

        /// <summary>Turn rates are measured over at least this long -- one 60fps frame.</summary>
        private const float RateWindowSeconds = 0.016f;

        /// <summary>The arm's fastest turn in any frame. Smoothed, a near/far flick measures ~170 at the muzzle and ~560 at the free hand (short forearm: it turns ~3x the gun); the unsmoothed snap measured 2,100 (34 degrees in one frame).</summary>
        private const float SteadyBoneDegreesPerSecond = 800.0f;
        private const float SteadyMuzzleDegreesPerSecond = 500.0f;

        [UnityTest]
        public IEnumerator AimedGunStaysSteadyWhenTheHitDepthFlickersNearAndFar()
        {
#if UNITY_EDITOR
            Build();
            Worst worst = default;
            yield return Flicker(true, 3.0f, 30.0f, 0.08f, 3.0f, 1.0f, w => worst = w);
            Debug.Log($"CityX aim depth flicker (aimed): bones {worst.BoneDegreesPerSecond:0} deg/s ({worst.Where}), muzzle {worst.MuzzleDegreesPerSecond:0} deg/s");
            Assert.Less(worst.BoneDegreesPerSecond, SteadyBoneDegreesPerSecond, $"an arm bone snaps when the aim depth jumps: {worst.Where}");
            Assert.Less(worst.MuzzleDegreesPerSecond, SteadyMuzzleDegreesPerSecond, "the barrel swings when the aim depth jumps");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AHipFiredGunStaysSteadyWhenTheHitDepthFlickersToo()
        {
#if UNITY_EDITOR
            Build();
            Worst worst = default;
            // Fired once so the arm is raised, then flickered: what hip fire across a facade looks like.
            _pistol.Fire(_player.transform.position + Vector3.up * 1.6f, Vector3.forward, false, _player);
            yield return Flicker(false, 3.0f, 30.0f, 0.08f, 0.45f, 0.0f, _ => { });
            _pistol.Fire(_player.transform.position + Vector3.up * 1.6f, Vector3.forward, false, _player);
            yield return Flicker(false, 3.0f, 30.0f, 0.08f, 0.4f, 0.0f, w => worst = w);
            Debug.Log($"CityX aim depth flicker (hip): bones {worst.BoneDegreesPerSecond:0} deg/s ({worst.Where}), muzzle {worst.MuzzleDegreesPerSecond:0} deg/s");
            Assert.Less(worst.BoneDegreesPerSecond, SteadyBoneDegreesPerSecond, $"an arm bone snaps when the aim depth jumps: {worst.Where}");
#else
            yield break;
#endif
        }
    }
}
