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
    /// <summary>The Wooden Boomerang on a real body (docs/BOOMERANG.md), with Terraria's rules.</summary>
    public sealed class BoomerangPlayTests
    {
        private const string HeldPath = "Assets/CityX/Weapons/Boomerang.prefab";
        private readonly List<GameObject> _made = new List<GameObject>();

        private sealed class Dummy : MonoBehaviour, IDamageable
        {
            public float Taken;
            public int Hits;
            public Vector3 From;

            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Taken += amount;
                Hits++;
                From = fromPosition;
                return HitResponse.Landed;
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            foreach (ThrownBoomerang flying in Object.FindObjectsByType<ThrownBoomerang>(FindObjectsSortMode.None)) Object.Destroy(flying.gameObject);
        }

        private GameObject Body(Vector3 at, float yawDegrees = 0.0f)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
                _made.Add(ground);
            }
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.Euler(0.0f, yawDegrees, 0.0f));
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.AddComponent<BoomerangThrower>().ThrownPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/ThrownBoomerang.prefab");
            return player;
#else
            return null;
#endif
        }

        private BoomerangItem Held()
        {
#if UNITY_EDITOR
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HeldPath));
            Assert.IsNotNull(model, "no Boomerang.prefab -- rebuild the scene");
            _made.Add(model);
            return model.GetComponent<BoomerangItem>();
#else
            return null;
