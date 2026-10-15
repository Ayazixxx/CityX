using System.Collections.Generic;
using System.Text;
using CityX.Character;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// THE SKILL MENU (owner, 2026-10-14: "i want the skill tree to be scrollable like the skyrim skill tree but in our game it wouldnt be a fixed
    /// perspective. make it one big menu ... skyrim is right to have them all adjacent side by side but you should be able to click and drag across
    /// as well as scroll"). Reached from the C menu: Player, then Skills.
    ///
    /// One big night sky with every tree standing side by side as a constellation, growing up from its name (SkillCatalog's order). Drag anywhere
    /// to move across it (and up a tall tree); the mouse wheel slides along the row; Ctrl and the wheel, or the buttons, zoom, out to the whole row
    /// at once. The row wraps round like Skyrim's sky (SkillMapLayout). Click a star: its card (bottom right) says what it does and whether it can
    /// be learnt, with a Learn button. A learnt spell or technique goes on the bar (bottom left, the same ten round slots as the HUD's): drag the
    /// star onto a slot, click the star and then a slot, or click it and press a key from 1 to 0. Right-click a slot to empty it. Movement keeps its
    /// own controls and never goes on the bar (owner). Reset gives every point back (two clicks).
    ///
    /// On a controller: the left stick moves, the right stick zooms, the bumpers jump from tree to tree, A picks the star in the middle (and learns
    /// it on a second press), Y puts it on the next slot, B closes.
    ///
    /// Built in code the first time it opens, like ReDraw's creator: nothing in the scene to rebuild, no image files (HudArt, SkillIcons).
    /// </summary>
    public sealed class SkillMenuView : MonoBehaviour
    {
        public static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);

        /// <summary>Above the HUD (40) and the Stats/Loadout panels (55).</summary>
        public const int SortingOrder = 56;

        public const float MaxZoom = 1.6f;
        public const float StartZoom = 1.0f;

        /// <summary>Below this zoom the stars' names are too small to read and are hidden; the trees' names grow instead.</summary>
        public const float LabelsHideBelowZoom = 0.62f;

        /// <summary>How far one notch of the wheel slides the row, and how much one notch zooms with Ctrl held.</summary>
        public const float SlidePerNotch = 120.0f;
        public const float ZoomPerNotch = 1.15f;

        /// <summary>Map units: a star's size, an empty star's, the lines' width, a tree's base below the middle of the view at the start.</summary>
        public const float StarSize = 46.0f;
        public const float EmptyStarSize = 32.0f;
        public const float LineWidth = 3.0f;
        public const float BaseY = -165.0f;

        /// <summary>How far up or down the map can be moved from where it starts.</summary>
        public const float PanLimitY = 220.0f;

        /// <summary>A controller moves the map this many design pixels a second at full tilt, and picks the star within this of the middle.</summary>
        public const float StickPanSpeed = 750.0f;
        public const float StickPickRadius = 90.0f;

        public const float ResetConfirmSeconds = 3.0f;

        private static readonly Color Sky = new Color(0.043f, 0.059f, 0.11f, 0.97f);
        private static readonly Color Gold = new Color(1.0f, 0.83f, 0.42f, 1.0f);
        private static readonly Color LearntFace = new Color(0.79f, 0.64f, 0.29f, 1.0f);
        private static readonly Color LearntIcon = new Color(0.16f, 0.11f, 0.04f, 1.0f);
        private static readonly Color DarkFace = new Color(0.08f, 0.1f, 0.17f, 1.0f);
        private static readonly Color LockedRim = new Color(0.36f, 0.39f, 0.45f, 1.0f);
        private static readonly Color LockedIcon = new Color(0.5f, 0.53f, 0.6f, 1.0f);
        private static readonly Color LineLearnt = new Color(0.62f, 0.52f, 0.28f, 1.0f);
        private static readonly Color LineOther = new Color(0.27f, 0.29f, 0.36f, 1.0f);
        private static readonly Color LabelInk = new Color(0.84f, 0.86f, 0.94f, 1.0f);
        private static readonly Color HintInk = new Color(0.58f, 0.61f, 0.7f, 1.0f);
        private static readonly Color ButtonFace = new Color(0.16f, 0.18f, 0.25f, 0.95f);
        private static readonly Color StarDot = new Color(0.79f, 0.83f, 1.0f, 1.0f);

        private enum StarState { Learnt, Ready, Locked, Empty }

        private sealed class StarGraphics
        {
            public SkillNode Node;
            public RectTransform Root;
            public Image Face;
            public Image Rim;
            public Image Icon;
            public Image Selection;
            public Text Mark;
            public Text Label;
        }

        private sealed class LineGraphics
        {
            public string From;
            public string To;
            public readonly List<Image> Pieces = new List<Image>();
        }

        private static SkillMenuView _instance;

        /// <summary>The menu, once it has been opened (it is built then).</summary>
        public static SkillMenuView Instance => _instance;

        public static bool IsAnyOpen => _instance != null && _instance.IsOpen;

        /// <summary>The frame it closed on: the pause menu (Escape) and the C menu (C), which answer the same keys, leave that press alone.</summary>
        public static int ClosedOnFrame { get; private set; } = -1;

        public PlayerSkills Skills { get; private set; }
        public string Selected { get; private set; }

        /// <summary>The row's x at the middle of the view.</summary>
        public float Scroll { get; private set; }

        /// <summary>The map's y at the middle of the view (0 where it starts).</summary>
        public float PanY { get; private set; }

        public float Zoom { get; private set; } = StartZoom;

        public SkillMapLayout Layout { get; } = new SkillMapLayout();

        public bool IsOpen => _root != null && _root.activeSelf;

        private GameObject _root;
        private Canvas _canvas;
        private RectTransform _viewport;
        private RectTransform _content;
        private readonly RectTransform[] _copies = new RectTransform[2];
        private readonly List<StarGraphics> _stars = new List<StarGraphics>();
        private readonly List<LineGraphics> _lines = new List<LineGraphics>();
        private readonly List<Text> _treeNames = new List<Text>();
        private Text _points;
        private GameObject _card;
        private Text _cardTitle;
        private Text _cardSubtitle;
        private Text _cardBody;
        private Button _learn;
        private Text _resetLabel;
        private float _resetArmedUntil = -1.0f;
        private HotbarView.SlotGraphics[] _barSlots;
        private Image _carryGhost;
        private string _carrying;
        private PlayerInputReader _input;
        private ThirdPersonCamera _camera;
        private bool _wasCursorCaptured;
        private bool _dirty = true;
        private bool _placedOnce;

        /// <summary>Opens the menu on a body's skills, building it the first time; the player's controls and camera rest while it is open.</summary>
        public static SkillMenuView Show(PlayerSkills skills, PlayerInputReader input, ThirdPersonCamera camera)
        {
            if (_instance == null)
            {
                var go = new GameObject("SkillMenu");
                _instance = go.AddComponent<SkillMenuView>();
            }
            _instance.Open(skills, input, camera);
            return _instance;
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            BuildInterface();
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            Watch(null);
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void Open(PlayerSkills skills, PlayerInputReader input, ThirdPersonCamera camera)
        {
            Watch(skills);
            _input = input;
            _camera = camera;
            if (!IsOpen)
            {
                _wasCursorCaptured = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (_input != null) _input.enabled = false;
                if (_camera != null) _camera.enabled = false;
            }
            EnsureEventSystem();
            _root.SetActive(true);
            if (!_placedOnce)
            {
                _placedOnce = true;
                Scroll = Layout.TreeCentre(Layout.TreeOrder[0]) + SkillCatalog.TreeWidth;
                PanY = 0.0f;
                Zoom = StartZoom;
            }
            _dirty = true;
            Refresh();
            ApplyView();
        }

        public void Close()
        {
            if (_root == null || !_root.activeSelf)
            {
                return;
            }
            EndCarry(null);
            _root.SetActive(false);
            ClosedOnFrame = Time.frameCount;
            if (_input != null) _input.enabled = true;
            if (_camera != null) _camera.enabled = true;
            if (_wasCursorCaptured)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Watch(PlayerSkills skills)
        {
            if (Skills == skills)
            {
                return;
            }
            if (Skills != null)
            {
                Skills.Changed -= MarkDirty;
            }
            Skills = skills;
            if (Skills != null)
            {
                Skills.Changed += MarkDirty;
            }
            _dirty = true;
        }

        private void MarkDirty() => _dirty = true;

        // --- what the player does (public: the handlers and the tests drive these) ---

        /// <summary>Picks a star (null: none) and shows its card.</summary>
        public void Select(string id)
        {
            Selected = SkillCatalog.Find(id) != null ? id : null;
            _dirty = true;
            Refresh();
        }

        /// <summary>Learns the chosen star if it can be.</summary>
        public SkillBook.Refusal LearnSelected()
        {
            if (Skills == null || Selected == null)
            {
                return SkillBook.Refusal.Unknown;
            }
            SkillBook.Refusal refusal = Skills.TryLearn(Selected);
            _dirty = true;
            Refresh();
            return refusal;
        }

        /// <summary>The chosen star can go on the bar: a learnt spell or technique.</summary>
        public bool SelectedGoesOnBar => CanCarry(Selected);

        private bool CanCarry(string id)
        {
            SkillNode node = SkillCatalog.Find(id);
            return node != null && node.GoesOnBar && Skills != null && Skills.Knows(id);
        }

        /// <summary>Puts the chosen star in a slot of the bar.</summary>
        public bool PutSelectedOnBar(int slot)
        {
            if (!SelectedGoesOnBar)
            {
                return false;
            }
            bool done = Skills.PutOnBar(slot, Selected);
            _dirty = true;
            Refresh();
            return done;
        }

        /// <summary>A click on a slot: with a learnt spell chosen, it goes there; otherwise the slot's own skill is chosen.</summary>
        public void ClickSlot(int slot)
        {
            if (Skills == null)
            {
                return;
            }
            if (SelectedGoesOnBar && Skills.Bar[slot] != Selected)
            {
                PutSelectedOnBar(slot);
                return;
            }
            if (Skills.Bar[slot] != null)
            {
                Select(Skills.Bar[slot]);
            }
        }

        public void ClearSlot(int slot)
        {
            Skills?.ClearSlot(slot);
            _dirty = true;
            Refresh();
        }

        /// <summary>Moves the map with the pointer: <paramref name="delta"/> in design pixels.</summary>
        public void Pan(Vector2 delta)
        {
            Scroll -= delta.x / Zoom;
            PanY -= delta.y / Zoom;
            ApplyView();
        }

        /// <summary>Slides the row along: one notch of the wheel is <see cref="SlidePerNotch"/> design pixels.</summary>
        public void Slide(float notches)
        {
            Scroll += notches * SlidePerNotch / Zoom;
            ApplyView();
        }

        /// <summary>Zooms by a factor, keeping the point under <paramref name="viewPoint"/> (design pixels from the middle of the view) still.</summary>
        public void ZoomBy(float factor, Vector2 viewPoint)
        {
            float before = Zoom;
            float after = Mathf.Clamp(Zoom * factor, MinZoom, MaxZoom);
            Vector2 under = new Vector2(Scroll, PanY) + viewPoint / before;
            Zoom = after;
            Scroll = under.x - viewPoint.x / after;
            PanY = under.y - viewPoint.y / after;
            ApplyView();
        }

        /// <summary>Out to the whole row at once, from its first tree on the left to its last on the right.</summary>
        public void ShowAll()
        {
            Zoom = MinZoom;
            PanY = 0.0f;
            Scroll = Layout.RowWidth * 0.5f;
            ApplyView();
        }

        /// <summary>Centres the next tree along (+1) or the one before (-1).</summary>
        public void JumpTree(int direction)
        {
            Scroll = Layout.ScrollToNeighbour(Scroll, direction);
            ApplyView();
        }

        /// <summary>The furthest the view can zoom out: the whole row across it.</summary>
        public float MinZoom => Mathf.Min(StartZoom, Layout.ZoomToShowAll(ViewWidth));

        private float ViewWidth => _viewport != null && _viewport.rect.width > 1.0f ? _viewport.rect.width : DesignResolution.x;

        /// <summary>The star nearest a point of the view (design pixels from its middle), within <paramref name="radius"/>; null if none.</summary>
        public string StarNear(Vector2 viewPoint, float radius)
        {
            string best = null;
            float bestDistance = radius;
            Vector2 mapPoint = new Vector2(Scroll, PanY) + viewPoint / Zoom;
            foreach (SkillNode node in SkillCatalog.Nodes)
            {
                Vector2 star = Layout.StarPosition(node) + new Vector2(0.0f, BaseY);
                float across = Layout.Across(mapPoint.x, star.x);
                float distance = new Vector2(across, star.y - mapPoint.y).magnitude * Zoom;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node.Id;
                }
            }
            return best;
        }

        /// <summary>Where a star is drawn now, in design pixels from the middle of the view (the copy nearest the middle).</summary>
        public Vector2 OnScreen(string id)
        {
            SkillNode node = SkillCatalog.Find(id);
            if (node == null)
            {
                return new Vector2(float.NaN, float.NaN);
            }
            Vector2 star = Layout.StarPosition(node) + new Vector2(0.0f, BaseY);
            return new Vector2(Layout.Across(Scroll, star.x), star.y - PanY) * Zoom;
        }

        // --- dragging a star onto the bar ---

        /// <summary>Starts carrying a star to the bar; false when it cannot go there (the drag then moves the map).</summary>
        public bool BeginCarry(string id, Vector2 screenPosition)
        {
            if (!CanCarry(id))
            {
                return false;
            }
            _carrying = id;
            Select(id);
            _carryGhost.sprite = SkillIcons.For(id);
            _carryGhost.gameObject.SetActive(true);
            MoveCarry(screenPosition);
            return true;
        }

        public bool IsCarrying => _carrying != null;

        public void MoveCarry(Vector2 screenPosition)
        {
            if (_carrying == null)
            {
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_root.transform, screenPosition, null, out Vector2 local);
            _carryGhost.rectTransform.anchoredPosition = local;
        }

        /// <summary>Drops what is carried: on a slot it goes there; anywhere else nothing changes.</summary>
        public void EndCarry(Vector2? screenPosition)
        {
            if (_carrying == null)
            {
                return;
            }
            string id = _carrying;
            _carrying = null;
            _carryGhost.gameObject.SetActive(false);
            if (screenPosition.HasValue && _barSlots != null)
            {
                for (int i = 0; i < _barSlots.Length; i++)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(_barSlots[i].Root, screenPosition.Value, null))
                    {
                        Skills.PutOnBar(i, id);
                        break;
                    }
                }
            }
            _dirty = true;
            Refresh();
        }

        /// <summary>Screen pixels (an event's delta) to design pixels.</summary>
        public Vector2 ScreenToDesign(Vector2 screenDelta)
        {
            float scale = _canvas != null && _canvas.scaleFactor > 0.0f ? _canvas.scaleFactor : 1.0f;
            return screenDelta / scale;
        }

        // --- each frame ---

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // Escape closes it, and so does C, the key that leads to it.
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.cKey.wasPressedThisFrame)
                {
                    Close();
                    return;
                }
                if (SelectedGoesOnBar)
                {
                    for (int slot = 0; slot < Hotbar.SlotCount; slot++)
                    {
                        if (keyboard[PlayerSkills.KeyFor(slot)].wasPressedThisFrame)
                        {
                            PutSelectedOnBar(slot);
                        }
                    }
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float raw = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(raw) > 0.01f && RectTransformUtility.RectangleContainsScreenPoint(_viewport, mouse.position.ReadValue(), null))
                {
                    // One notch: 120 on some set-ups, 1 on others.
                    float notches = Mathf.Clamp(Mathf.Abs(raw) >= 20.0f ? raw / 120.0f : raw, -3.0f, 3.0f);
                    if (keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed))
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, mouse.position.ReadValue(), null, out Vector2 local);
                        ZoomBy(Mathf.Pow(ZoomPerNotch, notches), local - _viewport.rect.center);
                    }
                    else
                    {
                        Slide(-notches);
                    }
                }
            }

            TickGamepad(Gamepad.current, Time.unscaledDeltaTime);

            if (_resetArmedUntil > 0.0f && Time.unscaledTime > _resetArmedUntil)
            {
                _resetArmedUntil = -1.0f;
                _resetLabel.text = "Reset";
            }
            Refresh();
        }

        private void TickGamepad(Gamepad pad, float deltaTime)
        {
            if (pad == null)
            {
                return;
            }
            Vector2 stick = pad.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.04f)
            {
                Scroll += stick.x * StickPanSpeed * deltaTime / Zoom;
                PanY += stick.y * StickPanSpeed * deltaTime / Zoom;
                ApplyView();
            }
            float zoomStick = pad.rightStick.ReadValue().y;
            if (Mathf.Abs(zoomStick) > 0.2f)
            {
                ZoomBy(Mathf.Pow(2.0f, zoomStick * deltaTime * 2.0f), Vector2.zero);
            }
            if (pad.rightShoulder.wasPressedThisFrame) JumpTree(1);
            if (pad.leftShoulder.wasPressedThisFrame) JumpTree(-1);
            if (pad.buttonSouth.wasPressedThisFrame)
            {
                string near = StarNear(Vector2.zero, StickPickRadius);
                if (near != null && near == Selected && Skills != null && Skills.WhyNot(near) == SkillBook.Refusal.None)
                {
                    LearnSelected();
                }
                else
                {
                    Select(near);
                }
            }
            if (pad.buttonNorth.wasPressedThisFrame && SelectedGoesOnBar && Skills != null)
            {
                int now = Skills.Bar.SlotOf(Selected);
                PutSelectedOnBar((now + 1) % Hotbar.SlotCount);
            }
            if (pad.buttonEast.wasPressedThisFrame)
            {
                Close();
            }
        }

        /// <summary>Moves and scales the map to the view: the two copies of the row, the zoom, the names.</summary>
        public void ApplyView()
        {
            if (_content == null)
            {
                return;
            }
            Zoom = Mathf.Clamp(Zoom, MinZoom, MaxZoom);
            PanY = Mathf.Clamp(PanY, -PanLimitY, PanLimitY);
            Scroll = Layout.Wrap(Scroll);
            _content.localScale = new Vector3(Zoom, Zoom, 1.0f);
            _content.anchoredPosition = new Vector2(0.0f, -PanY * Zoom);
            Layout.CopyOffsets(Scroll, out float first, out float second);
            _copies[0].anchoredPosition = new Vector2(first, 0.0f);
            _copies[1].anchoredPosition = new Vector2(second, 0.0f);

            bool labels = Zoom >= LabelsHideBelowZoom;
            foreach (StarGraphics star in _stars)
            {
                if (star.Label != null && star.Label.enabled != labels)
                {
                    star.Label.enabled = labels;
                }
            }
            float nameScale = Mathf.Max(1.0f, 0.8f / Zoom);
            foreach (Text name in _treeNames)
            {
                name.rectTransform.localScale = new Vector3(nameScale, nameScale, 1.0f);
            }
        }

        // --- what the stars and the card show ---

        private StarState StateOf(SkillNode node)
        {
            if (node.IsPlaceholder || !SkillCatalog.FindTree(node.TreeId).InGame)
            {
                return StarState.Empty;
            }
            if (Skills != null && Skills.Knows(node.Id))
            {
                return StarState.Learnt;
            }
            return Skills != null && Skills.Book.PrerequisitesMet(node) ? StarState.Ready : StarState.Locked;
        }

        /// <summary>Brings every star, line, slot and the card up to date when anything has changed.</summary>
        public void Refresh()
        {
            if (_root == null)
            {
                return;
            }
            // Whether a sword is in hand changes without telling anyone: the bar's dimming is checked every frame.
            RefreshBar();
            if (!_dirty)
            {
                return;
            }
            _dirty = false;

            foreach (StarGraphics star in _stars)
            {
                PaintStar(star, StateOf(star.Node), star.Node.Id == Selected);
            }
            foreach (LineGraphics line in _lines)
            {
                bool lit = Skills != null && Skills.Knows(line.From) && Skills.Knows(line.To);
                foreach (Image piece in line.Pieces)
                {
                    piece.color = lit ? LineLearnt : LineOther;
                }
            }
            int points = Skills != null ? Skills.Points : 0;
            _points.text = points == 0 ? "No skill points" : points == 1 ? "1 skill point" : points + " skill points";
            RefreshCard();
        }

        private void RefreshBar()
        {
            if (_barSlots == null)
            {
                return;
            }
            for (int i = 0; i < Hotbar.SlotCount; i++)
            {
                string id = Skills != null ? Skills.Bar[i] : null;
                HotbarView.Show(_barSlots[i], id, id != null && Skills.CanUseNow(id));
            }
        }

        private void PaintStar(StarGraphics star, StarState state, bool selected)
        {
            star.Selection.enabled = selected;
            switch (state)
            {
                case StarState.Learnt:
                    star.Face.color = LearntFace;
                    star.Rim.color = Gold;
                    star.Icon.color = LearntIcon;
                    break;
                case StarState.Ready:
                    star.Face.color = DarkFace;
                    star.Rim.color = Gold;
                    star.Icon.color = Gold;
                    break;
                case StarState.Locked:
                    star.Face.color = DarkFace;
                    star.Rim.color = LockedRim;
                    star.Icon.color = LockedIcon;
                    break;
                default:
                    star.Face.color = DarkFace;
                    star.Rim.color = LockedRim;
                    break;
            }
        }

        private void RefreshCard()
        {
            SkillNode node = SkillCatalog.Find(Selected);
            _card.SetActive(node != null);
            if (node == null)
            {
                return;
            }
            SkillTree tree = SkillCatalog.FindTree(node.TreeId);
            _learn.gameObject.SetActive(false);
            if (!tree.InGame)
            {
                _cardTitle.text = tree.Name;
                _cardSubtitle.text = tree.IsHybrid ? "A hybrid tree" : "A skill tree";
                _cardBody.text = tree.IsHybrid
                    ? $"It opens once you have mastered both {SkillCatalog.FindTree(tree.Parents[0]).Name} and {SkillCatalog.FindTree(tree.Parents[1]).Name}. Not in the game yet."
                    : "Not in the game yet. Trees like this are found out in the world.";
                return;
            }
            if (node.IsPlaceholder)
            {
                _cardTitle.text = "An empty star";
                _cardSubtitle.text = tree.Name;
                _cardBody.text = "Not designed yet: a skill to come.";
                return;
            }

            _cardTitle.text = node.Name;
            _cardSubtitle.text = tree.Name + "  ·  " + KindName(node);
            var body = new StringBuilder(node.Description);
            body.Append("\n\n");
            SkillBook.Refusal refusal = Skills != null ? Skills.WhyNot(node.Id) : SkillBook.Refusal.Unknown;
            switch (refusal)
            {
                case SkillBook.Refusal.AlreadyLearnt:
                    if (node.KnownFromStart)
                    {
                        body.Append("Everyone has it.");
                    }
                    else if (!node.GoesOnBar)
                    {
                        body.Append("Learnt. It has its own controls.");
                    }
                    else
                    {
                        int slot = Skills.Bar.SlotOf(node.Id);
                        body.Append(slot >= 0
                            ? $"Learnt. On key {Hotbar.KeyLabel(slot)}."
                            : "Learnt. Drag it onto the bar, or press a key from 1 to 0.");
                    }
                    break;
                case SkillBook.Refusal.None:
                    body.Append("Ready to learn for 1 skill point.");
                    _learn.gameObject.SetActive(true);
                    break;
                case SkillBook.Refusal.NoPoints:
                    body.Append("Ready to learn, but you have no skill points. Every second level brings one.");
                    break;
                case SkillBook.Refusal.NeedsPrerequisite:
                    body.Append("Learn ").Append(MissingNames(node)).Append(" first.");
                    break;
            }
            _cardBody.text = body.ToString();
        }

        private string MissingNames(SkillNode node)
        {
            var names = new List<string>();
            foreach (string required in node.Requires)
            {
                if (Skills == null || !Skills.Knows(required))
                {
                    names.Add(SkillCatalog.Find(required).Name);
                }
            }
            return string.Join(" and ", names);
        }

        private static string KindName(SkillNode node)
        {
            switch (node.Kind)
            {
                case SkillKind.Movement: return "Movement";
                case SkillKind.Spell: return "Spell";
                case SkillKind.Ability: return "Sword technique";
                default: return "Skill";
            }
        }

        // --- building it ---

        private void BuildInterface()
        {
            _root = new GameObject("SkillMenuCanvas", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _root.AddComponent<GraphicRaycaster>();
            var rootRect = (RectTransform)_root.transform;

            // The sky, and the view the map moves in.
            Image sky = Stretch(NewChild("Sky", rootRect)).gameObject.AddComponent<Image>();
            sky.color = Sky;
            sky.raycastTarget = true;
            sky.gameObject.AddComponent<SkillMapDrag>().Menu = this;
            _viewport = (RectTransform)sky.transform;
            _viewport.gameObject.AddComponent<RectMask2D>();

            _content = NewChild("Map", _viewport);
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.pivot = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = Vector2.zero;
            for (int i = 0; i < _copies.Length; i++)
            {
                _copies[i] = NewChild(i == 0 ? "Row" : "RowAgain", _content);
                _copies[i].anchorMin = _copies[i].anchorMax = new Vector2(0.5f, 0.5f);
                _copies[i].pivot = Vector2.zero;
                _copies[i].sizeDelta = Vector2.zero;
                BuildRow(_copies[i]);
            }

            BuildTopBar(rootRect);
            BuildCard(rootRect);
            BuildBar(rootRect);

            _carryGhost = NewChild("Carried", rootRect).gameObject.AddComponent<Image>();
            _carryGhost.rectTransform.anchorMin = _carryGhost.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _carryGhost.rectTransform.sizeDelta = new Vector2(34.0f, 34.0f);
            _carryGhost.color = Gold;
            _carryGhost.raycastTarget = false;
            _carryGhost.gameObject.SetActive(false);
        }

        /// <summary>One copy of the whole row: the sky's little stars, then each tree's lines, stars and name.</summary>
        private void BuildRow(RectTransform row)
        {
            // The background stars, the same in both copies (a fixed seed) so the seam never shows.
            RectTransform dots = Origin(NewChild("Stars", row));
            var random = new System.Random(5);
            int count = Mathf.RoundToInt(Layout.RowWidth / 9.0f);
            for (int i = 0; i < count; i++)
            {
                float x = (float)random.NextDouble() * Layout.RowWidth;
                float y = (float)(random.NextDouble() * 900.0 - 450.0);
                float size = 1.5f + (float)random.NextDouble() * 2.2f;
                Image dot = Place(NewChild("Dot", dots), new Vector2(x, y), new Vector2(size, size)).gameObject.AddComponent<Image>();
                dot.sprite = HudArt.Disc;
                dot.color = new Color(StarDot.r, StarDot.g, StarDot.b, 0.12f + (float)random.NextDouble() * 0.45f);
                dot.raycastTarget = false;
            }

            foreach (SkillTree tree in SkillCatalog.Trees)
            {
                RectTransform group = Origin(NewChild(tree.Name, row));
                if (!tree.InGame)
                {
                    var faded = group.gameObject.AddComponent<CanvasGroup>();
                    faded.alpha = 0.42f;
                }
                RectTransform lines = Origin(NewChild("Lines", group));
                foreach (SkillNode node in SkillCatalog.InTree(tree.Id))
                {
                    foreach (string required in node.Requires)
                    {
                        SkillNode from = SkillCatalog.Find(required);
                        bool acrossTrees = from.TreeId != node.TreeId;
                        _lines.Add(MakeLine(lines, from, node, acrossTrees));
                    }
                }
                foreach (SkillNode node in SkillCatalog.InTree(tree.Id))
                {
                    _stars.Add(MakeStar(group, node));
                }

                // The tree's name under its base (a hybrid's under its star, high in the gap).
                float nameY = tree.IsHybrid
                    ? BaseY + MinStarY(tree.Id) - 46.0f
                    : BaseY - 74.0f;
                Text name = MakeText(group, tree.Name, 21, Gold, TextAnchor.MiddleCenter);
                name.fontStyle = FontStyle.Bold;
                Place(name.rectTransform, new Vector2(Layout.TreeCentre(tree.Id), nameY), new Vector2(260.0f, 30.0f));
                _treeNames.Add(name);
            }
        }

        private static float MinStarY(string treeId)
        {
            float lowest = float.MaxValue;
            foreach (SkillNode node in SkillCatalog.InTree(treeId))
            {
                lowest = Mathf.Min(lowest, node.Position.y);
            }
            return lowest;
        }

        private StarGraphics MakeStar(RectTransform parent, SkillNode node)
        {
            bool empty = node.IsPlaceholder;
            float size = empty ? EmptyStarSize : StarSize;
            Vector2 at = Layout.StarPosition(node) + new Vector2(0.0f, BaseY);
            var star = new StarGraphics { Node = node };
            star.Root = Place(NewChild(node.Id, parent), at, new Vector2(size, size));

            star.Selection = Centred(star.Root, "Chosen", HudArt.ThinRing, Color.white, size * 1.38f);
            star.Face = Centred(star.Root, "Face", HudArt.Disc, DarkFace, size);
            star.Face.raycastTarget = true;
            star.Rim = Centred(star.Root, "Rim", HudArt.ThinRing, LockedRim, size);
            star.Icon = Centred(star.Root, "Icon", SkillIcons.For(node.Id), LockedIcon, size * 0.56f);
            star.Icon.enabled = star.Icon.sprite != null;
            if (empty)
            {
                star.Mark = MakeText(star.Root, "?", 15, LockedIcon, TextAnchor.MiddleCenter);
                Place(star.Mark.rectTransform, Vector2.zero, new Vector2(size, size));
                star.Mark.rectTransform.anchorMin = star.Mark.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                star.Mark.rectTransform.anchoredPosition = Vector2.zero;
            }
            else
            {
                star.Label = MakeText(star.Root, node.Name, 13, LabelInk, TextAnchor.UpperCenter);
                RectTransform label = star.Label.rectTransform;
                label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.0f);
                label.pivot = new Vector2(0.5f, 1.0f);
                // Below the white ring a chosen star wears.
                label.anchoredPosition = new Vector2(0.0f, -size * 0.2f - 2.0f);
                label.sizeDelta = new Vector2(140.0f, 18.0f);
                var shadow = star.Label.gameObject.AddComponent<Outline>();
                shadow.effectColor = new Color(0.0f, 0.0f, 0.0f, 0.75f);
            }
            star.Selection.enabled = false;

            var handle = star.Face.gameObject.AddComponent<SkillStarHandle>();
            handle.Menu = this;
            handle.SkillId = node.Id;
            return star;
        }

        /// <summary>A line from one star to the next, solid within a tree, dashed across to a hybrid.</summary>
        private LineGraphics MakeLine(RectTransform parent, SkillNode from, SkillNode to, bool dashed)
        {
            var line = new LineGraphics { From = from.Id, To = to.Id };
            Vector2 a = Layout.StarPosition(from) + new Vector2(0.0f, BaseY);
            Vector2 b = Layout.StarPosition(to) + new Vector2(0.0f, BaseY);
            Vector2 along = b - a;
            float length = along.magnitude;
            float angle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
            const float dash = 10.0f, gap = 8.0f;
            float step = dashed ? dash + gap : length;
            for (float start = 0.0f; start < length - 0.5f; start += step)
            {
                float piece = dashed ? Mathf.Min(dash, length - start) : length;
                RectTransform rect = NewChild("Line", parent);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.0f, 0.5f);
                rect.anchoredPosition = a + along.normalized * start;
                rect.sizeDelta = new Vector2(piece, LineWidth);
                rect.localRotation = Quaternion.Euler(0.0f, 0.0f, angle);
                Image image = rect.gameObject.AddComponent<Image>();
                image.color = LineOther;
                image.raycastTarget = false;
                line.Pieces.Add(image);
            }
            return line;
        }

        private void BuildTopBar(RectTransform root)
        {
            Text title = MakeText(root, "SKILLS", 24, Gold, TextAnchor.UpperLeft);
            title.fontStyle = FontStyle.Bold;
            TopLeft(title.rectTransform, new Vector2(24.0f, -14.0f), new Vector2(140.0f, 32.0f));

            _points = MakeText(root, string.Empty, 15, LabelInk, TextAnchor.UpperLeft);
            TopLeft(_points.rectTransform, new Vector2(136.0f, -22.0f), new Vector2(200.0f, 22.0f));

            Text hint = MakeText(root, "Drag or scroll to move  ·  Ctrl + scroll to zoom  ·  Click a star  ·  Drag a learnt spell onto the bar  ·  " +
                "Right-click a slot to empty it", 12, HintInk, TextAnchor.UpperLeft);
            TopLeft(hint.rectTransform, new Vector2(24.0f, -50.0f), new Vector2(820.0f, 18.0f));

            float x = -16.0f;
            x = TopRightButton(root, "Close", 84.0f, x, Close);
            Button reset = null;
            x = TopRightButton(root, "Reset", 84.0f, x, () => OnResetClicked(), b => reset = b);
            _resetLabel = reset.GetComponentInChildren<Text>();
            x = TopRightButton(root, "Show all", 96.0f, x, ShowAll);
            x = TopRightButton(root, "+", 36.0f, x, () => ZoomBy(1.3f, Vector2.zero));
            TopRightButton(root, "-", 36.0f, x, () => ZoomBy(1.0f / 1.3f, Vector2.zero));
        }

        private void OnResetClicked()
        {
            if (Skills == null)
            {
                return;
            }
            if (_resetArmedUntil < 0.0f)
            {
                _resetArmedUntil = Time.unscaledTime + ResetConfirmSeconds;
                _resetLabel.text = "Sure?";
                return;
            }
            _resetArmedUntil = -1.0f;
            _resetLabel.text = "Reset";
            Skills.ResetAll();
            _dirty = true;
            Refresh();
        }

        private float TopRightButton(RectTransform root, string label, float width, float right, UnityEngine.Events.UnityAction onClick,
            System.Action<Button> made = null)
        {
            RectTransform rect = NewChild(label, root);
            rect.anchorMin = rect.anchorMax = new Vector2(1.0f, 1.0f);
            rect.pivot = new Vector2(1.0f, 1.0f);
            rect.anchoredPosition = new Vector2(right, -14.0f);
            rect.sizeDelta = new Vector2(width, 30.0f);
            Button button = MakeButton(rect, label, onClick);
            made?.Invoke(button);
            return right - width - 8.0f;
        }

        private Button MakeButton(RectTransform rect, string label, UnityEngine.Events.UnityAction onClick)
        {
            Image face = rect.gameObject.AddComponent<Image>();
            face.sprite = HudArt.Plate;
            face.type = Image.Type.Sliced;
            face.color = ButtonFace;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.onClick.AddListener(onClick);
            Text text = MakeText(rect, label, 14, LabelInk, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        private void BuildCard(RectTransform root)
        {
            RectTransform card = NewChild("Card", root);
            card.anchorMin = card.anchorMax = new Vector2(1.0f, 0.0f);
            card.pivot = new Vector2(1.0f, 0.0f);
            card.anchoredPosition = new Vector2(-16.0f, 16.0f);
            card.sizeDelta = new Vector2(340.0f, 168.0f);
            // A solid back under the plate's gold edge: the plate alone lets the map show through the text.
            Image back = card.gameObject.AddComponent<Image>();
            back.color = new Color(0.045f, 0.05f, 0.075f, 0.98f);
            Image plate = Stretch(NewChild("Plate", card)).gameObject.AddComponent<Image>();
            plate.sprite = HudArt.Plate;
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = false;
            _card = card.gameObject;

            _cardTitle = MakeText(card, string.Empty, 19, Gold, TextAnchor.UpperLeft);
            _cardTitle.fontStyle = FontStyle.Bold;
            TopLeft(_cardTitle.rectTransform, new Vector2(14.0f, -10.0f), new Vector2(312.0f, 24.0f));
            _cardSubtitle = MakeText(card, string.Empty, 12, HintInk, TextAnchor.UpperLeft);
            TopLeft(_cardSubtitle.rectTransform, new Vector2(14.0f, -34.0f), new Vector2(312.0f, 16.0f));
            _cardBody = MakeText(card, string.Empty, 13, LabelInk, TextAnchor.UpperLeft);
            _cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cardBody.verticalOverflow = VerticalWrapMode.Truncate;
            TopLeft(_cardBody.rectTransform, new Vector2(14.0f, -54.0f), new Vector2(312.0f, 74.0f));

            RectTransform learn = NewChild("Learn", card);
            learn.anchorMin = learn.anchorMax = new Vector2(1.0f, 0.0f);
            learn.pivot = new Vector2(1.0f, 0.0f);
            learn.anchoredPosition = new Vector2(-12.0f, 10.0f);
            learn.sizeDelta = new Vector2(150.0f, 30.0f);
            _learn = MakeButton(learn, "Learn (1 point)", () => LearnSelected());
            ((Image)_learn.targetGraphic).color = new Color(0.55f, 0.42f, 0.14f, 1.0f);
            _card.SetActive(false);
        }

        private void BuildBar(RectTransform root)
        {
            RectTransform bar = NewChild("Bar", root);
            bar.anchorMin = bar.anchorMax = Vector2.zero;
            bar.pivot = Vector2.zero;
            bar.anchoredPosition = HotbarView.Corner;
            float size = HotbarView.SlotSize;
            bar.sizeDelta = new Vector2(Hotbar.SlotCount * size + (Hotbar.SlotCount - 1) * HotbarView.SlotGap, size);
            _barSlots = new HotbarView.SlotGraphics[Hotbar.SlotCount];
            for (int i = 0; i < Hotbar.SlotCount; i++)
            {
                _barSlots[i] = HotbarView.MakeSlot(bar, i, size, new Vector2(i * (size + HotbarView.SlotGap), 0.0f));
                _barSlots[i].Face.raycastTarget = true;
                var handle = _barSlots[i].Face.gameObject.AddComponent<SkillSlotHandle>();
                handle.Menu = this;
                handle.Slot = i;
            }
        }

        // --- small helpers ---

        private static RectTransform NewChild(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>A container with no size whose origin is its parent's: what is placed in it keeps the row's own coordinates.</summary>
        private static RectTransform Origin(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Puts a child's middle at a point of a row (its bottom-left origin).</summary>
        private static RectTransform Place(RectTransform rect, Vector2 at, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            return rect;
        }

        private static void TopLeft(RectTransform rect, Vector2 at, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.0f, 1.0f);
            rect.pivot = new Vector2(0.0f, 1.0f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
        }

        private static Image Centred(RectTransform parent, string name, Sprite sprite, Color colour, float size)
        {
            RectTransform rect = NewChild(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static Text MakeText(RectTransform parent, string content, int size, Color colour, TextAnchor alignment)
        {
            RectTransform rect = NewChild("Text", parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = content;
            return text;
        }

        /// <summary>Without one, nothing on the menu hears a click (the city scene has one; a bare test scene may not).</summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
        }
    }

    /// <summary>The sky behind the stars: dragging it moves the map; a click on empty sky lets go of the chosen star.</summary>
    public sealed class SkillMapDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerClickHandler
    {
        public SkillMenuView Menu;

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData) => Menu.Pan(Menu.ScreenToDesign(eventData.delta));

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Menu.Select(null);
            }
        }
    }

    /// <summary>A star: a click chooses it; a drag carries a learnt spell to the bar, or moves the map when there is nothing to carry.</summary>
    public sealed class SkillStarHandle : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public SkillMenuView Menu;
        public string SkillId;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Menu.Select(SkillId);
            }
        }

        public void OnBeginDrag(PointerEventData eventData) => Menu.BeginCarry(SkillId, eventData.position);

        public void OnDrag(PointerEventData eventData)
        {
            if (Menu.IsCarrying)
            {
                Menu.MoveCarry(eventData.position);
            }
            else
            {
                Menu.Pan(Menu.ScreenToDesign(eventData.delta));
            }
        }

        public void OnEndDrag(PointerEventData eventData) => Menu.EndCarry(eventData.position);
    }

    /// <summary>A slot of the menu's bar: a click puts the chosen spell there (or chooses what is there); a right-click empties it.</summary>
    public sealed class SkillSlotHandle : MonoBehaviour, IPointerClickHandler
    {
        public SkillMenuView Menu;
        public int Slot;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                Menu.ClearSlot(Slot);
            }
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                Menu.ClickSlot(Slot);
            }
        }
    }
}
