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
        /// the trail and trajectory of the swing", "im still seeing the trails switching direction"; 2026-11-15, "make it straight across like
        /// the red line". Seen from the game's camera at rest while the trail is drawn (the live window), the tip of each of the two diagonal
        /// cuts sweeps ONE WAY: the way it goes in the second half of the sweep is the way it went in the first.
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
                float from = SwordStrikes.ActiveStart(kind), to = SwordStrikes.ActiveEnd(kind);
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
                        path.Add(AsTheCameraSeesIt(_player.transform.InverseTransformPoint(_sword.BladeTipWorld)));
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

        /// <summary>
        /// Owner, 2026-11-17, with a red line drawn over the first cut from the upper right to the lower left: "make the first swing with the combat
        /// sword more diagonal like the red line, and make the second swing follow through the same distance as the first slash. right now the
        /// second slash stops halfway through". Seen from the game's camera at rest, from the moment the trail starts to the end of the
        /// follow-through: the first cut's tip goes down to the left at about 45 degrees (it went at 60), and the second's goes as far as the
        /// first's (it went about half as far).
        /// </summary>
        [UnityTest]
        public IEnumerator TheFirstCutGoesDownAt45DegreesAndTheSecondFollowsThroughAsFar()
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
            var sweeps = new System.Collections.Generic.Dictionary<SwordStrikeKind, Vector2>();
            combat.RequestLight(0.0f);
            foreach (SwordStrikeKind kind in new[] { SwordStrikeKind.Slash1H, SwordStrikeKind.Cut1H })
            {
                SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(kind);
                float from = SwordStrikes.ActiveStart(kind), to = (timing.Contact + timing.Follow) / timing.Total;
                Vector2? first = null, last = null;
                for (int frame = 0; frame < 200; frame++)
                {
                    if (poser.Kind == SwordStrikeKind.Slash1H && poser.Progress > SwordStrikes.ActiveEnd(SwordStrikeKind.Slash1H))
                    {
                        combat.RequestLight(0.0f);   // the next cut, buffered as this one's blade goes dead
                    }
                    combat.Tick(Dt);
                    locomotion.Tick(input, Dt);
                    yield return null;
                    if (poser.IsStriking && poser.Kind == kind && poser.Progress >= from && poser.Progress <= to)
                    {
                        last = AsTheCameraSeesIt(_player.transform.InverseTransformPoint(_sword.BladeTipWorld));
                        first ??= last;
                    }
                    else if (last.HasValue)
                    {
                        break;
                    }
                }
                Assert.IsTrue(first.HasValue && last.HasValue, $"{kind}: the sweep was never seen");
                sweeps[kind] = last.Value - first.Value;
            }
            Vector2 slash = sweeps[SwordStrikeKind.Slash1H], cut = sweeps[SwordStrikeKind.Cut1H];
            float slashBelowLevel = Mathf.Atan2(-slash.y, Mathf.Abs(slash.x)) * Mathf.Rad2Deg;
            Debug.Log($"CityX first two cuts as the camera sees them: the first {slash.magnitude:0.00} long, {slashBelowLevel:0} degrees below level, "
                      + $"to the {(slash.x < 0.0f ? "left" : "right")}; the second {cut.magnitude:0.00} long, to the {(cut.x < 0.0f ? "left" : "right")}");
            Assert.Less(slash.x, 0.0f, "the first cut does not go to the left");
            Assert.That(slashBelowLevel, Is.InRange(38.0f, 52.0f), "the first cut is not a 45 degree diagonal");
            Assert.Greater(cut.x, 0.0f, "the second cut does not go to the right");
            Assert.Greater(cut.magnitude, slash.magnitude * 0.9f, "the second cut stops short of the first's follow-through");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Owner, 2026-11-14, of the second cut: "make it more human like animation the arm bends strangly". Through each of the first two
        /// cuts, wind-up to follow-through: the hand bends no further from the relaxed grip than a wrist goes (the second cut's had folded
        /// back on itself), the sword-arm's elbow is never raised above the line from its shoulder to its hand (a chicken wing), and the
        /// hand stays a forearm's length off the face.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFirstTwoCutsMoveTheArmAsAnArmMoves()
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
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm), elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand), head = animator.GetBoneTransform(HumanBodyBones.Head);
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
                float to = (tm.Contact + tm.Follow) / tm.Total;
                float worstWrist = 0.0f, worstHeld = 0.0f, worstRise = float.MinValue, nearestFace = float.MaxValue;
                string wristWhen = "", heldWhen = "", riseWhen = "", faceWhen = "";
                bool seen = false;
                for (int frame = 0; frame < 200; frame++)
                {
                    if (kind == SwordStrikeKind.Slash1H && poser.Kind == SwordStrikeKind.Slash1H && poser.Progress > to)
                    {
                        combat.RequestLight(0.0f);
                    }
                    combat.Tick(Dt);
                    locomotion.Tick(input, Dt);
                    yield return null;
                    if (!(poser.IsStriking && poser.Kind == kind))
                    {
                        if (seen) break;
                        continue;
                    }
                    if (poser.Progress > to || poser.ClipWeight < 0.99f)
                    {
                        continue;
                    }
                    seen = true;
                    string when = $"{poser.Progress * tm.Total:0.00} s";
                    Vector3 forearm = hand.position - elbow.position;
                    // How far the hand is bent away from the relaxed grip: the forearm comes into a fist holding a sword with the wrist tipped
                    // toward the little finger (PlayerLocomotion.HeldWristCockDegrees), not straight along the knuckles.
                    float wrist = Vector3.Angle(forearm, hand.rotation * Quaternion.AngleAxis(-PlayerLocomotion.HeldWristCockDegrees, Vector3.up) * Vector3.right);
                    if (wrist > worstWrist) { worstWrist = wrist; wristWhen = when; }
                    if (poser.Progress * tm.Total >= tm.Contact - 0.5f * Dt && wrist > worstHeld) { worstHeld = wrist; heldWhen = when; }
                    Vector3 chord = hand.position - shoulder.position;
                    float t = Mathf.Clamp01(Vector3.Dot(elbow.position - shoulder.position, chord) / chord.sqrMagnitude);
                    float rise = elbow.position.y - (shoulder.position.y + chord.y * t);
                    if (rise > worstRise) { worstRise = rise; riseWhen = when; }
                    float face = Vector3.Distance(hand.position, head.position);
                    if (face < nearestFace) { nearestFace = face; faceWhen = when; }
                }
                float forearmLength = Vector3.Distance(elbow.position, hand.position);
                log.AppendLine($"{kind}: the wrist bent at most {worstWrist:0} degrees ({wristWhen}), from the hit on {worstHeld:0} ({heldWhen}), the elbow at most {worstRise * 100.0f:0} cm above the shoulder-hand line ({riseWhen}), the hand at least {nearestFace * 100.0f:0} cm from the head's middle ({faceWhen}; a forearm is {forearmLength * 100.0f:0} cm)");
                if (!seen) failures.Add($"{kind}: never seen");
                if (worstHeld > MostWristDegrees) failures.Add($"{kind}: from the hit on the wrist bends {worstHeld:0} degrees ({heldWhen})");
                if (worstWrist > MostWhipWristDegrees) failures.Add($"{kind}: the wrist bends {worstWrist:0} degrees ({wristWhen})");
                if (worstRise > MostElbowRise) failures.Add($"{kind}: the elbow is raised {worstRise * 100.0f:0} cm above the line to the hand ({riseWhen})");
                if (nearestFace < forearmLength) failures.Add($"{kind}: the hand comes within {nearestFace * 100.0f:0} cm of the head's middle ({faceWhen})");
            }
            Debug.Log("CityX the first two cuts' arm:\n" + log);
            Assert.IsEmpty(failures, string.Join("; ", failures) + "\n" + log);
