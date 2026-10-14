using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// The foot placement on the real rig (owner, 2026-10-13: "walks/runs smoothly on slopes and stairs without sliding feet"): standing across a slope
    /// each sole sits on its own ground and the hips drop to let it; walking up stairs the planted feet sit on the treads and do not slide; on flat
    /// ground nothing changes; a landing bends the knees and the body stands back up.
    /// </summary>
    public sealed class FootIkPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private GameObject _player;
        private const float Dt = 1.0f / 60.0f;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_player != null) Object.Destroy(_player);
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        private GameObject Box(Vector3 centre, Vector3 size, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = centre;
            go.transform.rotation = rotation ?? Quaternion.identity;
            go.transform.localScale = size;
            _made.Add(go);
            return go;
        }

#if UNITY_EDITOR
        private PlayerLocomotion Spawn(Vector3 at)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, at, Quaternion.identity);
            _player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            Time.captureFramerate = 60;
            return _player.GetComponent<PlayerLocomotion>();
        }

        private static float SoleOffset(Transform foot) => BodyRig.FootY * foot.lossyScale.y;

        /// <summary>The surface straight under an ankle (not the body itself).</summary>
        private static bool GroundUnder(Vector3 ankle, Transform self, out RaycastHit best)
        {
            best = default;
            RaycastHit[] hits = Physics.RaycastAll(ankle + Vector3.up * 0.3f, Vector3.down, 2.0f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(self) || h.distance >= nearest) continue;
                nearest = h.distance;
                best = h;
                found = true;
            }
            return found;
        }

        private static Transform Bone(GameObject player, HumanBodyBones bone) => player.GetComponentInChildren<Animator>().GetBoneTransform(bone);

        /// <summary>The lowest point of the foot's sole (toe, heel or middle) in the world: what touches the ground, whichever way the foot is pitched.</summary>
        private static Vector3 LowestFootPoint(Transform foot)
        {
            float scale = foot.lossyScale.y;
            Vector3 lowest = Vector3.positiveInfinity;
            foreach (float along in new[] { FusedBodyShape.FootToeCentreForward + FusedBodyShape.FootToeRadius, 0.06f, FusedBodyShape.FootHeelCentreForward - FusedBodyShape.FootHeelRadius })
            {
                Vector3 corner = foot.position + foot.rotation * (new Vector3(0.0f, -BodyRig.FootY, along) * scale);
                if (corner.y < lowest.y) lowest = corner;
            }
            return lowest;
        }

        /// <summary>How far the foot's lowest point is above the ground under it (negative: buried); NaN with no ground.</summary>
        private static float SoleAboveGround(Transform foot, Transform self)
        {
            Vector3 lowest = LowestFootPoint(foot);
            return GroundUnder(lowest, self, out RaycastHit hit) ? lowest.y - hit.point.y : float.NaN;
        }

        /// <summary>A foot flat enough to be in mid-stance: the walk rolls heel to toe, the run strikes with the toes and never lies quite flat.</summary>
        private static bool FootFlat(Transform foot, bool running) => Vector3.Dot(foot.up, Vector3.up) > (running ? 0.8f : 0.95f);
