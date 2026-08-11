using CityX.Character;
using CityX.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the HUD prefab in code.
    ///
    /// Same reason the rig and the massing scene are built in code: a prefab
    /// full of nested RectTransforms is an opaque binary asset. A wrong anchor
    /// in one is invisible in a diff and can only be fixed by clicking through
    /// an inspector, whereas the numbers below can be read, reviewed and
    /// re-run. The layout figures are carried straight over from ui/hud.tscn so
    /// the two builds look like each other.
    ///
    /// LEGACY UnityEngine.UI.Text RATHER THAN TextMeshPro. TMP needs its
    /// essential resources imported through a dialog before any of its text
    /// renders, which is a manual step that would break a headless rebuild and
    /// leave blank labels with no error. This has none.
    /// </summary>
    public static class PlayerHudBuilder
    {
        private const string PrefabPath = "Assets/CityX/UI/PlayerHud.prefab";

        /// <summary>
        /// Godot's own default viewport, which is what hud.tscn was authored
        /// against -- every offset below is one of its numbers, and a 300px bar
        /// is a quarter of this width.
        ///
        /// NOT 1920x1080. That was the first guess, and because the scaler
        /// treats this as the design size it silently rendered the whole HUD at
        /// 60% of its intended proportion: correct pixel counts, wrong share of
        /// the screen. Only visible by rendering it.
        /// </summary>
        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);

        // --- palette, from hud.tscn's StyleBoxFlats ---

        private static readonly Color Gold = new Color(0.75f, 0.6f, 0.2f, 1.0f);
        private static readonly Color BarBackground = new Color(0.08f, 0.09f, 0.12f, 0.9f);
        private static readonly Color StaminaBlue = new Color(0.3f, 0.65f, 0.95f, 1.0f);
        private static readonly Color BadgeText = new Color(0.15f, 0.12f, 0.05f, 1.0f);
        private static readonly Color DeathDim = new Color(0.0f, 0.0f, 0.0f, 0.75f);
        private static readonly Color DeathRed = new Color(0.85f, 0.1f, 0.1f, 1.0f);

        private const float HealthBorder = 5.0f;
        private const float StaminaBorder = 4.0f;

        [MenuItem("CityX/Rebuild Player HUD")]
        public static GameObject Build()
        {
            var root = new GameObject("PlayerHud", typeof(RectTransform));
            try
            {
                Canvas canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // hud.tscn's own CanvasLayer layer number, kept so the HUD
                // keeps sorting above anything added later at a lower number.
                canvas.sortingOrder = 40;

                CanvasScaler scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = DesignResolution;
                // Halfway between matching width and matching height. Biasing
                // either way makes the HUD grow or shrink on ultrawide screens.
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                root.AddComponent<GraphicRaycaster>();

                PlayerHud hud = root.AddComponent<PlayerHud>();

                hud.Crosshair = BuildCrosshair(root.transform);
                BuildStatusFrame(root.transform, hud);
                BuildDeathScreen(root.transform, hud);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: HUD written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Puts a HUD into the open scene, pointed at <paramref name="target"/>,
        /// along with the EventSystem its buttons need.
        ///
        /// IN THE SCENE RATHER THAN ON THE PLAYER PREFAB, which is where the
        /// Godot build put it. A screen-space HUD living inside the player means
        /// a second HUD the moment a second player exists, drawn over the first
        /// and reporting somebody else's health. One per world, told explicitly
        /// whose vitals to show, has no such question to get wrong.
        /// </summary>
        public static GameObject AddToScene(PlayerVitals target)
        {
            GameObject prefab = Build();
            if (prefab == null)
            {
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "PlayerHud";
            instance.GetComponent<PlayerHud>().Target = target;

            EnsureEventSystem();
            return instance;
        }

        /// <summary>
        /// Without one of these, a Button is a coloured rectangle that cannot be
        /// clicked -- no error, no warning, it simply never fires.
        ///
        /// The module has to be the Input System one. This project sets
        /// activeInputHandler to the new system only, so the StandaloneInputModule
        /// that Unity's own menu adds by default would compile, sit there, and
        /// silently deliver nothing.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject BuildCrosshair(Transform parent)
        {
            var crosshair = new GameObject("Crosshair", typeof(RectTransform));
            RectTransform rect = Attach(crosshair, parent);
            Centre(rect, Vector2.zero, Vector2.zero);

            var white = new Color(1.0f, 1.0f, 1.0f, 0.85f);
            MakeRect("Horizontal", crosshair.transform, white, new Vector2(16.0f, 2.0f));
            MakeRect("Vertical", crosshair.transform, white, new Vector2(2.0f, 16.0f));
            return crosshair;
        }

        private static void BuildStatusFrame(Transform parent, PlayerHud hud)
        {
            var frame = new GameObject("StatusFrame", typeof(RectTransform));
            RectTransform rect = Attach(frame, parent);
            TopLeft(rect, new Vector2(20.0f, -20.0f), new Vector2(500.0f, 130.0f));

            // hud.tscn indents the bars by 115px to clear a 130px portrait, and
            // hangs the level badge off that portrait's bottom-left corner.
            // With no portrait ported, keeping those offsets leaves a badge
            // floating alone in an empty gap, which reads as a broken layout
            // rather than a missing feature.
            //
            // So the badge takes the left slot and the bars close up beside it.
            // When the portrait lands it reclaims this corner and these three
            // numbers move back.
            hud.LevelLabel = BuildLevelBadge(frame.transform, new Vector2(0.0f, -12.0f));

            hud.HealthFill = BuildBar(frame.transform, "HealthBar",
                new Vector2(56.0f, -10.0f), new Vector2(300.0f, 38.0f),
                HealthBorder, PlayerHud.HealthGreen, out Image healthFillImage);
            hud.HealthFillImage = healthFillImage;

            hud.StaminaFill = BuildBar(frame.transform, "StaminaBar",
                new Vector2(56.0f, -54.0f), new Vector2(300.0f, 20.0f),
                StaminaBorder, StaminaBlue, out _);
        }

        /// <summary>
        /// A bar is three nested rects: a gold border, a dark well inset inside
        /// it, and the fill inside that. Godot drew the border with a StyleBox;
        /// uGUI has no equivalent without a sprite, and nesting is both exact
        /// and free.
        /// </summary>
        private static RectTransform BuildBar(Transform parent, string name, Vector2 position,
            Vector2 size, float border, Color fillColour, out Image fillImage)
        {
            var borderObject = new GameObject(name, typeof(RectTransform));
            RectTransform borderRect = Attach(borderObject, parent);
            TopLeft(borderRect, position, size);
            borderObject.AddComponent<Image>().color = Gold;

            var well = new GameObject("Well", typeof(RectTransform));
            RectTransform wellRect = Attach(well, borderObject.transform);
            Stretch(wellRect, border);
            well.AddComponent<Image>().color = BarBackground;

            var fill = new GameObject("Fill", typeof(RectTransform));
            RectTransform fillRect = Attach(fill, well.transform);
            Stretch(fillRect, 0.0f);
            fillImage = fill.AddComponent<Image>();
            fillImage.color = fillColour;

            return fillRect;
        }

        private static Text BuildLevelBadge(Transform parent, Vector2 position)
        {
            var badge = new GameObject("LevelBadge", typeof(RectTransform));
            RectTransform badgeRect = Attach(badge, parent);
            TopLeft(badgeRect, position, new Vector2(44.0f, 44.0f));
            badge.AddComponent<Image>().color = Gold;

            var label = new GameObject("LevelLabel", typeof(RectTransform));
            RectTransform labelRect = Attach(label, badge.transform);
            Stretch(labelRect, 0.0f);

            Text text = label.AddComponent<Text>();
            text.font = BuiltinFont();
            text.fontSize = 20;
            text.color = BadgeText;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = "1";
            return text;
        }

        private static void BuildDeathScreen(Transform parent, PlayerHud hud)
        {
            var screen = new GameObject("DeathScreen", typeof(RectTransform));
            RectTransform screenRect = Attach(screen, parent);
            Stretch(screenRect, 0.0f);

            var dim = new GameObject("Dim", typeof(RectTransform));
            RectTransform dimRect = Attach(dim, screen.transform);
            Stretch(dimRect, 0.0f);
            // Raycast target left on deliberately: it swallows clicks aimed at
            // the world behind it while you are dead.
            dim.AddComponent<Image>().color = DeathDim;

            var died = new GameObject("YouDiedLabel", typeof(RectTransform));
            RectTransform diedRect = Attach(died, screen.transform);
            Centre(diedRect, new Vector2(0.0f, 55.0f), new Vector2(440.0f, 70.0f));
            Text diedText = died.AddComponent<Text>();
            diedText.font = BuiltinFont();
            diedText.fontSize = 56;
            diedText.color = DeathRed;
            diedText.alignment = TextAnchor.MiddleCenter;
            diedText.text = "YOU DIED";

            var button = new GameObject("RespawnButton", typeof(RectTransform));
            RectTransform buttonRect = Attach(button, screen.transform);
            Centre(buttonRect, new Vector2(0.0f, -30.0f), new Vector2(150.0f, 40.0f));
            Image buttonImage = button.AddComponent<Image>();
            buttonImage.color = new Color(0.22f, 0.22f, 0.24f, 1.0f);
            Button respawn = button.AddComponent<Button>();
            respawn.targetGraphic = buttonImage;

            var buttonLabel = new GameObject("Label", typeof(RectTransform));
            RectTransform buttonLabelRect = Attach(buttonLabel, button.transform);
            Stretch(buttonLabelRect, 0.0f);
            Text buttonText = buttonLabel.AddComponent<Text>();
            buttonText.font = BuiltinFont();
            buttonText.fontSize = 20;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.text = "Respawn";

            hud.DeathScreen = screen;
            hud.RespawnButton = respawn;

            // Starts hidden. PlayerHud turns it on when Died fires.
            screen.SetActive(false);
        }

        // --- RectTransform helpers. Anchor maths is the part of uGUI that is
        // --- easy to get subtly wrong, so it is written once here.

        private static RectTransform Attach(GameObject child, Transform parent)
        {
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Anchored to the parent's top-left corner, as Godot lays out.</summary>
        private static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.0f, 1.0f);
            rect.anchorMax = new Vector2(0.0f, 1.0f);
            rect.pivot = new Vector2(0.0f, 1.0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Centre(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Fills the parent, inset by <paramref name="inset"/> on every side.</summary>
        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void MakeRect(string name, Transform parent, Color colour, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = Attach(go, parent);
            Centre(rect, Vector2.zero, size);
            Image image = go.AddComponent<Image>();
            image.color = colour;
            // Nothing should ever click a crosshair, and every raycast target
            // costs work per pointer event.
            image.raycastTarget = false;
        }

        private static Font BuiltinFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
