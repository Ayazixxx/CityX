using System.Collections.Generic;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The chain (docs/CHAIN.md; owner, 2026-11-22, with two photographs of plain steel chain: "make it something like those two chains i sent"):
    /// long oval links of round bar, bright steel, each turned a quarter turn from the last. One skinned mesh, a bone a link (ChainWeapon puts
    /// each bone where its link is every frame: wound on the arm, or hanging and swung), so the whole chain is one thing to draw. As it is built,
    /// and as it lies on the ground to be picked up, it is coiled flat.
    /// </summary>
    public static class ChainBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/Chain.prefab";
        private const string Folder = "Assets/CityX/Weapons/Chain";

        public static readonly Color SteelColour = new Color(0.74f, 0.76f, 0.80f);

        /// <summary>Hot, the steel is pale yellow and gives off this light of its own, near white (the glow about it goes orange and red: FireLook.Shed): past 1, so the bloom catches it.</summary>
        public static readonly Color HotSteelColour = new Color(1.0f, 0.8f, 0.55f);

        public static readonly Color HotGlow = new Color(3.2f, 2.3f, 1.2f);

        /// <summary>Round the link's oval, and round its bar: enough to read as round bar at the size it is seen.</summary>
        public const int PointsRoundAnEnd = 5, SidesOfTheBar = 5;

        /// <summary>The coil on the ground: its innermost turn's radius and how far apart its turns lie, metres.</summary>
        public const float CoilInnerRadius = 0.10f, CoilTurnSpacing = 0.075f;

        /// <summary>How big a box the chain is never drawn outside of, from the hand, metres (a skinned mesh is culled by a box that does not follow its bones).</summary>
        public const float DrawnWithinMetres = 5.0f;

        [MenuItem("CityX/Rebuild Chain")]
        public static GameObject Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Chain");
            }
            Material steel = HeldModelBaker.LitMaterial($"{Folder}/ChainSteel.mat", SteelColour, 0.55f, 0.62f);
            // The same steel glowing hot, for the Fire Enhancement: a material of its own, so a build has the shader's glowing variant.
            Material hot = HeldModelBaker.LitMaterial($"{Folder}/ChainSteelHot.mat", HotSteelColour, 0.2f, 0.4f);
            hot.EnableKeyword("_EMISSION");
            hot.SetColor("_EmissionColor", HotGlow);
            hot.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;   // with none, the keyword is dropped as the material is saved
            EditorUtility.SetDirty(hot);

            Link(out Vector3[] linkVertices, out Vector3[] linkNormals, out int[] linkTriangles);
            int count = ChainWrap.TotalLinks;
            var root = new GameObject("Chain");
            try
            {
                var bones = new Transform[count];
                var vertices = new List<Vector3>(count * linkVertices.Length);
                var normals = new List<Vector3>(count * linkVertices.Length);
                var triangles = new List<int>(count * linkTriangles.Length);
                var weights = new List<BoneWeight>(count * linkVertices.Length);
                var binds = new Matrix4x4[count];
                for (int i = 0; i < count; i++)
                {
                    Coiled(i, out Vector3 at, out Quaternion turn);
                    var bone = new GameObject("Link" + i.ToString("00")).transform;
                    bone.SetParent(root.transform, false);
                    bone.localPosition = at;
                    bone.localRotation = turn;
                    bones[i] = bone;
                    binds[i] = bone.worldToLocalMatrix * root.transform.localToWorldMatrix;
                    int first = vertices.Count;
                    for (int v = 0; v < linkVertices.Length; v++)
                    {
                        vertices.Add(at + turn * linkVertices[v]);
                        normals.Add(turn * linkNormals[v]);
                        weights.Add(new BoneWeight { boneIndex0 = i, weight0 = 1.0f });
                    }
                    for (int t = 0; t < linkTriangles.Length; t++)
                    {
                        triangles.Add(first + linkTriangles[t]);
                    }
                }
                Mesh mesh = HeldModelBaker.Save($"{Folder}/ChainLinks.asset", "ChainLinks", vertices.ToArray(), normals.ToArray(), triangles.ToArray());
                mesh.boneWeights = weights.ToArray();
                mesh.bindposes = binds;
                EditorUtility.SetDirty(mesh);

                var drawn = new GameObject("Links");
                drawn.transform.SetParent(root.transform, false);
                var skin = drawn.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.sharedMaterial = steel;
                skin.bones = bones;
                skin.rootBone = root.transform;
                skin.updateWhenOffscreen = false;
                skin.localBounds = new Bounds(Vector3.zero, Vector3.one * (2.0f * DrawnWithinMetres));

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = ChainWeapon.InteractRange;

                ChainWeapon weapon = root.AddComponent<ChainWeapon>();
                weapon.Links = bones;
                weapon.SetLook(skin, steel, hot);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Link <paramref name="index"/> of the chain coiled flat on the ground, a spiral out from the middle: where the prefab's links are bound.</summary>
        public static void Coiled(int index, out Vector3 position, out Quaternion rotation)
        {
            // An Archimedean spiral walked a link's pitch at a time.
            float angle = 0.0f;
            for (int i = 0; i < index; i++)
            {
                angle += ChainWrap.Pitch / (CoilInnerRadius + CoilTurnSpacing * angle / (2.0f * Mathf.PI));
            }
            float radius = CoilInnerRadius + CoilTurnSpacing * angle / (2.0f * Mathf.PI);
            var outward = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
            var along = new Vector3(-Mathf.Sin(angle), 0.0f, Mathf.Cos(angle));
            position = outward * radius + Vector3.up * (ChainWrap.LinkWidth * 0.5f);
            rotation = ChainWrap.LinkTurn(along, Vector3.up, index);
        }

        /// <summary>One link in its own frame: its length along +Z, lying in the XZ plane (the hole looks along Y), round bar bent into a long oval.</summary>
        public static void Link(out Vector3[] vertices, out Vector3[] normals, out int[] triangles)
        {
            float bar = ChainWrap.BarRadius;
            float end = ChainWrap.LinkWidth * 0.5f - bar;                         // the bar's middle line round an end
            float straight = (ChainWrap.LinkLength - ChainWrap.LinkWidth) * 0.5f;   // from the link's middle to where an end's curve starts
            var centres = new List<Vector3>();
            var outs = new List<Vector3>();
            for (int half = 0; half < 2; half++)
            {
                float sign = half == 0 ? 1.0f : -1.0f;
                for (int p = 0; p < PointsRoundAnEnd; p++)
                {
                    float t = Mathf.PI * (half + p / (float)(PointsRoundAnEnd - 1));
                    var radial = new Vector3(Mathf.Cos(t), 0.0f, Mathf.Sin(t));
                    centres.Add(new Vector3(0.0f, 0.0f, sign * straight) + radial * end);
                    outs.Add(radial);
                }
            }
            int rings = centres.Count;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tris = new List<int>();
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < SidesOfTheBar; s++)
                {
                    float a = 2.0f * Mathf.PI * s / SidesOfTheBar;
                    Vector3 normal = outs[r] * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);
                    v.Add(centres[r] + normal * bar);
                    n.Add(normal);
                }
            }
            for (int r = 0; r < rings; r++)
            {
                int next = (r + 1) % rings;
                for (int s = 0; s < SidesOfTheBar; s++)
                {
                    int s1 = (s + 1) % SidesOfTheBar;
                    int a = r * SidesOfTheBar + s, b = r * SidesOfTheBar + s1, c = next * SidesOfTheBar + s, d = next * SidesOfTheBar + s1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }
            vertices = v.ToArray();
            normals = n.ToArray();
            triangles = tris.ToArray();
        }
    }
}
