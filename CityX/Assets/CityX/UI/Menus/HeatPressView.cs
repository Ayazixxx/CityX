using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Wearables;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// THE HEAT PRESS SCREEN (docs/HEAT_PRESS.md; owner, 2026-11-24: "make the whole thing go ahead. make the ui good, make it use images off your
    /// computer and visible across computers"). E at a press opens it over the game: the shirts you have (worn or carried) down the left, the
    /// chosen one in the middle as it looks from the front or the back with its print area marked, and the tools on the right: a brush and an
    /// eraser in any colour and size, and a picture off this computer (Windows' Open window), moved, sized and turned over the shirt before
    /// it is laid down. Undo and redo. PRESS lays the shirt in the press; it comes back printed, and everyone sees it so (HeatPress,
    /// DesignLibrary). What is printed on a shirt already stays: new ink goes over it.
    ///
    /// Built in code when the HUD wakes (PlayerHud adds it), as the graffiti canvas is: no prefab, no images. Its own canvas over everything;
    /// the player's controls and camera rest while it is open.
    /// </summary>
    public sealed partial class HeatPressView : MonoBehaviour
    {
        public enum Tool { Brush, Eraser, Picture }

        /// <summary>One shirt the player could press: worn in a slot, or in a storage slot.</summary>
        public struct ShirtChoice
        {
            public string ItemId;
            public string WornSlot;
            public int StorageIndex;
            public string DesignId;
            public string Label;
        }

        public const int SortingOrder = 58;
        public const int MinBrush = 1;
        public const int MaxBrush = 60;

        public static readonly Color DefaultColour = new Color(0.95f, 0.15f, 0.2f);

        /// <summary>The screen open now (null when none is).</summary>
        public static HeatPressView Current { get; private set; }

        public static bool IsAnyOpen => Current != null && Current.IsOpen;

        /// <summary>The frame it last closed on, so the Escape that closed it is not also read by another menu.</summary>
        public static int ClosedOnFrame { get; private set; } = -1;

        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>The design being made (for tests).</summary>
        public ShirtDesignCanvas Canvas { get; } = new ShirtDesignCanvas();

        public Tool CurrentTool { get; private set; } = Tool.Brush;

        public ShirtDesignCanvas.Side Side { get; private set; } = ShirtDesignCanvas.Side.Front;

        public Color Colour { get; private set; } = DefaultColour;

        public int Brush { get; private set; } = 8;

        /// <summary>The shirts offered, and which is chosen (-1: none).</summary>
        public IReadOnlyList<ShirtChoice> Shirts => _shirts;

        public int Chosen { get; private set; } = -1;

        /// <summary>The line under the shirt: what just happened, or what to do.</summary>
        public string Status => _status != null ? _status.text : string.Empty;

        /// <summary>The screen's own canvas (null until first opened): a capture draws it.</summary>
        public UnityEngine.Canvas Overlay => _root != null ? _root.GetComponent<UnityEngine.Canvas>() : null;

        private readonly List<ShirtChoice> _shirts = new List<ShirtChoice>();
        private readonly MenuFocus _focus = new MenuFocus();
        private HeatPress _press;
        private Inventory _inventory;
        private PlayerInputReader _reader;
        private ThirdPersonCamera _orbit;

        /// <summary>The shirt chosen's look from the front and the back, made once per shirt kind while open.</summary>
        private readonly Dictionary<string, Texture2D[]> _looks = new Dictionary<string, Texture2D[]>();

        private void OnEnable()
        {
            HeatPress.UseRequested += OnUseRequested;
        }

        private void OnDisable()
        {
            HeatPress.UseRequested -= OnUseRequested;
            if (IsOpen)
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            ForgetLooks();
            if (_designTexture != null)
            {
                Destroy(_designTexture);
            }
            if (_root != null)
            {
                Destroy(_root);   // its own root object, not under the HUD (see Build)
            }
            if (Current == this)
            {
                Current = null;
            }
        }

        private void OnUseRequested(HeatPress press, GameObject player)
        {
            if (IsOpen || player == null || GraffitiMenuView.IsAnyOpen || SkillMenuView.IsAnyOpen)
            {
                return;
            }
            var reader = player.GetComponent<PlayerInputReader>();
            if (reader == null || !reader.enabled)
            {
                return;   // only this machine's own player opens it
            }
            Open(press, player);
        }

        /// <summary>Opens the screen at <paramref name="press"/> for <paramref name="player"/> (public for tests).</summary>
        public void Open(HeatPress press, GameObject player)
        {
            Build();
            _press = press;
            _inventory = player != null ? player.GetComponent<Inventory>() : null;
            _reader = player != null ? player.GetComponent<PlayerInputReader>() : null;
            Camera main = Camera.main;
            _orbit = main != null ? main.GetComponent<ThirdPersonCamera>() : null;
            _shirts.Clear();
            _shirts.AddRange(ShirtsOf(_inventory));
            ShowShirtList();
            CancelPicture();
            Side = ShirtDesignCanvas.Side.Front;
            SetColour(Colour);
            UiKit.EnsureEventSystem();
            _root.SetActive(true);
            Current = this;
            _focus.Take(this, _reader, _orbit);
            Choose(_shirts.Count > 0 ? 0 : -1);
            if (_shirts.Count == 0)
            {
                Say("You have no T-shirt to print on. Bring one, worn or in your bag.");
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            CancelPicture();
            _root.SetActive(false);
            _focus.GiveBack(this, _reader, _orbit);
            ForgetLooks();
            ClosedOnFrame = Time.frameCount;
            if (Current == this)
            {
                Current = null;
            }
        }

        /// <summary>Every T-shirt the player has: worn (the top, or under a coat), then the bag's.</summary>
        public static List<ShirtChoice> ShirtsOf(Inventory inventory)
        {
            var shirts = new List<ShirtChoice>();
            if (inventory == null)
            {
                return shirts;
            }
            foreach (string slot in new[] { TopLayers.Top, TopLayers.Under })
            {
                GameObject worn = inventory.EquippedInstance(slot);
                var printable = worn != null ? worn.GetComponent<IPrintable>() : null;
                if (printable != null)
                {
                    string itemId = inventory.EquippedItemId(slot);
                    shirts.Add(new ShirtChoice { ItemId = itemId, WornSlot = slot, StorageIndex = -1, DesignId = printable.DesignId, Label = LabelOf(itemId, printable.DesignId, true) });
                }
            }
            for (int i = 0; i < inventory.Storage.Length; i++)
            {
                Inventory.StorageSlot stored = inventory.Storage[i];
                GameObject prefab = stored.IsEmpty ? null : inventory.PrefabForItem(stored.ItemId);
                if (prefab != null && prefab.GetComponent<IPrintable>() != null)
                {
                    string design = stored.InstanceData.DesignId ?? string.Empty;
                    shirts.Add(new ShirtChoice { ItemId = stored.ItemId, StorageIndex = i, DesignId = design, Label = LabelOf(stored.ItemId, design, false) });
                }
            }
            return shirts;
        }

        private static string LabelOf(string itemId, string designId, bool worn)
        {
            string name = ItemRegistry.TryGet(itemId, out ItemDefinition definition) ? definition.DisplayName : itemId;
            string where = worn ? "WORN" : "BAG";
            return string.IsNullOrEmpty(designId) ? $"{name}\n<size=11>{where}</size>" : $"{name}\n<size=11>{where} · PRINTED</size>";
        }

        /// <summary>Chooses the shirt to print on: its look, and what is printed on it already under the new ink.</summary>
        public void Choose(int index)
        {
            Chosen = index >= 0 && index < _shirts.Count ? index : -1;
            CancelPicture();
            Canvas.Begin(null, null);
            ShowShirtList();
            if (Chosen < 0)
            {
                ShowLook(null);
                return;
            }
            ShirtChoice shirt = _shirts[Chosen];
            ShowLook(LookOf(shirt.ItemId));
            if (!string.IsNullOrEmpty(shirt.DesignId))
            {
                int chosen = Chosen;
                DesignLibrary.Get(shirt.DesignId, blob =>
                {
                    if (this == null || !IsOpen || Chosen != chosen || Canvas.HasInk)
                    {
                        return;
                    }
                    if (ShirtDesignBlob.TryUnpack(blob, out byte[] front, out byte[] back))
                    {
                        Canvas.Begin(DesignPng.Pixels(front), DesignPng.Pixels(back));
                    }
                });
            }
            Say(Hint);
        }

        public const string Hint = "Draw on the shirt inside the dotted line, or add a picture. Tab turns it round.";

        /// <summary>
        /// Presses the chosen shirt with the design made: the design is kept and sent to everyone (DesignLibrary.Publish), and the shirt goes
        /// into the press. False, with why under the shirt, when it cannot.
        /// </summary>
        public bool PressIt()
        {
            if (Chosen < 0 || _press == null)
            {
                return false;
            }
            if (_placing)
            {
                PlacePicture();
            }
            if (!Canvas.HasInk)
            {
                Say("Draw something or add a picture first.");
                return false;
            }
            byte[] front = DesignPng.Encode(Canvas.Composite(ShirtDesignCanvas.Side.Front));
            byte[] back = DesignPng.Encode(Canvas.Composite(ShirtDesignCanvas.Side.Back));
            string designId = front != null && back != null ? DesignLibrary.Publish(ShirtDesignBlob.Pack(front, back)) : null;
            if (designId == null)
            {
                Say("That design is too heavy a picture to print. Try a smaller or simpler one.");
                return false;
            }
            ShirtChoice shirt = _shirts[Chosen];
            if (!_press.PressShirt(_inventory, shirt.ItemId, shirt.WornSlot, shirt.StorageIndex, designId))
            {
                Say(string.IsNullOrEmpty(shirt.WornSlot) ? "That shirt is not in your bag any more." : "Make room in your bag to take that shirt off first.");
                return false;
            }
            Close();
            return true;
        }

        private Texture2D[] LookOf(string itemId)
        {
            if (_looks.TryGetValue(itemId, out Texture2D[] look))
            {
                return look;
            }
            GameObject prefab = _inventory != null ? _inventory.PrefabForItem(itemId) : null;
            var shirt = prefab != null ? prefab.GetComponent<TShirt>() : null;
            if (shirt == null || shirt.GarmentMesh == null || shirt.GarmentMaterial == null)
            {
                return null;
            }
            look = new[] { Draw(shirt, true), Draw(shirt, false) };
            _looks[itemId] = look;
            return look;
        }

        /// <summary>The shirt from one side, at the stage's scale, in its own cloth.</summary>
        private static Texture2D Draw(TShirt shirt, bool front)
        {
            Material material = shirt.GarmentMaterial;
            bool textured = material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null;
            Color32[] cloth = textured ? PrintedShirt.Cloth(material) : null;
            Color plain = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            int width = Mathf.RoundToInt(StageSize.x), height = Mathf.RoundToInt(StageSize.y);
            Color32[] pixels = ShirtSilhouette.Draw(shirt.GarmentMesh, front, cloth, PrintedShirt.TextureSize, plain, width, height,
                StagePixelsPerMetre, new Vector2(0.0f, StageMiddleHeight));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Shirt " + (front ? "front" : "back"), wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void ForgetLooks()
        {
            foreach (Texture2D[] look in _looks.Values)
            {
                foreach (Texture2D side in look)
                {
                    if (side != null) Destroy(side);
                }
            }
            _looks.Clear();
        }

        private void Say(string line)
        {
            if (_status != null)
            {
                _status.text = line;
            }
        }
    }
}
