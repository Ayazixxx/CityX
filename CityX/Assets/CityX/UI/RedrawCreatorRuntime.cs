using CityX.Character;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Builds ReDraw's own character creator overlay -- AT RUNTIME, during
    /// play. This is a deliberate, purpose-built twin of
    /// CityX.UIEditor.CharacterCreatorBuilder, not a call into it:
    /// CharacterCreatorBuilder lives in CityX.UI.Editor, an editor-only
    /// assembly (includePlatforms: Editor) built around AssetDatabase and
    /// meant to bake the title screen's own creator into a scene ONCE, at
    /// edit time. Pressing ReDraw happens DURING PLAY, and nothing
    /// editor-only can run then -- so the whole stage-building/stripping/
    /// tool-column pipeline is reimplemented here using only runtime-safe
    /// APIs (Destroy instead of DestroyImmediate, serialized Mesh/Material
    /// references instead of AssetDatabase.LoadAssetAtPath, a project layer
    /// looked up by name instead of one created on demand).
    ///
    /// CLONES THE LIVE PLAYER, NOT A PREFAB. The title screen's creator
    /// clones Player.prefab because it is making a character from nothing;
    /// ReDraw already has one -- the body standing in the world -- so that
    /// is what gets cloned, then immediately reseeded from CharacterSave
    /// (see PlayerRedrawStarter) rather than trusting whatever the live
    /// renderer happens to be showing.
    ///
    /// NO NAME PROMPT, NO CHARACTER-SELECT HANDOFF. Those are how a BRAND
    /// NEW character gets made; ReDraw only ever edits an existing one, so
    /// its own action bar is a plain SAVE/CANCEL pair instead of ENTER THE
    /// CITY/BACK -- see CreatorOverlays' own OnConfirmRedraw/OnCancelRedraw.
    /// </summary>
    public static class RedrawCreatorRuntime
    {
        private static readonly Color Gold = new Color(0.75f, 0.6f, 0.2f, 1.0f);
        private static readonly Color Paper = new Color(0.82f, 0.82f, 0.84f, 1.0f);
        private static readonly Color Idle = new Color(0.11f, 0.12f, 0.15f, 0.95f);

        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);

        private const float ColumnWidth = 200.0f;
        private const float ActionBarHeight = 88.0f;

        private static readonly Color[] Palette =
        {
            new Color(0.10f, 0.10f, 0.12f), new Color(0.95f, 0.95f, 0.95f),
            new Color(0.72f, 0.16f, 0.16f), new Color(0.90f, 0.45f, 0.12f),
            new Color(0.92f, 0.80f, 0.20f), new Color(0.25f, 0.60f, 0.28f),
            new Color(0.20f, 0.45f, 0.80f), new Color(0.45f, 0.25f, 0.65f),
            new Color(0.85f, 0.55f, 0.70f), new Color(0.55f, 0.40f, 0.28f),
            new Color(0.78f, 0.62f, 0.53f), new Color(0.45f, 0.48f, 0.52f),
        };

        /// <summary>
        /// Builds the whole overlay -- 3D stage plus tool column -- and
        /// returns its root. Destroy the returned GameObject to tear the
        /// whole session down; there is nothing else to clean up.
        /// </summary>
        public static GameObject Build(GameObject player, Mesh[] hairMeshes, Material hairMaterial,
            System.Action<CharacterCreator> onConfirm, System.Action onCancel)
        {
            var root = new GameObject("RedrawOverlay", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above every other panel -- ReDraw is a full takeover, the same
            // way the C-menu's own camera cut is, and it is reached only
            // after the C-menu has already closed itself.
            canvas.sortingOrder = 90;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            GameObject stage = BuildStage(player, hairMeshes, hairMaterial, out CharacterCreator creator);
            root.AddComponent<CreatorStage>().Stage = stage;

            var overlays = root.AddComponent<CreatorOverlays>();
            overlays.Creator = creator;
            overlays.OnConfirmRedraw = onConfirm;
            overlays.OnCancelRedraw = onCancel;

            BuildToolColumn(root.transform, creator, overlays);
            HairColourPanel.Build(root.transform, creator);
            BuildDiscardPrompt(root.transform, overlays);

            EnsureEventSystem();
            return root;
        }

        // --- the 3D half ---

        /// <summary>
        /// The turntable, the cloned body and the camera. A runtime twin of
        /// CharacterCreatorBuilder.BuildStage -- see this file's own doc
        /// comment for why it cannot just call that one.
        /// </summary>
        private static GameObject BuildStage(GameObject player, Mesh[] hairMeshes, Material hairMaterial,
            out CharacterCreator creator)
        {
            var stage = new GameObject("RedrawStage");
            // INACTIVE FOR THE WHOLE BUILD, activated only at the very end.
            // CharacterCreator.Awake() (which strips ragdoll physics via its
            // own Disarm()) and CharacterHair.Awake() both fire the instant
            // their component is added to an ACTIVE GameObject -- which,
            // this early, is before BodyCollider/HeadCollider/Head/
            // StyleMeshes are ever assigned to them below. Disarm() would
            // then see those fields still null and destroy the very
            // MeshColliders just added a few lines above it, and the hair
            // would rebuild against nothing. CharacterCreatorBuilder's own
            // BuildStage sidesteps this the same way, just later -- it also
            // leaves its stage inactive until CreatorStage re-enables it,
            // well after every field below is already wired.
            stage.SetActive(false);

            var pivotObject = new GameObject("RotationPivot");
            pivotObject.transform.SetParent(stage.transform, false);
            pivotObject.transform.localPosition =
                new Vector3(0.0f, BodyRig.StandingHeight * 0.5f, 0.0f);

            GameObject body = Object.Instantiate(player, pivotObject.transform);
            body.name = "Body";
            body.transform.localPosition = new Vector3(0.0f, -BodyRig.StandingHeight * 0.5f, 0.0f);
            // The live player carries whatever yaw they were last facing --
            // reset so the clone always stands square to the camera, the
            // same framing every session opens on.
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            // DEPENDANTS BEFORE WHAT THEY DEPEND ON, matching
            // CharacterCreatorBuilder.BuildStage's own order -- PlayerLocomotion,
            // PlayerClimbing and GrappleSwing all require the CharacterController,
            // PlayerAnimationDriver requires the MeleeAttacker. Destroy() defers
            // to end of frame rather than refusing the way DestroyImmediate can,
            // but the order still keeps this predictable rather than relying on
            // that.
            // PlayerRedrawStarter FIRST -- the clone is Instantiate()'d from
            // the LIVE player, which carries PlayerRedrawStarter itself, and
            // that component's own [RequireComponent(CharacterIdentity)]
            // blocks CharacterIdentity's own removal below until this is gone.
            Strip<PlayerRedrawStarter>(body);
            Strip<PlayerInputReader>(body);
            Strip<PlayerLocomotion>(body);
            Strip<PlayerClimbing>(body);
            Strip<PlayerVaulting>(body);
            Strip<WallStick>(body);
            Strip<GrappleSwing>(body);
            Strip<CharacterController>(body);
            Strip<PlayerAnimationDriver>(body);
            Strip<MeleeAttacker>(body);
            Strip<Ragdoll>(body);
            Strip<CharacterIdentity>(body);
            Strip<PlayerVitals>(body);
            Strip<Inventory>(body);
            // The clone already wears one, cloned from the live player --
            // CharacterHair fights over the same head bone if two land on
            // the same body, and AddHair below builds a fresh one anyway.
            Strip<CharacterHair>(body);

            // Ragdoll (every Joint/Rigidbody/Collider except the two a click
            // is tested against) and the frozen T-pose (every Animator
            // disabled) are CharacterCreator's own job -- see its Disarm(),
            // called from Initialise(), called from its own Awake() the
            // instant the component below is added. It is already written
            // runtime-safe (Destroy, not DestroyImmediate) for exactly this
            // reason: the editor's screenshot tool calls Initialise() too.

            var renderer = body.GetComponentInChildren<SkinnedMeshRenderer>();
            MeshCollider surface = null;
            if (renderer != null)
            {
                surface = renderer.gameObject.AddComponent<MeshCollider>();
                surface.sharedMesh = renderer.sharedMesh;
            }

            var headRenderer = FindHead(body);
            MeshCollider skull = null;
            if (headRenderer != null)
            {
                var filter = headRenderer.GetComponent<MeshFilter>();
                skull = headRenderer.gameObject.AddComponent<MeshCollider>();
                skull.sharedMesh = filter != null ? filter.sharedMesh : null;
            }
            else
            {
                Debug.LogWarning("CityX: ReDraw's clone has no head mesh -- the face cannot be painted.");
            }

            CharacterHair hair = AddHair(body, hairMeshes, hairMaterial);

            // THE LAYER MUST ALREADY EXIST -- a runtime script cannot add one
            // to the project the way CharacterCreatorBuilder.EnsureLayer does
            // at edit time (that needs TagManager.asset, an editor-only
            // asset). The title screen's own creator already creates
            // "CityXCreator" the first time it is built, so in practice this
            // is always there by the time ReDraw ships; this only degrades
            // rather than throwing if a project somehow reaches ReDraw
            // without ever having built the title screen.
            int layer = LayerMask.NameToLayer("CityXCreator");
            if (layer < 0)
            {
                Debug.LogWarning("CityX: no CityXCreator layer -- ReDraw will render the whole " +
                                 "city behind the character. Build the title screen scene once " +
                                 "in the editor to create it.");
                layer = 0;
            }
            SetLayerRecursively(stage, layer);

            var cameraObject = new GameObject("RedrawCamera");
            cameraObject.transform.SetParent(stage.transform, false);
            // Character_creator.tscn's own framing, the same numbers
            // CharacterCreatorBuilder.BuildStage uses -- 1.6 up, 4 in front,
            // pitched 8 degrees down, 75 degree vertical field of view.
            cameraObject.transform.localPosition = new Vector3(0.0f, 1.6f, 4.0f);
            cameraObject.transform.localRotation = Quaternion.Euler(8.0f, 180.0f, 0.0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
            camera.fieldOfView = 75.0f;
            camera.nearClipPlane = 0.05f;
            camera.cullingMask = 1 << layer;
            // Above every camera already in the world -- the C-menu's own
            // front-on shot is the only thing that could plausibly still be
            // live underneath, and ReDraw is only reached after that menu
            // has already closed itself.
            camera.depth = 10;
            camera.allowMSAA = true;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraObject.layer = layer;

            AddLight(stage, layer, "Key", new Vector3(22.0f, 150.0f, 0.0f), 1.5f, Color.white);
            AddLight(stage, layer, "Fill", new Vector3(10.0f, -40.0f, 0.0f), 0.9f,
                new Color(0.82f, 0.86f, 1.0f));
            AddLight(stage, layer, "Back", new Vector3(-25.0f, 20.0f, 0.0f), 0.6f,
                new Color(1.0f, 0.95f, 0.85f));

            creator = stage.AddComponent<CharacterCreator>();
            creator.RotationPivot = pivotObject.transform;
            creator.Camera = camera;
            creator.BodyCollider = surface;
            creator.BodyRenderer = renderer;
            creator.HeadCollider = skull;
            creator.HeadRenderer = headRenderer;
            creator.Hair = hair;

            // NOW it is safe to switch on -- every field the Awake() calls
            // about to fire (CharacterCreator's, CharacterHair's) depend on
            // is already wired.
            stage.SetActive(true);
            return stage;
        }

        private static CharacterHair AddHair(GameObject body, Mesh[] hairMeshes, Material hairMaterial)
        {
            Transform head = FindDeep(body.transform, "head");
            if (head == null)
            {
                Debug.LogWarning("CityX: no head bone on ReDraw's clone -- no hair.");
                return null;
            }
            var hair = body.AddComponent<CharacterHair>();
            hair.Head = head;
            // Every style after "none", in CharacterHair.ValidStyles order.
            hair.StyleMeshes = hairMeshes ?? new Mesh[0];
            hair.HairMaterial = hairMaterial;
            return hair;
        }

        private static MeshRenderer FindHead(GameObject body)
        {
            Transform head = FindDeep(body.transform, "head");
            return head != null ? head.GetComponentInChildren<MeshRenderer>(true) : null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        private static void AddLight(GameObject stage, int layer, string name, Vector3 angles,
            float intensity, Color colour)
        {
            var go = new GameObject($"{name}Light");
            go.transform.SetParent(stage.transform, false);
            go.transform.rotation = Quaternion.Euler(angles);
            go.layer = layer;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = colour;
            light.cullingMask = 1 << layer;
            light.shadows = LightShadows.None;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        /// <summary>RUNTIME-SAFE: Destroy(), not DestroyImmediate() -- this always runs during play.</summary>
        private static void Strip<T>(GameObject root) where T : Component
        {
            foreach (T found in root.GetComponentsInChildren<T>(includeInactive: true))
            {
                Object.Destroy(found);
            }
        }

        // --- the tool column ---

        /// <summary>
        /// The same tool set the title screen's creator has -- brush/bucket/
        /// eyedropper, the colour square and palette, brush size, mirror/
        /// eraser, undo/redo, hair -- minus the name prompt and CharacterSelect
        /// handoff, which only apply to making a brand new character.
        /// </summary>
        private static void BuildToolColumn(Transform parent, CharacterCreator creator,
            CreatorOverlays overlays)
        {
            var column = new GameObject("Tools", typeof(RectTransform));
            var rect = (RectTransform)column.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.0f, 0.0f);
            rect.anchorMax = new Vector2(0.0f, 1.0f);
            rect.pivot = new Vector2(0.0f, 0.5f);
            rect.offsetMin = new Vector2(0.0f, 0.0f);
            rect.offsetMax = new Vector2(ColumnWidth, 0.0f);

            column.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.92f);

            var controls = column.AddComponent<CreatorControls>();
            controls.Creator = creator;

            Transform t = column.transform;

            MakeLabel(t, "Heading", "REDRAW", 20, Gold, At(-24.0f), 24.0f);

            MakeToolRow(t, creator, controls);

            MakeLabel(t, "PaletteLabel", "COLOUR", 13, Paper, At(-94.0f), 18.0f);
            controls.SVSquare = MakeSquare(t, At(-166.0f), 128.0f);
            controls.Hue = MakeHueSlider(t, "HueSlider", At(-244.0f), 150.0f);
            controls.Preview = MakeSwatch(t, "Preview", At(-266.0f),
                new Vector2(150.0f, 16.0f), Color.white);

            MakePalette(t, creator, controls, -292.0f);

            controls.BrushLabel =
                MakeLabel(t, "BrushLabel", "BRUSH  1", 13, Paper, At(-348.0f), 18.0f);
            controls.BrushSize = MakeSlider(t, "BrushSlider", At(-370.0f), 1.0f, 15.0f,
                CharacterCreator.DefaultBrushRadius, wholeNumbers: true);

            controls.MirrorButton = MakeButton(t, "MirrorButton", "MIRROR", Paper,
                new Vector2(-46.0f, -400.0f), new Vector2(88.0f, 30.0f),
                ButtonAction.Kind.CreatorToggleMirror, creator, overlays);
            controls.EraserButton = MakeButton(t, "EraserButton", "ERASER", Paper,
                new Vector2(46.0f, -400.0f), new Vector2(88.0f, 30.0f),
                ButtonAction.Kind.CreatorToggleEraser, creator, overlays);

            MakeButton(t, "UndoButton", "UNDO", Paper,
                new Vector2(-46.0f, -434.0f), new Vector2(88.0f, 30.0f),
                ButtonAction.Kind.CreatorUndo, creator, overlays);
            MakeButton(t, "RedoButton", "REDO", Paper,
                new Vector2(46.0f, -434.0f), new Vector2(88.0f, 30.0f),
                ButtonAction.Kind.CreatorRedo, creator, overlays);
            controls.UndoButton = t.Find("UndoButton").GetComponent<Button>();
            controls.RedoButton = t.Find("RedoButton").GetComponent<Button>();

            MakeLabel(t, "HairLabel", "HAIR", 13, Paper, At(-458.0f), 18.0f);
            int styleCount = CharacterHair.ValidStyles.Length;
            for (int style = 0; style < styleCount; style++)
            {
                MakeButton(t, "Hair" + CharacterHair.ValidStyles[style] + "Button",
                    CharacterHair.LabelFor(CharacterHair.ValidStyles[style]), Paper,
                    new Vector2(HairButtonRow.CentreX(style, styleCount), -484.0f + HairButtonRow.RowOffsetY(style)),
                    new Vector2(HairButtonRow.ButtonWidth(styleCount), HairButtonRow.Height),
                    ButtonAction.Kind.CreatorHair, creator, overlays, value: style,
                    fontSize: HairButtonRow.FontSize(styleCount));
            }

            MakeLabel(t, "HintLabel",
                "Drag the body to paint it.\nDrag anywhere else to turn it.\nScroll to zoom.",
                10, new Color(0.62f, 0.64f, 0.70f),
                At(-524.0f - (HairButtonRow.Rows(styleCount) - 1) * HairButtonRow.RowPitch), 40.0f);

            var bar = new GameObject("ActionBar", typeof(RectTransform));
            var barRect = (RectTransform)bar.transform;
            barRect.SetParent(t, false);
            barRect.anchorMin = new Vector2(0.0f, 0.0f);
            barRect.anchorMax = new Vector2(1.0f, 0.0f);
            barRect.pivot = new Vector2(0.5f, 0.0f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0.0f, ActionBarHeight);
            bar.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 1.0f);

            // SAVE/CANCEL, not ENTER THE CITY/BACK -- there is no name to
            // give an existing character and nowhere to hand it off to.
            // CANCEL reuses CreatorTryBack rather than a redraw-only Kind:
            // that already shows the discard prompt when HasEdits is true
            // and calls Discard() otherwise -- the exact same "are you sure"
            // guard the title screen gets, just routed to OnCancelRedraw
            // instead of Title.Show (see CreatorOverlays.Discard).
            MakeAnchoredButton(t, "SaveButton", "SAVE", Gold,
                new Vector2(0.0f, 52.0f), new Vector2(172.0f, 36.0f),
                ButtonAction.Kind.CreatorRedrawSave, creator, overlays);
            MakeAnchoredButton(t, "CancelButton", "CANCEL", Paper,
                new Vector2(0.0f, 16.0f), new Vector2(172.0f, 32.0f),
                ButtonAction.Kind.CreatorTryBack, creator, overlays);
        }

        private static Vector2 At(float y) => new Vector2(0.0f, y);

        private static void MakeToolRow(Transform parent, CharacterCreator creator,
            CreatorControls controls)
        {
            string[] names = { "Brush", "Bucket", "Eyedropper" };
            string[] kinds = { "brush", "bucket", "eyedropper" };

            var faces = new Image[names.Length];
            var icons = new RawImage[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                Image face = MakeButton(parent, names[i] + "Button", string.Empty, Gold,
                    new Vector2(i * 58.0f - 58.0f, -62.0f), new Vector2(54.0f, 40.0f),
                    ButtonAction.Kind.CreatorTool, creator, overlays: null, value: i);
                faces[i] = face;

                var iconObject = new GameObject("Icon", typeof(RectTransform));
                var iconRect = (RectTransform)iconObject.transform;
                iconRect.SetParent(face.transform, false);
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(30.0f, 30.0f);

                var icon = iconObject.AddComponent<RawImage>();
                icon.texture = ToolIcons.Build(kinds[i]);
                icon.raycastTarget = false;
                icons[i] = icon;
            }

            controls.ToolButtons = faces;
            controls.ToolIconImages = icons;
        }

        private static RawImage MakeSquare(Transform parent, Vector2 position, float size)
        {
            var go = new GameObject("SVSquare", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);

            var image = go.AddComponent<RawImage>();
            image.texture = CreatorControls.BuildSquare(0.0f);
            return image;
        }

        private static Slider MakeHueSlider(Transform parent, string name, Vector2 position,
            float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 16.0f);

            var background = go.AddComponent<RawImage>();
            background.texture = CreatorControls.BuildHueStrip();

            var fillArea = new GameObject("Fill", typeof(RectTransform));
            var fillRect = (RectTransform)fillArea.transform;
            fillRect.SetParent(go.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fill = fillArea.AddComponent<Image>();
            fill.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);

            var handle = new GameObject("Handle", typeof(RectTransform));
            var handleRect = (RectTransform)handle.transform;
            handleRect.SetParent(go.transform, false);
            handleRect.sizeDelta = new Vector2(6.0f, 20.0f);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Paper;

            Slider slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0.0f;
            slider.maxValue = 1.0f;
            slider.wholeNumbers = false;
            slider.value = 0.0f;
            return slider;
        }

        private static Image MakeSwatch(Transform parent, string name, Vector2 position,
            Vector2 size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static Slider MakeSlider(Transform parent, string name, Vector2 position,
            float minimum, float maximum, float value, bool wholeNumbers)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(150.0f, 16.0f);

            Image background = go.AddComponent<Image>();
            background.color = new Color(0.16f, 0.17f, 0.20f, 1.0f);

            var fillArea = new GameObject("Fill", typeof(RectTransform));
            var fillRect = (RectTransform)fillArea.transform;
            fillRect.SetParent(go.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fill = fillArea.AddComponent<Image>();
            fill.color = Gold;

            var handle = new GameObject("Handle", typeof(RectTransform));
            var handleRect = (RectTransform)handle.transform;
            handleRect.SetParent(go.transform, false);
            handleRect.sizeDelta = new Vector2(10.0f, 20.0f);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Paper;

            Slider slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;
            return slider;
        }

        private static void MakePalette(Transform parent, CharacterCreator creator,
            CreatorControls controls, float top)
        {
            const int perRow = 6;
            const float pitch = 28.0f;

            for (int i = 0; i < Palette.Length; i++)
            {
                Color swatch = Palette[i];
                int column = i % perRow;
                int row = i / perRow;

                var go = new GameObject($"Swatch{i}", typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(
                    column * pitch - pitch * (perRow - 1) * 0.5f,
                    top - row * pitch);
                rect.sizeDelta = new Vector2(26.0f, 26.0f);

                Image image = go.AddComponent<Image>();
                image.color = swatch;

                Button button = go.AddComponent<Button>();
                button.targetGraphic = image;

                var wiring = go.AddComponent<ButtonAction>();
                wiring.Action = ButtonAction.Kind.CreatorColour;
                wiring.Creator = creator;
                wiring.Colour = swatch;
                wiring.Controls = controls;
            }
        }

        // --- the discard-confirmation prompt ---

        /// <summary>
        /// The same "discard your painted design and go back?" check the
        /// title screen's creator has, reused as-is -- CreatorOverlays'
        /// TryGoBack/KeepEditing/Discard already work for either flow (see
        /// Discard's own Title-vs-redraw branch).
        /// </summary>
        private static void BuildDiscardPrompt(Transform parent, CreatorOverlays overlays)
        {
            GameObject dim = MakeDim(parent, "DiscardPrompt");
            RectTransform panel = MakePromptPanel(dim.transform, new Vector2(400.0f, 180.0f));

            Text question = MakeLabel(panel, "Question",
                "Discard your painted design and go back?", 16, Paper,
                new Vector2(0.0f, 34.0f), 48.0f);
            ((RectTransform)question.transform).sizeDelta = new Vector2(340.0f, 48.0f);

            MakePromptButton(panel, "DiscardButton", "DISCARD & BACK", Paper,
                new Vector2(-80.0f, -44.0f), new Vector2(150.0f, 40.0f),
                ButtonAction.Kind.CreatorDiscard, overlays);
            MakePromptButton(panel, "CancelButton", "CANCEL", Gold,
                new Vector2(85.0f, -44.0f), new Vector2(110.0f, 40.0f),
                ButtonAction.Kind.CreatorKeepEditing, overlays);

            overlays.ConfirmPanel = dim;
            dim.SetActive(false);
        }

        private static GameObject MakeDim(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            go.AddComponent<Image>().color = new Color(0.0f, 0.0f, 0.0f, 0.7f);
            return go;
        }

        private static RectTransform MakePromptPanel(Transform parent, Vector2 size)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            go.AddComponent<Image>().color = new Color(0.10f, 0.11f, 0.14f, 0.98f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.63f, 1.0f, 0.6f);
            outline.effectDistance = new Vector2(2.0f, -2.0f);
            return rect;
        }

        private static void MakePromptButton(Transform parent, string name, string label,
            Color tint, Vector2 position, Vector2 size, ButtonAction.Kind action,
            CreatorOverlays overlays)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image background = go.AddComponent<Image>();
            background.color = Idle;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;

            var wiring = go.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Overlays = overlays;
            wiring.Creator = overlays.Creator;

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = tint;
            button.colors = colours;

            Caption(go.transform, label, 13, tint);
        }

        // --- small pieces ---

        private static Image MakeButton(Transform parent, string name, string label, Color tint,
            Vector2 position, Vector2 size, ButtonAction.Kind action,
            CharacterCreator creator, CreatorOverlays overlays, int value = 0, int fontSize = 13)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image background = go.AddComponent<Image>();
            background.color = Idle;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;

            var wiring = go.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Creator = creator;
            wiring.Value = value;
            wiring.Overlays = overlays;

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = tint;
            colours.disabledColor = new Color(0.09f, 0.09f, 0.10f, 0.6f);
            button.colors = colours;

            if (!string.IsNullOrEmpty(label))
            {
                Caption(go.transform, label, fontSize, tint);
            }
            return background;
        }

        private static Text Caption(Transform parent, string content, int size, Color colour)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static void MakeAnchoredButton(Transform parent, string name, string label,
            Color tint, Vector2 position, Vector2 size, ButtonAction.Kind action,
            CharacterCreator creator, CreatorOverlays overlays)
        {
            MakeButton(parent, name, label, tint, position, size, action, creator, overlays);
            var rect = (RectTransform)parent.Find(name);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.0f);
        }

        private static Text MakeLabel(Transform parent, string name, string content, int size,
            Color colour, Vector2 position, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(ColumnWidth - 16.0f, height);

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static Text MakeLabel(RectTransform parent, string name, string content,
            int size, Color colour, Vector2 position, float height)
        {
            Text text = MakeLabel(parent.transform, name, content, size, colour, position, height);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Without one of these no button on this panel ever receives a click -- see MapPanelBuilder's own copy of this note.</summary>
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
    }
}
