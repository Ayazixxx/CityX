using System.Collections;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders long hairstyles with the Giant Boomerang on the back (Items/BackClip; owner, 2026-11-12: "the hairstyles clip through the boomerang
    /// when its worn on the back") to Logs/CityShots/backclip-*.png, each with the hair tucked under it and without, from behind and from the side.
    /// Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BackClipCapture
    {
        [UnityTest]
        public IEnumerator RenderHairUnderTheBoomerang()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45.0f, 150.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.nearClipPlane = 0.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                Vector3.zero, Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.AddComponent<BoomerangThrower>();
            var item = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/GiantBoomerang.prefab"))
                .GetComponent<GiantBoomerangItem>();
            item.OnEquip(player, ItemInstanceData.None);
            Material hairMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/Hair.mat");
            var hair = player.GetComponent<CharacterHair>();
            if (hair == null)
            {
                hair = player.AddComponent<CharacterHair>();
            }
            foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "head") hair.Head = child;
            }
            // One mesh per style after "none", in CharacterHair.ValidStyles order: the three long ones tried here.
            var meshes = new Mesh[CharacterHair.ValidStyles.Length - 1];
            foreach ((string style, string asset) in new[] { (CharacterHair.Dreads, "DreadsMesh"), (CharacterHair.Hyde, "HydeMesh"), (CharacterHair.AnimeHair, "AnimeHairMesh") })
            {
                meshes[System.Array.IndexOf(CharacterHair.ValidStyles, style) - 1] = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/CityX/Character/Hair/{asset}.asset");
            }
            hair.StyleMeshes = meshes;
            hair.HairMaterial = hairMaterial;
            foreach (string style in new[] { CharacterHair.Dreads, CharacterHair.Hyde, CharacterHair.AnimeHair })
            {
                var save = CharacterSave.Create("Clip", new Color(0.95f, 0.78f, 0.6f));
                save.HairStyle = style;
                save.HairColour = new Color(0.12f, 0.1f, 0.1f);
                player.GetComponent<CharacterIdentity>().Apply(save);
                for (int i = 0; i < 10; i++) yield return null;
                foreach (bool tucked in new[] { false, true })
                {
                    if (tucked) hairMaterial.EnableKeyword(BackClip.Keyword);
                    else hairMaterial.DisableKeyword(BackClip.Keyword);
                    foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
                    {
                        if (r.gameObject.name == CharacterHair.WornName)
                        {
                            foreach (Material m in r.materials)
                            {
                                if (tucked) m.EnableKeyword(BackClip.Keyword);
                                else m.DisableKeyword(BackClip.Keyword);
                            }
                        }
                    }
                    string name = $"backclip-{style}-{(tucked ? "tucked" : "through")}";
                    yield return Shoot(camera, name + "-behind", new Vector3(0.6f, 2.5f, -2.6f), new Vector3(0.0f, 2.0f, 0.0f));
                    yield return Shoot(camera, name + "-side", new Vector3(2.8f, 2.3f, -0.6f), new Vector3(0.0f, 2.0f, -0.2f));
                }
            }
            hairMaterial.EnableKeyword(BackClip.Keyword);
            foreach (GameObject go in new[] { player, item.gameObject, camera.gameObject, ground, sun.gameObject })
            {
                if (go != null) Object.Destroy(go);
            }
#else
            yield break;
#endif
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
