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
    /// Arcane Barrier on the real body (docs/ARCANE_BARRIER.md): raised before the body turned to where the camera looks, the arms out to it, the
    /// body held still; a shot at its face stops on the wall and hurts only the wall, a blade bounces off it, from behind it stops nothing;
    /// broken it fades away where it stood and never shatters, let go it fades quickly, unheld or out of mana it goes; another player's copy sees
    /// it rise with its collider, take each blow and fade; an arcane ball on it leaves no orbs.
    /// </summary>
    public sealed class ArcaneBarrierPlayTests
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
            foreach (ArcaneBarrierWall wall in Object.FindObjectsByType<ArcaneBarrierWall>(FindObjectsSortMode.None)) Object.Destroy(wall.gameObject);
            foreach (ArcaneOrbProjectile b in Object.FindObjectsByType<ArcaneOrbProjectile>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
            foreach (CursedOrbs c in Object.FindObjectsByType<CursedOrbs>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
        }

        private GameObject Body(Vector3 at)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
                _made.Add(ground);
            }
            // The plain body (a networked one would want a session), with the spell as PlayerAbilityWiring gives it.
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            if (player.GetComponent<ArcaneBarrierCaster>() == null)
            {
                player.AddComponent<ArcaneBarrierCaster>();
            }
            return player;
#else
            return null;
