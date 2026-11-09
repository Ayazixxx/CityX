using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// Ladder climbing on the real body (owner, 2026-11-09: "make ladder climbing so make that look good with hand ik"): a ladder up a wall
    /// 8 m to its top, got on at the foot and from the top, climbed, slid, hopped off, climbed out of; every hand and foot on a rung; and
    /// another player's copy on the same rungs.
    /// </summary>
    public sealed class LadderPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private const float WallFace = 1.0f;
        private const float Top = 8.0f;

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            Ladders.Clear();
        }

        private static Ladder TheLadder(bool open = true) => new Ladder
        {
            Foot = new Vector3(0.0f, 0.0f, WallFace - Ladders.OffWall),
            Normal = Vector3.back,
            Height = Top,
            TopOpen = open,
        };

        /// <summary>A floor, a wall 8 m high and 4 m thick whose top is the ladder's, and the ladder against it.</summary>
        private Ladder Stage(bool open = true)
        {
            Time.captureFramerate = 60;   // the limbs' blend runs in frame time
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(60, 1, 60);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, Top * 0.5f, WallFace + 2.0f);
            wall.transform.localScale = new Vector3(12, Top, 4);
            _made.Add(ground);
            _made.Add(wall);
            Physics.SyncTransforms();
            Ladder ladder = TheLadder(open);
            Ladders.Place(ladder);
            return ladder;
        }

        private PlayerLadder Player(Vector3 feet, Quaternion facing)
        {
#if UNITY_EDITOR
            GameObject body = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), feet, facing);
            _made.Add(body);
            PlayerLadder ladder = body.GetComponent<PlayerLadder>();
            if (ladder == null) ladder = body.AddComponent<PlayerLadder>();
            return ladder;
#else
            return null;
