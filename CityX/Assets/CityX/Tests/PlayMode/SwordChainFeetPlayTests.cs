using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The light chains' feet on the real rig (docs/SWORD_FULL_BODY.md): the whole string thrown as a player throws it, one-handed and
    /// two-handed. Each cut strides on its own foot, which leaves the ground in the wind-up and is planted, still, as the blade lands, ahead
    /// of the other; the other foot stays where it stood until the blow. Measured on the foot bones in the world, so it is the clip's feet
    /// AND the step the game carries the body by (SwordStrikes.Stepped) that are checked: out of step, a planted foot slides.
    /// </summary>
    public sealed class SwordChainFeetPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        /// <summary>The next cut's clip takes over from the last one's across this long (SwordPoseDriver.ClipCrossfadeSeconds, and a frame or two).</summary>
        private const float AfterTheCrossfade = SwordPoseDriver.ClipCrossfadeSeconds + 0.03f;

        private GameObject _ground;
        private GameObject _player;
        private GameObject _sword;
        private int _capturedFramerate;

        [SetUp]
        public void SetUp()
        {
            _capturedFramerate = Time.captureFramerate;
            Time.captureFramerate = 60;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = _capturedFramerate;
            if (_player != null) Object.Destroy(_player);
            if (_sword != null) Object.Destroy(_sword);
            if (_ground != null) Object.Destroy(_ground);
        }

        private static readonly (SwordStrikeKind kind, bool rightStrides)[] OneHanded =
        {
            (SwordStrikeKind.Slash1H, false), (SwordStrikeKind.Cut1H, true), (SwordStrikeKind.Finisher1H, false),
        };

        private static readonly (SwordStrikeKind kind, bool rightStrides)[] TwoHanded =
        {
            (SwordStrikeKind.Sweep2H, false), (SwordStrikeKind.Reverse2H, true), (SwordStrikeKind.Cut2H, false),
        };

        [UnityTest]
        public IEnumerator TheOneHandedChainStridesOnAPlantedFootAndTheOtherStaysWhereItStood()
        {
            yield return ThrowTheChain("CombatSword", OneHanded);
        }

        [UnityTest]
        public IEnumerator TheTwoHandedChainStridesOnAPlantedFootAndTheOtherStaysWhereItStood()
        {
            yield return ThrowTheChain("SpikeSword", TwoHanded);
        }

        private IEnumerator ThrowTheChain(string weapon, (SwordStrikeKind kind, bool rightStrides)[] chain)
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Weapons/{weapon}.prefab");
            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            var vitals = _player.GetComponent<PlayerVitals>();
            vitals.InfiniteStamina = true;
            _sword = Object.Instantiate(swordPrefab, new Vector3(10.0f, 1.0f, 10.0f), Quaternion.identity);
            _sword.GetComponent<SwordWeapon>().OnEquip(_player, CityX.Items.ItemInstanceData.None);
            var combat = _player.GetComponent<SoulsSwordCombat>();
            combat.HitStopSeconds = 0.0f;
            var driver = _player.GetComponent<PlayerAnimationDriver>();
            var idle = new LocomotionInput { Sword1HEquipped = combat.IsOneHanded, SwordEquipped = !combat.IsOneHanded };
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot), right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            for (int i = 0; i < 60; i++)
            {
                locomotion.Tick(idle, Dt);
                yield return null;
            }

            foreach ((SwordStrikeKind kind, bool rightStrides) in chain)
            {
                string name = SwordStrikes.NameOf(kind);
                Transform striding = rightStrides ? right : left, standing = rightStrides ? left : right;
                for (int frames = 0; combat.CurrentAttack?.Animation != name; frames++)
                {
                    Assert.Less(frames, 180, $"{name} never started");
                    combat.RequestLight(0.0f);
                    combat.Tick(Dt);
                    vitals.TickHitstun(Dt);
                    locomotion.Tick(idle, Dt);
                    yield return null;
                }

                float seconds = SwordStrikes.Seconds(kind), contact = SwordStrikes.ContactFraction(kind) * seconds;
                Vector3? stood = null;
                float slid = 0.0f, highest = float.MinValue;
                Vector3 justBefore = striding.position;
                for (int frames = 0; driver.SwordPoser.Progress * seconds < contact || driver.SwordPoser.Kind != kind; frames++)
                {
                    Assert.Less(frames, 120, $"{name} never reached its contact");
                    justBefore = striding.position;
                    combat.Tick(Dt);
                    vitals.TickHitstun(Dt);
                    locomotion.Tick(idle, Dt);
                    yield return null;
                    float into = driver.SwordPoser.Kind == kind ? driver.SwordPoser.Progress * seconds : 0.0f;
                    highest = Mathf.Max(highest, striding.position.y);
                    if (into >= AfterTheCrossfade && into <= contact - 0.05f)
                    {
                        stood ??= standing.position;
                        Vector3 gone = standing.position - stood.Value;
                        slid = Mathf.Max(slid, new Vector2(gone.x, gone.z).magnitude);
                    }
                }
                Assert.IsTrue(stood.HasValue, $"{name}: no frame between its start and its blow was measured");
                Assert.Greater(driver.SwordPoser.ClipWeight, 0.99f, $"{name}: its clip does not hold the body as it lands");
                float lifted = highest - striding.position.y;
                float ahead = Vector3.Dot(striding.position - standing.position, _player.transform.forward);
                float moving = Vector3.Distance(striding.position, justBefore);
                Debug.Log($"CityX sword feet, {name}: the striding foot lifted {lifted * 100.0f:0} cm and lands {ahead * 100.0f:0} cm ahead of the other, " +
                          $"moving {moving * 100.0f:0.0} cm in the blow's frame; the standing foot slid {slid * 100.0f:0} cm before the blow");
                Assert.Greater(lifted, 0.04f, $"{name}: the foot it strides on never left the ground");
                Assert.Greater(ahead, 0.2f, $"{name}: no stride under the blow");
                Assert.Less(moving, 0.02f, $"{name}: the striding foot is still moving as the blade lands");
                // (Rolling up onto its ball, the ankle goes a few centimetres forward over toes that have not moved.)
                Assert.Less(slid, 0.1f, $"{name}: the standing foot slides {slid * 100.0f:0} cm along the ground before the blow");
            }
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