#endif

        [UnityTest]
        public IEnumerator StandingAcrossASlopeEachSoleSitsOnItsOwnGroundAndTheHipsDrop()
        {
#if UNITY_EDITOR
            // A 25 degree hillside rising toward +x; the body faces +z, so its feet stand side by side ACROSS the slope at two different heights.
            Box(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(30.0f, 1.0f, 30.0f), Quaternion.Euler(0.0f, 0.0f, 25.0f));
            Physics.SyncTransforms();
            PlayerLocomotion locomotion = Spawn(new Vector3(0.0f, 0.75f, 0.0f));
            yield return null;
            for (int i = 0; i < 150; i++)
            {
                locomotion.Tick(default, Dt);
                yield return null;
            }
            Assert.IsTrue(locomotion.FootIkActive, "the feet were not being placed while standing on the ground");
            Assert.Greater(locomotion.FootPlanting.HipDrop, 0.03f, "the hips did not drop for the downhill foot");
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                Transform foot = Bone(_player, bone);
                Assert.IsTrue(GroundUnder(foot.position, _player.transform, out RaycastHit hit), $"no ground under {bone}");
                float above = SoleAboveGround(foot, _player.transform);
                Assert.AreEqual(0.0f, above, 0.03f, $"{bone}'s sole is {above:+0.000;-0.000} m off its ground (floating or buried)");
                Assert.Less(Vector3.Angle(foot.up, hit.normal), 12.0f, $"{bone}'s sole is not laid on the slope");
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator WalkingUpStairsThePlantedFeetSitOnTheTreadsAndDoNotSlide()
        {
#if UNITY_EDITOR
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(100.0f, 1.0f, 100.0f));
            // Twelve steps, 25 cm risers and 40 cm treads, starting a metre ahead.
            for (int i = 0; i < 12; i++)
            {
                Box(new Vector3(0.0f, 0.125f * (i + 1), 1.2f + 0.4f * i), new Vector3(6.0f, 0.25f * (i + 1), 0.4f));
            }
            Physics.SyncTransforms();
            PlayerLocomotion locomotion = Spawn(new Vector3(0.0f, 0.05f, 0.0f));
            yield return null;
            yield return null;

            Transform[] feet = { Bone(_player, HumanBodyBones.LeftFoot), Bone(_player, HumanBodyBones.RightFoot) };
            var lastLocked = new Vector3?[2];
            int lockedFrames = 0;
            float worstSlide = 0.0f;
            float worstOffGround = 0.0f;
            float lowestLift = float.MaxValue, highestLift = float.MinValue;
            int inactiveFrames = 0;
            float lastDrop = 0.0f;
            for (int frame = 0; frame < 170 && _player.transform.position.z < 5.4f; frame++)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.up }, Dt);
                yield return null;
                if (_player.transform.position.z < 1.0f) continue;   // not on the stairs yet
                if (!locomotion.FootIkActive) inactiveFrames++;
                // The hips take a few frames to come down to a lower tread; a planted foot is measured against its tread once they have.
                bool dropSettled = Mathf.Abs(locomotion.FootPlanting.HipDrop - lastDrop) < 0.004f;
                lastDrop = locomotion.FootPlanting.HipDrop;
                for (int i = 0; i < 2; i++)
                {
                    lowestLift = Mathf.Min(lowestLift, locomotion.FootPlanting.LastLift(i));
                    highestLift = Mathf.Max(highestLift, locomotion.FootPlanting.LastLift(i));
                    Vector3 ankle = feet[i].position;
                    // A foot that is HELD must not slide (the walk clip is too slow for the body to be held all the time: see LocksViable).
                    if (locomotion.FootPlanting.IsLocked(i))
                    {
                        if (lastLocked[i].HasValue && dropSettled)
                        {
                            Vector3 slid = ankle - lastLocked[i].Value;
                            slid.y = 0.0f;
                            worstSlide = Mathf.Max(worstSlide, slid.magnitude);
                        }
                        lastLocked[i] = ankle;
                    }
                    else
                    {
                        lastLocked[i] = null;
                    }
                    if (!locomotion.FootPlanting.IsOnFloor(i))
                    {
                        continue;
                    }
                    lockedFrames++;   // on its tread
                    // The tread under the ANKLE (a ray from the toe can start inside the next riser); the height is the sole's lowest point over it.
                    if (dropSettled && GroundUnder(ankle, _player.transform, out RaycastHit tread))
                    {
                        worstOffGround = Mathf.Max(worstOffGround, Mathf.Abs(LowestFootPoint(feet[i]).y - tread.point.y));
                    }
                }
            }
            Assert.Greater(_player.transform.position.z, 2.5f, "the body did not climb the stairs");
            Assert.Greater(lockedFrames, 30, $"the feet were hardly ever on the treads (clip lift {lowestLift:0.000} to {highestLift:0.000} m, the placement off for {inactiveFrames} frames; " +
                $"releases: {locomotion.FootPlanting.ReleasedBySwing} swing, {locomotion.FootPlanting.ReleasedByStretch} stretch, {locomotion.FootPlanting.ReleasedByLift} lift)");
            Assert.Less(worstSlide, 0.015f, $"a planted foot slid {worstSlide * 100:0.0} cm in one frame");
            Assert.Less(worstOffGround, 0.06f, $"a planted foot sat {worstOffGround * 100:0.0} cm off its tread (floating over the step below, or buried)");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator OnFlatGroundTheHipsStayUpAndThePlantedFeetStayOnTheFloor()
        {
#if UNITY_EDITOR
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(100.0f, 1.0f, 100.0f));
            Physics.SyncTransforms();
            PlayerLocomotion locomotion = Spawn(new Vector3(0.0f, 0.05f, 0.0f));
            yield return null;
            yield return null;
            Transform[] feet = { Bone(_player, HumanBodyBones.LeftFoot), Bone(_player, HumanBodyBones.RightFoot) };
            float worstDrop = 0.0f, worstOffGround = 0.0f;
            int plantedFrames = 0;
            // How fast the clip moves a planted foot back through the body against how fast the body goes: a mismatch is what slides feet (the
            // clips are paced to the body when the animator is built: ClipCadence). Walking first, then sprinting.
            var lastAnimated = new Vector3?[2];
            Vector3 lastBody = _player.transform.position;
            var clipSpeedSum = new float[2];
            var bodySpeedSum = new float[2];
            var speedSamples = new int[2];
            for (int frame = 0; frame < 170; frame++)
            {
                bool sprint = frame >= 85;
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = sprint }, Dt);
                yield return null;
                worstDrop = Mathf.Max(worstDrop, locomotion.FootPlanting.HipDrop);
                Vector3 bodyMoved = _player.transform.position - lastBody;
                lastBody = _player.transform.position;
                int gait = sprint ? 1 : 0;
                bool settled = frame > 30 && (!sprint || frame > 125);
                for (int i = 0; i < 2; i++)
                {
                    bool planted = locomotion.FootPlanting.IsOnFloor(i);
                    Vector3 animated = locomotion.FootPlanting.LastAnimated(i);
                    // Mid-stance only (the foot flat on the ground): heel strike and toe-off roll the ankle about the sole at their own speeds.
                    if (planted && FootFlat(feet[i], sprint) && lastAnimated[i].HasValue && settled)
                    {
                        Vector3 clipMoved = animated - lastAnimated[i].Value - bodyMoved;   // the foot through the body's frame
                        clipSpeedSum[gait] += -Vector3.Dot(clipMoved, _player.transform.forward) / Dt;
                        bodySpeedSum[gait] += Vector3.Dot(bodyMoved, _player.transform.forward) / Dt;
                        speedSamples[gait]++;
                    }
                    lastAnimated[i] = animated;
                    if (planted)   // on the clip's floor, held or not: the sole must be on the ground (the "floating" the owner saw)
                    {
                        float above = SoleAboveGround(feet[i], _player.transform);
                        if (float.IsNaN(above)) continue;
                        plantedFrames++;
                        worstOffGround = Mathf.Max(worstOffGround, Mathf.Abs(above));
                    }
                }
            }
            for (int gait = 0; gait < 2; gait++)
            {
                Assert.Greater(speedSamples[gait], 5, gait == 0 ? "no planted walking samples" : "no planted sprinting samples");
                float clipSpeed = clipSpeedSum[gait] / speedSamples[gait];
                float bodySpeed = bodySpeedSum[gait] / speedSamples[gait];
                Debug.Log($"CityX foot placement ({(gait == 0 ? "walk" : "sprint")}): planted feet move back through the body at {clipSpeed:0.00} m/s while the body goes {bodySpeed:0.00} m/s " +
                          $"(releases so far: {locomotion.FootPlanting.ReleasedBySwing} swing, {locomotion.FootPlanting.ReleasedByStretch} stretch, {locomotion.FootPlanting.ReleasedByLift} lift)");
                // The sprint (the run clip within the 20% limits plus the stride) keeps up; the walk clip cannot at 4 m/s (owner's choice: the walk's
                // own look over a jog), but is paced up within the limit, so its feet move faster than the clip alone (1.4 m/s).
                if (gait == 1)
                {
                    Assert.AreEqual(bodySpeed, clipSpeed, bodySpeed * 0.25f,
                        $"sprinting, the clip moves the planted feet at {clipSpeed:0.00} m/s under a body going {bodySpeed:0.00} m/s: the feet slide");
                }
                else
                {
                    Assert.Greater(clipSpeed, 1.6f, $"walking, the walk clip is not paced up (its feet move at {clipSpeed:0.00} m/s)");
                }
            }
            // The controller's capsule rests a skin width (up to 3 cm) above the floor; the placement brings the feet down that much, plus at most the
            // small dip a long stride is allowed at heel strike (FootPlanting.MaxReachDip), and no more.
            Assert.Less(worstDrop, 0.045f + FootPlanting.MaxReachDip, $"the hips dropped {worstDrop * 100:0.0} cm on flat ground");
            Assert.Greater(plantedFrames, 20, "the feet were hardly ever planted on flat ground");
            // A planted foot keeps the clip's own lift over its floor (up to PlantBelow) and the sole's hair of clearance: toe-off starts under the lock.
            Assert.Less(worstOffGround, FootPlanting.PlantBelow + 0.025f, $"a planted foot sat {worstOffGround * 100:0.0} cm off the flat floor");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ALandingBendsTheKneesThenTheBodyStandsBackUp()
        {
#if UNITY_EDITOR
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(100.0f, 1.0f, 100.0f));
            Physics.SyncTransforms();
            PlayerLocomotion locomotion = Spawn(new Vector3(0.0f, 3.0f, 0.0f));
            Transform root = _player.GetComponentInChildren<Animator>().transform;
            yield return null;
            int frame = 0;
            while (!locomotion.IsGrounded && frame++ < 240)
            {
                locomotion.Tick(default, Dt);
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "never landed");
            float deepest = 0.0f;
            for (int i = 0; i < 24; i++)
            {
                locomotion.Tick(default, Dt);
                yield return null;
                deepest = Mathf.Max(deepest, locomotion.FootPlanting.LandingDip);
            }
            Assert.Greater(deepest, 0.04f, "the landing did not bend the knees");
            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(default, Dt);
                yield return null;
            }
            // Back to the few centimetres the capsule always hovers above the floor (its skin width), and no more.
            Assert.Less(locomotion.FootPlanting.TotalDrop, 0.045f, "the body never stood back up after landing");
            Assert.Less(locomotion.FootPlanting.LandingDip, 0.005f, "the landing dip never faded");
            Assert.Greater(root.localPosition.y, -0.05f, "the drawn body is still lowered");
#else
            yield break;
#endif
        }
    }
}
