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
    /// The bat size slider (owner, 2026-11-16: "let me adjust the baseball bat size in unity"), on the real bodies: dragged while the game runs, the bat in hand
    /// changes at once, and another player's copy of it (mounted from the same prefab) is the same size.
    /// </summary>
    public sealed class BatSizePlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private WeaponGripSettings _real;
        private WeaponGripSettings _settings;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            if (_real != null)
            {
                WeaponGripSettings.Current = _real;
                _real = null;
            }
            if (_settings != null) Object.Destroy(_settings);
        }

#if UNITY_EDITOR
        private T Make<T>(string path, Vector3 at) where T : Component
        {
            GameObject made = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path), at, Quaternion.identity);
            _made.Add(made);
            return made.GetComponent<T>();
        }
#endif

        [UnityTest]
        public IEnumerator TheBatInHandAndAnotherPlayersCopyChangeSizeTogether()
        {
#if UNITY_EDITOR
            _real = WeaponGripSettings.Current;
            _settings = ScriptableObject.CreateInstance<WeaponGripSettings>();
            WeaponGripSettings.Current = _settings;

            var ours = Make<PlayerLocomotion>("Assets/CityX/Character/Player.prefab", Vector3.zero);
            var theirs = Make<PlayerLocomotion>("Assets/CityX/Character/Player.prefab", new Vector3(5.0f, 0.0f, 0.0f));
            ours.GetComponent<PlayerInputReader>().enabled = false;
            theirs.GetComponent<PlayerInputReader>().enabled = false;
            var held = Make<SwordWeapon>("Assets/CityX/Weapons/BaseballBat.prefab", new Vector3(20.0f, 1.0f, 0.0f));
            var copy = Make<SwordWeapon>("Assets/CityX/Weapons/BaseballBat.prefab", new Vector3(25.0f, 1.0f, 0.0f));
            held.OnEquip(ours.gameObject, CityX.Items.ItemInstanceData.None);
            copy.MountCosmetic(theirs.gameObject);
            yield return null;

            float builtReach = held.GetComponent<BatSize>().BuiltReach;
            Assert.AreEqual(builtReach, held.BladeLength, 0.001f, "at size 1 the bat is not as built");

            _settings.BaseballBatSize = 1.4f;   // the slider dragged while the game runs
            yield return null;
            foreach (SwordWeapon bat in new[] { held, copy })
            {
                Transform model = bat.GetComponent<BatSize>().Model;
                Assert.AreEqual(1.4f, model.localScale.y, 0.001f, $"{bat.transform.parent?.name}: the bat did not change size with the slider");
                Assert.AreEqual(BatSize.ReachAt(builtReach, 1.4f), bat.BladeLength, 0.001f, "the swing's reach did not follow the bat");
            }
#else
            yield break;
#endif
        }
    }
}
