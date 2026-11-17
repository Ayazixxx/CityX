using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The objective in the top-right corner of the HUD: what to do now and a hint how (the tutorial's steps, TutorialDirector). Built in code
    /// on the HUD's canvas, as the hotbar is; hidden while there is nothing to do.
    /// </summary>
    public sealed class ObjectiveView : MonoBehaviour
    {
        /// <summary>Design pixels on the HUD's 1152 x 648 canvas.</summary>
        public const float Width = 290.0f;
        public static readonly Vector2 Corner = new Vector2(-16.0f, -16.0f);
        public const int Padding = 10;

        public const float FadeInSeconds = 0.35f;

        public static readonly Color BoxColour = new Color(0.06f, 0.055f, 0.05f, 0.82f);
        public static readonly Color RimColour = new Color(0.85f, 0.68f, 0.25f, 0.9f);
        public static readonly Color HeadingColour = new Color(1.0f, 0.82f, 0.3f, 1.0f);
        public static readonly Color HintColour = new Color(0.78f, 0.78f, 0.76f, 1.0f);

        public const string HeadingText = "OBJECTIVE";

        public RectTransform Box { get; private set; }
        public Text Line { get; private set; }
        public Text Hint { get; private set; }

        /// <summary>Whether an objective is up.</summary>
        public bool Showing => Box != null && Box.gameObject.activeSelf;

        private CanvasGroup _group;
        private float _shownFor;

        private void Awake()
        {
            Build(transform);
        }

        /// <summary>Builds the box, hidden, in the top-right corner of <paramref name="canvasRoot"/>.</summary>
        public void Build(Transform canvasRoot)
        {
            if (Box != null)
            {
                return;
            }
            var box = new GameObject("Objective", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            Box = (RectTransform)box.transform;
            Box.SetParent(canvasRoot, false);
            Box.SetAsFirstSibling();   // under the death screen and the menus
            Box.anchorMin = Vector2.one;
            Box.anchorMax = Vector2.one;
            Box.pivot = Vector2.one;
            Box.anchoredPosition = Corner;
            Box.sizeDelta = new Vector2(Width, 0.0f);

            Image back = box.GetComponent<Image>();
            back.color = BoxColour;
            back.raycastTarget = false;
            Outline rim = box.GetComponent<Outline>();
            rim.effectColor = RimColour;
            rim.effectDistance = new Vector2(1.5f, -1.5f);

            var layout = box.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(Padding, Padding, Padding - 2, Padding);
            layout.spacing = 3.0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            box.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _group = box.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Text heading = Label("Heading", HeadingText, 11, HeadingColour);
            heading.fontStyle = FontStyle.Bold;
            Line = Label("Line", "", 16, Color.white);
            Line.fontStyle = FontStyle.Bold;
            Hint = Label("Hint", "", 12, HintColour);

            box.SetActive(false);
        }

        private Text Label(string name, string content, int size, Color colour)
        {
            Text text = UiKit.MakeText(Box, name, content, size, colour);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.0f, 0.0f, 0.0f, 0.8f);
            return text;
        }

        /// <summary>Puts <paramref name="objective"/> up with its hint, fading in when it is new; an empty objective takes the box down.</summary>
        public void Show(string objective, string hint)
        {
            if (Box == null)
            {
                Build(transform);
            }
            if (string.IsNullOrEmpty(objective))
            {
                Box.gameObject.SetActive(false);
                return;
            }
            bool changed = !Showing || Line.text != objective;
            Line.text = objective;
            Hint.text = hint ?? "";
            Hint.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            Box.gameObject.SetActive(true);
            if (changed)
            {
                _shownFor = 0.0f;
                _group.alpha = 0.0f;
            }
        }

        private void Update()
        {
            if (!Showing || _group.alpha >= 1.0f)
            {
                return;
            }
            // By the clock, so it fades in with the game paused; by the game's own time where that runs ahead (a render made frame by frame).
            _shownFor += Mathf.Max(Time.unscaledDeltaTime, Time.deltaTime);
            _group.alpha = Mathf.Clamp01(_shownFor / FadeInSeconds);
        }
    }
}
