using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Crazy Guy on the real bodies (owner, 2026-11-16: "make a hostile npcs called "Crazy Guy" ... make him wield a kitchen knife and utilize its
    /// advantages"): spawned in front of a player who stands still, he equips the kitchen knife, works round to the player's back and cuts it from
    /// there: the knife's backstab.
    /// </summary>
    public sealed class CrazyGuyPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private GameObject _ground;
        private GameObject _player;
        private GameObject _crazyGuy;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_crazyGuy != null) Object.Destroy(_crazyGuy);
            if (_ground != null) Object.Destroy(_ground);
        }

        /// <summary>
        /// His clothes go with him: they were made at the world's middle and left there when he was removed, each with its pickup trigger, and
        /// the next body there stood on them (found 2026-11-19: GetUpFloorPlayTests stood up 2.5 m in the air after these tests).
        /// </summary>
        [UnityTest]
        public IEnumerator WhenHeIsGoneNothingHeWoreIsLeftBehind()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/CrazyGuy.prefab");
            Assert.IsNotNull(prefab, "CrazyGuy.prefab is missing -- rebuild the scene");
            var before = new System.Collections.Generic.HashSet<GameObject>();
            foreach (CityX.Wearables.SkinnedGarment garment in Object.FindObjectsByType<CityX.Wearables.SkinnedGarment>(FindObjectsSortMode.None))
            {
                before.Add(garment.gameObject);
            }
            _crazyGuy = Object.Instantiate(prefab, new Vector3(7.0f, 0.05f, 0.0f), Quaternion.identity);
            yield return null;
            var worn = new System.Collections.Generic.List<GameObject>();   // what was made for him, wherever it was put
            foreach (CityX.Wearables.SkinnedGarment garment in Object.FindObjectsByType<CityX.Wearables.SkinnedGarment>(FindObjectsSortMode.None))
            {
                if (!before.Contains(garment.gameObject))
                {
                    worn.Add(garment.gameObject);
                }
            }
            Assert.Greater(worn.Count, 0, "setup: he wears nothing");
            Object.Destroy(_crazyGuy);
            yield return null;
            foreach (GameObject piece in worn)
            {
                Assert.IsTrue(piece == null, $"{(piece != null ? piece.name : "")} he wore is still in the world");
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// One head of hair, guycrazy's colour (owner, 2026-11-17: "the crazy guy has two hairstyles clipping over eachother one blonde one blue,
        /// remove the blonde"): a copy built into the prefab used to stay on, undyed, inside the one grown at spawn.
        /// </summary>
        [UnityTest]
        public IEnumerator HeWearsOneHeadOfHairInHisOwnColour()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/CrazyGuy.prefab");
            Assert.IsNotNull(prefab, "CrazyGuy.prefab is missing -- rebuild the scene");
            _crazyGuy = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            yield return null;
            var hair = _crazyGuy.GetComponent<CharacterHair>();
            Assert.IsNotNull(hair, "Crazy Guy has no hair component");
            Assert.AreEqual("kickhair", hair.Style);
            int worn = 0;
            foreach (Transform child in _crazyGuy.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == CharacterHair.WornName && child.GetComponent<MeshRenderer>() != null) worn++;
            }
            Assert.AreEqual(1, worn, "Crazy Guy wears more than one head of hair");
            var block = new MaterialPropertyBlock();
            hair.Worn.GetComponent<MeshRenderer>().GetPropertyBlock(block, 0);
            Color dyed = block.GetColor(Shader.PropertyToID("_BaseColor"));
            Assert.Less(Vector4.Distance(dyed, hair.Colour), 0.01f, "the hair he wears is not dyed his colour");
            Assert.Greater(hair.Colour.b, hair.Colour.r, "guycrazy's hair is blue");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Face to face, he fights face on (owner, 2026-11-17: "the crazy guy always seems to go for backstabs make it so he only goes for that when
        /// he has a clear shot"): most of his cuts land from in front.
        /// </summary>
        [UnityTest]
        public IEnumerator FaceToFaceHeCutsFromTheFront()
        {
#if UNITY_EDITOR
            yield return Fight(0.0f, 10.0f);
            Assert.Greater(_hits, 2, "face to face he hardly cut the player");
            Assert.Less(_hitsFromBehind, _hits - _hitsFromBehind, "face to face he still cut the player mostly from behind");
#else
            yield break;
#endif
        }

        /// <summary>Already round past the player's side, he has a clear shot at its back: he steps round to it and stabs it there.</summary>
        [UnityTest]
        public IEnumerator BesideThePlayersBackHeTakesTheBackstab()
        {
#if UNITY_EDITOR
            yield return Fight(135.0f, 4.0f);
            Assert.Greater(_hitsFromBehind, 0, "with a clear shot at the player's back he never cut it from behind");
#else
            yield break;
#endif
        }

        private int _hits;
        private int _hitsFromBehind;

        /// <summary>
        /// Crazy Guy against a player who stands still facing +z, starting 3.5 m from it, <paramref name="startRound"/> degrees round from its face;
        /// counts his hits and how many land from behind.
        /// </summary>
        private IEnumerator Fight(float startRound, float seconds)
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            var vitals = _player.GetComponent<PlayerVitals>();
            vitals.InfiniteHealth = true;

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/CrazyGuy.prefab");
            Assert.IsNotNull(prefab, "CrazyGuy.prefab is missing -- rebuild the scene");
            Vector3 start = Quaternion.Euler(0.0f, startRound, 0.0f) * Vector3.forward * 3.5f + Vector3.up * 0.05f;
            _crazyGuy = Object.Instantiate(prefab, start, Quaternion.LookRotation(-new Vector3(start.x, 0.0f, start.z)));   // facing the player
            var brain = _crazyGuy.GetComponent<SwordsmanBrain>();
            brain.Seed = 20261116;
            brain.Target = vitals;
            yield return null;
            Assert.IsNotNull(brain.Combat, "Crazy Guy's knife did not equip");
            Assert.IsTrue(brain.Combat.IsOneHanded, "Crazy Guy's blade is not one-handed");

            _hits = 0;
            _hitsFromBehind = 0;
            vitals.Hit += from =>
            {
                _hits++;
                if (Backstab.IsBehind(_player.transform.position, _player.transform.forward, _crazyGuy.transform.position)) _hitsFromBehind++;
            };
            for (float clock = 0.0f; clock < seconds; clock += Dt)
            {
                vitals.TickHitstun(Dt);
                yield return null;
            }
            Debug.Log($"CityX crazy guy from {startRound} degrees round: {_hits} hits, {_hitsFromBehind} from behind in {seconds} s");
#else
            yield break;
#endif
        }
    }
}
