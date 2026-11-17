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

        [UnityTest]
        public IEnumerator HeGoesRoundAStandingPlayerAndStabsItInTheBack()
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
            _crazyGuy = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 6.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));   // in front of the player, facing it
            var brain = _crazyGuy.GetComponent<SwordsmanBrain>();
            brain.Seed = 20261116;
            brain.Target = vitals;
            yield return null;
            Assert.IsNotNull(brain.Combat, "Crazy Guy's knife did not equip");
            Assert.IsTrue(brain.Combat.IsOneHanded, "Crazy Guy's blade is not one-handed");

            int hits = 0, hitsFromBehind = 0;
            vitals.Hit += from =>
            {
                hits++;
                if (Backstab.IsBehind(_player.transform.position, _player.transform.forward, _crazyGuy.transform.position)) hitsFromBehind++;
            };
            float behindFor = 0.0f;
            for (float clock = 0.0f; clock < 15.0f; clock += Dt)
            {
                vitals.TickHitstun(Dt);
                yield return null;
                if (Backstab.IsBehind(_player.transform.position, _player.transform.forward, _crazyGuy.transform.position)) behindFor += Dt;
            }
            Debug.Log($"CityX crazy guy: {hits} hits, {hitsFromBehind} from behind, {behindFor:0.0} s of 15 behind the player");
            Assert.Greater(behindFor, 3.0f, "he hardly ever got round behind a player who stood still");
            Assert.Greater(hitsFromBehind, 0, "he never cut the player from behind");
#else
            yield break;
#endif
        }
    }
}
