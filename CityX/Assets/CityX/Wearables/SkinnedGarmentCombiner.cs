using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// One SkinnedMeshRenderer shared by every currently-worn skinned
    /// garment on a player, replacing the one-renderer-per-item that
    /// SkinnedGarment.OnEquip used to build directly. EXPLICIT ASK:
    /// preparing the equip pipeline for "100s if not 1000s" of clothing
    /// items worn by many players in the same area at once -- a real GPU
    /// skin evaluation runs once per SkinnedMeshRenderer regardless of how
    /// large the catalog gets, so combining whatever is CURRENTLY worn (at
    /// most one item per slot -- see ItemRegistry's own EquipSlot field, so
    /// at most a top and a pair of legs go through this at once) into one
    /// renderer keeps the per-character skinning cost bounded no matter how
    /// many items ever get added.
    ///
    /// A MANUAL VERTEX/WEIGHT CONCATENATION, NOT Mesh.CombineMeshes --
    /// deliberately, not just for style. Mesh.CombineMeshes' documented
    /// contract is for MeshFilter/MeshRenderer geometry; its behaviour for
    /// boneWeights/bindposes across multiple skinned sources sharing one
    /// skeleton is not something to trust unread. This project's every
    /// SkinnedGarment mesh is already baked, at build time, against the
    /// EXACT SAME fixed 14-bone bind skeleton (see SkinnedGarment.Bones and
    /// SkinnedGarmentBuilder.BakeMesh) -- same bone order, same bindposes --
    /// so combining is a degenerate case needing no per-item transform or
    /// bone remapping at all, just concatenating vertices/weights and
    /// offsetting triangle indices, verified directly by
    /// SkinnedGarmentCombinerTests rather than assumed from the API name.
    /// </summary>
    public sealed class SkinnedGarmentCombiner : MonoBehaviour
    {
        private const string RendererName = "CombinedGarmentSkin";

        private sealed class Entry
        {
            public Mesh Mesh;
            public Material Material;
            public bool Visible = true;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        // Kept separately from the dictionary -- a Dictionary's own
        // enumeration order is not guaranteed, and Rebuild has to produce
        // submesh N and sharedMaterials[N] for the SAME item every time, or
        // an item's own material would silently land on a different item's
        // geometry the next time an unrelated register/unregister reshuffled
        // a Dictionary-driven order.
        private readonly List<string> _order = new List<string>();

        // How often distance is re-checked for the LOD switch below, not
        // how often the mesh itself rebuilds -- re-measuring every frame for
        // every worn character in a crowd would burn more CPU than the skin
        // quality drop it decides ever saves.
        private const float LodCheckIntervalSeconds = 0.5f;

        // Past this distance a garment's own extra bone influences are not
        // legible anyway -- dropping to one bone per vertex there is a real
        // GPU skinning saving with many characters on screen, at a cost
        // (visibly blockier deformation right at a joint) that only ever
        // shows up close, which is exactly where this never applies.
        private const float LodFarDistance = 30.0f;

        private SkinnedMeshRenderer _renderer;
        private bool _bonesResolved;
        private float _sinceLodCheck;

        public void Register(string itemId, Mesh mesh, Material material)
        {
            if (mesh == null)
            {
                return;
            }
            if (!_entries.ContainsKey(itemId))
            {
                _order.Add(itemId);
            }
            _entries[itemId] = new Entry { Mesh = mesh, Material = material, Visible = true };
            Rebuild();
        }

        public void Unregister(string itemId)
        {
            if (_entries.Remove(itemId))
            {
                _order.Remove(itemId);
                Rebuild();
            }
        }

        /// <summary>
        /// Excludes (or restores) an item's own submesh without unwearing
        /// it -- HoodedCloak's own UpdateSwingHiding needs the cloak's worn
        /// skin to disappear and reappear without losing its registration.
        /// </summary>
        public void SetVisible(string itemId, bool visible)
        {
            if (_entries.TryGetValue(itemId, out Entry entry) && entry.Visible != visible)
            {
                entry.Visible = visible;
                Rebuild();
            }
        }

        /// <summary>Whether an item is both worn and currently included in the combined mesh.</summary>
        public bool IsShowing(string itemId) =>
            _entries.TryGetValue(itemId, out Entry entry) && entry.Visible;

        /// <summary>The live combined mesh -- for tests and any other direct inspection.</summary>
        public Mesh CombinedMesh => _renderer != null ? _renderer.sharedMesh : null;

        /// <summary>The renderer's own current material list, in the same order as CombinedMesh's submeshes.</summary>
        public Material[] CombinedMaterials => _renderer != null ? _renderer.sharedMaterials : null;

        /// <summary>
        /// Distance-based skin quality, the one LOD lever that actually
        /// applies today -- these items have no cloth simulation or other
        /// per-distance detail to shed yet, so this is deliberately small
        /// rather than a speculative general LOD framework built ahead of
        /// anything that would use its other tiers.
        /// </summary>
        private void Update()
        {
            if (_renderer == null)
            {
                return;
            }
            _sinceLodCheck += Time.deltaTime;
            if (_sinceLodCheck < LodCheckIntervalSeconds)
            {
                return;
            }
            _sinceLodCheck = 0.0f;

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }
            float distance = Vector3.Distance(camera.transform.position, transform.position);
            _renderer.quality = distance > LodFarDistance ? SkinQuality.Bone1 : SkinQuality.Bone4;
        }

        private void Rebuild()
        {
            EnsureRenderer();
            ResolveBonesOnce();

            var visible = new List<Entry>(_order.Count);
            foreach (string id in _order)
            {
                Entry entry = _entries[id];
                if (entry.Visible)
                {
                    visible.Add(entry);
                }
            }

            if (visible.Count == 0)
            {
                _renderer.sharedMesh = null;
                _renderer.enabled = false;
                return;
            }

            var materials = new Material[visible.Count];
            for (int i = 0; i < visible.Count; i++)
            {
                materials[i] = visible[i].Material;
            }

            _renderer.sharedMesh = BuildCombinedMesh(visible);
            _renderer.sharedMaterials = materials;
            _renderer.enabled = true;
        }

        /// <summary>
        /// Plain concatenation -- see this class's own doc comment for why
        /// this is correct precisely because every source already shares
        /// one bind skeleton, rather than a general-purpose skinned-mesh
        /// merge.
        /// </summary>
        private static Mesh BuildCombinedMesh(List<Entry> entries)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var submeshTriangles = new List<int>[entries.Count];
            int vertexOffset = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                Mesh source = entries[i].Mesh;
                Vector3[] sourceVertices = source.vertices;
                Vector3[] sourceNormals = source.normals;
                Vector2[] sourceUVs = source.uv;
                BoneWeight[] sourceWeights = source.boneWeights;

                vertices.AddRange(sourceVertices);
                normals.AddRange(sourceNormals.Length == sourceVertices.Length
                    ? sourceNormals : new Vector3[sourceVertices.Length]);
                uvs.AddRange(sourceUVs.Length == sourceVertices.Length
                    ? sourceUVs : new Vector2[sourceVertices.Length]);
                weights.AddRange(sourceWeights.Length == sourceVertices.Length
                    ? sourceWeights : new BoneWeight[sourceVertices.Length]);

                int[] sourceTriangles = source.triangles;
                var offsetTriangles = new List<int>(sourceTriangles.Length);
                for (int t = 0; t < sourceTriangles.Length; t++)
                {
                    offsetTriangles.Add(sourceTriangles[t] + vertexOffset);
                }
                submeshTriangles[i] = offsetTriangles;

                vertexOffset += sourceVertices.Length;
            }

            var combined = new Mesh { name = RendererName };
            // Two or three garment meshes each running a few thousand
            // vertices (SkinnedGarmentBuilder's own build log already shows
            // per-item counts in that range) clears 65535 well before
            // "100s of items" ever becomes relevant to a single combined
            // mesh -- UInt32 from the start rather than waiting to hit it.
            combined.indexFormat = vertexOffset > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            combined.SetVertices(vertices);
            combined.SetNormals(normals);
            combined.SetUVs(0, uvs);
            combined.boneWeights = weights.ToArray();
            // Identical across every entry by construction (see class doc
            // comment) -- any one of them is as correct as picking the
            // "true" shared skeleton bindpose.
            combined.bindposes = entries[0].Mesh.bindposes;
            combined.subMeshCount = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                combined.SetTriangles(submeshTriangles[i], i);
            }
            combined.RecalculateBounds();
            return combined;
        }

        private void EnsureRenderer()
        {
            if (_renderer != null)
            {
                return;
            }
            var holder = new GameObject(RendererName);
            holder.transform.SetParent(transform, false);
            _renderer = holder.AddComponent<SkinnedMeshRenderer>();
            // Four bones per vertex, matching FusedBodyBuilder's own body
            // renderer and every individual SkinnedGarment renderer this
            // replaces -- a garment sharing a joint with the body has to
            // deform by the same rule or the two visibly diverge wherever
            // more than one bone influences a vertex.
            _renderer.quality = SkinQuality.Bone4;
            _renderer.updateWhenOffscreen = true;
        }

        /// <summary>
        /// Resolved once, from whichever Animator exists the first time
        /// anything is registered -- neither the Animator nor its avatar
        /// changes for the life of a character, so re-resolving on every
        /// Rebuild would just repeat SkinnedGarment.OnEquip's own
        /// GetBoneTransform call for no reason.
        ///
        /// TOLERATES A MISSING OR AVATAR-LESS ANIMATOR -- GetBoneTransform
        /// THROWS rather than returning null when the Animator has no
        /// avatar at all (unlike a missing individual bone, which it
        /// tolerates), the same gap SkinnedGarment.OnEquip's own doc
        /// comment already found and guards before ever calling this.
        /// </summary>
        private void ResolveBonesOnce()
        {
            if (_bonesResolved)
            {
                return;
            }
            _bonesResolved = true;

            var animator = GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null)
            {
                return;
            }

            var bones = new Transform[SkinnedGarment.Bones.Length];
            for (int i = 0; i < SkinnedGarment.Bones.Length; i++)
            {
                bones[i] = animator.GetBoneTransform(SkinnedGarment.Bones[i]);
            }
            _renderer.bones = bones;
            _renderer.rootBone = bones[0];   // Hips
        }
    }
}
