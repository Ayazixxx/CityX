using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.UI;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Looking at an item and inspecting it through the real input path (docs/ITEM_INSPECT.md): the prompt names the knife at the feet and
    /// its keys; F opens the appraisal card beside it and the camera glides in without a jump, the controls going to the card; F closes it and
    /// the camera is back exactly where it was; E takes the knife; a flashlight's F stands aside for a card; the arrows compare with the hand.
    /// </summary>
    public sealed class ItemInspectPlayTests : InputTestFixture
    {
        private const string PlayerPath = "Assets/CityX/Character/Player.prefab";

        private Keyboard _keyboard;
        private GameObject _ground;
        private GameObject _player;
        private GameObject _cameraObject;
        private Inventory _inventory;
        private ItemInspector _inspector;
        private ItemPromptView _prompt;
        private ItemCardView _card;
        private DroppedItem _knife;
        private readonly List<GameObject> _made = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            DroppedItems.Shared.Clear();
            DroppedItems.Shared.Relay = null;
            DroppedItems.Shared.PrefabFor = Inventory.FindPrefab;
            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            DroppedItems.Shared.Clear();
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
            if (_ground != null) Object.DestroyImmediate(_ground);
            base.TearDown();
        }

        private static GameObject Load(string path)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, "no " + path + ": rebuild the scene first");
            return prefab;
#else
            return null;
