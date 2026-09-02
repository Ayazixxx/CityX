using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the hanging ragdoll dummy: a pole, a rope, and a real body
    /// permanently limp from the moment the scene starts. See
    /// HangingRagdollDummy's own class doc comment for why this exists --
    /// a live reference for whether Ragdoll.cs itself is stable, walkable
    /// up to in the actual game rather than only checkable through a
    /// diagnostic render.
    ///
    /// REUSES Civilian.prefab WHOLESALE for the body -- same rig, same
    /// animator, same already-built Ragdoll component CivilianBuilder
    /// already wires up. The Civilian script itself (the wander AI) and
    /// its CharacterController are disabled rather than removed, since
    /// removing either mid-build risks disturbing component references
    /// RagdollBuilder/Ragdoll already wired onto the same GameObject.
    /// </summary>
    public static class HangingRagdollDummyBuilder
    {
        private const string CivilianPrefabPath = "Assets/CityX/Character/Civilian.prefab";

        /// <summary>
        /// How tall the pole stands, in metres. Taller than a real hook
        /// pole needs to be for its own sake -- confirmed directly with a
        /// render that 2.6m left the body resting most of its own weight
        /// on the ground rather than actually dangling clear of it, since
        /// a raised-arm-to-feet span on this rig is close to its own full
        /// standing height. 3.4m leaves feet a real gap above the ground.
        /// </summary>
        public const float PoleHeight = 3.4f;

        /// <summary>
        /// Builds the whole prop at <paramref name="position"/> (the pole's
        /// own base) and parents it under <paramref name="parent"/>.
        /// </summary>
        public static GameObject Build(Vector3 position, Transform parent)
        {
            GameObject civilianPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CivilianPrefabPath);
            if (civilianPrefab == null)
            {
                Debug.LogError("CityX: run CityX/Rebuild Civilian first -- the hanging dummy shares its body.");
                return null;
            }

            var root = new GameObject("HangingRagdollDummy");
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            // --- the pole. A plain cylinder plus a small hook nub at the
            // --- top, where the rope actually anchors.
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localPosition = new Vector3(0.0f, PoleHeight * 0.5f, 0.0f);
            pole.transform.localScale = new Vector3(0.08f, PoleHeight * 0.5f, 0.08f);
            Material poleMaterial = CharacterSkin.Ensure(
                "HangingDummyPole", new Color(0.20f, 0.19f, 0.18f));
            pole.GetComponent<MeshRenderer>().sharedMaterial = poleMaterial;

            var anchor = new GameObject("RopeAnchor");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.localPosition = new Vector3(0.0f, PoleHeight, 0.0f);

            // --- the body. Civilian.prefab already carries a fully built
            // --- Ragdoll (RagdollBuilder.Build already ran when it was
            // --- last rebuilt) -- nothing here builds a rig from scratch.
            var body = (GameObject)PrefabUtility.InstantiatePrefab(civilianPrefab, root.transform);
            body.name = "Dummy";
            // GROUND LEVEL, not half-height -- confirmed directly with a
            // render: putting the root at StandingHeight*0.5 (thinking of
            // it as "torso height") left the whole rig starting a metre
            // higher than the rope could actually hold once it fell and
            // went taut, so it fell all the way to the ground and lay
            // there rather than hanging. A CharacterController-driven root
            // sits at feet level, same as CityMassingSceneBuilder's own
            // player spawn -- letting the rope (not this starting height)
            // be the only thing that decides where it ends up.
            //
            // ONLY 0.3m OFF THE POLE'S OWN X, not further -- a bigger
            // sideways offset (1.4m, tried first) meant falling under
            // gravity built up a real pendulum's worth of swing before the
            // rope ever went taut, confirmed directly with a render: the
            // hips overshot clean past the anchor's own height mid-swing.
            // Starting almost directly under the anchor means gravity has
            // nothing much to swing it INTO, so it settles into a calm
            // hang instead.
            body.transform.localPosition = new Vector3(0.3f, 0.0f, 0.0f);

            Civilian civilian = body.GetComponent<Civilian>();
            if (civilian != null) civilian.enabled = false;
            CharacterController controller = body.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            Ragdoll ragdoll = body.GetComponent<Ragdoll>();
            if (ragdoll == null)
            {
                Debug.LogError("CityX: Civilian.prefab has no Ragdoll -- run CityX/Rebuild Civilian first.");
                return root;
            }

            Animator animator = body.GetComponentInChildren<Animator>();
            Transform rightLowerArm = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;
            Transform rightHand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            Rigidbody handBody = rightLowerArm != null ? rightLowerArm.GetComponent<Rigidbody>() : null;

            if (handBody == null || rightHand == null)
            {
                Debug.LogError("CityX: couldn't find the dummy's own right lower arm rigidbody -- nothing to hang.");
                return root;
            }

            Material ropeMaterial = CharacterSkin.Ensure(
                "HangingDummyRope", new Color(0.14f, 0.13f, 0.12f));
            ropeMaterial.SetFloat("_Smoothness", 0.1f);

            HangingRagdollDummy dummy = root.AddComponent<HangingRagdollDummy>();
            dummy.Ragdoll = ragdoll;
            dummy.HandBody = handBody;
            dummy.HandBone = rightHand;
            dummy.AnchorPoint = anchor.transform;
            // SHORT AND FIXED, not derived from a starting position that
            // physics is about to move anyway -- confirmed directly with a
            // render that a position-derived length landed short of the
            // mark. A short tether keeps the hand close to the hook itself
            // (like actually gripping it), and the body's OWN
            // raised-arm-to-feet length -- not this number -- is what
            // decides how far it hangs below that, the same way a real
            // person hung from a hook works.
            dummy.RopeLength = 0.35f;
            dummy.RopeMaterial = ropeMaterial;

            return root;
        }
    }
}
