using CityX.Character;
using CityX.Core;
using CityX.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the character creator: the body on its turntable, its own camera,
    /// and the tool panel down the side.
    ///
    /// A SEPARATE CAMERA POINTED AT A LAYER OF ITS OWN, rather than reusing the
    /// title screen's. The creator has to be able to orbit and zoom without the
    /// night city behind it swinging around too, and the Godot build hit
    /// exactly this -- its own note records the creator being visually
    /// corrupted by the title screen's viewport until the two were separated.
    /// </summary>
    public static class CharacterCreatorBuilder
    {
        private static readonly Color Gold = new Color(0.75f, 0.6f, 0.2f, 1.0f);
        private static readonly Color Paper = new Color(0.82f, 0.82f, 0.84f, 1.0f);

        /// <summary>An unlit button. CreatorControls lights the active ones.</summary>
        private static readonly Color Idle = new Color(0.11f, 0.12f, 0.15f, 0.95f);

        /// <summary>
        /// The panel down the left, in canvas units. The canvas is 1152x648, so
        /// the whole stack below has to fit above the action bar at the bottom
        /// -- it grew past it once already and the two ended up drawn over each
        /// other, hiding UNDO and REDO completely.
        /// </summary>
        private const float ColumnWidth = 200.0f;

        private const float ActionBarHeight = 88.0f;

        /// <summary>
        /// The palette down the side. Twelve presets for the colours anybody
        /// reaches for first; the wheel above them is what makes every other
        /// colour reachable.
        /// </summary>
        private static readonly Color[] Palette =
        {
            new Color(0.10f, 0.10f, 0.12f), new Color(0.95f, 0.95f, 0.95f),
            new Color(0.72f, 0.16f, 0.16f), new Color(0.90f, 0.45f, 0.12f),
            new Color(0.92f, 0.80f, 0.20f), new Color(0.25f, 0.60f, 0.28f),
            new Color(0.20f, 0.45f, 0.80f), new Color(0.45f, 0.25f, 0.65f),
            new Color(0.85f, 0.55f, 0.70f), new Color(0.55f, 0.40f, 0.28f),
            new Color(0.78f, 0.62f, 0.53f), new Color(0.45f, 0.48f, 0.52f),
        };

        public static GameObject Build(Transform parent, TitleScreen title, GameObject bodyPrefab)
        {
            var panel = new GameObject("CreatorPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            Stretch(rect);

            // The 3D half lives outside the canvas -- a canvas cannot hold a
            // body -- and is switched on and off with the panel by a small
            // relay rather than by anything remembering to do it by hand.
            GameObject stage = BuildStage(bodyPrefab, out CharacterCreator creator);
            panel.AddComponent<CreatorStage>().Stage = stage;

            var overlays = panel.AddComponent<CreatorOverlays>();
            overlays.Creator = creator;
            overlays.Title = title;

            BuildTools(panel.transform, creator, title, overlays);
            // Down the right, where there is room -- the left column is full. Shown only while the
            // character has hair.
            HairColourPanel.Build(panel.transform, creator);
            BuildNamePrompt(panel.transform, overlays);
            BuildDiscardPrompt(panel.transform, overlays);
            return panel;
        }

        /// <summary>
        /// The turntable, the body and the camera.
        ///
        /// The body gets a MESH COLLIDER, which is the only reason a click can
        /// become a triangle at all -- without one the raycast finds nothing
        /// and every stroke silently does nothing.
        /// </summary>
        private static GameObject BuildStage(GameObject bodyPrefab, out CharacterCreator creator)
        {
            var stage = new GameObject("CreatorStage");

            var pivotObject = new GameObject("RotationPivot");
            pivotObject.transform.SetParent(stage.transform, false);
            // Raised to the torso so the body orbits about its middle rather
            // than swinging around its feet -- character_creator.gd's own
            // _recenter_on_torso, which does the same thing in two halves.
            pivotObject.transform.localPosition =
                new Vector3(0.0f, BodyRig.StandingHeight * 0.5f, 0.0f);

            GameObject body = Object.Instantiate(bodyPrefab, pivotObject.transform);
            body.name = "Body";
            body.transform.localPosition =
                new Vector3(0.0f, -BodyRig.StandingHeight * 0.5f, 0.0f);

            // Everything the world half of the prefab brings with it is dead
            // weight here and some of it is actively wrong -- a
            // CharacterController would push the body about, and the input
            // reader would try to walk it.
            // DEPENDANTS BEFORE WHAT THEY DEPEND ON, throughout. Unity will not
            // remove a component another one declares it needs, and it refuses
            // with a console line rather than an exception -- so the wrong
            // order leaves the component behind and nothing stops the build.
            // PlayerLocomotion, PlayerClimbing and GrappleSwing all require
            // the CharacterController; PlayerAnimationDriver requires the
            // MeleeAttacker.
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

            // JOINTS BEFORE BODIES, AND CHECKED AFTERWARDS.
            //
            // A CharacterJoint depends on the Rigidbody on its own object, and
            // Unity refuses to remove a component another one still needs. So
            // destroying the bodies first quietly failed on every bone that
            // carried a joint, and ten rigidbodies and ten joints went into
            // the saved scene. Their colliders HAD gone, so the ragdoll had
            // nothing to rest on: the moment play started the bones fell, and
            // the skin stretched out behind them into a twisted ribbon.
            //
            // Nothing about that was visible at build time, and the editor's
            // screenshot does not run physics, so the creator looked correct
            // in every check made of it right up until somebody pressed play.
            Strip<Joint>(body);
            Strip<Rigidbody>(body);
            Strip<Collider>(body);

            int leftover = body.GetComponentsInChildren<Rigidbody>(true).Length
                           + body.GetComponentsInChildren<Joint>(true).Length
                           + body.GetComponentsInChildren<Collider>(true).Length;
            if (leftover > 0)
            {
                Debug.LogError($"CityX: {leftover} physics components would not come off the " +
                               "creator's body -- it will collapse into a ragdoll on play.");
            }

            // THE BODY HOLDS ITS BIND POSE HERE. The mesh collider below is
            // baked from sharedMesh, which is the UNPOSED mesh -- so an idle
            // animation playing underneath would move the visible body while
            // the surface a click tests against stayed where it was, and every
            // stroke would land somewhere other than where it was aimed. A
            // still T-pose is also the easiest thing to paint: nothing is
            // hidden behind anything else.
            foreach (Animator animator in body.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }

            var renderer = body.GetComponentInChildren<SkinnedMeshRenderer>();
            MeshCollider surface = null;
            if (renderer != null)
            {
                surface = renderer.gameObject.AddComponent<MeshCollider>();
                surface.sharedMesh = renderer.sharedMesh;
            }

            // THE SKULL IS ITS OWN MESH AND NEEDS ITS OWN COLLIDER. It is not
            // part of the body's shape field -- see HeadMesh -- so the body's
            // collider stops at the shoulders and a click on the face would
            // find nothing at all.
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
                Debug.LogWarning("CityX: the body has no head mesh -- the face cannot be painted.");
            }

            CharacterHair hair = AddHair(body);

            // ON ITS OWN LAYER, WHICH I SAID BEFORE AND DID NOT DO. Without a
            // culling mask this camera renders the whole scene, so the night
            // city came through behind the body and its own light lit the city
            // instead of the character -- you could not see what you were
            // painting. The layer is what makes the backdrop actually blank.
            int layer = EnsureLayer("CityXCreator");
            SetLayerRecursively(stage, layer);

            var cameraObject = new GameObject("CreatorCamera");
            cameraObject.transform.SetParent(stage.transform, false);
            // CHARACTER_CREATOR.TSCN'S OWN CAMERA, to the number: 1.6 up, 4
            // in front, pitched 8 degrees down, 75 degree vertical field of
            // view. The invented framing before this had the camera level
            // with the body's middle at a 42 degree field of view, which put
            // the whole character high in the frame and cropped -- the shot
            // the report "the character is too high up" was about. This is
            // the framing the Godot build actually shipped.
            //
            // POSITIVE Z, NOT NEGATIVE, AND YAWED 180: Godot's camera forward
            // is -Z, Unity's is +Z, so a value-for-value copy of Godot's own
            // node position put the camera on the WRONG side of the pivot --
            // behind the character rather than in front, the same spot
            // ThirdPersonCamera deliberately targets (pivot - forward *
            // distance) for its own over-the-shoulder gameplay shot. The
            // creator wants the opposite of that shot.
            cameraObject.transform.localPosition = new Vector3(0.0f, 1.6f, 4.0f);
            cameraObject.transform.localRotation = Quaternion.Euler(8.0f, 180.0f, 0.0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            // character_creator.tscn's own Environment background_color.
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
            camera.fieldOfView = 75.0f;
            camera.nearClipPlane = 0.05f;
            camera.cullingMask = 1 << layer;
            // Above the title's own camera, so the creator draws over the city
            // rather than being buried in it.
            camera.depth = 5;
            camera.allowMSAA = true;
            // The closest, most static view in the whole game -- players
            // stare at this body for minutes while painting it, so any
            // residual edge aliasing MSAA alone misses is the most visible
            // it will ever be.
            var creatorCameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            creatorCameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            creatorCameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraObject.layer = layer;

            // THREE LIGHTS, because one directional light on a matte body gives
            // a black side, and half of painting is on the side facing away.
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

            stage.SetActive(false);
            return stage;
        }

        /// <summary>
        /// Puts the hairstyle component on the head bone.
        ///
        /// The mesh arrives as an OBJ rather than the .glb the Godot build
        /// loads: Unity has no built-in glTF importer, so the .glb sat in the
        /// project as an opaque blob with a DefaultImporter meta and could not
        /// have been used at all. Same mesh, converted on the way in.
        /// </summary>
        private static CharacterHair AddHair(GameObject body)
        {
            Transform head = FindDeep(body.transform, "head");
            if (head == null)
            {
                Debug.LogWarning("CityX: no head bone on the body -- the creator has no hair.");
                return null;
            }

            var hair = body.AddComponent<CharacterHair>();
            hair.Head = head;

            // Every style's mesh, from the one list the other builders use too.
            hair.StyleMeshes = HairAssets.StyleMeshes();
            hair.HairMaterial = HairMaterial();
            return hair;
        }

        /// <summary>
        /// One shared hair material, saved as a real asset.
        ///
        /// A material made in memory here would be written into whatever scene
        /// happened to reference it, so the same hair would end up with a
        /// separate copy per scene and no single place to change its colour.
        /// </summary>
        internal static Material HairMaterial()
        {
            const string path = "Assets/CityX/Character/Materials/Hair.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                MakeHairMatte(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard");
            var material = new Material(shader);
            // character.gd's own HAIR_COLOR.
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", CharacterHair.HairColour);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", CharacterHair.HairColour);
            }
            MakeHairMatte(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// No shine on hair (owner, 2026-10-09: "get rid of the shinyness that is appearing on the character and the hair"): it had character.gd's
        /// gloss of 0.4, which caught every lamp at night. No gloss and no specular highlights, the same as the skin (CharacterSkin.MakeMatte).
        /// </summary>
        private static void MakeHairMatte(Material material)
        {
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.0f);
            }
            if (material.HasProperty("_SpecularHighlights"))
            {
                material.SetFloat("_SpecularHighlights", 0.0f);
            }
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }

        /// <summary>
        /// The skull's renderer: the one MeshRenderer under the head bone.
        /// Found by component rather than by name so a rename does not quietly
        /// leave the face unpaintable.
        /// </summary>
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
            // Only this layer, so the creator's lights do not also brighten the
            // night city the title screen is showing behind it.
            light.cullingMask = 1 << layer;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// Finds a layer by name, adding it to the project if it is not there.
        ///
        /// Written rather than asking somebody to add it by hand, because a
        /// missing layer fails as "the creator shows the whole city and is too
        /// dark to paint in" rather than as an error.
        /// </summary>
        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0)
            {
                return existing;
            }

            var manager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = manager.FindProperty("layers");

            // 8 is the first layer Unity leaves to the project; 0-7 are its own.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = name;
                    manager.ApplyModifiedProperties();
                    return i;
                }
            }

            Debug.LogWarning($"CityX: no free layer for '{name}' -- the creator will render " +
                             "the whole scene behind the character.");
            return 0;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        private static void Strip<T>(GameObject root) where T : Component
        {
            foreach (T found in root.GetComponentsInChildren<T>(includeInactive: true))
            {
                Object.DestroyImmediate(found);
            }
        }

        // --- the tool column ---

        private static void BuildTools(Transform parent, CharacterCreator creator,
            TitleScreen title, CreatorOverlays overlays)
        {
            // Down the left, so the body stays centred and a stroke near the
            // edge of it does not start under a button.
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

            MakeLabel(t, "Heading", "CREATE", 20, Gold, At(-24.0f), 24.0f);

            MakeToolRow(t, creator, controls);

            MakeLabel(t, "PaletteLabel", "COLOUR", 13, Paper, At(-94.0f), 18.0f);

            // THE SV SQUARE, not just swatches. Any colour has to be
            // reachable -- twelve presets is a palette, not a character
            // creator. Godot's own ColorPickerButton opens its default
            // SHAPE_HSV_RECTANGLE popup: a saturation/value square for
            // whatever hue is selected, plus a separate hue strip -- this is
            // that shape, not the hue/saturation wheel it replaced.
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
                ButtonAction.Kind.CreatorToggleMirror, creator);
            controls.EraserButton = MakeButton(t, "EraserButton", "ERASER", Paper,
                new Vector2(46.0f, -400.0f), new Vector2(88.0f, 30.0f),
                ButtonAction.Kind.CreatorToggleEraser, creator);

            MakeButton(t, "UndoButton", "UNDO", Paper,
                new Vector2(-46.0f, -434.0f), new Vector2(88.0f, 30.0f),
                ButtonAction.Kind.CreatorUndo, creator);
            MakeButton(t, "RedoButton", "REDO", Paper,
                new Vector2(46.0f, -434.0f), new Vector2(88.0f, 30.0f),
                ButtonAction.Kind.CreatorRedo, creator);
            controls.UndoButton = t.Find("UndoButton").GetComponent<Button>();
            controls.RedoButton = t.Find("RedoButton").GetComponent<Button>();

            MakeLabel(t, "HairLabel", "HAIR", 13, Paper, At(-458.0f), 18.0f);
            // One button per style, in CharacterHair.ValidStyles order (the value IS the
            // index), in a single row that narrows to fit -- see HairButtonRow.
            int styleCount = CharacterHair.ValidStyles.Length;
            for (int style = 0; style < styleCount; style++)
            {
                MakeButton(t, "Hair" + CharacterHair.ValidStyles[style] + "Button",
                    CharacterHair.LabelFor(CharacterHair.ValidStyles[style]), Paper,
                    new Vector2(HairButtonRow.CentreX(style, styleCount), -484.0f + HairButtonRow.RowOffsetY(style)),
                    new Vector2(HairButtonRow.ButtonWidth(styleCount), HairButtonRow.Height),
                    ButtonAction.Kind.CreatorHair, creator, value: style,
                    fontSize: HairButtonRow.FontSize(styleCount));
            }

            // character_creator.gd's own HintLabel. Nothing about "drag the
            // body to paint, drag the space to spin it" is discoverable, and
            // that one sentence is the whole control scheme.
            MakeLabel(t, "HintLabel",
                "Drag the body to paint it.\nDrag anywhere else to turn it.\nScroll to zoom.",
                10, new Color(0.62f, 0.64f, 0.70f),
                At(-524.0f - (HairButtonRow.Rows(styleCount) - 1) * HairButtonRow.RowPitch), 40.0f);

            // Pinned to the bottom rather than following the stack, so it stays
            // put as tools are added above it -- ON ITS OWN OPAQUE BAR, because
            // the stack grew past it once already and the two ended up drawn
            // over each other.
            var bar = new GameObject("ActionBar", typeof(RectTransform));
            var barRect = (RectTransform)bar.transform;
            barRect.SetParent(t, false);
            barRect.anchorMin = new Vector2(0.0f, 0.0f);
            barRect.anchorMax = new Vector2(1.0f, 0.0f);
            barRect.pivot = new Vector2(0.5f, 0.0f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0.0f, ActionBarHeight);
            bar.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 1.0f);

            // Built AFTER the bar and therefore drawn over it, which is the
            // whole point -- built before it, they were the ones underneath.
            MakeAnchoredButton(t, "EnterCityButton", "ENTER THE CITY", Gold,
                new Vector2(0.0f, 52.0f), new Vector2(172.0f, 36.0f),
                ButtonAction.Kind.CreatorAskName, creator, title, overlays);
            MakeAnchoredButton(t, "BackButton", "BACK", Paper,
                new Vector2(0.0f, 16.0f), new Vector2(172.0f, 32.0f),
                ButtonAction.Kind.CreatorTryBack, creator, title, overlays);
        }

        /// <summary>Centred in the column, this far down from its top.</summary>
        private static Vector2 At(float y) => new Vector2(0.0f, y);

        /// <summary>
        /// Brush, bucket and eyedropper, as icons rather than the words BRUSH,
        /// FILL and PICK -- the Godot build drew these and then cleared the
        /// button text entirely, and this had quietly gone back to text.
        /// </summary>
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
                    ButtonAction.Kind.CreatorTool, creator, value: i);
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
                // Nothing should ever click the picture on top of a button --
                // it would eat the press meant for the button underneath.
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

        /// <summary>
        /// A plain Slider's flat fill would just be a bar sitting over half
        /// the rainbow -- the gradient itself, drawn once behind the handle,
        /// is what a hue slider actually shows, so the fill is left
        /// invisible and only the background carries the strip.
        /// </summary>
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

        /// <summary>
        /// A slider, built by hand rather than from DefaultControls so its
        /// hierarchy is the one written here and not one to reverse-engineer.
        /// </summary>
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
                // So the wheel and the preview follow a swatch press rather
                // than still showing whatever was picked before it.
                wiring.Controls = controls;
            }
        }

        // --- the two prompts on top ---

        /// <summary>
        /// ENTER THE CITY opens this rather than finishing directly, which is
        /// character_creator.gd's own flow: the character is only made once a
        /// name has actually been confirmed.
        /// </summary>
        private static void BuildNamePrompt(Transform parent, CreatorOverlays overlays)
        {
            GameObject dim = MakeDim(parent, "NamePrompt");
            RectTransform panel = MakePromptPanel(dim.transform, new Vector2(360.0f, 170.0f));

            MakeLabel(panel, "Title", "NAME YOUR CHARACTER", 16, Gold,
                new Vector2(0.0f, 34.0f), 24.0f);

            overlays.NameField = MakeField(panel, new Vector2(0.0f, -4.0f),
                new Vector2(260.0f, 34.0f));

            MakePromptButton(panel, "StartButton", "START", Gold,
                new Vector2(0.0f, -52.0f), new Vector2(150.0f, 36.0f),
                ButtonAction.Kind.CreatorConfirmName, overlays);

            overlays.NamePanel = dim;
            dim.SetActive(false);
        }

        /// <summary>
        /// Nothing is ever saved from the creator until a name is confirmed, so
        /// walking out after painting silently throws the work away. Having
        /// painted at all is what raises this.
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

        /// <summary>
        /// The full-screen darkener behind a prompt. It is a raycast target on
        /// purpose: it is what stops a click reaching a tool button behind it.
        /// </summary>
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

        private static InputField MakeField(Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject("NameField", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image background = go.AddComponent<Image>();
            background.color = new Color(0.16f, 0.17f, 0.21f, 1.0f);

            Text placeholder = Caption(go.transform, "Enter name...", 14,
                new Color(0.5f, 0.5f, 0.55f));
            placeholder.name = "Placeholder";
            Text typed = Caption(go.transform, string.Empty, 14, Paper);
            typed.name = "Text";
            typed.supportRichText = false;

            InputField field = go.AddComponent<InputField>();
            field.targetGraphic = background;
            field.textComponent = typed;
            field.placeholder = placeholder;
            field.characterLimit = CharacterSave.MaximumNameLength;
            field.lineType = InputField.LineType.SingleLine;
            return field;
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
            wiring.Title = overlays.Title;

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = tint;
            button.colors = colours;

            Caption(go.transform, label, 13, tint);
        }

        // --- small pieces ---

        /// <summary>
        /// Carries a ButtonAction, not a listener -- see that class. A listener
        /// added from an editor script is not serialized, so buttons wired that
        /// way are dead the moment the scene is saved and reopened.
        /// </summary>
        private static Image MakeButton(Transform parent, string name, string label, Color tint,
            Vector2 position, Vector2 size, ButtonAction.Kind action,
            CharacterCreator creator, TitleScreen title = null, int value = 0,
            CreatorOverlays overlays = null, int fontSize = 13)
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
            wiring.Title = title;
            wiring.Value = value;
            wiring.Overlays = overlays;

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = tint;
            // Disabled has to be visibly different, or a greyed-out UNDO looks
            // exactly like a live one that does nothing when pressed.
            colours.disabledColor = new Color(0.09f, 0.09f, 0.10f, 0.6f);
            button.colors = colours;

            if (!string.IsNullOrEmpty(label))
            {
                Caption(go.transform, label, fontSize, tint);
            }
            return background;
        }

        /// <summary>
        /// A button's own text, STRETCHED OVER THE WHOLE BUTTON.
        ///
        /// Not MakeLabel: that one hangs a label from the top of the tool
        /// column, and reusing it inside a button anchored the text to the
        /// button's top EDGE with the pivot in the middle of it -- so every
        /// caption on the panel was drawn half above the button it named.
        /// MIRROR, ERASER, ENTER THE CITY and BACK were all floating over
        /// their own boxes.
        /// </summary>
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
            CharacterCreator creator, TitleScreen title, CreatorOverlays overlays)
        {
            MakeButton(parent, name, label, tint, position, size, action, creator, title,
                overlays: overlays);
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

        /// <summary>
        /// The same label, centred on its parent rather than hung from the top
        /// of the column -- for the prompts, which are centred boxes.
        /// </summary>
        private static Text MakeLabel(RectTransform parent, string name, string content,
            int size, Color colour, Vector2 position, float height)
        {
            Text text = MakeLabel(parent.transform, name, content, size, colour,
                position, height);
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
    }
}
