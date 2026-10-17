using System.Collections.Generic;
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
    /// Builds the character select panel onto the title screen's canvas.
    ///
    /// A CAROUSEL, NOT A SCROLLING LIST -- saved characters stand on
    /// pedestals in a row with the chosen one centred, cycled with the
    /// arrows either side. The 3D half is built exactly the way
    /// CharacterCreatorBuilder builds its own single preview body (own
    /// layer, own camera, own lights, drawn over the title screen by
    /// camera depth rather than baked to a RenderTexture) -- see that
    /// file's own doc comment for why a RenderTexture was never the
    /// pattern here. This just needs up to three of that same kind of body
    /// on screen at once instead of one.
    /// </summary>
    public static class CharacterSelectBuilder
    {
        private static readonly Color Gold = new Color(0.75f, 0.6f, 0.2f, 1.0f);
        private static readonly Color Paper = new Color(0.82f, 0.82f, 0.84f, 1.0f);
        private static readonly Color Danger = new Color(0.55f, 0.16f, 0.16f, 1.0f);

        /// <summary>How far the side pedestals sit from the centre one, in metres.</summary>
        private const float PedestalSpacing = 2.4f;

        /// <summary>
        /// The skins a new character can be. Deliberately a short list of
        /// distinguishable colours rather than a colour wheel: the Godot build
        /// let you paint the whole body in its creator, and offering a wheel
        /// here would look like that feature rather than admitting it is a
        /// stand-in until the creator is ported.
        /// </summary>
        private static readonly Color[] Skins =
        {
            new Color(0.78f, 0.62f, 0.53f),
            new Color(0.55f, 0.40f, 0.32f),
            new Color(0.35f, 0.25f, 0.20f),
            new Color(0.86f, 0.76f, 0.68f),
            new Color(0.44f, 0.52f, 0.58f),
            new Color(0.62f, 0.30f, 0.30f),
        };

        public static GameObject Build(Transform parent, TitleScreen title, GameObject bodyPrefab)
        {
            var panel = new GameObject("CharacterSelectPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            Stretch(rect, 0.0f);

            CharacterSelect select = panel.AddComponent<CharacterSelect>();
            title.Select = select;

            // The 3D half lives outside the canvas, same reason and same
            // relay CharacterCreatorBuilder already uses for its own body --
            // a canvas cannot hold one.
            GameObject stage = BuildCarouselStage(bodyPrefab, select);
            panel.AddComponent<CreatorStage>().Stage = stage;

            select.ListPanel = BuildListUI(panel.transform, select, title);
            select.CreatePanel = BuildCreate(panel.transform, select, title);
            select.ConfirmPanel = BuildConfirm(panel.transform, select);

            select.Show(CharacterSelect.Mode.List);
            return panel;
        }

        // --- the carousel's 3D half ---

        /// <summary>
        /// The pedestals, the bodies' anchors, the camera and the lights.
        ///
        /// SAME LAYER THE CREATOR ALREADY OWNS, deliberately reused rather
        /// than claiming a second one: TitleScreen.Panel shows exactly one
        /// panel at a time, so the creator's stage and this one are never
        /// both active together, and reusing "CityXCreator" means this does
        /// not spend another of the project's own limited layer slots on a
        /// backdrop that is functionally the same kind of thing (a body,
        /// lit, on nothing else).
        /// </summary>
        private static GameObject BuildCarouselStage(GameObject bodyPrefab, CharacterSelect select)
        {
            var stage = new GameObject("SelectStage");
            int layer = EnsureLayer("CityXCreator");
            stage.layer = layer;

            select.CarouselBodyPrefab = bodyPrefab;
            LoadHair(select);

            var anchors = new Transform[3];
            string[] names = { "PedestalLeft", "PedestalCentre", "PedestalRight" };
            float[] xPositions = { -PedestalSpacing, 0.0f, PedestalSpacing };
            // The centre pedestal reads as chosen by standing a little proud
            // of its neighbours and under its own brighter light, rather
            // than by a UI frame around it -- the body IS the selection.
            float[] platformHeights = { 0.18f, 0.30f, 0.18f };
            float[] platformScales = { 0.78f, 1.0f, 0.78f };

            Material platformMaterial = PedestalMaterial();

            for (int i = 0; i < 3; i++)
            {
                var anchorObject = new GameObject(names[i]);
                anchorObject.transform.SetParent(stage.transform, false);
                anchorObject.transform.localPosition =
                    new Vector3(xPositions[i], platformHeights[i], 0.0f);
                anchorObject.transform.localScale = Vector3.one * platformScales[i];
                anchors[i] = anchorObject.transform;

                GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                platform.name = "Platform";
                Object.DestroyImmediate(platform.GetComponent<Collider>());
                platform.transform.SetParent(stage.transform, false);
                platform.transform.localPosition =
                    new Vector3(xPositions[i], platformHeights[i] * 0.5f, 0.0f);
                platform.transform.localScale =
                    new Vector3(0.9f * platformScales[i], platformHeights[i] * 0.5f, 0.9f * platformScales[i]);
                platform.GetComponent<MeshRenderer>().sharedMaterial = platformMaterial;
            }

            select.PedestalAnchors = anchors;

            CreatorKit.SetLayerRecursively(stage, layer);

            var cameraObject = new GameObject("SelectCamera");
            cameraObject.transform.SetParent(stage.transform, false);
            // Pulled back and widened from the creator's own single-body
            // framing (0, 1.6, 4 at 75 degrees) -- three bodies spaced
            // PedestalSpacing apart need enough width in frame to clear all
            // three, not just the one at the centre.
            cameraObject.transform.localPosition = new Vector3(0.0f, 1.7f, 7.5f);
            cameraObject.transform.localRotation = Quaternion.Euler(6.0f, 180.0f, 0.0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Depth;
            camera.fieldOfView = 50.0f;
            camera.nearClipPlane = 0.05f;
            camera.cullingMask = 1 << layer;
            camera.depth = 5;
            camera.allowMSAA = true;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.layer = layer;

            // AN OVERLAY ON THE TITLE'S OWN CAMERA, NOT A SECOND BASE CAMERA. The first version
            // was a base camera with "depth only" clearing so the city would stay behind the
            // characters -- and in URP a second base camera cannot keep what the first drew: it
            // renders into a fresh target that is cleared to the camera's default background (that
            // stock blue) and the city vanished, on the character list only. An overlay camera is
            // the supported way to draw one camera's things over another's picture. It takes its
            // anti-aliasing from the base camera it is stacked on; the stack is stored on the base.
            // The list skips a camera that is switched off, so this costs nothing while the stage
            // is hidden.
            // (Whether it clears depth is read off the camera's own clear flags, set above.)
            cameraData.renderType = CameraRenderType.Overlay;

            Camera titleCamera = Camera.main;
            if (titleCamera != null)
            {
                UniversalAdditionalCameraData titleData = titleCamera.GetUniversalAdditionalCameraData();
                if (!titleData.cameraStack.Contains(camera))
                {
                    titleData.cameraStack.Add(camera);
                }
            }
            else
            {
                Debug.LogWarning("CityX: no main camera to stack the character list's camera on -- " +
                                 "the characters will not draw.");
            }

            CreatorKit.AddLight(stage, layer, "Key", new Vector3(22.0f, 150.0f, 0.0f), 1.4f, Color.white);
            CreatorKit.AddLight(stage, layer, "Fill", new Vector3(10.0f, -40.0f, 0.0f), 0.8f,
                new Color(0.82f, 0.86f, 1.0f));
            CreatorKit.AddLight(stage, layer, "Back", new Vector3(-25.0f, 20.0f, 0.0f), 0.5f,
                new Color(1.0f, 0.95f, 0.85f));

            // The one light that says "this is the chosen one" -- a downward
            // spot only the centre pedestal sits in.
            var spotObject = new GameObject("SelectionSpot");
            spotObject.transform.SetParent(stage.transform, false);
            spotObject.transform.localPosition = new Vector3(0.0f, 4.0f, 0.5f);
            spotObject.transform.localRotation = Quaternion.Euler(75.0f, 0.0f, 0.0f);
            spotObject.layer = layer;
            Light spot = spotObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = Gold;
            spot.intensity = 3.0f;
            spot.range = 8.0f;
            spot.spotAngle = 45.0f;
            spot.cullingMask = 1 << layer;
            spot.shadows = LightShadows.None;

            stage.SetActive(false);
            return stage;
        }

        /// <summary>
        /// One shared, saved material for every pedestal -- an in-memory
        /// one assigned straight to a renderer does not reliably survive
        /// the scene being saved (see PistolBuilder's own fix for the
        /// identical failure on the muzzle sparks, which came back with a
        /// null material and rendered the reported bright pink/purple).
        /// </summary>
        private static Material PedestalMaterial()
        {
            const string path = "Assets/CityX/UI/SelectPedestal.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            var dark = new Color(0.10f, 0.11f, 0.14f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", dark);
            if (material.HasProperty("_Color")) material.SetColor("_Color", dark);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.6f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.5f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// The hair mesh/material CharacterSelect needs to show a saved
        /// character's chosen style -- loaded here, once, because runtime
        /// code has no AssetDatabase to load them with itself. Same asset
        /// CharacterCreatorBuilder.AddHair wires for the creator's own body.
        /// </summary>
        private static void LoadHair(CharacterSelect select)
        {
            select.HairStyleMeshes = HairAssets.StyleMeshes();

            const string materialPath = "Assets/CityX/Character/Materials/Hair.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", CharacterHair.HairColour);
                if (material.HasProperty("_Color")) material.SetColor("_Color", CharacterHair.HairColour);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.4f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            select.HairMaterialAsset = material;
        }

        /// <summary>See CharacterCreatorBuilder's own identical method -- kept as its own copy rather than shared, since the two builders otherwise depend on nothing of each other's.</summary>
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
            Debug.LogWarning($"CityX: no free layer for '{name}' -- the select screen will render " +
                             "the whole scene behind the pedestals.");
            return 0;
        }

        // --- the carousel's UI half ---

        private static GameObject BuildListUI(Transform parent, CharacterSelect select,
            TitleScreen title)
        {
            var panel = new GameObject("ListPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            Stretch(rect, 0.0f);

            MakeLabel(panel.transform, "Heading", "SELECT YOUR CHARACTER", 28, Gold,
                Anchor(0.5f, 1.0f), new Vector2(0.0f, -44.0f), new Vector2(600.0f, 40.0f));

            // Wide, so a press lands on the arrow rather than needing to be
            // aimed at the very edge of the screen.
            MakeArrowButton(panel.transform, "PreviousButton", "<", Anchor(0.0f, 0.5f),
                new Vector2(48.0f, 0.0f), ButtonAction.Kind.SelectPrevious, select);
            MakeArrowButton(panel.transform, "NextButton", ">", Anchor(1.0f, 0.5f),
                new Vector2(-48.0f, 0.0f), ButtonAction.Kind.SelectNext, select);

            select.IndexLabel = MakeLabel(panel.transform, "IndexLabel", "1 / 1", 14,
                new Color(0.6f, 0.6f, 0.64f),
                Anchor(0.5f, 1.0f), new Vector2(0.0f, -78.0f), new Vector2(200.0f, 24.0f));

            select.EmptyNotice = MakeLabel(panel.transform, "EmptyNotice",
                "No characters yet -- make one below.", 18, Paper,
                Anchor(0.5f, 0.42f), new Vector2(0.0f, 0.0f), new Vector2(460.0f, 30.0f));

            // The stat card, bottom centre, standing in front of the
            // pedestals rather than beside them -- the reference this was
            // asked to follow puts the chosen character's own details in
            // exactly that spot.
            GameObject card = MakeRect("StatCard", panel.transform,
                new Color(0.05f, 0.06f, 0.08f, 0.90f));
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.0f);
            cardRect.pivot = new Vector2(0.5f, 0.0f);
            cardRect.anchoredPosition = new Vector2(0.0f, 118.0f);
            cardRect.sizeDelta = new Vector2(380.0f, 76.0f);

            select.NameLabel = MakeLabel(card.transform, "Name", "Name", 22, Paper,
                Anchor(0.0f, 1.0f), new Vector2(96.0f, -18.0f), new Vector2(270.0f, 28.0f));
            select.NameLabel.alignment = TextAnchor.MiddleLeft;

            select.DetailLabel = MakeLabel(card.transform, "Detail", "Level 1   0m", 14,
                new Color(0.62f, 0.62f, 0.66f),
                Anchor(0.0f, 1.0f), new Vector2(96.0f, -40.0f), new Vector2(270.0f, 20.0f));
            select.DetailLabel.alignment = TextAnchor.MiddleLeft;

            select.StatsLabel = MakeLabel(card.transform, "Stats", "VIT 0   END 0   STR 0", 13,
                new Color(0.62f, 0.62f, 0.66f),
                Anchor(0.0f, 1.0f), new Vector2(96.0f, -58.0f), new Vector2(270.0f, 20.0f));
            select.StatsLabel.alignment = TextAnchor.MiddleLeft;

            MakeButton(panel.transform, "PlayButton", "PLAY", Gold,
                Anchor(0.5f, 0.0f), new Vector2(-160.0f, 58.0f), new Vector2(140.0f, 44.0f),
                ButtonAction.Kind.SelectPlaySelected, title, select);
            MakeButton(panel.transform, "DeleteButton", "DELETE", Danger,
                Anchor(0.5f, 0.0f), new Vector2(0.0f, 58.0f), new Vector2(140.0f, 44.0f),
                ButtonAction.Kind.SelectBeginDeleteSelected, title, select);
            // Into the CREATOR, not straight to the name box. Painting comes
            // first and naming last, which is the order the Godot build used
            // and the order that makes sense: you name the thing you made.
            MakeButton(panel.transform, "NewButton", "NEW CHARACTER", Paper,
                Anchor(0.5f, 0.0f), new Vector2(160.0f, 58.0f), new Vector2(160.0f, 44.0f),
                ButtonAction.Kind.ShowCreator, title, select);

            MakeButton(panel.transform, "BackButton", "BACK", Paper,
                Anchor(0.0f, 0.0f), new Vector2(90.0f, 30.0f), new Vector2(120.0f, 38.0f),
                ButtonAction.Kind.TitleBack, title, select);

            return panel;
        }

        /// <summary>A big, plain chevron button rather than MakeButton's boxed style -- it sits over the 3D stage, not on a panel background.</summary>
        private static void MakeArrowButton(Transform parent, string name, string glyph,
            Vector2 anchor, Vector2 position, ButtonAction.Kind action, CharacterSelect select)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(64.0f, 64.0f);

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.09f, 0.10f, 0.13f, 0.75f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = Gold;
            button.colors = colours;

            var wiring = buttonObject.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Select = select;

            MakeLabel(buttonObject.transform, "Glyph", glyph, 30, Gold,
                Anchor(0.5f, 0.5f), Vector2.zero, new Vector2(60.0f, 60.0f));
        }

        // --- making one ---

        private static GameObject BuildCreate(Transform parent, CharacterSelect select,
            TitleScreen title)
        {
            var panel = new GameObject("CreatePanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            Stretch(rect, 0.0f);

            GameObject wash = MakeRect("Wash", panel.transform,
                new Color(0.0f, 0.0f, 0.0f, 0.72f));
            Stretch((RectTransform)wash.transform, 0.0f);
            wash.GetComponent<Image>().raycastTarget = false;

            MakeLabel(panel.transform, "Heading", "NEW CHARACTER", 26, Gold,
                Anchor(0.5f, 0.5f), new Vector2(0.0f, 130.0f), new Vector2(420.0f, 36.0f));

            select.NameField = MakeNameField(panel.transform);

            MakeLabel(panel.transform, "SkinLabel", "SKIN", 16, Paper,
                Anchor(0.5f, 0.5f), new Vector2(0.0f, 20.0f), new Vector2(420.0f, 24.0f));

            var swatches = new List<Image>();
            for (int i = 0; i < Skins.Length; i++)
            {
                int index = i;
                float span = (Skins.Length - 1) * 52.0f;
                var swatchObject = new GameObject($"Swatch{i}", typeof(RectTransform));
                var swatchRect = (RectTransform)swatchObject.transform;
                swatchRect.SetParent(panel.transform, false);
                swatchRect.anchorMin = swatchRect.anchorMax = new Vector2(0.5f, 0.5f);
                swatchRect.pivot = new Vector2(0.5f, 0.5f);
                swatchRect.anchoredPosition = new Vector2(i * 52.0f - span * 0.5f, -18.0f);
                swatchRect.sizeDelta = new Vector2(42.0f, 42.0f);

                Image image = swatchObject.AddComponent<Image>();
                image.color = Skins[i];

                var outline = swatchObject.AddComponent<Outline>();
                outline.effectColor = Gold;
                outline.effectDistance = new Vector2(3.0f, -3.0f);
                outline.enabled = i == 0;

                Button button = swatchObject.AddComponent<Button>();
                button.targetGraphic = image;
                var wiring = swatchObject.AddComponent<ButtonAction>();
                wiring.Action = ButtonAction.Kind.SelectChooseSkin;
                wiring.Select = select;
                wiring.Value = index;

                swatches.Add(image);
            }
            select.Swatches = swatches.ToArray();

            MakeButton(panel.transform, "CreateButton", "ENTER THE CITY", Gold,
                Anchor(0.5f, 0.5f), new Vector2(-80.0f, -90.0f), new Vector2(220.0f, 44.0f),
                ButtonAction.Kind.SelectConfirmCreate, title, select);
            MakeButton(panel.transform, "CancelButton", "CANCEL", Paper,
                Anchor(0.5f, 0.5f), new Vector2(140.0f, -90.0f), new Vector2(140.0f, 44.0f),
                ButtonAction.Kind.TitleBack, title, select);

            return panel;
        }

        /// <summary>
        /// The name box. Built by hand rather than from Unity's own
        /// DefaultControls, whose generated hierarchy is what a later reader has
        /// to reverse-engineer when a field stops accepting text.
        /// </summary>
        private static InputField MakeNameField(Transform parent)
        {
            var fieldObject = new GameObject("NameField", typeof(RectTransform));
            var rect = (RectTransform)fieldObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0.0f, 72.0f);
            rect.sizeDelta = new Vector2(360.0f, 44.0f);

            Image background = fieldObject.AddComponent<Image>();
            background.color = new Color(0.05f, 0.06f, 0.08f, 0.95f);

            Text text = MakeLabel(fieldObject.transform, "Text", string.Empty, 20, Paper,
                Anchor(0.5f, 0.5f), Vector2.zero, new Vector2(340.0f, 36.0f));
            text.supportRichText = false;

            Text placeholder = MakeLabel(fieldObject.transform, "Placeholder", "Name...", 20,
                new Color(0.45f, 0.45f, 0.48f),
                Anchor(0.5f, 0.5f), Vector2.zero, new Vector2(340.0f, 36.0f));
            placeholder.fontStyle = FontStyle.Italic;

            InputField field = fieldObject.AddComponent<InputField>();
            field.targetGraphic = background;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = CharacterSave.MaximumNameLength;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        // --- deleting one ---

        private static GameObject BuildConfirm(Transform parent, CharacterSelect select)
        {
            var panel = new GameObject("ConfirmPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            Stretch(rect, 0.0f);

            GameObject wash = MakeRect("Wash", panel.transform,
                new Color(0.0f, 0.0f, 0.0f, 0.78f));
            Stretch((RectTransform)wash.transform, 0.0f);
            wash.GetComponent<Image>().raycastTarget = false;

            select.ConfirmText = MakeLabel(panel.transform, "ConfirmText",
                "Delete this character?", 20, Paper,
                Anchor(0.5f, 0.5f), new Vector2(0.0f, 40.0f), new Vector2(460.0f, 70.0f));

            // Delete is on the RIGHT and cancel on the left, so the destructive
            // one is not where the eye lands first or where a repeated click
            // from the previous screen would fall.
            MakeButton(panel.transform, "CancelButton", "KEEP", Paper,
                Anchor(0.5f, 0.5f), new Vector2(-100.0f, -40.0f), new Vector2(160.0f, 44.0f),
                ButtonAction.Kind.SelectCancelDelete, null, select);
            MakeButton(panel.transform, "DeleteButton", "DELETE", Danger,
                Anchor(0.5f, 0.5f), new Vector2(100.0f, -40.0f), new Vector2(160.0f, 44.0f),
                ButtonAction.Kind.SelectConfirmDelete, null, select);

            return panel;
        }

        // --- shared pieces ---

        private static Vector2 Anchor(float x, float y)
        {
            return new Vector2(x, y);
        }

        /// <summary>
        /// Buttons carry a ButtonAction rather than a listener -- see that
        /// class. A listener added from an editor script is not serialized, so
        /// every one of these was dead once the scene had been saved.
        /// </summary>
        private static void MakeButton(Transform parent, string name, string label, Color tint,
            Vector2 anchor, Vector2 position, Vector2 dimensions,
            ButtonAction.Kind action, TitleScreen title, CharacterSelect select, int value = 0)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.09f, 0.10f, 0.13f, 0.92f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;

            var wiring = buttonObject.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Title = title;
            wiring.Select = select;
            wiring.Value = value;

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = tint;
            button.colors = colours;

            MakeLabel(buttonObject.transform, "Label", label, 16, tint,
                Anchor(0.5f, 0.5f), Vector2.zero,
                new Vector2(dimensions.x - 10.0f, dimensions.y - 8.0f));
        }

        private static Text MakeLabel(Transform parent, string name, string content, int size,
            Color colour, Vector2 anchor, Vector2 position, Vector2 dimensions)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            Text text = labelObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static GameObject MakeRect(string name, Transform parent, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = colour;
            return go;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
