using CityX.Character;
using CityX.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The parts both character creators are built from: the title screen's (CityX.UIEditor.CharacterCreatorBuilder, baked
    /// into the title scene in the editor) and ReDraw's (RedrawCreatorRuntime, built during play). Everything here uses only
    /// calls that work in both, so the two creators cannot drift apart; each keeps only what is really its own -- which body
    /// it clones and how, where its hair and its layer come from, and the two buttons at the bottom of its column.
    /// </summary>
    public static class CreatorKit
    {
        public static readonly Color Gold = new Color(0.75f, 0.6f, 0.2f, 1.0f);
        public static readonly Color Paper = new Color(0.82f, 0.82f, 0.84f, 1.0f);

        /// <summary>An unlit button. CreatorControls lights the active ones.</summary>
        public static readonly Color Idle = new Color(0.11f, 0.12f, 0.15f, 0.95f);

        /// <summary>Hints, and the captions of the tabs that are not up.</summary>
        public static readonly Color Hint = new Color(0.62f, 0.64f, 0.70f, 1.0f);

        /// <summary>
        /// The panel down the left, in canvas units. The canvas is 1152x648, so
        /// every page under the tabs has to fit above the action bar at the
        /// bottom -- the stack grew past it once already and the two ended up
        /// drawn over each other, hiding UNDO and REDO completely. Wide enough
        /// for four hairstyle pictures in a row (HairPage).
        /// </summary>
        public const float ColumnWidth = 232.0f;

        /// <summary>The PAINT / HAIR / FACE tabs, under the heading (CreatorTabs).</summary>
        public const float TabStripY = -58.0f;

        /// <summary>How much lower the PAINT page sits than the column's top: its tools were laid out before there were tabs.</summary>
        public const float PaintPageDrop = 38.0f;

        private const float TabWidth = 72.0f, TabHeight = 26.0f, TabPitch = 76.0f;

        public const float ActionBarHeight = 88.0f;

        /// <summary>
        /// The palette down the side. Twelve presets for the colours anybody
        /// reaches for first; the wheel above them is what makes every other
        /// colour reachable.
        /// </summary>
        private static readonly Color[] Palette =
        {
            new Color(0.10f, 0.10f, 0.12f), new Color(0.95f, 0.95f, 0.95f),
            new Color(0.72f, 0.16f, 0.16f), new Color(0.90f, 0.45f, 0.12f),
            new Color(0.92f, 0.80f, 0.20f), new Color(0.25f, 0.60f, 0.28f),
            new Color(0.20f, 0.45f, 0.80f), new Color(0.45f, 0.25f, 0.65f),
            new Color(0.85f, 0.55f, 0.70f), new Color(0.55f, 0.40f, 0.28f),
            new Color(0.78f, 0.62f, 0.53f), new Color(0.45f, 0.48f, 0.52f),
        };

        // --- the stage ---

        /// <summary>
        /// The skull's renderer: the one MeshRenderer under the head bone.
        /// Found by component rather than by name so a rename does not quietly
        /// leave the face unpaintable.
        /// </summary>
        public static MeshRenderer FindHead(GameObject body)
        {
            Transform head = TransformSearch.FindDeep(body.transform, "head");
            return head != null ? head.GetComponentInChildren<MeshRenderer>(true) : null;
        }

        public static void AddLight(GameObject stage, int layer, string name, Vector3 angles,
            float intensity, Color colour)
        {
            var go = new GameObject($"{name}Light");
            go.transform.SetParent(stage.transform, false);
            go.transform.rotation = Quaternion.Euler(angles);
            go.layer = layer;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = colour;
            // Only this layer, so the creator's lights do not also brighten the
            // night city the title screen is showing behind it.
            light.cullingMask = 1 << layer;
            light.shadows = LightShadows.None;
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        // --- the tool column ---

        /// <summary>
        /// The tool column down the left: the heading (none if null), then the PAINT / HAIR / FACE tabs (CreatorTabs) over their pages -- PAINT the
        /// tools, the colour square and palette, the brush size, mirror and eraser, undo and redo, the hint; HAIR the styles and their
        /// colour (HairPage); FACE the premade parts to come (FacePage) -- and under every tab the opaque bar pinned to the bottom, on
        /// which <paramref name="addActions"/> puts the creator's own two buttons (it is handed the column).
        /// <paramref name="buttonOverlays"/> goes on mirror, eraser, undo and redo (ReDraw's do carry it, the title screen's do not).
        /// </summary>
        public static void BuildToolColumn(Transform parent, CharacterCreator creator, string heading,
            CreatorOverlays buttonOverlays, System.Action<Transform> addActions)
        {
            // Down the left, so the body stays centred and a stroke near the
            // edge of it does not start under a button.
            var column = new GameObject("Tools", typeof(RectTransform));
            var rect = (RectTransform)column.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.0f, 0.0f);
            rect.anchorMax = new Vector2(0.0f, 1.0f);
            rect.pivot = new Vector2(0.0f, 0.5f);
            rect.offsetMin = new Vector2(0.0f, 0.0f);
            rect.offsetMax = new Vector2(ColumnWidth, 0.0f);

            column.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.92f);

            var controls = column.AddComponent<CreatorControls>();
            controls.Creator = creator;

            Transform t = column.transform;

            if (heading != null)
            {
                MakeLabel(t, "Heading", heading, 20, Gold, At(-24.0f), 24.0f);
            }

            // Every page is built in full, whichever is up: a page is hidden by its CanvasGroup, never switched off, so each button and
            // picker on it has woken and wired itself (ButtonAction, CreatorControls, HairColourControls).
            CreatorTabs tabs = column.AddComponent<CreatorTabs>();
            tabs.Creator = creator;
            Transform paint = MakePage(t, "PaintPage", PaintPageDrop, out CanvasGroup paintPage);
            Transform hair = MakePage(t, "HairPage", 0.0f, out CanvasGroup hairPage);
            Transform face = MakePage(t, "FacePage", 0.0f, out CanvasGroup facePage);
            tabs.Pages = new[] { paintPage, hairPage, facePage };
            MakeTabStrip(t, tabs, creator);

            BuildPaintPage(paint, creator, controls, buttonOverlays);
            HairPage.Build(hair, creator);
            FacePage.Build(face, creator);
            // Here as well as on waking: the title screen's creator is saved by the editor, where nothing wakes.
            tabs.Show(CreatorTabs.Paint);

            // Pinned to the bottom rather than following the stack, so it stays
            // put as tools are added above it -- ON ITS OWN OPAQUE BAR, because
            // the stack grew past it once already and the two ended up drawn
            // over each other.
            var bar = new GameObject("ActionBar", typeof(RectTransform));
            var barRect = (RectTransform)bar.transform;
            barRect.SetParent(t, false);
            barRect.anchorMin = new Vector2(0.0f, 0.0f);
            barRect.anchorMax = new Vector2(1.0f, 0.0f);
            barRect.pivot = new Vector2(0.5f, 0.0f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0.0f, ActionBarHeight);
            bar.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 1.0f);

            // Built AFTER the bar and therefore drawn over it, which is the
            // whole point -- built before it, they were the ones underneath.
            addActions(t);
        }

        /// <summary>
        /// One tab's page: the whole column, <paramref name="drop"/> lower, shown and hidden by its CanvasGroup (CreatorTabs).
        /// </summary>
        private static Transform MakePage(Transform column, string name, float drop, out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(column, false);
            UiKit.Stretch(rect);
            rect.offsetMin = new Vector2(0.0f, -drop);
            rect.offsetMax = new Vector2(0.0f, -drop);
            group = go.AddComponent<CanvasGroup>();
            return go.transform;
        }

        /// <summary>PAINT, HAIR and FACE in a row under the heading, each with its caption and the gold line under the one that is up.</summary>
        private static void MakeTabStrip(Transform column, CreatorTabs tabs, CharacterCreator creator)
        {
            string[] names = { "PaintTab", "HairTab", "FaceTab" };
            string[] captions = { "DRAW", "HAIR", "FACE" };   // DRAW, not PAINT: the owner's word, 2026-11-10
            var labels = new Text[names.Length];
            var underlines = new Image[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Image tab = MakeButton(column, names[i], string.Empty, Gold,
                    new Vector2((i - 1) * TabPitch, TabStripY), new Vector2(TabWidth, TabHeight),
                    ButtonAction.Kind.CreatorTab, creator, value: i);
                tab.GetComponent<ButtonAction>().Tabs = tabs;
                labels[i] = Caption(tab.transform, captions[i], 13, Hint);

                var line = new GameObject("Underline", typeof(RectTransform));
                var lineRect = (RectTransform)line.transform;
                lineRect.SetParent(tab.transform, false);
                lineRect.anchorMin = new Vector2(0.0f, 0.0f);
                lineRect.anchorMax = new Vector2(1.0f, 0.0f);
                lineRect.pivot = new Vector2(0.5f, 0.0f);
                lineRect.anchoredPosition = Vector2.zero;
                lineRect.sizeDelta = new Vector2(0.0f, 2.0f);
                underlines[i] = line.AddComponent<Image>();
                underlines[i].color = Gold;
                underlines[i].raycastTarget = false;
            }
            tabs.Labels = labels;
            tabs.Underlines = underlines;
        }

        /// <summary>The PAINT tab's page: everything the column held before it had tabs.</summary>
        private static void BuildPaintPage(Transform t, CharacterCreator creator, CreatorControls controls, CreatorOverlays buttonOverlays)
        {
            MakeToolRow(t, creator, controls);

            MakeLabel(t, "PaletteLabel", "COLOUR", 13, Paper, At(-94.0f), 18.0f);

            // THE SV SQUARE, not just swatches. Any colour has to be
            // reachable -- twelve presets is a palette, not a character
            // creator. Godot's own ColorPickerButton opens its default
            // SHAPE_HSV_RECTANGLE popup: a saturation/value square for
            // whatever hue is selected, plus a separate hue strip -- this is
            // that shape, not the hue/saturation wheel it replaced.
            controls.SVSquare = MakeSquare(t, "SVSquare", At(-166.0f), 128.0f);

            controls.Hue = MakeHueSlider(t, "HueSlider", At(-244.0f), 150.0f);
            controls.Preview = MakeColourCode(t, "Preview", At(-266.0f), new Vector2(150.0f, 16.0f), controls);

            MakePalette(t, creator, controls, -292.0f);

            controls.BrushLabel =
                MakeLabel(t, "BrushLabel", "BRUSH  1", 13, Paper, At(-348.0f), 18.0f);
            controls.BrushSize = MakeSlider(t, "BrushSlider", At(-370.0f), 1.0f, 15.0f,
                CharacterCreator.DefaultBrushRadius, wholeNumbers: true);

            controls.MirrorButton = MakeButton(t, "MirrorButton", "MIRROR", Paper,
                new Vector2(-46.0f, -400.0f), new Vector2(88.0f, 30.0f),
                ButtonAction.Kind.CreatorToggleMirror, creator, overlays: buttonOverlays);
            controls.EraserButton = MakeButton(t, "EraserButton", "ERASER", Paper,
                new Vector2(46.0f, -400.0f), new Vector2(88.0f, 30.0f),
                ButtonAction.Kind.CreatorToggleEraser, creator, overlays: buttonOverlays);

            MakeButton(t, "UndoButton", "UNDO", Paper,
                new Vector2(-46.0f, -434.0f), new Vector2(88.0f, 30.0f),
                ButtonAction.Kind.CreatorUndo, creator, overlays: buttonOverlays);
            MakeButton(t, "RedoButton", "REDO", Paper,
                new Vector2(46.0f, -434.0f), new Vector2(88.0f, 30.0f),
                ButtonAction.Kind.CreatorRedo, creator, overlays: buttonOverlays);
            controls.UndoButton = t.Find("UndoButton").GetComponent<Button>();
            controls.RedoButton = t.Find("RedoButton").GetComponent<Button>();

            MakeLabel(t, "HintLabel",
                "Drag the body to paint it.\nDrag anywhere else to turn it.\nScroll to zoom.",
                10, Hint, At(-480.0f), 40.0f);
        }

        /// <summary>Centred in the column or page, this far down from its top.</summary>
        private static Vector2 At(float y) => new Vector2(0.0f, y);

        /// <summary>
        /// Brush, bucket and eyedropper, as icons rather than the words BRUSH,
        /// FILL and PICK -- the Godot build drew these and then cleared the
        /// button text entirely, and this had quietly gone back to text.
        /// </summary>
        private static void MakeToolRow(Transform parent, CharacterCreator creator,
            CreatorControls controls)
        {
            string[] names = { "Brush", "Bucket", "Eyedropper" };
            string[] kinds = { "brush", "bucket", "eyedropper" };

            var faces = new Image[names.Length];
            var icons = new RawImage[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                Image face = MakeButton(parent, names[i] + "Button", string.Empty, Gold,
                    new Vector2(i * 58.0f - 58.0f, -62.0f), new Vector2(54.0f, 40.0f),
                    ButtonAction.Kind.CreatorTool, creator, value: i);
                faces[i] = face;

                var iconObject = new GameObject("Icon", typeof(RectTransform));
                var iconRect = (RectTransform)iconObject.transform;
                iconRect.SetParent(face.transform, false);
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(30.0f, 30.0f);

                var icon = iconObject.AddComponent<RawImage>();
                icon.texture = ToolIcons.Build(kinds[i]);
                // Nothing should ever click the picture on top of a button --
                // it would eat the press meant for the button underneath.
                icon.raycastTarget = false;
                icons[i] = icon;
            }

            controls.ToolButtons = faces;
            controls.ToolIconImages = icons;
        }

        /// <summary>A saturation/value square for the hue a slider picks (CreatorControls, HairColourControls).</summary>
        public static RawImage MakeSquare(Transform parent, string name, Vector2 position, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);

            var image = go.AddComponent<RawImage>();
            image.texture = CreatorControls.BuildSquare(0.0f);
            return image;
        }

        /// <summary>
        /// A plain Slider's flat fill would just be a bar sitting over half
        /// the rainbow -- the gradient itself, drawn once behind the handle,
        /// is what a hue slider actually shows, so the fill is left
        /// invisible and only the background carries the strip.
        /// </summary>
        public static Slider MakeHueSlider(Transform parent, string name, Vector2 position,
            float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 16.0f);

            var background = go.AddComponent<RawImage>();
            background.texture = CreatorControls.BuildHueStrip();

            var fillArea = new GameObject("Fill", typeof(RectTransform));
            var fillRect = (RectTransform)fillArea.transform;
            fillRect.SetParent(go.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fill = fillArea.AddComponent<Image>();
            fill.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);

            // The handle is a ring round a disc of the hue it is on, standing a little proud of the strip: grabbed and dragged
            // (HueKnob; owner, 2026-11-17, "puts a circle over your selected hue so you can hold click on the circle and move it around").
            var handle = new GameObject("Handle", typeof(RectTransform));
            var handleRect = (RectTransform)handle.transform;
            handleRect.SetParent(go.transform, false);
            handleRect.sizeDelta = new Vector2(HueKnobSize, HueKnobSize - 16.0f);   // (the slider stretches it over the strip's height)
            RawImage ring = handle.AddComponent<RawImage>();
            var fillDisc = new GameObject("HueFill", typeof(RectTransform));
            var discRect = (RectTransform)fillDisc.transform;
            discRect.SetParent(handleRect, false);
            UiKit.Stretch(discRect);
            RawImage disc = fillDisc.AddComponent<RawImage>();
            disc.raycastTarget = false;

            Slider slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = ring;
            HueKnob knob = go.AddComponent<HueKnob>();
            knob.Ring = ring;
            knob.Fill = disc;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0.0f;
            slider.maxValue = 1.0f;
            slider.wholeNumbers = false;
            slider.value = 0.0f;
            return slider;
        }

        /// <summary>The hue strip's handle across, canvas units: the strip is 16 high, the circle stands proud of it.</summary>
        public const float HueKnobSize = 24.0f;

        /// <summary>
        /// A picker's preview bar that shows the colour's code over it and takes one typed in (ColourCodeField; owner, 2026-11-17: "make the
        /// color code visible at the bottom and it lets you enter color codes").
        /// </summary>
        public static Image MakeColourCode(Transform parent, string name, Vector2 position, Vector2 size, MonoBehaviour picker)
        {
            Image bar = MakeSwatch(parent, name, position, size, Color.white);
            bar.raycastTarget = true;
            Text typed = Caption(bar.transform, string.Empty, Mathf.RoundToInt(Mathf.Min(13.0f, size.y - 3.0f)), Paper);
            typed.name = "Code";
            typed.supportRichText = false;
            typed.verticalOverflow = VerticalWrapMode.Overflow;
            typed.fontStyle = FontStyle.Bold;

            InputField field = bar.gameObject.AddComponent<InputField>();
            field.targetGraphic = bar;
            field.transition = Selectable.Transition.None;   // a tint would change the colour it shows
            field.textComponent = typed;
            field.characterLimit = 7;
            field.lineType = InputField.LineType.SingleLine;
            field.customCaretColor = true;
            ColourCodeField code = bar.gameObject.AddComponent<ColourCodeField>();
            code.Picker = picker;
            return bar;
        }

        /// <summary>A flat colour that takes no clicks: a picker's preview.</summary>
        public static Image MakeSwatch(Transform parent, string name, Vector2 position,
            Vector2 size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// A slider, built by hand rather than from DefaultControls so its
        /// hierarchy is the one written here and not one to reverse-engineer.
        /// </summary>
        private static Slider MakeSlider(Transform parent, string name, Vector2 position,
            float minimum, float maximum, float value, bool wholeNumbers)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(150.0f, 16.0f);

            Image background = go.AddComponent<Image>();
            background.color = new Color(0.16f, 0.17f, 0.20f, 1.0f);

            var fillArea = new GameObject("Fill", typeof(RectTransform));
            var fillRect = (RectTransform)fillArea.transform;
            fillRect.SetParent(go.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fill = fillArea.AddComponent<Image>();
            fill.color = Gold;

            var handle = new GameObject("Handle", typeof(RectTransform));
            var handleRect = (RectTransform)handle.transform;
            handleRect.SetParent(go.transform, false);
            handleRect.sizeDelta = new Vector2(10.0f, 20.0f);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Paper;

            Slider slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;
            return slider;
        }

        private static void MakePalette(Transform parent, CharacterCreator creator,
            CreatorControls controls, float top)
        {
            const int perRow = 6;
            const float pitch = 28.0f;

            for (int i = 0; i < Palette.Length; i++)
            {
                Color swatch = Palette[i];
                int column = i % perRow;
                int row = i / perRow;

                var go = new GameObject($"Swatch{i}", typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(
                    column * pitch - pitch * (perRow - 1) * 0.5f,
                    top - row * pitch);
                rect.sizeDelta = new Vector2(26.0f, 26.0f);

                Image image = go.AddComponent<Image>();
                image.color = swatch;

                Button button = go.AddComponent<Button>();
                button.targetGraphic = image;

                var wiring = go.AddComponent<ButtonAction>();
                wiring.Action = ButtonAction.Kind.CreatorColour;
                wiring.Creator = creator;
                wiring.Colour = swatch;
                // So the wheel and the preview follow a swatch press rather
                // than still showing whatever was picked before it.
                wiring.Controls = controls;
            }
        }

        // --- the prompts ---

        /// <summary>
        /// Nothing is ever saved from the creator until a name is confirmed, so
        /// walking out after painting silently throws the work away. Having
        /// painted at all is what raises this. (ReDraw's CANCEL raises the same
        /// check: CreatorOverlays.Discard knows which creator it is in.)
        /// </summary>
        public static void BuildDiscardPrompt(Transform parent, CreatorOverlays overlays)
        {
            GameObject dim = MakeDim(parent, "DiscardPrompt");
            RectTransform panel = MakePromptPanel(dim.transform, new Vector2(400.0f, 180.0f));

            Text question = MakeLabel(panel, "Question",
                "Discard your painted design and go back?", 16, Paper,
                new Vector2(0.0f, 34.0f), 48.0f);
            ((RectTransform)question.transform).sizeDelta = new Vector2(340.0f, 48.0f);

            MakePromptButton(panel, "DiscardButton", "DISCARD & BACK", Paper,
                new Vector2(-80.0f, -44.0f), new Vector2(150.0f, 40.0f),
                ButtonAction.Kind.CreatorDiscard, overlays);
            MakePromptButton(panel, "CancelButton", "CANCEL", Gold,
                new Vector2(85.0f, -44.0f), new Vector2(110.0f, 40.0f),
                ButtonAction.Kind.CreatorKeepEditing, overlays);

            overlays.ConfirmPanel = dim;
            dim.SetActive(false);
        }

        /// <summary>
        /// The full-screen darkener behind a prompt. It is a raycast target on
        /// purpose: it is what stops a click reaching a tool button behind it.
        /// </summary>
        public static GameObject MakeDim(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            UiKit.Stretch(rect);
            go.AddComponent<Image>().color = new Color(0.0f, 0.0f, 0.0f, 0.7f);
            return go;
        }

        public static RectTransform MakePromptPanel(Transform parent, Vector2 size)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            go.AddComponent<Image>().color = new Color(0.10f, 0.11f, 0.14f, 0.98f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.63f, 1.0f, 0.6f);
            outline.effectDistance = new Vector2(2.0f, -2.0f);
            return rect;
        }

        public static void MakePromptButton(Transform parent, string name, string label,
            Color tint, Vector2 position, Vector2 size, ButtonAction.Kind action,
            CreatorOverlays overlays)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image background = go.AddComponent<Image>();
            background.color = Idle;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;

            var wiring = go.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Overlays = overlays;
            wiring.Creator = overlays.Creator;
            wiring.Title = overlays.Title;

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = tint;
            button.colors = colours;

            Caption(go.transform, label, 13, tint);
        }

        // --- small pieces ---

        /// <summary>
        /// Carries a ButtonAction, not a listener -- see that class. A listener
        /// added from an editor script is not serialized, so buttons wired that
        /// way are dead the moment the scene is saved and reopened.
        /// </summary>
        private static Image MakeButton(Transform parent, string name, string label, Color tint,
            Vector2 position, Vector2 size, ButtonAction.Kind action,
            CharacterCreator creator, TitleScreen title = null, int value = 0,
            CreatorOverlays overlays = null, int fontSize = 13)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image background = go.AddComponent<Image>();
            background.color = Idle;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;

            var wiring = go.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Creator = creator;
            wiring.Title = title;
            wiring.Value = value;
            wiring.Overlays = overlays;

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = tint;
            // Disabled has to be visibly different, or a greyed-out UNDO looks
            // exactly like a live one that does nothing when pressed.
            colours.disabledColor = new Color(0.09f, 0.09f, 0.10f, 0.6f);
            button.colors = colours;

            if (!string.IsNullOrEmpty(label))
            {
                Caption(go.transform, label, fontSize, tint);
            }
            return background;
        }

        /// <summary>
        /// A button's own text, STRETCHED OVER THE WHOLE BUTTON.
        ///
        /// Not MakeLabel: that one hangs a label from the top of the tool
        /// column, and reusing it inside a button anchored the text to the
        /// button's top EDGE with the pivot in the middle of it -- so every
        /// caption on the panel was drawn half above the button it named.
        /// MIRROR, ERASER, ENTER THE CITY and BACK were all floating over
        /// their own boxes.
        /// </summary>
        public static Text Caption(Transform parent, string content, int size, Color colour)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            UiKit.Stretch(rect);

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        /// <summary>A button hung from the bottom of the column (for the action bar) rather than its top.</summary>
        public static void MakeAnchoredButton(Transform parent, string name, string label,
            Color tint, Vector2 position, Vector2 size, ButtonAction.Kind action,
            CharacterCreator creator, TitleScreen title, CreatorOverlays overlays)
        {
            MakeButton(parent, name, label, tint, position, size, action, creator, title,
                overlays: overlays);
            var rect = (RectTransform)parent.Find(name);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.0f);
        }

        public static Text MakeLabel(Transform parent, string name, string content, int size,
            Color colour, Vector2 position, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(ColumnWidth - 16.0f, height);

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        /// <summary>
        /// The same label, centred on its parent rather than hung from the top
        /// of the column -- for the prompts, which are centred boxes.
        /// </summary>
        public static Text MakeLabel(RectTransform parent, string name, string content,
            int size, Color colour, Vector2 position, float height)
        {
            Text text = MakeLabel(parent.transform, name, content, size, colour,
                position, height);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            return text;
        }
    }
}
