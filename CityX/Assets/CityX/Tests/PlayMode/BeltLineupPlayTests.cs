using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The figures of the character list's row on the belt (<see cref="BeltLineup"/>), on a stage of their own: each on the stage's layer
    /// (the stage's camera sees nothing else), dressed, its hair its colour, idling with its soles still, silent, with nothing to collide;
    /// carried by the belt; a deleted one's place left empty.
    /// </summary>
    public sealed class BeltLineupPlayTests
    {
        private const int StageLayer = 1;   // not Default: a figure left on the prefab's own layer would be missed
        private GameObject _stage;
        private BeltLineup _lineup;
        private ConveyorBelt _belt;

        [SetUp]
        public void MakeTheStage()
        {
            _stage = new GameObject("Stage") { layer = StageLayer };
            var belt = new GameObject("Belt") { layer = StageLayer };
            belt.transform.SetParent(_stage.transform, false);
            _belt = belt.AddComponent<ConveyorBelt>();
            _lineup = _stage.AddComponent<BeltLineup>();
            _lineup.Belt = _belt;
#if UNITY_EDITOR
            _lineup.BodyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
#endif
        }

        [TearDown]
        public void TakeTheStageDown()
        {
            Object.Destroy(_stage);
        }

        private static CharacterSave Character(string name) => CharacterSave.Create(name, new Color(0.3f, 0.4f, 0.5f));

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AFigureStandsOnTheStagesLayerAliveWithItsArmsDownAndItsSolesStill()
        {
            Time.captureFramerate = 60;
            _lineup.Show(new List<CharacterSave> { Character("Ayaz") });
            GameObject figure = _lineup.FigureAt(0);
            Assert.IsNotNull(figure, "the middle character has no figure");
            foreach (Transform part in figure.GetComponentsInChildren<Transform>(true))
            {
                Assert.AreEqual(StageLayer, part.gameObject.layer, $"{part.name} is off the stage's layer, where its camera would never see it");
            }
            yield return Frames(10);

            Animator animator = figure.GetComponentInChildren<Animator>();
            Assert.IsTrue(animator.enabled, "the figure is held still, not idling");
            Assert.Less(animator.GetBoneTransform(HumanBodyBones.LeftHand).position.y,
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y - 0.3f, "its arms are out, not down at its sides");
            Assert.IsNotNull(figure.GetComponent<LineupFigure>().Feet, "its feet were never set down");
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot), right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 leftAt = left.position, rightAt = right.position;
            float drift = 0.0f;
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                drift = Mathf.Max(drift, Mathf.Max(Vector3.Distance(leftAt, left.position), Vector3.Distance(rightAt, right.position)));
            }
            Assert.Less(drift, 0.02f, "its feet glide over the belt as it idles");
        }

        /// <summary>A figure is to be looked at: PlayerAudio starts its looping city air inside Instantiate, so its source must go too.</summary>
        [UnityTest]
        public IEnumerator NoSoundColliderOrPhysicsSurvivesOnAFigure()
        {
            _lineup.Show(new List<CharacterSave> { Character("Ayaz") });
            yield return null;
            GameObject figure = _lineup.FigureAt(0);
            Assert.IsEmpty(figure.GetComponentsInChildren<AudioSource>(true), "a figure carries a sound");
            Assert.IsEmpty(figure.GetComponentsInChildren<Rigidbody>(true), "a figure carries a ragdoll body");
            Assert.IsEmpty(figure.GetComponentsInChildren<CharacterController>(true));
            Assert.IsNull(figure.GetComponentInChildren<CharacterIdentity>(true), "a figure could count play time or save");
        }

        /// <summary>A saved character's clothes are on their figure (not a weapon), on the stage's layer.</summary>
        [UnityTest]
        public IEnumerator ACharactersClothesAreOnTheirFigure()
        {
            CharacterSave save = Character("Ayaz");
            save.Wearing = new List<string> { ItemRegistry.RedCamoTShirt, ItemRegistry.KnightHelmet, ItemRegistry.Pistol };
#if UNITY_EDITOR
            _lineup.ClothingPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry
                {
                    ItemId = ItemRegistry.RedCamoTShirt,
                    Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/RedCamoTShirt.prefab"),
                },
                new Inventory.ItemPrefabEntry
                {
                    ItemId = ItemRegistry.KnightHelmet,
                    Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/KnightHelmet.prefab"),
                },
                new Inventory.ItemPrefabEntry
                {
                    ItemId = ItemRegistry.Pistol,
                    Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Pistol.prefab"),
                },
            };
