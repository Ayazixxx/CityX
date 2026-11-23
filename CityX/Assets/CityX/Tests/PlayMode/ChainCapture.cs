using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the chain on the game's own player in the real scene (docs/CHAIN.md). One sheet of it worn (wound round the wrist and the
    /// palm, close up from four sides, and the whole body standing), and one sheet for each attack: its moments from the game's own camera,
    /// from the side and from the front. Written to Logs/CityShots/chain-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ChainCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const int Cell = 340;
        private const int Moments = 10;
        private const int WideWidth = 960, WideHeight = 540;

        private Scene _scene;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator RenderTheChainWornAndSwung()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            PlayerInputReader input = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (input == null) input = root.GetComponentInChildren<PlayerInputReader>(true);
            }
            GameObject player = input.gameObject;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Time.captureFramerate = 60;
            var vitals = player.GetComponent<PlayerVitals>();
            vitals.InfiniteHealth = true;
            vitals.InfiniteStamina = true;
            var inventory = player.GetComponent<Inventory>();
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform body = player.transform;
            Camera game = Camera.main;
            var other = new GameObject("OtherCam").AddComponent<Camera>();
            other.enabled = false;

            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.Chain, player), "the chain could not be equipped: rebuild the scene");
            for (int i = 0; i < 40; i++)
            {
                yield return null;
            }

            // WORN: the wound hand close up from its four sides, then the whole body from the front, the side and the game's camera.
            var worn = new Texture2D(Cell * 7, Cell, TextureFormat.RGB24, false);
            Vector3[] sides = { body.right, body.forward, -body.right, -body.forward };
            for (int i = 0; i < sides.Length; i++)
            {
                Vector3 middle = hand.position + hand.right * 0.02f;
                other.fieldOfView = 24.0f;
                other.transform.position = middle + sides[i] * 1.25f + Vector3.up * 0.25f;
                other.transform.LookAt(middle);
                Shoot(other, worn, i * Cell, 0);
            }
            Vector3 chest = body.position + Vector3.up * 1.4f;
            other.fieldOfView = 32.0f;
            other.transform.position = chest + body.forward * 5.5f + body.right * 1.5f;
            other.transform.LookAt(chest);
            Shoot(other, worn, 4 * Cell, 0);
            other.transform.position = chest + body.right * 5.5f;
            other.transform.LookAt(chest);
            Shoot(other, worn, 5 * Cell, 0);
            Shoot(game, worn, 6 * Cell, 0);
            Save(worn, "chain-worn.png");

            var driver = player.GetComponent<SwordPoseDriver>();
            if (driver == null)
            {
                driver = player.AddComponent<SwordPoseDriver>();
            }

            // THE LASH AS THE GAME SHOWS IT, at the game's own size: wound up, mid-strike, landing, carried through, and hanging after.
            float[] seen = { 0.22f, 0.33f, 0.37f, 0.40f, 0.44f, 1.2f };
            var wide = new Texture2D(WideWidth * 3, WideHeight * 2, TextureFormat.RGB24, false);
            driver.Begin(SwordStrikeKind.ChainLash);
            float gone = 0.0f;
            for (int shot = 0; shot < seen.Length; shot++)
            {
                while (gone < seen[shot])
                {
                    yield return null;
                    gone += 1.0f / 60.0f;
                }
                Shoot(game, wide, (shot % 3) * WideWidth, (1 - shot / 3) * WideHeight, WideWidth, WideHeight);
            }
            Save(wide, "chain-game.png");
            for (int i = 0; i < 240; i++)
            {
                yield return null;
            }
            // HOT (the Fire Enhancement): the lash and the backhand as the game shows them, a frame at a time through the swing.
            var fire = player.GetComponent<FireEnhancementAbility>();
            Assert.IsTrue(fire.TryCast(), "the fire enhancement could not be cast");   // (it waits half a minute for a real attack; these are only shown)
            // By day the lash and the backhand; then the lash by night, when the steel is the only light there is.
            var cycle = Object.FindFirstObjectByType<CityX.View.DayNightCycle>();
            float noon = cycle != null ? cycle.TimeOfDay : 0.0f;
            (SwordStrikeKind kind, bool night)[] hot = { (SwordStrikeKind.ChainLash, false), (SwordStrikeKind.ChainBackhand, false), (SwordStrikeKind.ChainLash, true) };
            foreach ((SwordStrikeKind kind, bool night) in hot)
            {
                if (cycle != null)
                {
                    cycle.TimeOfDay = night ? CityX.View.SkyCycle.DaySeconds + CityX.View.SkyCycle.NightSeconds * 0.5f : noon;
                }
                for (int i = 0; i < 20; i++)
                {
                    yield return null;
                }
                float[] glowing = { 0.0f, 0.30f, 0.37f, 0.40f, 0.44f, 0.50f, 0.62f, 0.90f };
                var sheet = new Texture2D(WideWidth * 4, WideHeight * 2, TextureFormat.RGB24, false);
                driver.Begin(kind);
                float since = 0.0f;
                for (int shot = 0; shot < glowing.Length; shot++)
                {
                    while (since < glowing[shot])
                    {
                        yield return null;
                        since += 1.0f / 60.0f;
                    }
                    Shoot(game, sheet, (shot % 4) * WideWidth, (1 - shot / 4) * WideHeight, WideWidth, WideHeight);
                }
                Save(sheet, "chain-hot-" + SwordStrikes.NameOf(kind) + (night ? "-night" : string.Empty) + ".png");
                for (int i = 0; i < 150; i++)
                {
                    yield return null;
                }
            }
            // Worn and hot: the wound hand close up.
            for (int i = 0; i < 200; i++)
            {
                yield return null;
            }
            var wornHot = new Texture2D(WideHeight, WideHeight, TextureFormat.RGB24, false);
            other.fieldOfView = 24.0f;
            other.transform.position = hand.position + body.right * 1.1f + body.forward * 0.5f + Vector3.up * 0.25f;
            other.transform.LookAt(hand.position);
            Shoot(other, wornHot, 0, 0, WideHeight, WideHeight);
            Save(wornHot, "chain-hot-worn.png");
            if (cycle != null)
            {
                cycle.TimeOfDay = noon;
            }
            fire.Tick(FireEnhancementRules.ArmedSeconds + 1.0f);   // out again, for the plain sheets below
            yield return null;

            SwordStrikeKind[] kinds = { SwordStrikeKind.ChainLash, SwordStrikeKind.ChainBackhand, SwordStrikeKind.ChainAir, SwordStrikeKind.ChainGround };
            foreach (SwordStrikeKind kind in kinds)
            {
                if (kind == SwordStrikeKind.ChainAir)
                {
                    Press(_keyboard.spaceKey);
                    yield return null;
                    Release(_keyboard.spaceKey);
                    for (int i = 0; i < 10; i++)
                    {
                        yield return null;
                    }
                }
                SwordStrikes.StrikeTiming t = SwordStrikes.TimingOf(kind);
                float strike = t.Strike;
                float held = t.WindUp + t.Hold;
                float[] marks =
                {
                    t.WindUp, held, held + strike * 0.4f, held + strike * 0.75f, t.Contact, t.Contact + t.Follow * 0.5f, t.Contact + t.Follow,
                    t.Contact + t.Follow + t.Settle, t.Total - 0.05f, t.Total + 0.35f,
                };
                var sheet = new Texture2D(Cell * Moments, Cell * 3, TextureFormat.RGB24, false);
                driver.Begin(kind);
                float elapsed = 0.0f;
                for (int shot = 0; shot < Moments; shot++)
                {
                    while (elapsed < marks[shot])
                    {
                        yield return null;
                        elapsed += 1.0f / 60.0f;
                    }
                    chest = body.position + Vector3.up * 1.4f;
                    Shoot(game, sheet, shot * Cell, 2 * Cell);
                    other.fieldOfView = 38.0f;
                    other.transform.position = chest + body.right * 7.5f + body.forward * 1.0f + Vector3.up * 0.3f;
                    other.transform.LookAt(chest + body.forward * 1.0f);
                    Shoot(other, sheet, shot * Cell, Cell);
                    other.transform.position = chest + body.forward * 8.0f + body.right * -2.0f + Vector3.up * 0.6f;
                    other.transform.LookAt(chest + body.forward * 1.0f);
                    Shoot(other, sheet, shot * Cell, 0);
                }
                Save(sheet, "chain-" + SwordStrikes.NameOf(kind) + ".png");
                for (int i = 0; i < 240; i++)
                {
                    yield return null;   // drawn back in and wound on again before the next
                }
            }
            Object.Destroy(other.gameObject);
