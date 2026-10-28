using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Crouching as Source does it (owner, 2026-10-27: "the crouch animation looks bad and i dont think it even shortens the hitbox. I kind of
    /// want the crouching to work like it does in source games with the crouch jump as well"; docs/CROUCH.md), on the real player body on
    /// flat ground: the controller really is shorter once ducked, and the crouched pose fits inside it with its feet on the ground; a third
    /// of the speed; no standing up under a low ceiling; a duck in the air lifts the feet and not the head, which clears a ledge a plain jump
    /// cannot; and the camera's eyes come down with the duck.
    /// </summary>
    public sealed class PlayerLocomotionCrouchPlayTests
    {
        private const float StepSeconds = 1.0f / 60.0f;
        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private CharacterController _controller;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _ground = Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(200.0f, 1.0f, 200.0f));
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            foreach (GameObject made in _made)
            {
                if (made != null) Object.Destroy(made);
            }
            _made.Clear();
        }

        private GameObject Box(Vector3 centre, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = centre;
            box.transform.localScale = size;
            _made.Add(box);
            Physics.SyncTransforms();
            return box;
        }

        private IEnumerator Spawn(Vector3 at)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(prefab, "Player.prefab is missing");
            _player = Object.Instantiate(prefab, at, Quaternion.identity);
            var reader = _player.GetComponent<PlayerInputReader>();
            if (reader != null) reader.enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _controller = _player.GetComponent<CharacterController>();
            yield return Hold(default, 0.5f);
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        private IEnumerator Hold(LocomotionInput input, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += StepSeconds)
            {
                _locomotion.Tick(input, StepSeconds);
                yield return null;
            }
        }

        private static LocomotionInput Crouch(bool held, Vector2 move = default) => new LocomotionInput { Crouch = held, Move = move };

        [UnityTest]
        public IEnumerator DuckedTheControllerIsShorterAndTheCrouchedBodyFitsInsideItWithItsFeetOnTheGround()
        {
            yield return Spawn(new Vector3(0.0f, 0.1f, 0.0f));
            Assert.AreEqual(_locomotion.StandingHeight, _controller.height, 1e-3f);
            yield return Hold(Crouch(true), 1.0f);
            Assert.IsTrue(_locomotion.IsDucked);
            Assert.AreEqual(_locomotion.CrouchedHeight, _controller.height, 1e-3f, "the hitbox did not shrink");

            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            float scale = _locomotion.StandingHeight / BodyRig.StandingHeight;
            float feet = _player.transform.position.y;
            float crown = head.position.y + FusedBodyShape.HeadRadius * scale - feet;
            TestContext.WriteLine($"crouched crown {crown:0.00} m, controller {_controller.height:0.00} m, feet {leftFoot.position.y - feet:0.000} / {rightFoot.position.y - feet:0.000} m");
            Assert.LessOrEqual(crown, _controller.height + 0.03f, "the crouched head sticks out of the crouched hitbox");
            Assert.Greater(crown, _controller.height - 0.25f, "the crouched hitbox is far taller than the crouched body");
            Assert.AreEqual(BodyRig.FootY * scale, leftFoot.position.y - feet, 0.06f, "the left foot is off the ground");
            Assert.AreEqual(BodyRig.FootY * scale, rightFoot.position.y - feet, 0.06f, "the right foot is off the ground");

            yield return Hold(Crouch(false), 0.5f);
            Assert.IsFalse(_locomotion.IsDucked);
            Assert.AreEqual(_locomotion.StandingHeight, _controller.height, 1e-3f, "it did not stand back up");
        }

        [UnityTest]
        public IEnumerator CrouchedTheBodyWalksAtAThirdOfItsSpeedAndCannotSprint()
        {
            yield return Spawn(new Vector3(0.0f, 0.1f, 0.0f));
            var walk = new LocomotionInput { Move = Vector2.up, Crouch = true, Sprint = true };
            yield return Hold(walk, 1.5f);
            Assert.IsFalse(_locomotion.IsSprinting, "a crouched body sprinted");
            Assert.AreEqual(_locomotion.WalkSpeed * CrouchHull.SpeedShare, _locomotion.CurrentSpeed, 0.05f, "not Source's third of the speed");
        }

        [UnityTest]
        public IEnumerator UnderALowCeilingTheBodyStaysCrouchedUntilItIsOut()
        {
            yield return Spawn(new Vector3(0.0f, 0.1f, -4.0f));
            // A slab whose underside is between the crouched and the standing height, from z = -1 to z = 3.
            float under = (_locomotion.CrouchedHeight + _locomotion.StandingHeight) * 0.5f;
            Box(new Vector3(0.0f, under + 0.25f, 1.0f), new Vector3(6.0f, 0.5f, 4.0f));
            yield return Hold(Crouch(true), 0.6f);
            yield return Hold(Crouch(true, Vector2.up), 3.5f);
            Assert.Greater(_player.transform.position.z, -0.5f, "setup: the body never got under the slab");
            Assert.Less(_player.transform.position.z, 2.5f, "setup: the body walked right through");
            yield return Hold(Crouch(false), 0.5f);
            Assert.IsTrue(_locomotion.IsDucked, "it stood up under the slab");
            Assert.AreEqual(_locomotion.CrouchedHeight, _controller.height, 1e-3f);
            yield return Hold(Crouch(false, Vector2.up), 4.0f);
            Assert.Greater(_player.transform.position.z, 3.5f, "it could not walk out crouched");
            yield return Hold(Crouch(false), 0.5f);
            Assert.IsFalse(_locomotion.IsDucked, "out from under the slab it did not stand up");
        }

        [UnityTest]
        public IEnumerator DuckingInTheAirLiftsTheFeetAndKeepsTheHeadAndTheEyes()
        {
            yield return Spawn(new Vector3(0.0f, 0.1f, 0.0f));
            yield return Hold(new LocomotionInput { Jump = true }, StepSeconds);
            yield return Hold(default, 0.15f);
            float feetBefore = _player.transform.position.y;
            float topBefore = feetBefore + _controller.height;
            float eyesBefore = feetBefore - _locomotion.CrouchDrop;
            _locomotion.Tick(Crouch(true), StepSeconds);
            float feetAfter = _player.transform.position.y;
            float topAfter = feetAfter + _controller.height;
            float rise = feetAfter - feetBefore;
            Assert.AreEqual(_locomotion.StandingHeight - _locomotion.CrouchedHeight, rise, 0.12f, "the feet did not come up by the hull difference");
            Assert.AreEqual(topBefore, topAfter, 0.12f, "the head moved");
            Assert.AreEqual(eyesBefore, feetAfter - _locomotion.CrouchDrop, 0.12f, "the camera's eyes jumped with the tuck");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheCrouchJumpClearsALedgeAPlainJumpCannot()
        {
            // A wall in front whose top is above a plain jump's reach and under a crouch jump's.
            yield return Spawn(new Vector3(0.0f, 0.1f, 0.0f));
            float plainReach = _locomotion.JumpVelocity * _locomotion.JumpVelocity / (2.0f * _locomotion.Gravity * Mathf.Max(0.01f, _locomotion.GravityScale));
            // The controller climbs any edge within its step height even in the air: a plain jump reaches that much higher than its feet do.
            float ledge = plainReach + _controller.stepOffset + 0.2f;
            TestContext.WriteLine($"plain jump reach {plainReach:0.00} m + step {_controller.stepOffset:0.00} m, ledge {ledge:0.00} m, tuck {_locomotion.StandingHeight - _locomotion.CrouchedHeight:0.00} m");
            Box(new Vector3(0.0f, ledge * 0.5f, 22.0f), new Vector3(6.0f, ledge, 40.0f));   // deep, so a body that gets up there stays up there

            float plainTop = 0.0f;
            yield return JumpAt(false, top => plainTop = top);
            Assert.Less(plainTop, ledge - 0.05f, "a plain jump got on the ledge: the test proves nothing");

            _player.transform.position = new Vector3(0.0f, 0.1f, 0.0f);
            Physics.SyncTransforms();
            yield return Hold(default, 0.5f);
            float crouchTop = 0.0f;
            yield return JumpAt(true, top => crouchTop = top);
            Assert.GreaterOrEqual(crouchTop, ledge - 0.05f, "the crouch jump did not get the body onto the ledge");
        }

        /// <summary>Jumps forward at the wall (ducking at the top if <paramref name="crouch"/>) and reports where the feet stand after.</summary>
        private IEnumerator JumpAt(bool crouch, System.Action<float> landed)
        {
            yield return Hold(new LocomotionInput { Move = Vector2.up, Jump = true }, StepSeconds);
            for (float t = 0.0f; t < 2.0f; t += StepSeconds)
            {
                _locomotion.Tick(new LocomotionInput { Move = Vector2.up, Crouch = crouch && t > 0.2f }, StepSeconds);
                yield return null;
            }
            yield return Hold(new LocomotionInput { Move = Vector2.up }, 1.0f);
            landed(_player.transform.position.y);
        }
    }
}
