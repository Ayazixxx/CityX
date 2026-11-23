using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player in each colourway of the fingerless gloves (docs/FINGERLESS_GLOVES.md), standing, close on a hand from the back and
    /// the palm, and with both hands made into fists; then the pairs lying on the ground. Written to Logs/CityShots/gloves-*.png. Explicit;
    /// needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FingerlessGlovesCapture
    {
        [UnityTest]
        public IEnumerator RenderThePlayerInEachPair()
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

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            Animator animator = player.GetComponentInChildren<Animator>();
            // The finger bones are not in the humanoid avatar: found by name (BodyRig), in HandGrip's order.
            Transform[] Digits(bool right)
            {
                string side = right ? "_r" : "_l";
                var digits = new System.Collections.Generic.List<Transform>();
                foreach (string digit in new[] { "index", "middle", "ring", "little", "thumb" })
                {
                    digits.Add(CityX.Core.TransformSearch.FindDeep(player.transform, digit + "1" + side));
                    digits.Add(CityX.Core.TransformSearch.FindDeep(player.transform, digit + "2" + side));
                }
                return digits.ToArray();
            }

            foreach (FingerlessGloveColourways.Colourway colourway in FingerlessGloveColourways.All)
            {
                GameObject pair = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    FingerlessGloveColourways.PrefabPath(colourway.ItemId)), new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
                pair.GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
                // The fists are posed after this frame's skinning: skinned again for each picture.
                foreach (SkinnedMeshRenderer skinned in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skinned.forceMatrixRecalculationPerRender = true;
                }
                for (int i = 0; i < 60; i++)
                {
                    locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                    yield return null;
                }
                string name = colourway.ItemId.Replace("_fingerless_gloves", string.Empty);
                Vector3 right = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                Vector3 hips = player.transform.position + Vector3.up * 1.3f;
                BuildingShapeCapture.Shoot(camera, folder, $"gloves-{name}-whole", hips + new Vector3(1.5f, 0.4f, 6.0f), hips);
                BuildingShapeCapture.Shoot(camera, folder, $"gloves-{name}-back", right + new Vector3(0.9f, 0.15f, 0.35f), right + Vector3.down * 0.1f);
                BuildingShapeCapture.Shoot(camera, folder, $"gloves-{name}-palm", right + new Vector3(-0.45f, 0.05f, 0.85f), right + Vector3.down * 0.1f);
                BuildingShapeCapture.Shoot(camera, folder, $"gloves-{name}-front", right + new Vector3(0.2f, 0.1f, 1.0f), right + Vector3.down * 0.1f);

                // Fists: the finger bones folded as a grip folds them, after the body has been posed.
                for (int i = 0; i < 3; i++)
                {
                    locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                    yield return new WaitForEndOfFrame();
                    HandGrip.Apply(Digits(true), true, 1.0f);
                    HandGrip.Apply(Digits(false), false, 1.0f);
                }
                BuildingShapeCapture.Shoot(camera, folder, $"gloves-{name}-fist-back", right + new Vector3(0.9f, 0.15f, 0.35f), right + Vector3.down * 0.1f);
                BuildingShapeCapture.Shoot(camera, folder, $"gloves-{name}-fist-front", right + new Vector3(0.2f, 0.1f, 1.0f), right + Vector3.down * 0.1f);
                // Open again for the next pair's pictures (nothing else poses these bones).
                HandGrip.Apply(Digits(true), true, 0.0f);
                HandGrip.Apply(Digits(false), false, 0.0f);
                pair.GetComponent<IEquippableItem>().OnUnequip(player);
                yield return null;
            }

            // The pairs lying on the ground.
            float x = 20.0f;
            foreach (FingerlessGloveColourways.Colourway colourway in FingerlessGloveColourways.All)
            {
                Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FingerlessGloveColourways.PrefabPath(colourway.ItemId)),
                    new Vector3(x, 0.0f, 20.0f), Quaternion.identity);
                x += 0.6f;
            }
            yield return null;
            var middle = new Vector3(20.3f, 0.05f, 20.0f);
            BuildingShapeCapture.Shoot(camera, folder, "gloves-on-ground", middle + new Vector3(0.0f, 0.9f, 1.1f), middle);

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
