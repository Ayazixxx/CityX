using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A black puffer jacket, torso-only like MidnightPatchworkCoat -- the
    /// body underneath keeps showing wherever the jacket itself does not
    /// reach. Unlike every other SkinnedGarment source so far, the source
    /// mesh (blackpuffer.glb) arrived with NO bone weights or skeleton at
    /// all -- see BlackPufferJacketBuilder's own class comment for how its
    /// weights were assigned from geometry alone instead of remapped from
    /// an existing rig.
    ///
    /// SUPPORTS LIVE PLACEMENT RE-TUNING (DevConsoleView's own /jacket
    /// command) -- EXPLICIT BUG, found the hard way: the first version of
    /// that command nudged SkinnedGarmentCombiner.RendererTransform
    /// directly, on the mistaken assumption that a SkinnedMeshRenderer's
    /// own Transform affects its rendered result the way an ordinary
    /// MeshRenderer's does. It does not, once bones are assigned -- the
    /// skinning math derives every vertex's final WORLD position from the
    /// bones' own world transforms; the renderer's own Transform has no
    /// effect on a skinned mesh at all, which is exactly why "the commands
    /// aren't altering it" was reported. SetLivePlacement instead
    /// re-derives the actual mesh -- new vertex positions AND new bone
    /// weights, via the same GeometryRig.TorsoAndSleeveWeight the
    /// permanent editor bake uses -- from a cached copy of the ORIGINAL,
    /// untransformed source vertices, and re-Registers it with the
    /// combiner, the only way a skinned mesh's placement can actually
    /// change live.
    /// </summary>
    public sealed class BlackPufferJacket : SkinnedGarment
    {
        public override string ItemId => ItemRegistry.BlackPufferJacket;

        private Vector3[] _originalVertices;
        private Vector3[] _originalNormals;
        private Vector2[] _originalUVs;
        private int[] _originalTriangles;
        private Matrix4x4[] _bindposes;

        public float LiveScale { get; private set; } = 1.0f;
        public float LiveVerticalOffset { get; private set; }

        /// <summary>
        /// Caches the mesh exactly as baked (ManualScale/ManualVerticalOffset
        /// already applied by the editor builder) as the ORIGIN for live
        /// re-tuning -- LiveScale/LiveVerticalOffset start at 1.0/0.0
        /// meaning "exactly what was baked," not "the source .glb's own raw
        /// coordinates," so a first /jacket pose read-back reflects reality.
        /// </summary>
        protected override void OnEquipped(GameObject player)
        {
            if (GarmentMesh == null || _originalVertices != null)
            {
                return;
            }
            _originalVertices = GarmentMesh.vertices;
            _originalNormals = GarmentMesh.normals;
            _originalUVs = GarmentMesh.uv;
            _originalTriangles = GarmentMesh.triangles;
            _bindposes = GarmentMesh.bindposes;
        }

        /// <summary>
        /// Rebuilds the worn mesh from the cached original vertices at a
        /// new scale/vertical offset, recomputing bone weights against the
        /// NEW positions (so the shoulder/elbow blend zones stay correct
        /// wherever the mesh ends up -- see GeometryRig's own doc comment),
        /// and re-registers with the same combiner an ordinary equip
        /// already found. A no-op if nothing is actually equipped yet.
        /// </summary>
        public void SetLivePlacement(float scale, float verticalOffset)
        {
            if (_originalVertices == null || Combiner == null)
            {
                return;
            }
            LiveScale = scale;
            LiveVerticalOffset = verticalOffset;

            int count = _originalVertices.Length;
            var positions = new Vector3[count];
            var weights = new BoneWeight[count];
            float elbowY = GeometryRig.ElbowY();

            for (int i = 0; i < count; i++)
            {
                Vector3 raw = _originalVertices[i];
                Vector3 p = new Vector3(raw.x * scale, raw.y * scale + verticalOffset, raw.z * scale);
                positions[i] = p;
                weights[i] = GeometryRig.TorsoAndSleeveWeight(p, elbowY);
            }

            var mesh = new Mesh { name = "BlackPufferJacketLiveMesh" };
            mesh.indexFormat = count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(positions);
            mesh.SetNormals(_originalNormals);
            mesh.SetUVs(0, _originalUVs);
            mesh.boneWeights = weights;
            // Bindposes only depend on the SKELETON reference, never on
            // these vertex positions -- reused as-is rather than rebuilt.
            mesh.bindposes = _bindposes;
            mesh.SetTriangles(_originalTriangles, 0);
            mesh.RecalculateBounds();

            Combiner.Register(ItemId, mesh, GarmentMaterial);
        }
    }
}
