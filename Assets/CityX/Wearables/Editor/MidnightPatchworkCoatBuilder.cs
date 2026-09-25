using System.Collections.Generic;
using CityX.Character;
using CityX.CharacterEditor;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the Midnight Patchwork Coat from a third-party Meshy AI biped
    /// export (midnight_patchwork_coat.glb) -- a full character, not a
    /// pre-cut garment the way hooded_cloak.glb/skelli_jeans.glb already
    /// are, so unlike SkinnedGarmentBuilder.BakeMesh this has to do its own
    /// cutting before it can reuse that file's own bind-and-scaffold
    /// machinery (BuildPrefabFromMesh, PoseBindSkeletonArmsDown).
    ///
    /// THREE PROBLEMS THIS SOURCE FILE HAS THAT hooded_cloak.glb/
    /// skelli_jeans.glb DO NOT, confirmed by instantiating it and dumping
    /// its own bones and per-vertex dominant weights rather than assumed:
    ///
    /// 1. IT IS A WHOLE CHARACTER, ONE MESH, ONE MATERIAL -- head, hands,
    ///    feet and a full pair of legs are baked into the exact same
    ///    139,443-vertex "char1" mesh as the coat itself, with nothing to
    ///    key off but bone weight (see StripAndRebind). Cut by whichever
    ///    bone most influences each vertex, keeping the torso (Hips/Spine/
    ///    Spine01/Spine02/neck) and both full sleeves (LeftShoulder/LeftArm/
    ///    LeftForeArm and the right-side mirror), stripping the head, both
    ///    hands and both legs. Removing the head and legs, and the hands at
    ///    the sleeve cuff, was the user's own explicit request; the
    ///    sleeves themselves were stripped in an earlier version too, on
    ///    the same request, and put back after seeing the result worn
    ///    in-game -- see KeepBoneTarget's own doc comment for why keeping
    ///    them also fixed a real tearing bug, not just an aesthetic one.
    ///
    /// 2. IT IS REAL-HUMAN SCALE, NOT THIS RIG'S SCALE. hooded_cloak.glb and
    ///    skelli_jeans.glb were authored specifically for fused_body.gd's
    ///    own proportions, so BakeMesh could bind their vertices straight
    ///    against this rig's bones with no scale correction. This mesh's
    ///    own Hips-to-shoulder span is 0.54m; this rig's is ShoulderY-HipY
    ///    = 1.0m. Skipped, a coat authored for a real body reads shrink-
    ///    wrapped onto a fraction of this one's much taller torso. Fixed
    ///    with a single scale+recentre applied to every kept vertex before
    ///    binding, calibrated on exactly that span (see StripAndRebind) --
    ///    the legs/head/arms being stripped anyway is what makes a single
    ///    torso-span scale factor the right one, rather than needing this
    ///    rig's own very different (long-armed, big-headed) full-body
    ///    proportions to reconcile too.
    ///
    /// 3. IT IS ALREADY CORRECTLY HANDED -- checked, not assumed, because
    ///    hooded_cloak.glb/skelli_jeans.glb both needed SkinnedGarmentBuilder's
    ///    own MirrorX for exactly this. This file's LeftShoulder/LeftArm sit
    ///    at negative X, matching this rig's own arm_l -- so MirrorX is
    ///    deliberately NOT applied here; doing it anyway on an
    ///    already-correct mesh would introduce the exact bug it exists to
    ///    fix elsewhere.
    /// </summary>
    public static class MidnightPatchworkCoatBuilder
    {
        private const string GlbPath = "Assets/CityX/Wearables/Models/midnight_patchwork_coat.glb";
        private const string MeshPath = "Assets/CityX/Wearables/MidnightPatchworkCoatMesh.asset";
        private const string PrefabPath = "Assets/CityX/Wearables/MidnightPatchworkCoat.prefab";
        private const string MaterialPath = "Assets/CityX/Wearables/MidnightPatchworkCoatFabric.mat";

        // Matches HoodedCloak's own ground-pose numbers -- a similarly
        // bulky garment, tipped the same way so it reads as a dropped coat
        // rather than half an invisible person.
        private const float GroundScale = 0.45f;
        private const float GroundPitchDegrees = -78.0f;
        private const float GroundLift = 0.15f;

        /// <summary>
        /// Index into this array IS the BoneWeight index the baked mesh
        /// uses -- matches SkinnedGarment.Bones exactly, so the runtime
        /// rebind in SkinnedGarment.OnEquip (which builds boneTransforms in
        /// THIS SAME order via Animator.GetBoneTransform) lines up with
        /// bindposes computed here without either file needing to know
        /// about the other's indices.
        /// </summary>
        private static readonly string[] TargetBoneNames =
        {
            "hips", "spine", "neck", "head",
            "arm_l", "forearm_l", "hand_l",
            "arm_r", "forearm_r", "hand_r",
            "thigh_l", "shin_l",
            "thigh_r", "shin_r",
        };

        private const int Hips = 0, Spine = 1, Neck = 2, ArmL = 4, ForearmL = 5, ArmR = 7, ForearmR = 8;

        /// <summary>
        /// The source biped's own 24 joint names, mapped to a slot in
        /// TargetBoneNames above -- everything NOT listed here (Head,
        /// head_end, headfront, both hands, the whole leg chain) is the
        /// strip set, checked by absence rather than a second list, so the
        /// two can never drift out of sync.
        ///
        /// THE SLEEVES ARE KEPT, per an explicit follow-up ask after seeing
        /// the first stripped version worn in-game: LeftArm/LeftForeArm/
        /// RightArm/RightForeArm bind to this rig's own arm_l/forearm_l/
        /// arm_r/forearm_r, the same bones the shoulder itself binds to (see
        /// below) -- only the hands are stripped, so the coat ends at the
        /// wrist like a real sleeve cuff rather than continuing onto bare
        /// fingers.
        ///
        /// LeftShoulder/RightShoulder MAP TO ARM_L/ARM_R, not spine, NOW
        /// THAT THE SLEEVE IS KEPT -- this is the second version of this
        /// mapping, not the first. Spine was tried when the arm was still
        /// being stripped entirely: with nothing below it, the shoulder was
        /// a dead-end flap cantilevered off whatever bone it was bound to,
        /// and binding it to the rotating arm_l/arm_r swung that whole
        /// unattached flap through the live pose's own angular mismatch
        /// against PoseBindSkeletonArmsDown's fixed 78-degree assumption --
        /// confirmed by an actual in-game screenshot showing it bunched up
        /// near the collar. With the sleeve now kept, the shoulder is the
        /// TOP of a continuous tube that already has to move with arm_l for
        /// the sleeve below it to hang together as one piece -- binding it
        /// to spine instead would reintroduce the identical tear at the
        /// shoulder/upper-arm seam this fix just removed, just one joint
        /// higher up.
        /// </summary>
        private static readonly Dictionary<string, int> KeepBoneTarget = new Dictionary<string, int>
        {
            ["Hips"] = Hips,
            ["Spine"] = Spine, ["Spine01"] = Spine, ["Spine02"] = Spine,
            ["neck"] = Neck,
            ["LeftShoulder"] = ArmL, ["LeftArm"] = ArmL, ["LeftForeArm"] = ForearmL,
            ["RightShoulder"] = ArmR, ["RightArm"] = ArmR, ["RightForeArm"] = ForearmR,
        };

        [MenuItem("CityX/Rebuild Midnight Patchwork Coat")]
        public static GameObject Build()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (source == null)
            {
                Debug.LogError($"CityX: {GlbPath} did not load as a GameObject -- " +
                                "check that com.unity.cloud.gltfast is installed and the file reimported.");
                return null;
            }

            var sourceRenderer = source.GetComponentInChildren<SkinnedMeshRenderer>();
            if (sourceRenderer == null || sourceRenderer.sharedMesh == null)
            {
                Debug.LogError($"CityX: {GlbPath} has no SkinnedMeshRenderer.");
                return null;
            }

            Mesh bakedMesh = StripAndRebind(sourceRenderer, MeshPath);
            if (bakedMesh == null)
            {
                return null;
            }

            Material material = BuildMaterial();

            GameObject saved = SkinnedGarmentBuilder.BuildPrefabFromMesh<MidnightPatchworkCoat>(
                bakedMesh, material, PrefabPath, "MidnightPatchworkCoat",
                GroundScale, GroundPitchDegrees, GroundLift);
            if (saved != null)
            {
                Debug.Log($"CityX: MidnightPatchworkCoat written to {PrefabPath} -- " +
                          $"{bakedMesh.vertexCount} vertices (from {sourceRenderer.sharedMesh.vertexCount} source)");
            }
            return saved;
        }

        /// <summary>
        /// A plain, UNTEXTURED URP Lit material -- flat "midnight" near-
        /// black, not the embedded texture. Checked directly, not assumed:
        /// the source .glb's own baseColorTexture decodes to a valid PNG at
        /// its declared 4096x4096 (confirmed by extracting its raw bytes
        /// from the glb's BIN chunk and reading the PNG header), but
        /// Unity's own glTFast import of that exact file silently produces
        /// a 4x4 Texture2D instead -- an import-time failure, not a missing
        /// or corrupt source asset. Worse, the 4096x4096 PNG itself, viewed
        /// directly, is not usable art anyway: large rectangular patches of
        /// its own UV layout are blank white, and the painted regions are a
        /// jagged, faceted mess rather than a clean fabric photo -- Meshy's
        /// auto-bake failing on this mesh's many overlapping quilted-square
        /// panels, confirmed by extracting and viewing that PNG directly
        /// rather than guessing from the in-game result. That mottled,
        /// shredded look is exactly what showed up worn in-game once this
        /// was traced all the way through, not a skinning bug. Fixing
        /// glTFast's own decode would still hand back unusable art, so
        /// this uses the coat's own name instead: a flat near-black reads
        /// as "midnight" on its own, and the mesh's own quilted-square
        /// geometry (see this file's class comment) still shows the
        /// patchwork shape through normal-based shading with no texture at
        /// all -- the same way the reference render of this coat on a bare
        /// white material already reads as a coat.
        /// </summary>
        private static Material BuildMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = "MidnightPatchworkCoatFabric" };
            material.SetColor("_BaseColor", new Color(0.04f, 0.04f, 0.05f));
            material.SetFloat("_Smoothness", 0.25f);
            material.SetFloat("_Metallic", 0.0f);
            // The armholes, hem and collar are all freshly-cut open
            // boundaries -- back-face culled (Unity's own default) they
            // read as literal holes into nothing, the same fix BuildCloak
            // and BuildJeansMaterial both already needed for their own
            // open seams.
            material.SetFloat("_Cull", 0.0f);

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(MaterialPath);
            }
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Mesh StripAndRebind(SkinnedMeshRenderer source, string path)
        {
            Transform[] sourceBones = source.bones;
            Transform hips = System.Array.Find(sourceBones, b => b != null && b.name == "Hips");
            Transform leftShoulder = System.Array.Find(sourceBones, b => b != null && b.name == "LeftShoulder");
            Transform rightShoulder = System.Array.Find(sourceBones, b => b != null && b.name == "RightShoulder");
            if (hips == null || leftShoulder == null || rightShoulder == null)
            {
                Debug.LogError("CityX: coat source is missing Hips/LeftShoulder/RightShoulder -- " +
                                "cannot calibrate its scale against this rig.");
                return null;
            }

            // Calibrated on hip-to-shoulder span only, not the whole body --
            // this mesh's own head/arms/legs are being discarded, so only
            // the torso the coat actually keeps needs to land at the right
            // size. See this file's own class comment for the real numbers.
            Vector3 hipsWorld = hips.position;
            float sourceSpan = ((leftShoulder.position.y + rightShoulder.position.y) * 0.5f) - hipsWorld.y;
            float scale = (BodyRig.ShoulderY - BodyRig.HipY) / sourceSpan;
            var targetHips = new Vector3(0.0f, BodyRig.HipY, 0.0f);
            Vector3 Reposition(Vector3 v) => (v - hipsWorld) * scale + targetHips;

            // WHERE THE ARM PIVOT ACTUALLY LANDS ONCE SCALED, not
            // BodyRig.ShoulderX -- the scale above is calibrated purely on
            // vertical (hip-to-shoulder) span, so this mesh's own natural
            // width after that same uniform scale does not necessarily
            // land on this rig's own shoulder-width constant. Confirmed
            // wrong directly: BodyRig.ShoulderX is 0.35, this source's own
            // LeftArm bone scales to roughly 0.44. SmoothShoulderWeights
            // needs the REAL number, or its own blend band sits entirely
            // outside where the seam actually is and smooths nothing.
            Transform leftArm = System.Array.Find(sourceBones, b => b != null && b.name == "LeftArm");
            float shoulderPivotX = leftArm != null
                ? Mathf.Abs(Reposition(leftArm.position).x)
                : BodyRig.ShoulderX;

            var targetIndexForSourceBone = new int[sourceBones.Length];
            for (int i = 0; i < sourceBones.Length; i++)
            {
                string name = sourceBones[i] != null ? sourceBones[i].name : null;
                targetIndexForSourceBone[i] = (name != null && KeepBoneTarget.TryGetValue(name, out int t)) ? t : -1;
            }

            Mesh sourceMesh = source.sharedMesh;
            Vector3[] positions = sourceMesh.vertices;
            Vector3[] normals = sourceMesh.normals;
            Vector2[] uvs = sourceMesh.uv;
            BoneWeight[] weights = sourceMesh.boneWeights;
            int[] triangles = sourceMesh.triangles;

            int vertexCount = positions.Length;
            var kept = new bool[vertexCount];
            var oldToNew = new int[vertexCount];
            var resolvedWeights = new float[vertexCount][];

            for (int i = 0; i < vertexCount; i++)
            {
                BoneWeight w = weights[i];

                // DOMINANT BONE ONLY, NOT "ANY WEIGHT AT ALL" -- tried the
                // looser version first (any nonzero weight on a kept bone)
                // on the theory that a smooth blend was being cut too
                // strictly. Confirmed wrong by an actual render: it kept
                // 125,934/139,443 vertices (barely 4,640 of which were the
                // targeted rescue below), because plenty of leg/head
                // vertices carry a small real bleed onto Hips/Spine even
                // though their own dominant bone is nowhere near the coat
                // -- pulling in most of a leg, bound to the torso, showed
                // up as a long warped flap hanging off the hem. Bone
                // assignment here IS effectively single-dominant (see
                // SmoothShoulderWeights' own doc comment); the strict gate
                // is what that fact actually calls for, with the rescue
                // pass below doing the real, targeted work of closing the
                // few-vertex notches the boundary's own inconsistency
                // leaves behind, instead of loosening admission everywhere.
                int dominantSource = DominantBoneIndex(w);
                int dominantTarget = (dominantSource >= 0 && dominantSource < targetIndexForSourceBone.Length)
                    ? targetIndexForSourceBone[dominantSource]
                    : -1;
                if (dominantTarget < 0)
                {
                    continue;   // head, an arm, or a leg -- stripped per the user's own request
                }

                var slot = new float[TargetBoneNames.Length];
                Accumulate(slot, targetIndexForSourceBone, w.boneIndex0, w.weight0);
                Accumulate(slot, targetIndexForSourceBone, w.boneIndex1, w.weight1);
                Accumulate(slot, targetIndexForSourceBone, w.boneIndex2, w.weight2);
                Accumulate(slot, targetIndexForSourceBone, w.boneIndex3, w.weight3);

                float total = 0.0f;
                for (int s = 0; s < slot.Length; s++) total += slot[s];
                if (total <= 0.0001f)
                {
                    continue;   // every real weight this vertex had pointed at a stripped bone
                }
                for (int s = 0; s < slot.Length; s++) slot[s] /= total;

                kept[i] = true;
                resolvedWeights[i] = slot;
            }

            var repositioned = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                repositioned[i] = Reposition(positions[i]);
            }
            RescueSingleVertexNotches(triangles, kept, resolvedWeights, repositioned);

            var newPositions = new List<Vector3>(vertexCount / 3);
            var newNormals = new List<Vector3>(vertexCount / 3);
            var newUVs = new List<Vector2>(vertexCount / 3);
            var newWeights = new List<BoneWeight>(vertexCount / 3);

            for (int i = 0; i < vertexCount; i++)
            {
                if (!kept[i])
                {
                    continue;
                }
                oldToNew[i] = newPositions.Count;
                newPositions.Add(Reposition(positions[i]));
                // A uniform scale never changes a normal's direction, only
                // its magnitude -- copied as-is rather than rescaled.
                newNormals.Add((normals != null && normals.Length == vertexCount) ? normals[i] : Vector3.up);
                newUVs.Add((uvs != null && uvs.Length == vertexCount) ? uvs[i] : Vector2.zero);
                newWeights.Add(PackTop4(resolvedWeights[i]));
            }

            var newTriangles = new List<int>(triangles.Length / 2);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                // Only a triangle with all three corners kept -- a partial
                // one would stretch a single vertex across the cut boundary
                // into whatever the stripped side used to be.
                if (kept[a] && kept[b] && kept[c])
                {
                    newTriangles.Add(oldToNew[a]);
                    newTriangles.Add(oldToNew[b]);
                    newTriangles.Add(oldToNew[c]);
                }
            }

            GameObject skeletonRoot = BodyRigBuilder.BuildSkeleton(
                "CoatBindSkeleton", out Dictionary<string, Transform> named, BodyRig.RestBones());
            try
            {
                // Same arms-down correction the cloak/jeans bind needs, for
                // the same reason -- the shoulder bones this coat still
                // carries weight on (LeftShoulder/RightShoulder -> arm_l/
                // arm_r) have to be bound at the pose the live rig actually
                // rests in, not the T-pose BuildSkeleton starts from.
                SkinnedGarmentBuilder.PoseBindSkeletonArmsDown(named);

                var bindPoses = new Matrix4x4[TargetBoneNames.Length];
                for (int i = 0; i < TargetBoneNames.Length; i++)
                {
                    Transform bone = named[TargetBoneNames[i]];
                    bindPoses[i] = bone.worldToLocalMatrix * skeletonRoot.transform.localToWorldMatrix;
                }

                var baked = new Mesh { name = "MidnightPatchworkCoatMesh" };
                baked.indexFormat = newPositions.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16;
                baked.SetVertices(newPositions);
                baked.SetNormals(newNormals);
                baked.SetUVs(0, newUVs);
                baked.boneWeights = newWeights.ToArray();
                baked.bindposes = bindPoses;
                baked.SetTriangles(newTriangles, 0);
                baked.RecalculateBounds();
                SmoothShoulderWeights(baked, shoulderPivotX);

                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
                AssetDatabase.CreateAsset(baked, path);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: coat strip kept {newPositions.Count}/{vertexCount} vertices, " +
                          $"{newTriangles.Count / 3} triangles.");
                return baked;
            }
            finally
            {
                Object.DestroyImmediate(skeletonRoot);
            }
        }

        /// <summary>
        /// THE REAL CAUSE OF THE SLEEVE TEARING AT THE SHOULDER -- confirmed
        /// directly by rendering the equipped coat mid-stride: the arm
        /// swung forward and the sleeve visibly split away from the torso
        /// at the shoulder seam, exactly where spine-bound vertices meet
        /// arm_l-bound ones. SkinnedGarmentBuilder's own SmoothNeckWeights
        /// hit an identical bug on the cloak's collar for an identical
        /// reason, confirmed there by dumping the source's own weights
        /// directly: a Meshy auto-rig, unlike hooded_cloak.glb/
        /// skelli_jeans.glb's own hand-authored skinning, assigns each
        /// vertex a single dominant bone with no blend across a joint that
        /// is about to move -- fine at the bind pose, guaranteed to tear
        /// the instant spine (frozen every frame by PlayerLocomotion) and
        /// arm_l (which actually rotates) diverge.
        ///
        /// A SYNTHETIC BLEND ACROSS A HORIZONTAL BAND, not a vertical one
        /// like the neck's -- the shoulder seam runs along the arm's own
        /// axis, not the spine's, so what stands in for "distance across
        /// the joint" here is a vertex's horizontal (X) distance from the
        /// body's own centreline: BodyRig's own torso radius is fully
        /// spine, <paramref name="bandOuterX"/> (this specific mesh's own
        /// scaled arm-pivot X, NOT BodyRig.ShoulderX -- see StripAndRebind's
        /// own comment on why the two differ) is fully arm_l/arm_r, and
        /// everything between blends linearly. Only vertices already on
        /// spine or the relevant arm, and only near shoulder height, are
        /// touched -- a sleeve cuff far down the arm, or a hip vertex
        /// nowhere near the shoulder, is left exactly as StripAndRebind
        /// already weighted it.
        /// </summary>
        private static void SmoothShoulderWeights(Mesh mesh, float bandOuterX)
        {
            const float bandInnerX = FusedBodyShape.TorsoRadius;
            const float bandHalfHeight = 0.35f;

            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            int touched = 0;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (Mathf.Abs(v.y - BodyRig.ShoulderY) > bandHalfHeight)
                {
                    continue;
                }

                float absX = Mathf.Abs(v.x);
                if (absX < bandInnerX || absX > bandOuterX)
                {
                    continue;   // fully inside the torso, or already well out into the sleeve
                }

                int armIndex = v.x < 0.0f ? ArmL : ArmR;
                BoneWeight w = weights[i];
                bool onSpineOrArm =
                    (w.boneIndex0 == Spine && w.weight0 > 0.0f) || (w.boneIndex1 == Spine && w.weight1 > 0.0f) ||
                    (w.boneIndex0 == armIndex && w.weight0 > 0.0f) || (w.boneIndex1 == armIndex && w.weight1 > 0.0f);
                if (!onSpineOrArm)
                {
                    continue;
                }

                float t = Mathf.InverseLerp(bandInnerX, bandOuterX, absX);
                weights[i] = new BoneWeight
                {
                    boneIndex0 = Spine, weight0 = 1.0f - t,
                    boneIndex1 = armIndex, weight1 = t,
                    boneIndex2 = 0, weight2 = 0.0f,
                    boneIndex3 = 0, weight3 = 0.0f,
                };
                touched++;
            }

            mesh.boneWeights = weights;
            Debug.Log($"CityX: smoothed {touched} shoulder vertices between spine and arm.");
        }

        /// <summary>
        /// CLOSES SINGLE-VERTEX NOTCHES ALONG THE CUT BOUNDARY. The source
        /// rig assigns each vertex to a single dominant bone with no blend
        /// across a joint (see SmoothShoulderWeights' own doc comment) --
        /// so the strict per-vertex classification above does not cut a
        /// clean topological ring at the neckline/cuffs/hem, it follows
        /// wherever the auto-rigger's own (imprecise, per-panel) weight
        /// assignment happens to flip from a kept bone to a stripped one,
        /// vertex by vertex. A triangle whose assignment flips mid-triangle
        /// -- two corners on a kept bone, one isolated corner assigned to
        /// the stripped bone right next to them -- had its one discarded
        /// corner drop the whole triangle under the "all three kept" rule
        /// below, which is where the reported holes at those exact
        /// boundaries came from: confirmed by dumping the per-vertex
        /// classification along the hem and finding it interleaved rather
        /// than a clean ring, not a smooth gradient crossing (a smooth
        /// blend would have made a lower keep-threshold the right fix
        /// instead of this).
        ///
        /// PROMOTES THE ISOLATED CORNER RATHER THAN DROPPING THE TRIANGLE:
        /// any triangle with exactly two kept corners gets its third pulled
        /// in too, inheriting the average of its two already-resolved
        /// neighbours' bone weights -- never its own, since that vertex's
        /// own weight pointed at a stripped bone, which is the entire
        /// reason it needed rescuing.
        ///
        /// A HARD GEOMETRIC FENCE, NOT JUST A PASS CAP -- run uncapped, this
        /// does not converge: confirmed directly by logging every pass, which
        /// kept rescuing hundreds of vertices a pass for 59 passes straight,
        /// climbing from 77,746 kept toward 101,872/139,443 with no sign of
        /// stopping. The auto-rig's own weight noise is not confined to a
        /// thin ring around the true seam -- it is scattered widely enough
        /// that majority-vote dilation alone tunnels straight down into the
        /// actual legs and up into the actual head a vertex at a time, which
        /// is exactly how the very first, ungated version of this rescue
        /// produced the long stretched flap a raw render caught. Only a
        /// candidate whose OWN repositioned position already falls in the
        /// hem or collar band below can be promoted, however many passes
        /// run -- capping is therefore about closing the true boundary
        /// completely rather than stopping a runaway partway through it.
        /// </summary>
        // Lower than a plain hem needs -- this coat hangs OPEN at the
        // front (confirmed directly by rendering the raw, untouched source:
        // a buttoned placket with both flaps left open, the gap between
        // them showing the legs by design, not a hole). Those two flaps
        // reach well below the hip line, so the rescue band has to reach
        // that far too or their own ragged edges are simply out of its
        // reach regardless of pass count.
        private const float HemBandLowY = BodyRig.HipY - 0.60f;
        private const float HemBandHighY = BodyRig.HipY + 0.25f;
        private const float CollarBandLowY = BodyRig.ShoulderY - 0.15f;
        private static readonly float CollarBandHighY = BodyRig.HeadCentreY - 0.05f;
        private const float BoundaryBandMaxAbsX = 0.30f;

        private static bool InBoundaryBand(Vector3 position)
        {
            if (Mathf.Abs(position.x) > BoundaryBandMaxAbsX)
            {
                return false;   // out along a sleeve, not near the hem/collar centreline
            }
            return (position.y >= HemBandLowY && position.y <= HemBandHighY) ||
                   (position.y >= CollarBandLowY && position.y <= CollarBandHighY);
        }

        private static void RescueSingleVertexNotches(
            int[] triangles, bool[] kept, float[][] resolvedWeights, Vector3[] repositioned)
        {
            const int MaxPasses = 40;
            int totalRescued = 0;
            for (int pass = 0; pass < MaxPasses; pass++)
            {
                int rescuedThisPass = 0;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    rescuedThisPass += TryRescue(a, b, c, kept, resolvedWeights, repositioned);
                    rescuedThisPass += TryRescue(b, c, a, kept, resolvedWeights, repositioned);
                    rescuedThisPass += TryRescue(c, a, b, kept, resolvedWeights, repositioned);
                }
                totalRescued += rescuedThisPass;
                if (rescuedThisPass == 0)
                {
                    break;
                }
            }
            Debug.Log($"CityX: rescued {totalRescued} single-vertex notches along the coat's hem/collar boundary.");
        }

        /// <summary>
        /// Promotes <paramref name="candidate"/> if it is the lone unkept
        /// corner of a triangle whose other two corners are both already
        /// kept AND it falls inside the hem/collar boundary band -- checked
        /// once per corner as the candidate (three calls per triangle,
        /// rotating which corner is which), since which one is the isolated
        /// corner is not known ahead of time.
        /// </summary>
        private static int TryRescue(
            int candidate, int otherA, int otherB, bool[] kept, float[][] resolvedWeights, Vector3[] repositioned)
        {
            if (kept[candidate] || !kept[otherA] || !kept[otherB] || !InBoundaryBand(repositioned[candidate]))
            {
                return 0;
            }
            float[] weightsA = resolvedWeights[otherA];
            float[] weightsB = resolvedWeights[otherB];
            var averaged = new float[weightsA.Length];
            for (int s = 0; s < averaged.Length; s++)
            {
                averaged[s] = (weightsA[s] + weightsB[s]) * 0.5f;
            }
            kept[candidate] = true;
            resolvedWeights[candidate] = averaged;
            return 1;
        }

        private static int DominantBoneIndex(BoneWeight w)
        {
            int dominant = w.boneIndex0;
            float best = w.weight0;
            if (w.weight1 > best) { best = w.weight1; dominant = w.boneIndex1; }
            if (w.weight2 > best) { best = w.weight2; dominant = w.boneIndex2; }
            if (w.weight3 > best) { dominant = w.boneIndex3; }
            return dominant;
        }

        private static void Accumulate(float[] slot, int[] targetIndexForSourceBone, int sourceBoneIndex, float weight)
        {
            if (weight <= 0.0f || sourceBoneIndex < 0 || sourceBoneIndex >= targetIndexForSourceBone.Length)
            {
                return;
            }
            int target = targetIndexForSourceBone[sourceBoneIndex];
            if (target < 0)
            {
                return;   // this vertex's small bleed onto a stripped bone -- dropped, not kept
            }
            slot[target] += weight;
        }

        /// <summary>Up to the four largest nonzero slots, descending -- BoneWeight only ever carries four.</summary>
        private static BoneWeight PackTop4(float[] slot)
        {
            int i0 = 0, i1 = 0, i2 = 0, i3 = 0;
            float w0 = 0.0f, w1 = 0.0f, w2 = 0.0f, w3 = 0.0f;
            for (int s = 0; s < slot.Length; s++)
            {
                float v = slot[s];
                if (v <= 0.0f)
                {
                    continue;
                }
                if (v > w0) { w3 = w2; i3 = i2; w2 = w1; i2 = i1; w1 = w0; i1 = i0; w0 = v; i0 = s; }
                else if (v > w1) { w3 = w2; i3 = i2; w2 = w1; i2 = i1; w1 = v; i1 = s; }
                else if (v > w2) { w3 = w2; i3 = i2; w2 = v; i2 = s; }
                else if (v > w3) { w3 = v; i3 = s; }
            }
            return new BoneWeight
            {
                boneIndex0 = i0, weight0 = w0,
                boneIndex1 = i1, weight1 = w1,
                boneIndex2 = i2, weight2 = w2,
                boneIndex3 = i3, weight3 = w3,
            };
        }
    }
}