#endif
        }

        private static IEnumerator Hold(PlayerLadder body, float seconds, bool up = false, bool down = false, bool fast = false)
        {
            for (float t = 0.0f; t < seconds && body.IsOn; t += Dt)
            {
                body.Tick(up, down, fast, false, false, Dt);
                yield return null;
            }
        }

        /// <summary>How far a point is from the nearest rung's middle line, up and off the ladder (not along the rung).</summary>
        private static float OffItsRung(Ladder ladder, Vector3 point, float lower = 0.0f)
        {
            float rung = Mathf.Round((point.y - lower - ladder.Foot.y - Ladders.FirstRung) / Ladders.Pitch);
            Vector3 middle = ladder.Rung(Mathf.Clamp(rung, 0, ladder.RungCount - 1));
            return new Vector2(point.y - lower - middle.y, point.z - middle.z).magnitude;
        }

        [UnityTest]
        public IEnumerator WalkingIntoALadderGetsOnItAndWClimbsWithEveryHandAndFootOnARung()
        {
#if UNITY_EDITOR
            Ladder ladder = Stage();
            PlayerLadder body = Player(new Vector3(0.1f, 0.05f, 0.0f), Quaternion.identity);
            Animator animator = body.GetComponentInChildren<Animator>();
            yield return null;
            yield return null;
            Assert.IsFalse(body.TryWalkOn(true, Vector3.right), "walking along the wall got on the ladder");
            Assert.IsFalse(body.TryWalkOn(false, Vector3.forward), "standing by the ladder got on it");
            Assert.IsTrue(body.TryWalkOn(true, Vector3.forward), "walking into the ladder did not get on it");
            yield return Hold(body, 0.4f);
            Assert.IsFalse(body.IsGettingOn);
            float from = body.transform.position.y;
            yield return Hold(body, 2.0f, up: true);
            float climbed = body.transform.position.y - from;
            Assert.AreEqual(PlayerLadder.ClimbSpeed * 2.0f, climbed, 0.25f, "two seconds of W did not climb as fast as a ladder is climbed");
            Assert.AreEqual(ladder.Foot.z - PlayerLadder.Standoff, body.transform.position.z, 0.01f, "the body is not hanging off the rungs");
            Assert.Less(Vector3.Angle(body.transform.forward, Vector3.forward), 2.0f, "the body is not facing the ladder");

            // Let go of the keys: the step is finished, and every hand and foot is on its rung.
            yield return Hold(body, 1.0f);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand), rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot), rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            float grip = PlayerLocomotion.LadderGripAlong + PlayerLocomotion.LadderGripDeep + 0.03f;
            Assert.Less(OffItsRung(ladder, leftHand.position), grip, "the left hand is not on a rung");
            Assert.Less(OffItsRung(ladder, rightHand.position), grip, "the right hand is not on a rung");
            float ankle = BodyRig.FootY + PlayerLocomotion.LadderRungHalf;
            Assert.Less(OffItsRung(ladder, leftFoot.position, ankle), PlayerLocomotion.LadderAnkleBack + 0.04f, "the left foot is not on a rung");
            Assert.Less(OffItsRung(ladder, rightFoot.position, ankle), PlayerLocomotion.LadderAnkleBack + 0.04f, "the right foot is not on a rung");
            Assert.Less(leftHand.position.x, 0.0f, "the hands are crossed");
            Assert.Greater(rightHand.position.x, 0.0f, "the hands are crossed");
            Assert.Greater(Mathf.Abs(leftFoot.position.y - rightFoot.position.y), Ladders.Pitch * 0.8f, "both feet are on one rung");
            foreach (HumanBodyBones joint in new[] { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
            {
                Assert.Less(animator.GetBoneTransform(joint).position.z, WallFace, $"{joint} is in the wall behind the ladder");
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ClimbingOutOfAnOpenTopStandsOnItAndAShutOneStopsTheClimbUnderItsLid()
        {
#if UNITY_EDITOR
            Ladder ladder = Stage();
            PlayerLadder body = Player(new Vector3(0.0f, 0.05f, 0.1f), Quaternion.identity);
            PlayerClimbing climbing = body.GetComponent<PlayerClimbing>();
            yield return null;
            body.GetOn(ladder, false);
            yield return Hold(body, 12.0f, up: true);
            Assert.IsFalse(body.IsOn, "twelve seconds of W did not reach the top");
            Assert.IsTrue(climbing.IsToppingOut, "the top of the ladder did not start the pull-up");
            for (int i = 0; i < 200 && climbing.IsToppingOut; i++)
            {
                climbing.TickMantle(Dt);
                yield return null;
            }
            Assert.Less(Vector3.Distance(ladder.StandAtTop, body.transform.position), 0.3f, "the pull-up did not end standing at the top");
            Assert.IsTrue(body.GetComponent<CharacterController>().enabled, "the body was not given back to its controller");

            // The same ladder under a shut lid: the head stops under it, and the body stays on the rungs.
            Ladders.Clear();
            Ladder shut = TheLadder(false);
            PlayerLadder other = Player(new Vector3(3.0f, 0.05f, 0.1f), Quaternion.identity);
            shut.Foot += Vector3.right * 3.0f;
            yield return null;
            other.GetOn(shut, false);
            yield return Hold(other, 12.0f, up: true);
            Assert.IsTrue(other.IsOn, "the body left a ladder whose top is shut");
            Assert.AreEqual(Top - BodyRig.StandingHeight - PlayerLadder.UnderLid, other.transform.position.y, 0.02f, "the head is not just under the lid");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator FromTheTopTheBodyBacksDownOntoTheRungsAndSComesDownToTheFloor()
        {
#if UNITY_EDITOR
            Ladder ladder = Stage();
            PlayerLadder body = Player(ladder.StandAtTop + new Vector3(0.4f, 0.02f, 0.5f), Quaternion.Euler(0.0f, 140.0f, 0.0f));
            yield return null;
            yield return null;
            Assert.IsTrue(body.Nearby(out Ladder found, out bool fromTop), "standing at the top, the ladder is not found");
            Assert.IsTrue(fromTop, "from the top of a ladder it is offered as from its foot");
            body.GetOn(found, true);
            yield return Hold(body, PlayerLadder.TopGetOnSeconds + 0.2f);
            Assert.IsTrue(body.IsOn && !body.IsGettingOn, "backing down from the top did not end on the ladder");
            Assert.AreEqual(Top - PlayerLadder.TopHang, body.transform.position.y, 0.25f, "the body is not hanging under the top");
            Assert.Less(Vector3.Angle(body.transform.forward, Vector3.forward), 2.0f, "the body did not turn to face the ladder");

            // Sliding is much faster than climbing down; then the last of it on the rungs, and off onto the floor.
            float before = body.transform.position.y;
            yield return Hold(body, 0.5f, down: true, fast: true);
            Assert.IsTrue(body.IsSliding);
            Assert.AreEqual(PlayerLadder.SlideSpeed * 0.5f, before - body.transform.position.y, 0.4f, "the slide is not at its speed");
            yield return Hold(body, 6.0f, down: true);
            Assert.IsFalse(body.IsOn, "S to the foot of the ladder did not step off it");
            Assert.IsTrue(body.GetComponent<CharacterController>().enabled);
            Assert.Less(body.transform.position.y, 0.2f, "the body is not on the floor");
            Assert.IsFalse(body.TryWalkOn(true, Vector3.forward), "W still held from before the foot climbed straight back on");
            Assert.IsFalse(body.TryWalkOn(false, Vector3.forward));
            Assert.IsTrue(body.TryWalkOn(true, Vector3.forward), "letting go of W and pressing it again did not get back on");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator SpaceHopsOffBackwardsAndControlDropsStraightDown()
        {
#if UNITY_EDITOR
            Ladder ladder = Stage();
            PlayerLadder body = Player(new Vector3(0.0f, 0.05f, 0.1f), Quaternion.identity);
            yield return null;
            body.GetOn(ladder, false);
            yield return Hold(body, 2.5f, up: true);
            float z = body.transform.position.z;
            body.Tick(false, false, false, true, false, Dt);
            Assert.IsFalse(body.IsOn, "Space did not let go");
            Assert.Less(body.transform.position.z, z - 0.01f, "the hop did not push off backwards");
            Assert.IsTrue(body.GetComponent<CharacterController>().enabled);

            body.transform.position = new Vector3(0.0f, 0.05f, 0.1f);
            Physics.SyncTransforms();
            yield return null;
            body.GetOn(ladder, false);
            yield return Hold(body, 2.5f, up: true);
            z = body.transform.position.z;
            body.Tick(false, false, false, false, true, Dt);
            Assert.IsFalse(body.IsOn, "Control did not let go");
            Assert.AreEqual(z, body.transform.position.z, 0.01f, "the drop pushed off");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ACopyToldWhichLadderPutsItsHandsAndFeetOnTheSameRungs()
        {
#if UNITY_EDITOR
            Ladder ladder = Stage();
            PlayerLadder owner = Player(new Vector3(0.0f, 0.05f, 0.1f), Quaternion.identity);
            PlayerLadder copy = Player(new Vector3(0.0f, 0.05f, -6.0f), Quaternion.identity);
            copy.GetComponent<CharacterController>().enabled = false;
            var said = new List<AbilityEvent>();
            owner.Fired += said.Add;
            yield return null;
            owner.GetOn(ladder, false);
            Assert.AreEqual(1, said.Count, "getting on a ladder was not said");
            Assert.AreEqual(PlayerLadder.Id, owner.AbilityId);
            // The copy is moved as the network moves it: to where its owner is, a wall's width to the side so both can be seen.
            Vector3 aside = Vector3.right * 2.0f;
            int heard = 0;
            for (float t = 0.0f; t < 3.0f; t += Dt)
            {
                owner.Tick(t < 2.0f, false, false, false, false, Dt);
                for (; heard < said.Count; heard++)
                {
                    AbilityEvent e = said[heard];
                    copy.PlayRemote(new AbilityEvent(e.Kind, e.Origin + aside, e.Direction, e.A, e.B, e.N));
                }
                copy.transform.SetPositionAndRotation(owner.transform.position + aside, owner.transform.rotation);
                yield return null;
            }
            Assert.Greater(said.Count, 2, "a body on a ladder does not say so again for whoever comes into view");
            Animator a = owner.GetComponentInChildren<Animator>(), b = copy.GetComponentInChildren<Animator>();
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                Assert.Less(Vector3.Distance(a.GetBoneTransform(bone).position + aside, b.GetBoneTransform(bone).position), 0.04f, $"the copy's {bone} is not where its owner's is");
            }
            // Told the body is off, the copy lets go; and it lets go by itself when nothing more is said.
            copy.PlayRemote(new AbilityEvent(0, Vector3.zero, Vector3.up));
            yield return new WaitForSeconds(0.5f);
            Assert.Less(copy.GetComponent<PlayerLocomotion>().LadderWeight, 0.05f, "told its owner let go, the copy still holds the ladder");
#else
            yield break;
#endif
        }
    }
}
