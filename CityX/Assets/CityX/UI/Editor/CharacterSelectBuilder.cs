using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the character list onto the title screen's canvas (docs/CHARACTER_SELECT.md): the characters stand in a row on the conveyor belt
    /// the creator makes them on, so its 3D half is the creator's own stage (its belt, sky, lights and camera), with a
    /// <see cref="BeltLineup"/> added to stand them there. Over it: the chosen one's name, the arrows, CREATE NEW and BACK; the card the view
    /// closes in to; the delete check.
    /// </summary>
    public static class CharacterSelectBuilder
    {
        private static readonly Color Gold = new Color(0.75f, 0.6f, 0.2f, 1.0f);
        private static readonly Color Paper = new Color(0.82f, 0.82f, 0.84f, 1.0f);
        private static readonly Color Danger = new Color(0.55f, 0.16f, 0.16f, 1.0f);

        /// <summary>The dark edge round words standing on the sky and the belt with nothing behind them (as the creator's tools).</summary>
        private static readonly Color Edge = new Color(0.03f, 0.04f, 0.07f, 1.0f);

        /// <summary>CREATE NEW's arrow: gold, apart from PLAY's green and BACK's red.</summary>
        private static readonly Color CreateTop = new Color(1.0f, 0.80f, 0.25f);

        private static readonly Color CreateBottom = new Color(0.80f, 0.50f, 0.04f);

        /// <summary>
        /// The skins a new character can be. Deliberately a short list of
        /// distinguishable colours rather than a colour wheel: the Godot build
        /// let you paint the whole body in its creator, and offering a wheel
        /// here would look like that feature rather than admitting it is a
        /// stand-in until the creator is ported.
        /// </summary>
        private static readonly Color[] Skins =
        {
            new Color(0.78f, 0.62f, 0.53f),
            new Color(0.55f, 0.40f, 0.32f),
            new Color(0.35f, 0.25f, 0.20f),
            new Color(0.86f, 0.76f, 0.68f),
            new Color(0.44f, 0.52f, 0.58f),
            new Color(0.62f, 0.30f, 0.30f),
        };

        /// <summary>
        /// <paramref name="creatorPanel"/> is the creator's (CharacterCreatorBuilder): the row stands on its stage, and its belt sequence
        /// brings a new body in on CREATE NEW. Without it (no player body to build either from) the list has no belt.
        /// </summary>
        public static GameObject Build(Transform parent, TitleScreen title, GameObject bodyPrefab, GameObject creatorPanel)
        {
            var panel = new GameObject("CharacterSelectPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            Stretch(rect, 0.0f);

            BeltIntro intro = creatorPanel != null ? creatorPanel.GetComponent<BeltIntro>() : null;
            CreatorStage creatorStage = creatorPanel != null ? creatorPanel.GetComponent<CreatorStage>() : null;
            bool onTheBelt = intro != null && creatorStage != null && creatorStage.Stage != null;
            if (onTheBelt)
            {
                // The creator's stage, kept up while either panel is (CreatorStage); its relay first, so the stage is up before the list
                // stands figures on it: a figure made under a stage still down wakes later, after it was stripped, and starts its sounds.
                panel.AddComponent<CreatorStage>().Stage = creatorStage.Stage;
            }

            CharacterSelect select = panel.AddComponent<CharacterSelect>();
            title.Select = select;
            select.Title = title;
            if (onTheBelt)
            {
                select.Intro = intro;
                select.Lineup = BuildLineup(creatorStage.Stage, intro, bodyPrefab);
            }
            else
            {
                Debug.LogWarning("CityX: no creator to stand the characters on -- the character list will be empty.");
            }

            select.ListPanel = BuildRow(panel.transform, select, title);
            select.EmptyNotice = Outlined(MakeLabel(panel.transform, "EmptyNotice", "No characters yet", 26, Color.white,
                Anchor(0.5f, 0.62f), Vector2.zero, new Vector2(560.0f, 40.0f)), 1.2f);
            BuildCreateNew(panel.transform, select);
            select.CardPanel = BuildCard(panel.transform, select, title);
            select.CreatePanel = BuildCreate(panel.transform, select, title);
            select.ConfirmPanel = BuildConfirm(panel.transform, select);

            select.Show(CharacterSelect.Mode.List);
            return panel;
        }

        // --- the row's 3D half ---

        /// <summary>The figures' row on the creator's stage, its layer, seen through the creator's camera.</summary>
        private static BeltLineup BuildLineup(GameObject stage, BeltIntro intro, GameObject bodyPrefab)
        {
            BeltLineup lineup = stage.AddComponent<BeltLineup>();
            lineup.Belt = intro.Belt;
            lineup.View = intro.MakingCamera;
            lineup.BodyPrefab = bodyPrefab;
            lineup.HairStyleMeshes = HairAssets.StyleMeshes();
            lineup.HairMaterialAsset = CharacterCreatorBuilder.HairMaterial();
            lineup.ClothingPrefabs = CityX.CharacterEditor.NetworkedPlayerWiring.ItemPrefabEntries(item => item.IsWearable);
            intro.Lineup = lineup;
            return lineup;
        }

        // --- the row's controls ---

        /// <summary>The name over the middle character, the arrows either side, and BACK to the menu. Nothing here covers the screen: a click between them reaches the figures.</summary>
        private static GameObject BuildRow(Transform parent, CharacterSelect select, TitleScreen title)
        {
            var panel = new GameObject("ListPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            Stretch(rect, 0.0f);

            // Placed over the middle character's head each frame (CharacterSelect.ShowTheTag), from its bottom middle.
            var tag = new GameObject("NameTag", typeof(RectTransform));
            var tagRect = (RectTransform)tag.transform;
            tagRect.SetParent(panel.transform, false);
            tagRect.anchorMin = tagRect.anchorMax = new Vector2(0.5f, 0.5f);
            tagRect.pivot = new Vector2(0.5f, 0.0f);
            tagRect.sizeDelta = new Vector2(420.0f, 64.0f);
            var group = tag.AddComponent<CanvasGroup>();
            group.alpha = 0.0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            select.Tag = group;
            select.TagName = Outlined(MakeLabel(tag.transform, "Name", "Name", 30, Color.white,
                Anchor(0.5f, 1.0f), new Vector2(0.0f, -18.0f), new Vector2(420.0f, 36.0f)), 2.0f);
            select.TagName.fontStyle = FontStyle.Bold;
            select.TagDetail = Outlined(MakeLabel(tag.transform, "Detail", "Level 1", 19, Color.white,
                Anchor(0.5f, 0.0f), new Vector2(0.0f, 12.0f), new Vector2(420.0f, 24.0f)), 1.6f);

            select.PreviousButton = KeyButton(panel.transform, "PreviousButton", "←", Anchor(0.0f, KeysUp), new Vector2(64.0f, 0.0f),
                ButtonAction.Kind.SelectPrevious, select);
            select.NextButton = KeyButton(panel.transform, "NextButton", "→", Anchor(1.0f, KeysUp), new Vector2(-64.0f, 0.0f),
                ButtonAction.Kind.SelectNext, select);

            Button back = ArrowButtons.Curled(panel.transform, "BackButton", "BACK", BackSize, ArrowButtons.RedTop, ArrowButtons.RedBottom);
            Place(back, Anchor(0.0f, 0.0f), new Vector2(100.0f, 52.0f));
            Wire(back, ButtonAction.Kind.TitleBack, title, select);
            return panel;
        }

        /// <summary>Where the arrow buttons go: their size, as the creator's.</summary>
        private static readonly Vector2 BackSize = new Vector2(150.0f, 70.0f);

        private static readonly Vector2 ArrowSize = new Vector2(214.0f, 92.0f);

        /// <summary>How far up the screen the arrow key caps are: over the characters' heads, clear of the ones beside the middle.</summary>
        private const float KeysUp = 0.72f;

        /// <summary>How far along from the card's left edge the bin is: over the end of a name of ordinary length.</summary>
        private const float BinRowWidth = 250.0f;

        /// <summary>
        /// CREATE NEW, in the top right corner over the row and the card alike (owner, 2026-11-11: "make it so a create new button is on screen
        /// always").
        /// </summary>
        private static void BuildCreateNew(Transform parent, CharacterSelect select)
        {
            Button create = ArrowButtons.Straight(parent, "NewButton", "CREATE NEW", new Vector2(250.0f, 92.0f), CreateTop, CreateBottom);
            Place(create, Anchor(1.0f, 1.0f), new Vector2(-142.0f, -58.0f));
            Wire(create, ButtonAction.Kind.SelectCreateNew, null, select);
        }

        /// <summary>
        /// The chosen character's card, in the right half of the screen while the view has closed in on it (the body stands in the left):
        /// its name, level and play time and stats, the little bin to delete it, PLAY and BACK. No box behind it, as the creator's tools.
        /// </summary>
        private static GameObject BuildCard(Transform parent, CharacterSelect select, TitleScreen title)
        {
            var panel = new GameObject("CardPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.56f, 0.0f);
            rect.anchorMax = new Vector2(0.98f, 1.0f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            select.NameLabel = Outlined(MakeLabel(panel.transform, "Name", "Name", 40, Color.white,
                Anchor(0.0f, 0.5f), new Vector2(0.0f, 132.0f), new Vector2(400.0f, 52.0f)), 2.2f);
            select.NameLabel.alignment = TextAnchor.MiddleLeft;
            select.NameLabel.fontStyle = FontStyle.Bold;
            select.DetailLabel = Outlined(MakeLabel(panel.transform, "Detail", "Level 1   0m", 22, Color.white,
                Anchor(0.0f, 0.5f), new Vector2(0.0f, 92.0f), new Vector2(400.0f, 28.0f)), 1.6f);
            select.DetailLabel.alignment = TextAnchor.MiddleLeft;
            select.StatsLabel = Outlined(MakeLabel(panel.transform, "Stats", "VIT 0   END 0   STR 0", 19, Color.white,
                Anchor(0.0f, 0.5f), new Vector2(0.0f, 62.0f), new Vector2(400.0f, 26.0f)), 1.6f);
            select.StatsLabel.alignment = TextAnchor.MiddleLeft;

            // Deleting is a small bin, not a button beside PLAY (owner, 2026-10-26: "move the delete button to just a little trash icon"). It
            // asks first (the confirm panel).
            var binRow = new GameObject("BinRow", typeof(RectTransform));
            var binRect = (RectTransform)binRow.transform;
            binRect.SetParent(panel.transform, false);
            binRect.anchorMin = binRect.anchorMax = Anchor(0.0f, 0.5f);
            binRect.pivot = new Vector2(0.0f, 0.5f);
            binRect.anchoredPosition = new Vector2(0.0f, 178.0f);
            binRect.sizeDelta = new Vector2(BinRowWidth, 40.0f);
            MakeTrashButton(binRect, select);
            MakePencilButton(binRect, select);

            Button play = ArrowButtons.Straight(panel.transform, "PlayButton", "PLAY", ArrowSize, ArrowButtons.GreenTop, ArrowButtons.GreenBottom);
            Place(play, Anchor(0.0f, 0.5f), new Vector2(ArrowSize.x * 0.5f, -40.0f));
            Wire(play, ButtonAction.Kind.SelectPlaySelected, title, select);
            Button back = ArrowButtons.Curled(panel.transform, "BackButton", "BACK", BackSize, ArrowButtons.RedTop, ArrowButtons.RedBottom);
            Place(back, Anchor(0.0f, 0.5f), new Vector2(ArrowSize.x * 0.5f - 42.0f, -112.0f));
            Wire(back, ButtonAction.Kind.TitleBack, title, select);
            panel.SetActive(false);
            return panel;
        }

        /// <summary>A key cap to click (the belt's own prompt's look): the arrow of the key that does the same.</summary>
        private static GameObject KeyButton(Transform parent, string name, string glyph, Vector2 anchor, Vector2 position,
            ButtonAction.Kind action, CharacterSelect select)
        {
            const float Size = 64.0f;
            var key = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)key.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(Size, Size);
            var frame = key.AddComponent<KeyFrame>();
            frame.Thickness = 4.0f;
            frame.color = Color.white;
            Button button = key.AddComponent<Button>();
            button.targetGraphic = frame;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(1.0f, 0.85f, 0.4f, 1.0f);
            colours.pressedColor = Gold;
            button.colors = colours;
            Wire(button, action, null, select);
            Text arrow = Outlined(MakeLabel(key.transform, "Key", glyph, 46, Color.white, Anchor(0.5f, 0.5f), Vector2.zero, new Vector2(Size, Size)), 1.6f);
            arrow.fontStyle = FontStyle.Bold;
            // The frame alone is clicked through its middle, so a clear square behind it takes the click.
            var hit = new GameObject("Hit", typeof(RectTransform));
            var hitRect = (RectTransform)hit.transform;
            hitRect.SetParent(key.transform, false);
            hitRect.SetAsFirstSibling();
            Stretch(hitRect, 0.0f);
            hit.AddComponent<Image>().color = Color.clear;
            return key;
        }

        private static void Place(Button button, Vector2 anchor, Vector2 position)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
        }

        private static void Wire(Button button, ButtonAction.Kind action, TitleScreen title, CharacterSelect select)
        {
            var wiring = button.gameObject.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Title = title;
            wiring.Select = select;
        }

        /// <summary>Words with a dark edge, to read on the pale sky and on the belt alike.</summary>
        private static Text Outlined(Text words, float edge)
        {
            Outline outline = words.gameObject.AddComponent<Outline>();
            outline.effectColor = Edge;
            outline.effectDistance = new Vector2(edge, -edge);
            return words;
        }

        /// <summary>
        /// The bin at the end of the card's top row: a lid with its handle over a can with two slots, drawn from plain rectangles (no image
        /// file, no glyph the built-in font lacks). Its square lights red under the pointer.
        /// </summary>
        private static void MakeTrashButton(Transform card, CharacterSelect select)
        {
            GameObject buttonObject = IconButton(card, "TrashButton", -6.0f, Danger, ButtonAction.Kind.SelectBeginDeleteSelected, select);
            var bin = new Color(0.62f, 0.62f, 0.66f, 1.0f);
            IconPart(buttonObject.transform, "Handle", bin, new Vector2(0.0f, 8.5f), new Vector2(6.0f, 2.0f));
            IconPart(buttonObject.transform, "Lid", bin, new Vector2(0.0f, 6.0f), new Vector2(16.0f, 2.5f));
            IconPart(buttonObject.transform, "Can", bin, new Vector2(0.0f, -3.0f), new Vector2(12.0f, 13.0f));
            var slot = new Color(0.05f, 0.06f, 0.08f, 1.0f);
            IconPart(buttonObject.transform, "SlotLeft", slot, new Vector2(-2.5f, -3.0f), new Vector2(1.5f, 9.0f));
            IconPart(buttonObject.transform, "SlotRight", slot, new Vector2(2.5f, -3.0f), new Vector2(1.5f, 9.0f));
        }

        /// <summary>
        /// The pencil beside the bin (owner, 2026-11-15: "add a pencil icon next to the trash icon to redraw"): the chosen character opens in the
        /// redraw creator, the one the Player menu's ReDraw opens in the world. A yellow pencil leaning up to the right, its tip at the bottom left,
        /// drawn from plain rectangles as the bin is. Its square lights gold under the pointer.
        /// </summary>
        private static void MakePencilButton(Transform card, CharacterSelect select)
        {
            GameObject buttonObject = IconButton(card, "PencilButton", -6.0f - 26.0f - 8.0f, Gold, ButtonAction.Kind.SelectBeginRedrawSelected, select);
            const float Lean = 45.0f;
            Vector2 along = new Vector2(Mathf.Cos(Lean * Mathf.Deg2Rad), Mathf.Sin(Lean * Mathf.Deg2Rad));
            IconPart(buttonObject.transform, "Body", new Color(0.96f, 0.76f, 0.18f, 1.0f), along * 1.5f, new Vector2(13.0f, 5.0f), Lean);
            IconPart(buttonObject.transform, "Band", new Color(0.62f, 0.62f, 0.66f, 1.0f), along * 9.0f, new Vector2(2.0f, 5.0f), Lean);
            IconPart(buttonObject.transform, "Eraser", new Color(0.93f, 0.48f, 0.52f, 1.0f), along * 11.0f, new Vector2(2.5f, 5.0f), Lean);
            IconPart(buttonObject.transform, "Wood", new Color(0.93f, 0.82f, 0.62f, 1.0f), along * -6.5f, new Vector2(3.5f, 3.5f), Lean);
            IconPart(buttonObject.transform, "Lead", new Color(0.05f, 0.06f, 0.08f, 1.0f), along * -8.8f, new Vector2(1.8f, 1.8f), Lean);
        }

        /// <summary>A little square button on the card's top row, <paramref name="fromRight"/> pixels in from its right end, clear until pointed at.</summary>
        private static GameObject IconButton(Transform card, string name, float fromRight, Color lit, ButtonAction.Kind action, CharacterSelect select)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(card, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1.0f, 1.0f);
            rect.pivot = new Vector2(1.0f, 1.0f);
            rect.anchoredPosition = new Vector2(fromRight, -6.0f);
            rect.sizeDelta = new Vector2(26.0f, 26.0f);

            Image background = buttonObject.AddComponent<Image>();
            background.color = Color.white;   // tinted by the button: clear until pointed at
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colours = button.colors;
            colours.normalColor = new Color(1.0f, 1.0f, 1.0f, 0.0f);
            colours.highlightedColor = new Color(lit.r, lit.g, lit.b, 0.85f);
            colours.selectedColor = colours.normalColor;
            colours.pressedColor = lit;
            button.colors = colours;

            var wiring = buttonObject.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Select = select;
            return buttonObject;
        }

        private static void IconPart(Transform parent, string name, Color colour, Vector2 centre, Vector2 size, float degrees = 0.0f)
        {
            GameObject part = MakeRect(name, parent, colour);
            var rect = (RectTransform)part.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = centre;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0.0f, 0.0f, degrees);
            part.GetComponent<Image>().raycastTarget = false;
        }

        // --- making one ---

        private static GameObject BuildCreate(Transform parent, CharacterSelect select,
            TitleScreen title)
        {
            var panel = new GameObject("CreatePanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            Stretch(rect, 0.0f);

            GameObject wash = MakeRect("Wash", panel.transform,
                new Color(0.0f, 0.0f, 0.0f, 0.72f));
            Stretch((RectTransform)wash.transform, 0.0f);
            wash.GetComponent<Image>().raycastTarget = false;

            MakeLabel(panel.transform, "Heading", "NEW CHARACTER", 26, Gold,
                Anchor(0.5f, 0.5f), new Vector2(0.0f, 130.0f), new Vector2(420.0f, 36.0f));

            select.NameField = MakeNameField(panel.transform);

            MakeLabel(panel.transform, "SkinLabel", "SKIN", 16, Paper,
                Anchor(0.5f, 0.5f), new Vector2(0.0f, 20.0f), new Vector2(420.0f, 24.0f));

            var swatches = new List<Image>();
            for (int i = 0; i < Skins.Length; i++)
            {
                int index = i;
                float span = (Skins.Length - 1) * 52.0f;
                var swatchObject = new GameObject($"Swatch{i}", typeof(RectTransform));
                var swatchRect = (RectTransform)swatchObject.transform;
                swatchRect.SetParent(panel.transform, false);
                swatchRect.anchorMin = swatchRect.anchorMax = new Vector2(0.5f, 0.5f);
                swatchRect.pivot = new Vector2(0.5f, 0.5f);
                swatchRect.anchoredPosition = new Vector2(i * 52.0f - span * 0.5f, -18.0f);
                swatchRect.sizeDelta = new Vector2(42.0f, 42.0f);

                Image image = swatchObject.AddComponent<Image>();
                image.color = Skins[i];

                var outline = swatchObject.AddComponent<Outline>();
                outline.effectColor = Gold;
                outline.effectDistance = new Vector2(3.0f, -3.0f);
                outline.enabled = i == 0;

                Button button = swatchObject.AddComponent<Button>();
                button.targetGraphic = image;
                var wiring = swatchObject.AddComponent<ButtonAction>();
                wiring.Action = ButtonAction.Kind.SelectChooseSkin;
                wiring.Select = select;
                wiring.Value = index;

                swatches.Add(image);
            }
            select.Swatches = swatches.ToArray();

            MakeButton(panel.transform, "CreateButton", "ENTER THE CITY", Gold,
                Anchor(0.5f, 0.5f), new Vector2(-80.0f, -90.0f), new Vector2(220.0f, 44.0f),
                ButtonAction.Kind.SelectConfirmCreate, title, select);
            MakeButton(panel.transform, "CancelButton", "CANCEL", Paper,
                Anchor(0.5f, 0.5f), new Vector2(140.0f, -90.0f), new Vector2(140.0f, 44.0f),
                ButtonAction.Kind.TitleBack, title, select);

            return panel;
        }

        /// <summary>
        /// The name box. Built by hand rather than from Unity's own
        /// DefaultControls, whose generated hierarchy is what a later reader has
        /// to reverse-engineer when a field stops accepting text.
        /// </summary>
        private static InputField MakeNameField(Transform parent)
        {
            var fieldObject = new GameObject("NameField", typeof(RectTransform));
            var rect = (RectTransform)fieldObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0.0f, 72.0f);
            rect.sizeDelta = new Vector2(360.0f, 44.0f);

            Image background = fieldObject.AddComponent<Image>();
            background.color = new Color(0.05f, 0.06f, 0.08f, 0.95f);

            Text text = MakeLabel(fieldObject.transform, "Text", string.Empty, 20, Paper,
                Anchor(0.5f, 0.5f), Vector2.zero, new Vector2(340.0f, 36.0f));
            text.supportRichText = false;

            Text placeholder = MakeLabel(fieldObject.transform, "Placeholder", "Name...", 20,
                new Color(0.45f, 0.45f, 0.48f),
                Anchor(0.5f, 0.5f), Vector2.zero, new Vector2(340.0f, 36.0f));
            placeholder.fontStyle = FontStyle.Italic;

            InputField field = fieldObject.AddComponent<InputField>();
            field.targetGraphic = background;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = CharacterSave.MaximumNameLength;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        // --- deleting one ---

        private static GameObject BuildConfirm(Transform parent, CharacterSelect select)
        {
            var panel = new GameObject("ConfirmPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            Stretch(rect, 0.0f);

            GameObject wash = MakeRect("Wash", panel.transform,
                new Color(0.0f, 0.0f, 0.0f, 0.78f));
            Stretch((RectTransform)wash.transform, 0.0f);
            wash.GetComponent<Image>().raycastTarget = false;

            select.ConfirmText = MakeLabel(panel.transform, "ConfirmText",
                "Delete this character?", 20, Paper,
                Anchor(0.5f, 0.5f), new Vector2(0.0f, 40.0f), new Vector2(460.0f, 70.0f));

            // Delete is on the RIGHT and cancel on the left, so the destructive
            // one is not where the eye lands first or where a repeated click
            // from the previous screen would fall.
            MakeButton(panel.transform, "CancelButton", "KEEP", Paper,
                Anchor(0.5f, 0.5f), new Vector2(-100.0f, -40.0f), new Vector2(160.0f, 44.0f),
                ButtonAction.Kind.SelectCancelDelete, null, select);
            MakeButton(panel.transform, "DeleteButton", "DELETE", Danger,
                Anchor(0.5f, 0.5f), new Vector2(100.0f, -40.0f), new Vector2(160.0f, 44.0f),
                ButtonAction.Kind.SelectConfirmDelete, null, select);

            return panel;
        }

        // --- shared pieces ---

        private static Vector2 Anchor(float x, float y)
        {
            return new Vector2(x, y);
        }

        /// <summary>
        /// Buttons carry a ButtonAction rather than a listener -- see that
        /// class. A listener added from an editor script is not serialized, so
        /// every one of these was dead once the scene had been saved.
        /// </summary>
        private static void MakeButton(Transform parent, string name, string label, Color tint,
            Vector2 anchor, Vector2 position, Vector2 dimensions,
            ButtonAction.Kind action, TitleScreen title, CharacterSelect select, int value = 0)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.09f, 0.10f, 0.13f, 0.92f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;

            var wiring = buttonObject.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Title = title;
            wiring.Select = select;
            wiring.Value = value;

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = tint;
            button.colors = colours;

            MakeLabel(buttonObject.transform, "Label", label, 16, tint,
                Anchor(0.5f, 0.5f), Vector2.zero,
                new Vector2(dimensions.x - 10.0f, dimensions.y - 8.0f));
        }

        private static Text MakeLabel(Transform parent, string name, string content, int size,
            Color colour, Vector2 anchor, Vector2 position, Vector2 dimensions)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            Text text = labelObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static GameObject MakeRect(string name, Transform parent, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = colour;
            return go;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
