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
    /// A bullet the guard turns aside flies off (owner, 2026-10-27: "like in lego star wars when you hold block as a jedi it reflects all
    /// bullets ... into another direction ... not if its going to hit an ally"; docs/BULLET_DEFLECTION.md), on the real player body: it hits
    /// an enemy with the damage it carried; it never hits anyone who is not one (nothing at all if that is all there is); a failed roll on a
    /// held sword guard lets through a quarter and costs no stamina; a parry's instant turns a bullet aside for certain.
    /// </summary>
    public sealed class BulletRicochetPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        /// <summary>A wall that is someone: an enemy (or not), counting what hits it.</summary>
        private class Target : MonoBehaviour, IDamageable
        {
            public float Taken;
            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Taken += amount;
                return HitResponse.Landed;
            }
        }

        private sealed class HostileTarget : Target, IHostile
        {
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private GameObject Box(Vector3 centre, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = centre;
            box.transform.localScale = size;
            _made.Add(box);
            return box;
        }

        private (PlayerVitals vitals, BulletRicochet ricochet) Defender()
        {
#if UNITY_EDITOR
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(200.0f, 1.0f, 200.0f));
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            BulletRicochet ricochet = player.GetComponent<BulletRicochet>() ?? player.AddComponent<BulletRicochet>();
            Physics.SyncTransforms();
            return (player.GetComponent<PlayerVitals>(), ricochet);
#else
            return (null, null);
#endif
        }

        /// <summary>Where the shots come from: 12 m in front of the defender, at a gun's height.</summary>
        private static readonly Vector3 Shooter = new Vector3(0.0f, 2.2f, 12.0f);

        /// <summary>A wall of <typeparamref name="T"/> across the whole of the bullet's way back, near enough that even the widest, lowest way meets it before the street.</summary>
        private T WallAcrossTheWayBack<T>() where T : Target
        {
            GameObject wall = Box(new Vector3(0.0f, 15.0f, 2.5f), new Vector3(100.0f, 30.0f, 0.5f));
            Physics.SyncTransforms();
            return wall.AddComponent<T>();
        }

        [UnityTest]
        public IEnumerator ATurnedAsideBulletFliesOffAndHitsAnEnemyWithTheDamageItCarried()
        {
            (PlayerVitals vitals, BulletRicochet ricochet) = Defender();
            yield return null;
            HostileTarget enemy = WallAcrossTheWayBack<HostileTarget>();
            vitals.Roll = () => 0.0f;   // the roll always wins
            vitals.StartBlock();
            float before = vitals.Health;
            DamageContext.Strike(vitals, 20.0f, Shooter, 0.0f, DamageKind.Bullet, 0.0f);
            Assert.AreEqual(before, vitals.Health, 0.01f, "the turned-aside bullet hurt the defender");
            Assert.AreEqual(1, ricochet.Ricochets, "the bullet did not fly off");
            Assert.AreEqual(20.0f, enemy.Taken, 0.01f, "the enemy in its way did not take the bullet's damage");
            Assert.Greater(ricochet.LastDirection.z, 0.0f, "it did not fly back the way it came");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ItNeverHitsAnyoneWhoIsNotAnEnemyAndWithNoOtherWayItOnlyGlancesOff()
        {
            (PlayerVitals vitals, BulletRicochet ricochet) = Defender();
            yield return null;
            Target bystander = WallAcrossTheWayBack<Target>();   // a civilian, an ally: anyone who is not an enemy
            vitals.Roll = () => 0.0f;
            for (int i = 0; i < 20; i++)
            {
                vitals.StartBlock();
                DamageContext.Strike(vitals, 20.0f, Shooter, 0.0f, DamageKind.Bullet, 0.0f);
            }
            Assert.AreEqual(0.0f, bystander.Taken, "a turned-aside bullet hit someone who is not an enemy");
            Assert.AreEqual(0, ricochet.Ricochets);
            Assert.AreEqual(20, ricochet.Dropped, "with no safe way the bullets should only have glanced off");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnotherPlayerIsNeverHitButWhoeverFiredIt()
        {
#if UNITY_EDITOR
            (PlayerVitals vitals, BulletRicochet ricochet) = Defender();
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject ally = Object.Instantiate(prefab, new Vector3(4.0f, 0.05f, 6.0f), Quaternion.identity);
            GameObject shooter = Object.Instantiate(prefab, new Vector3(Shooter.x, 0.05f, Shooter.z), Quaternion.identity);
            _made.Add(ally);
            _made.Add(shooter);
            yield return null;
            Physics.SyncTransforms();
            vitals.Roll = () => 0.0f;
            vitals.StartBlock();
            DamageContext.Strike(vitals, 20.0f, Shooter, 0.0f, DamageKind.Bullet, 0.0f);   // sets where it came from
            Assert.IsFalse(ricochet.MayHit(ally.GetComponent<CharacterController>()), "another player, an ally, may be hit");
            Assert.IsTrue(ricochet.MayHit(shooter.GetComponent<CharacterController>()), "whoever fired it may not be hit");
            Assert.IsFalse(ricochet.MayHit(vitals.GetComponent<CharacterController>()), "the defender may hit itself");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AFailedRollOnAHeldSwordGuardLetsAQuarterThroughAndCostsNoStamina()
        {
            (PlayerVitals vitals, BulletRicochet ricochet) = Defender();
            yield return null;
            vitals.Roll = () => 1.0f;   // the roll always fails
            vitals.BlockDrainsStamina = true;   // the soulslike guard, held past its parry
            vitals.StartBlock();
            float health = vitals.Health, stamina = vitals.Stamina;
            DamageContext.Strike(vitals, 20.0f, Shooter, 0.0f, DamageKind.Bullet, 0.0f);
            Assert.AreEqual(health - 20.0f * CombatTuning.BlockDamageMultiplier, vitals.Health, 0.01f, "not a quarter of the bullet came through");
            Assert.AreEqual(stamina, vitals.Stamina, 0.01f, "the bullet cost the guard stamina");
            Assert.AreEqual(0, ricochet.Ricochets, "a bullet that came through the guard flew off as well");
        }

        [UnityTest]
        public IEnumerator AParrysInstantTurnsABulletAsideForCertain()
        {
            (PlayerVitals vitals, BulletRicochet ricochet) = Defender();
            yield return null;
            HostileTarget enemy = WallAcrossTheWayBack<HostileTarget>();
            vitals.Roll = () => 1.0f;   // the roll would fail: the parry does not roll
            vitals.BlockDrainsStamina = true;
            vitals.StartBlock();
            vitals.BeginPerfectGuard(0.2f);
            float health = vitals.Health;
            DamageContext.Strike(vitals, 20.0f, Shooter, 0.0f, DamageKind.Bullet, 0.0f);
            Assert.AreEqual(health, vitals.Health, 0.01f, "the parried bullet hurt");
            Assert.AreEqual(1, ricochet.Ricochets, "the parried bullet did not fly off");
            Assert.AreEqual(20.0f, enemy.Taken, 0.01f);
        }
    }
}
