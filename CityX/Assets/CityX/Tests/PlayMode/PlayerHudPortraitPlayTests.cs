using System.Collections;
using System.Collections.Generic;
using System.IO;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// THE HUD PORTRAIT, taken of the real player with a hairstyle and a skin colour on, through the
    /// real HUD prefab. It used to be a bare head: the camera only draws one layer, the builder put
    /// the renderers that existed at scene-build time on it, and everything made while the game runs
    /// (the hair, worn clothes) stayed off it. These tests take the picture and look at its pixels.
    /// </summary>
    public sealed class PlayerHudPortraitPlayTests
    {
        private static readonly Color Background = PlayerHud.PortraitBackgroundColour;

        private GameObject _ground;
        private GameObject _player;
        private GameObject _hudObject;
        private GameObject _light;
        private PlayerHud _hud;
        private Camera _portraitCamera;
        private RenderTexture _texture;

        [TearDown]
        public void TearDown()
        {
            if (_hudObject != null) Object.Destroy(_hudObject);
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
            if (_light != null) Object.Destroy(_light);
            if (_texture != null) { _texture.Release(); Object.Destroy(_texture); }
        }

        /// <summary>The player with hair and a coloured body, and a HUD pointed at it -- built the way PlayerHudBuilder builds it.</summary>
        private void Build(string hairStyle)
        {
#if UNITY_EDITOR
            int layer = LayerMask.NameToLayer(PlayerHudLayerName);
            Assert.GreaterOrEqual(layer, 0, $"the '{PlayerHudLayerName}' layer is not defined");

            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var hudPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/UI/PlayerHud.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");
            Assert.IsNotNull(hudPrefab, "PlayerHud.prefab is missing");

            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.1f, 0.0f), Quaternion.identity);
            var reader = _player.GetComponent<PlayerInputReader>();
            if (reader != null) reader.enabled = false;

            // The world player gets its hair at scene-build time (HairAssets.AddTo); do the same here:
            // the real baked hairstyle on the head bone, wearing the shared hair material.
            Transform headBone = null;
            foreach (Transform child in _player.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "head") { headBone = child; break; }
            }
            Assert.IsNotNull(headBone, "the player has no head bone");
            var hairMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/Hair1Mesh.asset");
            var hairMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/Hair.mat");
            Assert.IsNotNull(hairMesh, "the baked hairstyle mesh is missing");
            Assert.IsNotNull(hairMaterial, "the hair material is missing");
            var hair = _player.GetComponent<CharacterHair>() ?? _player.AddComponent<CharacterHair>();
            hair.Head = headBone;
            hair.StyleMeshes = new[] { hairMesh };
            hair.HairMaterial = hairMaterial;

            // The look the character was given in the creator: a hairstyle, brown hair, a yellow body.
            var save = CharacterSave.Create("Portrait", new Color(0.95f, 0.9f, 0.05f));
            save.HairStyle = hairStyle;
            save.HairColour = new Color(0.35f, 0.18f, 0.08f);
            _player.GetComponent<CharacterIdentity>().Apply(save);

            _light = new GameObject("Light");
            Light sun = _light.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);

            _hudObject = Object.Instantiate(hudPrefab);
            _hud = _hudObject.GetComponent<PlayerHud>();
            _hud.Target = _player.GetComponent<PlayerVitals>();

            var cameraObject = new GameObject("PortraitCamera");
            cameraObject.transform.SetParent(_player.transform, false);
            _portraitCamera = cameraObject.AddComponent<Camera>();
            _portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            _portraitCamera.backgroundColor = Background;
            _portraitCamera.cullingMask = 1 << layer;
            _portraitCamera.nearClipPlane = 0.05f;
            _portraitCamera.farClipPlane = 5.0f;
            _portraitCamera.fieldOfView = 35.0f;
            _texture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32) { name = "PortraitTexture" };
            _portraitCamera.targetTexture = _texture;
            var lightObject = new GameObject("PortraitLight");
            lightObject.transform.SetParent(cameraObject.transform, false);
            Light point = lightObject.AddComponent<Light>();
            point.type = LightType.Point;
            point.intensity = 3.0f;
            point.range = 6.0f;

            _hud.PortraitCamera = _portraitCamera;
            _hud.PortraitImage.texture = _texture;
