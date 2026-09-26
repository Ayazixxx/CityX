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
    /// THE ARMS WHILE SHOOTING THE PISTOL. Reported: "sometimes the arms spazz out a bit when shooting."
    /// A spasm is a bone that jumps -- turns a long way in a single frame -- so this fires real shots
    /// (through PistolWeapon.Fire, so the kick, the flash, the fire-pose hold and the support hand all run)
    /// while aiming and from the hip, and records the most any arm bone turns from one frame to the next.
    /// </summary>
    public sealed class PistolArmsSteadyPlayTests
    {
        private static readonly HumanBodyBones[] Tracked =
        {
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        };

        private GameObject _ground;
        private GameObject _player;
        private GameObject _pistolObject;
        private GameObject _light;
        private PlayerLocomotion _locomotion;
        private Animator _animator;
        private PistolWeapon _pistol;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_pistolObject != null) Object.Destroy(_pistolObject);
            if (_ground != null) Object.Destroy(_ground);
            if (_light != null) Object.Destroy(_light);
        }

        private void Build(bool supportHand, bool kick)
        {
#if UNITY_EDITOR
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
            if (!supportHand)
            {
                _locomotion.ClearSupportHand(_pistol.LeftHandGrip);
            }
            if (!kick)
            {
                _pistol.KickBackMetres = 0.0f;
                _pistol.KickUpDegrees = 0.0f;
            }
#endif
        }

        private struct Spike
        {
            public string Bone;
            public float Degrees;
            public float Dt;
            public float Time;
        }

        /// <summary>Fires a shot every <paramref name="interval"/> seconds for <paramref name="seconds"/>, recording every bone's turn per frame.</summary>
        private IEnumerator Shoot(bool aiming, float seconds, float interval, List<Spike> spikes)
        {
            Vector3 chest = _player.transform.position + Vector3.up * 1.4f;
            Vector3 cameraPosition = chest + Vector3.right * 0.65f - Vector3.forward * 4.0f;
            var previous = new Dictionary<HumanBodyBones, Quaternion>();
            float elapsed = 0.0f;
            float nextShot = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                _locomotion.Tick(new LocomotionInput
                {
                    Aiming = aiming,
                    GunEquipped = true,
                    AimCameraPosition = cameraPosition,
                    AimCameraForward = Vector3.forward,
                }, dt);
                if (elapsed >= nextShot)
                {
                    nextShot += interval;
                    _pistol.Fire(cameraPosition, Vector3.forward, aiming, _player);
                }
                yield return null;
                elapsed += dt;

                foreach (HumanBodyBones bone in Tracked)
                {
                    Quaternion now = _animator.GetBoneTransform(bone).rotation;
                    if (previous.TryGetValue(bone, out Quaternion before) && dt > 1e-5f)
                    {
                        spikes.Add(new Spike { Bone = bone.ToString(), Degrees = Quaternion.Angle(before, now), Dt = dt, Time = elapsed });
                    }
                    previous[bone] = now;
                }
            }
        }

        private static string Worst(List<Spike> spikes, string label)
        {
            spikes.Sort((a, b) => b.Degrees.CompareTo(a.Degrees));
            var text = new System.Text.StringBuilder($"CityX arms {label}: ");
            for (int i = 0; i < Mathf.Min(5, spikes.Count); i++)
            {
                text.Append($"{spikes[i].Bone} {spikes[i].Degrees:0.0} deg in one frame (dt {spikes[i].Dt * 1000:0.0}ms) @{spikes[i].Time:0.00}s; ");
            }
            return text.ToString();
        }

        private IEnumerator Measure(bool aiming, bool supportHand, bool kick, string label, System.Action<float> worstOut)
        {
            Build(supportHand, kick);
            yield return Shoot(aiming, 0.8f, 100.0f, new List<Spike>());   // settle into the pose (one shot at the start)
            var spikes = new List<Spike>();
            yield return Shoot(aiming, 3.0f, 0.3f, spikes);
            Debug.Log(Worst(spikes, label));
            worstOut(spikes.Count > 0 ? spikes[0].Degrees : 0.0f);
            TearDown();
        }

        /// <summary>
        /// No arm bone may turn faster than this, in degrees per second, in any frame of a steady aimed burst.
        /// The spasm was 5-6.6 degrees in a sub-millisecond frame: 7000+ deg/s. A steady aim measures under a
        /// tenth of this.
        /// </summary>
        private const float SteadyDegreesPerSecond = 2000.0f;

        private static float FastestAfter(List<Spike> spikes, float afterSeconds, out string where)
        {
            float worst = 0.0f;
            where = "";
            foreach (Spike spike in spikes)
            {
                if (spike.Time < afterSeconds)
                {
                    continue;
                }
                float rate = spike.Degrees / spike.Dt;
                if (rate > worst)
                {
                    worst = rate;
                    where = $"{spike.Bone} {spike.Degrees:0.0} deg in {spike.Dt * 1000:0.0}ms @{spike.Time:0.00}s";
                }
            }
            return worst;
        }

        private IEnumerator AimedBurst(bool supportHand)
        {
            Build(supportHand, kick: true);
            yield return Shoot(true, 0.8f, 100.0f, new List<Spike>());   // settle into the aim
            var spikes = new List<Spike>();
            yield return Shoot(true, 3.0f, 0.3f, spikes);
            float worst = FastestAfter(spikes, 0.0f, out string where);
            Debug.Log($"CityX arms aimed burst (support hand {supportHand}): fastest {worst:0} deg/s ({where})");
            Assert.Less(worst, SteadyDegreesPerSecond, $"the arms jerk when shooting while aiming: {where}");
        }

        [UnityTest]
        public IEnumerator TheArmsStaySteadyThroughAnAimedBurst()
        {
#if UNITY_EDITOR
            yield return AimedBurst(supportHand: true);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheArmsStaySteadyThroughAnAimedBurstWithoutTheFreeHandToo()
        {
#if UNITY_EDITOR
            yield return AimedBurst(supportHand: false);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ARaisedHipFiredBurstDoesNotJerkTheArmsOnceTheGunIsUp()
        {
#if UNITY_EDITOR
            Build(supportHand: true, kick: true);
            var spikes = new List<Spike>();
            // The first shot raises the arm (fast, by design); judge the shots that follow it.
            yield return Shoot(false, 3.0f, 0.3f, spikes);
            float worst = FastestAfter(spikes, 0.6f, out string where);
            Debug.Log($"CityX arms hip burst after the raise: fastest {worst:0} deg/s ({where})");
            Assert.Less(worst, SteadyDegreesPerSecond, $"the arms jerk on each hip-fired shot once the gun is up: {where}");
#else
            yield break;
#endif
        }
    }
}
