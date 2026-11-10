using System.Collections.Generic;
using CityX.Core;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The conveyor belt a character is made on (docs/BELT_INTRO.md): a long run of rubber slats over a grey steel frame, ending in a drum the
    /// slats turn down round. Nothing holds it up: it hangs in the open sky (owner, 2026-11-10: "no ladder holding up the conveyer
    /// belt"). Built in code when it wakes, from its numbers alone. In its own frame it
    /// runs along +X from 0 to <see cref="Length"/>, its top at y = 0. <see cref="Travel"/> is how far its surface has run: the slats
    /// are laid out again from it and the hubs along the frame turn with it.
    /// </summary>
    public sealed class ConveyorBelt : MonoBehaviour
    {
        /// <summary>From its far end (never seen) to the middle of the drum at its near end, metres.</summary>
        public const float Length = 60.0f;

        /// <summary>Across, metres: a band under the body's feet, seen from the front.</summary>
        public const float Width = 2.0f;

        public const float DrumRadius = 0.28f;

        /// <summary>How far under the slats the frame's lower edge is, and how far out from the belt's middle its side faces are, metres.</summary>
        public const float FrameBottom = -DrumRadius * 2.0f + 0.04f;

        public const float FrameOutside = Width * 0.5f + 0.13f;

        /// <summary>One slat and the gap after it, along the belt; the gap; and how thick the rubber is.</summary>
        public const float SlatPitch = 1.1f;

        public const float SlatGap = 0.035f;
        public const float SlatThickness = 0.05f;

        /// <summary>A hub on the frame every so far, and how much of the belt's near end has them (the rest is never seen).</summary>
        public const float HubSpacing = 1.4f;

        public const float HubbedLength = 38.0f;
        public const float HubRadius = 0.13f;


        private const int ArcSteps = 8;

        private static readonly Color Rubber = new Color(0.33f, 0.35f, 0.39f);
        private static readonly Color Bed = new Color(0.04f, 0.04f, 0.05f);
        private static readonly Color Frame = new Color(0.60f, 0.62f, 0.66f);

        private static readonly Color HubDark = new Color(0.10f, 0.10f, 0.12f);

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<Transform> _hubs = new List<Transform>();
        private readonly List<Object> _made = new List<Object>();
        private Mesh _slats;
        private float _travel;
        private float _laidOutAt = float.NaN;

        /// <summary>How far the belt's surface has run toward its end, metres (it may run backwards).</summary>
        public float Travel
        {
            get => _travel;
            set
            {
                _travel = value;
                LayOut();
            }
        }

        private void Awake()
        {
            Build();
        }

        private void OnDestroy()
        {
            foreach (Object made in _made)
            {
                if (made != null)
                {
                    Destroy(made);
                }
            }
        }

        /// <summary>Makes the belt if it has not been made. Awake does; public for a render made outside play.</summary>
        public void Build()
        {
            if (_slats != null)
            {
                return;
            }
            float half = Width * 0.5f;
            var parts = new BoxMesh();

            // The bed under the top run: what shows through the gaps between the slats.
            parts.Box(new Vector3(0.0f, -SlatThickness - 0.06f, -half + 0.03f), new Vector3(Length - 0.05f, -SlatThickness - 0.005f, half - 0.03f));
            AddPart("Bed", parts.ToMesh(_made), Bed);

            // The frame: a rail down each side, rounded off at the drum.
            parts.Clear();
            foreach (float side in new[] { -1.0f, 1.0f })
            {
                float inner = side * (half + 0.03f), outer = side * FrameOutside;
                parts.Box(new Vector3(0.0f, FrameBottom, Mathf.Min(inner, outer)), new Vector3(Length, -0.06f, Mathf.Max(inner, outer)));
                parts.Disc(new Vector3(Length, -DrumRadius, 0.0f), DrumRadius - 0.06f, Mathf.Min(inner, outer), Mathf.Max(inner, outer), 20);
            }
            AddPart("Frame", parts.ToMesh(_made), Frame);

            // The hubs on the frame's two faces, one mesh shared: a dark disc with a pale bar across it, so its turning shows.
            parts.Clear();
            parts.Disc(Vector3.zero, HubRadius, -0.02f, 0.02f, 16);
            Mesh hub = parts.ToMesh(_made);
            parts.Clear();
            parts.Box(new Vector3(-HubRadius * 0.85f, -0.022f, -0.028f), new Vector3(HubRadius * 0.85f, 0.022f, 0.028f));
            Mesh bar = parts.ToMesh(_made);
            Material dark = Paint(HubDark), pale = Paint(Frame * 1.05f);
            for (float x = Length; x > Length - HubbedLength; x -= HubSpacing)
            {
                foreach (float side in new[] { -1.0f, 1.0f })
                {
                    var holder = new GameObject("Hub");
                    holder.layer = gameObject.layer;
                    holder.transform.SetParent(transform, false);
                    holder.transform.localPosition = new Vector3(x, -DrumRadius, side * (half + 0.14f));
                    AddMesh(holder, hub, dark);
                    var marker = new GameObject("Bar");
                    marker.layer = gameObject.layer;
                    marker.transform.SetParent(holder.transform, false);
                    AddMesh(marker, bar, pale);
                    _hubs.Add(holder.transform);
                }
            }

            _slats = new Mesh { name = "BeltSlats", hideFlags = HideFlags.DontSave };
            _slats.MarkDynamic();
            _made.Add(_slats);
            AddPart("Slats", _slats, Rubber);

            // What a limp body lands on and slides off: the top run and the drum.
            var top = gameObject.AddComponent<BoxCollider>();
            top.center = new Vector3(Length * 0.5f, -DrumRadius, 0.0f);
            top.size = new Vector3(Length, DrumRadius * 2.0f, Width);
            var drum = gameObject.AddComponent<CapsuleCollider>();
            drum.direction = 2;
            drum.center = new Vector3(Length, -DrumRadius, 0.0f);
            drum.radius = DrumRadius;
            drum.height = Width + DrumRadius * 2.0f;

            LayOut();
        }

        private void AddPart(string partName, Mesh mesh, Color colour)
        {
            var part = new GameObject(partName);
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            AddMesh(part, mesh, Paint(colour));
        }

        private static void AddMesh(GameObject onto, Mesh mesh, Material material)
        {
            onto.AddComponent<MeshFilter>().sharedMesh = mesh;
            onto.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private Material Paint(Color colour)
        {
            var material = new Material(LitShader.Find()) { hideFlags = HideFlags.DontSave };
            material.SetColor("_BaseColor", colour);
            LitShader.MakeMatte(material);
            _made.Add(material);
            return material;
        }

        /// <summary>Whether anything can land on it. Off once the body has been rolled off: a limb caught on the end hooks the whole fall.</summary>
        public void SetSolid(bool solid)
        {
            foreach (Collider part in GetComponents<Collider>())
            {
                part.enabled = solid;
            }
        }

        // --- the slats ---

        /// <summary>The whole way round: along the top, down round the drum, and back underneath.</summary>
        private static float Around => Length * 2.0f + Mathf.PI * DrumRadius;

        /// <summary>The belt's surface <paramref name="along"/> its way round (0 at the far end of the top run), and the way it faces there.</summary>
        public static void Surface(float along, out Vector3 at, out Vector3 facing)
        {
            if (along <= Length)
            {
                at = new Vector3(along, 0.0f, 0.0f);
                facing = Vector3.up;
                return;
            }
            float arc = Mathf.PI * DrumRadius;
            if (along < Length + arc)
            {
                float angle = Mathf.PI * 0.5f - (along - Length) / DrumRadius;
                facing = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0.0f);
                at = new Vector3(Length, -DrumRadius, 0.0f) + facing * DrumRadius;
                return;
            }
            at = new Vector3(Length - (along - Length - arc), -DrumRadius * 2.0f, 0.0f);
            facing = Vector3.down;
        }

        /// <summary>The slats where <see cref="Travel"/> has them, and the hubs turned as far.</summary>
        private void LayOut()
        {
            if (_slats == null || Mathf.Approximately(_travel, _laidOutAt))
            {
                return;
            }
            _laidOutAt = _travel;
            _vertices.Clear();
            _normals.Clear();
            _triangles.Clear();

            float around = Around;
            float shift = Mathf.Repeat(_travel, SlatPitch);
            float arcFrom = Length, arcTo = Length + Mathf.PI * DrumRadius;
            float arcStep = (arcTo - arcFrom) / ArcSteps;
            for (float from = shift - SlatPitch; from < around; from += SlatPitch)
            {
                float a = Mathf.Max(0.0f, from), b = Mathf.Min(around, from + SlatPitch - SlatGap);
                if (b - a < 0.01f)
                {
                    continue;
                }
                // Straight where it lies flat, in short pieces where it turns round the drum.
                float at = a;
                while (at < b - 1e-4f)
                {
                    float next = b;
                    if (at < arcFrom)
                    {
                        next = Mathf.Min(b, arcFrom);
                    }
                    else if (at < arcTo - 1e-4f)
                    {
                        next = Mathf.Min(b, Mathf.Min(arcTo, arcFrom + (Mathf.Floor((at - arcFrom) / arcStep + 1e-3f) + 1.0f) * arcStep));
                    }
                    Piece(at, next);
                    at = next;
                }
            }
            _slats.Clear();
            _slats.SetVertices(_vertices);
            _slats.SetNormals(_normals);
            _slats.SetTriangles(_triangles, 0);
            _slats.bounds = new Bounds(new Vector3(Length * 0.5f, -DrumRadius, 0.0f), new Vector3(Length + 2.0f, 2.0f, Width + 1.0f));

            float turned = -_travel / DrumRadius * Mathf.Rad2Deg;
            for (int i = 0; i < _hubs.Count; i++)
            {
                _hubs[i].localRotation = Quaternion.Euler(0.0f, 0.0f, turned);
            }
        }

        /// <summary>A piece of one slat, between two places along the belt's way round: its outer face and its two edges.</summary>
        private void Piece(float from, float to)
        {
            Surface(from, out Vector3 a, out Vector3 aFacing);
            Surface(to, out Vector3 b, out Vector3 bFacing);
            Vector3 across = Vector3.forward * (Width * 0.5f);
            Quad(a - across, a + across, b + across, b - across, aFacing, aFacing, bFacing, bFacing);
            Vector3 aInner = a - aFacing * SlatThickness, bInner = b - bFacing * SlatThickness;
            Quad(a + across, aInner + across, bInner + across, b + across, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward);
            Quad(b - across, bInner - across, aInner - across, a - across, Vector3.back, Vector3.back, Vector3.back, Vector3.back);
        }

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd)
        {
            // Wound to face the way its normals say, whichever way round the corners came.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), na) < 0.0f)
            {
                (b, d) = (d, b);
                (nb, nd) = (nd, nb);
            }
            int first = _vertices.Count;
            _vertices.Add(a);
            _vertices.Add(b);
            _vertices.Add(c);
            _vertices.Add(d);
            _normals.Add(na);
            _normals.Add(nb);
            _normals.Add(nc);
            _normals.Add(nd);
            _triangles.Add(first);
            _triangles.Add(first + 1);
            _triangles.Add(first + 2);
            _triangles.Add(first);
            _triangles.Add(first + 2);
            _triangles.Add(first + 3);
        }

        /// <summary>Boxes and discs gathered into one mesh, flat shaded.</summary>
        private sealed class BoxMesh
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();

            public void Clear()
            {
                _vertices.Clear();
                _normals.Clear();
                _triangles.Clear();
            }

            public void Box(Vector3 min, Vector3 max)
            {
                Vector3 a = new Vector3(min.x, min.y, min.z), b = new Vector3(max.x, min.y, min.z), c = new Vector3(max.x, max.y, min.z),
                    d = new Vector3(min.x, max.y, min.z), e = new Vector3(min.x, min.y, max.z), f = new Vector3(max.x, min.y, max.z),
                    g = new Vector3(max.x, max.y, max.z), h = new Vector3(min.x, max.y, max.z);
                Face(a, d, c, b, Vector3.back);
                Face(e, f, g, h, Vector3.forward);
                Face(a, e, h, d, Vector3.left);
                Face(b, c, g, f, Vector3.right);
                Face(d, h, g, c, Vector3.up);
                Face(a, b, f, e, Vector3.down);
            }

            /// <summary>A disc on its edge (its axle along Z), from <paramref name="z0"/> to <paramref name="z1"/>.</summary>
            public void Disc(Vector3 centre, float radius, float z0, float z1, int sides)
            {
                Vector3 front = centre + Vector3.forward * z1, back = centre + Vector3.forward * z0;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2.0f / sides, a1 = (i + 1) * Mathf.PI * 2.0f / sides;
                    Vector3 r0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0.0f) * radius, r1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0.0f) * radius;
                    Triangle(front, front + r1, front + r0, Vector3.forward);
                    Triangle(back, back + r0, back + r1, Vector3.back);
                    Face(back + r0, front + r0, front + r1, back + r1, (r0 + r1).normalized);
                }
            }

            private void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                Triangle(a, b, c, normal);
                Triangle(a, c, d, normal);
            }

            private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
            {
                // Wound to face the way its normal says, whichever way round the corners came.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0.0f)
                {
                    (b, c) = (c, b);
                }
                int first = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _normals.Add(normal);
                _normals.Add(normal);
                _normals.Add(normal);
                _triangles.Add(first);
                _triangles.Add(first + 1);
                _triangles.Add(first + 2);
            }

            public Mesh ToMesh(List<Object> made)
            {
                var mesh = new Mesh { hideFlags = HideFlags.DontSave };
                mesh.indexFormat = _vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                made.Add(mesh);
                return mesh;
            }
        }
    }
}
