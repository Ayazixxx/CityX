using CityX.Character;
using CityX.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        private static readonly Color DeathDim = new Color(0.0f, 0.0f, 0.0f, 0.75f);
        private static readonly Color DeathRed = new Color(0.85f, 0.1f, 0.1f, 1.0f);

        /// <summary>
        /// The Unity layer PortraitCamera's own cullingMask is restricted
        /// to -- Godot instead OR's a second layer BIT onto every body-part
        /// mesh (character.gd's own PORTRAIT_VISUAL_LAYER), so the same
        /// mesh renders for both the main camera and the portrait one at
        /// once. Unity's GameObject.layer is a single value, not a bitmask,
        /// so that specific trick doesn't carry over -- moving the head/skin
        /// renderers onto a layer of their own, while the main game camera's
        /// own cullingMask stays Everything, reaches the identical result:
        /// one real mesh, visible to both cameras, and nothing else in the
        /// world visible to the portrait one.
        /// </summary>
        public const string PortraitLayerName = "PlayerPortrait";

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
                hud.AmmoLabel = BuildAmmoLabel(root.transform);
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
            PlayerHud hud = instance.GetComponent<PlayerHud>();
            hud.Target = target;

            if (target != null)
            {
                hud.PortraitCamera = BuildPortraitCamera(target.transform, out RenderTexture texture);
                // WITHOUT THIS the camera renders the character into its own
                // private texture every frame and nothing on screen ever
                // shows it -- RawImage defaults to a null texture, which
                // draws as a plain white rect tinted by its own .color
                // (also white here), not the portrait. Confirmed exactly
                // that: a solid white circle where the character should be.
                if (hud.PortraitImage != null)
                {
                    hud.PortraitImage.texture = texture;
                }
            }

            UiKit.EnsureEventSystem();
            return instance;
        }

        /// <summary>
        /// The portrait's own camera and light, cullingMask-restricted to
        /// PortraitLayerName, plus moving the target's own body renderers
        /// onto that layer so this camera (and only this camera) can see
        /// them -- see PortraitLayerName's own doc comment for why Unity
        /// needs a real layer move here where Godot only ORs in a bit.
        ///
        /// A DEDICATED LIGHT, cullingMask-restricted the same way, rather
        /// than reading whatever DayNightCycle currently has the sun/moon
        /// set to -- hud.gd's own Environment override on just this camera
        /// exists so the portrait reads the same at noon and at midnight;
        /// letting it fall under the world's own lighting would make a
        /// portrait taken at 2am unrecognisably dark for no reason related
        /// to the character it is a portrait of.
        /// </summary>
        private static Camera BuildPortraitCamera(Transform target, out RenderTexture texture)
        {
            texture = null;
            int layer = LayerMask.NameToLayer(PortraitLayerName);
            if (layer < 0)
            {
                Debug.LogError($"CityX: no '{PortraitLayerName}' layer defined -- add it in Project Settings > Tags and Layers.");
                return null;
            }

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                renderer.gameObject.layer = layer;
            }

            var cameraObject = new GameObject("PortraitCamera");
            cameraObject.transform.SetParent(target, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = PlayerHud.PortraitBackgroundColour;
            camera.cullingMask = 1 << layer;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 5.0f;
            camera.fieldOfView = 35.0f;
            camera.allowMSAA = true;
            // A one-shot capture of a head silhouette against a flat colour --
            // exactly the shape (a curved edge on a plain background) MSAA
            // alone shows the most jaggies on, and it stays on screen in the
            // HUD corner for the entire session, so it is worth spending
            // SMAA on too rather than leaving it at whatever this camera's
            // antialiasing default happens to be.
            var portraitCameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            portraitCameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            portraitCameraData.antialiasingQuality = AntialiasingQuality.High;

            const int resolution = 256;
            texture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32)
            {
                name = "PortraitTexture",
                antiAliasing = 4,
            };
            camera.targetTexture = texture;

            var lightObject = new GameObject("PortraitLight");
            lightObject.transform.SetParent(cameraObject.transform, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.white;
            light.intensity = 3.0f;
            light.range = 3.0f;
            light.cullingMask = 1 << layer;
            light.shadows = LightShadows.None;

            return camera;
        }

        /// <summary>How wide the crosshair's dot is, in design pixels (about 8 screen pixels at 1080p).</summary>
        public const float CrosshairDotSize = 5.0f;

        /// <summary>The crosshair: a small white dot you can see through (owner, 2026-10-22: "change the crosshair to a white transparent dot").</summary>
        private static GameObject BuildCrosshair(Transform parent)
        {
            var crosshair = new GameObject("Crosshair", typeof(RectTransform));
            RectTransform rect = Attach(crosshair, parent);
            Centre(rect, Vector2.zero, Vector2.zero);

            Image dot = MakeRect("Dot", crosshair.transform, GunReticleView.NormalColour, new Vector2(CrosshairDotSize, CrosshairDotSize));
            dot.sprite = CircleSprite.Dot();
            return crosshair;
        }

        // --- THE UNIT FRAME, World of Warcraft style (owner, 2026-10-14, with two of WoW's frames to go by: "make it look like the world of
        // --- warcraft version ours looks like crap", and a mana bar "without it being too bunched up"). A round portrait in a gold ring with
        // --- the level on a small badge at its lower left; to its right a dark plate, its end tucked under the ring, holding the name and the
        // --- bars: health (with its percentage), mana (with its percentage, hidden until the player has mana), stamina (thin, no text). The
        // --- plate is a column that sizes itself to its rows, so without mana it closes up instead of leaving a gap. HudArt paints the ring,
        // --- the plate, the wells and the glossy fills at run time.

        /// <summary>The portrait's diameter, ring included, in design pixels (the canvas is 1152 x 648).</summary>
        private const float PortraitSize = 104.0f;

        /// <summary>Where the portrait's top-left corner sits from the screen's.</summary>
        private static readonly Vector2 PortraitCorner = new Vector2(16.0f, -14.0f);

        /// <summary>The plate's left end tucks this far under the ring.</summary>
        private const float PlateTuck = 22.0f;

        private const float PlateWidth = 236.0f;

        /// <summary>The plate's top is this far below the portrait's top.</summary>
        private const float PlateDrop = 16.0f;

        /// <summary>The rows' heights: the name, then the health, mana and stamina bars.</summary>
        private const float NameHeight = 18.0f;

        private const float HealthHeight = 17.0f;
        private const float ManaHeight = 14.0f;
        private const float StaminaHeight = 7.0f;

        private const float LevelBadgeSize = 30.0f;

        /// <summary>WoW's own yellow for a name.</summary>
        private static readonly Color NameGold = new Color(1.0f, 0.82f, 0.0f, 1.0f);

        private static readonly Color ManaBlue = new Color(0.12f, 0.38f, 1.0f, 1.0f);

        /// <summary>Stamina is white with a tinge of blue (owner, 2026-10-14: "make stamina very light blue like white with a slight tinge of blue").</summary>
        private static readonly Color StaminaPale = new Color(0.86f, 0.93f, 1.0f, 1.0f);

        private static readonly Color BadgeDark = new Color(0.06f, 0.05f, 0.04f, 0.96f);
        private static readonly Color BarText = new Color(1.0f, 1.0f, 1.0f, 0.95f);
        private static readonly Color TextOutline = new Color(0.0f, 0.0f, 0.0f, 0.9f);

        private static void BuildStatusFrame(Transform parent, PlayerHud hud)
        {
            var frame = new GameObject("StatusFrame", typeof(RectTransform));
            RectTransform rect = Attach(frame, parent);
            TopLeft(rect, PortraitCorner, new Vector2(PortraitSize - PlateTuck + PlateWidth, PortraitSize));

            // The plate first, so the portrait's ring is drawn over its left end.
            BuildPlate(frame.transform, hud);
            hud.LevelLabel = BuildPortrait(frame.transform, hud);
        }

        private static void BuildPlate(Transform parent, PlayerHud hud)
        {
            var plate = new GameObject("Plate", typeof(RectTransform));
            RectTransform rect = Attach(plate, parent);
            TopLeft(rect, new Vector2(PortraitSize - PlateTuck, -PlateDrop), new Vector2(PlateWidth, 0.0f));
            hud.Plate = plate.AddComponent<Image>();
            hud.Plate.color = Color.white;
            hud.Plate.raycastTarget = false;

            var rows = plate.AddComponent<VerticalLayoutGroup>();
            rows.padding = new RectOffset(Mathf.RoundToInt(PlateTuck + 9.0f), 10, 5, 8);
            rows.spacing = 3.0f;
            rows.childControlWidth = true;
            rows.childControlHeight = true;
            rows.childForceExpandWidth = true;
            rows.childForceExpandHeight = false;
            var fit = plate.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            hud.NameLabel = BuildName(plate.transform);
            var wells = new System.Collections.Generic.List<Image>();
            var fills = new System.Collections.Generic.List<Image>();
            BuildBar(plate.transform, "HealthBar", HealthHeight, PlayerHud.HealthGreen, 13, wells, fills,
                out hud.HealthFill, out hud.HealthFillImage, out hud.HealthText);
            hud.ManaBar = BuildBar(plate.transform, "ManaBar", ManaHeight, ManaBlue, 11, wells, fills,
                out hud.ManaFill, out _, out hud.ManaText);
            BuildBar(plate.transform, "StaminaBar", StaminaHeight, StaminaPale, 0, wells, fills,
                out hud.StaminaFill, out _, out _);
            hud.BarWells = wells.ToArray();
            hud.BarFills = fills.ToArray();
        }

        private static Text BuildName(Transform parent)
        {
            var name = new GameObject("NameLabel", typeof(RectTransform));
            Attach(name, parent);
            var layout = name.AddComponent<LayoutElement>();
            layout.preferredHeight = NameHeight;
            layout.minHeight = NameHeight;
            Text text = name.AddComponent<Text>();
            text.font = BuiltinFont();
            text.fontSize = 15;
            text.fontStyle = FontStyle.Bold;
            text.color = NameGold;
            text.alignment = TextAnchor.MiddleCenter;
            // A long name shrinks to fit rather than spilling past the plate.
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = 15;
            text.text = PlayerHud.DefaultName;
            text.raycastTarget = false;
            Outline outline = name.AddComponent<Outline>();
            outline.effectColor = TextOutline;
            outline.effectDistance = new Vector2(1.0f, -1.0f);
            return text;
        }

        /// <summary>
        /// One bar: its well (the bar's own image), the fill inside it a pixel in from the well's black edge (PlayerHud moves the fill's right
        /// edge), and its percentage over both when <paramref name="fontSize"/> is above zero.
        /// </summary>
        private static GameObject BuildBar(Transform parent, string name, float height, Color colour, int fontSize,
            System.Collections.Generic.List<Image> wells, System.Collections.Generic.List<Image> fills,
            out RectTransform fill, out Image fillImage, out Text label)
        {
            var bar = new GameObject(name, typeof(RectTransform));
            Attach(bar, parent);
            var layout = bar.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            Image well = bar.AddComponent<Image>();
            well.color = Color.white;
            well.raycastTarget = false;
            wells.Add(well);

            var inner = new GameObject("Inner", typeof(RectTransform));
            Stretch(Attach(inner, bar.transform), 1.0f);
            var fillObject = new GameObject("Fill", typeof(RectTransform));
            fill = Attach(fillObject, inner.transform);
            Stretch(fill, 0.0f);
            fillImage = fillObject.AddComponent<Image>();
            fillImage.color = colour;
            fillImage.raycastTarget = false;
            fills.Add(fillImage);

            label = null;
            if (fontSize > 0)
            {
                var textObject = new GameObject("Text", typeof(RectTransform));
                Stretch(Attach(textObject, bar.transform), 0.0f);
                label = textObject.AddComponent<Text>();
                label.font = BuiltinFont();
                label.fontSize = fontSize;
                label.fontStyle = FontStyle.Bold;
                label.color = BarText;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.text = "100%";
                label.raycastTarget = false;
                Outline outline = textObject.AddComponent<Outline>();
                outline.effectColor = TextOutline;
                outline.effectDistance = new Vector2(1.0f, -1.0f);
            }
            return bar;
        }

        private static Text BuildPortrait(Transform parent, PlayerHud hud)
        {
            Sprite circle = CircleSprite.Ensure();

            var clip = new GameObject("PortraitClip", typeof(RectTransform));
            RectTransform clipRect = Attach(clip, parent);
            TopLeft(clipRect, Vector2.zero, new Vector2(PortraitSize, PortraitSize));

            // The ring's width in pixels: the face is cut to a circle just inside it, a pixel under its inner edge.
            float ringWidth = PortraitSize * 0.5f * HudArt.RingWidthFraction;

            // A dark disc behind the face, so the ring's soft inner edge blends onto dark, never onto the plate.
            var backing = new GameObject("Backing", typeof(RectTransform));
            Stretch(Attach(backing, clip.transform), ringWidth * 0.5f);
            Image backingImage = backing.AddComponent<Image>();
            backingImage.sprite = circle;
            backingImage.color = BadgeDark;
            backingImage.raycastTarget = false;

            var maskObject = new GameObject("PortraitMask", typeof(RectTransform));
            RectTransform maskRect = Attach(maskObject, clip.transform);
            Stretch(maskRect, ringWidth - 1.0f);
            Image maskImage = maskObject.AddComponent<Image>();
            maskImage.sprite = circle;
            maskImage.raycastTarget = false;
            maskObject.AddComponent<Mask>().showMaskGraphic = false;

            var portraitImageObject = new GameObject("PortraitImage", typeof(RectTransform));
            RectTransform imageRect = Attach(portraitImageObject, maskObject.transform);
            Stretch(imageRect, 0.0f);
            RawImage rawImage = portraitImageObject.AddComponent<RawImage>();
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
            hud.PortraitImage = rawImage;

            // The hollow under the rim, over the face.
            var shadow = new GameObject("RimShadow", typeof(RectTransform));
            Stretch(Attach(shadow, maskObject.transform), 0.0f);
            hud.PortraitRimShadow = shadow.AddComponent<Image>();
            hud.PortraitRimShadow.color = Color.white;
            hud.PortraitRimShadow.raycastTarget = false;

            // The gold ring over the face's edge and the plate's end.
            var ring = new GameObject("Ring", typeof(RectTransform));
            Stretch(Attach(ring, clip.transform), 0.0f);
            hud.PortraitRing = ring.AddComponent<Image>();
            hud.PortraitRing.color = Color.white;
            hud.PortraitRing.raycastTarget = false;

            return BuildLevelBadge(clip.transform, hud, circle);
        }

        /// <summary>The level on a small dark disc in its own gold ring, over the portrait's lower left, as WoW's frames have it.</summary>
        private static Text BuildLevelBadge(Transform parent, PlayerHud hud, Sprite circle)
        {
            var badge = new GameObject("LevelBadge", typeof(RectTransform));
            RectTransform badgeRect = Attach(badge, parent);
            TopLeft(badgeRect, new Vector2(-3.0f, -(PortraitSize - LevelBadgeSize + 3.0f)), new Vector2(LevelBadgeSize, LevelBadgeSize));
            Image disc = badge.AddComponent<Image>();
            disc.sprite = circle;
            disc.color = BadgeDark;
            disc.raycastTarget = false;

            var ring = new GameObject("LevelRing", typeof(RectTransform));
            Stretch(Attach(ring, badge.transform), 0.0f);
            hud.LevelRing = ring.AddComponent<Image>();
            hud.LevelRing.color = Color.white;
            hud.LevelRing.raycastTarget = false;

            var label = new GameObject("LevelLabel", typeof(RectTransform));
            Stretch(Attach(label, badge.transform), 0.0f);
            Text text = label.AddComponent<Text>();
            text.font = BuiltinFont();
            text.fontSize = 13;
            text.fontStyle = FontStyle.Bold;
            text.color = NameGold;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = "1";
            text.raycastTarget = false;
            Outline outline = label.AddComponent<Outline>();
            outline.effectColor = TextOutline;
            outline.effectDistance = new Vector2(1.0f, -1.0f);
            return text;
        }

        /// <summary>
        /// hud.tscn's own AmmoLabel: bottom-right, hidden by PlayerHud
        /// itself unless a gun is actually equipped.
        /// </summary>
        private static Text BuildAmmoLabel(Transform parent)
        {
            var label = new GameObject("AmmoLabel", typeof(RectTransform));
            RectTransform rect = Attach(label, parent);
            BottomRight(rect, new Vector2(-20.0f, 20.0f), new Vector2(200.0f, 40.0f));

            Text text = label.AddComponent<Text>();
            text.font = BuiltinFont();
            text.fontSize = 28;
            text.color = Gold;
            text.alignment = TextAnchor.MiddleRight;
            text.text = "15 / 45";
            label.SetActive(false);
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

        /// <summary>Anchored to the parent's bottom-right corner, as Godot lays out.</summary>
        private static void BottomRight(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(1.0f, 0.0f);
            rect.anchorMax = new Vector2(1.0f, 0.0f);
            rect.pivot = new Vector2(1.0f, 0.0f);
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

        private static Image MakeRect(string name, Transform parent, Color colour, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = Attach(go, parent);
            Centre(rect, Vector2.zero, size);
            Image image = go.AddComponent<Image>();
            image.color = colour;
            // Nothing should ever click a crosshair, and every raycast target
            // costs work per pointer event.
            image.raycastTarget = false;
            return image;
        }

        private static Font BuiltinFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
