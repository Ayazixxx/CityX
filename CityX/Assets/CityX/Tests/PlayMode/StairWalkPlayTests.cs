using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The real body on stairs of the real size (owner, 2026-11-02: "make going up and down stairs look less jank", docs/STAIRS.md): it stays on the ground going down, its body
    /// and the camera climb steadily though the capsule lurches, no foot jumps, and the hips stay up. Off the stairs it is as it was: a jump is a jump, a drop is a fall, the flat is flat.
    /// </summary>
    public sealed class StairWalkPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private const float Tread = 0.36f;
        private const float Riser = 0.227f;
        private const int Steps = 22;
        private const float FirstStepZ = 2.0f;
        private const float SettledAfterMetres = 2.5f;

        private readonly List<GameObject> _made = new List<GameObject>();
        private GameObject _player;
        private PlayerLocomotion _locomotion;

        private static float TopZ => FirstStepZ + Tread * Steps;
        private static float TopY => Riser * Steps;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        private GameObject Box(Vector3 centre, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = centre;
            go.transform.localScale = size;
            _made.Add(go);
            return go;
        }

        private void BuildStairs()
        {
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(300.0f, 1.0f, 300.0f));
            for (int i = 0; i < Steps; i++)
            {
                float height = Riser * (i + 1);
                Box(new Vector3(0.0f, height * 0.5f, FirstStepZ + Tread * (i + 0.5f)), new Vector3(8.0f, height, Tread));
            }
            Box(new Vector3(0.0f, TopY * 0.5f, TopZ + 10.0f), new Vector3(8.0f, TopY, 20.0f));
            Physics.SyncTransforms();
        }

        /// <summary>A body slowed to a stair's own walk and run (by snow, water or a crouch-free slow walk), where the legs step tread by tread and the recorded clip plays.</summary>
        private void SlowToAStairsPace()
        {
            _locomotion.WalkSpeed = StairGait.WalkPace;
            _locomotion.SprintSpeed = StairGait.RunPace;
        }

        private IEnumerator Spawn(Vector3 at, float yaw)
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), at, Quaternion.Euler(0.0f, yaw, 0.0f));
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 20; i++)
            {
                _locomotion.Tick(new LocomotionInput { YawDegrees = yaw }, Step);
                yield return null;
            }
#else
            Assert.Ignore("needs the editor's asset database");
            yield break;
