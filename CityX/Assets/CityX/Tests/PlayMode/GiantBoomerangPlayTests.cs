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
    /// The Giant Boomerang on a real body (docs/BOOMERANG.md; owner, 2026-11-12): carried on the back; right-click draws it into the aim and letting go puts it
    /// back; a click throws it from the aim, or straight off the back; it goes through people on its way out, knocking them down, and a wall turns it back;
    /// it comes home onto the back; and another player's copy draws, aims and throws it too.
    /// </summary>
    public sealed class GiantBoomerangPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        private sealed class Dummy : MonoBehaviour, IDamageable, ILaunchable
        {
            public int Hits;
            public int Launches;
            public float HitAt = -1.0f;

            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Hits++;
                if (HitAt < 0.0f) HitAt = Time.time;
                return HitResponse.Landed;
            }

            public void Launch(Vector3 velocity)
            {
                Launches++;
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

        private GameObject Body(Vector3 at)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
                _made.Add(ground);
            }
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var thrower = player.AddComponent<BoomerangThrower>();
            thrower.GiantThrownPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/ThrownGiantBoomerang.prefab");
            Assert.IsNotNull(thrower.GiantThrownPrefab, "no ThrownGiantBoomerang.prefab -- rebuild the scene");
            return player;
#else
            return null;
