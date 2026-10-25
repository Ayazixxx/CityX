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
                MakeHairMatte(existing);
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
