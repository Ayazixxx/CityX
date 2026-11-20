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
    /// Not a test: renders the Giant Boomerang (docs/BOOMERANG.md) to Logs/CityShots/giantrang-*.png, and the Iron Spiked one to ironrang-*.png: on the back,
    /// drawn into the aim, the hand on it close up, the aimed throw, the catch, put back, and the quick throw off the back, each from the game's camera
    /// behind the shoulder and from the side, and in the air. The FrameByFrame ones shoot each one's moves every few frames (giantrang-seq-*.png,
    /// ironrang-seq-*.png). Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class GiantBoomerangCapture
    {
        /// <summary>A frame-by-frame strip takes a picture every this many frames (of 60 a second).</summary>
        private const int StripEvery = 3;

        [UnityTest]
        public IEnumerator RenderTheGiantBoomerang()
        {
            yield return Render("giantrang", "GiantBoomerang", "ThrownGiantBoomerang", GiantBoomerangModel.Bone);
        }

        [UnityTest]
        public IEnumerator RenderTheIronSpikedGiantBoomerang()
        {
            yield return Render("ironrang", "IronSpikedGiantBoomerang", "ThrownIronSpikedGiantBoomerang", GiantBoomerangModel.IronSpiked);
        }

        [UnityTest]
        public IEnumerator RenderTheGiantBoomerangsMovesFrameByFrame()
        {
            yield return Strips("giantrang", "GiantBoomerang", "ThrownGiantBoomerang", GiantBoomerangModel.Bone);
        }

        [UnityTest]
        public IEnumerator RenderTheIronSpikedMovesFrameByFrame()
        {
            yield return Strips("ironrang", "IronSpikedGiantBoomerang", "ThrownIronSpikedGiantBoomerang", GiantBoomerangModel.IronSpiked);
        }

        /// <summary>
        /// The bone one's moves in the white T-Shirt, with a third view close behind the right shoulder (owner, 2026-11-20, of the arm held out to
        /// the side: "there is a bit of the shoulder that clips through and also the sleeve has this weird indent ... try to make it straight like
        /// the red line like the sleeve would be in real life").
        /// </summary>
        [UnityTest]
        public IEnumerator RenderTheGiantBoomerangsMovesInAShirt()
        {
            yield return Strips("giantrang-shirt", "GiantBoomerang", "ThrownGiantBoomerang", GiantBoomerangModel.Bone, "TShirt");
        }

        private static IEnumerator Strips(string label, string heldName, string thrownName, GiantBoomerangModel kind, string shirt = null)
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            var stage = new Stage(heldName, thrownName, shirt);
            for (int i = 0; i < 20; i++) yield return null;

            stage.Thrower.SetGiantAiming(true);
            yield return stage.Strip(label + "-seq-draw", 0.6f);
            stage.Thrower.StartGiantThrow(Vector3.forward, 0.0f, 0.0f, kind);
            stage.Thrower.SetGiantAiming(false);
            yield return stage.Strip(label + "-seq-aimed", 0.72f);
            for (float t = 0.0f; t < 8.0f && stage.Thrower.IsOut && !stage.Thrower.GiantMoves.IsCatching; t += Time.deltaTime) yield return null;
            Assert.IsTrue(stage.Thrower.GiantMoves.IsCatching, "the hand never went out for it");
            yield return stage.Strip(label + "-seq-catch", 1.0f);
            Assert.IsFalse(stage.Thrower.IsOut, "it never came back");
            yield return Frames(0.3f);
            stage.Thrower.StartGiantThrow(Vector3.forward, 0.0f, 0.0f, kind);
            yield return stage.Strip(label + "-seq-quick", 0.87f);
            stage.Clear();
#else
            yield break;
#endif
        }

        private static IEnumerator Render(string label, string heldName, string thrownName, GiantBoomerangModel kind)
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            GiantBoomerangClips clips = GiantBoomerangMoves.For(kind);
            var stage = new Stage(heldName, thrownName);
            Camera camera = stage.Camera;
            BoomerangThrower thrower = stage.Thrower;
            Vector3 body = stage.Player.transform.position;
            for (int i = 0; i < 20; i++) yield return null;

            yield return Views(camera, label + "-back", body);
            yield return Shoot(camera, label + "-back-behind", body + new Vector3(0.3f, 2.2f, -3.2f), body + Vector3.up * 1.6f);

            // Drawn into the aim: halfway (the grab), then held.
            thrower.SetGiantAiming(true);
            yield return Frames(clips.DrawGrabSeconds);
            yield return Views(camera, label + "-draw-grab", body);
            yield return Frames(0.6f);
            yield return Views(camera, label + "-aim", body);
            // The hand on it, close: from above and behind, from in front, from the head's side and from below; and the whole of it from above.
            Vector3 hand = stage.Player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand).position;
            yield return Shoot(camera, label + "-grip-above", hand + new Vector3(0.25f, 0.9f, -0.9f), hand);
            yield return Shoot(camera, label + "-grip-front", hand + new Vector3(0.5f, 0.25f, 1.1f), hand);
            yield return Shoot(camera, label + "-grip-inside", hand + new Vector3(-0.8f, 0.7f, 0.5f), hand);
            yield return Shoot(camera, label + "-grip-below", hand + new Vector3(0.2f, -0.8f, 0.9f), hand);
            yield return Shoot(camera, label + "-aim-above", body + new Vector3(0.4f, 7.0f, -0.3f), body + new Vector3(0.4f, 1.5f, 0.0f));
            // The aimed throw: the wind's end, and the release.
            thrower.StartGiantThrow(Vector3.forward, 0.0f, 0.0f, kind);
            thrower.SetGiantAiming(false);
            yield return Frames(clips.AimedReleaseSeconds * 0.5f);
            yield return Views(camera, label + "-aimed-mid", body);
            yield return Frames(clips.AimedReleaseSeconds * 0.5f + 0.05f);
            yield return Views(camera, label + "-aimed-released", body);
            yield return Frames(0.25f);
            Assert.IsNotNull(thrower.Flying, "the aimed throw never let it go");
            Vector3 at = thrower.Flying.transform.position;
            yield return Shoot(camera, label + "-air-behind", body + new Vector3(0.87f, 2.8f, -4.5f), at);
            at = thrower.Flying.transform.position;
            yield return Shoot(camera, label + "-air-side", at + new Vector3(6.0f, 1.0f, 0.0f), at);
            // The catch: the hand out for it as it comes in, the moment it is caught, the arm giving with it, and put back on the back.
            for (float t = 0.0f; t < 8.0f && thrower.IsOut && !thrower.GiantMoves.IsCatching; t += Time.deltaTime) yield return null;
            Assert.IsTrue(thrower.GiantMoves.IsCatching, "the hand never went out for it");
            yield return Shoot(camera, label + "-catch-reach-game", body + new Vector3(0.87f, 2.9f, -5.0f), body + new Vector3(0.0f, 1.9f, 2.0f));
            for (float t = 0.0f; t < 8.0f && thrower.IsOut; t += Time.deltaTime) yield return null;
            Assert.IsFalse(thrower.IsOut, "it never came back");
            yield return Views(camera, label + "-catch-caught", body);
            yield return Frames(0.2f);
            yield return Views(camera, label + "-catch-give", body);
            yield return Frames(0.3f);
            yield return Views(camera, label + "-catch-putting-away", body);
            yield return Frames(0.6f);
            yield return Views(camera, label + "-caught", body);

            // The quick throw straight off the back.
            yield return Frames(BoomerangRules.ThrowEverySeconds);
            thrower.StartGiantThrow(Vector3.forward, 0.0f, 0.0f, kind);
            yield return Frames(clips.QuickGrabSeconds);
            yield return Views(camera, label + "-quick-grab", body);
            yield return Frames((clips.QuickReleaseSeconds - clips.QuickGrabSeconds) * 0.6f);
            yield return Views(camera, label + "-quick-wind", body);
            yield return Frames((clips.QuickReleaseSeconds - clips.QuickGrabSeconds) * 0.4f + 0.03f);
            yield return Views(camera, label + "-quick-released", body);
            stage.Clear();
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        /// <summary>A floor, a sun, a camera and a player carrying a giant boomerang.</summary>
        private sealed class Stage
        {
            public readonly Camera Camera;
            public readonly GameObject Player;
            public readonly BoomerangThrower Thrower;
            private readonly GameObject _ground;
            private readonly Light _sun;
            private readonly GiantBoomerangItem _item;
            private readonly GameObject _shirt;

            public Stage(string heldName, string thrownName, string shirt = null)
            {
                Time.captureFramerate = 60;
                _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                _ground.transform.localScale = new Vector3(160.0f, 1.0f, 160.0f);
                _sun = new GameObject("Sun").AddComponent<Light>();
                _sun.type = LightType.Directional;
                _sun.intensity = 1.2f;
                _sun.shadows = LightShadows.Soft;
                _sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
                Camera = new GameObject("Cam").AddComponent<Camera>();
                Camera.fieldOfView = 50.0f;
                Camera.nearClipPlane = 0.02f;
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Weapons/{heldName}.prefab");
                Assert.IsNotNull(prefab, $"no {heldName}.prefab -- rebuild the scene");
                Player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                    new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
                Player.GetComponent<PlayerInputReader>().enabled = false;
                Thrower = Player.AddComponent<BoomerangThrower>();
                Thrower.GiantThrownPrefab = Thrower.IronSpikedThrownPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Weapons/{thrownName}.prefab");
                _item = Object.Instantiate(prefab).GetComponent<GiantBoomerangItem>();
                _item.OnEquip(Player, ItemInstanceData.None);
                if (shirt != null)
                {
                    _shirt = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{shirt}.prefab"));
                    _shirt.GetComponent<CityX.Items.IEquippableItem>().OnEquip(Player, ItemInstanceData.None);
                }
            }

            /// <summary>A picture every <see cref="StripEvery"/> frames for <paramref name="seconds"/>, from the game's camera and from the body's left side: name-game-NN, name-side-NN.</summary>
            public IEnumerator Strip(string name, float seconds)
            {
                Vector3 body = Player.transform.position;
                int shot = 0;
                for (float t = 0.0f; t < seconds; shot++)
                {
                    BuildingShapeCapture.Shoot(Camera, BuildingShapeCapture.ShotsFolder, $"{name}-game-{shot:00}", body + new Vector3(0.87f, 2.9f, -5.0f), body + new Vector3(0.3f, 1.7f, 2.0f));
                    BuildingShapeCapture.Shoot(Camera, BuildingShapeCapture.ShotsFolder, $"{name}-side-{shot:00}", body + new Vector3(-7.0f, 1.7f, 0.1f), body + new Vector3(0.0f, 1.4f, 0.1f));
                    if (_shirt != null)
                    {
                        BuildingShapeCapture.Shoot(Camera, BuildingShapeCapture.ShotsFolder, $"{name}-shoulder-{shot:00}", body + new Vector3(0.9f, 2.5f, -2.0f), body + new Vector3(0.35f, 1.85f, 0.0f));
                    }
                    for (int i = 0; i < StripEvery; i++)
                    {
                        yield return null;
                        t += Time.deltaTime;
                    }
                }
            }

            public void Clear()
            {
                foreach (GameObject go in new[] { Player, _item != null ? _item.gameObject : null, _shirt, Camera.gameObject, _ground, _sun.gameObject })
                {
                    if (go != null) Object.Destroy(go);
                }
                foreach (ThrownBoomerang b in Object.FindObjectsByType<ThrownBoomerang>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
            }
        }
#endif

        private static IEnumerator Frames(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        /// <summary>From the game's camera behind the right shoulder, and from the right side.</summary>
        private static IEnumerator Views(Camera camera, string name, Vector3 body)
        {
            yield return Shoot(camera, name + "-game", body + new Vector3(0.87f, 2.9f, -5.0f), body + new Vector3(0.0f, 1.9f, 2.0f));
            yield return Shoot(camera, name + "-side", body + new Vector3(6.0f, 1.8f, 0.6f), body + new Vector3(0.0f, 1.5f, 0.3f));
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
