using System.Collections;
using System.Collections.Generic;
using System.IO;
using CityX.Character;
using CityX.Core;
using CityX.Items;
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
        private void Build(string hairStyle, bool withInventory = false)
        {
#if UNITY_EDITOR
            // A portrait is a picture: there is nothing to check without a graphics device (the default
            // headless run has none). Run these with -Graphics.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("the portrait needs a graphics device -- run with -Graphics");
            }
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

            if (withInventory)
            {
                // The world player's inventory, as the scene builder gives it (Player.prefab has none): what can be worn.
                _player.AddComponent<Inventory>().ItemPrefabs = new[]
                {
                    new Inventory.ItemPrefabEntry
                    {
                        ItemId = ItemRegistry.KnightHelmet,
                        Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/KnightHelmet.prefab"),
                    },
                    new Inventory.ItemPrefabEntry
                    {
                        ItemId = ItemRegistry.CombatSword,
                        Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab"),
                    },
                };
            }

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

        /// <summary>The picture above its bottom quarter: the face, where raised fists show, clear of the shoulders.</summary>
        private const float FaceBandBottom = 0.25f;

        /// <summary>How many pixels the framing may move between two portraits of the same pose and still count as the same picture.</summary>
        private const int FramingShift = 2;

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
        private static float Differs(Texture2D a, Texture2D b, float fromRow, float toRow, float fromColumn = 0.0f, float toColumn = 1.0f)
        {
            int y0 = Mathf.RoundToInt(fromRow * (a.height - 1));
            int y1 = Mathf.RoundToInt(toRow * (a.height - 1));
            int x0 = Mathf.RoundToInt(fromColumn * (a.width - 1));
            int x1 = Mathf.RoundToInt(toColumn * (a.width - 1));
            int differing = 0, total = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
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

        /// <summary>
        /// The share of pixels in a band of rows of <paramref name="a"/> that match no pixel of <paramref name="b"/> within <paramref name="shift"/>
        /// pixels of the same place: a picture framed a pixel or two otherwise counts as the same (the cel look's outline and flat bands turn
        /// such a shift into hard-edged slivers), a different pose does not.
        /// </summary>
        private static float DiffersBeyondAShift(Texture2D a, Texture2D b, float fromRow, float toRow, int shift)
        {
            Color[] p = a.GetPixels(), q = b.GetPixels();
            int w = a.width, h = a.height;
            int y0 = Mathf.RoundToInt(fromRow * (h - 1)), y1 = Mathf.RoundToInt(toRow * (h - 1));
            int differing = 0, total = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    total++;
                    bool matched = false;
                    for (int dy = -shift; dy <= shift && !matched; dy++)
                    {
                        for (int dx = -shift; dx <= shift && !matched; dx++)
                        {
                            int nx = Mathf.Clamp(x + dx, 0, w - 1), ny = Mathf.Clamp(y + dy, 0, h - 1);
                            Color c = p[y * w + x], d = q[ny * w + nx];
                            matched = Mathf.Abs(c.r - d.r) + Mathf.Abs(c.g - d.g) + Mathf.Abs(c.b - d.b) <= 0.15f;
                        }
                    }
                    differing += matched ? 0 : 1;
                }
            }
            return total == 0 ? 0.0f : differing / (float)total;
        }

        [UnityTest]
        public IEnumerator ThePortraitIsTheHeadWithItsHairAndAGlimpseOfTheShoulders()
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

            // Close on the head (owner, 2026-10-14: "just the head and a small glimpse of the shoulders visible ... i shouldnt see the whole
            // players torso"): the head fills most of the picture's width across its middle, and the bottom rows hold only the tops of the
            // shoulders. The chest framing this replaced covered about 40% of the middle band.
            float middle = Covered(haired, 0.42f, 0.6f);
            float shoulders = Covered(haired, 0.0f, 0.08f);
            Debug.Log($"CityX portrait: middle band {middle * 100:0}% covered, bottom band {shoulders * 100:0}%; top band with hair {Covered(haired, 0.8f, 1.0f) * 100:0}%, bald {Covered(bald, 0.8f, 1.0f) * 100:0}%");
            Assert.Greater(middle, 0.55f, "the head does not fill the portrait -- it is framed too far out");
            Assert.Greater(shoulders, 0.15f, "no shoulders at the bottom of the portrait at all");
            Assert.AreEqual("Portrait", _hud.NameLabel.text, "the frame does not show the character's name");

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

        /// <summary>
        /// Owner, 2026-10-18: "make the portrait in the top left show helmets and any other items equipped. dont alter the camera angle of the
        /// portrait", "dont alter the positioning of the playermodel in the portrait either". Putting on the Knight Helmet retakes the portrait
        /// with the helmet in it and the hair hidden under it, from exactly where the camera stood before.
        /// </summary>
        [UnityTest]
        public IEnumerator PuttingOnAHelmetRetakesThePortraitWithItFromTheSameSpot()
        {
#if UNITY_EDITOR
            Build("hair1", withInventory: true);
            for (int i = 0; i < 60; i++)
            {
                yield return null;   // standing on the ground, not still dropping onto it
            }
            _hud.CaptureNow();
            Texture2D before = Read();
            Vector3 cameraBefore = _portraitCamera.transform.position;
            Quaternion turnBefore = _portraitCamera.transform.rotation;

            // Mid-game the body is in some other pose when the helmet goes on: here the arms are flung out and the chest turned.
            Animator animator = _player.GetComponentInChildren<Animator>();
            animator.enabled = false;
            Transform leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            leftArm.localRotation *= Quaternion.Euler(0.0f, 0.0f, 70.0f);
            animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localRotation *= Quaternion.Euler(0.0f, 0.0f, -70.0f);
            chest.localRotation *= Quaternion.Euler(0.0f, 25.0f, 0.0f);
            Quaternion flungArm = leftArm.localRotation;

            Assert.IsTrue(_player.GetComponent<Inventory>().EquipItemId(ItemRegistry.KnightHelmet, _player), "the helmet could not be put on");
            yield return null;
            yield return null;

            Texture2D after = Read();
            Save("helmet-before", before);
            Save("helmet-after", after);
            Assert.IsTrue(_player.GetComponent<CharacterHair>().IsCovered, "the hair is still drawn under the helmet");
            Assert.Greater(Differs(before, after, 0.0f, 1.0f), 0.05f, "the portrait was not retaken with the helmet on");
            // A centimetre for the idle's breathing; framed round the helmet, the camera would stand about ten back.
            float moved = Vector3.Distance(cameraBefore, _portraitCamera.transform.position);
            Assert.Less(moved, 0.01f, $"the helmet moved the portrait's camera {moved:0.000} m");
            Assert.Less(Quaternion.Angle(turnBefore, _portraitCamera.transform.rotation), 0.5f, "the helmet turned the portrait's camera");
            float shouldersChanged = Differs(before, after, 0.0f, 0.2f, 0.0f, 0.3f);   // the shoulder at the left edge, clear of the helmet
            Assert.Less(shouldersChanged, 0.05f, $"the portrait was retaken in the body's pose of the moment ({shouldersChanged * 100:0}% of the shoulders differ)");
            Assert.Less(Quaternion.Angle(flungArm, leftArm.localRotation), 0.01f, "taking the portrait left the body in the portrait's pose");

            Assert.IsTrue(_player.GetComponent<Inventory>().UnequipToSlot(EquipSlots.Helmet, _player));
            yield return null;
            Assert.IsFalse(_player.GetComponent<CharacterHair>().IsCovered, "the hair did not come back when the helmet came off");
            Object.Destroy(before);
            Object.Destroy(after);
#else
            yield break;
#endif
        }

        /// <summary>
        /// Owner, 2026-10-19: "the portrait in the top left changing when i am switching weapons just make it stay as the default". Taking a
        /// weapon out or putting it away leaves the portrait as it was (what is held is never in it); putting something on still retakes it.
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingWeaponsLeavesThePortraitAsItWas()
        {
#if UNITY_EDITOR
            Build("hair1", withInventory: true);
            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }
            _hud.CaptureNow();
            int taken = _hud.PortraitsTaken;
            var inventory = _player.GetComponent<Inventory>();
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.CombatSword, _player), "the sword could not be taken out");
            yield return null;
            yield return null;
            Assert.IsTrue(inventory.UnequipToSlot(EquipSlots.Primary, _player), "the sword could not be put away");
            yield return null;
            yield return null;
            Assert.AreEqual(taken, _hud.PortraitsTaken, "switching weapons retook the portrait");
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.KnightHelmet, _player));
            yield return null;
            yield return null;
            Assert.AreEqual(taken + 1, _hud.PortraitsTaken, "putting on a helmet no longer retakes the portrait");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Owner, 2026-10-24: "why is my portrait in the top left switching poses still", "there should be no fists up pose in the portrait or
        /// any poses besides the plain one". A punch thrown as the world loaded put the fists up in the first portrait, and every later one was
        /// taken in that pose. A first shot out of the plain pose now only stands in: once the arms are down it is taken again, and that pose is
        /// the one kept; a body plain from the start is shot exactly as before.
        /// </summary>
        [UnityTest]
        public IEnumerator APortraitTakenWithTheFistsUpIsTakenAgainOnceTheyAreDown()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            try
            {
                // Standing plain from the start: the portrait the HUD takes by itself, kept at once.
                Build("hair1");
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                }
                Assert.IsFalse(_hud.AwaitingPlainPose, "a body standing plain was not kept as the portrait");
                Texture2D plain = Read();
                Save("plain", plain);
                TearDown();
                yield return null;

                // A jab thrown as the world loads: the fists are up when the HUD takes its first portrait.
                Build("hair1");
                var fists = _player.GetComponent<FistPoseDriver>() ?? _player.AddComponent<FistPoseDriver>();
                fists.Begin(FistKind.Jab);
                for (int i = 0; i < 30; i++)
                {
                    yield return null;   // the jab thrown and the guard fully up
                }
                _hud.CaptureNow();
                Assert.IsTrue(_player.GetComponent<PlayerLocomotion>().FistPoseActive, "the fists were not up for the first portrait");
                Assert.IsTrue(_hud.AwaitingPlainPose, "the portrait taken with the fists up was kept");
                Texture2D fistsUp = Read();
                Save("fists-up", fistsUp);
                float fistsShow = Differs(plain, fistsUp, 0.0f, 1.0f);
                Debug.Log($"CityX portrait: {fistsShow * 100:0.0}% of the picture differs with the fists up");
                Assert.Greater(fistsShow, 0.02f, "the fists made no difference to the picture: nothing was tested");

                // The guard drops after FistPoseDriver.GuardLingerSeconds: the portrait is taken again, plain, and kept.
                for (int i = 0; i < 300; i++)
                {
                    yield return null;
                }
                Assert.IsFalse(_hud.AwaitingPlainPose, "the portrait was never taken again once the fists were down");
                Texture2D after = Read();
                Save("fists-down", after);
                // Above the shoulders (the fists rose across the face): as plain as the plain one, give or take the pixel or two the framing moves
                // (a body settled on the ground for seconds against one still dropping the 10 cm it was put above it).
                float face = DiffersBeyondAShift(plain, after, FaceBandBottom, 1.0f, FramingShift);
                float left = DiffersBeyondAShift(plain, after, 0.0f, 1.0f, FramingShift);
                float fistsFace = DiffersBeyondAShift(plain, fistsUp, FaceBandBottom, 1.0f, FramingShift);
                Debug.Log($"CityX portrait: once the fists were down {face * 100:0.0}% of the face band and {left * 100:0.0}% of the picture differ " +
                    $"(with them up {fistsFace * 100:0.0}% of the face band)");
                Assert.Greater(fistsFace, 0.1f, "the raised fists hardly showed in the face band: the measure cannot tell them");
                Assert.Less(face, 0.03f, $"the portrait once the fists were down still has them up ({face * 100:0.0}% of the face band differs)");
                Assert.Less(left, fistsShow * 0.2f, $"the portrait once the fists were down is not the plain one ({left * 100:0.0}% differs)");
                Object.Destroy(plain);
                Object.Destroy(fistsUp);
                Object.Destroy(after);
            }
            finally
            {
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
#else
            yield break;
#endif
        }
    }
}
