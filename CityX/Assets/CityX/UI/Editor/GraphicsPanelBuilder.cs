using System;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the settings' graphics page (<see cref="CityX.UI.GraphicsSettingsPanel"/>) inside a settings panel, for the title screen and the
    /// pause menu alike: a heading, one row a <see cref="CityX.UI.GraphicsMenu"/> row (name, arrows, value), a line saying what the row does, and
    /// BACK. Each screen hands over its own way of making labels and buttons, so the page looks like the panel it lies on.
    /// </summary>
    public static class GraphicsPanelBuilder
    {
        public sealed class Style
        {
            /// <summary>A label: parent, name, text, font size, centre, size.</summary>
            public Func<Transform, string, string, int, Vector2, Vector2, Text> Label;

            /// <summary>A small square arrow button: parent, name, glyph, centre.</summary>
            public Func<Transform, string, string, Vector2, Button> Arrow;

            /// <summary>A wide button: parent, name, text, centre.</summary>
            public Func<Transform, string, string, Vector2, Button> Button;

            public Color HeadingColour = Color.white;
            public int HeadingSize = 24;
            public int RowSize = 12;
            public float Width = 360.0f;

            /// <summary>Where the heading sits, and the first row under it, the gap between rows, and BACK.</summary>
            public float HeadingY = 262.0f;

            public float FirstRowY = 200.0f;
            public float RowGap = 44.0f;
            public float BackY = -274.0f;

            /// <summary>The row's name's centre, the arrows' and the value's across the panel.</summary>
            public float NameX = -100.0f;

            public float PreviousX = 40.0f;
            public float ValueX = 95.0f;
            public float NextX = 150.0f;
        }

        public static CityX.UI.GraphicsSettingsPanel Build(Transform settingsPanel, Style style)
        {
            var page = new GameObject("GraphicsPanel", typeof(RectTransform));
            var rect = (RectTransform)page.transform;
            rect.SetParent(settingsPanel, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var panel = page.AddComponent<CityX.UI.GraphicsSettingsPanel>();

            style.Label(page.transform, "Heading", "GRAPHICS", style.HeadingSize, new Vector2(0.0f, style.HeadingY), new Vector2(style.Width, 40.0f))
                .color = style.HeadingColour;

            int rows = CityX.UI.GraphicsMenu.Rows.Length;
            panel.Values = new Text[rows];
            panel.PreviousButtons = new Button[rows];
            panel.NextButtons = new Button[rows];
            for (int row = 0; row < rows; row++)
            {
                float y = style.FirstRowY - row * style.RowGap;
                string name = CityX.UI.GraphicsMenu.Rows[row].Name;
                string key = name.Replace(" ", "").Replace("-", "");
                Text label = style.Label(page.transform, key + "Label", name, style.RowSize, new Vector2(style.NameX, y), new Vector2(170.0f, 24.0f));
                label.alignment = TextAnchor.MiddleLeft;
                panel.PreviousButtons[row] = style.Arrow(page.transform, key + "Previous", "<", new Vector2(style.PreviousX, y));
                panel.Values[row] = style.Label(page.transform, key + "Value", "", style.RowSize, new Vector2(style.ValueX, y), new Vector2(80.0f, 24.0f));
                panel.NextButtons[row] = style.Arrow(page.transform, key + "Next", ">", new Vector2(style.NextX, y));
            }

            float lastRowY = style.FirstRowY - (rows - 1) * style.RowGap;
            float descriptionY = (lastRowY - style.RowGap * 0.5f + style.BackY + 24.0f) * 0.5f;
            panel.Description = style.Label(page.transform, "Description", "", style.RowSize - 1, new Vector2(0.0f, descriptionY),
                new Vector2(style.Width - 40.0f, 70.0f));
            panel.Description.horizontalOverflow = HorizontalWrapMode.Wrap;
            panel.Description.verticalOverflow = VerticalWrapMode.Truncate;
            panel.Description.alignment = TextAnchor.MiddleCenter;
            // Small type over the lit windows behind the title screen's panel: outlined, or it cannot be read.
            panel.Description.gameObject.AddComponent<Outline>().effectColor = new Color(0.0f, 0.0f, 0.0f, 0.9f);

            panel.BackButton = style.Button(page.transform, "GraphicsBackButton", "BACK", new Vector2(0.0f, style.BackY));
            page.SetActive(false);
            return panel;
        }
    }
}