#else
            yield break;
#endif
        }

        /// <summary>
        /// The two lashes in the parka over the maroon jeans, close on the coat's back from behind and from either side behind (owner,
        /// 2026-11-22, of the second swing: "the coat has a hole showing the maroon jeans through it"). Logs/CityShots/chain-parka-*.png.
        /// </summary>
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator RenderTheLashesInTheParka()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            PlayerInputReader input = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (input == null) input = root.GetComponentInChildren<PlayerInputReader>(true);
            }
            GameObject player = input.gameObject;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Time.captureFramerate = 60;
            var inventory = player.GetComponent<Inventory>();
            inventory.EquipItemId(ItemRegistry.MaroonJeans, player);
            inventory.EquipItemId(ItemRegistry.GreenFurHoodedParka, player);
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.Chain, player), "the chain could not be equipped: rebuild the scene");
            Transform body = player.transform;
            var other = new GameObject("OtherCam").AddComponent<Camera>();
            other.enabled = false;
            other.fieldOfView = 22.0f;
            var cycle = Object.FindFirstObjectByType<CityX.View.DayNightCycle>();
            if (cycle != null)
            {
                cycle.TimeOfDay = CityX.View.SkyCycle.DaySeconds * 0.5f;
            }
            var driver = player.GetComponent<SwordPoseDriver>();
            if (driver == null)
            {
                driver = player.AddComponent<SwordPoseDriver>();
            }
            for (int i = 0; i < 40; i++)
            {
                yield return null;
            }
            foreach (SwordStrikeKind kind in new[] { SwordStrikeKind.ChainLash, SwordStrikeKind.ChainBackhand })
            {
                var sheet = new Texture2D(Cell * Moments, Cell * 3, TextureFormat.RGB24, false);
                driver.Begin(kind);
                for (int shot = 0; shot < Moments; shot++)
                {
                    for (int f = 0; f < (shot == 0 ? 14 : 3); f++)
                    {
                        yield return null;
                    }
                    Vector3 hips = body.position + Vector3.up * 0.9f;
                    Vector3[] from = { -body.forward, -body.forward + body.right, -body.forward - body.right };
                    for (int v = 0; v < from.Length; v++)
                    {
                        other.transform.position = hips + from[v].normalized * 3.6f + Vector3.up * 0.9f;
                        other.transform.LookAt(hips);
                        Shoot(other, sheet, shot * Cell, (2 - v) * Cell);
                    }
                }
                Save(sheet, "chain-parka-" + SwordStrikes.NameOf(kind) + ".png");
                for (int i = 0; i < 240; i++)
                {
                    yield return null;
                }
            }
            Object.Destroy(other.gameObject);
#else
            yield break;
#endif
        }

        private static void Save(Texture2D sheet, string name)
        {
            sheet.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(BuildingShapeCapture.ShotsFolder, name), (byte[])encode.Invoke(null, new object[] { sheet }));
        }

        private static void Shoot(Camera camera, Texture2D sheet, int x, int y, int width = Cell, int height = Cell)
        {
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture before = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            sheet.ReadPixels(new Rect(0, 0, width, height), x, y);
            RenderTexture.active = null;
            camera.targetTexture = before;
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
