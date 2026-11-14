using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The one-handed sword's combo from the owner's reference (2026-11-13: "make the animation exactly like this gif"; docs/SWORD_GIF_COMBO.md),
    /// on the real body: the second cut's full turn of the whole body, and the charged attack's blade driven into the ground with sparks
    /// thrown from where it struck, both on the player's own body and on another player's copy of it (which is only told the attack's name).
    /// </summary>
    public sealed class SwordGifComboPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        private GameObject _ground;
        private GameObject _player;
        private GameObject _copy;
        private SwordWeapon _sword;
        private SwordWeapon _copySword;

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (Object made in new Object[] { _sword != null ? _sword.gameObject : null, _copySword != null ? _copySword.gameObject : null, _player, _copy, _ground })
            {
                if (made != null) Object.Destroy(made);
            }
        }

#if UNITY_EDITOR
        private static GameObject Body(Vector3 at)
        {
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = false;
            return body;
        }

        private static SwordWeapon Sword() => Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab"),
            new Vector3(40.0f, 1.0f, 40.0f), Quaternion.identity).GetComponent<SwordWeapon>();

        /// <summary>How far round to the right the line of the shoulders is turned from the way the body faces, degrees (-180 to 180).</summary>
        private static float ShouldersTurned(GameObject body)
        {
            Animator animator = body.GetComponentInChildren<Animator>();
            Vector3 across = body.transform.InverseTransformDirection(
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position - animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position);
            return Mathf.Atan2(-across.z, across.x) * Mathf.Rad2Deg;
        }
