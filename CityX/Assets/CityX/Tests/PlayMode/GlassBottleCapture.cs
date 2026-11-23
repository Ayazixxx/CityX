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
    /// Not a test: the glass bottles (docs/GLASS_BOTTLE.md). Every kind standing in a row, whole, broken and down to its neck; each in the
    /// player's hand, carried, from the front and the side; the wind-up and the throw, moment by moment; and one thrown at a wall, its pieces
    /// frame by frame. Written to Logs/CityShots/bottle-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class GlassBottleCapture
    {
        private static GameObject Load(string path) => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

        [UnityTest]
        public IEnumerator RenderTheBottles()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(35.0f, 200.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            string folder = BuildingShapeCapture.ShotsFolder;

            // ---- the row: each kind whole, broken and down to its neck, standing on its base ----
            float x = 40.0f;
            foreach (GlassBottleKinds.Kind kind in GlassBottleKinds.All)
            {
                BottleShape shape = BottleShape.For(kind.ItemId);
                for (int state = 0; state < 3; state++)
                {
                    GameObject standing = Object.Instantiate(Load(GlassBottleKinds.PrefabPath(kind.ItemId)),
                        new Vector3(x, shape.GripHeight * BottleShape.Scale, 40.0f), Quaternion.Euler(180.0f, 0.0f, 0.0f));
                    standing.GetComponent<Rigidbody>().isKinematic = true;
                    var bottle = standing.GetComponent<BottleWeapon>();
                    bottle.IntactVisual.SetActive(state == 0);
                    bottle.BrokenVisual.SetActive(state == 1);
                    bottle.ShatteredVisual.SetActive(state == 2);
                    x += 0.34f;
                }
                x += 0.2f;
            }
            yield return null;
            var middle = new Vector3((40.0f + x) * 0.5f - 0.2f, 0.36f, 40.0f);
            BuildingShapeCapture.Shoot(camera, folder, "bottle-row", middle + new Vector3(0.0f, 0.5f, 7.5f), middle);
            BuildingShapeCapture.Shoot(camera, folder, "bottle-row-above", middle + new Vector3(1.5f, 2.4f, 6.5f), middle);
            // The last kind close (the soda bottle and its printed label): whole, broken and its neck, from the front and the back.
            var lastKind = new Vector3(x - 0.2f - 0.34f * 2.0f, 0.36f, 40.0f);
            BuildingShapeCapture.Shoot(camera, folder, "bottle-last-front", lastKind + new Vector3(0.0f, 0.15f, 2.0f), lastKind);
            BuildingShapeCapture.Shoot(camera, folder, "bottle-last-back", lastKind + new Vector3(0.0f, 0.15f, -2.0f), lastKind);

            // ---- in the hand ----
            GameObject player = Object.Instantiate(Load("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            Animator animator = player.GetComponentInChildren<Animator>();
            foreach (GlassBottleKinds.Kind kind in GlassBottleKinds.All)
            {
                GameObject held = Object.Instantiate(Load(GlassBottleKinds.PrefabPath(kind.ItemId)), new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
                held.GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
                for (int i = 0; i < 60; i++)
                {
                    locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                    yield return null;
                }
                string name = kind.ItemId.Replace("_bottle", string.Empty);
                Vector3 chest = player.transform.position + Vector3.up * 1.7f;
                Vector3 hand = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                BuildingShapeCapture.Shoot(camera, folder, $"bottle-held-{name}-front", chest + new Vector3(0.6f, 0.3f, 6.0f), chest);
                BuildingShapeCapture.Shoot(camera, folder, $"bottle-held-{name}-side", chest + new Vector3(6.0f, 0.3f, 0.6f), chest);
                BuildingShapeCapture.Shoot(camera, folder, $"bottle-held-{name}-back", chest + new Vector3(-0.87f, 1.2f, -5.0f), chest);
                BuildingShapeCapture.Shoot(camera, folder, $"bottle-held-{name}-hand", hand + new Vector3(1.0f, 0.2f, 1.3f), hand + Vector3.up * 0.15f);
                held.GetComponent<IEquippableItem>().OnUnequip(player);
                yield return null;
            }

            // ---- the aim and the throw, moment by moment, and the bottle shattering on a wall ----
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 2.0f, 7.0f);
            wall.transform.localScale = new Vector3(8.0f, 4.0f, 0.4f);
            var thrower = player.GetComponent<BottleThrower>();
            if (thrower == null)
            {
                thrower = player.AddComponent<BottleThrower>();
            }
            GameObject thrownFrom = Object.Instantiate(Load(GlassBottleKinds.PrefabPath(GlassBottleKinds.Wine)), new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
            var bottleInHand = thrownFrom.GetComponent<BottleWeapon>();
            bottleInHand.OnEquip(player, ItemInstanceData.None);
            for (int i = 0; i < 40; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Vector3 body = player.transform.position + Vector3.up * 1.7f;
            Vector3 gameCamera = body + new Vector3(0.87f, 1.2f, -5.0f);
            Vector3 fromTheSide = body + new Vector3(6.5f, 0.3f, 0.5f);
            bottleInHand.SetAiming(player, true);
            for (int i = 0; i < 30; i++)
            {
                locomotion.Tick(new LocomotionInput { ThrowAiming = true }, 1.0f / 60.0f);
                yield return null;
                if (i == 6 || i == 12 || i == 29)
                {
                    BuildingShapeCapture.Shoot(camera, folder, $"bottle-aim-side-{i:00}", fromTheSide, body);
                    BuildingShapeCapture.Shoot(camera, folder, $"bottle-aim-game-{i:00}", gameCamera, body + Vector3.forward * 2.0f);
                }
            }
            bottleInHand.Throw(player, body, Vector3.forward);
            for (int i = 0; i < 70; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
                if (i % 4 == 1 && i < 34)
                {
                    BuildingShapeCapture.Shoot(camera, folder, $"bottle-throw-side-{i:00}", fromTheSide + Vector3.forward * 1.5f, body + Vector3.forward * 1.5f);
                    BuildingShapeCapture.Shoot(camera, folder, $"bottle-throw-game-{i:00}", gameCamera, body + Vector3.forward * 2.0f);
                }
                if (i >= 34 && i % 5 == 0)
                {
                    var atTheWall = new Vector3(0.0f, 2.2f, 6.6f);
                    BuildingShapeCapture.Shoot(camera, folder, $"bottle-shatter-{i:00}", atTheWall + new Vector3(2.6f, 0.4f, -3.2f), atTheWall);
                }
            }

            Object.Destroy(wall);
            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }
    }
}
