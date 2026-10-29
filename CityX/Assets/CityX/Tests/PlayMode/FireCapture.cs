using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders fire and the jerry can (docs/FIRE.md) to Logs/CityShots/fire-*.png -- the can carried and pouring, a poured trail
    /// burning by day and at night, the flames close, and the black spots after. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.FireCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FireCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderFireAndTheJerryCan()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            FireScene.Clear();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(2000.0f, 1.0f, 2000.0f);
            ground.GetComponent<MeshRenderer>().material.color = new Color(0.42f, 0.42f, 0.44f);   // this cube's own copy, not the shared default
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 40.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            // The game's own grade (its bloom above all: the flames' cores are past white and glow through it), so the pictures show the fire as the game does.
            camera.allowHDR = true;
            camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            var grade = new GameObject("Grade").AddComponent<UnityEngine.Rendering.Volume>();
            grade.isGlobal = true;
            grade.profile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/CityX/View/Materials/CityGrade.asset");
            Physics.SyncTransforms();

            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            GameObject player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var inventory = player.GetComponent<Inventory>();

            // The can carried, then pouring a trail ahead while walking.
            inventory.EquipItemId(ItemRegistry.JerryCan, player);
            var can = player.GetComponentInChildren<JerryCanItem>();
            Assert.IsNotNull(can, "no jerry can in hand");
            for (float t = 0.0f; t < 0.6f; t += Step)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }
            CrouchCapture.Views(camera, player, "fire-can-carried");
            // Pouring ahead while backing away, as a trail is laid in GTA, then a few steps more out of the fire's way.
            Vector3 trailFront = new Vector3(0.0f, 0.0f, 1.0f);
            bool shotPouring = false;
            for (float t = 0.0f; t < 2.5f; t += Step)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f, Move = new Vector2(0.0f, -0.5f) }, Step);
                can.Tick(true, player.transform.position + Vector3.up * 2.4f - Vector3.forward * 3.0f, (Vector3.forward * 4.0f + Vector3.down * 2.0f).normalized, player, Step);
                yield return null;
                if (!shotPouring && t > 1.2f)
                {
                    shotPouring = true;
                    Vector3 hip = player.transform.position + Vector3.up * 1.2f;
                    yield return ArcaneOrbCapture.ShootFacing(camera, "fire-can-pouring", hip + new Vector3(2.8f, 0.6f, -1.0f), hip + Vector3.forward * 0.6f);
                    yield return ArcaneOrbCapture.ShootFacing(camera, "fire-can-pouring-side", hip + new Vector3(2.6f, 0.1f, 0.3f), hip + Vector3.down * 0.3f + Vector3.forward * 0.3f);
                }
            }
            Vector3 trailBack = player.transform.position + Vector3.forward * 1.0f;
            FireScene.Pour(trailBack, 1.2f);   // a puddle at the end, as a pour held on one spot leaves
            for (float t = 0.0f; t < 1.5f; t += Step)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f, Move = new Vector2(0.0f, -0.6f) }, Step);
                can.Tick(false, Vector3.zero, Vector3.forward, player, Step);
                yield return null;
            }

            // Somebody standing in the puddle; the trail lit at its front, the fire watched run back to them.
            GameObject victim = Object.Instantiate(prefab, trailBack + Vector3.up * 0.05f, Quaternion.Euler(0.0f, 180.0f, 0.0f));
            victim.GetComponent<PlayerInputReader>().enabled = false;
            var victimLocomotion = victim.GetComponent<PlayerLocomotion>();
            FireScene.Ignite(trailFront, 0.4f, true, player);
            for (float t = 0.0f; t < 2.5f; t += Step)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                victimLocomotion.Tick(new LocomotionInput { YawDegrees = 180.0f }, Step);
                yield return null;
            }
            Vector3 middle = (trailFront + trailBack) * 0.5f;
            yield return ArcaneOrbCapture.ShootFacing(camera, "fire-trail-day", middle + new Vector3(7.0f, 2.2f, -2.0f), middle + Vector3.up * 0.4f);
            yield return ArcaneOrbCapture.ShootFacing(camera, "fire-close", middle + new Vector3(2.2f, 0.8f, -0.6f), middle + Vector3.up * 0.45f);
            for (float t = 0.0f; t < 1.5f; t += Step)
            {
                victimLocomotion.Tick(new LocomotionInput { YawDegrees = 180.0f }, Step);
                yield return null;
            }
            Vector3 chest = victim.transform.position + Vector3.up * 1.4f;
            yield return ArcaneOrbCapture.ShootFacing(camera, "fire-victim", chest + new Vector3(3.0f, 0.6f, -2.5f), chest);
            sun.intensity = 0.03f;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
            for (float t = 0.0f; t < 0.5f; t += Step)
            {
                yield return null;
            }
            yield return ArcaneOrbCapture.ShootFacing(camera, "fire-trail-night", middle + new Vector3(7.0f, 2.2f, -2.0f), middle + Vector3.up * 0.4f);
            yield return ArcaneOrbCapture.ShootFacing(camera, "fire-night-close", middle + new Vector3(2.2f, 0.8f, -0.6f), middle + Vector3.up * 0.45f);
            sun.intensity = 1.3f;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            // Burned out: the black spots.
            float limit = FireRules.PuddleFuelSeconds + FireRules.BareFuelSeconds + 3.0f;
            for (float t = 0.0f; t < limit && (FireScene.BurningCount > 0 || t < 5.0f); t += Step)
            {
                yield return null;
            }
            yield return ArcaneOrbCapture.ShootFacing(camera, "fire-scorch", middle + new Vector3(4.0f, 5.0f, -4.0f), middle);
            Debug.Log($"CityX fire capture: {FireScene.Instance.Field.CountBurned()} cells burned, {FireMarks.ScorchCount} black spots, {FireScene.BurningCount} still burning");

            FireScene.Clear();
            Object.Destroy(player);
            Object.Destroy(victim);
            Object.Destroy(grade.gameObject);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }
    }
}
