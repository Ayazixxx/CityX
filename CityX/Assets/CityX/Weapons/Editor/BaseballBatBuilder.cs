using System.Collections.Generic;
using CityX.CharacterEditor;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The baseball bat (owner, 2026-10-15: "put this baseball bat in as a two handed weapon", "give the baseball bat a wooden texture"): a
    /// TWO-HANDED blunt weapon, fought exactly like the greatsword (the same swings, guard, roll and two-handed hold), held at the knob and swung
    /// barrel first.
    ///
    /// The glb lies at 45 degrees, so it is MEASURED (HeldModelBaker): its long axis is the bat's, and the barrel is the end that is thicker on
    /// average. Scaled to <see cref="Length"/> (a big bat: the owner found a real 86 cm one at the game's 1.3x, 1.12 m, "two small", 2026-10-19), turned knob down and placed in the greatsword's frame: the
    /// bottom hand just over the knob, the root SwordWeapon.DefaultSupportGripDown above it, so it shares the greatsword's tuned two-handed hold.
    /// Unwrapped round its length (U round it, V along it; the seam's triangles get their own vertices) for the grain WoodGrain draws.
    /// </summary>
    public static class BaseballBatBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/BaseballBat.prefab";
        public const string SourcePath = "Assets/CityX/Weapons/baseballbat.glb";
        private const string MeshPath = "Assets/CityX/Weapons/BaseballBatMesh.asset";

        /// <summary>The bat end to end on the 2.6 m body, metres.</summary>
        public const float Length = 1.6f;

        /// <summary>The middle of the bottom hand above the knob's end, metres.</summary>
        public const float BottomHandAboveKnob = 0.11f;

        /// <summary>The share of the length at either end whose thickness tells the barrel from the handle.</summary>
        private const float EndShare = 0.3f;

        /// <summary>A light varnish over the grain.</summary>
        public const float VarnishSmoothness = 0.35f;

        [MenuItem("CityX/Rebuild Baseball Bat")]
        public static GameObject Build()
        {
            Mesh mesh = BakedMesh(out float reach);
            var root = new GameObject("BaseballBat");
            try
            {
                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;

                // White: the colour is the grain's (WoodGrain sets the texture when the bat appears).
                Material wood = CharacterSkin.Ensure("BaseballBatWood", Color.white);
                wood.SetFloat("_Metallic", 0.0f);
                wood.SetFloat("_Smoothness", VarnishSmoothness);
                wood.SetFloat("_Cull", 0.0f); // the glb is double-sided
                model.AddComponent<MeshRenderer>().sharedMaterial = wood;
                model.AddComponent<WoodGrain>();

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;
                pickupTrigger.center = new Vector3(0.0f, reach * 0.5f, 0.0f);

                SwordWeapon weapon = root.AddComponent<SwordWeapon>();
                weapon.ItemId = ItemRegistry.BaseballBat;
                weapon.OneHanded = false;
                weapon.BladeLength = reach;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The model measured, turned knob down, scaled, unwrapped and saved; <paramref name="reach"/> is how far the barrel's end stands above the root.</summary>
        public static Mesh BakedMesh(out float reach)
        {
            HeldModelBaker.Load(SourcePath, out Vector3[] vertices, out Vector3[] normals, out int[] triangles);

            Vector3 centre = HeldModelBaker.Centre(vertices);
            Vector3 axis = HeldModelBaker.LongAxis(vertices);
            HeldModelBaker.Extent(vertices, axis, out float min, out float max);
            float span = max - min;

            // The barrel is the end that is thicker on average: point the axis at it.
            if (MeanRadius(vertices, centre, axis, v => Vector3.Dot(v, axis) < min + span * EndShare) >
                MeanRadius(vertices, centre, axis, v => Vector3.Dot(v, axis) > max - span * EndShare))
            {
                axis = -axis;
                HeldModelBaker.Extent(vertices, axis, out min, out max);
            }
            float scale = Length / span;

            // The knob's end on the axis line; the bottom hand just over it; the root the support grip's drop above that.
            Vector3 knob = centre + axis * (min - Vector3.Dot(centre, axis));
            Vector3 origin = knob + axis * ((BottomHandAboveKnob + SwordWeapon.DefaultSupportGripDown) / scale);
            HeldModelBaker.Place(vertices, normals, origin, Quaternion.FromToRotation(axis, Vector3.up), scale);

            float bottom = float.MaxValue;
            reach = float.MinValue;
            foreach (Vector3 v in vertices)
            {
                bottom = Mathf.Min(bottom, v.y);
                reach = Mathf.Max(reach, v.y);
            }
            Vector2[] uv = Unwrap(ref vertices, ref normals, triangles, bottom, reach);
            return HeldModelBaker.Save(MeshPath, "BaseballBatMesh", vertices, normals, triangles, uv);
        }

        /// <summary>How far the points <paramref name="keep"/> accepts stand from the axis line through <paramref name="centre"/>, on average.</summary>
        private static float MeanRadius(Vector3[] points, Vector3 centre, Vector3 axis, System.Func<Vector3, bool> keep)
        {
            float sum = 0.0f;
            int count = 0;
            foreach (Vector3 p in points)
            {
                if (!keep(p))
                {
                    continue;
                }
                Vector3 off = p - centre;
                sum += (off - axis * Vector3.Dot(off, axis)).magnitude;
                count++;
            }
            return count > 0 ? sum / count : 0.0f;
        }

        /// <summary>
        /// U round the bat (from +X through +Z, 0..1), V along it (0 at the knob's end, 1 at the barrel's). A triangle that straddles the seam (its
        /// U spanning more than half the way round) gets copies of its vertices on the low side with U + 1: the texture repeats, so the grain runs on
        /// across the seam instead of squeezing a whole turn into one sliver.
        /// </summary>
        public static Vector2[] Unwrap(ref Vector3[] vertices, ref Vector3[] normals, int[] triangles, float bottom, float top)
        {
            var positions = new List<Vector3>(vertices);
            var norms = new List<Vector3>(normals);
            var uv = new List<Vector2>(vertices.Length);
            foreach (Vector3 v in vertices)
            {
                uv.Add(new Vector2(Mathf.Atan2(v.z, v.x) / (2.0f * Mathf.PI) + 0.5f, Mathf.InverseLerp(bottom, top, v.y)));
            }
            var wrapped = new Dictionary<int, int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                float a = uv[triangles[i]].x, b = uv[triangles[i + 1]].x, c = uv[triangles[i + 2]].x;
                if (Mathf.Max(a, Mathf.Max(b, c)) - Mathf.Min(a, Mathf.Min(b, c)) <= 0.5f)
                {
                    continue;
                }
                for (int k = i; k < i + 3; k++)
                {
                    int index = triangles[k];
                    if (uv[index].x >= 0.5f)
                    {
                        continue;
                    }
                    if (!wrapped.TryGetValue(index, out int copy))
                    {
                        copy = positions.Count;
                        positions.Add(positions[index]);
                        norms.Add(norms[index]);
                        uv.Add(new Vector2(uv[index].x + 1.0f, uv[index].y));
                        wrapped[index] = copy;
                    }
                    triangles[k] = copy;
                }
            }
            vertices = positions.ToArray();
            normals = norms.ToArray();
            return uv.ToArray();
        }
    }
}
