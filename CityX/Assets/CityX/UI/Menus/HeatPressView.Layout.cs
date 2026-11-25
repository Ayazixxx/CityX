using System.Collections.Generic;
using CityX.Wearables;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The heat press screen's layout, on the HUD's 1152 x 648 design screen (docs/HEAT_PRESS.md): the shirts down the left, the shirt in the
    /// middle (front or back) with its print area dotted round, the tools on the right, BACK and PRESS along the bottom.
    /// </summary>
    public sealed partial class HeatPressView
    {
        /// <summary>The shirt's stage: its size on the screen, how many design pixels a metre of shirt is, and the height its middle shows.</summary>
        public static readonly Vector2 StageSize = new Vector2(460.0f, 517.0f);

        public const float StagePixelsPerMetre = 470.0f;
        public const float StageMiddleHeight = 1.55f;
        public static readonly Vector2 StageAt = new Vector2(-40.0f, -4.0f);

        private const float ToolsX = 405.0f;
        private const float ListX = -452.0f;

        private static readonly Color Backdrop = new Color(0.02f, 0.02f, 0.03f, 0.82f);
        private static readonly Color PanelColour = new Color(0.09f, 0.09f, 0.11f, 0.96f);
        private static readonly Color StageColour = new Color(0.30f, 0.31f, 0.34f, 1.0f);
        private static readonly Color ButtonColour = new Color(0.17f, 0.17f, 0.20f, 1.0f);
        private static readonly Color ChosenColour = new Color(0.96f, 0.78f, 0.16f, 1.0f);
        private static readonly Color PlaceColour = new Color(0.20f, 0.46f, 0.22f, 1.0f);
        private static readonly Color Ink = new Color(0.93f, 0.91f, 0.86f, 1.0f);
        private static readonly Color DarkInk = new Color(0.08f, 0.08f, 0.09f, 1.0f);
        private static readonly Color Dotted = new Color(1.0f, 1.0f, 1.0f, 0.55f);

        private GameObject _root;
        private Text _status;
        private RawImage _look;
        private RawImage _design;
        private Texture2D _designTexture;
        private RawImage _pictureImage;
        private RawImage _square;
        private RawImage _hueStrip;
        private Image _swatch;
        private Text _brushLabel;
        private RectTransform _list;
        private GameObject _pictureControls;
        private GameObject _keysHint;
        private Button _pressButton;
        private readonly Dictionary<Tool, Image> _toolButtons = new Dictionary<Tool, Image>();
        private readonly Image[] _sideTabs = new Image[2];

        private void Build()
        {
            if (_root != null)
            {
                return;
            }
            // A root canvas of its own, as the graffiti canvas is: inside the HUD's it would fade with the HUD.
            _root = new GameObject("HeatPressCanvas", typeof(RectTransform));
            if (gameObject.scene.IsValid())
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_root, gameObject.scene);
            }
            var canvas = _root.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = SkillMenuView.DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _root.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)_root.transform;

            Image backdrop = UiKit.MakeImage("Backdrop", root, Backdrop);
            UiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;   // nothing behind the screen takes a click

            BuildShirtColumn(root);
            BuildStage(root);
            BuildTools(root);

            _status = UiKit.MakeLabel("Status", root, string.Empty, 14, TextAnchor.MiddleCenter, Ink);
            UiKit.Place(_status.rectTransform, new Vector2(StageAt.x, -292.0f), new Vector2(560.0f, 24.0f));
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;

            Button back = ArrowButtons.Curled(root, "Back", "BACK", new Vector2(150.0f, 70.0f), ArrowButtons.RedTop, ArrowButtons.RedBottom);
            UiKit.Place((RectTransform)back.transform, new Vector2(ListX, -262.0f), new Vector2(150.0f, 70.0f));
            back.onClick.AddListener(Close);
            _pressButton = ArrowButtons.Straight(root, "Press", "PRESS", new Vector2(220.0f, 70.0f), ArrowButtons.GreenTop, ArrowButtons.GreenBottom);
            UiKit.Place((RectTransform)_pressButton.transform, new Vector2(ToolsX, -262.0f), new Vector2(220.0f, 70.0f));
            _pressButton.onClick.AddListener(() => PressIt());

            _root.SetActive(false);
        }

        private void BuildShirtColumn(RectTransform root)
        {
            Image panel = UiKit.MakeImage("ShirtPanel", root, PanelColour);
            UiKit.Place(panel.rectTransform, new Vector2(ListX, 22.0f), new Vector2(220.0f, 520.0f));
            Text title = UiKit.MakeLabel("Title", root, "HEAT PRESS", 26, TextAnchor.MiddleCenter, ChosenColour);
            UiKit.Place(title.rectTransform, new Vector2(ListX, 300.0f), new Vector2(220.0f, 34.0f));
            Text shirts = UiKit.MakeLabel("ShirtsLabel", root, "YOUR SHIRTS", 15, TextAnchor.MiddleCenter, Ink);
            UiKit.Place(shirts.rectTransform, new Vector2(ListX, 262.0f), new Vector2(220.0f, 24.0f));
            _list = UiKit.NewChild("Shirts", root);
            UiKit.Place(_list, new Vector2(ListX, 22.0f), new Vector2(220.0f, 500.0f));
        }

        private void BuildStage(RectTransform root)
        {
            Image frame = UiKit.MakeImage("Stage", root, StageColour);
            UiKit.Place(frame.rectTransform, StageAt, StageSize + new Vector2(10.0f, 10.0f));
            _look = UiKit.MakeRaw("Shirt", root, null, Color.white);
            UiKit.Place(_look.rectTransform, StageAt, StageSize);

            // The print area: the design over the shirt, and a dotted line round it.
            var areaSize = new Vector2(ShirtDesignArea.WidthMetres, ShirtDesignArea.HeightMetres) * StagePixelsPerMetre;
            Vector2 areaAt = StageAt + new Vector2(0.0f, (ShirtDesignArea.MiddleHeight - StageMiddleHeight) * StagePixelsPerMetre);
            _designTexture = new Texture2D(ShirtDesignArea.Width, ShirtDesignArea.Height, TextureFormat.RGBA32, false)
            {
                name = "HeatPressDesign", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            _design = UiKit.MakeRaw("Design", root, _designTexture, Color.white);
            UiKit.Place(_design.rectTransform, areaAt, areaSize);
            DottedBox(root, areaAt, areaSize);
            _pictureImage = UiKit.MakeRaw("Picture", _design.rectTransform, null, new Color(1.0f, 1.0f, 1.0f, 0.85f));
            _pictureImage.gameObject.SetActive(false);

            for (int side = 0; side < 2; side++)
            {
                int which = side;
                Button tab = UiKit.MakeButton(root, side == 0 ? "FRONT" : "BACK", StageAt + new Vector2(-62.0f + side * 124.0f, StageSize.y * 0.5f + 22.0f),
                    new Vector2(116.0f, 28.0f), ButtonColour, Ink, () =>
                    {
                        if ((int)Side != which) TurnShirt();
                    });
                _sideTabs[side] = (Image)tab.targetGraphic;
            }
        }

        /// <summary>A dotted rectangle (dashes along each side).</summary>
        private static void DottedBox(RectTransform root, Vector2 centre, Vector2 size)
        {
            const float dash = 7.0f, gap = 6.0f, thickness = 2.0f;
            for (float along = 0.0f; along < size.x; along += dash + gap)
            {
                float length = Mathf.Min(dash, size.x - along);
                float x = centre.x - size.x * 0.5f + along + length * 0.5f;
                UiKit.Place(UiKit.MakeImage("Dash", root, Dotted).rectTransform, new Vector2(x, centre.y + size.y * 0.5f), new Vector2(length, thickness));
                UiKit.Place(UiKit.MakeImage("Dash", root, Dotted).rectTransform, new Vector2(x, centre.y - size.y * 0.5f), new Vector2(length, thickness));
            }
            for (float along = 0.0f; along < size.y; along += dash + gap)
            {
                float length = Mathf.Min(dash, size.y - along);
                float y = centre.y - size.y * 0.5f + along + length * 0.5f;
                UiKit.Place(UiKit.MakeImage("Dash", root, Dotted).rectTransform, new Vector2(centre.x - size.x * 0.5f, y), new Vector2(thickness, length));
                UiKit.Place(UiKit.MakeImage("Dash", root, Dotted).rectTransform, new Vector2(centre.x + size.x * 0.5f, y), new Vector2(thickness, length));
            }
        }

        private void BuildTools(RectTransform root)
        {
            Image panel = UiKit.MakeImage("ToolPanel", root, PanelColour);
            UiKit.Place(panel.rectTransform, new Vector2(ToolsX, 30.0f), new Vector2(250.0f, 504.0f));
            Text label = UiKit.MakeLabel("ToolsLabel", root, "TOOLS", 15, TextAnchor.MiddleCenter, Ink);
            UiKit.Place(label.rectTransform, new Vector2(ToolsX, 262.0f), new Vector2(220.0f, 24.0f));

            ToolButton(root, Tool.Brush, "BRUSH", new Vector2(ToolsX - 58.0f, 226.0f), new Vector2(108.0f, 34.0f));
            ToolButton(root, Tool.Eraser, "ERASER", new Vector2(ToolsX + 58.0f, 226.0f), new Vector2(108.0f, 34.0f));
            ToolButton(root, Tool.Picture, "ADD PICTURE...", new Vector2(ToolsX, 186.0f), new Vector2(224.0f, 34.0f));

            _square = UiKit.MakeRaw("Colours", root, CreatorControls.BuildSquare(_hue), Color.white);
            UiKit.Place(_square.rectTransform, new Vector2(ToolsX + 20.0f, 92.0f), new Vector2(184.0f, 136.0f));
            _square.raycastTarget = true;
            _hueStrip = UiKit.MakeRaw("Hue", root, CreatorControls.BuildHueStrip(), Color.white);
            UiKit.Place(_hueStrip.rectTransform, new Vector2(ToolsX + 20.0f, 10.0f), new Vector2(184.0f, 16.0f));
            _hueStrip.raycastTarget = true;
            _swatch = UiKit.MakeImage("Swatch", root, Colour);
            UiKit.Place(_swatch.rectTransform, new Vector2(ToolsX - 96.0f, 92.0f), new Vector2(30.0f, 136.0f));

            UiKit.MakeButton(root, "-", new Vector2(ToolsX - 90.0f, -34.0f), new Vector2(40.0f, 32.0f), ButtonColour, Ink, () => SetBrush(Brush - 2));
            _brushLabel = UiKit.MakeLabel("BrushSize", root, string.Empty, 15, TextAnchor.MiddleCenter, Ink);
            UiKit.Place(_brushLabel.rectTransform, new Vector2(ToolsX, -34.0f), new Vector2(130.0f, 32.0f));
            UiKit.MakeButton(root, "+", new Vector2(ToolsX + 90.0f, -34.0f), new Vector2(40.0f, 32.0f), ButtonColour, Ink, () => SetBrush(Brush + 2));

            UiKit.MakeButton(root, "UNDO", new Vector2(ToolsX - 58.0f, -76.0f), new Vector2(108.0f, 32.0f), ButtonColour, Ink, Canvas.Undo);
            UiKit.MakeButton(root, "REDO", new Vector2(ToolsX + 58.0f, -76.0f), new Vector2(108.0f, 32.0f), ButtonColour, Ink, Canvas.Redo);
            UiKit.MakeButton(root, "CLEAR THIS SIDE", new Vector2(ToolsX, -116.0f), new Vector2(224.0f, 32.0f), ButtonColour, Ink, () => Canvas.ClearInk(Side));
            Text keys = UiKit.MakeLabel("Keys", root, "Alt+click picks a colour  ·  [ ] size\nCtrl+Z undo  ·  Tab turns the shirt", 11,
                TextAnchor.MiddleCenter, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            UiKit.Place(keys.rectTransform, new Vector2(ToolsX, -160.0f), new Vector2(240.0f, 30.0f));
            _keysHint = keys.gameObject;

            // While a picture is laid on: turn it, fit it, place it or not.
            _pictureControls = UiKit.NewChild("PictureControls", root).gameObject;
            var controls = (RectTransform)_pictureControls.transform;
            UiKit.Place(controls, new Vector2(ToolsX, -178.0f), new Vector2(240.0f, 80.0f));   // in the keys' place while a picture is laid on
            UiKit.MakeButton(controls, "TURN LEFT", new Vector2(-62.0f, 20.0f), new Vector2(108.0f, 30.0f), ButtonColour, Ink, () => TurnPicture(15.0f));
            UiKit.MakeButton(controls, "TURN RIGHT", new Vector2(62.0f, 20.0f), new Vector2(108.0f, 30.0f), ButtonColour, Ink, () => TurnPicture(-15.0f));
            UiKit.MakeButton(controls, "FIT", new Vector2(-80.0f, -18.0f), new Vector2(64.0f, 30.0f), ButtonColour, Ink, FitPicture);
            UiKit.MakeButton(controls, "PLACE", new Vector2(4.0f, -18.0f), new Vector2(96.0f, 30.0f), PlaceColour, Ink, PlacePicture);
            UiKit.MakeButton(controls, "X", new Vector2(84.0f, -18.0f), new Vector2(56.0f, 30.0f), ButtonColour, Ink, CancelPicture);
            _pictureControls.SetActive(false);
        }

        private void ToolButton(RectTransform root, Tool tool, string word, Vector2 at, Vector2 size)
        {
            Button button = UiKit.MakeButton(root, word, at, size, ButtonColour, Ink, () => SetTool(tool));
            _toolButtons[tool] = (Image)button.targetGraphic;
        }

        /// <summary>The shirt buttons down the left, the chosen one picked out.</summary>
        private void ShowShirtList()
        {
            if (_list == null)
            {
                return;
            }
            for (int i = _list.childCount - 1; i >= 0; i--)
            {
                Destroy(_list.GetChild(i).gameObject);
            }
            const float height = 50.0f, step = 56.0f;
            for (int i = 0; i < _shirts.Count && i < 9; i++)
            {
                int index = i;
                bool chosen = i == Chosen;
                Button button = UiKit.MakeButton(_list, _shirts[i].Label, new Vector2(0.0f, 225.0f - i * step), new Vector2(200.0f, height),
                    chosen ? ChosenColour : ButtonColour, chosen ? DarkInk : Ink, () => Choose(index));
                button.GetComponentInChildren<Text>().fontSize = 14;
            }
        }

        /// <summary>The shirt chosen from the side shown; none: an empty stage.</summary>
        private void ShowLook(Texture2D[] look)
        {
            if (_look == null)
            {
                return;
            }
            _look.texture = look != null ? look[(int)Side] : null;
            _look.color = look != null ? Color.white : Color.clear;
            _shownVersion = -1;
        }

        /// <summary>The tools as they are: which is in use, the colour, the brush size, the side shown, whether it can press.</summary>
        private void ShowTools()
        {
            if (_root == null)
            {
                return;
            }
            foreach (KeyValuePair<Tool, Image> pair in _toolButtons)
            {
                bool on = pair.Key == CurrentTool;
                pair.Value.color = on ? ChosenColour : ButtonColour;
                pair.Value.GetComponentInChildren<Text>().color = on ? DarkInk : Ink;
            }
            for (int side = 0; side < 2; side++)
            {
                bool on = side == (int)Side;
                _sideTabs[side].color = on ? ChosenColour : ButtonColour;
                _sideTabs[side].GetComponentInChildren<Text>().color = on ? DarkInk : Ink;
            }
            _swatch.color = Colour;
            _brushLabel.text = "SIZE " + Brush;
            _pictureControls.SetActive(_placing);
            _keysHint.SetActive(!_placing);
            _pressButton.interactable = Chosen >= 0 && (Canvas.HasInk || _placing);
        }

        /// <summary>The colour square for the hue now: a new texture each time, the old one let go of.</summary>
        private void ShowSquare()
        {
            Texture old = _square.texture;
            _square.texture = CreatorControls.BuildSquare(_hue);
            if (old != null)
            {
                Destroy(old);
            }
        }
    }
}
