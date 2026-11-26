using System.Collections.Generic;
using CityX.Core;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THREE STREAKS OF LIGHT BEHIND A CLAWING HAND (owner, 2026-11-25, with a picture of three curved white slashes glowing warm at their
    /// edges, a few sparks off them: "give squirrel boy a claw attack with a trail that looks like the image"). Three claws stand out from the
    /// knuckles, spread across the hand; each draws a thin streak along the path it cuts, white in the middle and glowing peach at its edges
    /// (the material's), tapering to a point at both ends, so the whole cut reads as three crescents. The streaks start only when the claws move
    /// fast (the arm drawing back draws nothing) and fade away soon after the cut, throwing a few sparks as they go.
    ///
    /// In the body's own space, so a cut stays where it was cut round a body that moves on (as BladeTrail's). Each streak is turned to face the
    /// camera, so it is a line of light from any side.
    /// </summary>
    [DefaultExecutionOrder(32000)]   // after PlayerLocomotion's LateUpdate has laid this frame's pose on the arm
    public sealed class ClawTrail : MonoBehaviour
    {
        public const int Claws = 3;

        /// <summary>How far the claws reach past the knuckles, metres at the body's scale (a squirrel's: a finger and a claw).</summary>
        public const float ClawReach = 0.17f;

        /// <summary>How far apart the claws stand across the hand, metres at the body's scale.</summary>
        public const float ClawSpread = 0.1f;

        /// <summary>How broad a streak's glow is at its widest, metres at the body's scale (the white core is the middle of it).</summary>
        public const float Width = 0.1f;

        /// <summary>Faster than this a claw cuts and draws, metres a second at the body's scale.</summary>
        public const float CutSpeed = 2.0f;

        /// <summary>How long the streaks take to fade once the cut is over, seconds.</summary>
        public const float FadeSeconds = 0.35f;

        /// <summary>
        /// How much of the claws' path a streak shows while they cut, metres at the body's scale: the last stretch, a crescent that runs after
        /// the hand and is left where the cut ended. By length, not time: the Wraith's punch is a whip, and its last tenth of a second alone
        /// swept a metre and a half, from behind the head down past the hip; the owner's picture has short crescents.
        /// </summary>
        public const float StreakMetres = 1.0f;

        /// <summary>Points put between two frames' claws (on a curve through them), so a fast cut draws a curve, not a polyline.</summary>
        public const int StepsPerFrame = 4;

        /// <summary>How many flecks of light come off the streaks as the cut ends.</summary>
        public const int Sparks = 7;

        private static readonly string[] Fingers = { "index", "middle", "ring" };

        private readonly List<Vector3>[] _paths = { new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };
        private readonly Vector3[] _lastWorld = new Vector3[Claws];
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<Color> _colours = new List<Color>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<Vector3> _smooth = new List<Vector3>();
        private Transform _hand;
        private Transform _indexRoot;
        private Transform _middleRoot;
        private Transform _middleTip;
        private Transform _ringRoot;
        private bool _following;
        private bool _drawing;
        private bool _hadLast;
        private float _stoppedAt = -1.0f;
        private GameObject _ribbon;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private ParticleSystem _sparks;

        /// <summary>Whether the claws are being followed.</summary>
        public bool IsFollowing => _following;

        /// <summary>Whether any streak shows now.</summary>
        public bool IsShowing => _renderer != null && _renderer.enabled;

        /// <summary>How many streaks have been drawn (each claw's, with at least two points).</summary>
        public int Streaks
        {
            get
            {
                int count = 0;
                foreach (List<Vector3> path in _paths)
                {
                    count += path.Count >= 2 ? 1 : 0;
                }
                return count;
            }
        }

        /// <summary>The streaks' extent in the world (empty when nothing shows).</summary>
        public Bounds Extent => IsShowing ? _renderer.bounds : default;

        /// <summary>Where each claw's tip is now, in the world (for tests and the hit).</summary>
        public Vector3 ClawTip(int claw)
        {
            FindTheHand();
            return Tip(claw);
        }

        /// <summary>Starts following the claws of the right hand, drawing the streaks in <paramref name="material"/> and the sparks in <paramref name="sparkMaterial"/>.</summary>
        public void Begin(Material material, Material sparkMaterial)
        {
            FindTheHand();
            foreach (List<Vector3> path in _paths)
            {
                path.Clear();
            }
            _following = _hand != null;
            _drawing = false;
            _hadLast = false;
            _stoppedAt = -1.0f;
            if (_ribbon == null)
            {
                _ribbon = new GameObject("ClawTrail");
                _ribbon.transform.SetParent(transform, false);
                _mesh = new Mesh { name = "ClawTrail" };
                _mesh.MarkDynamic();
                _ribbon.AddComponent<MeshFilter>().sharedMesh = _mesh;
                _renderer = _ribbon.AddComponent<MeshRenderer>();
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
                _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _renderer.enabled = false;
                _sparks = MakeSparks(_ribbon.transform);
            }
            _renderer.sharedMaterial = material;
            _sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkMaterial != null ? sparkMaterial : material;
        }

        /// <summary>The cut is over: the streaks fade and the sparks fly.</summary>
        public void Stop()
        {
            if (!_following)
            {
                return;
            }
            _following = false;
            _stoppedAt = Time.time;
            ThrowSparks();
        }

        private float Scale => Mathf.Max(0.2f, transform.lossyScale.y);

        private void FindTheHand()
        {
            if (_hand != null)
            {
                return;
            }
            _hand = TransformSearch.FindDeep(transform, "hand_r");
            if (_hand == null)
            {
                return;
            }
            _indexRoot = _hand.Find(Fingers[0] + "1_r");
            _middleRoot = _hand.Find(Fingers[1] + "1_r");
            _ringRoot = _hand.Find(Fingers[2] + "1_r");
            _middleTip = _middleRoot != null ? _middleRoot.Find(Fingers[1] + "2_r") : null;
        }

        /// <summary>
        /// A claw's tip in the world: out from the knuckles along the middle finger, spread across the hand from the ring finger's side
        /// (claw 0) to the index's (claw 2). A hand with no finger bones reaches out of its own forward.
        /// </summary>
        private Vector3 Tip(int claw)
        {
            if (_hand == null)
            {
                return transform.position;
            }
            float scale = Scale;
            Vector3 knuckles = _middleRoot != null ? _middleRoot.position : _hand.position;
            Vector3 along = _middleTip != null && _middleRoot != null ? _middleTip.position - _middleRoot.position : _hand.forward;
            along = along.sqrMagnitude > 1e-8f ? along.normalized : _hand.forward;
            Vector3 across = _indexRoot != null && _ringRoot != null ? _indexRoot.position - _ringRoot.position : _hand.right;
            across = Vector3.ProjectOnPlane(across, along);
            across = across.sqrMagnitude > 1e-8f ? across.normalized : _hand.right;
            return knuckles + along * (ClawReach * scale) + across * ((claw - 1) * ClawSpread * scale);
        }

        private void LateUpdate()
        {
            if (_following)
            {
                Follow();
            }
            float fade = _stoppedAt < 0.0f ? 1.0f : 1.0f - (Time.time - _stoppedAt) / FadeSeconds;
            if (_mesh == null)
            {
                return;
            }
            if (fade <= 0.0f)
            {
                if (_renderer.enabled)
                {
                    foreach (List<Vector3> path in _paths)
                    {
                        path.Clear();
                    }
                    _mesh.Clear();
                    _renderer.enabled = false;
                }
                return;
            }
            Build(fade);
        }

        /// <summary>The claws this frame: drawing once they move fast enough to cut, in the body's own space.</summary>
        private void Follow()
        {
            if (_hand == null)
            {
                _following = false;
                return;
            }
            float deltaTime = Time.deltaTime;
            for (int claw = 0; claw < Claws; claw++)
            {
                Vector3 tip = Tip(claw);
                if (!_drawing && _hadLast && deltaTime > 1e-5f && (tip - _lastWorld[claw]).magnitude / deltaTime >= CutSpeed * Scale)
                {
                    _drawing = true;
                    for (int each = 0; each < Claws; each++)
                    {
                        _paths[each].Add(transform.InverseTransformPoint(_lastWorld[each]));   // from where the cut began
                    }
                }
                _lastWorld[claw] = tip;
            }
            _hadLast = true;
            if (!_drawing)
            {
                return;
            }
            for (int claw = 0; claw < Claws; claw++)
            {
                _paths[claw].Add(transform.InverseTransformPoint(_lastWorld[claw]));
                KeepTheLast(_paths[claw], StreakMetres);
            }
        }

        /// <summary>
        /// Shortens <paramref name="path"/> from its start to at most <paramref name="length"/> along it (in the body's own space, which is at
        /// the body's scale), the first point slid along so the streak's tail moves smoothly rather than jumping a frame's stretch at a time.
        /// </summary>
        public static void KeepTheLast(List<Vector3> path, float length)
        {
            float total = 0.0f;
            for (int i = 1; i < path.Count; i++)
            {
                total += Vector3.Distance(path[i - 1], path[i]);
            }
            while (total > length && path.Count >= 2)
            {
                float first = Vector3.Distance(path[0], path[1]);
                if (path.Count > 2 && total - first >= length)
                {
                    path.RemoveAt(0);
                    total -= first;
                    continue;
                }
                path[0] = Vector3.MoveTowards(path[0], path[1], total - length);
                break;
            }
        }

        /// <summary>The streaks as one mesh: each a ribbon along its curve, pointed at both ends, turned to the camera, faded by <paramref name="fade"/>.</summary>
        private void Build(float fade)
        {
            _vertices.Clear();
            _uvs.Clear();
            _colours.Clear();
            _triangles.Clear();
            Camera view = Camera.main;
            Vector3 eye = view != null ? transform.InverseTransformPoint(view.transform.position) : new Vector3(0.0f, 2.0f, 10.0f);
            float width = Width * Mathf.Lerp(0.55f, 1.0f, fade);   // the body's own space is at the body's scale
            var colour = new Color(1.0f, 1.0f, 1.0f, Mathf.Clamp01(fade));
            foreach (List<Vector3> path in _paths)
            {
                if (path.Count < 2)
                {
                    continue;
                }
                Smooth(path, _smooth);
                float total = 0.0f;
                for (int i = 1; i < _smooth.Count; i++)
                {
                    total += Vector3.Distance(_smooth[i - 1], _smooth[i]);
                }
                if (total < 1e-4f)
                {
                    continue;
                }
                int first = _vertices.Count;
                float run = 0.0f;
                for (int i = 0; i < _smooth.Count; i++)
                {
                    if (i > 0)
                    {
                        run += Vector3.Distance(_smooth[i - 1], _smooth[i]);
                    }
                    float u = run / total;
                    Vector3 here = _smooth[i];
                    Vector3 tangent = _smooth[Mathf.Min(i + 1, _smooth.Count - 1)] - _smooth[Mathf.Max(i - 1, 0)];
                    Vector3 side = Vector3.Cross(tangent, eye - here);
                    side = side.sqrMagnitude > 1e-10f ? side.normalized : Vector3.up;
                    float half = 0.5f * width * StreakTaper(u);
                    _vertices.Add(here - side * half);
                    _vertices.Add(here + side * half);
                    _uvs.Add(new Vector2(u, 0.0f));
                    _uvs.Add(new Vector2(u, 1.0f));
                    _colours.Add(colour);
                    _colours.Add(colour);
                }
                for (int i = 0; i < _smooth.Count - 1; i++)
                {
                    int a = first + i * 2, b = a + 1, c = a + 2, d = a + 3;
                    _triangles.Add(a); _triangles.Add(c); _triangles.Add(b);
                    _triangles.Add(b); _triangles.Add(c); _triangles.Add(d);
                    _triangles.Add(a); _triangles.Add(b); _triangles.Add(c);
                    _triangles.Add(b); _triangles.Add(d); _triangles.Add(c);
                }
            }
            _mesh.Clear();
            bool showing = _triangles.Count > 0;
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

        /// <summary>A streak's breadth along it, 0 to 1 (<paramref name="u"/> 0 where the cut began, 1 where it ended): pointed at both ends, fullest a little past the middle.</summary>
        public static float StreakTaper(float u)
        {
            u = Mathf.Clamp01(u);
            float skewed = Mathf.Pow(u, 0.8f);
            // (The sine of a whole half-turn comes out a hair below nought in floats, and a power of that is not a number.)
            return Mathf.Pow(Mathf.Max(0.0f, Mathf.Sin(Mathf.PI * skewed)), 0.7f);
        }

        /// <summary>The path with <see cref="StepsPerFrame"/> - 1 points put between each two on a curve through them (Catmull-Rom).</summary>
        public static void Smooth(List<Vector3> path, List<Vector3> into)
        {
            into.Clear();
            int count = path.Count;
            for (int i = 0; i < count - 1; i++)
            {
                Vector3 before = path[Mathf.Max(i - 1, 0)];
                Vector3 from = path[i];
                Vector3 to = path[i + 1];
                Vector3 after = path[Mathf.Min(i + 2, count - 1)];
                for (int step = 0; step < StepsPerFrame; step++)
                {
                    float t = step / (float)StepsPerFrame;
                    float t2 = t * t, t3 = t2 * t;
                    into.Add(0.5f * (2.0f * from + (to - before) * t + (2.0f * before - 5.0f * from + 4.0f * to - after) * t2
                                     + (3.0f * from - before - 3.0f * to + after) * t3));
                }
            }
            into.Add(path[count - 1]);
        }

        /// <summary>A few small flecks of light along the streaks as the cut ends, drifting off the way it went and falling.</summary>
        private void ThrowSparks()
        {
            if (_sparks == null || _hand == null)
            {
                return;
            }
            float scale = Scale;
            for (int i = 0; i < Sparks; i++)
            {
                List<Vector3> path = _paths[i % Claws];
                if (path.Count < 2)
                {
                    continue;
                }
                int at = Random.Range(path.Count / 3, path.Count - 1);
                Vector3 here = transform.TransformPoint(path[at]);
                Vector3 way = transform.TransformVector(path[at + 1] - path[at]);
                way = way.sqrMagnitude > 1e-8f ? way.normalized : Vector3.down;
                var spark = new ParticleSystem.EmitParams
                {
                    position = here,
                    velocity = (way * Random.Range(0.4f, 1.2f) + Random.insideUnitSphere * 0.5f) * scale,
                    startSize = Random.Range(0.015f, 0.03f) * scale,
                    startLifetime = Random.Range(0.2f, 0.4f),
                    startColor = Color.white,
                };
                _sparks.Emit(spark, 1);
            }
        }

        private static ParticleSystem MakeSparks(Transform under)
        {
            var go = new GameObject("ClawSparks");
            go.transform.SetParent(under, false);
            var sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.3f;
            main.maxParticles = 64;
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule fadeOut = sparks.colorOverLifetime;
            fadeOut.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(1.0f, 0.7f, 0.55f), 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
            fadeOut.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1.0f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            sparks.Play();
            return sparks;
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
}
