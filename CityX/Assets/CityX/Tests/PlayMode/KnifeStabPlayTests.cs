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
    /// The knife's heavy on the real body (docs/KNIFE.md): at the top of the raise the fist is above the head with the knife upside down in it,
    /// point down (the hand posed for the knife's own hold, so the spin shows instead of being turned back out of it); the stab drives the
    /// point down; afterwards the knife is back in its hold. The same on another player's copy, which is told only the attack's name.
    /// </summary>
    public sealed class KnifeStabPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        private GameObject _ground;
        private GameObject _body;
        private SwordWeapon _knife;

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (Object made in new Object[] { _knife != null ? _knife.gameObject : null, _body, _ground })
            {
                if (made != null) Object.Destroy(made);
            }
        }

#if UNITY_EDITOR
        private void MakeBody(bool copy)
        {
            _body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _body.GetComponent<PlayerInputReader>().enabled = false;
            _body.GetComponent<PlayerVitals>().InfiniteStamina = true;
            _knife = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/KitchenKnife.prefab"),
                new Vector3(20.0f, 1.0f, 20.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            if (copy)
            {
                _knife.MountCosmetic(_body);
            }
            else
            {
                _knife.OnEquip(_body, ItemInstanceData.None);
                _body.GetComponent<SoulsSwordCombat>().HitStopSeconds = 0.0f;
            }
        }
#endif

        [UnityTest]
        public IEnumerator TheStabRaisesTheKnifeUpsideDownAboveTheHeadAndDrivesItDownOnTheBodyAndOnACopy()
        {
#if UNITY_EDITOR
            foreach (bool copy in new[] { false, true })
            {
                string who = copy ? "the copy" : "the player";
                MakeBody(copy);
                var locomotion = _body.GetComponent<PlayerLocomotion>();
                var combat = _body.GetComponent<SoulsSwordCombat>();
                var poser = _body.GetComponent<SwordPoseDriver>();
                Animator animator = _body.GetComponentInChildren<Animator>();
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var input = new LocomotionInput { Sword1HEquipped = true };

                IEnumerator Frame()
                {
                    if (copy)
                    {
                        animator.SetBool("Grounded", true);   // what NetworkedAnimatorSync hands a copy every tick
                    }
                    else
                    {
                        combat.Tick(Dt);
                        locomotion.Tick(input, Dt);
                    }
                    yield return null;
                }

                for (int i = 0; i < 40; i++)
                {
                    yield return Frame();
                }
                Vector3 holdPosition = _knife.transform.localPosition;
                Quaternion holdRotation = _knife.transform.localRotation;

                if (copy)
                {
                    _body.GetComponent<PlayerAnimationDriver>().PlayRemoteState(SwordStrikes.Thrust1HName);
                }
                else
                {
                    combat.RequestHeavy(0.0f);
                }
                SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(SwordStrikeKind.Thrust1H);
                float top = (timing.WindUp + timing.Hold * 0.8f) / timing.Total;
                float contact = SwordStrikes.ContactFraction(SwordStrikeKind.Thrust1H);
                bool sawTop = false, sawContact = false;
                float tipAtTop = 0.0f;
                for (int i = 0; i < 120 && poser.IsStriking; i++)
                {
                    yield return Frame();
                    Assert.AreEqual(SwordStrikeKind.Thrust1H, poser.Kind, $"{who}: the knife's heavy is not the stab");
                    if (!sawTop && poser.Progress >= top)
                    {
                        sawTop = true;
                        Vector3 baseAt = _knife.BladeBaseWorld, tip = _knife.BladeTipWorld;
                        tipAtTop = tip.y;
                        Assert.Greater(hand.position.y, head.position.y, $"{who}: the fist is not raised above the head");
                        Assert.Less(tip.y, baseAt.y - 0.15f, $"{who}: the raised knife's point is not down (the hand turned the spin back out)");
                        Assert.Less(Vector3.Angle(_knife.transform.localRotation * Vector3.up, -(holdRotation * Vector3.up)), 15.0f,
                            $"{who}: the knife is not upside down in the fist");
                    }
                    if (sawTop && !sawContact && poser.Progress >= contact)
                    {
                        sawContact = true;
                        Vector3 tip = _body.transform.InverseTransformPoint(_knife.BladeTipWorld);
                        Assert.Less(_knife.BladeTipWorld.y, tipAtTop - 0.3f, $"{who}: the stab does not drive the point down");
                        Assert.Greater(tip.z, 0.6f, $"{who}: the stab does not land in front");
                    }
                }
                Assert.IsTrue(sawTop && sawContact, $"{who}: the stab never reached its top and its contact");
                for (int i = 0; i < 30; i++)
                {
                    yield return Frame();
                }
                Assert.AreEqual(0.0f, poser.GripFlip, $"{who}: the knife is still spun");
                Assert.Less(Vector3.Distance(holdPosition, _knife.transform.localPosition), 0.001f, $"{who}: the knife is not back where its hold has it");
                Assert.Less(Quaternion.Angle(holdRotation, _knife.transform.localRotation), 0.5f, $"{who}: the knife is not back in its hold");

                Object.Destroy(_knife.gameObject);
                Object.Destroy(_body);
                _knife = null;
                _body = null;
                yield return null;
            }
#else
            yield break;
#endif
        }
    }
}
