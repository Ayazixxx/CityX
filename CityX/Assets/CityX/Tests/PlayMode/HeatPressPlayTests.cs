using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.UI;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Pressing a shirt (docs/HEAT_PRESS.md; owner, 2026-11-24: "make the whole thing go ahead ... visible across computers"): drawn on in the
    /// press screen and pressed, a worn shirt comes back worn with the print, a carried one back into the bag; another player's copy of the
    /// wearer wears the same print; another machine shows the shirt going through the press.
    /// </summary>
    public sealed class HeatPressPlayTests
    {
        private string _folder;
        private GameObject _ground;
        private GameObject _player;
        private GameObject _press;
        private GameObject _view;
        private GameObject _copy;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "cityx-press-" + System.Guid.NewGuid().ToString("N"));
            DesignLibrary.Store = new DesignStore(_folder);
            DesignLibrary.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            foreach (GameObject made in new[] { _view, _press, _player, _copy, _ground })
            {
                if (made != null) Object.Destroy(made);
            }
            DesignLibrary.Store = null;
            DesignLibrary.Reset();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private static GameObject Asset(string path)
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(asset, $"{path} is missing -- rebuild the scene");
            return asset;
#else
            return null;
#endif
        }

        private (Inventory inventory, HeatPress press, HeatPressView view) Make()
        {
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            _player = Object.Instantiate(Asset("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            AddInventory(_player);
            _press = Object.Instantiate(Asset("Assets/CityX/Wearables/HeatPress/HeatPress.prefab"), new Vector3(0.0f, 0.0f, 1.6f), Quaternion.Euler(0, 180, 0));
            _view = new GameObject("HeatPressView");
            return (_player.GetComponent<Inventory>(), _press.GetComponentInChildren<HeatPress>(), _view.AddComponent<HeatPressView>());
        }

        /// <summary>The inventory the scene builder gives the scene's player, with the shirts these tests press.</summary>
        public static Inventory AddInventory(GameObject player)
        {
            var inventory = player.AddComponent<Inventory>();
            inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.TShirt, Prefab = Asset("Assets/CityX/Wearables/TShirt.prefab") },
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.BlackTShirt, Prefab = Asset("Assets/CityX/Wearables/BlackTShirt.prefab") },
            };
            return inventory;
        }

        private static void Draw(HeatPressView view)
        {
            view.Canvas.BeginChange(ShirtDesignCanvas.Side.Front);
            view.Canvas.Stroke(ShirtDesignCanvas.Side.Front, new Vector2(100, 280), new Vector2(350, 280), 20.0f, Color.red, false);
            view.Canvas.BeginChange(ShirtDesignCanvas.Side.Back);
            view.Canvas.Stroke(ShirtDesignCanvas.Side.Back, new Vector2(224, 100), new Vector2(224, 450), 30.0f, Color.blue, false);
        }

        private static IEnumerator ThroughThePress()
        {
            for (float t = 0.0f; t < HeatPressCycle.TotalSeconds + 0.5f; t += 1.0f / 60.0f)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AWornShirtComesBackWornWithItsPrint()
        {
            (Inventory inventory, HeatPress press, HeatPressView view) = Make();
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.TShirt, _player));
            yield return null;
            view.Open(press, _player);
            Assert.AreEqual(1, view.Shirts.Count, "the worn shirt is not offered");
            Assert.AreEqual(TopLayers.Top, view.Shirts[0].WornSlot);
            Draw(view);
            Assert.IsTrue(view.PressIt(), view.Status);
            Assert.IsFalse(view.IsOpen, "the screen stayed open");
            Assert.AreEqual(string.Empty, inventory.EquippedItemId(TopLayers.Top), "the shirt is still worn while it is in the press");
            Assert.IsNotNull(press.ShirtShown, "no shirt on the drawer");
            Assert.IsTrue(press.Pressing);

            yield return ThroughThePress();
            Assert.AreEqual(ItemRegistry.TShirt, inventory.EquippedItemId(TopLayers.Top), "the shirt was not put back on");
            var shirt = inventory.EquippedInstance(TopLayers.Top).GetComponent<TShirt>();
            Assert.IsTrue(DesignStore.IsValidId(shirt.DesignId), "it came back without a design");
            Assert.IsTrue(shirt.ShowsDesign, "it does not show its design");
            Assert.IsTrue(DesignLibrary.Has(shirt.DesignId), "the design was not kept");
            Assert.IsNull(press.ShirtShown, "the shirt is still on the drawer too");
            Assert.AreEqual(shirt.DesignId, inventory.ToBag().Find(entry => entry.ItemId == ItemRegistry.TShirt).DesignId, "the save loses the design");
            CollectionAssert.Contains(inventory.VisibleItemIds(), ItemRegistry.TShirt + Inventory.DesignMark + shirt.DesignId, "other players are not told the design");
        }

        [UnityTest]
        public IEnumerator ACarriedShirtGoesBackIntoTheBagPrinted()
        {
            (Inventory inventory, HeatPress press, HeatPressView view) = Make();
            Assert.IsTrue(inventory.AddItem(ItemRegistry.BlackTShirt, 1));
            yield return null;
            view.Open(press, _player);
            Assert.AreEqual(1, view.Shirts.Count);
            Assert.GreaterOrEqual(view.Shirts[0].StorageIndex, 0, "the carried shirt is offered as worn");
            Draw(view);
            Assert.IsTrue(view.PressIt(), view.Status);
            Assert.IsFalse(inventory.HasItem(ItemRegistry.BlackTShirt), "the shirt is still in the bag while it is in the press");
            yield return ThroughThePress();
            int slot = System.Array.FindIndex(inventory.Storage, s => s.ItemId == ItemRegistry.BlackTShirt);
            Assert.GreaterOrEqual(slot, 0, "the shirt did not come back to the bag");
            Assert.IsTrue(DesignStore.IsValidId(inventory.Storage[slot].InstanceData.DesignId), "it came back without its design");

            // Pressed again, the print on it stays under the new ink: the new design holds both.
            string first = inventory.Storage[slot].InstanceData.DesignId;
            view.Open(press, _player);
            yield return null;
            Assert.IsTrue(view.Canvas.Shows(ShirtDesignCanvas.Side.Front), "what is printed on the shirt is not shown to print over");
            Assert.IsFalse(view.Canvas.HasInk);
            Assert.IsFalse(view.PressIt(), "pressed with nothing new on it");
            view.Canvas.BeginChange(ShirtDesignCanvas.Side.Front);
            view.Canvas.Stroke(ShirtDesignCanvas.Side.Front, new Vector2(224, 500), new Vector2(224, 500), 15.0f, Color.green, false);
            Assert.IsTrue(view.PressIt(), view.Status);
            yield return ThroughThePress();
            slot = System.Array.FindIndex(inventory.Storage, s => s.ItemId == ItemRegistry.BlackTShirt);
            string second = inventory.Storage[slot].InstanceData.DesignId;
            Assert.AreNotEqual(first, second);
            Assert.IsTrue(DesignLibrary.TryGet(second, out byte[] blob), "the second design was not kept");
            Assert.IsTrue(ShirtDesignBlob.TryUnpack(blob, out byte[] front, out _));
            Color32[] pixels = DesignPng.Pixels(front);
            Assert.Greater(pixels[280 * ShirtDesignArea.Width + 224].r, 200, "the first print was lost under the second");
            Assert.Greater(pixels[500 * ShirtDesignArea.Width + 224].g, 200, "the new ink is not in the design");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyWearsThePrintAndAnotherMachineShowsThePressing()
        {
            (Inventory inventory, HeatPress press, HeatPressView view) = Make();
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.TShirt, _player));
            yield return null;
            view.Open(press, _player);
            Draw(view);
            Assert.IsTrue(view.PressIt(), view.Status);
            string design = null;
            yield return ThroughThePress();
            design = inventory.EquippedInstance(TopLayers.Top).GetComponent<TShirt>().DesignId;

            // Another player's copy of the wearer is told the shirt with its design, and wears it printed. (Made outside a session, the networked
            // body says it was never started on a network, as in CopyClothesCapture.)
            LogAssert.ignoreFailingMessages = true;
            _copy = Object.Instantiate(Asset("Assets/CityX/Character/NetworkedPlayer.prefab"), new Vector3(5.0f, 0.0f, 0.0f), Quaternion.identity);
            yield return null;
            _copy.GetComponent<NetworkedEquipmentSync>().Show(new[] { ItemRegistry.TShirt + Inventory.DesignMark + design });
            yield return null;
            var worn = _copy.GetComponentInChildren<TShirt>();
            Assert.IsNotNull(worn, "the copy wears no shirt");
            Assert.AreEqual(design, worn.DesignId);
            Assert.IsTrue(worn.ShowsDesign, "the copy's shirt is plain");

            // Another machine's press shows a pressing it was told of: the shirt on the drawer, plain, then printed at the steam; nothing given.
            int carried = System.Array.FindAll(inventory.Storage, s => !s.IsEmpty).Length;
            press.ShowPressing(ItemRegistry.TShirt, string.Empty, design, inventory.PrefabForItem(ItemRegistry.TShirt));
            Assert.AreEqual(string.Empty, press.ShirtShown.DesignId);
            for (float t = 0.0f; t < HeatPressCycle.DoneAt + 0.2f; t += 1.0f / 60.0f) yield return null;
            Assert.AreEqual(design, press.ShirtShown.DesignId, "the shirt on the drawer is not printed at the steam");
            yield return ThroughThePress();
            Assert.IsNull(press.ShirtShown);
            Assert.AreEqual(carried, System.Array.FindAll(inventory.Storage, s => !s.IsEmpty).Length, "a pressing shown from elsewhere gave this player a shirt");
        }
    }
}
