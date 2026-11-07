using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// Checks that every button on the title screen is actually connected to
    /// something.
    ///
    /// THIS TEST EXISTS BECAUSE ALL OF THEM WERE DEAD AND NOTHING SAID SO.
    /// The screens are authored by editor scripts, and wiring a button there
    /// with onClick.AddListener adds a NON-PERSISTENT listener: it lives in
    /// memory, the scene is written to disk without it, and every button is
    /// inert the next time the scene is opened. It compiles, it builds, the
    /// buttons highlight and press -- and SINGLEPLAYER went nowhere. The fix
    /// was ButtonAction, a component whose behaviour is serialized data; this
    /// is what notices if anything goes back to the old way, or if a new
    /// button is added and its references are left blank.
    ///
    /// The three bugs this file's own subject has produced so far were all the
    /// same shape: something that only runs in play mode was relied on at
    /// author time. A button's listener, a panel's OnEnable, a component's
    /// Awake. None of them fail loudly.
    /// </summary>
    public class TitleScreenWiringTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/Title.unity";

        private Scene _scene;

        [SetUp]
        public void OpenTitleScene()
        {
            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Assert.IsTrue(_scene.IsValid(),
                $"no title scene at {ScenePath} -- run CityX/Rebuild Title Screen");
        }

        [TearDown]
        public void CloseTitleScene()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, removeScene: true);
            }
        }

        private IEnumerable<GameObject> Everything()
        {
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    yield return t.gameObject;
                }
            }
        }

        private T Find<T>() where T : Component
        {
            return Everything()
                .Select(go => go.GetComponent<T>())
                .FirstOrDefault(found => found != null);
        }

        /// <summary>
        /// The row template is the one legitimate exception: its PLAY and
        /// DELETE are bound per row at runtime, on clones that are never
        /// saved, where a listener is real.
        /// </summary>
        private static bool IsRowTemplate(GameObject button)
        {
            // The FACE tab's category chips light a page of this screen only, wired at runtime by their row (FacePartChips), as nothing is saved by them.
            FacePartChips chips = button.GetComponentInParent<FacePartChips>(true);
            if (chips != null && System.Array.IndexOf(chips.Chips, button.GetComponent<Button>()) >= 0)
            {
                return true;
            }
            // The graphics page and the button opening it are wired at runtime by their views, which list them.
            Button pressed = button.GetComponent<Button>();
            GraphicsSettingsPanel graphics = button.GetComponentInParent<GraphicsSettingsPanel>(true);
            if (graphics != null && (graphics.PreviousButtons.Contains(pressed) || graphics.NextButtons.Contains(pressed) || graphics.BackButton == pressed))
            {
                return true;
            }
            TitleSettingsView settings = button.GetComponentInParent<TitleSettingsView>(true);
            if (settings != null && settings.GraphicsButton == pressed && settings.Graphics != null)
            {
                return true;
            }
            for (Transform t = button.transform; t != null; t = t.parent)
            {
                if (t.name == "RowTemplate")
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Owner, 2026-10-26: "move the delete button to just a little trash icon that is in the top right of that little panel that says Happy
        /// Level 1 ...": the list has no DELETE button beside PLAY; the bin in the stat card's top-right corner starts the (confirmed) delete; and
        /// the row has five pedestals, the back pair drawn in the silhouette.
        /// </summary>
        [Test]
        public void TheCharacterListDeletesFromABinInTheCardsCornerAndStandsFive()
        {
            GameObject list = Everything().First(go => go.name == "ListPanel");
            Assert.IsNull(list.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DeleteButton"),
                "the list still has its DELETE button");
            Transform card = list.GetComponentsInChildren<Transform>(true).First(t => t.name == "StatCard");
            Transform bin = card.Find("TrashButton");
            Assert.IsNotNull(bin, "no bin on the stat card");
            Assert.AreEqual(ButtonAction.Kind.SelectBeginDeleteSelected, bin.GetComponent<ButtonAction>().Action);
            var binRect = (RectTransform)bin;
            Assert.AreEqual(new Vector2(1.0f, 1.0f), binRect.anchorMin, "the bin is not in the card's top-right corner");
            Assert.LessOrEqual(binRect.sizeDelta.x, 32.0f, "the bin is not little");
            // Owner, 2026-10-27: "instead of "new character" make the button say "CREATE NEW"".
            Transform create = list.GetComponentsInChildren<Transform>(true).First(t => t.name == "NewButton");
            Assert.AreEqual("CREATE NEW", create.GetComponentInChildren<Text>(true).text);

            var select = Find<CharacterSelect>();
            Assert.AreEqual(CharacterSelect.SlotCount, select.PedestalAnchors.Length);
            Assert.IsTrue(select.PedestalAnchors.All(anchor => anchor != null), "a pedestal has no anchor");
            Assert.IsNotNull(select.SilhouetteMaterial, "the back pair have no dark material to be drawn in");

            // Left to right as the camera sees them, so the next character (CharacterSelect.Next) really stands on the right.
            Camera stage = Everything().Select(go => go.GetComponent<Camera>()).First(c => c != null && c.name == "SelectCamera");
            for (int slot = 1; slot < CharacterSelect.SlotCount; slot++)
            {
                Assert.Greater(stage.WorldToViewportPoint(select.PedestalAnchors[slot].position).x,
                    stage.WorldToViewportPoint(select.PedestalAnchors[slot - 1].position).x, $"pedestal {slot} is not right of pedestal {slot - 1} on screen");
            }
        }

        [Test]
        public void EveryButtonDoesSomething()
        {
            var dead = new List<string>();

            foreach (GameObject go in Everything())
            {
                if (go.GetComponent<Button>() == null || IsRowTemplate(go))
                {
                    continue;
                }

                var action = go.GetComponent<ButtonAction>();
                if (action == null)
                {
                    dead.Add($"{Path(go)} (no ButtonAction at all)");
                }
                else if (action.Action == ButtonAction.Kind.None)
                {
                    dead.Add($"{Path(go)} (ButtonAction set to None)");
                }
            }

            Assert.IsEmpty(dead,
                "these buttons press and do nothing:\n  " + string.Join("\n  ", dead));
        }

        /// <summary>
        /// A ButtonAction with the right Kind and a null target is exactly as
        /// dead as no ButtonAction -- Invoke checks for null and silently
        /// returns, which is right at runtime and useless as a signal.
        /// </summary>
        [Test]
        public void EveryButtonActionHasWhateverItActsOn()
        {
            var unwired = new List<string>();

            foreach (GameObject go in Everything())
            {
                var action = go.GetComponent<ButtonAction>();
                if (action == null || action.Action == ButtonAction.Kind.None)
                {
                    continue;
                }

                switch (action.Action)
                {
                    case ButtonAction.Kind.ShowMenu:
                    case ButtonAction.Kind.ShowCharacterSelect:
                    case ButtonAction.Kind.ShowCreator:
                    case ButtonAction.Kind.ShowMultiplayer:
                    case ButtonAction.Kind.ShowSettings:
                    case ButtonAction.Kind.TitleBack:
                        Require(unwired, go, action.Title, "a TitleScreen");
                        break;

                    case ButtonAction.Kind.SelectBeginCreate:
                    case ButtonAction.Kind.SelectConfirmCreate:
                    case ButtonAction.Kind.SelectChooseSkin:
                    case ButtonAction.Kind.SelectConfirmDelete:
                    case ButtonAction.Kind.SelectCancelDelete:
                    case ButtonAction.Kind.SelectNext:
                    case ButtonAction.Kind.SelectPrevious:
                    case ButtonAction.Kind.SelectPlaySelected:
                    case ButtonAction.Kind.SelectBeginDeleteSelected:
                        Require(unwired, go, action.Select, "a CharacterSelect");
                        break;

                    case ButtonAction.Kind.SettingsResolutionPrevious:
                    case ButtonAction.Kind.SettingsResolutionNext:
                    case ButtonAction.Kind.SettingsInputDevicePrevious:
                    case ButtonAction.Kind.SettingsInputDeviceNext:
                    case ButtonAction.Kind.SettingsOutputDevicePrevious:
                    case ButtonAction.Kind.SettingsOutputDeviceNext:
                    case ButtonAction.Kind.SettingsFpsPrevious:
                    case ButtonAction.Kind.SettingsFpsNext:
                        Require(unwired, go, action.Settings, "a TitleSettingsView");
                        break;

                    case ButtonAction.Kind.MultiplayerHost:
                    case ButtonAction.Kind.MultiplayerJoin:
                    case ButtonAction.Kind.MultiplayerBack:
                        Require(unwired, go, action.Multiplayer, "a TitleMultiplayerView");
                        break;

                    case ButtonAction.Kind.CreatorAskName:
                    case ButtonAction.Kind.CreatorConfirmName:
                    case ButtonAction.Kind.CreatorTryBack:
                    case ButtonAction.Kind.CreatorDiscard:
                    case ButtonAction.Kind.CreatorKeepEditing:
                        Require(unwired, go, action.Overlays, "a CreatorOverlays");
                        break;

                    case ButtonAction.Kind.CreatorColour:
                        Require(unwired, go, action.Controls, "a CreatorControls");
                        break;

                    case ButtonAction.Kind.CreatorHairColour:
                        Require(unwired, go, action.HairControls, "a HairColourControls");
                        break;

                    case ButtonAction.Kind.CreatorTab:
                        Require(unwired, go, action.Tabs, "a CreatorTabs");
                        break;

                    default:
                        Require(unwired, go, action.Creator, "a CharacterCreator");
                        break;
                }
            }

            Assert.IsEmpty(unwired,
                "these buttons are wired to nothing:\n  " + string.Join("\n  ", unwired));
        }

        private static void Require(List<string> into, GameObject go, Object target, string what)
        {
            if (target == null)
            {
                into.Add($"{Path(go)} needs {what}");
            }
        }

        /// <summary>
        /// The title has NO music. Requested: "remove the title screen music
        /// also" -- title_screen.gd's StarsPlayer (stars.mp3) is gone. The rain
        /// sound is ambience and is the one looping sound the title still has.
        /// </summary>
        [Test]
        public void TheTitleHasNoMusic()
        {
            var title = Find<TitleScreen>();
            Assert.IsNotNull(title, "there is no TitleScreen in the title scene");
            Assert.IsNull(title.MusicPlayer, "the title screen should have no music player");

            foreach (AudioSource source in _scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<AudioSource>(true)))
            {
                Assert.IsFalse(source.clip != null && source.clip.name.ToLowerInvariant().Contains("stars"),
                    $"{source.name} still plays the music track");
            }
        }

        /// <summary>
        /// The panel that lets somebody leave the creator, and the one that
        /// asks for a name. Either left unwired means a screen with no way out
        /// or no way forward.
        /// </summary>
        [Test]
        public void TheCreatorsPromptsAreWiredAndHidden()
        {
            var overlays = Find<CreatorOverlays>();
            Assert.IsNotNull(overlays, "the creator has no CreatorOverlays");

            Assert.IsNotNull(overlays.Creator, "the prompts do not know their creator");
            Assert.IsNotNull(overlays.Title, "the prompts cannot get back to the title screen");
            Assert.IsNotNull(overlays.NamePanel, "there is no name prompt");
            Assert.IsNotNull(overlays.NameField, "the name prompt has nothing to type into");
            Assert.IsNotNull(overlays.ConfirmPanel, "there is no discard check");

            Assert.IsFalse(overlays.NamePanel.activeSelf,
                "the name prompt ships open -- it would cover the creator from the first frame");
            Assert.IsFalse(overlays.ConfirmPanel.activeSelf, "the discard check ships open");
        }

        [Test]
        public void TheCreatorsControlsAreWired()
        {
            var controls = Find<CreatorControls>();
            Assert.IsNotNull(controls, "the creator has no CreatorControls");

            Assert.IsNotNull(controls.Creator, "the controls drive nothing");
            Assert.IsNotNull(controls.SVSquare, "no saturation/value square");
            Assert.IsNotNull(controls.SVSquare.texture,
                "the saturation/value square has no texture -- it would draw blank");
            Assert.IsNotNull(controls.Hue, "no hue slider -- only one hue would ever be reachable");
            Assert.IsNotNull(controls.BrushSize, "no brush size slider");
            Assert.IsNotNull(controls.Preview, "no colour preview");

            Assert.AreEqual(3, controls.ToolButtons.Length, "expected brush, bucket, eyedropper");
            Assert.AreEqual(3, controls.ToolIconImages.Length, "expected three tool icons");
            for (int i = 0; i < controls.ToolIconImages.Length; i++)
            {
                Assert.IsNotNull(controls.ToolIconImages[i], $"tool {i} has no icon");
                Assert.IsNotNull(controls.ToolIconImages[i].texture,
                    $"tool {i}'s icon has no texture -- it would be a blank button");
            }

            Assert.IsNotNull(controls.UndoButton, "undo cannot be greyed out");
            Assert.IsNotNull(controls.RedoButton, "redo cannot be greyed out");
        }

        /// <summary>The creator's own object called <paramref name="name"/>, on its panel (several panels have a BackButton).</summary>
        private Transform OnTheCreator(string name)
        {
            var tabs = Find<CreatorTabs>();
            Assert.IsNotNull(tabs, "the creator has no tabs -- rebuild the title screen");
            Transform found = Core.TransformSearch.FindDeep(tabs.transform, name);
            Assert.IsNotNull(found, $"the creator's panel has no {name}");
            return found;
        }

        /// <summary>
        /// The creator's PAINT, HAIR and FACE tabs: each a button that puts up its own page, the pages on the creator's panel and SAVED SWITCHED
        /// ON. A page is hidden by its CanvasGroup, never switched off: one saved off would wake with its buttons unwired the first time its tab
        /// came up (ButtonAction wires itself in Awake). No tab sits on a page, or it would go with it.
        /// </summary>
        [Test]
        public void TheCreatorHasPaintHairAndFaceTabsEachWiredToItsOwnPage()
        {
            var tabs = Find<CreatorTabs>();
            Assert.IsNotNull(tabs, "the creator has no tabs -- rebuild the title screen");
            Assert.AreSame(Find<CharacterCreator>(), tabs.Creator, "the tabs cannot switch the painting on the creator's body off");
            Assert.IsNotNull(tabs.Pages, "the tabs have no pages");
            Assert.AreEqual(3, tabs.Pages.Length, "expected a PAINT, a HAIR and a FACE page");
            CollectionAssert.AllItemsAreNotNull(tabs.Pages, "a page is missing");

            foreach ((int index, string tab, string page) in new[]
            {
                (CreatorTabs.Paint, "PaintTab", "PaintPage"),
                (CreatorTabs.Hair, "HairTab", "HairPage"),
                (CreatorTabs.Face, "FaceTab", "FacePage"),
            })
            {
                CanvasGroup group = tabs.Pages[index];
                Assert.AreEqual(page, group.name, $"{tab} puts up {group.name}");
                Assert.IsTrue(group.transform.IsChildOf(tabs.transform), $"{page} is not on the creator's panel");
                Assert.IsTrue(group.gameObject.activeSelf, $"{page} is saved switched off: its buttons would wake up dead");

                var action = OnTheCreator(tab).GetComponent<ButtonAction>();
                Assert.IsNotNull(action, $"{tab} has no ButtonAction");
                Assert.AreEqual(ButtonAction.Kind.CreatorTab, action.Action, $"{tab} does not change the tab");
                Assert.AreEqual(index, action.Value, $"{tab} puts up another page");
                Assert.AreSame(tabs, action.Tabs, $"{tab} is not wired to the creator's tabs");
                foreach (CanvasGroup other in tabs.Pages)
                {
                    Assert.IsFalse(action.transform.IsChildOf(other.transform), $"{tab} is on {other.name}: it would go when that page does");
                }
            }
        }

        /// <summary>
        /// What was the whole column is the PAINT page, the hair colour is on the HAIR page, and ENTER THE CITY and BACK stay on the column
        /// itself, outside every page, so they are there whichever tab is up.
        /// </summary>
        [Test]
        public void EachToolSitsOnItsOwnTabsPageAndTheActionBarUnderThemAll()
        {
            var tabs = Find<CreatorTabs>();
            Assert.IsNotNull(tabs, "the creator has no tabs -- rebuild the title screen");
            Transform paint = OnTheCreator("PaintPage");
            Transform hair = OnTheCreator("HairPage");

            var controls = Find<CreatorControls>();
            Assert.IsNotNull(controls, "the creator has no CreatorControls");
            var paintTools = new List<Component>
            {
                controls.SVSquare, controls.Hue, controls.BrushSize, controls.Preview,
                controls.MirrorButton, controls.EraserButton, controls.UndoButton, controls.RedoButton,
            };
            paintTools.AddRange(controls.ToolButtons);
            foreach (Component tool in paintTools)
            {
                Assert.IsNotNull(tool, "a paint tool is not wired (TheCreatorsControlsAreWired says which)");
                Assert.IsTrue(tool.transform.IsChildOf(paint), $"{tool.name} is not on the PAINT page");
            }
            Assert.IsNotNull(Core.TransformSearch.FindDeep(paint, "Swatch0"), "the palette is not on the PAINT page");

            var picker = Find<HairColourControls>();
            Assert.IsNotNull(picker, "the creator has no hair colour picker");
            Assert.IsTrue(picker.transform.IsChildOf(hair), "the hair colour is not on the HAIR page");

            foreach (string name in new[] { "EnterCityButton", "BackButton" })
            {
                Transform button = OnTheCreator(name);
                foreach (CanvasGroup page in tabs.Pages)
                {
                    Assert.IsFalse(button.IsChildOf(page.transform), $"{name} is on {page.name}: it would go when another tab comes up");
                }
            }
        }

        /// <summary>
        /// The body, its collider and its camera. Without the collider a click
        /// finds nothing and every stroke silently does nothing, which is the
        /// failure mode that looks most like the paint system being broken.
        /// </summary>
        [Test]
        public void TheCreatorsBodyCanBePaintedOn()
        {
            var creator = Find<CharacterCreator>();
            Assert.IsNotNull(creator, "there is no CharacterCreator in the title scene");

            Assert.IsNotNull(creator.Camera, "the creator has no camera");
            Assert.IsNotNull(creator.RotationPivot, "the body cannot be turned");
            Assert.IsNotNull(creator.BodyRenderer, "there is no body");
            Assert.IsNotNull(creator.BodyCollider,
                "the body has no mesh collider -- every stroke would land on nothing");
            Assert.IsNotNull(creator.BodyCollider.sharedMesh,
                "the body's collider has no mesh");
            Assert.IsNotNull(creator.Hair, "the Hair buttons have nothing to change");
        }

        /// <summary>
        /// The creator's body must carry NO PHYSICS AT ALL except the one mesh
        /// collider a click is tested against.
        ///
        /// The prefab it is cloned from is a full ragdoll -- a rigidbody, a
        /// joint and a collider on every bone -- and the first attempt to take
        /// that off destroyed the rigidbodies before the joints. Unity will not
        /// remove a component another one depends on, so it silently failed on
        /// every bone that had a joint and ten of each went into the saved
        /// scene with their colliders gone. On play the bones fell through the
        /// world and dragged the skin out into a twisted ribbon. Nothing said
        /// so: not the build, not the tests, not the screenshot, which does not
        /// run physics.
        /// </summary>
        [Test]
        public void TheCreatorsBodyIsNotSecretlyARagdoll()
        {
            var creator = Find<CharacterCreator>();
            Assert.IsNotNull(creator, "there is no CharacterCreator in the title scene");

            Assert.IsEmpty(creator.GetComponentsInChildren<Rigidbody>(true),
                "the creator's body still has rigidbodies -- it will fall apart on play");
            Assert.IsEmpty(creator.GetComponentsInChildren<Joint>(true),
                "the creator's body still has ragdoll joints");

            // TWO, AND EXACTLY TWO: the body's and the skull's. The head is its
            // own mesh on its own collider -- see HeadMesh -- so the body's
            // collider stops at the shoulders and a click on the face would
            // find nothing without the second one.
            Collider[] colliders = creator.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(2, colliders.Length,
                "the creator should carry exactly two colliders: the body and the skull");
            foreach (Collider collider in colliders)
            {
                Assert.IsInstanceOf<MeshCollider>(collider,
                    $"{collider.name} is not a mesh -- a click on it cannot become a triangle");
            }
            Assert.Contains(creator.BodyCollider, colliders);
            Assert.Contains(creator.HeadCollider, colliders);
        }

        /// <summary>
        /// And the animator has to be off, because the mesh collider above is
        /// baked from the UNPOSED mesh. A body that animates under a collider
        /// that does not is a body where every stroke lands somewhere other
        /// than where it was aimed.
        /// </summary>
        [Test]
        public void TheCreatorsBodyHoldsStillSoTheColliderStaysTrue()
        {
            var creator = Find<CharacterCreator>();
            Assert.IsNotNull(creator, "there is no CharacterCreator in the title scene");

            foreach (Animator animator in creator.GetComponentsInChildren<Animator>(true))
            {
                Assert.IsFalse(animator.enabled,
                    $"{animator.name} would animate the body away from its paint collider");
            }
        }

        /// <summary>
        /// character_creator.tscn's own camera, to the number. Getting this
        /// wrong is not subtle -- the last invented one put the whole
        /// character high in the frame and cropped it.
        /// </summary>
        [Test]
        public void TheCreatorsCameraIsTheOneGodotShipped()
        {
            var creator = Find<CharacterCreator>();
            Assert.IsNotNull(creator, "there is no CharacterCreator in the title scene");

            Transform camera = creator.Camera.transform;
            Assert.AreEqual(0.0f, camera.localPosition.x, 0.001f);
            Assert.AreEqual(1.6f, camera.localPosition.y, 0.001f, "camera height");
            Assert.AreEqual(4.0f, Mathf.Abs(camera.localPosition.z), 0.001f, "camera distance");
            Assert.AreEqual(8.0f, camera.localEulerAngles.x, 0.01f, "camera pitch");
            Assert.AreEqual(75.0f, creator.Camera.fieldOfView, 0.01f, "field of view");
        }

        /// <summary>
        /// The creator's camera renders ONLY its own layer, and its lights only
        /// light it. Both were missed once and the result was the night city
        /// showing through behind the character with the creator's own key
        /// light spent on the skyline -- too dark to see what you were
        /// painting.
        /// </summary>
        [Test]
        public void TheCreatorSeesNothingButItself()
        {
            var creator = Find<CharacterCreator>();
            Assert.IsNotNull(creator, "there is no CharacterCreator in the title scene");

            int layer = creator.gameObject.layer;
            Assert.AreNotEqual(0, layer, "the creator is on the default layer with the city");
            Assert.AreEqual(1 << layer, creator.Camera.cullingMask,
                "the creator's camera renders more than the creator");

            Light[] lights = creator.GetComponentsInChildren<Light>(true);
            Assert.AreEqual(3, lights.Length,
                "expected a key, a fill and a back light -- one alone leaves a black side");
            foreach (Light light in lights)
            {
                Assert.AreEqual(1 << layer, light.cullingMask,
                    $"{light.name} also lights the city behind the creator");
            }
        }

        /// <summary>
        /// The row of buildings behind the logo is the KEPT one (UI/Title/TitleSkyline.asset), not a fresh bake: a title rebuild
        /// for its interface -- a new hairstyle's button -- must leave the picture as it is (owner, 2026-10-12). Only
        /// CityX/Rebake Title Skyline changes it.
        /// </summary>
        [Test]
        public void TheBackdropIsTheKeptRowOfBuildings()
        {
            GameObject skyline = Everything().FirstOrDefault(go => go.name == "Skyline" && go.GetComponent<MeshFilter>() != null);
            Assert.IsNotNull(skyline, "there is no Skyline in the title scene");
            Mesh mesh = skyline.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh, "the title's row of buildings has no mesh");
            Assert.AreEqual(UIEditor.TitleScreenBuilder.SkylinePath, AssetDatabase.GetAssetPath(mesh),
                "the title's row of buildings is not the kept, baked one");
        }

        /// <summary>Every hairstyle, HYDE included, has its tile on the title creator's HAIR tab, and the tile picks that style.</summary>
        [Test]
        public void EveryHairstyleHasAButtonInTheCreator()
        {
            var creator = Find<CharacterCreator>();
            Assert.IsNotNull(creator, "there is no CharacterCreator in the title scene");
            CharacterHair hair = creator.Hair;
            Assert.IsNotNull(hair, "the creator's body wears no CharacterHair");
            Assert.AreEqual(CharacterHair.ValidStyles.Length - 1, hair.StyleMeshes.Length,
                "the creator's head was built with a different list of styles -- rebuild the title screen");
            for (int i = 0; i < hair.StyleMeshes.Length; i++)
            {
                Assert.IsNotNull(hair.StyleMeshes[i], $"the creator's head has no mesh for \"{CharacterHair.ValidStyles[i + 1]}\"");
            }
            Transform hairPage = OnTheCreator("HairPage");
            for (int i = 0; i < CharacterHair.ValidStyles.Length; i++)
            {
                string style = CharacterHair.ValidStyles[i];
                Transform tile = Core.TransformSearch.FindDeep(hairPage, $"Hair{style}Button");
                Assert.IsNotNull(tile, $"the creator's HAIR tab has no button for \"{style}\"");
                var action = tile.GetComponent<ButtonAction>();
                Assert.IsTrue(action != null && action.Action == ButtonAction.Kind.CreatorHair && action.Value == i,
                    $"the \"{style}\" button does not pick \"{style}\"");
            }
        }

        /// <summary>
        /// The select screen dresses each character in what it was saved wearing, from its own list of the clothes; one built before a garment
        /// came shows a character in it without (the jeans and the sneakers, 2026-10-25).
        /// </summary>
        [Test]
        public void TheSelectScreenCanDressACharacterInEveryGarment()
        {
            var select = Find<CharacterSelect>();
            Assert.IsNotNull(select, "there is no CharacterSelect in the title scene");
            var networked = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab").GetComponent<Inventory>();
            foreach (Inventory.ItemPrefabEntry entry in networked.ItemPrefabs)
            {
                if (!CityX.Items.ItemRegistry.TryGet(entry.ItemId, out CityX.Items.ItemDefinition item) || !item.IsWearable)
                {
                    continue;
                }
                Assert.IsTrue(select.ClothingPrefabs.Any(worn => worn.ItemId == entry.ItemId && worn.Prefab != null),
                    $"the select screen cannot show a character wearing {item.DisplayName} -- rebuild the title screen");
            }
        }

        private static string Path(GameObject go)
        {
            string path = go.name;
            for (Transform t = go.transform.parent; t != null; t = t.parent)
            {
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