#endif
        }

        private Dummy Target(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = at;
            Physics.SyncTransforms();
            _made.Add(go);
            return go.AddComponent<Dummy>();
        }

        private GameObject Wall(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = at;
            go.transform.localScale = new Vector3(8.0f, 6.0f, 0.4f);
            Physics.SyncTransforms();
            _made.Add(go);
            return go;
        }

        private static Vector3 Hand(GameObject player) => player.transform.position + Vector3.up * 1.6f;

        private static void Throw(GameObject player, BoomerangItem item) => item.Throw(player, Hand(player), player.transform.forward);

        private static IEnumerator UntilHome(BoomerangThrower thrower, float seconds = 4.0f)
        {
            for (float t = 0.0f; t < seconds && thrower.IsOut; t += Time.deltaTime)
            {
                yield return null;
            }
            Assert.IsFalse(thrower.IsOut, "it never came home");
        }

        private static bool Showing(BoomerangItem item)
        {
            foreach (Renderer part in item.GetComponentsInChildren<Renderer>(true))
            {
                if (part.enabled) return true;
            }
            return false;
        }

        [Test]
        public void TheBodyOtherPlayersJoinWithCanThrowItAndHoldIt()
        {
#if UNITY_EDITOR
            GameObject networked = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            var thrower = networked.GetComponent<BoomerangThrower>();
            Assert.IsNotNull(thrower, "no BoomerangThrower on NetworkedPlayer.prefab -- rebuild the scene");
            Assert.IsNotNull(thrower.ThrownPrefab, "its thrower has nothing to throw");
            Assert.IsNotNull(thrower.ThrownPrefab.GetComponent<ThrownBoomerang>());
#else
            Assert.Ignore("the prefab can only be loaded by path inside the editor");
#endif
        }

        [UnityTest]
        public IEnumerator ThrownItLeavesTheHandGoesOutAndComesBackToIt()
        {
            GameObject player = Body(Vector3.zero);
            BoomerangItem item = Held();
            item.OnEquip(player, ItemInstanceData.None);
            var thrower = player.GetComponent<BoomerangThrower>();
            Assert.IsTrue(Showing(item), "nothing in the hand");
            Time.captureFramerate = 60;

            Throw(player, item);
            Assert.IsTrue(thrower.IsOut, "the click threw nothing");
            Assert.IsFalse(Showing(item), "it is in the hand and in the air");
            Assert.IsTrue(player.GetComponent<PlayerLocomotion>().IsThrowSwinging, "the arm did not swing");

            float furthest = 0.0f;
            for (float t = 0.0f; t < 4.0f && thrower.IsOut; t += Time.deltaTime)
            {
                furthest = Mathf.Max(furthest, thrower.Flying.transform.position.z);
                yield return null;
            }
            Assert.IsFalse(thrower.IsOut, "it never came home");
            Assert.AreEqual(BoomerangRules.Range, furthest, 1.0f, "it did not go its range");
            Assert.IsTrue(Showing(item), "it came back and the hand stays empty");
            Assert.IsNotNull(item, "the boomerang was spent: it is not a grenade");
        }

        [UnityTest]
        public IEnumerator AHitGoingOutTurnsItBackAndItCanHitAgainComingHome()
        {
            GameObject player = Body(Vector3.zero);
            BoomerangItem item = Held();
            item.OnEquip(player, ItemInstanceData.None);
            var thrower = player.GetComponent<BoomerangThrower>();
            Dummy near = Target(new Vector3(0.0f, 1.6f, 5.0f));
            Dummy beyond = Target(new Vector3(0.0f, 1.6f, 9.0f));
            Time.captureFramerate = 60;

            Throw(player, item);
            ThrownBoomerang flying = thrower.Flying;
            yield return new WaitUntil(() => near.Hits > 0 || flying == null);
            Assert.AreEqual(1, near.Hits, "it did not hit the first body in its way");
            Assert.AreEqual(BoomerangRules.Damage, near.Taken, 1.0f, "not its damage");
            yield return null;
            Assert.IsTrue(flying.Returning, "the hit did not turn it back");
            float turnedAt = flying.transform.position.z;
            Assert.Less(turnedAt, 7.0f, "it flew on past what it hit");

            yield return UntilHome(thrower);
            Assert.AreEqual(0, beyond.Hits, "it reached the body behind the first");
            Assert.Less(Vector3.Distance(player.transform.position, near.From), 0.5f, "the hit is not from the thrower");
        }

        [UnityTest]
        public IEnumerator AWallTurnsItBackGoingOutButNotComingHome()
        {
            GameObject player = Body(Vector3.zero);
            BoomerangItem item = Held();
            item.OnEquip(player, ItemInstanceData.None);
            var thrower = player.GetComponent<BoomerangThrower>();
            Wall(new Vector3(0.0f, 2.0f, 5.0f));
            Dummy behind = Target(new Vector3(0.0f, 1.6f, 7.0f));
            Time.captureFramerate = 60;

            Throw(player, item);
            ThrownBoomerang flying = thrower.Flying;
            yield return new WaitUntil(() => flying == null || flying.Returning);
            Assert.IsNotNull(flying, "it came home before anything turned it");
            Assert.Less(flying.transform.position.z, 5.0f, "it went through the wall on its way out");
            yield return UntilHome(thrower);
            Assert.AreEqual(0, behind.Hits, "it hit someone behind the wall");

            // Coming home: past a turned-back body and a wall between it and the thrower, it goes through and hits.
            yield return new WaitForSeconds(BoomerangRules.ThrowEverySeconds);
            foreach (GameObject go in _made.ToArray())
            {
                if (go != null && go.GetComponent<BoxCollider>() != null && go.transform.position.z > 4.0f) Object.Destroy(go);
            }
            yield return null;
            Physics.SyncTransforms();
            Throw(player, item);
            flying = thrower.Flying;
            yield return new WaitUntil(() => flying == null || flying.Returning);
            Assert.IsNotNull(flying);
            Wall(new Vector3(0.0f, 2.0f, 4.0f));
            Dummy onTheWay = Target(new Vector3(0.0f, 1.6f, 2.5f));
            yield return UntilHome(thrower);
            Assert.AreEqual(1, onTheWay.Hits, "it did not come through the wall and hit who it passed");
            Assert.Less(Vector3.Distance(player.transform.position, onTheWay.From), 0.5f, "a hit coming home does not push away from the thrower");
        }

        /// <summary>
        /// Owner, 2026-11-17: "when thrown against a wall or the floor or any type of object there is some sort of bang noise and some impact fx".
        /// Into a wall, the bang and burst are on the wall's face, facing back at the thrower; thrown down at the floor, on the floor, facing up.
        /// Another player's copy, told where by the owner, bursts at the same place facing the same way.
        /// </summary>
        [UnityTest]
        public IEnumerator AWallOrTheFloorItStrikesBangsAndBurstsWhereItStruckOnEveryScreen()
        {
            GameObject player = Body(Vector3.zero);
            GameObject copy = Body(new Vector3(30.0f, 0.0f, 0.0f));
            BoomerangItem item = Held();
            item.OnEquip(player, ItemInstanceData.None);
            var thrower = player.GetComponent<BoomerangThrower>();
            var copyThrower = copy.GetComponent<BoomerangThrower>();
            var told = new List<AbilityEvent>();
            thrower.Fired += told.Add;
            Wall(new Vector3(0.0f, 2.0f, 5.0f));
            Time.captureFramerate = 60;

            int before = ImpactBurst.Shown;
            Throw(player, item);
            copyThrower.PlayRemote(new AbilityEvent(BoomerangThrower.Thrown, Hand(copy), copy.transform.forward, BoomerangRules.OutSpeed));
            ThrownBoomerang flying = thrower.Flying;
            yield return new WaitUntil(() => flying == null || flying.Returning);
            Assert.AreEqual(before + 1, ImpactBurst.Shown, "the wall it struck shows no burst");
            Assert.AreEqual(5.0f - 0.2f, ImpactBurst.LastPoint.z, 0.15f, "the burst is not on the wall's face");
            Assert.Greater(Vector3.Dot(ImpactBurst.LastNormal, Vector3.back), 0.9f, "the burst does not face back off the wall");

            AbilityEvent turned = told.Find(e => e.Kind == BoomerangThrower.TurnedBack);
            Assert.AreEqual(0.0f, turned.A, "the wall was told as a person");
            Vector3 wallPoint = ImpactBurst.LastPoint;
            copyThrower.PlayRemote(turned);
            Assert.AreEqual(before + 2, ImpactBurst.Shown, "another player's copy shows no burst");
            Assert.Less(Vector3.Distance(wallPoint, ImpactBurst.LastPoint), 0.01f, "the copy's burst is not where it struck");
            Assert.Greater(Vector3.Dot(ImpactBurst.LastNormal, Vector3.back), 0.9f, "the copy's burst does not face back off the wall");
            copyThrower.PlayRemote(new AbilityEvent(BoomerangThrower.Caught, Vector3.zero, Vector3.zero));
            yield return UntilHome(thrower);

            // Down at the floor.
            yield return new WaitForSeconds(BoomerangRules.ThrowEverySeconds);
            item.Throw(player, Hand(player), (player.transform.forward + Vector3.down * 1.2f).normalized);
            flying = thrower.Flying;
            yield return new WaitUntil(() => flying == null || flying.Returning);
            Assert.AreEqual(before + 3, ImpactBurst.Shown, "the floor it struck shows no burst");
            Assert.AreEqual(0.0f, ImpactBurst.LastPoint.y, 0.15f, "the burst is not on the floor");
            Assert.Greater(ImpactBurst.LastNormal.y, 0.9f, "the burst on the floor does not face up");
            thrower.Fired -= told.Add;
        }

        [Test]
        public void TheBangCracksAtOnceAndIsGoneInAThirdOfASecond()
        {
            float[] bang = BoomerangSounds.BangSamples();
            Assert.LessOrEqual(bang.Length / 44100.0f, 0.35f, "the bang rings on");
            float loudest = 0.0f, loudestAt = 0.0f;
            for (int i = 0; i < bang.Length; i++)
            {
                if (Mathf.Abs(bang[i]) > loudest)
                {
                    loudest = Mathf.Abs(bang[i]);
                    loudestAt = i / 44100.0f;
                }
            }
            Assert.Greater(loudest, 0.5f, "the bang is quiet");
            Assert.Less(loudestAt, 0.03f, "the bang does not hit at once");
            float tail = 0.0f;
            for (int i = bang.Length - 2205; i < bang.Length; i++) tail = Mathf.Max(tail, Mathf.Abs(bang[i]));
            Assert.Less(tail, 0.05f, "the bang has not died away by its end (it would click off)");
        }

        [Test]
        public void TheBurstsChipsFlyOutOfTheSurfaceWithinItsCone()
        {
            foreach (Vector3 normal in new[] { Vector3.up, Vector3.back, new Vector3(1.0f, 0.3f, 0.2f).normalized })
            {
                for (int i = 0; i <= 10; i++)
                {
                    for (int j = 0; j <= 10; j++)
                    {
                        Vector3 way = ImpactBurst.Scatter(normal, ImpactBurst.SpreadDegrees, i / 10.0f, j / 10.0f);
                        Assert.AreEqual(1.0f, way.magnitude, 1e-4f);
                        Assert.LessOrEqual(Vector3.Angle(normal, way), ImpactBurst.SpreadDegrees + 0.01f, "a chip flies outside the cone");
                    }
                }
                Assert.Less(Vector3.Angle(normal, ImpactBurst.Scatter(normal, ImpactBurst.SpreadDegrees, 0.3f, 0.0f)), 0.01f, "the cone's middle is not straight out");
            }
        }

        [UnityTest]
        public IEnumerator OnlyOneIsOutAtATime()
        {
            GameObject player = Body(Vector3.zero);
            BoomerangItem item = Held();
            item.OnEquip(player, ItemInstanceData.None);
            var thrower = player.GetComponent<BoomerangThrower>();

            Throw(player, item);
            ThrownBoomerang first = thrower.Flying;
            yield return null;
            Throw(player, item);
            Assert.AreEqual(first, thrower.Flying, "a second throw went while one was out");
            Assert.AreEqual(1, Object.FindObjectsByType<ThrownBoomerang>(FindObjectsSortMode.None).Length, "two in the air");
        }

        [UnityTest]
        public IEnumerator PuttingItAwayWhileItIsOutDoesNotLetASecondGo()
        {
            GameObject player = Body(Vector3.zero);
            BoomerangItem item = Held();
            item.OnEquip(player, ItemInstanceData.None);
            var thrower = player.GetComponent<BoomerangThrower>();

            Throw(player, item);
            item.OnUnequip(player);
            yield return null;
            BoomerangItem again = Held();
            again.OnEquip(player, ItemInstanceData.None);
            Assert.IsFalse(Showing(again), "taken out again while it is still in the air, it is in the hand too");
            Throw(player, again);
            Assert.AreEqual(1, Object.FindObjectsByType<ThrownBoomerang>(FindObjectsSortMode.None).Length, "a second went");

            yield return UntilHome(thrower);
            Assert.IsTrue(Showing(again), "it came home to an empty hand");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyFliesItsOwnHarmlessOneAndItsHandEmptiesAndFills()
        {
            GameObject shooter = Body(Vector3.zero);
            GameObject copy = Body(new Vector3(30.0f, 0.0f, 0.0f));
            BoomerangItem item = Held();
            item.OnEquip(shooter, ItemInstanceData.None);
            BoomerangItem copyItem = Held();
            copyItem.MountCosmetic(copy);
            var thrower = shooter.GetComponent<BoomerangThrower>();
            var copyThrower = copy.GetComponent<BoomerangThrower>();
            thrower.Fired += copyThrower.PlayRemote;
            Dummy nearCopy = Target(new Vector3(30.0f, 1.6f, 5.0f));

            // Sent from the copy's own place, as a real copy stands where its owner does: the copy's flight must not hurt.
            copyThrower.PlayRemote(new AbilityEvent(BoomerangThrower.Thrown, Hand(copy), copy.transform.forward, BoomerangRules.OutSpeed));
            Assert.IsTrue(copyThrower.IsOut, "the copy's boomerang did not fly");
            Assert.IsTrue(copyThrower.Flying.IsCopy);
            Assert.IsFalse(Showing(copyItem), "the copy's hand still holds it");
            Assert.IsTrue(copy.GetComponent<PlayerLocomotion>().IsThrowSwinging, "the copy's arm did not swing");
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(0, nearCopy.Hits, "a copy's boomerang hurt someone");

            yield return UntilHome(copyThrower);
            Assert.IsTrue(Showing(copyItem), "the copy's hand stays empty");

            // The owner's word of what turned it back reaches the copy, and so does the catch.
            Throw(shooter, item);
            Assert.IsTrue(copyThrower.IsOut, "the throw did not reach the copy");
            copyThrower.PlayRemote(new AbilityEvent(BoomerangThrower.TurnedBack, Vector3.zero, Vector3.zero, 1.0f));
            Assert.IsTrue(copyThrower.Flying.Returning, "the copy did not turn back when told");
            copyThrower.PlayRemote(new AbilityEvent(BoomerangThrower.Caught, Vector3.zero, Vector3.zero));
            yield return null;
            Assert.IsFalse(copyThrower.IsOut, "the catch did not end the copy's");
            thrower.Fired -= copyThrower.PlayRemote;
        }
    }
}
