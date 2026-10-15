using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// /pushups on the real rig with the real keys (owner, 2026-10-14): down, push-ups on repeat with 1 XP each, mouse look free, a movement
    /// key gets it up, and the body only walks once standing.
    /// </summary>
    public sealed class PushUpPlayTests : InputTestFixture
    {
        private GameObject _ground;
        private GameObject _player;
        private Keyboard _keyboard;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            base.TearDown();
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Seconds(float seconds) => Frames(Mathf.CeilToInt(seconds * 60.0f));

        private static bool In(Animator animator, string state) => animator.GetCurrentAnimatorStateInfo(0).IsName(state);

        [UnityTest]
        public IEnumerator PushUpsRepForXpUntilAKeyAndTheBodyWalksOnlyOnceStanding()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var reader = _player.GetComponent<PlayerInputReader>();
            var vitals = _player.GetComponent<PlayerVitals>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            yield return Frames(30);

            PushUpEmote emote = reader.PushUps;
            Assert.Greater(emote.EnterSeconds, 0.5f, "the clip lengths were not written into the reader (rebuild the player rig)");
            reader.StartPushUps();
            Assert.IsTrue(reader.IsDoingPushUps);
            yield return Frames(20);
            Assert.IsTrue(In(animator, "PushUpEnter"), "the body did not start going down");

            yield return Seconds(emote.EnterSeconds);
            Assert.AreEqual(PushUpEmote.Phase.PushingUp, emote.Current);
            yield return Frames(15);
            Assert.IsTrue(In(animator, "PushUpLoop"), "no push-ups on repeat");

            // Two push-ups, one XP each. The mouse turns the camera meanwhile without stopping them.
            vitals.Experience = 0;   // a few push-ups are far from a level, so the count is all here
            int repsBefore = emote.Reps;
            Move(_mouse.delta, new Vector2(40.0f, 0.0f));
            yield return Seconds(emote.RepSeconds * 2.0f + 0.1f);
            Move(_mouse.delta, Vector2.zero);
            Assert.AreEqual(PushUpEmote.Phase.PushingUp, emote.Current, "moving the mouse stopped the push-ups");
            Assert.GreaterOrEqual(emote.Reps - repsBefore, 2, "two push-ups in two reps' time");
            int reps = emote.Reps - repsBefore;
            Assert.AreEqual(reps * PushUpEmote.XpPerRep, vitals.Experience, "a push-up should be worth one XP");

            // W calls it off: up, and only walking once standing.
            Vector3 down = _player.transform.position;
            Press(_keyboard.wKey);
            yield return Frames(15);
            Assert.AreEqual(PushUpEmote.Phase.GettingUp, emote.Current, "W did not call it off");
            Assert.IsTrue(In(animator, "PushUpExit"), "the body did not start getting up");
            yield return Seconds(emote.ExitSeconds * 0.5f);
            Assert.Less(Vector2.Distance(new Vector2(down.x, down.z), new Vector2(_player.transform.position.x, _player.transform.position.z)), 0.05f,
                "the body walked off while still getting up");
            yield return Seconds(emote.ExitSeconds * 0.5f + 0.2f);
            Assert.IsFalse(reader.IsDoingPushUps, "never stood up");
            yield return Seconds(1.0f);
            Release(_keyboard.wKey);
            Assert.Greater(Vector2.Distance(new Vector2(down.x, down.z), new Vector2(_player.transform.position.x, _player.transform.position.z)), 1.0f,
                "standing again, the body should walk");
#else
            yield break;
#endif
        }

        /// <summary>
        /// The body really does push-ups on this rig: the palms stay on the floor (not half a metre through it, as the Mixamo clips left them on
        /// this long-armed body) and the shoulders rise high at the top of each push-up (PlayerLocomotion.ApplyPushUpGrounding).
        /// </summary>
        [UnityTest]
        public IEnumerator ThePalmsStayOnTheFloorAndTheBodyRisesOnEachPushUp()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var reader = _player.GetComponent<PlayerInputReader>();
            reader.enabled = false;
            var body = _player.GetComponent<PlayerLocomotion>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform shoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            PushUpEmote emote = reader.PushUps;
            for (int i = 0; i < 30; i++)
            {
                body.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            float floor = 0.0f;
            for (float t = 0.0f; t < emote.EnterSeconds; t += 1.0f / 60.0f)
            {
                body.Tick(new LocomotionInput { PushUpPhase = 1 }, 1.0f / 60.0f);
                yield return null;
            }
            float lowestWrist = float.MaxValue, highestShoulder = float.MinValue, lowestShoulder = float.MaxValue;
            for (float t = 0.0f; t < emote.RepSeconds * 2.0f; t += 1.0f / 60.0f)
            {
                body.Tick(new LocomotionInput { PushUpPhase = 2 }, 1.0f / 60.0f);
                yield return null;
                lowestWrist = Mathf.Min(lowestWrist, Mathf.Min(leftHand.position.y, rightHand.position.y));
                highestShoulder = Mathf.Max(highestShoulder, shoulder.position.y);
                lowestShoulder = Mathf.Min(lowestShoulder, shoulder.position.y);
            }
            Assert.Greater(lowestWrist, floor + 0.02f, $"a hand went through the floor ({lowestWrist:0.00} m)");
            Assert.Greater(highestShoulder, floor + 0.65f, $"the body never pushed up: the shoulders reached only {highestShoulder:0.00} m");
            Assert.Greater(highestShoulder - lowestShoulder, 0.4f, "the shoulders hardly rise and fall: no push-up");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AClickOrASpellKeyCallsItOffToo()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var reader = _player.GetComponent<PlayerInputReader>();
            yield return Frames(30);

            foreach (ButtonControl key in new ButtonControl[] { _mouse.leftButton, _keyboard.digit3Key })
            {
                reader.StartPushUps();
                yield return Seconds(reader.PushUps.EnterSeconds + 0.2f);
                Assert.AreEqual(PushUpEmote.Phase.PushingUp, reader.PushUps.Current);
                Press(key);
                yield return Frames(2);
                Release(key);
                Assert.AreEqual(PushUpEmote.Phase.GettingUp, reader.PushUps.Current, $"{key.name} did not call it off");
                yield return Seconds(reader.PushUps.ExitSeconds + 0.2f);
                Assert.IsFalse(reader.IsDoingPushUps);
            }
#else
            yield break;
#endif
        }
    }
}
