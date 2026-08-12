using System.Collections.Generic;
using CityX.Core;
using CityX.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the character select panel onto the title screen's canvas.
    ///
    /// A ROW TEMPLATE RATHER THAN A PREFAB. The rows are cloned at runtime, and
    /// a prefab would be a second binary asset to keep in step with this file.
    /// One inactive GameObject built here and cloned by CharacterSelect is the
    /// same mechanism with nothing to fall out of sync.
    ///
    /// Terraria-style rows, which is what the Godot build settled on: a colour
    /// swatch, the name, the level and playtime, and PLAY and DELETE on the
    /// right.
    /// </summary>
    public static class CharacterSelectBuilder
    {
        private static readonly Color Gold = new Color(0.75f, 0.6f, 0.2f, 1.0f);
        private static readonly Color Paper = new Color(0.82f, 0.82f, 0.84f, 1.0f);
        private static readonly Color RowBackground = new Color(0.07f, 0.08f, 0.10f, 0.90f);
        private static readonly Color Danger = new Color(0.55f, 0.16f, 0.16f, 1.0f);

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

        public static GameObject Build(Transform parent, TitleScreen title)
        {
            var panel = new GameObject("CharacterSelectPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            Stretch(rect, 0.0f);

            CharacterSelect select = panel.AddComponent<CharacterSelect>();
            title.Select = select;

            select.ListPanel = BuildList(panel.transform, select, title);
            select.CreatePanel = BuildCreate(panel.transform, select, title);
            select.ConfirmPanel = BuildConfirm(panel.transform, select);

            select.Show(CharacterSelect.Mode.List);
            return panel;
        }

        // --- the list ---

        private static GameObject BuildList(Transform parent, CharacterSelect select,
            TitleScreen title)
        {
            var panel = new GameObject("ListPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            Stretch(rect, 0.0f);

            // Darker than the title's own wash: a list of rows needs more
            // contrast behind it than three buttons do.
            GameObject wash = MakeRect("Wash", panel.transform,
                new Color(0.0f, 0.0f, 0.0f, 0.66f));
            Stretch((RectTransform)wash.transform, 0.0f);
            wash.GetComponent<Image>().raycastTarget = false;

            MakeLabel(panel.transform, "Heading", "CHARACTERS", 28, Gold,
                Anchor(0.5f, 1.0f), new Vector2(0.0f, -44.0f), new Vector2(420.0f, 40.0f));

            var scroll = new GameObject("Rows", typeof(RectTransform));
            var scrollRect = (RectTransform)scroll.transform;
            scrollRect.SetParent(panel.transform, false);
            scrollRect.anchorMin = new Vector2(0.5f, 1.0f);
            scrollRect.anchorMax = new Vector2(0.5f, 1.0f);
            scrollRect.pivot = new Vector2(0.5f, 1.0f);
            scrollRect.anchoredPosition = new Vector2(0.0f, -80.0f);
            scrollRect.sizeDelta = new Vector2(560.0f, 380.0f);

            var layout = scroll.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8.0f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            select.RowParent = scrollRect;
            select.RowTemplate = BuildRowTemplate(panel.transform);

            select.EmptyNotice = MakeLabel(panel.transform, "EmptyNotice",
                "No characters yet.", 18, Paper,
                Anchor(0.5f, 0.5f), new Vector2(0.0f, 40.0f), new Vector2(420.0f, 30.0f));

            // Into the CREATOR, not straight to the name box. Painting comes
            // first and naming last, which is the order the Godot build used
            // and the order that makes sense: you name the thing you made.
            MakeButton(panel.transform, "NewButton", "NEW CHARACTER", Gold,
                Anchor(0.5f, 0.0f), new Vector2(-90.0f, 58.0f), new Vector2(220.0f, 44.0f),
                ButtonAction.Kind.ShowCreator, title, select);
            MakeButton(panel.transform, "BackButton", "BACK", Paper,
                Anchor(0.5f, 0.0f), new Vector2(150.0f, 58.0f), new Vector2(140.0f, 44.0f),
                ButtonAction.Kind.TitleBack, title, select);

            return panel;
        }

        /// <summary>
        /// The row every character gets, built once and left INACTIVE.
        ///
        /// Parented to the list panel rather than to the row container, so the
        /// layout group does not count the template itself as a row and leave a
        /// permanent gap at the top of the list.
        /// </summary>
        private static GameObject BuildRowTemplate(Transform parent)
        {
            var row = new GameObject("RowTemplate", typeof(RectTransform));
            var rect = (RectTransform)row.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(560.0f, 62.0f);

            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 62.0f;
            element.preferredWidth = 560.0f;

            row.AddComponent<Image>().color = RowBackground;

            var portrait = new GameObject("Portrait", typeof(RectTransform));
            var portraitRect = (RectTransform)portrait.transform;
            portraitRect.SetParent(row.transform, false);
            portraitRect.anchorMin = new Vector2(0.0f, 0.5f);
            portraitRect.anchorMax = new Vector2(0.0f, 0.5f);
            portraitRect.pivot = new Vector2(0.0f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(10.0f, 0.0f);
            portraitRect.sizeDelta = new Vector2(42.0f, 42.0f);
            portrait.AddComponent<Image>().color = CharacterSave.DefaultSkin;

            Text name = MakeLabel(row.transform, "Name", "Name", 20, Paper,
                Anchor(0.0f, 0.5f), new Vector2(64.0f, 10.0f), new Vector2(260.0f, 26.0f));
            name.alignment = TextAnchor.MiddleLeft;

            Text detail = MakeLabel(row.transform, "Detail", "Level 1   0m", 14,
                new Color(0.62f, 0.62f, 0.66f),
                Anchor(0.0f, 0.5f), new Vector2(64.0f, -12.0f), new Vector2(260.0f, 22.0f));
            detail.alignment = TextAnchor.MiddleLeft;

            MakeButton(row.transform, "PlayButton", "PLAY", Gold,
                Anchor(1.0f, 0.5f), new Vector2(-108.0f, 0.0f), new Vector2(96.0f, 38.0f), ButtonAction.Kind.None, null, null);
            MakeButton(row.transform, "DeleteButton", "DELETE", Danger,
                Anchor(1.0f, 0.5f), new Vector2(-12.0f, 0.0f), new Vector2(88.0f, 38.0f), ButtonAction.Kind.None, null, null);

            row.SetActive(false);
            return row;
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
