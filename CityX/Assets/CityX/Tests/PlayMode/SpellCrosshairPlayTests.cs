using System.Collections;
using System.Reflection;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A spell from the bar lands where the crosshair is (owner, 2026-11-16: "the fireball doesnt go towards where your crosshair is pointed its
    /// off a bit"): it leaves the free palm, far off the camera's line, so it must be aimed at what the crosshair is ON, near or far, not at a
    /// point a fixed way down the line. On the real Player.prefab, through PlayerInputReader's own launch.
    /// </summary>
    public sealed class SpellCrosshairPlayTests
    {
        private const float StepSeconds = 1.0f / 60.0f;
        private GameObject _ground;
        private GameObject _wall;
        private GameObject _player;
        private GameObject _sword;
        private GameObject _eye;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in new[] { _ground, _wall, _player, _sword, _eye })
            {
                if (go != null) Object.Destroy(go);
            }
        }

        [UnityTest]
        public IEnumerator TheFireballFromTheLeftPalmLandsOnTheCrosshairNearAndFar([Values(5.0f, 40.0f)] float wallDistance, [Values(false, true)] bool sword)
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var reader = _player.GetComponent<PlayerInputReader>();
            reader.enabled = false;
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            if (sword)
            {
                _sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab"));
                _sword.GetComponent<SwordWeapon>().OnEquip(_player, ItemInstanceData.None);
            }
            for (int i = 0; i < 20; i++)
            {
                locomotion.Tick(new LocomotionInput { Sword1HEquipped = sword }, StepSeconds);
                yield return null;
            }
            Assert.AreEqual(sword, locomotion.CastsWithLeftHand, "the wrong hand casts");

            // The game's camera: behind and over the right shoulder (docs: Saints Row framing), looking a little down at a wall ahead.
            _eye = new GameObject("Eye");
            _eye.transform.position = _player.transform.position + new Vector3(0.87f, 2.9f, -5.0f);
            Vector3 crosshairSpot = _player.transform.position + new Vector3(-0.6f, 1.6f, wallDistance);
            _eye.transform.rotation = Quaternion.LookRotation(crosshairSpot - _eye.transform.position);
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0.0f, 5.0f, wallDistance + 0.5f);
            _wall.transform.localScale = new Vector3(60.0f, 10.0f, 1.0f);
            Physics.SyncTransforms();

            MethodInfo launch = typeof(PlayerInputReader).GetMethod("SpellLaunch", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(launch, "PlayerInputReader.SpellLaunch was renamed");
            object[] arguments = { _eye.transform, null, null };
            launch.Invoke(reader, arguments);
            var origin = (Vector3)arguments[1];
            var direction = (Vector3)arguments[2];

            Assert.IsTrue(_wall.GetComponent<Collider>().Raycast(new Ray(origin, direction), out RaycastHit hit, 200.0f), "the spell flies past the wall");
            Assert.Less(Vector3.Distance(hit.point, crosshairSpot), 0.1f, $"the spell lands {Vector3.Distance(hit.point, crosshairSpot):0.00} m off the crosshair");
        }
    }
}
