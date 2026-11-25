using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// THE HEAT PRESS'S SHAPE (docs/HEAT_PRESS.md; owner, 2026-11-24: "use this for the heat press model. I want parts of it to move", with a
    /// photograph of a clamshell press, and the CGTrader automatic flatbed press "as a guide for the opening and closing"). Made in code from
    /// boxes and short prisms, in three parts that move apart: the BASE (the black frame, its rails, the column at the back with its pivot,
    /// knob, switch and dial), the DRAWER (the lower platen: a silver plate, cream felt and the orange pad, sliding out along the rails to load
    /// a shirt) and the LID (the heating plate in its black frame with the V handle and rubber grip, turning on the pivot). Each part a mesh
    /// with one submesh per <see cref="Finish"/>. Sizes are the 2.6 m player's (real times 1.3); +z the front, where the user stands, y up,
    /// the origin under the middle of the base on the floor of whatever it stands on. The lid is made closed; it opens by turning about
    /// <see cref="Pivot"/> (front up).
    /// </summary>
    public static class HeatPressShape
    {
        /// <summary>The paints, in submesh order.</summary>
        public enum Finish { Frame, Plate, Heater, Felt, Pad, Rubber, Switch }

        public static readonly Color[] Colours =
        {
            new Color(0.09f, 0.09f, 0.10f),   // powder-coated black
            new Color(0.70f, 0.72f, 0.74f),   // the lower plate's bare aluminium
            new Color(0.90f, 0.86f, 0.86f),   // the heating plate's pale coating
            new Color(0.93f, 0.88f, 0.72f),   // felt
            new Color(0.88f, 0.40f, 0.28f),   // the silicone pad
            new Color(0.05f, 0.05f, 0.05f),   // the grip
            new Color(0.80f, 0.10f, 0.08f),   // the power switch
        };

        public static readonly float[] Smoothness = { 0.25f, 0.55f, 0.45f, 0.0f, 0.15f, 0.05f, 0.5f };

        /// <summary>Where the lid turns: the round boss through the column's top.</summary>
        public static readonly Vector3 Pivot = new Vector3(0.0f, 0.34f, -0.42f);

        /// <summary>How far the lid stands open, degrees (the photograph's).</summary>
        public const float OpenDegrees = 35.0f;

        /// <summary>How far the drawer slides out to load a shirt, metres.</summary>
        public const float DrawerTravel = 0.30f;

        /// <summary>The pad's top (closed, the heating plate rests on it): where a shirt lies.</summary>
        public const float PadTop = 0.118f;

        /// <summary>The pad's half width (x) and half depth (z).</summary>
        public static readonly Vector2 PadHalf = new Vector2(0.30f, 0.34f);

        public const int Sides = 12;

        public static Mesh Base()
        {
            var parts = new Parts();
            parts.Box(Finish.Frame, new Vector3(-0.24f, 0.0f, -0.48f), new Vector3(0.24f, 0.04f, 0.18f));   // the foot
            foreach (float x in new[] { -0.175f, 0.175f })
            {
                parts.Box(Finish.Frame, new Vector3(x - 0.025f, 0.0f, -0.48f), new Vector3(x + 0.025f, 0.05f, 0.36f));   // the rails the drawer runs on
            }
            parts.Box(Finish.Frame, new Vector3(-0.11f, 0.04f, -0.48f), new Vector3(0.11f, 0.38f, -0.36f));   // the column
            parts.Prism(Finish.Frame, Pivot + Vector3.left * 0.13f, Pivot + Vector3.right * 0.13f, 0.05f);   // the pivot's boss
            parts.Prism(Finish.Frame, new Vector3(0.0f, 0.38f, -0.44f), new Vector3(0.0f, 0.43f, -0.44f), 0.028f);   // the pressure knob
            parts.Box(Finish.Switch, new Vector3(0.11f, 0.10f, -0.445f), new Vector3(0.125f, 0.135f, -0.405f));   // the power switch
            parts.Prism(Finish.Rubber, new Vector3(0.11f, 0.22f, -0.42f), new Vector3(0.13f, 0.22f, -0.42f), 0.022f);   // the dial
            return parts.Mesh("HeatPressBase");
        }

        /// <summary>The lower platen, in its place under the lid; it slides out along +z.</summary>
        public static Mesh Drawer()
        {
            var parts = new Parts();
            parts.Box(Finish.Frame, new Vector3(-0.14f, 0.05f, -0.30f), new Vector3(0.14f, 0.075f, 0.28f));
            parts.Box(Finish.Plate, new Vector3(-PadHalf.x, 0.075f, -PadHalf.y), new Vector3(PadHalf.x, 0.095f, PadHalf.y));
            parts.Box(Finish.Felt, new Vector3(-PadHalf.x, 0.095f, -PadHalf.y), new Vector3(PadHalf.x, 0.104f, PadHalf.y));
            parts.Box(Finish.Pad, new Vector3(-PadHalf.x, 0.104f, -PadHalf.y), new Vector3(PadHalf.x, PadTop, PadHalf.y));
            return parts.Mesh("HeatPressDrawer");
        }

        /// <summary>The lid, closed, in the press's frame (its transform turns it about <see cref="Pivot"/>).</summary>
        public static Mesh Lid()
        {
            var parts = new Parts();
            parts.Box(Finish.Heater, new Vector3(-0.305f, PadTop, -0.345f), new Vector3(0.305f, 0.124f, 0.345f));
            parts.Box(Finish.Frame, new Vector3(-0.31f, 0.124f, -0.35f), new Vector3(0.31f, 0.17f, 0.35f));
            parts.Box(Finish.Frame, new Vector3(-0.07f, 0.15f, -0.46f), new Vector3(0.07f, 0.36f, -0.30f));   // the arm up to the pivot
            // The V handle: two flat bars from the lid's back rising forward, the grip across their tops.
            const float bar = 0.52f;   // the photograph's handle stands about as tall as the plate is deep
            float rise = bar * Mathf.Sin(58.0f * Mathf.Deg2Rad), reach = bar * Mathf.Cos(58.0f * Mathf.Deg2Rad);
            foreach (float side in new[] { -1.0f, 1.0f })
            {
                parts.Beam(Finish.Frame, new Vector3(side * 0.05f, 0.17f, -0.27f), new Vector3(side * 0.10f, 0.17f + rise, -0.27f + reach), 0.045f, 0.032f);
            }
            Vector3 grip = new Vector3(0.0f, 0.17f + rise, -0.27f + reach);
            parts.Prism(Finish.Rubber, grip + Vector3.left * 0.15f, grip + Vector3.right * 0.15f, 0.034f);
            return parts.Mesh("HeatPressLid");
        }

        /// <summary>The top of the plain steel work table it stands on (real 73 cm, times 1.3).</summary>
        public const float TableHeight = 0.95f;

        /// <summary>The table's half width (x) and half depth (z): room round the press, and for the drawer out front.</summary>
        public static readonly Vector2 TableHalf = new Vector2(0.55f, 0.45f);

        /// <summary>The work table, its origin on the floor under its middle; the press stands on it at <see cref="TableHeight"/>, set back.</summary>
        public static Mesh Table()
        {
            var parts = new Parts();
            parts.Box(Finish.Plate, new Vector3(-TableHalf.x, TableHeight - 0.04f, -TableHalf.y), new Vector3(TableHalf.x, TableHeight, TableHalf.y));
            foreach (float x in new[] { -1.0f, 1.0f })
            {
                foreach (float z in new[] { -1.0f, 1.0f })
                {
                    var corner = new Vector3(x * (TableHalf.x - 0.05f), 0.0f, z * (TableHalf.y - 0.05f));
                    parts.Box(Finish.Frame, corner - new Vector3(0.03f, 0.0f, 0.03f), corner + new Vector3(0.03f, TableHeight - 0.04f, 0.03f));
                }
            }
            parts.Box(Finish.Frame, new Vector3(-TableHalf.x + 0.05f, 0.18f, -TableHalf.y + 0.03f), new Vector3(TableHalf.x - 0.05f, 0.21f, TableHalf.y - 0.03f));   // the shelf's rail
            return parts.Mesh("HeatPressTable");
        }

        /// <summary>Where the press stands on the table: on its top, its column clear of the back edge.</summary>
        public static readonly Vector3 OnTheTable = new Vector3(0.0f, TableHeight, 0.04f);

        /// <summary>The lid's turn about <see cref="Pivot"/> when it stands open by <paramref name="degrees"/>: front up.</summary>
        public static Quaternion LidTurn(float degrees) => Quaternion.AngleAxis(-degrees, Vector3.right);

        /// <summary>Gathers faces by finish into one mesh.</summary>
        private sealed class Parts
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<int>[] _triangles = new List<int>[Colours.Length];

            public Parts()
            {
                for (int i = 0; i < _triangles.Length; i++)
                {
                    _triangles[i] = new List<int>();
                }
            }

            private void Quad(Finish finish, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int start = _vertices.Count;
                _vertices.AddRange(new[] { a, b, c, d });
                _normals.AddRange(new[] { normal, normal, normal, normal });
                _triangles[(int)finish].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            /// <summary>An axis-aligned box, every face out.</summary>
            public void Box(Finish finish, Vector3 min, Vector3 max)
            {
                Corners(finish, Vector3.Lerp(min, max, 0.5f), Vector3.right * (max.x - min.x) * 0.5f, Vector3.up * (max.y - min.y) * 0.5f,
                    Vector3.forward * (max.z - min.z) * 0.5f);
            }

            /// <summary>A bar of <paramref name="width"/> (across x) and <paramref name="thickness"/> from <paramref name="a"/> to <paramref name="b"/>.</summary>
            public void Beam(Finish finish, Vector3 a, Vector3 b, float width, float thickness)
            {
                Vector3 along = (b - a) * 0.5f;
                Vector3 side = Vector3.right * width * 0.5f;
                Vector3 depth = Vector3.Cross(along.normalized, Vector3.right).normalized * thickness * 0.5f;
                Corners(finish, (a + b) * 0.5f, side, along, depth);
            }

            /// <summary>A box about <paramref name="centre"/> with half-axes x, y, z (any directions; a right-handed set).</summary>
            private void Corners(Finish finish, Vector3 centre, Vector3 x, Vector3 y, Vector3 z)
            {
                Vector3 P(float i, float j, float k) => centre + x * i + y * j + z * k;
                bool flip = Vector3.Dot(Vector3.Cross(x, y), z) < 0.0f;
                void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
                {
                    if (flip) Quad(finish, d, c, b, a);
                    else Quad(finish, a, b, c, d);
                }
                Face(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));       // +x
                Face(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1));   // -x
                Face(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));       // +y
                Face(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1));   // -y
                Face(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1));       // +z
                Face(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1));   // -z
            }

            /// <summary>A round bar (<see cref="Sides"/> flat sides) from <paramref name="a"/> to <paramref name="b"/>, capped.</summary>
            public void Prism(Finish finish, Vector3 a, Vector3 b, float radius)
            {
                Vector3 axis = (b - a).normalized;
                Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 v = Vector3.Cross(axis, u);
                for (int i = 0; i < Sides; i++)
                {
                    float t0 = i * Mathf.PI * 2.0f / Sides, t1 = (i + 1) * Mathf.PI * 2.0f / Sides;
                    Vector3 r0 = (u * Mathf.Cos(t0) + v * Mathf.Sin(t0)) * radius, r1 = (u * Mathf.Cos(t1) + v * Mathf.Sin(t1)) * radius;
                    Quad(finish, a + r1, b + r1, b + r0, a + r0);
                    TriangleFan(finish, b, b + r0, b + r1);
                    TriangleFan(finish, a, a + r1, a + r0);
                }
            }

            private void TriangleFan(Finish finish, Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int start = _vertices.Count;
                _vertices.AddRange(new[] { a, b, c });
                _normals.AddRange(new[] { normal, normal, normal });
                _triangles[(int)finish].AddRange(new[] { start, start + 1, start + 2 });
            }

            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.subMeshCount = _triangles.Length;
                for (int i = 0; i < _triangles.Length; i++)
                {
                    mesh.SetTriangles(_triangles[i], i);
                }
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
