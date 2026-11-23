using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A SLASH OF LIGHT TRAILING FROM THE REAL BLADE (owner, 2026-10-12: "ideally the slashes would trail from the actual sword like the white slash
    /// trail"). Every frame, after the pose is on the arm, the blade's outer part is recorded, and a ribbon is drawn through where it has been for
    /// the last tenth of a second. The ribbon is wide and bright at the blade, narrowing onto the tip's own path and fading out at its tail. It is
    /// drawn in the sword spin's streak material.
    ///
    /// IN THE BODY'S OWN SPACE, carried along with it, so each cut stays a crescent round the body. Left in the world instead, a dash of 52 m/s
    /// smeared every cut into a long ribbon stretched back down the street.
    ///
    /// A fast cut moves the blade a hundred degrees in a frame. Joining two frames' blades with a straight band would draw a fan of straight
    /// edges, so the blade is TURNED between them (BladeTrailPath.Between) and the tip draws the arc it really swept.
    /// </summary>
    [DefaultExecutionOrder(32000)]   // after PlayerLocomotion's LateUpdate has laid this frame's pose on the arm the sword hangs from
    public sealed class BladeTrail : MonoBehaviour
    {
        /// <summary>How long a stretch of the blade's path stays lit: about the last three cuts of a flurry.</summary>
        public const float LifeSeconds = 0.1f;

        /// <summary>Blade positions turned in between two frames' (see the class comment).</summary>
        public const int StepsPerFrame = 8;

        /// <summary>The band runs from this far up the blade (0 the guard, 1 the tip) to the tip: the outer part, where a cut is fastest.</summary>
        public const float InnerFraction = 0.55f;

        /// <summary>How opaque the band is at the blade (additive light: overlapping cuts add up, and a whole one reads as a white sheet).</summary>
        public const float PeakAlpha = 0.7f;

        private readonly BladeTrailPath _path = new BladeTrailPath();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<Color> _colours = new List<Color>();
        private readonly List<int> _triangles = new List<int>();
        private ISwordBlade _blade;
        private bool _emitting;
        private GameObject _ribbon;
        private MeshRenderer _renderer;
        private Mesh _mesh;

        /// <summary>True while the blade is being followed.</summary>
        public bool IsEmitting => _emitting;

        /// <summary>How many blade positions the ribbon is drawn through now.</summary>
        public int SampleCount => _path.Count;

        /// <summary>Whether any of it is showing.</summary>
        public bool IsShowing => _renderer != null && _renderer.enabled;

        /// <summary>The ribbon's extent in the world (empty when nothing shows).</summary>
        public Bounds RibbonBounds => _renderer != null && _renderer.enabled ? _renderer.bounds : default;

        /// <summary>Starts a fresh trail behind <paramref name="blade"/>, drawn in <paramref name="material"/>. A null blade draws nothing.</summary>
        public void Begin(ISwordBlade blade, Material material)
        {
            _blade = blade;
            _emitting = blade != null;
            _path.Clear();
            if (_ribbon == null)
            {
                _ribbon = new GameObject("BladeTrail");
                _ribbon.transform.SetParent(transform, false);
                _mesh = new Mesh { name = "BladeTrail" };
                _mesh.MarkDynamic();
                _ribbon.AddComponent<MeshFilter>().sharedMesh = _mesh;
                _renderer = _ribbon.AddComponent<MeshRenderer>();
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
                _renderer.enabled = false;
            }
            _renderer.sharedMaterial = material;
        }

        /// <summary>Stops following the blade; what is lit fades out over its life.</summary>
        public void Stop()
        {
            _emitting = false;
        }

        private void LateUpdate()
        {
            float now = Time.time;
            if (_emitting && _blade is Object alive && alive != null)
            {
                Vector3 guard = transform.InverseTransformPoint(_blade.TrailBaseWorld);
                Vector3 tip = transform.InverseTransformPoint(_blade.TrailTipWorld);
                _path.Add(now, Vector3.Lerp(guard, tip, InnerFraction), tip, StepsPerFrame);
            }
            else if (_emitting)
            {
                _emitting = false;   // the sword is gone
            }
            _path.Trim(now, LifeSeconds);
            if (_mesh == null)
            {
                return;
            }
            _path.Build(now, LifeSeconds, PeakAlpha, _vertices, _uvs, _colours, _triangles);
            _mesh.Clear();
            bool showing = _vertices.Count >= 4;
            if (showing)
            {
                _mesh.SetVertices(_vertices);
                _mesh.SetUVs(0, _uvs);
                _mesh.SetColors(_colours);
                _mesh.SetTriangles(_triangles, 0);
                _mesh.RecalculateBounds();
            }
            _renderer.enabled = showing;
        }

        private void OnDestroy()
        {
            if (_ribbon != null)
            {
                Destroy(_ribbon);
            }
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }
    }

    /// <summary>The blade's recent path and the ribbon drawn through it: the plain logic under <see cref="BladeTrail"/>.</summary>
    public sealed class BladeTrailPath
    {
        public struct Sample
        {
            public float Time;
            public Vector3 Inner;
            public Vector3 Outer;
        }

        private readonly List<Sample> _samples = new List<Sample>();

        public int Count => _samples.Count;

        public Sample this[int index] => _samples[index];

        public void Clear()
        {
            _samples.Clear();
        }

        /// <summary>
        /// Adds the blade as it is at <paramref name="time"/>, and <paramref name="steps"/> - 1 blades turned part way to it from the last one, so the
        /// path between follows the arc the blade swung rather than the chord between its ends.
        /// </summary>
        public void Add(float time, Vector3 inner, Vector3 outer, int steps)
        {
            var now = new Sample { Time = time, Inner = inner, Outer = outer };
            if (_samples.Count > 0 && steps > 1)
            {
                Sample last = _samples[_samples.Count - 1];
                for (int i = 1; i < steps; i++)
                {
                    _samples.Add(Between(last, now, i / (float)steps));
                }
            }
            _samples.Add(now);
        }

        /// <summary>Below this turn between two frames the blade is only slid, not swung about a pivot.</summary>
        private const float SwingDegreesMin = 2.0f;

        /// <summary>
        /// The blade part way from <paramref name="a"/> to <paramref name="b"/>, swung as a rigid blade is: turned by the part of the turn between
        /// them about the one pivot that carries its inner end from where it was to where it is (the shoulder for a cut, the body's middle for a
        /// whole-body spin), so both its ends follow arcs. Sliding the inner end straight across instead drew a spin's ring of light as a polygon.
        /// </summary>
        public static Sample Between(Sample a, Sample b, float t)
        {
            float time = Mathf.Lerp(a.Time, b.Time, t);
            Vector3 alongA = a.Outer - a.Inner;
            Vector3 alongB = b.Outer - b.Inner;
            float length = Mathf.Lerp(alongA.magnitude, alongB.magnitude, t);
            Quaternion.FromToRotation(alongA, alongB).ToAngleAxis(out float angle, out Vector3 axis);
            if (angle < SwingDegreesMin || alongA.sqrMagnitude < 1e-8f || float.IsInfinity(axis.x))
            {
                Vector3 slid = Vector3.Lerp(a.Inner, b.Inner, t);
                return new Sample { Time = time, Inner = slid, Outer = slid + Vector3.Slerp(alongA, alongB, t) };
            }
            // In the plane across the turn's axis the inner end goes round a centre on the perpendicular bisector of its move; any move along
            // the axis is slid.
            Vector3 move = b.Inner - a.Inner;
            Vector3 axial = Vector3.Project(move, axis);
            Vector3 chord = move - axial;
            Vector3 centre = a.Inner;
            if (chord.sqrMagnitude > 1e-10f)
            {
                float fromMiddle = chord.magnitude * 0.5f / Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad);
                centre = a.Inner + chord * 0.5f + Vector3.Cross(axis, chord).normalized * fromMiddle;
            }
            Quaternion part = Quaternion.AngleAxis(angle * t, axis);
            Vector3 inner = centre + part * (a.Inner - centre) + axial * t;
            return new Sample { Time = time, Inner = inner, Outer = inner + part * alongA.normalized * length };
        }

        /// <summary>Forgets every position older than <paramref name="lifeSeconds"/>.</summary>
        public void Trim(float now, float lifeSeconds)
        {
            int old = 0;
            while (old < _samples.Count && _samples[old].Time < now - lifeSeconds)
            {
                old++;
            }
            if (old > 0)
            {
                _samples.RemoveRange(0, old);
            }
        }

        /// <summary>
        /// The ribbon through the path: two vertices a position, u along it (0 at its oldest, 1 at the blade) and v across it (0 inner, 1 outer). Its
        /// width shrinks onto the tip's own path toward the tail, and its colour fades from white at <paramref name="peakAlpha"/> at the blade to
        /// nothing at the tail (the additive streak material multiplies by it). Both sides are drawn.
        /// </summary>
        public void Build(float now, float lifeSeconds, float peakAlpha, List<Vector3> vertices, List<Vector2> uvs, List<Color> colours, List<int> triangles)
        {
            vertices.Clear();
            uvs.Clear();
            colours.Clear();
            triangles.Clear();
            int count = _samples.Count;
            if (count < 2)
            {
                return;
            }
            for (int i = 0; i < count; i++)
            {
                Sample s = _samples[i];
                float fresh = Mathf.Clamp01(1.0f - (now - s.Time) / lifeSeconds);
                float u = i / (float)(count - 1);
                float width = Mathf.Pow(fresh, 0.6f);
                vertices.Add(Vector3.Lerp(s.Outer, s.Inner, width));
                vertices.Add(s.Outer);
                uvs.Add(new Vector2(u, 0.0f));
                uvs.Add(new Vector2(u, 1.0f));
                var colour = new Color(1.0f, 1.0f, 1.0f, peakAlpha * fresh * fresh);
                colours.Add(colour);
                colours.Add(colour);
            }
            for (int i = 0; i < count - 1; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(b); triangles.Add(d); triangles.Add(c);
            }
        }
    }
}
