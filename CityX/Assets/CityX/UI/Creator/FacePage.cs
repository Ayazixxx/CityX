using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The creator's FACE tab (docs/CHARACTER_CREATOR.md): premade face parts for anybody who would rather not draw a face. A chip for each
    /// kind of part (FacePartCatalog: eyes, brows, mouth, nose, extras) and, under the lit chip, that kind's parts as pictures, laid out and
    /// scrolled like the hairstyles (CreatorPageKit). None is made yet, so each kind says so, and the page says where a face is drawn until
    /// then. Last, BEARD: the beards (CharacterHair.ValidBeards), worn at a click in the hair's colour (owner, 2026-11-22: "try making a few basic
    /// beard styles and put them into the editor"). Canvas units from the top of the creator's panel, under its tabs; for the title screen's
    /// creator and ReDraw's alike.
    /// </summary>
    public static class FacePage
    {
        /// <summary>The chips share the tile window's width, so a kind added to the catalog still fits the row.</summary>
        public const float ChipRowY = -96.0f, ChipHeight = 24.0f, ChipGap = 2.0f;

        public const float GridTopY = -122.0f;

        public const string BeardChipLabel = "BEARD";

        /// <summary>Six chips share the row: their names at a size "EXTRAS" and "MOUTH" still fit a chip at.</summary>
        private const int ChipLabelSize = 8;

        public const string BeardNote = "Worn in the hair's colour: change it on the HAIR tab.";

        private const float BeardNoteY = -430.0f;

        public const string ComingNote = "Premade eyes, brows and mouths are coming. Until then, paint your face on the PAINT tab.";

        private const float NoteY = -374.0f;

        /// <summary>Behind a kind with no parts: the window's place, faintly, so the page shows where the parts will be.</summary>
        private static readonly Color EmptyWindow = new Color(0.17f, 0.18f, 0.22f, 0.45f);

        /// <param name="creator">The body the beards go on (the face parts do nothing yet).</param>
        public static void Build(Transform page, CharacterCreator creator)
        {
            BuildFrom(page, FacePartCatalog.Current, creator);
        }

        /// <summary>The page from <paramref name="catalog"/> (the game's, or a test's own), and the beards for <paramref name="creator"/>.</summary>
        public static FacePartChips BuildFrom(Transform page, FacePartCatalog catalog, CharacterCreator creator = null)
        {
            int parts = catalog.Categories.Count;
            int count = parts + 1;
            var row = new GameObject("FaceChips", typeof(RectTransform));
            var rowRect = (RectTransform)row.transform;
            rowRect.SetParent(page, false);
            UiKit.Stretch(rowRect);
            FacePartChips chips = row.AddComponent<FacePartChips>();
            chips.Chips = new Button[count];
            chips.ChipFaces = new Image[count];
            chips.ChipLabels = new Text[count];
            chips.Panels = new CanvasGroup[count];

            float pitch = CreatorPageKit.WindowWidth / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                bool beard = i == parts;
                FacePartCatalog.Category category = beard ? null : catalog.Categories[i];
                string key = beard ? "beard" : category.Key;
                Image face = CreatorPageKit.MakeRect<Image>(row.transform, $"Face{key}Chip",
                    new Vector2((i - (count - 1) * 0.5f) * pitch, ChipRowY), new Vector2(pitch - ChipGap, ChipHeight));
                Button button = face.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                ColorBlock colours = button.colors;
                colours.highlightedColor = Color.white;
                colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1.0f);
                button.colors = colours;

                chips.Chips[i] = button;
                chips.ChipFaces[i] = face;
                // 10, not the tools' 13: EXTRAS has to fit a fifth of the row.
                chips.ChipLabels[i] = CreatorKit.Caption(face.transform, beard ? BeardChipLabel : category.Label, ChipLabelSize, CreatorKit.Paper);
                chips.Panels[i] = beard ? BuildBeardPanel(page, creator) : BuildPanel(page, category);
            }
            chips.Show(0);

            CreatorKit.MakeLabel(page, "FaceComingLabel", ComingNote, 11, CreatorKit.Hint, new Vector2(0.0f, NoteY), 48.0f);
            CreatorPageKit.MakeTurnHint(page, "FaceHintLabel");
            return chips;
        }

        /// <summary>The beards as tiles (their names until they have pictures), the one worn framed in gold (BeardTiles), each putting its beard on.</summary>
        private static CanvasGroup BuildBeardPanel(Transform page, CharacterCreator creator)
        {
            var panel = new GameObject("FacebeardPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(page, false);
            UiKit.Stretch(rect);
            CanvasGroup group = panel.AddComponent<CanvasGroup>();

            int count = CharacterHair.ValidBeards.Length;
            Transform tiles = CreatorPageKit.MakeTileWindow(panel.transform, "FacebeardGrid", GridTopY, count);
            var frames = new Image[count];
            for (int i = 0; i < count; i++)
            {
                string beard = CharacterHair.ValidBeards[i];
                Image frame = CreatorPageKit.MakeTile(tiles, "Beard" + beard + "Button", i);
                Button button = frame.gameObject.AddComponent<Button>();
                button.targetGraphic = frame;
                ColorBlock colours = button.colors;
                colours.highlightedColor = CreatorKit.Paper;
                colours.pressedColor = CreatorKit.Gold;
                button.colors = colours;
                var wiring = frame.gameObject.AddComponent<ButtonAction>();
                wiring.Action = ButtonAction.Kind.CreatorBeard;
                wiring.Creator = creator;
                wiring.Value = i;
                CreatorPageKit.ShowPicture(frame.transform, null, CharacterHair.BeardLabelFor(beard));
                frames[i] = frame;
            }
            BeardTiles selection = tiles.parent.gameObject.AddComponent<BeardTiles>();
            selection.Creator = creator;
            selection.Frames = frames;
            CreatorKit.MakeLabel(panel.transform, "BeardNoteLabel", BeardNote, 11, CreatorKit.Hint, new Vector2(0.0f, BeardNoteY), 30.0f);
            return group;
        }

        /// <summary>One kind's panel: its parts as tiles, or, with none yet, the window's place saying so.</summary>
        private static CanvasGroup BuildPanel(Transform page, FacePartCatalog.Category category)
        {
            var panel = new GameObject($"Face{category.Key}Panel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(page, false);
            UiKit.Stretch(rect);
            CanvasGroup group = panel.AddComponent<CanvasGroup>();

            if (category.Parts.Count == 0)
            {
                Image window = CreatorPageKit.MakeRect<Image>(panel.transform, $"Face{category.Key}Empty",
                    new Vector2(0.0f, GridTopY - CreatorPageKit.WindowHeight * 0.5f),
                    new Vector2(CreatorPageKit.WindowWidth, CreatorPageKit.WindowHeight));
                window.color = EmptyWindow;
                window.raycastTarget = false;
                CreatorKit.Caption(window.transform, $"No premade {category.Noun} yet.", 13, CreatorKit.Paper);
                return group;
            }

            Transform tiles = CreatorPageKit.MakeTileWindow(panel.transform, $"Face{category.Key}Grid", GridTopY, category.Parts.Count);
            for (int i = 0; i < category.Parts.Count; i++)
            {
                FacePartCatalog.Part part = category.Parts[i];
                string id = part.Id ?? string.Empty;
                Image frame = CreatorPageKit.MakeTile(tiles, $"Face{category.Key}{id}Tile", i);
                // Shown, not picked: wearing a part comes with the parts themselves, and a Button here would need a ButtonAction.
                frame.raycastTarget = false;
                CreatorPageKit.ShowPicture(frame.transform, part.Icon, id.ToUpperInvariant());
            }
            return group;
        }
    }
}
