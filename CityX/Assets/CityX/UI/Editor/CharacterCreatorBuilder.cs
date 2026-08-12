using CityX.Character;
using CityX.Core;
using CityX.UI;
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

            var cameraObject = new GameObject("CreatorCamera");
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.transform.localPosition =
                new Vector3(0.0f, 0.0f, -4.0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
            camera.fieldOfView = 34.0f;
            camera.nearClipPlane = 0.05f;
            // Above the title's own camera, so the creator draws over the city
            // rather than being buried in it.
            camera.depth = 5;

            var light = new GameObject("CreatorLight");
            light.transform.SetParent(stage.transform, false);
            Light key = light.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(28.0f, 150.0f, 0.0f);

            creator = stage.AddComponent<CharacterCreator>();
            creator.RotationPivot = pivotObject.transform;
            creator.Camera = camera;
            creator.BodyCollider = surface;
            creator.BodyRenderer = renderer;

            stage.SetActive(false);
            return stage;
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

            y -= 12.0f;
            MakeLabel(column.transform, "PaletteLabel", "COLOUR", 14, Paper,
                new Vector2(0.0f, y), 20.0f);
            y -= 26.0f;
            MakePalette(column.transform, creator, ref y);

            y -= 16.0f;
            MakeLabel(column.transform, "BrushLabel", "BRUSH", 14, Paper,
                new Vector2(0.0f, y), 20.0f);
            y -= 28.0f;
            MakeBrushSizes(column.transform, creator, ref y);

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

        private static void MakePalette(Transform parent, CharacterCreator creator, ref float y)
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
                    new Vector2(column * 40.0f - 60.0f, y - row * 40.0f);
                rect.sizeDelta = new Vector2(34.0f, 34.0f);

                Image image = go.AddComponent<Image>();
                image.color = swatch;

                Button button = go.AddComponent<Button>();
                button.targetGraphic = image;

                var wiring = go.AddComponent<ButtonAction>();
                wiring.Action = ButtonAction.Kind.CreatorColour;
                wiring.Creator = creator;
                wiring.Colour = swatch;
            }

            y -= Mathf.CeilToInt(Palette.Length / (float)perRow) * 40.0f;
        }

        private static void MakeBrushSizes(Transform parent, CharacterCreator creator, ref float y)
        {
            int[] sizes = { 1, 2, 4, 8, 15 };
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                MakeButton(parent, $"Brush{size}", size.ToString(), Paper,
                    new Vector2(i * 34.0f - 68.0f, y), new Vector2(30.0f, 28.0f),
                    ButtonAction.Kind.CreatorBrushSize, creator, null, size);
            }
            y -= 34.0f;
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
