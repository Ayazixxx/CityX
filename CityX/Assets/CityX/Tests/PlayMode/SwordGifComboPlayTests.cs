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
    /// on the real body: no cut of it turns the body round, and the charged attack's blade is driven into the ground with sparks
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
        public IEnumerator NeitherOfTheFirstTwoCutsTurnsTheBodyRoundStandingOrOnTheMove()
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
                // (Owner, 2026-11-14, of a full turn the second cut had: "this gay little twirl ... i never asked for".)
                Assert.Less(Mathf.Abs(turnedInTheSecond), 200.0f, $"{how}: the second cut turned the body round ({turnedInTheSecond:0} degrees)");
                Object.Destroy(_sword.gameObject);
                Object.Destroy(_player);
                _player = null;
                yield return null;
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// Owner, 2026-11-14, twice, with pictures of the first cut's trail: "i dont like this change of direction mid swing it looks bad. fix
        /// the trail and trajectory of the swing", "im still seeing the trails switching direction". Seen from behind, as the game's camera
        /// sees it, the tip of each of the two diagonal cuts sweeps ONE WAY from where the cut sets off to where it ends: the way it goes in
        /// the second half of the sweep is the way it went in the first.
        /// </summary>
        [UnityTest]
        public IEnumerator TheDiagonalCutsSweepTheTipOneWayAsTheCameraSeesIt()
        {
#if UNITY_EDITOR
            _player = Body(new Vector3(0.0f, 0.05f, 0.0f));
            _player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            _sword = Sword();
            _sword.OnEquip(_player, ItemInstanceData.None);
            var combat = _player.GetComponent<SoulsSwordCombat>();
            var poser = _player.GetComponent<SwordPoseDriver>();
            combat.HitStopSeconds = 0.0f;
            var input = new LocomotionInput { Sword1HEquipped = true };
            for (int i = 0; i < 50; i++)
            {
                combat.Tick(Dt);
                locomotion.Tick(input, Dt);
                yield return null;
            }
            var failures = new System.Collections.Generic.List<string>();
            var log = new System.Text.StringBuilder();
            combat.RequestLight(0.0f);
            foreach (SwordStrikeKind kind in new[] { SwordStrikeKind.Slash1H, SwordStrikeKind.Cut1H })
            {
                SwordStrikes.StrikeTiming tm = SwordStrikes.TimingOf(kind);
                float from = (tm.WindUp + tm.Hold) / tm.Total, to = (tm.Contact + tm.Follow) / tm.Total;
                var path = new System.Collections.Generic.List<Vector2>();
                for (int frame = 0; frame < 120 && path.Count < 2 || (poser.IsStriking && poser.Kind == kind && frame < 200); frame++)
                {
                    if (kind == SwordStrikeKind.Slash1H && poser.Kind == SwordStrikeKind.Slash1H && poser.Progress > to)
                    {
                        combat.RequestLight(0.0f);   // the next cut, buffered as this one ends
                    }
                    combat.Tick(Dt);
                    locomotion.Tick(input, Dt);
                    yield return null;
                    if (poser.IsStriking && poser.Kind == kind && poser.Progress >= from && poser.Progress <= to)
                    {
                        Vector3 tip = _player.transform.InverseTransformPoint(_sword.BladeTipWorld);
                        path.Add(new Vector2(tip.x, tip.y));   // as seen from behind: across and up
                    }
                    else if (path.Count > 0 && poser.Kind != kind)
                    {
                        break;
                    }
                }
                log.Append($"{kind}:");
                foreach (Vector2 p in path) log.Append($" ({p.x:0.00},{p.y:0.00})");
                log.AppendLine();
                if (path.Count < 6)
                {
                    failures.Add($"{kind}: the sweep was never seen ({path.Count} frames)");
                    continue;
                }
                Vector2 first = path[path.Count / 2] - path[0], second = path[path.Count - 1] - path[path.Count / 2];
                float turn = Vector2.Angle(first, second);
                log.AppendLine($"{kind}: the tip turns {turn:0} degrees between the halves of its sweep");
                if (turn > MostTipTurnDegrees)
                {
                    failures.Add($"{kind}: the tip's sweep turns {turn:0} degrees half way, seen from behind");
                }
            }
            Debug.Log("CityX the diagonal cuts' tips:\n" + log);
            Assert.IsEmpty(failures, string.Join("; ", failures) + "\n" + log);
#else
            yield break;
#endif
        }

        /// <summary>How far the way a diagonal cut's tip goes may turn between the halves of its sweep, degrees: a cut is an arc, not a line.</summary>
        private const float MostTipTurnDegrees = 35.0f;
    }
}
