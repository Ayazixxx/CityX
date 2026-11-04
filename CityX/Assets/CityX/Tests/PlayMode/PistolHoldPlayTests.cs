using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// The Glock in the real hands (owner, 2026-11-03: "put the finger that pulls the trigger through that hole", "look its fucking poking through"). The
    /// fingers are checked against the model's own outline in millimetres (CityX/SourceArt/Glock/glock_model.py, FRAME_OUTLINE and GUARD_OUTLINE, at its SCALE), on the
    /// shooter's body and on another player's copy, carried and aimed.
    /// </summary>
    public sealed class PistolHoldPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        /// <summary>A millimetre of the outline in the gun's metres (glock_model.py SCALE).</summary>
        private const float Mm = 0.0025f;

        private const float FrameHalfWidth = 15.0f * Mm;

        // The trigger guard's opening, gun frame: the frame's underside to the guard's bottom band, the frame's front to the guard's front wall.
        private const float OpeningTop = -5.0f * Mm;
        private const float OpeningBottom = -28.0f * Mm;
        private const float OpeningRear = 73.0f * Mm;
        private const float OpeningFront = 116.0f * Mm;

        /// <summary>The trigger's blade at the height a finger rests on it (TRIGGER_PIVOT_MM plus TRIGGER_OUTLINE's front edge).</summary>
        private const float TriggerFront = 87.0f * Mm;

        // The grip's front strap, height against how far forward (FRAME_OUTLINE), from the trigger guard down to the baseplate.
        private static readonly Vector2[] FrontStrap =
        {
            new Vector2(-30.0f, 64.5f), new Vector2(-40.0f, 61.0f), new Vector2(-55.0f, 54.0f), new Vector2(-74.0f, 47.0f), new Vector2(-86.5f, 44.0f),
        };

        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private PistolWeapon _gun;
        private Transform _handR, _handL;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_gun != null) Object.Destroy(_gun.gameObject);
            if (_ground != null) Object.Destroy(_ground);
        }

        private IEnumerator Spawn(bool asCopy)
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _gun = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Pistol.prefab"), new Vector3(20.0f, 1.0f, 20.0f), Quaternion.identity).GetComponent<PistolWeapon>();
            if (asCopy)
            {
                _gun.MountCosmetic(_player);
            }
            else
            {
                _gun.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            }
            Animator animator = _player.GetComponentInChildren<Animator>();
            _handR = animator.GetBoneTransform(HumanBodyBones.RightHand);
            _handL = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            yield return null;
#else
            yield break;
