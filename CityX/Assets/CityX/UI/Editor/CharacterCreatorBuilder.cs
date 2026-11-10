using CityX.Character;
using CityX.Core;
using CityX.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static CityX.UI.CreatorKit;

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
        public static GameObject Build(Transform parent, TitleScreen title, GameObject bodyPrefab)
        {
            var panel = new GameObject("CreatorPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            UiKit.Stretch(rect);

            // The 3D half lives outside the canvas -- a canvas cannot hold a
            // body -- and is switched on and off with the panel by a small
            // relay rather than by anything remembering to do it by hand.
            GameObject stage = BuildStage(bodyPrefab, out CharacterCreator creator);
            panel.AddComponent<CreatorStage>().Stage = stage;

            var overlays = panel.AddComponent<CreatorOverlays>();
            overlays.Creator = creator;
            overlays.Title = title;

            BuildToolColumn(panel.transform, creator, "CREATE", null, column =>
            {
                MakeAnchoredButton(column, "EnterCityButton", "ENTER THE CITY", Gold,
                    new Vector2(0.0f, 52.0f), new Vector2(172.0f, 36.0f),
                    ButtonAction.Kind.CreatorAskName, creator, title, overlays);
                MakeAnchoredButton(column, "BackButton", "BACK", Paper,
                    new Vector2(0.0f, 16.0f), new Vector2(172.0f, 32.0f),
                    ButtonAction.Kind.CreatorTryBack, creator, title, overlays);
            });
            BuildBelt(panel, stage, creator, title, overlays);
            BuildNamePrompt(panel.transform, overlays);
            BuildDiscardPrompt(panel.transform, overlays);
            return panel;
        }

        /// <summary>
        /// The conveyor belt's half of the creator (BeltIntro, docs/BELT_INTRO.md): the sequence itself on the panel, the prompt on the
        /// empty belt, and the button that lifts the body's arms to be painted. Under the two prompts, which cover everything.
        /// </summary>
        private static void BuildBelt(GameObject panel, GameObject stage, CharacterCreator creator, TitleScreen title, CreatorOverlays overlays)
        {
            var belt = panel.AddComponent<BeltIntro>();
            belt.Creator = creator;
            belt.Life = stage.GetComponent<CreatorLife>();
            belt.Belt = stage.GetComponentInChildren<ConveyorBelt>(true);
            belt.Hum = belt.Belt.GetComponent<AudioSource>();
            belt.Rider = creator.RotationPivot;
            belt.MakingCamera = creator.Camera;
            belt.WideCamera = stage.transform.Find(WideCameraName).GetComponent<Camera>();
            belt.Ragdoll = stage.GetComponentInChildren<Ragdoll>(true);
            belt.Tools = panel.transform.Find("Tools").gameObject.AddComponent<CanvasGroup>();
            belt.Overlays = overlays;
            belt.Title = title;

            Text prompt = UiKit.MakeText(panel.transform, "BeltPrompt", BeltIntro.PromptText, 30, new Color(0.09f, 0.13f, 0.22f));   // dark, on the pale sky under the belt
            var promptRect = (RectTransform)prompt.transform;
            promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.0f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);
            promptRect.anchoredPosition = new Vector2(0.0f, 72.0f);   // in the open sky under the belt
            promptRect.sizeDelta = new Vector2(520.0f, 48.0f);
            prompt.alignment = TextAnchor.MiddleCenter;
            prompt.fontStyle = FontStyle.Bold;
            prompt.gameObject.SetActive(false);
            belt.Prompt = prompt;

            // Built here and not by CreatorKit: the belt's body is the only one with a pose to toggle, and BeltIntro lights the icon.
            var pose = new GameObject("PoseButton", typeof(RectTransform));
            var poseRect = (RectTransform)pose.transform;
            poseRect.SetParent(panel.transform, false);
            poseRect.anchorMin = poseRect.anchorMax = Vector2.one;
            poseRect.pivot = Vector2.one;
            poseRect.anchoredPosition = new Vector2(-16.0f, -16.0f);
            poseRect.sizeDelta = new Vector2(PoseButtonSize, PoseButtonSize);
            Image background = pose.AddComponent<Image>();
            background.color = Idle;
            Button button = pose.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = Gold;
            button.colors = colours;
            pose.AddComponent<CanvasGroup>();
            var wiring = pose.AddComponent<ButtonAction>();
            wiring.Action = ButtonAction.Kind.CreatorTogglePose;
            wiring.Creator = creator;
            wiring.Title = title;
            belt.PoseButton = button;
            belt.PoseIcon = new Graphic[]
            {
                PoseIconPart(pose.transform, "Head", new Vector2(0.0f, 15.0f), new Vector2(11.0f, 11.0f), CircleSprite.Ensure()),
                PoseIconPart(pose.transform, "Arms", new Vector2(0.0f, 6.0f), new Vector2(36.0f, 4.0f), null),
                PoseIconPart(pose.transform, "Body", new Vector2(0.0f, -2.0f), new Vector2(8.0f, 20.0f), null),
                PoseIconPart(pose.transform, "LeftLeg", new Vector2(-2.5f, -14.0f), new Vector2(3.5f, 14.0f), null),
                PoseIconPart(pose.transform, "RightLeg", new Vector2(2.5f, -14.0f), new Vector2(3.5f, 14.0f), null),
            };
            ClearTheColumn(belt.Tools.transform);
        }

        /// <summary>One piece of the T-posing figure on the pose button, out of plain shapes. Clicks go to the button under it.</summary>
        private static Graphic PoseIconPart(Transform button, string name, Vector2 at, Vector2 size, Sprite sprite)
        {
            var part = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)part.transform;
            rect.SetParent(button, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            Image image = part.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Paper;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Only the buttons and the tools over the belt and the sky (owner, 2026-11-10: "get rid of this transparent rectangle behind the
        /// tools panel. just make it only the buttons and tools"). Clear rather than gone: a stroke must still not start between two
        /// buttons. With nothing behind them the words outside the buttons take a dark edge, to read on the sky and on the belt alike.
        /// </summary>
        private static void ClearTheColumn(Transform tools)
        {
            tools.GetComponent<Image>().color = Color.clear;
            tools.Find("ActionBar").GetComponent<Image>().color = Color.clear;
            foreach (Image panel in tools.GetComponentsInChildren<Image>(true))
            {
                bool seeThroughBackdrop = panel.color.a < 1.0f && panel.sprite == null && panel.GetComponent<Mask>() == null;
                if (seeThroughBackdrop && panel.GetComponentInParent<Selectable>(true) == null)
                {
                    panel.color = Color.clear;   // the FACE page's empty grid
                }
            }
            foreach (Text words in tools.GetComponentsInChildren<Text>(true))
            {
                if (words.GetComponentInParent<Selectable>(true) == null)
                {
                    if (words.color != Gold)
                    {
                        words.color = Color.white;
                    }
                    Outline edge = words.gameObject.AddComponent<Outline>();
                    edge.effectColor = new Color(0.03f, 0.04f, 0.07f, 1.0f);
                    edge.effectDistance = new Vector2(0.7f, -0.7f);
                }
            }
        }

        private const string WideCameraName = "WideCamera";
        private const string BeltLoopPath = "Assets/CityX/UI/Audio/conveyor_belt_loop.ogg";
        private const float PoseButtonSize = 56.0f;

        /// <summary>
        /// Everything physical comes off the body but its ragdoll, which it keeps whole: at the end of the belt it is let go and falls
        /// (BeltIntro). The Ragdoll holds every bone still until then.
        ///
        /// JOINTS BEFORE BODIES. A CharacterJoint depends on the Rigidbody on its own object, and Unity refuses to remove a component
        /// another one still needs, with a console line and not an exception.
        /// </summary>
        private static void KeepOnlyTheRagdoll(GameObject body)
        {
            var ragdoll = body.GetComponentInChildren<Ragdoll>(true);
            var kept = new System.Collections.Generic.HashSet<GameObject>();
            if (ragdoll != null && ragdoll.Bodies != null)
            {
                foreach (Rigidbody part in ragdoll.Bodies)
                {
                    if (part != null)
                    {
                        kept.Add(part.gameObject);
                    }
                }
            }
            if (kept.Count == 0)
            {
                Debug.LogError("CityX: the creator's body has no ragdoll -- it cannot be dropped off the belt's end.");
            }
            foreach (Joint joint in body.GetComponentsInChildren<Joint>(true))
            {
                if (!kept.Contains(joint.gameObject)) Object.DestroyImmediate(joint);
            }
            foreach (Rigidbody rigid in body.GetComponentsInChildren<Rigidbody>(true))
            {
                if (!kept.Contains(rigid.gameObject)) Object.DestroyImmediate(rigid);
            }
            foreach (Collider collider in body.GetComponentsInChildren<Collider>(true))
            {
                if (!kept.Contains(collider.gameObject)) Object.DestroyImmediate(collider);
            }
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
            Strip<CharacterIdentity>(body);
            Strip<PlayerVitals>(body);

            KeepOnlyTheRagdoll(body);

            // THE BODY WAKES HOLDING STILL: the mesh collider below is the
            // surface as it woke, and the brush's atlas is bound to it. Once it
            // idles (CreatorLife) the ray finds it as it stands instead
            // (CharacterCreator.LivePaint).
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
            // The sky the belt stands in (BeltSky), should anything show past its wall.
            camera.backgroundColor = BeltSky.Middle;
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
            // The title's night air is laid over every camera's picture: not over this one's daylight sky.
            cameraObject.AddComponent<CityX.View.ClearAirCamera>();

            // THREE LIGHTS, because one directional light on a matte body gives
            // a black side, and half of painting is on the side facing away.
            AddLight(stage, layer, "Key", new Vector3(22.0f, 150.0f, 0.0f), 1.5f, Color.white);
            AddLight(stage, layer, "Fill", new Vector3(10.0f, -40.0f, 0.0f), 0.9f,
                new Color(0.82f, 0.86f, 1.0f));
            AddLight(stage, layer, "Back", new Vector3(-25.0f, 20.0f, 0.0f), 0.6f,
                new Color(1.0f, 0.95f, 0.85f));

            // The key light throws the body's shadow on the belt, or it floats over it.
            stage.transform.Find("KeyLight").GetComponent<Light>().shadows = LightShadows.Soft;

            // The conveyor belt the body stands on and the sky it stands in (BeltIntro, docs/BELT_INTRO.md). Both build themselves when
            // they wake, on this layer.
            var belt = new GameObject("Belt") { layer = layer };
            belt.transform.SetParent(stage.transform, false);
            belt.AddComponent<ConveyorBelt>();
            // Its running sound: the steady part of the owner's recording as a loop (SourceArt/BeltIntro/belt_loop.py). BeltIntro plays it.
            AudioSource hum = belt.AddComponent<AudioSource>();
            hum.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BeltLoopPath);
            hum.loop = true;
            hum.playOnAwake = false;
            hum.spatialBlend = 0.0f;
            hum.volume = 0.0f;
            if (hum.clip == null)
            {
                Debug.LogWarning("CityX: no conveyor belt sound at " + BeltLoopPath + ".");
            }
            var sky =new GameObject("Sky") { layer = layer };
            sky.transform.SetParent(stage.transform, false);
            sky.AddComponent<BeltSky>();

            // The view from further back that shows the belt's end, for the body's last ride and its fall. BeltIntro places it.
            var wideObject = new GameObject(WideCameraName) { layer = layer };
            wideObject.transform.SetParent(stage.transform, false);
            Camera wide = wideObject.AddComponent<Camera>();
            wide.clearFlags = CameraClearFlags.SolidColor;
            wide.backgroundColor = BeltSky.Middle;
            wide.fieldOfView = BeltIntro.WideFieldOfView;
            wide.nearClipPlane = 0.1f;
            wide.cullingMask = 1 << layer;
            wide.depth = 5;
            wide.allowMSAA = true;
            wide.enabled = false;
            var wideData = wideObject.AddComponent<UniversalAdditionalCameraData>();
            wideData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            wideData.antialiasingQuality = AntialiasingQuality.High;
            wideObject.AddComponent<CityX.View.ClearAirCamera>();

            var life = stage.AddComponent<CreatorLife>();
            life.Animator = body.GetComponentInChildren<Animator>(true);

            creator = stage.AddComponent<CharacterCreator>();
            creator.KeepsRagdoll = true;
            life.Creator = creator;
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
            Transform head = TransformSearch.FindDeep(body.transform, "head");
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
                CityX.Core.LitShader.MakeMatte(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = CityX.Core.LitShader.Find()
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
            CityX.Core.LitShader.MakeMatte(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
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

        private static void Strip<T>(GameObject root) where T : Component
        {
            foreach (T found in root.GetComponentsInChildren<T>(includeInactive: true))
            {
                Object.DestroyImmediate(found);
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
    }
}
