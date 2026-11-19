using System.Collections.Generic;
using CityX.Wearables;
using CityX.WeaponsEditor;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds RiotShield.prefab (owner, 2026-11-18: "make a riot shield"; docs/SHIELD.md), drawn in code, a stand-in until the owner's model: a tall
    /// dark slab bowed round its bearer, a paler window across its upper part, and a grip bar on its back. Its face looks along +z, its top along +y;
    /// the body places it (PlayerLocomotion.Shield.cs). Sized for this body (2.6 m: as the old Call of Duty riot shield it hides a standing body from the shins to the top of the head, the window before the face (owner, 2026-11-18)).
    /// </summary>
    public static class RiotShieldBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/RiotShield.prefab";
        private const string BodyMeshPath = "Assets/CityX/Wearables/RiotShieldBody.asset";
        private const string WindowMeshPath = "Assets/CityX/Wearables/RiotShieldWindow.asset";
        private const string BodyMaterialPath = "Assets/CityX/Wearables/RiotShieldBody.mat";
        private const string WindowMaterialPath = "Assets/CityX/Wearables/RiotShieldWindow.mat";

        public const float Width = 0.95f;
        public const float Height = 1.95f;
        public const float Thickness = 0.03f;

        /// <summary>How far its middle stands forward of its side edges: bowed round the bearer.</summary>
        public const float Bow = 0.07f;

        /// <summary>The window: this wide, from this far above the middle up to this far above it.</summary>
        public const float WindowWidth = 0.7f, WindowBottom = 0.42f, WindowTop = 0.86f;

        private const float WindowProud = 0.004f;
        private const int Columns = 10;

        [MenuItem("CityX/Rebuild Riot Shield")]
        public static GameObject Build()
        {
            Mesh body = Save(BodyMeshPath, "RiotShieldBody", Body());
            Mesh window = Save(WindowMeshPath, "RiotShieldWindow", Window());
            Material bodyMaterial = HeldModelBaker.LitMaterial(BodyMaterialPath, new Color(0.07f, 0.08f, 0.09f), 0.1f, 0.55f);
            Material windowMaterial = HeldModelBaker.LitMaterial(WindowMaterialPath, new Color(0.42f, 0.5f, 0.55f), 0.0f, 0.9f);

            var root = new GameObject("RiotShield");
            try
            {
                var model = new GameObject(HeldShield.ModelName);
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = body;
                model.AddComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
                var pane = new GameObject("Window");
                pane.transform.SetParent(root.transform, false);
                pane.AddComponent<MeshFilter>().sharedMesh = window;
                pane.AddComponent<MeshRenderer>().sharedMaterial = windowMaterial;

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = GroundWearable.InteractRange;
                root.AddComponent<RiotShield>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: RiotShield written to {PrefabPath} -- {(body.triangles.Length + window.triangles.Length) / 3} triangles");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>How far forward the face is at <paramref name="x"/> across it: the bow, 0 at the side edges.</summary>
        public static float FaceAt(float x) => Bow * (1.0f - (2.0f * x / Width) * (2.0f * x / Width));

        /// <summary>Flat-shaded: every face has its own corners.</summary>
        private sealed class Shape
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int first = Vertices.Count;
                Vertices.AddRange(new[] { a, b, c, d });
                Triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
            }

            /// <summary>A bowed sheet from <paramref name="left"/> to <paramref name="right"/> and <paramref name="bottom"/> to <paramref name="top"/>: its front, its back and its four edges.</summary>
            public void Sheet(float left, float right, float bottom, float top, float front, float back)
            {
                for (int column = 0; column < Columns; column++)
                {
                    float x0 = Mathf.Lerp(left, right, column / (float)Columns), x1 = Mathf.Lerp(left, right, (column + 1) / (float)Columns);
                    float z0 = FaceAt(x0), z1 = FaceAt(x1);
                    Quad(new Vector3(x0, bottom, z0 + front), new Vector3(x0, top, z0 + front), new Vector3(x1, top, z1 + front), new Vector3(x1, bottom, z1 + front));
                    Quad(new Vector3(x1, bottom, z1 + back), new Vector3(x1, top, z1 + back), new Vector3(x0, top, z0 + back), new Vector3(x0, bottom, z0 + back));
                    Quad(new Vector3(x0, top, z0 + front), new Vector3(x0, top, z0 + back), new Vector3(x1, top, z1 + back), new Vector3(x1, top, z1 + front));
                    Quad(new Vector3(x0, bottom, z0 + back), new Vector3(x0, bottom, z0 + front), new Vector3(x1, bottom, z1 + front), new Vector3(x1, bottom, z1 + back));
                }
                float zl = FaceAt(left), zr = FaceAt(right);
                Quad(new Vector3(left, bottom, zl + back), new Vector3(left, top, zl + back), new Vector3(left, top, zl + front), new Vector3(left, bottom, zl + front));
                Quad(new Vector3(right, bottom, zr + front), new Vector3(right, top, zr + front), new Vector3(right, top, zr + back), new Vector3(right, bottom, zr + back));
            }
        }

        /// <summary>The slab, and the grip bar standing off its back where the fist holds it (ShieldTraits.Riot.GripAboveMiddle above the middle).</summary>
        private static Shape Body()
        {
            var shape = new Shape();
            shape.Sheet(-Width * 0.5f, Width * 0.5f, -Height * 0.5f, Height * 0.5f, 0.0f, -Thickness);
            float grip = CityX.Character.ShieldTraits.Riot.GripAboveMiddle;
            const float barHalfWidth = 0.13f, barHalfHeight = 0.02f, barOut = 0.09f, barDeep = 0.03f;
            float back = FaceAt(0.0f) - Thickness;
            Vector3 lo = new Vector3(-barHalfWidth, grip - barHalfHeight, back - barOut - barDeep), hi = new Vector3(barHalfWidth, grip + barHalfHeight, back - barOut);
            Box(shape, lo, hi);
            // Its two posts back to the slab.
            Box(shape, new Vector3(-barHalfWidth, grip - barHalfHeight, back - barOut), new Vector3(-barHalfWidth + 0.03f, grip + barHalfHeight, back));
            Box(shape, new Vector3(barHalfWidth - 0.03f, grip - barHalfHeight, back - barOut), new Vector3(barHalfWidth, grip + barHalfHeight, back));
            return shape;
        }

        /// <summary>The window: a thin sheet a hair proud of the slab on both its faces.</summary>
        private static Shape Window()
        {
            var shape = new Shape();
            shape.Sheet(-WindowWidth * 0.5f, WindowWidth * 0.5f, WindowBottom, WindowTop, WindowProud, -Thickness - WindowProud);
            return shape;
        }

        private static void Box(Shape shape, Vector3 lo, Vector3 hi)
        {
            Vector3 a = new Vector3(lo.x, lo.y, lo.z), b = new Vector3(hi.x, lo.y, lo.z), c = new Vector3(hi.x, hi.y, lo.z), d = new Vector3(lo.x, hi.y, lo.z);
            Vector3 e = new Vector3(lo.x, lo.y, hi.z), f = new Vector3(hi.x, lo.y, hi.z), g = new Vector3(hi.x, hi.y, hi.z), h = new Vector3(lo.x, hi.y, hi.z);
            shape.Quad(b, c, d, a);   // back (-z)
            shape.Quad(e, h, g, f);   // front (+z)
            shape.Quad(a, d, h, e);   // left
            shape.Quad(f, g, c, b);   // right
            shape.Quad(d, c, g, h);   // top
            shape.Quad(a, e, f, b);   // bottom
        }

        private static Mesh Save(string path, string name, Shape shape)
        {
            var scratch = new Mesh { vertices = shape.Vertices.ToArray(), triangles = shape.Triangles.ToArray() };
            scratch.RecalculateNormals();
            Mesh saved = HeldModelBaker.Save(path, name, scratch.vertices, scratch.normals, scratch.triangles);
            Object.DestroyImmediate(scratch);
            return saved;
        }
    }
}