#else
            yield break;
#endif
        }

        /// <summary>
        /// The most a swinging hand bends from the relaxed grip, degrees: about what a wrist goes (70 to the little finger's side, 80 down or
        /// up, less two ways at once). (Counted from the knuckles' own line, as this measured at first, the relaxed grip itself reads 35.)
        /// </summary>
        private const float MostWristDegrees = 75.0f;

        /// <summary>
        /// The most it bends anywhere in the swing, the strike's whip included, degrees: for the few frames the sword lags behind a hand
        /// already turned to the cut it runs in line with the forearm (87 in the second cut). A hand folded back on the wrist reads far more
        /// (the second cut's, before 2026-11-14, about 120).
        /// </summary>
        private const float MostWhipWristDegrees = 95.0f;

        /// <summary>How far the sword-arm's elbow may sit above the line from the shoulder to the hand, metres.</summary>
        private const float MostElbowRise = 0.05f;

        /// <summary>How far the way a diagonal cut's tip goes may turn between the halves of its sweep, degrees: a cut is an arc, not a line.</summary>
        private const float MostTipTurnDegrees = 35.0f;

        /// <summary>
        /// A point of the body's frame where the game's camera at rest sees it on its image (ThirdPersonCamera: 0.87 m to the right of the pivot at
        /// 0.73 of the body's 2.6 m and 0.6 m over it, DefaultDistance back, DefaultPitch down).
        /// </summary>
        private static Vector2 AsTheCameraSeesIt(Vector3 point)
        {
            float pitch = ThirdPersonCamera.DefaultPitch * Mathf.Deg2Rad;
            var eye = new Vector3(0.87f, 0.73f * BodyRig.StandingHeight + 0.6f + ThirdPersonCamera.DefaultDistance * Mathf.Sin(pitch),
                -ThirdPersonCamera.DefaultDistance * Mathf.Cos(pitch));
            Vector3 d = point - eye;
            var forward = new Vector3(0.0f, -Mathf.Sin(pitch), Mathf.Cos(pitch));
            var up = new Vector3(0.0f, Mathf.Cos(pitch), Mathf.Sin(pitch));
            float depth = Mathf.Max(Vector3.Dot(d, forward), 0.1f);
            return new Vector2(d.x / depth, Vector3.Dot(d, up) / depth);
        }
    }
}
