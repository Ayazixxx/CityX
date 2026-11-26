using System.Collections.Generic;
using CityX.Character;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The Stats page, reached from the Player submenu's Stats button: Level, an XP bar, how many points are left to spend, and every stat
    /// (owner, 2026-10-14: StatCatalog, twenty-four of them under Vitals, Offense, Precision, Mobility and Defense) in a list that scrolls, each
    /// with what its points come to and a "+" that spends one (Shift: five). Reads and writes PlayerVitals (AddXp, SpendStatPoint), owning no
    /// progression of its own.
    ///
    /// THE LIST IS BUILT IN CODE when the page first opens: the scene still holds the old three rows (Vitality, Endurance, Strength) until it is
    /// next rebuilt, and they are hidden. NO DEDICATED HOTKEY: reached through the C menu, which has closed by the time Open() runs; Escape closes.
    ///
    /// POLLED, NOT EVENT-DRIVEN: refreshed on Open and after every "+" this page causes, which is every way the numbers change while a player is
    /// looking at their own page.
    /// </summary>
    public sealed class StatsPanelView : MonoBehaviour
    {
        [Tooltip("The whole overlay, hidden until Open() is called.")]
        public GameObject Root;

        public PlayerVitals Vitals;

        public Text LevelLabel;
        public Text ExperienceLabel;

        [Tooltip("Scaled by moving its right anchor, the same technique PlayerHud's own health/stamina bars use.")]
        public RectTransform ExperienceBarFill;

        public Text PointsLabel;

        [Tooltip("The old rows from before the full list (2026-10-14): hidden when the list is built.")]
        public Text VitalityLabel;
        public Text EnduranceLabel;
        public Text StrengthLabel;

        public Button VitalityButton;
        public Button EnduranceButton;
        public Button StrengthButton;

        public Button CloseButton;

        [Tooltip("Movement/combat input. Disabled while the panel is open.")]
        public PlayerInputReader InputReader;

        [Tooltip("Mouse-look. Disabled while the panel is open.")]
        public ThirdPersonCamera OrbitCamera;

        /// <summary>Points one click spends with Shift held.</summary>
        public const int ShiftClickPoints = 5;

        private static readonly Color HeadingColour = new Color(1.0f, 0.83f, 0.42f);
        private static readonly Color NameColour = new Color(0.9f, 0.9f, 0.92f);
        private static readonly Color DescriptionColour = new Color(0.55f, 0.58f, 0.66f);
        private static readonly Color EffectColour = new Color(0.72f, 0.79f, 1.0f);
        private static readonly Color NotYetColour = new Color(0.45f, 0.47f, 0.52f);
        private static readonly Color RowColour = new Color(0.14f, 0.15f, 0.19f, 1.0f);
        private static readonly Color PlusColour = new Color(0.2f, 0.21f, 0.27f, 1.0f);

        private const float ListTop = 182.0f;
        private const float ListBottom = 66.0f;
        private const float RowHeight = 46.0f;
        private const float HeadingHeight = 30.0f;

        /// <summary>One stat's row.</summary>
        public sealed class StatRow
        {
            public StatId Id;
            public Text Name;
            public Text Effect;
            public Text Points;
            public Button Plus;
        }

        private readonly List<StatRow> _rows = new List<StatRow>();
        private readonly MenuFocus _focus = new MenuFocus();

        public bool IsOpen => Root != null && Root.activeSelf;

        /// <summary>The list's rows, once built.</summary>
        public IReadOnlyList<StatRow> Rows => _rows;

        private void Awake()
        {
            if (CloseButton != null) CloseButton.onClick.AddListener(Close);
            // The page wakes the first time it is shown (it sits on its own hidden object): closing it then would undo that very Open.
            if (!_opening)
            {
                Close();
            }
        }

        private bool _opening;

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && GameControls.Now.Cancel.Pressed)
            {
                Close();
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }
            _focus.Take(this, InputReader, OrbitCamera);

            _opening = true;
            if (Root != null) Root.SetActive(true);
            _opening = false;
            BuildList();
            Refresh();
        }

        public void Close()
        {
            if (Root != null) Root.SetActive(false);

            _focus.GiveBack(this, InputReader, OrbitCamera);
        }

        /// <summary>Spends points on a stat: one, or <see cref="ShiftClickPoints"/> with Shift held.</summary>
        public void Spend(StatId id, int points = 1)
        {
            if (Vitals == null)
            {
                return;
            }
            Vitals.SpendStatPoints(id, points);
            Refresh();
        }

        private void OnPlusClicked(StatId id)
        {
            Keyboard keyboard = Keyboard.current;
            bool shift = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            Spend(id, shift ? ShiftClickPoints : 1);
        }

        public void Refresh()
        {
            if (Vitals == null)
            {
                return;
            }

            if (LevelLabel != null) LevelLabel.text = "LEVEL " + Vitals.Level;
            if (ExperienceLabel != null)
            {
                ExperienceLabel.text = Vitals.Experience + " / " + Vitals.ExperienceToNextLevel + " XP";
            }
            if (ExperienceBarFill != null)
            {
                float toNext = Vitals.ExperienceToNextLevel;
                float fraction = toNext > 0.0f ? Mathf.Clamp01((float)Vitals.Experience / toNext) : 0.0f;
                // Moves the bar's right anchor rather than setting a pixel width, the same technique PlayerHud's own bars use: an anchor
                // fraction stays correct when the canvas scaler resizes everything.
                ExperienceBarFill.anchorMin = new Vector2(0.0f, 0.0f);
                ExperienceBarFill.anchorMax = new Vector2(fraction, 1.0f);
                ExperienceBarFill.offsetMin = Vector2.zero;
                ExperienceBarFill.offsetMax = Vector2.zero;
            }
            if (PointsLabel != null)
            {
                PointsLabel.text = Vitals.StatPoints == 1 ? "1 point to spend" : Vitals.StatPoints + " points to spend";
            }

            bool canSpend = Vitals.StatPoints > 0;
            foreach (StatRow row in _rows)
            {
                StatDefinition stat = StatCatalog.Get(row.Id);
                int points = Vitals.Stats[row.Id];
                row.Points.text = points.ToString();
                row.Effect.text = EffectText(stat, points);
                row.Plus.interactable = canSpend;
            }
        }

        /// <summary>What a stat's points come to on the page; Block Chance shows the whole chance, everyone's share included.</summary>
        public static string EffectText(StatDefinition stat, int points)
        {
            if (!stat.InGame)
            {
                return "not in the game yet";   // wings and the new cars come later (owner, 2026-10-14)
            }
            if (stat.Id == StatId.BlockChance)
            {
                return $"{(StatCatalog.BaseBlockChance + stat.Effect(points)) * 100.0f:0.#}% block chance";
            }
            return points == 0 ? "-" : stat.Describe(points);
        }

        // --- the list ---

        private void BuildList()
        {
            if (_rows.Count > 0 || Root == null)
            {
                return;
            }
            Transform panel = Root.transform.Find("Panel");
            if (panel == null)
            {
                panel = Root.transform;
            }

            // The old three rows, from before every stat had one: each row's strip when it has one (the scene's "Row"), else the label and
            // the button themselves -- never the page.
            foreach (Component old in new Component[] { VitalityLabel, EnduranceLabel, StrengthLabel, VitalityButton, EnduranceButton, StrengthButton })
            {
                if (old == null)
                {
                    continue;
                }
                Transform parent = old.transform.parent;
                bool inARow = parent != null && parent != panel && parent != Root.transform && parent.name == "Row";
                (inARow ? parent.gameObject : old.gameObject).SetActive(false);
            }

            // A window that scrolls with the wheel or a drag.
            var viewport = new GameObject("StatList", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            var viewRect = (RectTransform)viewport.transform;
            viewRect.SetParent(panel, false);
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = new Vector2(24.0f, ListBottom);
            viewRect.offsetMax = new Vector2(-24.0f, -ListTop);
            viewport.GetComponent<Image>().color = new Color(0.0f, 0.0f, 0.0f, 0.18f);

            var content = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var contentRect = (RectTransform)content.transform;
            contentRect.SetParent(viewRect, false);
            contentRect.anchorMin = new Vector2(0.0f, 1.0f);
            contentRect.anchorMax = new Vector2(1.0f, 1.0f);
            contentRect.pivot = new Vector2(0.5f, 1.0f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4.0f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28.0f;

            StatGroup? group = null;
            foreach (StatDefinition stat in StatCatalog.All)
            {
                if (group != stat.Group)
                {
                    group = stat.Group;
                    MakeHeading(contentRect, stat.Group.ToString().ToUpperInvariant());
                }
                _rows.Add(MakeRow(contentRect, stat));
            }
            UiKit.EnsureEventSystem();
        }

        private static void MakeHeading(Transform parent, string text)
        {
            var heading = new GameObject("Heading", typeof(RectTransform), typeof(LayoutElement));
            heading.transform.SetParent(parent, false);
            heading.GetComponent<LayoutElement>().preferredHeight = HeadingHeight;
            Text label = MakeText(heading.transform, "Label", text, 16, HeadingColour, TextAnchor.LowerLeft);
            label.fontStyle = FontStyle.Bold;
            Fill((RectTransform)label.transform, new Vector2(6.0f, 2.0f), new Vector2(-6.0f, 0.0f));
        }

        private StatRow MakeRow(Transform parent, StatDefinition stat)
        {
            var row = new GameObject(stat.Name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<Image>().color = RowColour;
            row.GetComponent<LayoutElement>().preferredHeight = RowHeight;

            var result = new StatRow { Id = stat.Id };
            result.Name = MakeText(row.transform, "Name", stat.Name, 15, stat.InGame ? NameColour : NotYetColour, TextAnchor.UpperLeft);
            TopLeft((RectTransform)result.Name.transform, new Vector2(12.0f, -5.0f), new Vector2(260.0f, 20.0f));
            Text description = MakeText(row.transform, "Description", stat.Description, 11, DescriptionColour, TextAnchor.UpperLeft);
            TopLeft((RectTransform)description.transform, new Vector2(12.0f, -25.0f), new Vector2(560.0f, 16.0f));
            description.horizontalOverflow = HorizontalWrapMode.Wrap;

            result.Effect = MakeText(row.transform, "Effect", string.Empty, 13, stat.InGame ? EffectColour : NotYetColour, TextAnchor.MiddleRight);
            Anchored((RectTransform)result.Effect.transform, new Vector2(1.0f, 0.5f), new Vector2(-104.0f, 0.0f), new Vector2(300.0f, 20.0f));
            result.Points = MakeText(row.transform, "Points", "0", 16, NameColour, TextAnchor.MiddleCenter);
            Anchored((RectTransform)result.Points.transform, new Vector2(1.0f, 0.5f), new Vector2(-56.0f, 0.0f), new Vector2(40.0f, 20.0f));

            var plus = new GameObject("Plus", typeof(RectTransform), typeof(Image), typeof(Button));
            var plusRect = (RectTransform)plus.transform;
            plusRect.SetParent(row.transform, false);
            Anchored(plusRect, new Vector2(1.0f, 0.5f), new Vector2(-8.0f, 0.0f), new Vector2(34.0f, 34.0f));
            var plusImage = plus.GetComponent<Image>();
            plusImage.color = PlusColour;
            result.Plus = plus.GetComponent<Button>();
            result.Plus.targetGraphic = plusImage;
            StatId id = stat.Id;
            result.Plus.onClick.AddListener(() => OnPlusClicked(id));
            Text plusLabel = MakeText(plus.transform, "Label", "+", 20, HeadingColour, TextAnchor.MiddleCenter);
            Fill((RectTransform)plusLabel.transform, Vector2.zero, Vector2.zero);
            return result;
        }

        private static Text MakeText(Transform parent, string name, string content, int size, Color colour, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.0f, 1.0f);
            rect.pivot = new Vector2(0.0f, 1.0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Anchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Fill(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
