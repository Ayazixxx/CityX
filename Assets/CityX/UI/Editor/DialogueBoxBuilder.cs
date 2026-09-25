using CityX.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the dialogue box prefab in code, for the same reason the HUD
    /// is: a prefab full of nested RectTransforms is an opaque binary asset.
    /// Layout numbers carried straight over from ui/dialogue_box.gd so the
    /// two builds read the same.
    /// </summary>
    public static class DialogueBoxBuilder
    {
        private const string PrefabPath = "Assets/CityX/UI/DialogueBox.prefab";

        private const float PanelHeight = 168.0f;
        private const float PanelMargin = 48.0f;
        private const float PanelBottomMargin = 56.0f;
        private const float ContentMargin = 18.0f;

        private static readonly Color BackgroundColour = new Color(0.06f, 0.07f, 0.09f, 0.92f);
        private static readonly Color BorderColour = new Color(0.55f, 0.63f, 1.0f, 0.55f);
        private static readonly Color SpeakerColour = new Color(0.72f, 0.79f, 1.0f);
        private static readonly Color LineColour = new Color(0.93f, 0.94f, 0.97f);
        private static readonly Color HintColour = new Color(0.55f, 0.58f, 0.66f);

        [MenuItem("CityX/Rebuild Dialogue Box")]
        public static GameObject Build()
        {
            var root = new GameObject("DialogueBox", typeof(RectTransform));
            try
            {
                Canvas canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Above the HUD (40), below nothing this project has yet --
                // ui/dialogue_box.gd's own layer is 5, the highest CanvasLayer
                // in the project at the time it was written.
                canvas.sortingOrder = 45;

                CanvasScaler scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1152.0f, 648.0f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                root.AddComponent<GraphicRaycaster>();

                DialogueBox box = root.AddComponent<DialogueBox>();

                var panel = new GameObject("Panel", typeof(RectTransform));
                RectTransform panelRect = Attach(panel, root.transform);
                panelRect.anchorMin = new Vector2(0.0f, 1.0f);
                panelRect.anchorMax = new Vector2(1.0f, 1.0f);
                panelRect.pivot = new Vector2(0.5f, 1.0f);
                panelRect.offsetMin = new Vector2(PanelMargin, 0.0f);
                panelRect.offsetMax = new Vector2(-PanelMargin, 0.0f);
                panelRect.anchoredPosition = new Vector2(0.0f, -PanelBottomMargin);
                panelRect.sizeDelta = new Vector2(0.0f, PanelHeight);
                Image panelImage = panel.AddComponent<Image>();
                panelImage.color = BackgroundColour;
                Outline border = panel.AddComponent<Outline>();
                border.effectColor = BorderColour;
                border.effectDistance = new Vector2(2.0f, -2.0f);
                box.Panel = panel;

                var column = new GameObject("Column", typeof(RectTransform));
                RectTransform columnRect = Attach(column, panel.transform);
                Stretch(columnRect, ContentMargin);
                VerticalLayoutGroup layout = column.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 6.0f;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.childControlWidth = true;
                layout.childControlHeight = false;

                box.SpeakerLabel = MakeText(column.transform, "Speaker", 20, SpeakerColour,
                    TextAnchor.UpperLeft, 26.0f);
                box.LineLabel = MakeText(column.transform, "Line", 18, LineColour,
                    TextAnchor.UpperLeft, 48.0f);
                box.LineLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

                var optionsObject = new GameObject("Options", typeof(RectTransform));
                RectTransform optionsRect = Attach(optionsObject, column.transform);
                var optionsLayout = optionsObject.AddComponent<VerticalLayoutGroup>();
                optionsLayout.spacing = 2.0f;
                optionsLayout.childForceExpandWidth = true;
                optionsLayout.childForceExpandHeight = false;
                optionsLayout.childControlHeight = false;
                var optionsFitter = optionsObject.AddComponent<ContentSizeFitter>();
                optionsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                box.OptionsParent = optionsObject.transform;
                box.OptionButtonTemplate = BuildOptionTemplate(optionsObject.transform);

                box.HintLabel = MakeText(column.transform, "Hint", 13, HintColour,
                    TextAnchor.LowerRight, 18.0f);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: dialogue box written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The reply button. Kept inactive under Options so Instantiate can
        /// clone it per option at runtime and DialogueBox never has to build
        /// a Button from primitives itself.
        /// </summary>
        private static GameObject BuildOptionTemplate(Transform parent)
        {
            var template = new GameObject("OptionTemplate", typeof(RectTransform));
            RectTransform rect = Attach(template, parent);
            var layoutElement = template.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 22.0f;

            Image image = template.AddComponent<Image>();
            image.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            Button button = template.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            Text label = MakeText(template.transform, "Label", 18, DialogueBox.OptionColour,
                TextAnchor.MiddleLeft, 0.0f);
            RectTransform labelRect = (RectTransform)label.transform;
            Stretch(labelRect, 0.0f);

            template.SetActive(false);
            return template;
        }

        private static RectTransform Attach(GameObject child, Transform parent)
        {
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Text MakeText(Transform parent, string name, int fontSize, Color colour,
            TextAnchor alignment, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Attach(go, parent);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = colour;
            text.alignment = alignment;
            if (preferredHeight > 0.0f)
            {
                LayoutElement element = go.AddComponent<LayoutElement>();
                element.preferredHeight = preferredHeight;
            }
            return text;
        }
    }
}