#endif
        }

        /// <summary>Holds the key (or not) for <paramref name="seconds"/>, ticking the body as PlayerInputReader does while the wall stands.</summary>
        private static IEnumerator Hold(GameObject player, bool held, float seconds)
        {
            var caster = player.GetComponent<ArcaneBarrierCaster>();
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                if (held)
                {
                    caster.Hold(true);
                }
                locomotion.Tick(new LocomotionInput { YawDegrees = player.transform.eulerAngles.y }, Step);
                yield return null;
            }
        }

        private static float Ahead(GameObject player, Vector3 point) => Vector3.Dot(point - player.transform.position, player.transform.forward);

        [UnityTest]
        public IEnumerator RaisedItStandsBeforeTheBodyTurnedToTheCameraWithTheArmsOutAndHoldsItStill()
        {
            GameObject player = Body(Vector3.zero);
            yield return Hold(player, false, 0.3f);
            var caster = player.GetComponent<ArcaneBarrierCaster>();
            var vitals = player.GetComponent<PlayerVitals>();
            float mana = vitals.Mana;

            Assert.IsTrue(caster.TryRaise(Vector3.right + Vector3.down * 0.3f), "it did not rise");
            Assert.Greater(Vector3.Dot(player.transform.forward, Vector3.right), 0.999f, "the body was not turned to where the camera looks");
            Assert.AreEqual(mana - ArcaneBarrierRules.RaiseManaCost, vitals.Mana, 0.5f, "raising it did not cost its mana");
            Assert.IsTrue(player.GetComponent<PlayerLocomotion>().MovementRooted, "the body is not held still");
            Assert.AreSame(caster, vitals.Barrier, "the body's hits do not ask the barrier");
            yield return Hold(player, true, 0.4f);

            ArcaneBarrierWall wall = caster.Wall;
            Assert.IsNotNull(wall, "no wall");
            Assert.AreEqual(ArcaneBarrierRules.Distance, Ahead(player, wall.transform.position), 0.05f, "not in front of the body");
            Assert.Greater(Vector3.Dot(wall.Facing, player.transform.forward), 0.999f, "it does not face away from the caster");
            Assert.IsTrue(wall.Collider.enabled, "its collider is off");
            Assert.That(wall.Strength, Is.InRange(0.5f, 1.0f), "its face is not drawn");
            Assert.AreEqual(ArcaneBarrierRules.HealthFor(1), caster.MaxHealth, 1e-3f);

            var locomotion = player.GetComponent<PlayerLocomotion>();
            Assert.Greater(locomotion.BarrierRaise, 0.99f, "the arms are not out");
            Animator animator = player.GetComponentInChildren<Animator>();
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
            {
                Transform hand = animator.GetBoneTransform(bone);
                Assert.Greater(Ahead(player, hand.position), 0.45f, $"the {bone} is not pushed out before the chest");
                Assert.Greater(hand.position.y, BodyRig.StandingHeight * 0.5f, $"the {bone} is not up at the chest");
            }
        }

        [UnityTest]
        public IEnumerator AShotAtItsFaceStopsOnTheWallAndABladeBouncesOffItHurtingOnlyTheWall()
        {
            GameObject player = Body(Vector3.zero);
            yield return Hold(player, false, 0.2f);
            var caster = player.GetComponent<ArcaneBarrierCaster>();
            var vitals = player.GetComponent<PlayerVitals>();
            Assert.IsTrue(caster.TryRaise(Vector3.forward));
            yield return Hold(player, true, 0.3f);
            float health = vitals.Health;

            Vector3 muzzle = new Vector3(0.3f, 1.6f, 15.0f);
            Vector3 chest = player.transform.position + Vector3.up * 1.5f;
            Assert.IsTrue(Physics.Raycast(muzzle, (chest - muzzle).normalized, out RaycastHit hit, 20.0f, ~0, QueryTriggerInteraction.Ignore), "the shot hit nothing");
            Assert.AreSame(caster.Wall.Collider, hit.collider, "the shot did not stop on the wall");
            Assert.AreSame(vitals, hit.collider.GetComponentInParent<IDamageable>(), "a shot on the wall does not go to the body behind it");

            HitResponse shot = DamageContext.Strike(vitals, 40.0f, muzzle, 0.0f, DamageKind.Bullet, 0.0f, hit.point, null);
            Assert.AreEqual(HitResponse.Guarded, shot);
            Assert.AreEqual(health, vitals.Health, 1e-3f, "the shot hurt the body");
            Assert.AreEqual(caster.MaxHealth - 40.0f, caster.Health, 1e-3f, "the wall did not take the shot");

            HitResponse blade = DamageContext.Strike(vitals, 25.0f, new Vector3(0.4f, 0.0f, 2.3f), 0.3f, DamageKind.Physical, 0.0f, null, null);
            Assert.AreEqual(HitResponse.Guarded, blade, "a blade does not bounce off it");
            Assert.AreEqual(health, vitals.Health, 1e-3f, "the blade hurt the body");
            Assert.IsFalse(vitals.IsStunned, "the blade staggered the caster through the wall");
            Assert.AreEqual(caster.MaxHealth - 65.0f, caster.Health, 1e-3f);
            yield return null;
            Assert.Greater(caster.Wall.transform.childCount, 4, "no ring where it was struck");
        }

        [UnityTest]
        public IEnumerator FromBehindItStopsNothing()
        {
            GameObject player = Body(Vector3.zero);
            yield return Hold(player, false, 0.2f);
            var caster = player.GetComponent<ArcaneBarrierCaster>();
            var vitals = player.GetComponent<PlayerVitals>();
            Assert.IsTrue(caster.TryRaise(Vector3.forward));
            yield return Hold(player, true, 0.2f);
            float health = vitals.Health;
            HitResponse response = DamageContext.Strike(vitals, 20.0f, new Vector3(0.0f, 1.6f, -8.0f), 0.0f, DamageKind.Bullet, 0.0f);
            Assert.AreEqual(HitResponse.Landed, response);
            Assert.Less(vitals.Health, health, "a shot from behind did not hurt");
            Assert.AreEqual(caster.MaxHealth, caster.Health, 1e-3f, "the wall took a shot from behind it");
        }

        /// <summary>Owner: "i dont want the arcane barrier to shatter make it fade away".</summary>
        [UnityTest]
        public IEnumerator BrokenItFadesAwayWhereItStoodAndNeverShatters()
        {
            GameObject player = Body(Vector3.zero);
            yield return Hold(player, false, 0.2f);
            var caster = player.GetComponent<ArcaneBarrierCaster>();
            var vitals = player.GetComponent<PlayerVitals>();
            Assert.IsTrue(caster.TryRaise(Vector3.forward));
            yield return Hold(player, true, 0.3f);
            ArcaneBarrierWall wall = caster.Wall;
            Vector3 stood = wall.transform.position;
            int bodiesBefore = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;
            int renderersBefore = wall.GetComponentsInChildren<Renderer>().Length;

            DamageContext.Strike(vitals, caster.MaxHealth + 10.0f, new Vector3(0.0f, 1.6f, 10.0f), 0.0f, DamageKind.Bullet, 0.0f);
            Assert.IsFalse(caster.IsUp, "it did not break");
            Assert.IsTrue(wall.Fading, "it did not start to fade");
            Assert.IsFalse(wall.Collider.enabled, "a fading wall still stops things");
            Assert.IsNull(wall.transform.parent, "it did not stay where it stood");
            Assert.IsNull(vitals.Barrier, "the body still asks it");
            Assert.IsFalse(player.GetComponent<PlayerLocomotion>().MovementRooted, "the body is still held");
            Assert.AreEqual(ArcaneBarrierRules.BrokenCooldownSeconds, caster.CooldownRemaining, 1e-3f);

            float last = float.MaxValue;
            for (float t = 0.0f; t < ArcaneBarrierRules.BreakFadeSeconds - 0.1f; t += Step)
            {
                yield return null;
                Assert.IsTrue(wall != null, "it vanished instead of fading");
                Assert.LessOrEqual(wall.Strength, last + 1e-3f, "it brightened while fading");
                last = wall.Strength;
                Assert.Less((wall.transform.position - stood).magnitude, 0.01f, "it moved: something broke it apart");
                Assert.AreEqual(renderersBefore, wall.GetComponentsInChildren<Renderer>().Length - CountRipples(wall), "it came apart into pieces");
                Assert.LessOrEqual(Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length, bodiesBefore, "pieces flew off it");
            }
            Assert.Less(last, 0.25f, "it had not faded");
            yield return Hold(player, false, 0.2f);
            Assert.IsTrue(wall == null, "it was not gone once faded");

            Assert.IsFalse(caster.TryRaise(Vector3.forward), "it rose again while waiting after it broke");
            caster.Tick(ArcaneBarrierRules.BrokenCooldownSeconds);
            Assert.IsTrue(caster.TryRaise(Vector3.forward), "it did not rise again after its wait");
        }

        private static int CountRipples(ArcaneBarrierWall wall)
        {
            int count = 0;
            foreach (Transform child in wall.transform)
            {
                if (child.name == "Ripple") count++;
            }
            return count;
        }

        [UnityTest]
        public IEnumerator LetGoItFadesQuicklyAndUnheldItGoes()
        {
            GameObject player = Body(Vector3.zero);
            yield return Hold(player, false, 0.2f);
            var caster = player.GetComponent<ArcaneBarrierCaster>();
            Assert.IsTrue(caster.TryRaise(Vector3.forward));
            yield return Hold(player, true, 0.3f);
            ArcaneBarrierWall wall = caster.Wall;
            caster.Hold(false);
            Assert.IsFalse(caster.IsUp, "letting go of the key did not lower it");
            Assert.IsTrue(wall.Fading);
            Assert.AreEqual(ArcaneBarrierRules.CooldownSeconds, caster.CooldownRemaining, 1e-3f);
            yield return Hold(player, false, ArcaneBarrierRules.LowerFadeSeconds + 0.1f);
            Assert.IsTrue(wall == null, "it had not faded");

            // Not told the key is held (the body stunned or knocked down: PlayerInputReader does not get that far), it goes by itself.
            caster.Tick(ArcaneBarrierRules.CooldownSeconds);
            Assert.IsTrue(caster.TryRaise(Vector3.forward));
            yield return Hold(player, false, ArcaneBarrierCaster.HoldGraceSeconds + 0.1f);
            Assert.IsFalse(caster.IsUp, "unheld, it stayed up");
        }

        [UnityTest]
        public IEnumerator HoldingItSpendsManaAndItDropsWhenTheManaRunsOut()
        {
            GameObject player = Body(Vector3.zero);
            yield return Hold(player, false, 0.2f);
            var caster = player.GetComponent<ArcaneBarrierCaster>();
            var vitals = player.GetComponent<PlayerVitals>();
            Assert.IsTrue(vitals.TrySpendMana(vitals.Mana - (ArcaneBarrierRules.RaiseManaCost + 3.0f)));
            Assert.IsTrue(caster.TryRaise(Vector3.forward));
            yield return Hold(player, true, 0.2f);
            Assert.IsTrue(caster.IsUp, "it dropped too soon");
            yield return Hold(player, true, 1.5f);
            Assert.IsFalse(caster.IsUp, "it stayed up with no mana to hold it");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopySeesItRiseWithItsColliderTakeEachBlowAndFade()
        {
            GameObject owner = Body(Vector3.zero);
            GameObject copy = Body(new Vector3(10.0f, 0.0f, 0.0f));
            yield return Hold(owner, false, 0.2f);
            var caster = owner.GetComponent<ArcaneBarrierCaster>();
            var copyCaster = copy.GetComponent<ArcaneBarrierCaster>();
            caster.Fired += copyCaster.PlayRemote;

            Assert.IsTrue(caster.TryRaise(Vector3.forward));
            yield return Hold(owner, true, 0.3f);
            ArcaneBarrierWall copyWall = copyCaster.Wall;
            Assert.IsNotNull(copyWall, "the copy shows no wall");
            Assert.IsTrue(copyWall.Collider.enabled, "shots on the copy's machine would not stop on its wall");
            Assert.AreEqual(ArcaneBarrierRules.Distance, Ahead(copy, copyWall.transform.position), 0.05f);
            Assert.IsTrue(copy.GetComponent<PlayerVitals>().Barrier.Covers(copy.transform.position + new Vector3(0.0f, 1.6f, 10.0f)),
                "on the copy's machine a blow at the wall does not bounce off it");
            Assert.Greater(copy.GetComponent<PlayerLocomotion>().BarrierRaise, 0.9f, "the copy's arms are not out");

            DamageContext.Strike(owner.GetComponent<PlayerVitals>(), caster.MaxHealth * 0.5f, new Vector3(0.0f, 1.6f, 10.0f), 0.0f, DamageKind.Bullet, 0.0f);
            Assert.AreEqual(0.5f, copyWall.HealthShare, 1e-3f, "the copy's wall did not take the blow");

            DamageContext.Strike(owner.GetComponent<PlayerVitals>(), caster.MaxHealth, new Vector3(0.0f, 1.6f, 10.0f), 0.0f, DamageKind.Bullet, 0.0f);
            Assert.IsTrue(copyWall.Fading, "the copy's wall did not fade when it broke");
            Assert.IsFalse(copyCaster.IsUp);
            Assert.IsNull(copy.GetComponent<PlayerVitals>().Barrier);
            caster.Fired -= copyCaster.PlayRemote;
        }

        [UnityTest]
        public IEnumerator AnArcaneBallOnItHurtsTheWallAndLeavesNoOrbs()
        {
            GameObject player = Body(Vector3.zero);
            yield return Hold(player, false, 0.2f);
            var caster = player.GetComponent<ArcaneBarrierCaster>();
            Assert.IsTrue(caster.TryRaise(Vector3.forward));
            yield return Hold(player, true, 0.3f);
            var shooter = new GameObject("Shooter");
            _made.Add(shooter);
            ArcaneOrbProjectile.Spawn(new Vector3(0.0f, 1.5f, 8.0f), Vector3.back * ArcaneOrbRules.BallSpeed, 3, 10.0f, 15.0f, 0.0f, shooter, null);
            yield return Hold(player, true, 0.6f);
            Assert.Less(caster.Health, caster.MaxHealth, "the ball did not strike the wall");
            Assert.IsNull(CursedOrbs.Find(player.transform, shooter), "the ball left orbs on whoever stood behind the wall");
        }
    }
}
