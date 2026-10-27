using System.Collections;
using System.IO;
using System.Linq;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE CAROUSEL'S PEDESTAL BODIES, confirmed against a real headless
    /// render rather than assumed from the code alone -- a render with
    /// these two bugs still in place showed pedestals standing empty (the
    /// body layer bug) and, once that was fixed, arms locked straight out
    /// in a T-pose (the missing PoseArmsDown call). Both are regression-
    /// tested here directly rather than only by eye.
    /// </summary>
    public sealed class CharacterSelectCarouselPlayTests
    {
        private string _directory;
        private GameObject _panel;
        private CharacterSelect _select;
        private GameObject[] _anchorObjects;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CityXTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            CharacterStore.DirectoryOverride = _directory;

            _panel = new GameObject("Panel");
            _select = _panel.AddComponent<CharacterSelect>();

#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            _select.CarouselBodyPrefab = playerPrefab;
#endif

            // A layer OTHER than Default (0), the same way the real
            // carousel's own anchors carry the dedicated "CityXCreator"
            // layer rather than whatever Player.prefab ships on -- the bug
            // this guards against only shows up when the two actually
            // differ.
            int stageLayer = 1;
            _anchorObjects = new GameObject[CharacterSelect.SlotCount];
            var anchors = new Transform[CharacterSelect.SlotCount];
            for (int i = 0; i < CharacterSelect.SlotCount; i++)
            {
                _anchorObjects[i] = new GameObject($"Anchor{i}");
                _anchorObjects[i].layer = stageLayer;
                anchors[i] = _anchorObjects[i].transform;
            }
            _select.PedestalAnchors = anchors;
        }

        [TearDown]
        public void TearDown()
        {
            CharacterStore.DirectoryOverride = ScratchDataFixture.Folder;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
            Object.Destroy(_panel);
            foreach (GameObject anchor in _anchorObjects)
            {
                if (anchor != null) Object.Destroy(anchor);
            }
        }

        [UnityTest]
        public IEnumerator APedestalBodyLandsOnTheAnchorsOwnLayerAndStandsWithItsArmsDown()
        {
            Assert.IsNotNull(_select.CarouselBodyPrefab, "Player.prefab is missing");

            LocalServer.Database.For(PlayerKey.Local).Add(CharacterSave.Create("Ayaz", new Color(0.3f, 0.4f, 0.5f)));
            _select.Refresh();
            yield return null;

            Transform centreAnchor = _select.PedestalAnchors[CharacterSelect.CentreSlot];
            Assert.AreEqual(1, centreAnchor.childCount, "the centre pedestal has no body on it");
            GameObject body = centreAnchor.GetChild(0).gameObject;

            Assert.AreEqual(centreAnchor.gameObject.layer, body.layer,
                "the pedestal body is not on the stage's own layer -- the carousel's camera, " +
                "culling-masked to that layer, would never render it (confirmed live: a render " +
                "with this bug showed the pedestal platforms lit but standing empty)");

            var animator = body.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "the pedestal body has no Animator");
            Assert.IsFalse(animator.enabled, "the pedestal body should be held still, not animated");

            Transform armLeft = TransformSearch.FindDeep(body.transform, "arm_l");
            Assert.IsNotNull(armLeft, "no arm_l bone -- cannot confirm the pose");
            float angle = Quaternion.Angle(armLeft.localRotation, Quaternion.Euler(0.0f, 0.0f, 78.0f));
            Assert.Less(angle, 1.0f,
                "the left arm is not down at the body's side -- it is still at (or near) the " +
                "bind pose's T-pose, confirmed live as the second thing the same render showed " +
                "once the layer fix alone let the bodies actually appear");
        }

        /// <summary>
        /// "THE WIND SOUND ON THE SELECT SCREEN KEEPS PLAYING AND RESETS
        /// WHENEVER I SELECT A NEW PLAYER" -- PlayerAudio's own Awake() runs
        /// synchronously the instant the pedestal body is Instantiate()d,
        /// AddComponent()ing an AudioSource and Play()ing the looping city
        /// ambience right there, before StripWorldComponents ever gets a
        /// chance to run. Every pedestal rebuild (Next/Previous re-populates
        /// all three slots from scratch) started a fresh one of these on top
        /// of whatever was already playing -- the reported "resets."
        /// </summary>
        [UnityTest]
        public IEnumerator NoAudioSourceSurvivesOnAPedestalBody()
        {
            Assert.IsNotNull(_select.CarouselBodyPrefab, "Player.prefab is missing");

            LocalServer.Database.For(PlayerKey.Local).Add(CharacterSave.Create("Ayaz", new Color(0.3f, 0.4f, 0.5f)));
            _select.Refresh();
            yield return null;

            Transform centreAnchor = _select.PedestalAnchors[CharacterSelect.CentreSlot];
            Assert.AreEqual(1, centreAnchor.childCount, "the centre pedestal has no body on it");
            GameObject body = centreAnchor.GetChild(0).gameObject;

            AudioSource[] sources = body.GetComponentsInChildren<AudioSource>(true);
            Assert.IsEmpty(sources,
                $"found {sources.Length} AudioSource(s) still on the pedestal body -- a silent " +
                "display prop has no legitimate reason to carry any audio, and this is exactly " +
                "the ambience PlayerAudio.Awake() started before it could be stripped");
        }

        /// <summary>
        /// A saved character's clothes are on their pedestal body (owner, 2026-10-20: "make sure they have the items equipped on the character
        /// select screen (not weapons yet just wearables)"), on the stage's layer, where its camera sees them.
        /// </summary>
        [UnityTest]
        public IEnumerator ACharactersClothesAreOnTheirPedestalBody()
        {
            CharacterSave save = CharacterSave.Create("Ayaz", new Color(0.3f, 0.4f, 0.5f));
            save.Wearing = new System.Collections.Generic.List<string>
            {
                CityX.Items.ItemRegistry.RedCamoTShirt, CityX.Items.ItemRegistry.KnightHelmet, CityX.Items.ItemRegistry.Pistol,
            };
            LocalServer.Database.For(PlayerKey.Local).Add(save);
#if UNITY_EDITOR
            _select.ClothingPrefabs = new[]
            {
                new CityX.Character.Inventory.ItemPrefabEntry
                {
                    ItemId = CityX.Items.ItemRegistry.RedCamoTShirt,
                    Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/RedCamoTShirt.prefab"),
                },
                new CityX.Character.Inventory.ItemPrefabEntry
                {
                    ItemId = CityX.Items.ItemRegistry.KnightHelmet,
                    Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/KnightHelmet.prefab"),
                },
                new CityX.Character.Inventory.ItemPrefabEntry
                {
                    ItemId = CityX.Items.ItemRegistry.Pistol,
                    Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Pistol.prefab"),
                },
            };
#endif
            _select.Refresh();
            yield return null;

            Transform centreAnchor = _select.PedestalAnchors[CharacterSelect.CentreSlot];
            GameObject body = centreAnchor.GetChild(0).gameObject;
            var clothes = body.GetComponent<CityX.Wearables.SkinnedGarmentCombiner>();
            Assert.IsNotNull(clothes, "the pedestal body wears nothing");
            Assert.IsTrue(clothes.IsShowing(CityX.Items.ItemRegistry.RedCamoTShirt), "the pedestal body is not wearing the T-shirt");
            var helmet = body.GetComponentInChildren<CityX.Wearables.KnightHelmet>(true);
            Assert.IsNotNull(helmet, "the pedestal body is not wearing the helmet");
            Assert.AreEqual(body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head), helmet.transform.parent,
                "the helmet is not on the head");
            Assert.IsNull(body.GetComponentInChildren<CityX.Weapons.PistolWeapon>(true), "a weapon was shown on the select screen");
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                Assert.AreEqual(centreAnchor.gameObject.layer, renderer.gameObject.layer, $"{renderer.name} is off the stage's layer");
            }
        }

        /// <summary>
        /// Owner, 2026-10-26: "make it display 5 character (assuming you have made 5) and the back 2 on the right and left are dark just dark and
        /// shadowy figures, also make it so the direction it scrolls ... are inverted": five on the row, the furthest pair all in the silhouette
        /// material, the next character standing on the RIGHT of the chosen one, so the right arrow brings it in from the right (it came from the
        /// left).
        /// </summary>
        [UnityTest]
        public IEnumerator FiveStandOnTheRowTheBackPairInShadowAndTheNextOneOnTheRight()
        {
            var shadow = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _select.SilhouetteMaterial = shadow;
            for (int i = 0; i < 6; i++)
            {
                LocalServer.Database.For(PlayerKey.Local).Add(CharacterSave.Create($"C{i}", new Color(0.3f, 0.4f, 0.5f)));
            }
            _select.Refresh();
            yield return null;

            for (int slot = 0; slot < CharacterSelect.SlotCount; slot++)
            {
                Assert.AreEqual(1, _select.PedestalAnchors[slot].childCount, $"pedestal {slot} stands empty with six characters saved");
                bool back = Mathf.Abs(slot - CharacterSelect.CentreSlot) == 2;
                foreach (Renderer renderer in _select.PedestalAnchors[slot].GetComponentsInChildren<Renderer>(true))
                {
                    bool inShadow = renderer.sharedMaterials.All(material => material == shadow);
                    Assert.AreEqual(back, inShadow, back ? $"pedestal {slot}'s {renderer.name} is not a dark figure" : $"pedestal {slot}'s {renderer.name} is in shadow");
                }
            }

            int selected = _select.SelectedIndex;
            int onTheRight = _select.ShownAt(CharacterSelect.CentreSlot + 1);
            Assert.AreEqual((selected + 1) % 6, onTheRight, "the next character is not on the right");
            _select.Next();
            yield return null;
            Assert.AreEqual(onTheRight, _select.ShownAt(CharacterSelect.CentreSlot), "the right arrow did not bring the right one into the centre");
            Assert.AreEqual(selected, _select.ShownAt(CharacterSelect.CentreSlot - 1), "the one that was chosen did not move to the left");
            Object.Destroy(shadow);
        }

        [Test]
        public void TheBackPairOnlyStandsWithFiveSoNobodyIsOnTheRowTwice()
        {
            for (int count = 1; count <= 6; count++)
            {
                var shown = new System.Collections.Generic.List<int>();
                for (int slot = 0; slot < CharacterSelect.SlotCount; slot++)
                {
                    int character = CharacterSelect.CharacterFor(slot, 0, count);
                    bool back = Mathf.Abs(slot - CharacterSelect.CentreSlot) == 2;
                    if (back)
                    {
                        Assert.AreEqual(count >= 5, character >= 0, $"{count} characters: back pedestal {slot}");
                    }
                    if (character >= 0 && Mathf.Abs(slot - CharacterSelect.CentreSlot) != 1)
                    {
                        shown.Add(character);
                    }
                }
                CollectionAssert.AllItemsAreUnique(shown, $"{count} characters: someone stands on the row twice");
            }
            Assert.AreEqual(1, CharacterSelect.CharacterFor(CharacterSelect.CentreSlot - 1, 0, 2), "with two, the other one stands beside the centre");
        }

        /// <summary>A saved character's hair colour is on the hair of their pedestal body -- not just the default gold.</summary>
        [UnityTest]
        public IEnumerator ACharactersHairColourShowsOnTheirPedestalBody()
        {
#if UNITY_EDITOR
            _select.HairStyleMeshes = new[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/Hair1Mesh.asset"),
            };
            _select.HairMaterialAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/Character/Materials/Hair.mat");

            var blue = new Color(0.2f, 0.4f, 0.85f);
            CharacterSave save = CharacterSave.Create("Ayaz", new Color(0.3f, 0.4f, 0.5f));
            save.HairStyle = CityX.Character.CharacterHair.Hair1;
            save.HairColour = blue;
            LocalServer.Database.For(PlayerKey.Local).Add(save);
            _select.Refresh();
            yield return null;

            GameObject body = _select.PedestalAnchors[CharacterSelect.CentreSlot].GetChild(0).gameObject;
            var hair = body.GetComponent<CityX.Character.CharacterHair>();
            Assert.IsNotNull(hair, "the pedestal body has no hair component");
            Assert.IsNotNull(hair.Worn, "the pedestal body is not wearing the character's hairstyle");

            var block = new MaterialPropertyBlock();
            hair.Worn.GetComponent<MeshRenderer>().GetPropertyBlock(block, 0);
            Color worn = block.GetColor("_BaseColor");
            Assert.AreEqual(blue.b, worn.b, 0.001f, "the pedestal body's hair is not the character's colour");
            Assert.AreEqual(blue.r, worn.r, 0.001f);
#else
            yield break;
#endif
        }
    }
}
