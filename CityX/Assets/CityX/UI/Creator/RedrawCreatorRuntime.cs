using CityX.Character;
using CityX.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static CityX.UI.CreatorKit;

namespace CityX.UI
{
    /// <summary>
    /// Builds ReDraw's own character creator overlay -- AT RUNTIME, during
    /// play. The tool column, the prompts and the stage's lights come from
    /// CreatorKit, which the title screen's creator
    /// (CityX.UIEditor.CharacterCreatorBuilder, baked in the editor) is built
    /// from too, so the two cannot drift apart. What is here is only what has
    /// to differ at runtime: Destroy instead of DestroyImmediate, serialized
    /// Mesh/Material references instead of AssetDatabase.LoadAssetAtPath, a
    /// project layer looked up by name instead of one created on demand.
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
        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);

        /// <summary>The layer the redraw's stage is drawn and lit on, alone.</summary>
        public const string Layer = "CityXRedraw";

        /// <summary>
        /// Builds the whole overlay -- 3D stage plus tool column -- and
        /// returns its root. Destroy the returned GameObject to tear the
        /// whole session down; there is nothing else to clean up.
        /// </summary>
        public static GameObject Build(GameObject player, Mesh[] hairMeshes, Material hairMaterial,
            System.Action<CharacterCreator> onConfirm, System.Action onCancel)
        {
            var root = new GameObject("RedrawOverlay", typeof(RectTransform));
            // INACTIVE FOR THE WHOLE BUILD, switched on at the very end -- the panel's own version of BuildStage's rule below. ButtonAction,
            // CreatorControls and HairColourControls wire themselves up in Awake, and Awake fires the instant a component is added to an
            // ACTIVE object: before this file has said what the button does or which slider is which. Built active, every button woke up as
            // "does nothing" and never listened, and neither did the sliders -- the tools, the swatches, mirror, eraser, undo, redo, the hue
            // and brush sliders and the hairstyles all did nothing (owner, 2026-10-17: "none of the tools work properly", "particularly the eye
            // dropper"). The title screen's creator never had this: it is built in the editor and saved, so its components wake up wired.
            root.SetActive(false);
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

            // SAVE/CANCEL, not ENTER THE CITY/BACK -- there is no name to
            // give an existing character and nowhere to hand it off to.
            // CANCEL reuses CreatorTryBack rather than a redraw-only Kind:
            // that already shows the discard prompt when HasEdits is true
            // and calls Discard() otherwise -- the exact same "are you sure"
            // guard the title screen gets, just routed to OnCancelRedraw
            // instead of Title.Show (see CreatorOverlays.Discard).
            BuildToolColumn(root.transform, creator, "REDRAW", overlays, column =>
            {
                MakeAnchoredButton(column, "SaveButton", "SAVE", Gold,
                    new Vector2(0.0f, 52.0f), new Vector2(172.0f, 36.0f),
                    ButtonAction.Kind.CreatorRedrawSave, creator, null, overlays);
                MakeAnchoredButton(column, "CancelButton", "CANCEL", Paper,
                    new Vector2(0.0f, 16.0f), new Vector2(172.0f, 32.0f),
                    ButtonAction.Kind.CreatorTryBack, creator, null, overlays);
            });
            BuildDiscardPrompt(root.transform, overlays);

            UiKit.EnsureEventSystem();
            // NOW every component's fields are set: waking them wires every button and slider to what it does.
            root.SetActive(true);
            return root;
        }

        /// <summary>
        /// Puts the character's paint and hair on the creator <see cref="Build"/> made. Read from the save, not off a body's renderer: the save is
        /// the single source of truth CharacterIdentity itself always writes to and reads from.
        /// </summary>
        public static void Seed(GameObject overlay, CharacterSave character)
        {
            CreatorOverlays overlays = overlay != null ? overlay.GetComponent<CreatorOverlays>() : null;
            CharacterCreator creator = overlays != null ? overlays.Creator : null;
            if (creator == null || creator.Paint == null || character == null)
            {
                return;
            }

            byte[] paint = character.GetPaint();
            if (paint != null)
            {
                creator.Paint.Decode(paint);
            }
            else
            {
                // No paint saved (a legacy or Godot-imported character): its flat skin colour rather than the blank white a fresh creator opens
                // on, matching CharacterIdentity.Apply's own Recolour fallback.
                creator.Paint.Clear(character.Skin);
            }
            creator.SetHairStyle(character.HairStyle);
            creator.SetHairColour(character.HairColour);

            // The colour picker beside the creator has to show the character's own hair colour, not the colour it was built with.
            HairColourControls hairControls = overlay.GetComponentInChildren<HairColourControls>(true);
            if (hairControls != null)
            {
                hairControls.Refresh();
            }
        }

        // --- the 3D half ---

        /// <summary>
        /// The turntable, the cloned body and the camera: the runtime version of
        /// CharacterCreatorBuilder.BuildStage -- see this file's own doc
        /// comment for what differs.
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
            // A figure from the character list (CharacterSelect's pencil) idles and plants its feet on the belt.
            Strip<LineupFigure>(body);
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
            // At once, not at the end of the frame: the stage comes up this frame, and a copied CharacterHair still waiting to go wakes then
            // and grows a second hair of its own.
            foreach (CharacterHair copied in body.GetComponentsInChildren<CharacterHair>(includeInactive: true))
            {
                Object.DestroyImmediate(copied);
            }
            // ...and the hair it wore is a plain object under the head, copied too: left there, the fresh hair sat inside the old and the two
            // showed through each other in speckles (owner, 2026-11-15: "make sure there isnt a new hair overlapping with the old one").
            foreach (Transform part in body.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (part.name == CharacterHair.WornName && part.GetComponent<MeshRenderer>() != null)
                {
                    part.name = "CopiedHair";   // gone at the end of the frame; renamed now, so nothing finds it before then
                    Object.Destroy(part.gameObject);
                }
            }

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

            // ITS OWN LAYER, set up in the project's layers (a runtime script cannot add one): not the title's creator layer, as the character
            // list's pencil opens it over the title's belt, whose figures, belt and lights are on that one. Shared, the redraw camera drew the
            // belt and the characters behind the one redrawn, and the belt's lights lit it twice over.
            int layer = LayerMask.NameToLayer(Layer);
            if (layer < 0)
            {
                Debug.LogWarning($"CityX: no {Layer} layer -- ReDraw will render the whole scene behind the character.");
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
            Transform head = TransformSearch.FindDeep(body.transform, "head");
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

        /// <summary>RUNTIME-SAFE: Destroy(), not DestroyImmediate() -- this always runs during play.</summary>
        private static void Strip<T>(GameObject root) where T : Component
        {
            foreach (T found in root.GetComponentsInChildren<T>(includeInactive: true))
            {
                Object.Destroy(found);
            }
        }
    }
}
