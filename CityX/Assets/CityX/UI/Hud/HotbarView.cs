using CityX.Character;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// THE BAR ON THE HUD (owner, 2026-10-14: round icons rather than World of Warcraft's squares, "just have the hotbar at the bottom left for
    /// now"): ten round slots in a row in the bottom-left corner, keys 1 to 9 then 0, each a gold ring round a dark face with the skill's icon.
    /// An empty slot is a dim ring; a skill that cannot be used right now (a sword technique with no sword in hand) is greyed; a slot whose key is
    /// pressed flashes. It shows the bar of the body this machine controls (PlayerSkills.Local), and steps aside while the chat line is open, which
    /// sits in the same corner.
    ///
    /// Built when the HUD wakes (PlayerHud.Awake adds it, as it does GunReticleView), from the shared art (HudArt) and the drawn icons
    /// (SkillIcons): no prefab to rebuild, no image files.
    /// </summary>
    public sealed class HotbarView : MonoBehaviour
    {
        /// <summary>Design pixels on the 1152 x 648 canvas.</summary>
        public const float SlotSize = 40.0f;
        public const float SlotGap = 6.0f;
        public static readonly Vector2 Corner = new Vector2(16.0f, 16.0f);

        /// <summary>The face's share of the slot (inside the gold ring) and the icon's.</summary>
        public const float FaceFraction = 0.84f;
        public const float IconFraction = 0.6f;

        public const float FlashSeconds = 0.18f;

        public static readonly Color FaceColour = new Color(0.06f, 0.055f, 0.05f, 0.92f);
        public static readonly Color IconColour = new Color(1.0f, 0.9f, 0.62f, 1.0f);
        public static readonly Color UnusableIconColour = new Color(0.45f, 0.43f, 0.4f, 0.9f);
        public static readonly Color EmptyRimColour = new Color(1.0f, 1.0f, 1.0f, 0.4f);

        /// <summary>The bar's own object, a child of the HUD canvas.</summary>
        public RectTransform Bar { get; private set; }

        public SlotGraphics[] Slots { get; private set; }

        /// <summary>One slot's pieces.</summary>
        public sealed class SlotGraphics
        {
            public RectTransform Root;
            public Image Rim;
            public Image Face;
            public Image Icon;

            /// <summary>The skill's first letter, shown in place of an icon nobody has drawn yet (SkillIcons.LetterFor).</summary>
            public Text Letter;

            public Text Key;
            public string SkillId;
            public float Flash;
        }

        private PlayerSkills _skills;
        private PlayerHud _hud;
        private PlayerChat _chat;
        private float _nextChatSearch;
        private bool _dirty = true;
        private CanvasGroup _group;
        private MenuHudFade _fade;

        private void Awake()
        {
            _hud = GetComponent<PlayerHud>();
            Build(transform);
        }

        private void OnDestroy()
        {
            Watch(null);
        }

        /// <summary>The skills this bar shows (the menu and tests point it; otherwise it follows PlayerSkills.Local).</summary>
        public PlayerSkills Skills
        {
            get => _skills;
            set => Watch(value);
        }

        private void Watch(PlayerSkills skills)
        {
            if (_skills == skills)
            {
                return;
            }
            if (_skills != null)
            {
                _skills.Changed -= MarkDirty;
            }
            _skills = skills;
            if (_skills != null)
            {
                _skills.Changed += MarkDirty;
            }
            _dirty = true;
        }

        private void MarkDirty() => _dirty = true;

        /// <summary>Builds the ten slots in the bottom-left corner of <paramref name="canvasRoot"/>.</summary>
        public void Build(Transform canvasRoot)
        {
            if (Bar != null)
            {
                return;
            }
            var bar = new GameObject("Hotbar", typeof(RectTransform), typeof(CanvasGroup));
            Bar = (RectTransform)bar.transform;
            Bar.SetParent(canvasRoot, false);
            // Behind everything else on the HUD (the death screen covers it).
            Bar.SetAsFirstSibling();
            Bar.anchorMin = Vector2.zero;
            Bar.anchorMax = Vector2.zero;
            Bar.pivot = Vector2.zero;
            Bar.anchoredPosition = Corner;
            Bar.sizeDelta = new Vector2(Hotbar.SlotCount * SlotSize + (Hotbar.SlotCount - 1) * SlotGap, SlotSize);
            _group = bar.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Slots = new SlotGraphics[Hotbar.SlotCount];
            for (int i = 0; i < Hotbar.SlotCount; i++)
            {
                Slots[i] = MakeSlot(Bar, i, SlotSize, new Vector2(i * (SlotSize + SlotGap), 0.0f));
            }
        }

        /// <summary>One round slot: a gold ring round a dark face, the icon, and the key in the lower right. Shared with the skill menu's bar.</summary>
        public static SlotGraphics MakeSlot(Transform parent, int index, float size, Vector2 bottomLeft)
        {
            var slot = new SlotGraphics();
            var root = new GameObject("Slot" + Hotbar.KeyLabel(index), typeof(RectTransform));
            slot.Root = (RectTransform)root.transform;
            slot.Root.SetParent(parent, false);
            slot.Root.anchorMin = Vector2.zero;
            slot.Root.anchorMax = Vector2.zero;
            slot.Root.pivot = Vector2.zero;
            slot.Root.anchoredPosition = bottomLeft;
            slot.Root.sizeDelta = new Vector2(size, size);

            slot.Face = MakeImage("Face", slot.Root, HudArt.Disc, FaceColour, size * FaceFraction);
            slot.Icon = MakeImage("Icon", slot.Root, null, IconColour, size * IconFraction);
            slot.Letter = MakeLetter(slot.Root, size * IconFraction);
            slot.Rim = MakeImage("Rim", slot.Root, HudArt.Ring, Color.white, size);

            var keyObject = new GameObject("Key", typeof(RectTransform));
            var keyRect = (RectTransform)keyObject.transform;
            keyRect.SetParent(slot.Root, false);
            keyRect.anchorMin = new Vector2(1.0f, 0.0f);
            keyRect.anchorMax = new Vector2(1.0f, 0.0f);
            keyRect.pivot = new Vector2(1.0f, 0.0f);
            keyRect.anchoredPosition = new Vector2(3.0f, -3.0f);
            keyRect.sizeDelta = new Vector2(16.0f, 16.0f);
            slot.Key = keyObject.AddComponent<Text>();
            slot.Key.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slot.Key.fontSize = 12;
            slot.Key.fontStyle = FontStyle.Bold;
            slot.Key.alignment = TextAnchor.LowerRight;
            slot.Key.color = Color.white;
            slot.Key.raycastTarget = false;
            slot.Key.text = Hotbar.KeyLabel(index);
            var outline = keyObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.0f, 0.0f, 0.0f, 0.9f);
            outline.effectDistance = new Vector2(1.0f, -1.0f);
            return slot;
        }

        private static Image MakeImage(string name, RectTransform parent, Sprite sprite, Color colour, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        /// <summary>
        /// A skill's first letter in place of its icon, <paramref name="size"/> design pixels square: bold, centred, hidden until a skill with no
        /// drawn icon is shown. Shared with the skill menu.
        /// </summary>
        public static Text MakeLetter(RectTransform parent, float size)
        {
            var go = new GameObject("Letter", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * 0.8f));
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.enabled = false;
            return text;
        }

        /// <summary>Shows a skill (or nothing) in a slot; greyed when it cannot be used now. A skill with no drawn icon shows its first letter.</summary>
        public static void Show(SlotGraphics slot, string skillId, bool usable)
        {
            slot.SkillId = skillId;
            Sprite icon = SkillIcons.For(skillId);
            slot.Icon.sprite = icon;
            slot.Icon.enabled = icon != null;
            slot.Icon.color = usable ? IconColour : UnusableIconColour;
            if (slot.Letter != null)
            {
                string letter = icon == null ? SkillIcons.LetterFor(skillId) : null;
                slot.Letter.text = letter ?? string.Empty;
                slot.Letter.enabled = letter != null;
                slot.Letter.color = slot.Icon.color;
            }
            slot.Rim.color = skillId != null ? Color.white : EmptyRimColour;
        }

        private void Update()
        {
            PlayerSkills wanted = PlayerSkills.Local;
            if (wanted == null && _hud != null && _hud.Target != null)
            {
                wanted = _hud.Target.GetComponent<PlayerSkills>();
            }
            if (wanted != null && wanted != _skills)
            {
                Watch(wanted);
            }
            if (Slots == null)
            {
                return;
            }

            // The chat line lies across the same corner while it is open (looked for once a second until found, not every frame).
            if (_chat == null && Time.unscaledTime >= _nextChatSearch)
            {
                _nextChatSearch = Time.unscaledTime + 1.0f;
                _chat = Object.FindFirstObjectByType<PlayerChat>();
            }
            if (_group != null)
            {
                // Gone while the chat line lies across this corner; faded with the portrait while the C menu is open (MenuHudFade).
                if (_fade == null)
                {
                    _fade = GetComponent<MenuHudFade>();
                }
                _group.alpha = _chat != null && _chat.IsOpen ? 0.0f : (_fade != null ? _fade.Visibility : 1.0f);
            }

            Refresh();
            Flash(Keyboard.current, Time.unscaledDeltaTime);
        }

        /// <summary>Puts the bar's skills in the slots; every frame, since whether a sword is in hand changes without telling anyone.</summary>
        public void Refresh()
        {
            if (Slots == null)
            {
                return;
            }
            for (int i = 0; i < Hotbar.SlotCount; i++)
            {
                string id = _skills != null ? _skills.Bar[i] : null;
                bool usable = id != null && _skills.CanUseNow(id);
                if (_dirty || Slots[i].SkillId != id || (Slots[i].Icon.color == IconColour) != usable)
                {
                    Show(Slots[i], id, usable);
                }
            }
            _dirty = false;
        }

        /// <summary>A slot whose key goes down flashes: the icon swells and settles back over <see cref="FlashSeconds"/>. Not while a menu has the keys.</summary>
        private void Flash(Keyboard keyboard, float deltaTime)
        {
            bool keysAreThePlayers = _skills != null && _skills.GetComponent<PlayerInputReader>() is PlayerInputReader reader && reader.TakesKeys;
            for (int i = 0; i < Hotbar.SlotCount; i++)
            {
                SlotGraphics slot = Slots[i];
                if (keyboard != null && keysAreThePlayers && slot.SkillId != null && keyboard[PlayerSkills.KeyFor(i)].wasPressedThisFrame)
                {
                    slot.Flash = FlashSeconds;
                }
                slot.Flash = Mathf.Max(0.0f, slot.Flash - deltaTime);
                float swell = 1.0f + 0.2f * (slot.Flash / FlashSeconds);
                slot.Icon.rectTransform.localScale = new Vector3(swell, swell, 1.0f);
                if (slot.Letter != null)
                {
                    slot.Letter.rectTransform.localScale = slot.Icon.rectTransform.localScale;
                }
            }
        }
    }
}
