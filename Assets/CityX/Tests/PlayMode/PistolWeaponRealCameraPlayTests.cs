using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE REPORTED "PRESSING E STILL DOESN'T PICK UP THE PISTOL" REGRESSION,
    /// confirmed live via CityX-PICKUP-DIAG: a real play session showed
    /// distance=6.3-6.6 against an InteractRange of 2.5 no matter how close
    /// the player actually stood, and nearbyPlayer=head instead of the
    /// player's own root.
    ///
    /// Root cause: TryPickup's range check measured from LookSourceFor's
    /// result -- the camera, when no YawSource is wired -- rather than from
    /// the player's own body. PistolWeaponPlayTests's own existing coverage
    /// never caught this because it glues a bare Camera directly onto the
    /// player with zero offset, which is nothing like the real
    /// ThirdPersonCamera that trails Distance (4.5m) behind the player's
    /// own pivot in actual gameplay -- at that real distance, a pistol at
    /// the player's own feet was permanently unreachable. This suite uses
    /// the real ThirdPersonCamera specifically to close that gap.
    /// </summary>
    public sealed class PistolWeaponRealCameraPlayTests
    {
        [UnityTest]
        public IEnumerator StandingRightNextToItWithTheRealTrailingCameraPicksItUp()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            GameObject pistolPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Weapons/Pistol.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");
            Assert.IsNotNull(pistolPrefab, "Pistol.prefab is missing");

            var player = Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            var inventory = player.AddComponent<Inventory>();

            var pistol = Object.Instantiate(pistolPrefab, new Vector3(0.0f, 0.0f, 1.5f), Quaternion.identity)
                .GetComponent<PistolWeapon>();

            var cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            ThirdPersonCamera thirdPerson = cameraObject.AddComponent<ThirdPersonCamera>();
            thirdPerson.Target = player.transform;
            thirdPerson.CollisionLayers = 0; // nothing built in this scene to collide with

            // Let the real spring-arm settle into its actual trailing
            // position before aiming it -- this IS the real camera, not a
            // stand-in glued to the player.
            yield return null;

            // Point the real orbit at the pistol by solving for the yaw/
            // pitch that would aim there, then letting the camera's own
            // LateUpdate move to match -- two passes since the camera's own
            // position depends on the angle being solved for, and this only
            // has to land inside LookDotThreshold's own 35-degree cone, not
            // dead centre.
            for (int pass = 0; pass < 3; pass++)
            {
                Vector3 toTarget = pistol.transform.position - cameraObject.transform.position;
                Quaternion aim = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                Vector3 euler = aim.eulerAngles;
                thirdPerson.Yaw = euler.y;
                thirdPerson.Pitch = euler.x > 180.0f ? euler.x - 360.0f : euler.x;
                yield return null;
            }

            float dot = Vector3.Dot(cameraObject.transform.forward,
                (pistol.transform.position - cameraObject.transform.position).normalized);
            Assert.Greater(dot, PistolWeapon.LookDotThreshold,
                $"test setup failed to aim the real camera at the pistol (dot={dot:0.00}) -- " +
                "fix the aiming loop above rather than trusting the result below");

            bool picked = pistol.TryPickup(player.transform, ePressed: true);

            Assert.IsTrue(picked,
                "standing at the pistol's own feet with the REAL trailing ThirdPersonCamera aimed at it " +
                "should pick it up -- if this fails, TryPickup is still measuring range from the camera " +
                "instead of the player's own body");
            Assert.AreEqual(ItemRegistry.Pistol, inventory.Storage[0].ItemId);

            Object.Destroy(player);
            Object.Destroy(cameraObject);
            yield return null;
#else
            Assert.Ignore("Player.prefab/Pistol.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