#endif
        }

        private GiantBoomerangItem Equip(GameObject player)
        {
#if UNITY_EDITOR
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/GiantBoomerang.prefab"));
            Assert.IsNotNull(model, "no GiantBoomerang.prefab -- rebuild the scene");
            _made.Add(model);
            var item = model.GetComponent<GiantBoomerangItem>();
            item.OnEquip(player, ItemInstanceData.None);
            return item;
#else
            return null;
#endif
        }

        private Dummy Target(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = at;
            go.transform.localScale = new Vector3(1.0f, 1.3f, 1.0f);
            Physics.SyncTransforms();
            _made.Add(go);
            return go.AddComponent<Dummy>();
        }

        private static Transform Bone(GameObject player, HumanBodyBones bone) => player.GetComponentInChildren<Animator>().GetBoneTransform(bone);

        private static IEnumerator Wait(float seconds, System.Func<bool> until = null)
        {
            for (float t = 0.0f; t < seconds && (until == null || !until()); t += Time.deltaTime)
            {
                yield return null;
            }
        }

        private static bool Showing(Component item)
        {
            foreach (Renderer part in item.GetComponentsInChildren<Renderer>(true))
            {
                if (part.enabled) return true;
            }
            return false;
        }

        [UnityTest]
        public IEnumerator ItRidesOnTheBackRightClickDrawsItIntoTheHandAndLettingGoPutsItBack()
        {
            GameObject player = Body(Vector3.zero);
            GiantBoomerangItem item = Equip(player);
            var thrower = player.GetComponent<BoomerangThrower>();
            yield return null;
            Assert.AreEqual(Bone(player, HumanBodyBones.Spine), item.transform.parent, "it is not carried on the back");
            Assert.Less(item.transform.position.z, player.transform.position.z, "it is not behind the body");

            thrower.SetGiantAiming(true);
            yield return Wait(0.6f);
            Assert.AreEqual(GiantBoomerangPoseDriver.Move.Aiming, thrower.GiantMoves.Current, "it was not drawn into the aim");
            Assert.AreEqual(Bone(player, HumanBodyBones.RightHand), item.transform.parent, "drawn into the aim, it is not in the hand");
            Assert.Greater(player.GetComponent<PlayerLocomotion>().BakedPoseWeight, 0.99f, "the aim is not on the body");

            thrower.SetGiantAiming(false);
            yield return Wait(0.6f);
            Assert.AreEqual(GiantBoomerangPoseDriver.Move.None, thrower.GiantMoves.Current);
            Assert.AreEqual(Bone(player, HumanBodyBones.Spine), item.transform.parent, "let go without throwing, it did not go back on the back");
            Assert.IsTrue(Showing(item));
        }

        [UnityTest]
        public IEnumerator AClickThrowsItStraightOffTheBackAndItComesHomeOntoIt()
        {
            GameObject player = Body(Vector3.zero);
            GiantBoomerangItem item = Equip(player);
            var thrower = player.GetComponent<BoomerangThrower>();
            yield return null;
            item.Throw(player, player.transform.position + Vector3.up * 1.8f, Vector3.forward);
            Assert.AreEqual(GiantBoomerangPoseDriver.Move.ThrowingQuick, thrower.GiantMoves.Current, "a click off the back is not the quick throw");
            Assert.IsFalse(thrower.IsOut, "it left before the arm brought it round");
            float started = Time.time;
            yield return Wait(1.0f, () => thrower.IsOut);
            Assert.IsTrue(thrower.IsOut, "it was never thrown");
            Assert.AreEqual(GiantBoomerangMoves.QuickReleaseSeconds, Time.time - started, 0.1f, "it did not leave the hand when the throw lets it go");
            Assert.IsTrue(thrower.Flying.Profile.Heavy, "what flew is not the giant one");
            Assert.IsFalse(Showing(item), "it is still shown on the body while it flies");

            yield return Wait(6.0f, () => !thrower.IsOut);
            Assert.IsFalse(thrower.IsOut, "it never came home");
            yield return Wait(0.3f);
            Assert.IsTrue(Showing(item), "home, it is not shown");
            Assert.AreEqual(Bone(player, HumanBodyBones.Spine), item.transform.parent, "home, it is not back on the back");
        }

        [UnityTest]
        public IEnumerator FromTheAimItLeavesSoonerThanOffTheBack()
        {
            GameObject player = Body(Vector3.zero);
            GiantBoomerangItem item = Equip(player);
            var thrower = player.GetComponent<BoomerangThrower>();
            thrower.SetGiantAiming(true);
            yield return Wait(0.6f);
            item.Throw(player, player.transform.position + Vector3.up * 1.8f, Vector3.forward);
            Assert.AreEqual(GiantBoomerangPoseDriver.Move.ThrowingAimed, thrower.GiantMoves.Current, "a click from the aim is not the aimed throw");
            float started = Time.time;
            yield return Wait(1.0f, () => thrower.IsOut);
            Assert.IsTrue(thrower.IsOut);
            Assert.Less(Time.time - started, GiantBoomerangMoves.QuickReleaseSeconds, "from the aim it was no quicker to leave the hand");
        }

        [UnityTest]
        public IEnumerator GoingOutItCutsThroughPeopleKnockingThemDownAndAWallTurnsItBack()
        {
            GameObject player = Body(Vector3.zero);
            var thrower = player.GetComponent<BoomerangThrower>();
            Dummy near = Target(new Vector3(0.0f, 1.3f, 6.0f));
            Dummy far = Target(new Vector3(0.4f, 1.3f, 11.0f));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 3.0f, 16.0f);
            wall.transform.localScale = new Vector3(10.0f, 6.0f, 0.4f);
            _made.Add(wall);
            Dummy behind = Target(new Vector3(0.0f, 1.3f, 18.0f));
            Physics.SyncTransforms();
            yield return null;
            Assert.IsTrue(thrower.StartGiantThrow(Vector3.forward, 30.0f, 0.0f));
            yield return Wait(1.0f, () => thrower.IsOut);
            ThrownBoomerang flying = thrower.Flying;
            float furthest = 0.0f;
            bool turnedAtTheWall = false;
            for (float t = 0.0f; t < 6.0f && thrower.IsOut; t += Time.deltaTime)
            {
                furthest = Mathf.Max(furthest, flying.transform.position.z);
                if (flying.Returning && !turnedAtTheWall)
                {
                    turnedAtTheWall = true;
                    Assert.AreEqual(1, near.Hits, "the first one in its way was not hit on the way out");
                    Assert.AreEqual(1, far.Hits, "the one behind them was not hit on the way out: it stopped at the first");
                }
                yield return null;
            }
            Assert.Less(furthest, 16.0f, "it went through the wall");
            Assert.Greater(furthest, 14.0f, "it turned back before the wall");
            Assert.AreEqual(0, behind.Hits, "it hit someone behind the wall");
            Assert.Greater(near.Launches, 0, "a hit did not knock the body down");
            Assert.Greater(far.Launches, 0);
        }

        /// <summary>Another player's copy of this body draws, aims and throws it too, and what flies there is the giant one, hurting no one.</summary>
        [UnityTest]
        public IEnumerator AnotherPlayersCopyDrawsAimsAndThrowsItToo()
        {
#if UNITY_EDITOR
            var networked = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab").GetComponent<BoomerangThrower>();
            Assert.IsTrue(networked != null && networked.GiantThrownPrefab != null, "the networked player cannot show another player's giant boomerang -- rebuild the scene");
            GameObject owner = Body(Vector3.zero);
            GameObject copyBody = Body(new Vector3(8.0f, 0.0f, 0.0f));
            Equip(owner);
            GiantBoomerangItem copyItem = Equip(copyBody);
            var mine = owner.GetComponent<BoomerangThrower>();
            var copy = copyBody.GetComponent<BoomerangThrower>();
            mine.Fired += e => copy.PlayRemote(new AbilityEvent(e.Kind, e.Origin + new Vector3(8.0f, 0.0f, 0.0f), e.Direction, e.A, e.B, e.N));
            yield return null;

            mine.SetGiantAiming(true);
            yield return Wait(0.6f);
            Assert.AreEqual(GiantBoomerangPoseDriver.Move.Aiming, copy.GiantMoves.Current, "the copy did not draw it into the aim");
            Assert.AreEqual(Bone(copyBody, HumanBodyBones.RightHand), copyItem.transform.parent, "the copy's is not in its hand");

            mine.StartGiantThrow(Vector3.forward, 30.0f, 0.0f);
            yield return null;
            Assert.IsTrue(copy.GiantMoves.IsThrowing, "the copy did not throw");
            yield return Wait(1.0f, () => copy.IsOut);
            Assert.IsTrue(copy.IsOut, "nothing flew from the copy");
            Assert.IsTrue(copy.Flying.IsCopy && copy.Flying.Profile.Heavy, "the copy's throw is not the giant one, or it can hurt");
#else
            yield break;
#endif
        }
    }
}