#endif
        }

        private IEnumerator Tick(bool aiming, float seconds = 1.0f)
        {
            for (float t = 0.0f; t < seconds; t += Dt)
            {
                _locomotion.Tick(new LocomotionInput { Aiming = aiming, GunEquipped = true, YawDegrees = 0.0f }, Dt);
                yield return null;
            }
        }

        /// <summary>Where a finger's tip bone ends, in the gun's frame.</summary>
        private Vector3 TipInGun(Transform hand, string digit, string side)
        {
            Transform tipBone = hand.Find(digit + "1_" + side).Find(digit + "2_" + side);
            foreach (FusedBodyShape.Digit shape in FusedBodyShape.Digits)
            {
                if (shape.Name == digit)
                {
                    return _gun.transform.InverseTransformPoint(tipBone.TransformPoint(BodyRig.FingerLocal(shape.Tip - shape.Knuckle, side == "r")));
                }
            }
            throw new System.ArgumentException(digit);
        }

        private Vector3 KnuckleInGun(Transform hand, string digit, string side)
        {
            return _gun.transform.InverseTransformPoint(hand.Find(digit + "1_" + side).Find(digit + "2_" + side).position);
        }

        /// <summary>How far forward the front strap is at a height in the gun's frame, metres (its ends carried on above and below).</summary>
        private static float FrontStrapAt(float y)
        {
            float mm = y / Mm;
            if (mm >= FrontStrap[0].x)
            {
                return FrontStrap[0].y * Mm;
            }
            for (int i = 1; i < FrontStrap.Length; i++)
            {
                if (mm >= FrontStrap[i].x)
                {
                    return Mathf.Lerp(FrontStrap[i - 1].y, FrontStrap[i].y, Mathf.InverseLerp(FrontStrap[i - 1].x, FrontStrap[i].x, mm)) * Mm;
                }
            }
            return FrontStrap[FrontStrap.Length - 1].y * Mm;
        }

        private void AssertTriggerFingerOnTheTrigger(string when)
        {
            Vector3 tip = TipInGun(_handR, "index", "r");
            Assert.Less(Mathf.Abs(tip.x), FrameHalfWidth, $"{when}: the trigger finger does not reach across to the trigger (tip at {tip:F3})");
            Assert.That(tip.y, Is.InRange(OpeningBottom, OpeningTop), $"{when}: the trigger finger's tip is not in the guard's opening (tip at {tip:F3})");
            Assert.That(tip.z, Is.InRange(OpeningRear, OpeningFront), $"{when}: the trigger finger's tip is not in the guard's opening (tip at {tip:F3})");
            Assert.Less(Mathf.Abs(tip.z - FusedBodyShape.FingerRadius - TriggerFront), 0.03f, $"{when}: the pad is not on the trigger (tip at {tip:F3}, the blade's front at {TriggerFront:F3})");
        }

        /// <summary>A finger's centre line must stay a good part of its radius in front of the front strap: inside it, it comes out the other side of the gun.</summary>
        private void AssertOutsideTheGrip(Transform hand, string digit, string side, string when)
        {
            float clear = FusedBodyShape.FingerRadius * 0.6f;
            foreach (Vector3 point in new[] { KnuckleInGun(hand, digit, side), TipInGun(hand, digit, side) })
            {
                bool besideTheGrip = Mathf.Abs(point.x) > FrameHalfWidth + clear;
                bool belowTheGrip = point.y < -86.5f * Mm - clear;
                float front = FrontStrapAt(point.y);
                Assert.IsTrue(besideTheGrip || belowTheGrip || point.z > front + clear,
                    $"{when}: {side} {digit} is inside the grip at {point:F3} (the front strap is at {front:F3})");
            }
        }

        [UnityTest]
        public IEnumerator TheTriggerFingerIsThroughTheGuardOnTheTriggerCarriedAndAimed()
        {
#if UNITY_EDITOR
            yield return Spawn(false);
            yield return Tick(false);
            AssertTriggerFingerOnTheTrigger("carried");
            yield return Tick(true);
            AssertTriggerFingerOnTheTrigger("aimed");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator NoFingerIsInsideTheGrip()
        {
#if UNITY_EDITOR
            yield return Spawn(false);
            yield return Tick(true);
            foreach (string digit in new[] { "middle", "ring", "little" })
            {
                AssertOutsideTheGrip(_handR, digit, "r", "aimed");
            }
            foreach (string digit in new[] { "index", "middle", "ring", "little" })
            {
                AssertOutsideTheGrip(_handL, digit, "l", "aimed");
            }
#else
            yield break;
#endif
        }

        /// <summary>The thumbs lie along the frame's sides, clear of it: their folds curled them into it.</summary>
        [UnityTest]
        public IEnumerator TheThumbsLieAlongTheFramesSides()
        {
#if UNITY_EDITOR
            yield return Spawn(false);
            yield return Tick(true);
            Vector3 right = TipInGun(_handR, "thumb", "r");
            Vector3 left = TipInGun(_handL, "thumb", "l");
            float clear = FrameHalfWidth + FusedBodyShape.ThumbRadius * 0.8f;
            Assert.Greater(right.x, clear, $"the right thumb's tip is in the frame at {right:F3}");
            Assert.Less(left.x, -clear, $"the left thumb's tip is in the frame at {left:F3}");
            Assert.Greater(right.z, _gun.transform.InverseTransformPoint(_handR.Find("thumb1_r").position).z, "the right thumb does not point forward");
            Assert.Greater(left.z, _gun.transform.InverseTransformPoint(_handL.Find("thumb1_l").position).z, "the left thumb does not point forward");
#else
            yield break;
#endif
        }

        /// <summary>Straightened along the forearm, as every empty hand is, the left hand's fingers ran through the gun.</summary>
        [UnityTest]
        public IEnumerator TheLeftHandLiesFlatOnTheGripWithItsFingersForward()
        {
#if UNITY_EDITOR
            yield return Spawn(false);
            yield return Tick(true);
            Assert.Less(_locomotion.SupportHandError, 0.02f, "the left palm is not on its place on the gun");
            Vector3 fingers = _gun.transform.InverseTransformDirection(-_handL.right);
            Vector3 palm = _gun.transform.InverseTransformDirection(-_handL.up);
            Assert.Greater(fingers.z, 0.8f, $"the left hand's fingers do not run forward along the gun: {fingers:F2}");
            Assert.Greater(palm.x, 0.95f, $"the left palm does not face the grip: {palm:F2}");
#else
            yield break;
#endif
        }

        /// <summary>Other players see the same hold: a copy's pistol is raised by its synced Aiming parameter, in both hands, finger on the trigger.</summary>
        [UnityTest]
        public IEnumerator AnotherPlayersCopyHoldsItTheSameWay()
        {
#if UNITY_EDITOR
            yield return Spawn(true);
            Assert.IsTrue(_locomotion.HoldsPistol, "the copy does not hold it as a pistol");
            Animator animator = _player.GetComponentInChildren<Animator>();
            for (float t = 0.0f; t < 1.0f; t += Dt)
            {
                animator.SetBool("Aiming", true);
                yield return null;
            }
            Assert.Less(_locomotion.SupportHandError, 0.02f, "the copy's left palm is not on the gun");
            Vector3 fingers = _gun.transform.InverseTransformDirection(-_handL.right);
            Assert.Greater(fingers.z, 0.8f, $"the copy's left hand does not lie on the grip: {fingers:F2}");
            AssertTriggerFingerOnTheTrigger("on a copy");
#else
            yield break;
#endif
        }
    }
}
