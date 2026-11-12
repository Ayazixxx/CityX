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
    /// Semtex (owner, 2026-11-11: "a semtex sticky grenade that is just the grenade that just sticks to whatever surface it touches, it sticks
    /// to players and enemies too wherever it contacts them"): it stops dead on a wall, flat against it; on a body it rides the bone nearest
    /// where it landed and goes off there; another player's copy of it is Semtex too.
    /// </summary>
    public sealed class SemtexPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            foreach (Explosion explosion in Object.FindObjectsByType<Explosion>(FindObjectsSortMode.None)) Object.Destroy(explosion.gameObject);
            foreach (ThrownGrenade grenade in Object.FindObjectsByType<ThrownGrenade>(FindObjectsSortMode.None)) Object.Destroy(grenade.gameObject);
            FireScene.Clear();
        }

        private static GameObject Load(string path)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"no {path} -- rebuild the scene");
            return prefab;
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            return null;
#endif
        }

        private GameObject Thrown() => Load("Assets/CityX/Weapons/ThrownSemtex.prefab");

        private void Ground()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _made.Add(ground);
        }

        private static IEnumerator Wait(float seconds, System.Func<bool> until = null)
        {
            for (float t = 0.0f; t < seconds && (until == null || !until()); t += Time.deltaTime)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ThrownAtAWallItStopsDeadFlatAgainstItAndStaysThere()
        {
            Ground();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 2.0f, 5.0f);
            wall.transform.localScale = new Vector3(6.0f, 4.0f, 0.5f);
            _made.Add(wall);
            Physics.SyncTransforms();

            ThrownGrenade semtex = ThrownGrenade.Spawn(Thrown(), new Vector3(0.0f, 2.0f, 0.0f), new Vector3(0.0f, 1.0f, 12.0f), null, false);
            Assert.IsTrue(semtex.Sticky, "ThrownSemtex is not sticky");
            yield return Wait(2.0f, () => semtex == null || semtex.IsStuck);
            Assert.IsTrue(semtex.IsStuck, "it never stuck to the wall");
            Vector3 at = semtex.transform.position;
            float face = 5.0f - 0.25f;
            Assert.AreEqual(face - semtex.StickOffset, at.z, 0.05f, "it is not flat against the wall's face");
            Assert.Greater(at.y, 0.5f, "it fell down the wall");
            yield return Wait(1.0f);
            Assert.Less(Vector3.Distance(at, semtex.transform.position), 0.01f, "it slid or fell off the wall");
            Assert.Greater(Vector3.Dot(semtex.transform.up, Vector3.back), 0.9f, "its flat side is not against the wall");
            yield return Wait(GrenadeRules.FuseSeconds + 1.0f, () => semtex == null);
            Assert.IsTrue(semtex == null, "it never went off");
        }

        [UnityTest]
        public IEnumerator ThrownAtABodyItRidesTheBoneItHitAndGoesOffOnIt()
        {
#if UNITY_EDITOR
            Ground();
            GameObject body = Object.Instantiate(Load("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 4.0f), Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = false;
            _made.Add(body);
            var vitals = body.GetComponent<PlayerVitals>();
            yield return null;
            Physics.SyncTransforms();
            float health = vitals.Health;
            Animator animator = body.GetComponentInChildren<Animator>();

            Vector3 chest = animator.GetBoneTransform(HumanBodyBones.Spine).position;
            Vector3 from = new Vector3(0.0f, chest.y, 0.0f);
            ThrownGrenade semtex = ThrownGrenade.Spawn(Thrown(), from, (chest - from).normalized * 12.0f + Vector3.up * 0.6f, null, false);
            yield return Wait(2.0f, () => semtex == null || semtex.IsStuck);
            Assert.IsTrue(semtex.IsStuck, "it never stuck to the body");
            Assert.IsTrue(semtex.StuckTo != null && semtex.StuckTo.IsChildOf(body.transform), "it is not stuck to the body");
            Assert.IsTrue(System.Array.Exists(new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head },
                bone => animator.GetBoneTransform(bone) == semtex.StuckTo), $"stuck on the front of the body, it rides {semtex.StuckTo.name}, not the trunk");

            // The body walks off: it goes with it.
            Vector3 offset = semtex.transform.position - body.transform.position;
            body.GetComponent<CharacterController>().Move(new Vector3(2.0f, 0.0f, 0.0f));
            yield return null;
            Assert.Less(Vector3.Distance(semtex.transform.position - body.transform.position, offset), 0.15f, "it did not go with the body");

            yield return Wait(GrenadeRules.FuseSeconds + 1.0f, () => semtex == null);
            Assert.IsTrue(semtex == null, "it never went off");
            Assert.Less(vitals.Health, health, "going off on the body, it did not hurt it");
#else
            yield break;
#endif
        }

        /// <summary>On another player's screen the throw is Semtex too, and sticks the same way.</summary>
        [UnityTest]
        public IEnumerator AnotherPlayersCopyIsSemtexToo()
        {
#if UNITY_EDITOR
            Ground();
            GameObject body = Object.Instantiate(Load("Assets/CityX/Character/Player.prefab"), new Vector3(30.0f, 0.05f, 0.0f), Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = false;
            _made.Add(body);
            var networked = Load("Assets/CityX/Character/NetworkedPlayer.prefab").GetComponent<GrenadeThrower>();
            Assert.IsTrue(networked != null && networked.StickyPrefab != null,
                "the networked player's GrenadeThrower has no Semtex for other players' throws -- rebuild the player");
            var thrower = body.GetComponent<GrenadeThrower>() ?? body.AddComponent<GrenadeThrower>();
            thrower.ThrownPrefab = networked.ThrownPrefab;
            thrower.StickyPrefab = networked.StickyPrefab;
            AbilityEvent seen = default;
            thrower.Fired += e => seen = e;
            ThrownGrenade mine = thrower.Throw(Thrown(), new Vector3(30.0f, 2.0f, 2.0f), new Vector3(0.0f, 0.0f, 8.0f));
            Assert.AreEqual(GrenadeThrower.Thrown, seen.Kind);
            Assert.AreEqual(1, seen.N, "the throw does not say it was Semtex");
            Object.Destroy(mine.gameObject);

            thrower.PlayRemote(new AbilityEvent(GrenadeThrower.Thrown, new Vector3(0.0f, 2.0f, 0.0f), Vector3.forward, 8.0f, 0.0f, 1));
            yield return null;
            bool copySticky = false;
            foreach (ThrownGrenade g in Object.FindObjectsByType<ThrownGrenade>(FindObjectsSortMode.None))
            {
                copySticky |= g.IsCopy && g.Sticky;
            }
            Assert.IsTrue(copySticky, "another player's Semtex shows here as a frag grenade");
#else
            yield break;
#endif
        }
    }
}