#endif
        }

        private static float SteadyRate(List<float> y) => (y[y.Count - 1] - y[0]) / (y.Count - 1);

        private static float Jolt(List<float> y)
        {
            float rate = SteadyRate(y), worst = 0.0f;
            for (int i = 1; i < y.Count; i++) worst = Mathf.Max(worst, Mathf.Abs(y[i] - y[i - 1] - rate));
            return worst;
        }

        private static float BiggestJump(List<float> y)
        {
            float worst = 0.0f;
            for (int i = 1; i < y.Count; i++) worst = Mathf.Max(worst, Mathf.Abs(y[i] - y[i - 1]));
            return worst;
        }

        /// <summary>Walks or sprints the flight, measuring on the steps away from its ends.</summary>
        [UnityTest]
        public IEnumerator TheBodyTakesAFlightSmoothly([Values(true, false)] bool up, [Values(false, true)] bool sprint)
        {
            BuildStairs();
            float yaw = up ? 0.0f : 180.0f;
            yield return Spawn(up ? new Vector3(0.0f, 0.05f, FirstStepZ - 3.0f) : new Vector3(0.0f, TopY + 0.05f, TopZ + 3.0f), yaw);
            SlowToAStairsPace();
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform[] feet = { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
            var capsule = new List<float>();
            var heads = new List<float>();
            var bodyPivot = new List<float>();
            var left = new List<float>();
            var right = new List<float>();
            Transform[] thighs = { animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightUpperLeg) };
            Transform[] shins = { animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg) };
            int inAir = 0, legsAreSteppers = 0, backward = 0;
            float tightestKnee = 180.0f;
            int stepsAtStart = -1;
            var input = new LocomotionInput { Move = Vector2.up, YawDegrees = yaw, Sprint = sprint };
            for (int frame = 0; frame < 900; frame++)
            {
                _locomotion.Tick(input, Step);
                yield return null;
                float z = _player.transform.position.z;
                if (up ? z > TopZ + 1.0f : z < FirstStepZ - 1.0f) break;
                // Past where the body has slowed to the stair's pace: a sprint arriving is still braking over the first metres, which is not a jolt.
                if (z < FirstStepZ + SettledAfterMetres || z > TopZ - SettledAfterMetres) continue;
                capsule.Add(_player.transform.position.y);
                bodyPivot.Add(_player.transform.position.y + _locomotion.GlideOffset);   // what the camera follows
                heads.Add(head.position.y);
                left.Add(feet[0].position.y);
                right.Add(feet[1].position.y);
                if (!_locomotion.IsGrounded) inAir++;
                if (stepsAtStart < 0) stepsAtStart = _locomotion.StairSteps.StepsTaken;
                if (_locomotion.StairSteps.Weight < 1.0f) continue;
                legsAreSteppers++;
                // The leg as the eye sees it: its knee not folded shut (the curled shin the owner saw), and bending forward.
                for (int f = 0; f < 2; f++)
                {
                    tightestKnee = Mathf.Min(tightestKnee, Vector3.Angle(thighs[f].position - shins[f].position, feet[f].position - shins[f].position));
                    Vector3 middle = 0.5f * (thighs[f].position + feet[f].position);
                    if (Vector3.Dot(shins[f].position - middle, _player.transform.forward) < 0.0f) backward++;
                }
            }
            string which = $"{(sprint ? "sprinting" : "walking")} {(up ? "up" : "down")}";
            Assert.Greater(capsule.Count, 20, $"setup: {which} never crossed the flight");
            Assert.LessOrEqual(inAir, 1, $"{which}, the body was off the ground for {inAir} of {capsule.Count} frames");
            Assert.Greater(Jolt(capsule), 0.03f, "setup: this capsule did not lurch, so nothing here is being smoothed");
            Assert.Less(Jolt(heads), 0.05f, $"{which}, the head jolted");
            Assert.Less(Jolt(bodyPivot), 0.04f, $"{which}, what the camera follows jolted");
            Assert.Less(Mathf.Max(BiggestJump(left), BiggestJump(right)), 0.45f, $"{which}, a foot jumped");
            Assert.Greater(legsAreSteppers, capsule.Count * 0.8f, $"{which}, the legs were not the stair's own steps ({legsAreSteppers} of {capsule.Count} frames)");
            Assert.GreaterOrEqual(_locomotion.StairSteps.StepsTaken - stepsAtStart, 6, $"{which}, it hardly stepped");
            Assert.GreaterOrEqual(tightestKnee, 65.0f, $"{which}, a knee folded shut");
            Assert.AreEqual(0, backward, $"{which}, a knee bent backward");
        }

        /// <summary>Off the top of a flight the clip has the legs back.</summary>
        [UnityTest]
        public IEnumerator AtTheTopTheLegsGoBackToTheClip()
        {
            BuildStairs();
            yield return Spawn(new Vector3(0.0f, 0.05f, FirstStepZ - 3.0f), 0.0f);
            SlowToAStairsPace();
            var input = new LocomotionInput { Move = Vector2.up, YawDegrees = 0.0f };
            bool had = false;
            while (_player.transform.position.z < TopZ + 3.0f)
            {
                _locomotion.Tick(input, Step);
                had |= _locomotion.StairSteps.Weight >= 1.0f;
                yield return null;
            }
            Assert.IsTrue(had, "the stair's steps never had the legs");
            Assert.AreEqual(0.0f, _locomotion.StairSteps.Weight, "the clip did not get the legs back on the landing");
        }

        [UnityTest]
        public IEnumerator ItCanJumpHalfwayDownAFlight()
        {
            BuildStairs();
            yield return Spawn(new Vector3(0.0f, TopY + 0.05f, TopZ + 3.0f), 180.0f);
            var down = new LocomotionInput { Move = Vector2.up, YawDegrees = 180.0f };
            while (_player.transform.position.z > FirstStepZ + Tread * Steps * 0.5f)
            {
                _locomotion.Tick(down, Step);
                yield return null;
            }
            float before = _player.transform.position.y;
            down.Jump = true;
            _locomotion.Tick(down, Step);
            yield return null;
            down.Jump = false;
            float highest = before;
            for (int i = 0; i < 20; i++)
            {
                _locomotion.Tick(down, Step);
                highest = Mathf.Max(highest, _player.transform.position.y);
                yield return null;
            }
            Assert.Greater(highest - before, 0.2f, "the jump did not leave the stairs");
        }

        [UnityTest]
        public IEnumerator ALowLedgeIsSteppedOffAndAHighOneIsAFall()
        {
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(300.0f, 1.0f, 300.0f));
            Box(new Vector3(-20.0f, 0.15f, 10.0f), new Vector3(8.0f, 0.3f, 20.0f));   // 30 cm: a kerb
            Box(new Vector3(20.0f, 1.0f, 10.0f), new Vector3(8.0f, 2.0f, 20.0f));   // 2 m
            Physics.SyncTransforms();

            yield return Spawn(new Vector3(-20.0f, 0.35f, 17.0f), 0.0f);
            var on = new LocomotionInput { Move = Vector2.up, YawDegrees = 0.0f };
            int off = 0;
            for (int i = 0; i < 120 && _player.transform.position.z < 24.0f; i++)
            {
                _locomotion.Tick(on, Step);
                if (!_locomotion.IsGrounded) off++;
                yield return null;
            }
            Assert.AreEqual(0, off, "stepping off a kerb left the ground");
            Object.Destroy(_player);

            yield return Spawn(new Vector3(20.0f, 2.05f, 17.0f), 0.0f);
            bool fell = false;
            for (int i = 0; i < 120 && _player.transform.position.z < 24.0f; i++)
            {
                _locomotion.Tick(on, Step);
                fell |= !_locomotion.IsGrounded;
                yield return null;
            }
            Assert.IsTrue(fell, "a body was held to the ground over a 2 m drop");
        }

        [UnityTest]
        public IEnumerator OnTheFlatNothingGlidesAndTheTorsoIsUpright()
        {
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(300.0f, 1.0f, 300.0f));
            Physics.SyncTransforms();
            yield return Spawn(new Vector3(0.0f, 0.05f, 0.0f), 0.0f);
            var input = new LocomotionInput { Move = Vector2.up, YawDegrees = 0.0f, Sprint = true };
            for (int i = 0; i < 90; i++)
            {
                _locomotion.Tick(input, Step);
                yield return null;
                Assert.AreEqual(0.0f, _locomotion.GlideOffset, 1e-4f);
                Assert.AreEqual(0.0f, _locomotion.GroundSlope, 1e-4f);
            }
        }

        /// <summary>Nothing is sent for any of this: a copy, moved by the network and never Ticked, reads the same stairs under it.</summary>
        [UnityTest]
        public IEnumerator AnotherPlayersCopyGlidesToo()
        {
            BuildStairs();
            yield return Spawn(new Vector3(0.0f, 0.05f, FirstStepZ + 2.0f), 0.0f);
            Animator animator = _player.GetComponentInChildren<Animator>();
            float z = FirstStepZ + 2.0f;
            for (int i = 0; i < 40; i++)
            {
                z += StairGait.WalkPace * Step;
                animator.SetBool("Grounded", true);
                float capsule = Riser * (Mathf.Floor((z - FirstStepZ) / Tread) + 1.0f) + 0.08f;
                _player.transform.position = new Vector3(0.0f, capsule, z);
                yield return null;
            }
            Assert.Greater(_locomotion.GroundSlope, 0.4f, "the copy did not read the flight");
            Assert.AreNotEqual(0.0f, _locomotion.GlideOffset, "the copy did not glide");
            Assert.Greater(_locomotion.StairClipShare, 0.0f, "the copy did not play the stair clip");
        }

        /// <summary>
        /// A stair does not slow a body (owner, 2026-11-05: "the stairs feel horrendous now why is it so slow make it normal speed"): it keeps its own walk and sprint, and at
        /// those the legs are the walk and run clips', not stepped tread by tread.
        /// </summary>
        [UnityTest]
        public IEnumerator ABodyKeepsItsOwnPaceOnAStair([Values(false, true)] bool sprint, [Values(true, false)] bool up)
        {
            BuildStairs();
            float yaw = up ? 0.0f : 180.0f;
            yield return Spawn(up ? new Vector3(0.0f, 0.05f, FirstStepZ - 3.0f) : new Vector3(0.0f, TopY + 0.05f, TopZ + 3.0f), yaw);
            var input = new LocomotionInput { Move = Vector2.up, YawDegrees = yaw, Sprint = sprint };
            float middle = FirstStepZ + Tread * Steps * 0.5f;
            while (up ? _player.transform.position.z < middle - 1.0f : _player.transform.position.z > middle + 1.0f)
            {
                _locomotion.Tick(input, Step);
                yield return null;
            }
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform[] thighs = { animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightUpperLeg) };
            Transform[] shins = { animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg) };
            Transform[] feet = { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
            float tightestKnee = 180.0f;
            int held = 0;
            float from = _player.transform.position.z;
            for (int i = 0; i < 60; i++)
            {
                _locomotion.Tick(input, Step);
                yield return null;
                for (int f = 0; f < 2; f++)
                {
                    tightestKnee = Mathf.Min(tightestKnee, Vector3.Angle(thighs[f].position - shins[f].position, feet[f].position - shins[f].position));
                    held += _locomotion.FootPlanting.IsLocked(f) && Mathf.Abs(_locomotion.GroundSlope) > 0.3f ? 1 : 0;
                }
            }
            float pace = Mathf.Abs(_player.transform.position.z - from);
            Assert.AreEqual(0, held, "at speed a foot was held on a tread");
            // The run's own stride bends a knee to about 60 degrees; a foot reaching for a tread far below folded it shut behind the body.
            Assert.GreaterOrEqual(tightestKnee, 45.0f, "a knee folded shut taking the stair at speed");
            Assert.AreEqual(sprint ? _locomotion.SprintSpeed : _locomotion.WalkSpeed, pace, 0.4f, "the stair slowed the body");
            Assert.AreEqual(0.0f, _locomotion.StairSteps.Weight, "at a walk's or a sprint's pace the legs were still stepped tread by tread");

            while (up ? _player.transform.position.z < TopZ + 1.5f : _player.transform.position.z > FirstStepZ - 1.5f)
            {
                _locomotion.Tick(input, Step);
                yield return null;
            }
            from = _player.transform.position.z;
            for (int i = 0; i < 30; i++)
            {
                _locomotion.Tick(input, Step);
                yield return null;
            }
            float flat = Mathf.Abs(_player.transform.position.z - from) * 2.0f;
            Assert.Greater(flat, sprint ? 6.0f : 3.2f, "the body stayed slow off the stair");
        }

        /// <summary>The recorded clip plays walking a flight and not standing on it or off it; the arms are left to a weapon's carry.</summary>
        [UnityTest]
        public IEnumerator TheRecordedClipPlaysOnAFlightOnly()
        {
            BuildStairs();
            yield return Spawn(new Vector3(0.0f, 0.05f, FirstStepZ - 3.0f), 0.0f);
            SlowToAStairsPace();
            Animator animator = _player.GetComponentInChildren<Animator>();
            int body = animator.GetLayerIndex(PlayerLocomotion.StairBodyLayerName);
            int arms = animator.GetLayerIndex(PlayerLocomotion.StairArmsLayerName);
            Assert.GreaterOrEqual(body, 0, "no stair layer: is the prefab rebuilt?");
            Assert.GreaterOrEqual(arms, 0);
            Assert.AreEqual(0.0f, animator.GetLayerWeight(body), "the stair clip played on the flat");

            var input = new LocomotionInput { Move = Vector2.up, YawDegrees = 0.0f };
            while (_player.transform.position.z < FirstStepZ + 3.0f)
            {
                _locomotion.Tick(input, Step);
                yield return null;
            }
            for (int i = 0; i < 20; i++)
            {
                _locomotion.Tick(input, Step);
                yield return null;
            }
            Assert.AreEqual(1.0f, animator.GetLayerWeight(body), 0.01f, "the body is not the clip's on the flight");
            Assert.AreEqual(1.0f, animator.GetLayerWeight(arms), 0.01f, "the free arms are not the clip's on the flight");
            Assert.AreEqual(1.0f, animator.GetFloat(PlayerLocomotion.StairFacingName), "going up, not the up clip");
            float phase = animator.GetFloat(PlayerLocomotion.StairPhaseName);
            _locomotion.Tick(input, Step);
            yield return null;
            Assert.AreNotEqual(phase, animator.GetFloat(PlayerLocomotion.StairPhaseName), "the clip is not moving with the feet");

            input.SwordEquipped = true;
            for (int i = 0; i < 10; i++)
            {
                _locomotion.Tick(input, Step);
                yield return null;
            }
            Assert.AreEqual(0.0f, animator.GetLayerWeight(arms), "the clip took the arms from a sword's carry");
            input.SwordEquipped = false;

            for (int i = 0; i < 30; i++)
            {
                _locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }
            Assert.AreEqual(0.0f, animator.GetLayerWeight(body), "standing on the flight the body stayed the clip's");
        }
    }
}