#endif
        }

        private static Inventory.ItemPrefabEntry Entry(string itemId, string prefabName) =>
            new Inventory.ItemPrefabEntry { ItemId = itemId, Prefab = Load("Assets/CityX/Weapons/" + prefabName + ".prefab") };

        /// <summary>The real player with the knife prefab in its inventory, its camera behind it, the prompt and the card, and a knife at its feet.</summary>
        private IEnumerator Rig()
        {
            _player = Object.Instantiate(Load(PlayerPath), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _made.Add(_player);
            _inventory = _player.AddComponent<Inventory>();
            _inventory.ItemPrefabs = new[]
            {
                Entry(ItemRegistry.KitchenKnife, "KitchenKnife"), Entry(ItemRegistry.CombatSword, "CombatSword"), Entry(ItemRegistry.Flashlight, "Flashlight"),
            };
            _cameraObject = new GameObject("Camera") { tag = "MainCamera" };
            _made.Add(_cameraObject);
            _cameraObject.AddComponent<Camera>();
            _cameraObject.AddComponent<ThirdPersonCamera>().Target = _player.transform;
            _player.GetComponent<PlayerInputReader>().YawSource = _cameraObject.transform;
            _inspector = _player.GetComponent<ItemInspector>();
            Assert.IsNotNull(_inspector, "the player has no ItemInspector");
            _prompt = new GameObject("ItemPrompt").AddComponent<ItemPromptView>();
            _made.Add(_prompt.gameObject);
            _card = new GameObject("ItemCard").AddComponent<ItemCardView>();
            _made.Add(_card.gameObject);
            _knife = DroppedItems.Shared.Drop(new ItemDrop
            {
                ItemId = ItemRegistry.KitchenKnife,
                Data = ItemInstanceData.None,
                Position = new Vector3(0.0f, 0.3f, 1.8f),
                Rotation = Quaternion.identity,
                Placed = true,
            }, false);
            Assert.IsNotNull(_knife, "the knife was not put down");
            for (int i = 0; i < 40; i++) yield return null;   // the camera settles behind the body, the views find the player
        }

        private IEnumerator Tap(KeyControl key)
        {
            Press(key);
            yield return null;
            yield return null;
            Release(key);
            yield return null;
            yield return null;
        }

        private IEnumerator UntilOpen()
        {
            for (int i = 0; i < 120 && !(_inspector.IsOpen && _inspector.Progress >= 1.0f); i++) yield return null;
            Assert.IsTrue(_inspector.IsOpen && _inspector.Progress >= 1.0f, "the card never finished opening");
        }

        [UnityTest]
        public IEnumerator ThePromptNamesTheKnifeAndItsKeys()
        {
            yield return Rig();
            Assert.AreSame(_knife, _inspector.Hovered, "the knife at the feet is not what the player is looking at");
            Assert.IsTrue(_prompt.Showing, "no prompt for the knife in reach");
            Assert.AreEqual("Kitchen Knife", _prompt.NameText);
            Assert.AreEqual(ItemPromptView.ItemKeys, _prompt.KeysText);

            var controller = _player.GetComponent<CharacterController>();
            controller.enabled = false;
            _player.transform.position = new Vector3(0.0f, 0.05f, -30.0f);
            controller.enabled = true;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(_prompt.Showing, "the prompt stayed up with the knife far off");
        }

        [UnityTest]
        public IEnumerator FOpensTheCardAndTheCameraGlidesInThenBackWithoutAJump()
        {
            yield return Rig();
            Vector3 start = _cameraObject.transform.position;
            Press(_keyboard.fKey);
            yield return null;
            Release(_keyboard.fKey);
            var positions = new List<Vector3> { start };
            for (int i = 0; i < 120 && !(_inspector.IsOpen && _inspector.Progress >= 1.0f); i++)
            {
                yield return null;
                positions.Add(_cameraObject.transform.position);
            }
            Assert.IsTrue(_inspector.IsOpen, "F did not open the card");
            Assert.IsFalse(_player.GetComponent<PlayerInputReader>().TakesKeys, "the controls should be the card's while it is open");
            float total = Vector3.Distance(start, _inspector.Shot.CameraPosition);
            Assert.Greater(total, 1.0f, "the camera should have somewhere to glide to");
            float largest = 0.0f;
            for (int i = 1; i < positions.Count; i++) largest = Mathf.Max(largest, Vector3.Distance(positions[i], positions[i - 1]));
            Assert.Less(Vector3.Distance(positions[1], start), 0.1f * total, "the first frame after F is a jump");
            Assert.Less(largest, 0.2f * total, "a frame of the glide jumps");
            Assert.Less(Vector3.Distance(_cameraObject.transform.position, _inspector.Shot.CameraPosition), 0.05f, "the camera did not arrive at the card");
            Assert.Greater(positions.Count, 10, "the glide should take many frames, not a cut");

            Assert.IsTrue(_card.Showing, "no card is shown");
            Assert.AreEqual("Kitchen Knife", _card.Card.Name);
            string text = _card.AllText;
            StringAssert.Contains("KITCHEN KNIFE", text);
            StringAssert.Contains("Knife / One Hand", text);
            StringAssert.Contains("Attack: 7", text);
            StringAssert.Contains(ItemCardView.Title, text);
            Assert.AreEqual(1.0f / ItemCardView.UnitsPerMetre, ((RectTransform)_card.transform.GetChild(0)).localScale.x, 1e-6f, "the card is not fully unfolded");
            Assert.IsFalse(_prompt.Showing, "the prompt should hide behind the card");

            Press(_keyboard.fKey);
            yield return null;
            Release(_keyboard.fKey);
            for (int i = 0; i < 120 && _inspector.IsOpen; i++) yield return null;
            Assert.IsFalse(_inspector.IsOpen, "F did not close the card");
            Assert.Less(Vector3.Distance(_cameraObject.transform.position, start), 0.05f, "the camera did not glide back to where it was");
            Assert.IsTrue(_player.GetComponent<PlayerInputReader>().TakesKeys, "the controls were not given back");
            Assert.IsFalse(_card.Showing);
            yield return null;
            yield return null;
            Assert.IsTrue(_prompt.Showing, "the prompt should be back once the card is away");
        }

        [UnityTest]
        public IEnumerator EWhileInspectingTakesTheKnife()
        {
            yield return Rig();
            _inspector.Open(_knife);
            yield return UntilOpen();
            yield return Tap(_keyboard.eKey);
            // A weapon picked up with an empty hand goes straight into it, not into storage.
            Assert.IsTrue(_inventory.HasItem(ItemRegistry.KitchenKnife) || _inventory.EquippedItemId(Inventory.PrimarySlot) == ItemRegistry.KitchenKnife,
                "E on the card did not take the knife");
            Assert.AreEqual(0, DroppedItems.Shared.Count, "the knife is still lying there");
            for (int i = 0; i < 120 && _inspector.IsOpen; i++) yield return null;
            Assert.IsFalse(_inspector.IsOpen, "the card should close once the item is taken");
        }

        [UnityTest]
        public IEnumerator FWithAFlashlightInHandInspectsInsteadOfSwitchingTheLight()
        {
            yield return Rig();
            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.Flashlight, _player), "the flashlight was not equipped");
            yield return null;
            var torch = _player.GetComponentInChildren<FlashlightItem>();
            Assert.IsNotNull(torch, "no flashlight in the hand");
            bool wasLit = torch.IsLit;
            Assert.IsTrue(_inspector.ClaimsF, "F should be the card's with a knife in reach");
            Press(_keyboard.fKey);
            yield return null;
            Release(_keyboard.fKey);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(_inspector.IsOpen, "F did not open the card with a flashlight in hand");
            Assert.AreEqual(wasLit, torch.IsLit, "F switched the flashlight as well as opening the card");
        }

        [UnityTest]
        public IEnumerator TheCardStandsClearOfAWallBesideTheItem()
        {
            yield return Rig();
            // A wall just to the right of the knife (the viewer looks along +z, so the right is +x), where the card would otherwise stand.
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(wall);
            wall.transform.position = _knife.Centre + new Vector3(0.6f, 1.0f, 0.0f);
            wall.transform.localScale = new Vector3(0.2f, 3.0f, 4.0f);
            Physics.SyncTransforms();
            _inspector.Open(_knife);
            yield return UntilOpen();
            InspectShot shot = _inspector.Shot;
            Assert.AreNotEqual(CardSide.Right, shot.Side, "the card went into the wall on the right");
            Assert.IsFalse(Physics.CheckBox(shot.CardPosition, InspectShot.CardHalfExtents, shot.CardRotation, ~0, QueryTriggerInteraction.Ignore)
                           && wall.GetComponent<Collider>().bounds.Intersects(new Bounds(shot.CardPosition, InspectShot.CardHalfExtents * 2.0f)),
                "the card stands inside the wall");
            Assert.IsTrue(_card.Showing);
        }

        [UnityTest]
        public IEnumerator TheArrowsCompareWithTheSwordInHand()
        {
            yield return Rig();
            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.CombatSword, _player), "the sword was not equipped");
            yield return null;
            _inspector.Open(_knife);
            yield return UntilOpen();
            string text = _card.AllText;
            StringAssert.Contains("Attack: 7 " + ItemCardView.Worse, text);
            StringAssert.Contains("Quick (x1.3) " + ItemCardView.Better, text);
        }
    }
}
