using System.Collections;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Drives the real ReDraw pipeline -- clone, strip, rig colliders, seed
    /// paint, paint a stroke, SAVE -- against a real Player.prefab clone, in
    /// play mode. EditMode cannot cover any of this: RedrawCreatorRuntime's
    /// own Strip() calls Destroy(), which throws outside play mode (see
    /// CreatorOverlaysRedrawTests' own doc comment), so the body-stripping/
    /// collider-rigging half of this feature -- exactly the half
    /// CharacterCreatorPlayTests' own doc comment describes going wrong
    /// silently before, for the title screen's identical pipeline -- has no
    /// EditMode coverage at all.
    ///
    /// PLAYER.PREFAB LOADED DIRECTLY, NOT THE MASSING SCENE. The massing
    /// scene streams real city geometry around wherever it spawns the
    /// player; this feature needs a body with a real mesh and a real head
    /// bone, not a real city, so instantiating the prefab by hand (the same
    /// technique MapPanelViewPlayTests/CharacterIdentityPlayTests already
    /// use for their own hand-built bodies) gets there without that cost.
    /// </summary>
    public class PlayerRedrawStarterPlayTests
    {
        private GameObject _playerObject;
        private CharacterSave _character;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CharacterCreator leftover = Object.FindFirstObjectByType<CharacterCreator>();
            if (leftover != null)
            {
                Transform root = leftover.transform;
                while (root.parent != null) root = root.parent;
                Object.Destroy(root.gameObject);
            }
            var overlayObject = Object.FindFirstObjectByType<CreatorOverlays>();
            if (overlayObject != null) Object.Destroy(overlayObject.gameObject);
            if (_playerObject != null) Object.Destroy(_playerObject);
            yield return null;
        }

        private PlayerRedrawStarter MakeStarter()
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(prefab, "Player.prefab is missing");
            _playerObject = Object.Instantiate(prefab);

            var identity = _playerObject.GetComponent<CharacterIdentity>();
            Assert.IsNotNull(identity, "Player.prefab has no CharacterIdentity");
            _character = CharacterSave.Create("Redraw Test", Color.white);
            identity.Apply(_character);

            var starter = _playerObject.AddComponent<PlayerRedrawStarter>();
            starter.InputReader = _playerObject.GetComponent<PlayerInputReader>();
            // Every style after none: Hair1's baked mesh is the only one.
            starter.HairMeshes = new[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/Hair1Mesh.asset"),
            };
            starter.HairMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/Character/Materials/Hair.mat");
            return starter;
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator BeginBuildsARealPaintableBodyAndDisablesInput()
        {
            PlayerRedrawStarter starter = MakeStarter();
            yield return null;

            starter.Begin();
            yield return null;

            Assert.IsTrue(starter.IsActive);
            Assert.IsFalse(starter.InputReader.TakesKeys, "movement should be disabled while redrawing");

            CharacterCreator creator = Object.FindFirstObjectByType<CharacterCreator>();
            Assert.IsNotNull(creator, "Begin() did not build a CharacterCreator");
            Assert.IsNotNull(creator.Paint, "the clone's paint was never bound");
            Assert.IsNotNull(creator.BodyCollider,
                "no body collider -- the clone has no SkinnedMeshRenderer, or the strip took it out");
            Assert.IsEmpty(creator.GetComponentsInChildren<Rigidbody>(true),
                "the clone is still a ragdoll -- Disarm() did not run or did not finish");
            Assert.IsEmpty(creator.GetComponentsInChildren<Joint>(true),
                "the clone still has ragdoll joints");
        }

        [UnityTest]
        public IEnumerator APaintedStrokeSurvivesSaveAndReachesCharacterSave()
        {
            PlayerRedrawStarter starter = MakeStarter();
            yield return null;
            starter.Begin();
            yield return null;

            CharacterCreator creator = Object.FindFirstObjectByType<CharacterCreator>();
            Assert.IsNotNull(creator.BodyCollider, "no body collider to paint on");

            Vector3 chest = creator.BodyCollider.transform.TransformPoint(
                creator.BodyCollider.sharedMesh.bounds.center);
            Vector2 onScreen = creator.Camera.WorldToScreenPoint(chest);

            creator.Colour = Color.red;
            creator.BrushRadiusPixels = 8;
            creator.PressAt(onScreen);
            yield return null;

            Assert.IsTrue(creator.HasEdits, "clicking the chest painted nothing");

            CreatorOverlays overlays = Object.FindFirstObjectByType<CreatorOverlays>();
            Assert.IsNotNull(overlays, "no CreatorOverlays -- Build() did not wire one on");
            overlays.SaveRedraw();

            Assert.IsFalse(starter.IsActive, "SAVE should close the session immediately");
            Assert.IsTrue(starter.InputReader.TakesKeys, "movement should come back after SAVE");
            Assert.IsTrue(_character.HasPaint, "the edited paint never reached CharacterSave");
        }

        /// <summary>
        /// ReDraw opens on the character's own hair colour -- on the head AND in the picker beside it --
        /// and a colour chosen there is saved back with the style.
        /// </summary>
        [UnityTest]
        public IEnumerator ReDrawStartsOnTheCharactersHairColourAndSavesAnewOneBack()
        {
            PlayerRedrawStarter starter = MakeStarter();
            var blue = new Color(0.2f, 0.4f, 0.85f);
            _character.HairStyle = CharacterHair.Hair1;
            _character.HairColour = blue;
            yield return null;
            starter.Begin();
            yield return null;

            CharacterCreator creator = Object.FindFirstObjectByType<CharacterCreator>();
            Assert.AreEqual(CharacterHair.Hair1, creator.HairStyle, "ReDraw did not start on the character's hairstyle");
            Assert.AreEqual(blue.b, creator.HairColour.b, 0.001f, "ReDraw did not start on the character's hair colour");

            var picker = Object.FindFirstObjectByType<HairColourControls>();
            Assert.IsNotNull(picker, "ReDraw has no hair colour picker");
            Assert.AreEqual(blue.b, picker.Current.b, 0.02f, "the picker shows a different colour from the hair");
            Assert.AreEqual(blue.r, picker.Current.r, 0.02f, "the picker shows a different colour from the hair");

            var pink = new Color(0.9f, 0.45f, 0.65f);
            picker.Adopt(pink);
            Assert.AreEqual(pink.r, creator.HairColour.r, 0.02f, "choosing a colour did not dye the hair");

            Object.FindFirstObjectByType<CreatorOverlays>().SaveRedraw();

            Assert.AreEqual(CharacterHair.Hair1, _character.HairStyle);
            Assert.AreEqual(pink.r, _character.HairColour.r, 0.02f, "the new hair colour never reached CharacterSave");
            Assert.AreEqual(pink.g, _character.HairColour.g, 0.02f);
        }

        [UnityTest]
        public IEnumerator CancellingWithNoEditsDiscardsImmediatelyAndWritesNothingBack()
        {
            PlayerRedrawStarter starter = MakeStarter();
            yield return null;
            starter.Begin();
            yield return null;

            Assert.IsFalse(_character.HasPaint, "the character started with paint -- test assumption is wrong");

            CreatorOverlays overlays = Object.FindFirstObjectByType<CreatorOverlays>();
            overlays.TryGoBack();
            yield return null;

            Assert.IsFalse(starter.IsActive, "CANCEL with nothing painted should close immediately");
            Assert.IsTrue(starter.InputReader.TakesKeys);
            Assert.IsFalse(_character.HasPaint, "cancelling should never write anything back");
        }

        [UnityTest]
        public IEnumerator CancellingAfterPaintingAsksFirstThenDiscardsOnConfirmation()
        {
            PlayerRedrawStarter starter = MakeStarter();
            yield return null;
            starter.Begin();
            yield return null;

            CharacterCreator creator = Object.FindFirstObjectByType<CharacterCreator>();
            Vector3 chest = creator.BodyCollider.transform.TransformPoint(
                creator.BodyCollider.sharedMesh.bounds.center);
            creator.PressAt(creator.Camera.WorldToScreenPoint(chest));
            yield return null;
            Assert.IsTrue(creator.HasEdits);

            CreatorOverlays overlays = Object.FindFirstObjectByType<CreatorOverlays>();
            overlays.TryGoBack();
            yield return null;

            Assert.IsTrue(starter.IsActive,
                "with real edits on the body, CANCEL should raise the discard prompt rather than close outright");

            overlays.Discard();
            yield return null;

            Assert.IsFalse(starter.IsActive);
            Assert.IsFalse(_character.HasPaint, "discarding should still never write anything back");
        }
    }
}
