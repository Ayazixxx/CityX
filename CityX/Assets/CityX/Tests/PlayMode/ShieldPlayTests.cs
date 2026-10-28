using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The round shield (owner, 2026-10-27: "add a basic round, 1 handed shield ... just do something that functions for testing now";
    /// docs/SHIELD.md) on the real player body: held in the left hand, its fingers closed on it; raised, it comes up before the chest facing forward and the body
    /// walks at half speed; a bullet from the front stops dead on it (no roll, nothing sent back) while one from behind hurts; a blow from the
    /// front is a held guard's (a quarter through, stamina).
    /// </summary>
    public sealed class ShieldPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private (PlayerLocomotion body, PlayerVitals vitals, Transform shield) Shielded()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _made.Add(ground);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            if (player.GetComponent<BulletRicochet>() == null)
            {
                player.AddComponent<BulletRicochet>();
            }
            GameObject shield = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/RoundShield.prefab"));
            Assert.IsNotNull(shield, "no RoundShield.prefab -- rebuild the scene");
            shield.GetComponent<CityX.Items.IEquippableItem>().OnEquip(player, CityX.Items.ItemInstanceData.None);
            return (player.GetComponent<PlayerLocomotion>(), player.GetComponent<PlayerVitals>(), shield.transform);
#else
            return (null, null, null);
#endif
        }

        private static IEnumerator Hold(PlayerLocomotion body, LocomotionInput input, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Step)
            {
                body.Tick(input, Step);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ItIsHeldInTheLeftHandAndRaisedItComesUpBeforeTheChestFacingForward()
        {
            (PlayerLocomotion body, PlayerVitals vitals, Transform shield) = Shielded();
            Animator animator = body.GetComponentInChildren<Animator>();
            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.LeftHand), shield.parent, "the shield is not in the left hand");
            Assert.AreSame(shield, vitals.Shield);
            Assert.AreSame(shield, body.Shield);

            yield return Hold(body, default, 0.5f);
            Assert.Greater(body.LeftGrip, 0.9f, "the left hand did not close on the shield's grip");
            Assert.Less(Vector3.Angle(shield.forward, -body.transform.right), 35.0f, "lowered, the shield does not face out to the side");

            yield return Hold(body, new LocomotionInput { ShieldRaised = true }, 0.5f);
            Assert.AreEqual(1.0f, body.ShieldRaise, 1e-3f);
            Assert.Less(Vector3.Angle(shield.forward, body.transform.forward), 5.0f, "raised, the shield does not face forward");
            Vector3 local = body.transform.InverseTransformPoint(shield.position);
            TestContext.WriteLine($"raised shield at {local} in the body's frame");
            Assert.Greater(local.z, 0.4f, "the raised shield is not out in front");
            Assert.Greater(local.y, 1.0f, "the raised shield is not up before the chest");
            Assert.Less(local.y, 2.2f, "the raised shield is over the head");
        }

        [UnityTest]
        public IEnumerator WithTheShieldUpTheBodyWalksAtHalfSpeed()
        {
            (PlayerLocomotion body, PlayerVitals vitals, Transform shield) = Shielded();
            yield return Hold(body, new LocomotionInput { Move = Vector2.up, ShieldRaised = true }, 1.0f);
            Assert.AreEqual(body.WalkSpeed * PlayerLocomotion.ShieldMoveSpeedShare, body.CurrentSpeed, 0.05f);
        }

        [UnityTest]
        public IEnumerator ARaisedShieldStopsABulletFromTheFrontDeadButNotOneFromBehind()
        {
            (PlayerLocomotion body, PlayerVitals vitals, Transform shield) = Shielded();
            BulletRicochet ricochet = body.GetComponent<BulletRicochet>();
            yield return null;
            vitals.ShieldRaised = true;
            vitals.Roll = () => 1.0f;   // the roll would fail: a shield does not roll for bullets
            float health = vitals.Health;
            Vector3 front = body.transform.position + body.transform.forward * 10.0f + Vector3.up * 2.0f;
            Vector3 behind = body.transform.position - body.transform.forward * 10.0f + Vector3.up * 2.0f;
            Assert.AreEqual(HitResponse.Blocked, DamageContext.Strike(vitals, 20.0f, front, 0.0f, DamageKind.Bullet, 0.0f));
            Assert.AreEqual(health, vitals.Health, 0.01f, "a bullet from the front came through the raised shield");
            Assert.AreEqual(0, ricochet.Ricochets, "a shield sent the bullet back: that is the sword's");
            DamageContext.Strike(vitals, 20.0f, behind, 0.0f, DamageKind.Bullet, 0.0f);
            Assert.AreEqual(health - 20.0f, vitals.Health, 0.01f, "a bullet from behind was stopped by the shield");
            vitals.ShieldRaised = false;
            health = vitals.Health;
            DamageContext.Strike(vitals, 20.0f, front, 0.0f, DamageKind.Bullet, 0.0f);
            Assert.AreEqual(health - 20.0f, vitals.Health, 0.01f, "a lowered shield stopped a bullet");
        }

        [UnityTest]
        public IEnumerator ABlowFromTheFrontOnARaisedShieldIsAHeldGuards()
        {
            (PlayerLocomotion body, PlayerVitals vitals, Transform shield) = Shielded();
            yield return null;
            vitals.ShieldRaised = true;
            vitals.Roll = () => 1.0f;
            float health = vitals.Health, stamina = vitals.Stamina;
            Vector3 front = body.transform.position + body.transform.forward * 1.5f + Vector3.up * 1.5f;
            Assert.AreEqual(HitResponse.Guarded, DamageContext.Strike(vitals, 20.0f, front, 0.3f, DamageKind.Physical, 0.0f));
            Assert.AreEqual(health - 20.0f * CombatTuning.BlockDamageMultiplier, vitals.Health, 0.01f, "not a quarter of the blow came through");
            Assert.Less(vitals.Stamina, stamina, "holding the blow off cost no stamina");
            Assert.AreEqual(0.0f, vitals.HitstunRemaining, 1e-4f, "a guarded blow stunned the defender");
        }
    }
}
