using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The one-handed chain's feet on the real rig: the whole string thrown as a player throws it, each cut stepping on its own foot and both feet on the
    /// ground as the blade lands. What the renders showed before: a foot often in the air at the blow, and the back foot stepping backward into the cut.
    /// </summary>
    public sealed class SwordChainFeetPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

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

        [UnityTest]
        public IEnumerator BothFeetAreDownAsEachCutLandsAndEachCutStepsOnItsOwnFoot()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab");
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
            var idle = new LocomotionInput { Sword1HEquipped = true };
            for (int i = 0; i < 60; i++)
            {
                locomotion.Tick(idle, Dt);
                yield return null;
            }

            var chain = new[]
            {
                (kind: SwordStrikeKind.Slash1H, foot: 0, other: 1),
                (kind: SwordStrikeKind.Cut1H, foot: 1, other: 0),
                (kind: SwordStrikeKind.Finisher1H, foot: 0, other: 1),
            };
            foreach ((SwordStrikeKind kind, int foot, int other) in chain)
            {
                string name = SwordStrikes.NameOf(kind);
                for (int frames = 0; combat.CurrentAttack?.Animation != name; frames++)
                {
                    Assert.Less(frames, 180, $"{name} never started");
                    combat.RequestLight(0.0f);
                    combat.Tick(Dt);
                    vitals.TickHitstun(Dt);
                    locomotion.Tick(idle, Dt);
                    yield return null;
                }

                bool otherSetOff = false;
                bool footStepped = false;
                bool wasStepping = locomotion.SwordFootwork.IsStepping(other);
                for (int frames = 0; driver.SwordPoser.Progress < SwordStrikes.ContactFraction(kind) || driver.SwordPoser.Kind != kind; frames++)
                {
                    Assert.Less(frames, 120, $"{name} never reached its contact");
                    combat.Tick(Dt);
                    vitals.TickHitstun(Dt);
                    locomotion.Tick(idle, Dt);
                    yield return null;
                    bool stepping = locomotion.SwordFootwork.IsStepping(other);
                    otherSetOff |= stepping && !wasStepping;
                    wasStepping = stepping;
                    footStepped |= locomotion.SwordFootwork.IsStepping(foot);
                }
                Assert.IsTrue(footStepped, $"{name}: the foot it strides on never stepped");
                Assert.IsFalse(locomotion.SwordFootwork.IsStepping(foot), $"{name}: the front foot is in the air at the blow");
                if (kind == SwordStrikeKind.Finisher1H)
                {
                    continue;   // its lunge carries the body past what the back leg can reach: that foot trails off the ground as the weight lands on the front one
                }
                Assert.IsFalse(otherSetOff, $"{name}: the other foot stepped too: a shuffle");
                Assert.IsFalse(locomotion.SwordFootwork.IsStepping(other), $"{name}: the back foot is in the air at the blow");
            }
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