#endif

        [UnityTest]
        public IEnumerator TheChargedAttackDrivesTheBladeIntoTheGroundAndSparksFlyOnTheBodyAndOnACopyOfIt()
        {
#if UNITY_EDITOR
            _player = Body(new Vector3(0.0f, 0.05f, 0.0f));
            _player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            _sword = Sword();
            _sword.OnEquip(_player, ItemInstanceData.None);
            var combat = _player.GetComponent<SoulsSwordCombat>();
            combat.HitStopSeconds = 0.0f;
            var input = new LocomotionInput { Sword1HEquipped = true };
            for (int i = 0; i < 40; i++)
            {
                combat.Tick(Dt);
                locomotion.Tick(input, Dt);
                yield return null;
            }

            int before = BlockDeflectEffect.GroundStrikesShown;
            combat.RequestHeavy(0.0f);
            float lowestTip = float.MaxValue;
            for (float t = 0.0f; t < SwordStrikes.Seconds(SwordStrikeKind.Overhead1H) + 0.2f; t += Dt)
            {
                combat.Tick(Dt);
                locomotion.Tick(input, Dt);
                yield return null;
                lowestTip = Mathf.Min(lowestTip, _sword.BladeTipWorld.y);   // (the ground's top is at 0)
            }
            Assert.Less(lowestTip, 0.15f, "the blade never got down to the ground");
            Assert.Greater(lowestTip, 0.0f, "the blade went through the ground");
            Assert.AreEqual(before + 1, BlockDeflectEffect.GroundStrikesShown, "the blade struck the ground and threw no sparks (or threw them twice)");
            Vector3 struck = _player.transform.InverseTransformPoint(BlockDeflectEffect.LastGroundStrike);
            Assert.Greater(struck.z, 1.0f, "the sparks are not out in front, where the blade struck");
            Assert.Less(Mathf.Abs(struck.y), 0.2f, "the sparks are not at the ground");

            // A light attack strikes no ground.
            combat.RequestLight(0.0f);
            for (float t = 0.0f; t < SwordStrikes.Seconds(SwordStrikeKind.Slash1H) + 0.2f; t += Dt)
            {
                combat.Tick(Dt);
                locomotion.Tick(input, Dt);
                yield return null;
            }
            Assert.AreEqual(before + 1, BlockDeflectEffect.GroundStrikesShown, "a light attack threw sparks from the ground");

            // ANOTHER PLAYER'S COPY is only told the attack's name: the same sparks fly from where its own blade strikes.
            _copy = Body(new Vector3(12.0f, 0.05f, 0.0f));
            _copySword = Sword();
            _copySword.MountCosmetic(_copy);
            Animator copyAnimator = _copy.GetComponentInChildren<Animator>();
            for (int i = 0; i < 40; i++)
            {
                copyAnimator.SetBool("Grounded", true);   // what NetworkedAnimatorSync hands a copy every tick
                yield return null;
            }
            _copy.GetComponent<PlayerAnimationDriver>().PlayRemoteState(SwordStrikes.Overhead1HName);
            for (float t = 0.0f; t < SwordStrikes.Seconds(SwordStrikeKind.Overhead1H) + 0.2f; t += Dt)
            {
                copyAnimator.SetBool("Grounded", true);
                yield return null;
            }
            Assert.AreEqual(before + 2, BlockDeflectEffect.GroundStrikesShown, "another player's copy threw no sparks from the ground");
            Assert.Less(Vector3.Distance(BlockDeflectEffect.LastGroundStrike, _copy.transform.position), 3.0f, "the copy's sparks are not at the copy");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheSecondCutTurnsTheWholeBodyRoundOnceStandingAndOnTheMoveAndTheOthersDoNot()
        {
#if UNITY_EDITOR
            foreach (bool walking in new[] { false, true })
            {
                _player = Body(new Vector3(0.0f, 0.05f, 0.0f));
                _player.GetComponent<PlayerVitals>().InfiniteStamina = true;
                var locomotion = _player.GetComponent<PlayerLocomotion>();
                _sword = Sword();
                _sword.OnEquip(_player, ItemInstanceData.None);
                var combat = _player.GetComponent<SoulsSwordCombat>();
                var poser = _player.GetComponent<SwordPoseDriver>();
                combat.HitStopSeconds = 0.0f;
                var input = new LocomotionInput { Sword1HEquipped = true, Move = walking ? new Vector2(0.0f, 1.0f) : Vector2.zero };
                for (int i = 0; i < 50; i++)
                {
                    combat.Tick(Dt);
                    locomotion.Tick(input, Dt);
                    yield return null;
                }
                string how = walking ? "walking" : "standing";
                float turnedInTheFirst = 0.0f, turnedInTheSecond = 0.0f, last = ShouldersTurned(_player);
                bool second = false;
                combat.RequestLight(0.0f);
                for (float t = 0.0f; t < 2.0f; t += Dt)
                {
                    if (!second && poser.Kind == SwordStrikeKind.Slash1H && combat.Phase == SoulsPhase.Recovery)
                    {
                        combat.RequestLight(0.0f);
                    }
                    combat.Tick(Dt);
                    locomotion.Tick(input, Dt);
                    yield return null;
                    second |= poser.IsStriking && poser.Kind == SwordStrikeKind.Cut1H;
                    float now = ShouldersTurned(_player);
                    float step = Mathf.DeltaAngle(last, now);
                    last = now;
                    if (second)
                    {
                        turnedInTheSecond += step;
                    }
                    else
                    {
                        turnedInTheFirst += step;
                    }
                    if (second && !poser.IsStriking)
                    {
                        break;
                    }
                }
                Assert.IsTrue(second, $"{how}: the chain never reached its second cut");
                Assert.Less(Mathf.Abs(turnedInTheFirst), 200.0f, $"{how}: the first cut turned the body round ({turnedInTheFirst:0} degrees)");
                // (Its own coil against the cut and the turn through it come and go on top of the full turn.)
                Assert.AreEqual(360.0f, turnedInTheSecond, 150.0f, $"{how}: the second cut turned the shoulders {turnedInTheSecond:0} degrees round, not once round to the right");
                Object.Destroy(_sword.gameObject);
                Object.Destroy(_player);
                _player = null;
                yield return null;
            }
#else
            yield break;
#endif
        }
    }
}