#endif
        }

        private const string PlayerHudLayerName = "PlayerPortrait";

        private Texture2D Read()
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _texture;
            var image = new Texture2D(_texture.width, _texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, _texture.width, _texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            return image;
        }

        private static void Save(string name, Texture2D image)
        {
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Portrait");
            Directory.CreateDirectory(folder);
            System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            System.Reflection.MethodInfo encode = conversion?.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            if (encode?.Invoke(null, new object[] { image }) is byte[] bytes)
            {
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), bytes);
            }
        }

        /// <summary>The share of pixels in a band of rows (0 = bottom row of the picture, 1 = top) that are not the flat background.</summary>
        private static float Covered(Texture2D image, float fromRow, float toRow)
        {
            int y0 = Mathf.RoundToInt(fromRow * (image.height - 1));
            int y1 = Mathf.RoundToInt(toRow * (image.height - 1));
            int covered = 0, total = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = 0; x < image.width; x++)
                {
                    Color c = image.GetPixel(x, y);
                    total++;
                    if (Mathf.Abs(c.r - Background.r) + Mathf.Abs(c.g - Background.g) + Mathf.Abs(c.b - Background.b) > 0.08f)
                    {
                        covered++;
                    }
                }
            }
            return total == 0 ? 0.0f : covered / (float)total;
        }

        /// <summary>The share of pixels in a band of rows that differ noticeably between two pictures.</summary>
        private static float Differs(Texture2D a, Texture2D b, float fromRow, float toRow)
        {
            int y0 = Mathf.RoundToInt(fromRow * (a.height - 1));
            int y1 = Mathf.RoundToInt(toRow * (a.height - 1));
            int differing = 0, total = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = 0; x < a.width; x++)
                {
                    Color p = a.GetPixel(x, y), q = b.GetPixel(x, y);
                    total++;
                    if (Mathf.Abs(p.r - q.r) + Mathf.Abs(p.g - q.g) + Mathf.Abs(p.b - q.b) > 0.15f)
                    {
                        differing++;
                    }
                }
            }
            return total == 0 ? 0.0f : differing / (float)total;
        }

        [UnityTest]
        public IEnumerator ThePortraitShowsTheHairAndTheTorso()
        {
#if UNITY_EDITOR
            Build("hair1");
            yield return null;
            yield return null;

            Assert.IsTrue(_hud.CaptureNow(), "the portrait could not be taken");
            Texture2D bald = null;
            Texture2D haired = Read();
            Save("with-hair", haired);

            // The same character with no hair, for comparison.
            var save = CharacterSave.Create("Portrait", new Color(0.95f, 0.9f, 0.05f));
            save.HairStyle = "none";
            _player.GetComponent<CharacterIdentity>().Apply(save);
            yield return null;
            Assert.IsTrue(_hud.CaptureNow());
            bald = Read();
            Save("no-hair", bald);

            // Torso: the bottom fifth of the picture is chest, so it must not be empty background.
            float chest = Covered(haired, 0.0f, 0.2f);
            Debug.Log($"CityX portrait: chest band {chest * 100:0}% covered; top band with hair {Covered(haired, 0.8f, 1.0f) * 100:0}%, bald {Covered(bald, 0.8f, 1.0f) * 100:0}%");
            Assert.Greater(chest, 0.35f, "the bottom of the portrait is empty -- the torso is not in the picture");

            // Hair: the top of the picture differs between the haired and the bald head (brown over yellow).
            float hairShows = Differs(haired, bald, 0.55f, 1.0f);
            Debug.Log($"CityX portrait: {hairShows * 100:0.0}% of the top pixels change when the hair goes");
            Assert.Greater(hairShows, 0.02f, "the hair makes no difference to the portrait -- it is not being drawn");

            Object.Destroy(haired);
            Object.Destroy(bald);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TakingThePortraitLeavesEveryLayerAndTheCameraAsItFoundThem()
        {
#if UNITY_EDITOR
            Build("hair1");
            yield return null;

            var before = new Dictionary<Renderer, int>();
            foreach (Renderer r in _player.GetComponentsInChildren<Renderer>(true))
            {
                before[r] = r.gameObject.layer;
            }

            Assert.IsTrue(_hud.CaptureNow());

            foreach (KeyValuePair<Renderer, int> pair in before)
            {
                Assert.AreEqual(pair.Value, pair.Key.gameObject.layer, $"{pair.Key.name} was left on another layer");
            }
            Assert.IsFalse(_portraitCamera.gameObject.activeSelf, "the portrait camera was left running");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ChangingTheLookRetakesThePortrait()
        {
#if UNITY_EDITOR
            Build("none");
            yield return null;
            yield return null;
            _hud.CaptureNow();
            Texture2D before = Read();
            float topBefore = Covered(before, 0.65f, 1.0f);

            var save = CharacterSave.Create("Portrait", new Color(0.95f, 0.9f, 0.05f));
            save.HairStyle = "hair1";
            save.HairColour = new Color(0.35f, 0.18f, 0.08f);
            _player.GetComponent<CharacterIdentity>().Apply(save);   // a redraw / load: nobody calls the HUD
            yield return null;
            yield return null;

            Texture2D after = Read();
            float changed = Differs(before, after, 0.55f, 1.0f);
            Debug.Log($"CityX portrait: {changed * 100:0.0}% of the top pixels changed after the new hair (was {topBefore * 100:0}% covered)");
            Assert.Greater(changed, 0.02f, "the portrait did not refresh when the character's look changed");
            Object.Destroy(before);
            Object.Destroy(after);
#else
            yield break;
#endif
        }
    }
}
