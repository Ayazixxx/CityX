using CityX.Character;
using CityX.Core;
using CityX.UI;
using UnityEditor;
using UnityEngine;
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

        /// <summary>
        /// The palette down the side. The Godot creator had a full colour
        /// picker; these are the swatches plus the picker's job done by the
        /// eyedropper, which is the part that actually gets used.
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

            BuildTools(panel.transform, creator, title);
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
            // than swinging around its feet.
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
            Strip<CharacterController>(body);
            Strip<PlayerInputReader>(body);
            Strip<PlayerLocomotion>(body);
            Strip<MeleeAttacker>(body);
            Strip<Ragdoll>(body);
            Strip<CharacterIdentity>(body);
            foreach (Rigidbody rigid in body.GetComponentsInChildren<Rigidbody>())
            {
                Object.DestroyImmediate(rigid);
            }
            foreach (Collider collider in body.GetComponentsInChildren<Collider>())
            {
                Object.DestroyImmediate(collider);
            }

            var renderer = body.GetComponentInChildren<SkinnedMeshRenderer>();
            MeshCollider surface = null;
            if (renderer != null)
            {
                surface = renderer.gameObject.AddComponent<MeshCollider>();
                surface.sharedMesh = renderer.sharedMesh;
            }

            // ON ITS OWN LAYER, WHICH I SAID BEFORE AND DID NOT DO. Without a
            // culling mask this camera renders the whole scene, so the night
            // city came through behind the body and its own light lit the city
            // instead of the character -- you could not see what you were
            // painting. The layer is what makes the backdrop actually blank.
            int layer = EnsureLayer("CityXCreator");
            SetLayerRecursively(stage, layer);

            var cameraObject = new GameObject("CreatorCamera");
            cameraObject.transform.SetParent(stage.transform, false);
            // Centred on the pivot, which is at the body's middle, so the
            // character sits in the frame rather than floating at the top.
            cameraObject.transform.localPosition = new Vector3(0.0f, 0.0f, -3.2f);
            cameraObject.transform.localRotation = Quaternion.identity;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.14f, 0.17f);
            camera.fieldOfView = 42.0f;
            camera.nearClipPlane = 0.05f;
            camera.cullingMask = 1 << layer;
            // Above the title's own camera, so the creator draws over the city
            // rather than being buried in it.
            camera.depth = 5;
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

            stage.SetActive(false);
            return stage;
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

        private static void BuildTools(Transform parent, CharacterCreator creator,
            TitleScreen title)
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
            rect.offsetMax = new Vector2(190.0f, 0.0f);

            column.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.92f);

            float y = -30.0f;
            MakeLabel(column.transform, "Heading", "CREATE", 22, Gold, new Vector2(0.0f, y), 30.0f);

            y -= 44.0f;
            MakeToolRow(column.transform, creator, ref y);

            var controls = column.AddComponent<CreatorControls>();
            controls.Creator = creator;

            y -= 12.0f;
            MakeLabel(column.transform, "PaletteLabel", "COLOUR", 14, Paper,
                new Vector2(0.0f, y), 20.0f);

            // THE WHEEL, not just swatches. Any colour has to be reachable --
            // twelve presets is a palette, not a character creator.
            y -= 90.0f;
            controls.Wheel = MakeWheel(column.transform, new Vector2(0.0f, y), 160.0f);
            y -= 96.0f;

            controls.Value = MakeSlider(column.transform, "ValueSlider",
                new Vector2(0.0f, y), 0.0f, 1.0f, 1.0f, wholeNumbers: false);
            y -= 26.0f;
            controls.Preview = MakeSwatch(column.transform, "Preview",
                new Vector2(0.0f, y), new Vector2(150.0f, 18.0f), Color.white);

            y -= 26.0f;
            MakePalette(column.transform, creator, controls, ref y);

            y -= 16.0f;
            MakeLabel(column.transform, "BrushLabel", "BRUSH", 14, Paper,
                new Vector2(0.0f, y), 20.0f);
            y -= 24.0f;
            controls.BrushSize = MakeSlider(column.transform, "BrushSlider",
                new Vector2(0.0f, y), 1.0f, 15.0f,
                CharacterCreator.DefaultBrushRadius, wholeNumbers: true);
            y -= 22.0f;
            controls.BrushLabel = MakeLabel(column.transform, "BrushValue", "1", 13, Paper,
                new Vector2(0.0f, y), 18.0f);

            y -= 20.0f;
            MakeToggle(column.transform, "MirrorButton", "MIRROR", new Vector2(0.0f, y),
                ButtonAction.Kind.CreatorToggleMirror, creator);
            y -= 40.0f;
            MakeToggle(column.transform, "EraserButton", "ERASER", new Vector2(0.0f, y),
                ButtonAction.Kind.CreatorToggleEraser, creator);

            y -= 48.0f;
            MakeButton(column.transform, "UndoButton", "UNDO", Paper,
                new Vector2(-45.0f, y), new Vector2(80.0f, 34.0f),
                ButtonAction.Kind.CreatorUndo, creator);
            MakeButton(column.transform, "RedoButton", "REDO", Paper,
                new Vector2(45.0f, y), new Vector2(80.0f, 34.0f),
                ButtonAction.Kind.CreatorRedo, creator);

            // Pinned to the bottom rather than following the stack, so it stays
            // put as tools are added above it.
            MakeAnchoredButton(column.transform, "EnterCityButton", "ENTER THE CITY", Gold,
                new Vector2(0.0f, 96.0f), new Vector2(160.0f, 42.0f),
                ButtonAction.Kind.CreatorFinish, creator, title);
            MakeAnchoredButton(column.transform, "BackButton", "BACK", Paper,
                new Vector2(0.0f, 46.0f), new Vector2(160.0f, 36.0f),
                ButtonAction.Kind.ShowCharacterSelect, creator, title);
        }

        /// <summary>
        /// Hands the painted character off to the select screen, which owns
        /// naming and entering the world.
        /// </summary>
        private static void Finish(CharacterCreator creator, TitleScreen title)
        {
            if (title.Select != null)
            {
                title.Select.PendingPaint = creator.Paint.Encode();
                title.Show(TitleScreen.Panel.CharacterSelect);
                title.Select.BeginCreate();
            }
        }

        private static void MakeToolRow(Transform parent, CharacterCreator creator, ref float y)
        {
            string[] names = { "BRUSH", "FILL", "PICK" };
            var tools = new[]
            {
                CharacterCreator.Tool.Brush,
                CharacterCreator.Tool.Bucket,
                CharacterCreator.Tool.Eyedropper,
            };

            for (int i = 0; i < names.Length; i++)
            {
                CharacterCreator.Tool tool = tools[i];
                MakeButton(parent, names[i] + "Button", names[i], Gold,
                    new Vector2(i * 58.0f - 58.0f, y), new Vector2(54.0f, 32.0f),
                    ButtonAction.Kind.CreatorTool, creator, null, (int)tool);
            }
            y -= 40.0f;
        }

        private static RawImage MakeWheel(Transform parent, Vector2 position, float size)
        {
            var go = new GameObject("ColourWheel", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);

            var image = go.AddComponent<RawImage>();
            image.texture = CreatorControls.BuildWheel();
            return image;
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
            rect.sizeDelta = new Vector2(150.0f, 18.0f);

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
            handleRect.sizeDelta = new Vector2(12.0f, 22.0f);
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
            CreatorControls controls, ref float y)
        {
            const int perRow = 4;
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
                rect.anchoredPosition =
                    new Vector2(column * 30.0f - 45.0f, y - row * 30.0f);
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

            y -= Mathf.CeilToInt(Palette.Length / (float)perRow) * 30.0f;
        }


        // --- small pieces ---

        private static void MakeToggle(Transform parent, string name, string label,
            Vector2 position, ButtonAction.Kind action, CharacterCreator creator)
        {
            MakeButton(parent, name, label, Paper, position, new Vector2(150.0f, 32.0f),
                action, creator);
        }

        /// <summary>
        /// Carries a ButtonAction, not a listener -- see that class. A listener
        /// added from an editor script is not serialized, so buttons wired that
        /// way are dead the moment the scene is saved and reopened.
        /// </summary>
        private static void MakeButton(Transform parent, string name, string label, Color tint,
            Vector2 position, Vector2 size, ButtonAction.Kind action,
            CharacterCreator creator, TitleScreen title = null, int value = 0,
            Color colour = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image background = go.AddComponent<Image>();
            background.color = new Color(0.11f, 0.12f, 0.15f, 0.95f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;

            var wiring = go.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Creator = creator;
            wiring.Title = title;
            wiring.Value = value;
            wiring.Colour = colour;

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = tint;
            button.colors = colours;

            MakeLabel(go.transform, "Label", label, 13, tint, Vector2.zero, size.y - 6.0f);
        }

        private static void MakeAnchoredButton(Transform parent, string name, string label,
            Color tint, Vector2 position, Vector2 size, ButtonAction.Kind action,
            CharacterCreator creator, TitleScreen title = null)
        {
            MakeButton(parent, name, label, tint, position, size, action, creator, title);
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
            rect.sizeDelta = new Vector2(170.0f, height);

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = content;
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
