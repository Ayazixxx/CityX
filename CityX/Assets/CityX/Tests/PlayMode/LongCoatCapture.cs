using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player wearing a long coat, standing (front, side, behind, three-quarters, the cuffs close up) and mid-stride and
    /// mid-sprint, so the fit, the texture and the skinning can be looked at. Written to Logs/CityShots/midnightcoat-*.png and
    /// greenparka-*.png, maroonparka-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class LongCoatCapture
    {
        [UnityTest]
        public IEnumerator RenderThePlayerInTheMidnightCoat() => Render("MidnightCoat", "midnightcoat");

        [UnityTest]
        public IEnumerator RenderThePlayerInTheGreenFurHoodedParka() => Render("GreenFurHoodedParka", "greenparka");

        [UnityTest]
        public IEnumerator RenderThePlayerInTheMaroonFurHoodedParka() => Render("MaroonFurHoodedParka", "maroonparka");

        /// <summary>The parka over a long-sleeved shirt (owner, 2026-11-21: "make it so i can wear a shirt underneath the parka"): greenparka-shirt-*.png.</summary>
        [UnityTest]
        public IEnumerator RenderThePlayerInTheGreenParkaOverALongSleevedShirt() => Render("GreenFurHoodedParka", "greenparka-shirt", "LongSleeveTShirt");

        /// <summary>The maroon parka over the red striped T-shirt (owner, 2026-11-21: "im seeing the shirt clipping through around the shoulders"): maroonparka-shirt-*.png.</summary>
        [UnityTest]
        public IEnumerator RenderThePlayerInTheMaroonParkaOverTheStripedShirt() => Render("MaroonFurHoodedParka", "maroonparka-shirt", "RedStripedTShirt");

        /// <summary>The Black Coat, the Brown Coat in black: blackcoat-*.png.</summary>
        [UnityTest]
        public IEnumerator RenderThePlayerInTheBlackCoat() => Render("BlackCoat", "blackcoat", "TShirt");

        /// <summary>The Brown Coat (docs/BROWN_COAT.md), hood down: browncoat-*.png.</summary>
        [UnityTest]
        public IEnumerator RenderThePlayerInTheBrownCoat() => Render("BrownCoat", "browncoat", "TShirt");

        /// <summary>The same with its hood up (/hoodon): browncoat-hood-*.png.</summary>
        [UnityTest]
        public IEnumerator RenderThePlayerInTheBrownCoatHoodUp() => Render("BrownCoat", "browncoat-hood", "TShirt", hoodUp: true);

        private static IEnumerator Render(string prefab, string shots, string shirtUnder = null, bool hoodUp = false)
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
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            if (player.GetComponent<HoodSync>() == null) player.AddComponent<HoodSync>();
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            if (shirtUnder != null)
            {
                Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{shirtUnder}.prefab"),
                    new Vector3(32.0f, 0.5f, 30.0f), Quaternion.identity).GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
            }
            GameObject coat = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{prefab}.prefab"),
                new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
            coat.GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
            if (hoodUp)
            {
                player.GetComponent<HoodSync>().SetHood(true);
            }

            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Vector3 chest = player.transform.position + Vector3.up * 1.4f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-front", chest + new Vector3(0.0f, 0.3f, 9.0f), chest);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-side", chest + new Vector3(9.0f, 0.3f, 0.0f), chest);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-behind", chest + new Vector3(0.0f, 0.6f, -9.0f), chest);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-threequarter", chest + new Vector3(6.4f, 1.5f, 6.4f), chest);
            Vector3 wrist = player.transform.position + new Vector3(0.6f, 1.05f, 0.0f);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-cuff", wrist + new Vector3(2.2f, 0.2f, 1.6f), wrist);
            // The crotch close, low in front (owner, 2026-11-24: "the croatch clips through"), and from under the coat looking up, as the
            // game's camera sees it when it comes in low (owner: "there is holes in the armpit and one in the back").
            Vector3 crotch = player.transform.position + Vector3.up * 0.95f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-crotch", crotch + new Vector3(0.4f, -0.2f, 4.0f), crotch);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-below", player.transform.position + new Vector3(0.5f, 0.3f, -1.6f),
                chest + Vector3.up * 0.3f);
            // The seat from behind, close (owner: "is that line around where the butt is a hole in the coat?").
            Vector3 seat = player.transform.position + Vector3.up * 0.85f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-seat", seat + new Vector3(0.25f, 0.1f, -2.6f), seat);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-seatside", seat + new Vector3(2.2f, 0.1f, -1.2f), seat);

            // The arms lifted, out to the sides and forward (a swing, an aim): the armpits open most there.
            var lift = player.AddComponent<CoatCaptureArmLift>();
            foreach ((string name, Vector3 way) in new[] { ("armsout", Vector3.right), ("armsforward", Vector3.forward) })
            {
                lift.Way = way;
                lift.Degrees = 80.0f;
                for (int i = 0; i < 10; i++)
                {
                    locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                    yield return null;
                }
                chest = player.transform.position + Vector3.up * 1.4f;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-{name}-front", chest + new Vector3(0.0f, 0.3f, 9.0f), chest);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-{name}-behind", chest + new Vector3(0.0f, 0.6f, -9.0f), chest);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-{name}-armpit",
                    chest + new Vector3(2.6f, -0.5f, 2.0f), chest + new Vector3(0.35f, 0.15f, 0.0f));
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-{name}-below",
                    player.transform.position + new Vector3(0.5f, 0.3f, -1.6f), chest + Vector3.up * 0.3f);
            }
            Object.Destroy(lift);
            yield return null;

            // The walk once; the sprint at four moments of a stride (the knees and the armpits move most there).
            foreach ((string name, bool sprint, int frames) in new[] { ("walk", false, 50), ("sprint1", true, 60), ("sprint2", true, 6),
                ("sprint3", true, 6), ("sprint4", true, 6) })
            {
                for (int i = 0; i < frames; i++)
                {
                    locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = sprint }, 1.0f / 60.0f);
                    yield return null;
                }
                chest = player.transform.position + Vector3.up * 1.4f;
                Vector3 side = player.transform.right;
                Vector3 ahead = player.transform.forward;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-{name}-side", chest + side * 9.0f + Vector3.up * 0.3f, chest);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-{name}-front", chest + ahead * 9.0f + Vector3.up * 0.3f, chest);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-{name}-armpit",
                    chest + (ahead + side) * 3.2f + Vector3.down * 0.4f, chest + side * 0.3f);
                Vector3 collar = chest + Vector3.up * 0.65f;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{shots}-{name}-collar",
                    collar + (ahead * 2.0f + side) * 1.2f, collar);
            }

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

namespace CityX.Tests
{
    /// <summary>For LongCoatCapture: both upper arms swung up by Degrees, out to the sides or forward, after the body is posed.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class CoatCaptureArmLift : MonoBehaviour
    {
        public Vector3 Way = Vector3.right;
        public float Degrees;

        private void LateUpdate()
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm })
            {
                Transform arm = animator.GetBoneTransform(bone);
                Vector3 side = arm.position - transform.position;
                side = Vector3.ProjectOnPlane(side, Vector3.up).normalized;
                Vector3 toward = Way == Vector3.right ? side : transform.forward;
                Vector3 axis = Vector3.Cross(Vector3.down, toward);
                arm.rotation = Quaternion.AngleAxis(Degrees, axis) * arm.rotation;
            }
        }
    }
}