#endif
            _lineup.Show(new List<CharacterSave> { save });
            yield return null;

            GameObject figure = _lineup.FigureAt(0);
            var clothes = figure.GetComponent<CityX.Wearables.SkinnedGarmentCombiner>();
            Assert.IsNotNull(clothes, "the figure wears nothing");
            Assert.IsTrue(clothes.IsShowing(ItemRegistry.RedCamoTShirt), "the figure is not wearing the T-shirt");
            var helmet = figure.GetComponentInChildren<CityX.Wearables.KnightHelmet>(true);
            Assert.IsNotNull(helmet, "the figure is not wearing the helmet");
            Assert.AreEqual(figure.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head), helmet.transform.parent, "the helmet is not on the head");
            Assert.IsNull(figure.GetComponentInChildren<CityX.Weapons.PistolWeapon>(true), "a weapon was put on as clothes");
            foreach (Renderer renderer in figure.GetComponentsInChildren<Renderer>(true))
            {
                Assert.AreEqual(StageLayer, renderer.gameObject.layer, $"{renderer.name} is off the stage's layer");
            }
        }

        /// <summary>A saved character's hair colour is on the hair of their figure, not the default.</summary>
        [UnityTest]
        public IEnumerator ACharactersHairColourShowsOnTheirFigure()
        {
#if UNITY_EDITOR
            _lineup.HairStyleMeshes = new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/Hair1Mesh.asset") };
            _lineup.HairMaterialAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/Hair.mat");
#endif
            var blue = new Color(0.2f, 0.4f, 0.85f);
            CharacterSave save = Character("Ayaz");
            save.HairStyle = CharacterHair.Hair1;
            save.HairColour = blue;
            _lineup.Show(new List<CharacterSave> { save });
            yield return null;

            var hair = _lineup.FigureAt(0).GetComponent<CharacterHair>();
            Assert.IsNotNull(hair, "the figure has no hair");
            Assert.IsNotNull(hair.Worn, "the figure is not wearing the character's hairstyle");
            var block = new MaterialPropertyBlock();
            hair.Worn.GetComponent<MeshRenderer>().GetPropertyBlock(block, 0);
            Color worn = block.GetColor("_BaseColor");
            Assert.AreEqual(blue.b, worn.b, 0.001f, "the figure's hair is not the character's colour");
            Assert.AreEqual(blue.r, worn.r, 0.001f);
        }

        /// <summary>The next character stands on the screen's right (the stage's -X: its cameras look back along -Z), and the row rides the belt.</summary>
        [UnityTest]
        public IEnumerator TheNextStandsOnTheRightAndTheRowRidesTheBelt()
        {
            _lineup.Origin = _belt.Travel;
            _lineup.Show(new List<CharacterSave> { Character("A"), Character("B"), Character("C"), Character("D") });
            yield return Frames(4);   // the ones out of sight, one a frame
            for (int slot = 0; slot < 4; slot++)
            {
                Assert.IsNotNull(_lineup.FigureAt(slot), $"slot {slot} was never made");
                Assert.AreEqual(-slot * _lineup.Spacing, _lineup.FigureAt(slot).transform.localPosition.x, 1e-4f, $"slot {slot} is not in its place");
            }
            _belt.Travel += 1.5f;
            yield return null;
            Assert.AreEqual(-1.5f, _lineup.FigureAt(0).transform.localPosition.x, 1e-4f, "the row did not ride the belt to the right");
            Assert.AreEqual(_lineup.Spacing + 1.5f, _lineup.Along(1), 1e-4f, "the next one does not ride along with the first");
        }

        /// <summary>A deleted character's figure goes and its spot stays empty; nobody else moves.</summary>
        [UnityTest]
        public IEnumerator EmptyingASlotTakesOnlyItsFigure()
        {
            _lineup.Origin = _belt.Travel;
            _lineup.Show(new List<CharacterSave> { Character("A"), Character("B"), Character("C") });
            yield return Frames(3);
            float third = _lineup.FigureAt(2).transform.localPosition.x;
            _lineup.Empty(1);
            yield return null;
            Assert.IsTrue(_lineup.FigureAt(1) == null, "the deleted character still stands there");
            Assert.AreEqual(3, _lineup.Count, "the row closed up: everybody past the gap would jump");
            Assert.AreEqual(third, _lineup.FigureAt(2).transform.localPosition.x, 1e-4f);

            _lineup.ClearLeftOf(0.5f * _lineup.Spacing);
            yield return null;
            Assert.IsTrue(_lineup.FigureAt(0) == null, "the one left of the line was kept");
            Assert.IsNotNull(_lineup.FigureAt(2), "one right of the line went too");
        }
    }
}
