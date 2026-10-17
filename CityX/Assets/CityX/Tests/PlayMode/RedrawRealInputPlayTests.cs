using System.Collections;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// ReDraw driven the way the owner drives it -- a mouse on the real buttons, the real colour square and the real body -- with the HUD up
    /// behind it as it is in the game (owner, 2026-10-17, with a screenshot: "the redraw menu is broken, none of the tools work properly and the
    /// portrait in the top left as well as the hotbar skills should go away on this redraw menu", then "particularly the eye dropper tool i
    /// noticed wasnt working"). PlayerRedrawStarterPlayTests calls the creator's own methods directly, which is how every one of these went
    /// unnoticed: ReDraw builds its panel during play, and a button that is wired before it is told what it does never listens at all.
    /// </summary>
    public sealed class RedrawRealInputPlayTests : InputTestFixture
    {
        private Mouse _mouse;
        private GameObject _player;
        private GameObject _hud;
        private PlayerRedrawStarter _starter;
        private EventSystem _eventSystemBefore;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            // An EventSystem another test left behind reads the input system this fixture has just replaced, and never sees this mouse:
            // ReDraw makes a fresh one when there is none (RedrawCreatorRuntime.EnsureEventSystem), as there is exactly one in the game.
            foreach (EventSystem leftover in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(leftover.gameObject);
            }
            _eventSystemBefore = null;
        }

        [UnityTearDown]
        public IEnumerator TearDownRedraw()
        {
            if (_starter != null && _starter.IsActive)
            {
                Object.Destroy(Object.FindFirstObjectByType<CreatorOverlays>()?.gameObject);
            }
            CharacterCreator leftover = Object.FindFirstObjectByType<CharacterCreator>();
            if (leftover != null)
            {
                Transform root = leftover.transform;
                while (root.parent != null) root = root.parent;
                Object.Destroy(root.gameObject);
            }
            var overlay = Object.FindFirstObjectByType<CreatorOverlays>();
            if (overlay != null) Object.Destroy(overlay.gameObject);
            if (_hud != null) Object.Destroy(_hud);
            if (_player != null) Object.Destroy(_player);
            EventSystem events = Object.FindFirstObjectByType<EventSystem>();
            if (events != null && events != _eventSystemBefore) Object.Destroy(events.gameObject);
            yield return null;
        }

        private IEnumerator Open(bool withHud)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(prefab, "Player.prefab is missing");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, -60.0f), Quaternion.identity);
            _player.GetComponent<CharacterIdentity>().Apply(CharacterSave.Create("Redraw Mouse Test", Color.white));
            _starter = _player.AddComponent<PlayerRedrawStarter>();
            _starter.InputReader = _player.GetComponent<PlayerInputReader>();
            _starter.HairMeshes = new[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/Hair1Mesh.asset"),
            };
            _starter.HairMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/Hair.mat");
            if (withHud)
            {
                GameObject hudPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/UI/PlayerHud.prefab");
                Assert.IsNotNull(hudPrefab, "PlayerHud.prefab is missing");
                _hud = Object.Instantiate(hudPrefab);
                _hud.GetComponent<PlayerHud>().Target = _player.GetComponent<PlayerVitals>();
            }
            for (int i = 0; i < 5; i++) yield return null;
            _starter.Begin();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(_starter.IsActive, "ReDraw did not open");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        private static Transform Find(string name)
        {
            CreatorOverlays overlays = Object.FindFirstObjectByType<CreatorOverlays>();
            foreach (Transform t in overlays.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            Assert.Fail($"ReDraw has no {name}");
            return null;
        }

        private IEnumerator ClickAt(Vector2 screen)
        {
            Set(_mouse.position, screen);
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
            yield return null;
        }

        private IEnumerator Click(string name)
        {
            yield return ClickAt(RectTransformUtility.WorldToScreenPoint(null, Find(name).position));
        }

        private static Vector2 ChestOnScreen(CharacterCreator creator)
        {
            Vector3 chest = creator.BodyCollider.transform.TransformPoint(creator.BodyCollider.sharedMesh.bounds.center + Vector3.up * 0.25f);
            return creator.Camera.WorldToScreenPoint(chest);
        }

        private static void AssertColour(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, 0.03f, message);
            Assert.AreEqual(expected.g, actual.g, 0.03f, message);
            Assert.AreEqual(expected.b, actual.b, 0.03f, message);
        }

        [UnityTest]
        public IEnumerator TheToolButtonsSwatchesAndSlidersWorkByMouse()
        {
            yield return Open(withHud: true);
            CharacterCreator creator = Object.FindFirstObjectByType<CharacterCreator>();
            CreatorControls controls = Object.FindFirstObjectByType<CreatorControls>();

            yield return Click("BucketButton");
            Assert.AreEqual(CharacterCreator.Tool.Bucket, creator.Current, "the bucket button does nothing");
            yield return Click("EyedropperButton");
            Assert.AreEqual(CharacterCreator.Tool.Eyedropper, creator.Current, "the eyedropper button does nothing");
            yield return Click("BrushButton");
            Assert.AreEqual(CharacterCreator.Tool.Brush, creator.Current, "the brush button does nothing");

            Color red = Find("Swatch2").GetComponent<Image>().color;
            yield return Click("Swatch2");
            AssertColour(red, creator.Colour, "a colour swatch does not pick its colour");
            AssertColour(red, controls.Preview.color, "the preview does not show the swatch's colour");

            yield return Click("MirrorButton");
            Assert.IsTrue(creator.Mirroring, "the mirror button does nothing");
            yield return Click("MirrorButton");
            yield return Click("EraserButton");
            Assert.IsTrue(creator.Erasing, "the eraser button does nothing");
            yield return Click("EraserButton");

            controls.BrushSize.value = 7.0f;
            Assert.AreEqual(7, creator.BrushRadiusPixels, "the brush size slider does nothing");
            Color before = creator.Colour;
            controls.Hue.value = 0.6f;
            Assert.Greater(Vector4.Distance(before, creator.Colour), 0.05f, "the hue slider does nothing");

            // A stroke, then undo and redo it.
            yield return Click("Swatch2");
            yield return ClickAt(ChestOnScreen(creator));
            Assert.IsTrue(creator.HasEdits, "clicking the body with the brush painted nothing");
            yield return Click("UndoButton");
            Assert.IsTrue(creator.CanRedo, "the undo button does nothing");
            yield return Click("RedoButton");
            Assert.IsFalse(creator.CanRedo, "the redo button does nothing");

            // A hairstyle button.
            ButtonAction style = null;
            foreach (ButtonAction action in Object.FindFirstObjectByType<CreatorOverlays>().GetComponentsInChildren<ButtonAction>(true))
            {
                if (action.Action == ButtonAction.Kind.CreatorHair && CharacterHair.ValidStyles[action.Value] != creator.HairStyle && CharacterHair.ValidStyles[action.Value] != CharacterHair.None)
                {
                    style = action;
                    break;
                }
            }
            Assert.IsNotNull(style, "no hairstyle button to try");
            yield return ClickAt(RectTransformUtility.WorldToScreenPoint(null, style.transform.position));
            Assert.AreEqual(CharacterHair.ValidStyles[style.Value], creator.HairStyle, "a hairstyle button does nothing");
        }

        [UnityTest]
        public IEnumerator TheEyedropperPicksTheColourUnderItAndThePanelShowsIt()
        {
            yield return Open(withHud: true);
            CharacterCreator creator = Object.FindFirstObjectByType<CharacterCreator>();
            CreatorControls controls = Object.FindFirstObjectByType<CreatorControls>();
            Vector2 chest = ChestOnScreen(creator);

            // Paint a red spot on the chest, then pick blue.
            Color red = Find("Swatch2").GetComponent<Image>().color;
            Color blue = Find("Swatch6").GetComponent<Image>().color;
            yield return Click("Swatch2");
            controls.BrushSize.value = 8.0f;
            yield return ClickAt(chest);
            yield return Click("Swatch6");
            AssertColour(blue, creator.Colour, "the blue swatch did not take");

            // The eyedropper on the red spot.
            yield return Click("EyedropperButton");
            Assert.AreEqual(CharacterCreator.Tool.Eyedropper, creator.Current, "the eyedropper button does nothing");
            yield return ClickAt(chest);
            AssertColour(red, creator.Colour, "the eyedropper did not pick the colour under it");
            AssertColour(red, controls.Preview.color, "the eyedropper picked a colour but the panel still shows the old one");
            Assert.AreEqual(CharacterCreator.Tool.Brush, creator.Current, "the eyedropper does not hand back to the brush after a pick");

            // And the bucket fills with the colour in hand: pick blue, fill, and read it back with the eyedropper.
            yield return Click("Swatch6");
            yield return Click("BucketButton");
            yield return ClickAt(chest + new Vector2(0.0f, -30.0f));
            yield return Click("Swatch2");
            yield return Click("EyedropperButton");
            yield return ClickAt(chest + new Vector2(0.0f, -30.0f));
            AssertColour(blue, creator.Colour, "the bucket did not fill with the colour in hand");
        }

        [UnityTest]
        public IEnumerator TheHudStepsAsideWhileRedrawingAndComesBackAfter()
        {
            yield return Open(withHud: true);
            for (float t = 0.0f; t < 0.5f; t += Time.unscaledDeltaTime) yield return null;
            PlayerHud hud = _hud.GetComponent<PlayerHud>();
            MenuHudFade fade = _hud.GetComponent<MenuHudFade>();
            Assert.IsNotNull(fade, "the HUD has no fade");
            Assert.AreEqual(0.0f, fade.Visibility, 0.01f, "the portrait and the hotbar stay up over ReDraw");
            CanvasGroup crosshair = hud.Crosshair != null ? hud.Crosshair.GetComponent<CanvasGroup>() : null;
            Assert.IsTrue(crosshair != null && crosshair.alpha < 0.01f, "the crosshair stays up over ReDraw");

            Object.FindFirstObjectByType<CreatorOverlays>().TryGoBack();   // CANCEL with nothing painted: straight out
            for (float t = 0.0f; t < 0.5f; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsFalse(_starter.IsActive, "CANCEL did not close ReDraw");
            Assert.AreEqual(1.0f, fade.Visibility, 0.01f, "the portrait and the hotbar did not come back after ReDraw");
            Assert.Greater(crosshair.alpha, 0.99f, "the crosshair did not come back after ReDraw");
        }
    }
}
