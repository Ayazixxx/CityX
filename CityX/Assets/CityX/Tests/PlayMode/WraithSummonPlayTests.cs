using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The Wraith Staff's summon on a real player body (owner, 2026-11-08: "it summons 1 wraith that follows you around and fights for you,
    /// when he dies you can summon another", then "have the enemies view the summon as an opponent as well"): one at a time, on your side; it
    /// keeps up with you; it hunts an enemy near you and the enemy fights it; it goes when you do; a wild Wraith can leave the staff.
    /// </summary>
    public sealed class WraithSummonPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private PlayerVitals _player;
        private WraithSummonCaster _caster;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(300.0f, 1.0f, 300.0f);
            _made.Add(ground);
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _made.Add(body);
            body.GetComponent<PlayerInputReader>().enabled = false;
            _player = body.GetComponent<PlayerVitals>();
            _player.Revive();
            _caster = body.AddComponent<WraithSummonCaster>();
            _caster.WraithPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Wraith.prefab");
            Physics.SyncTransforms();
#else
            Assert.Ignore("needs the editor to load the prefabs.");
#endif
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            _made.Clear();
            foreach (Wraith wraith in new List<Wraith>(Wraith.All))
            {
                Object.Destroy(wraith.gameObject);
            }
            foreach (DroppedItem drop in Object.FindObjectsByType<DroppedItem>(FindObjectsSortMode.None))
            {
                Object.Destroy(drop.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ItSummonsOneWraithOnYourSideAndAnotherOnceItIsGone()
        {
            float mana = _player.Mana;
            Assert.IsTrue(_caster.TrySummon(Vector3.forward), "the staff summoned nothing");
            Assert.AreEqual(mana - WraithSummonCaster.ManaCost, _player.Mana, 0.5f, "the summon cost no mana");
            Wraith summon = _caster.Current;
            Assert.IsNotNull(summon, "no Wraith of yours came");
            Assert.IsTrue(summon.IsSummoned && summon.Master == _player, "it is not on your side");
            Assert.IsTrue(Opponents.IsSummon(summon.gameObject), "the enemies do not know it for an opponent");
            Assert.IsFalse(_caster.TrySummon(Vector3.forward), "a second one came while the first was about");

            ((IDamageable)summon).ApplyHit(Wraith.SummonedMaxHealth + 1.0f, summon.transform.position, 0.0f);
            Assert.IsFalse(summon.IsAlive);
            Assert.IsNull(_caster.Current, "a dead summon still counts as about");
            _player.Revive();   // the mana for another
            Assert.IsTrue(_caster.TrySummon(Vector3.forward), "once it was gone, another would not come");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ItKeepsUpWithItsSummoner()
        {
            Assert.IsTrue(_caster.TrySummon(Vector3.forward));
            Wraith summon = _caster.Current;
            _player.transform.position = new Vector3(14.0f, 0.05f, 6.0f);
            for (float t = 0.0f; t < 4.0f; t += Time.deltaTime)
            {
                yield return null;
            }
            float apart = Flat(summon.transform.position - _player.transform.position).magnitude;
            Assert.Less(apart, 4.0f, "it did not keep up with its summoner");
        }

        /// <summary>It goes for the enemy near you and bites it; the enemy, finding it nearer than you, fights it back.</summary>
        [UnityTest]
        public IEnumerator ItFightsAnEnemyNearYouAndTheEnemyFightsIt()
        {
#if UNITY_EDITOR
            GameObject clawObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/CrimsonClaw.prefab"),
                new Vector3(0.0f, CrimsonClaw.HoverHeightAboveGround, 9.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            _made.Add(clawObject);
            var claw = clawObject.GetComponent<CrimsonClaw>();
            _player.transform.position = new Vector3(0.0f, 0.05f, -12.0f);   // out of the Claw's sight: only the summon is near it
            Physics.SyncTransforms();
            Assert.IsTrue(_caster.TrySummon(Vector3.forward));
            Wraith summon = _caster.Current;
            summon.transform.position = new Vector3(0.0f, summon.transform.position.y, 4.0f);
            _player.transform.position = new Vector3(0.0f, 0.05f, 1.0f);   // near enough that the Claw is an enemy to guard against
            for (float t = 0.0f; t < 20.0f && (claw.Health >= CrimsonClaw.MaxHealth || summon.Health >= Wraith.SummonedMaxHealth); t += Time.deltaTime)
            {
                _player.transform.position = new Vector3(0.0f, 0.05f, 1.0f);
                yield return null;
            }
            Assert.Less(claw.Health, CrimsonClaw.MaxHealth, "the summon never bit the Claw");
            Assert.Less(summon.Health, Wraith.SummonedMaxHealth, "the Claw never fought the summon");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ItGoesWhenItsSummonerDies()
        {
            Assert.IsTrue(_caster.TrySummon(Vector3.forward));
            Wraith summon = _caster.Current;
            _player.TakeDamage(_player.Health + 10.0f);
            yield return null;
            yield return null;
            Assert.IsFalse(summon.IsAlive, "the summon stayed after its summoner died");
            Assert.IsFalse(Opponents.IsSummon(summon.gameObject));
        }

        [UnityTest]
        public IEnumerator AWildWraithCanLeaveTheStaff()
        {
#if UNITY_EDITOR
            GameObject wild = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Wraith.prefab"),
                new Vector3(5.0f, 1.0f, 5.0f), Quaternion.identity);
            _made.Add(wild);
            yield return null;
            wild.GetComponent<Wraith>().DropStaff();
            yield return null;
            bool found = false;
            foreach (DroppedItem drop in Object.FindObjectsByType<DroppedItem>(FindObjectsSortMode.None))
            {
                found |= drop.ItemId == ItemRegistry.WraithStaff;
            }
            Assert.IsTrue(found, "no Wraith Staff fell out of it");
#else
            yield break;
#endif
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);
    }
}
