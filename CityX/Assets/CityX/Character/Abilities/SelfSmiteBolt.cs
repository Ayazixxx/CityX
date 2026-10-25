using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Character
{
    /// <summary>
    /// WHAT SELF SMITE LOOKS AND SOUNDS LIKE, on every machine: a dark thundercloud forms 1.5 km over the caster, a giant yellow bolt comes down
    /// out of it, slowing as it nears, and strikes the caster; a burst of electricity flares round the body, a yellow light floods the street,
    /// the sky flashes and the thunder cracks. Purely a show: the damage is the caster's own machine's (SelfSmiteAbility), so the same bolt is
    /// spawned for the caster and for every other player's copy of them, and hurts nothing.
    ///
    /// It keeps its own clock from the cast, on <see cref="SelfSmiteMove"/>'s timeline, and follows the caster's body down to the strike. If the
    /// caster is knocked out of the move (<see cref="Detach"/>), the bolt still comes down where they were, with no burst.
    ///
    /// DRAWN IN CODE, no texture files: the bolt, its forks and the burst's arcs are ribbons turned to face the camera, built into one mesh each
    /// frame (two materials: a white-hot core and a yellow glow, both additive and brighter than white, so the bloom takes them); the cloud is a
    /// handful of soft dark puffs in a second mesh. A soft round spot drawn into a 32 x 32 texture once is the only image.
    /// </summary>
    public sealed class SelfSmiteBolt : MonoBehaviour
    {
        public static readonly Color CoreColour = new Color(5.0f, 4.4f, 1.7f, 1.0f);
        public static readonly Color GlowColour = new Color(1.25f, 0.9f, 0.1f, 1.0f);

        /// <summary>The bolt's halo: blended rather than added, so it stays yellow against a bright sky (added light there washes out white).</summary>
        public static readonly Color HaloColour = new Color(1.0f, 0.82f, 0.04f, 0.8f);
        public static readonly Color CloudColour = new Color(0.10f, 0.10f, 0.13f, 0.93f);
        public static readonly Color LightColour = new Color(1.0f, 0.9f, 0.55f);

        /// <summary>The glow ribbon's width over the core's.</summary>
        public const float GlowWidthFactor = 3.5f;

        /// <summary>The burst's arcs, how many and their points each.</summary>
        public const int BlastArcs = 10;

        public const int ArcPoints = 5;

        /// <summary>The cloud: how many puffs, and their size in metres (a few hundred: it is a kilometre and a half away).</summary>
        public const int CloudPuffs = 12;

        public const float CloudPuffSize = 450.0f;

        /// <summary>The light at the strike: how bright, how far it reaches, and how long it takes to die away.</summary>
        public const float LightIntensity = 14.0f;

        public const float LightRange = 40.0f;

        public const float LightSeconds = 0.45f;

        /// <summary>How strongly the whole sky flashes (1 = a storm's nearest strike).</summary>
        public const float SkyFlashStrength = 0.6f;

        /// <summary>Raised when a bolt strikes, at the strike point: the sky flashes (RainEffect listens), on every machine.</summary>
        public static event System.Action<Vector3, float> Struck;

        private static Material _coreMaterial;
        private static Material _glowMaterial;
        private static Material _haloMaterial;
        private static Material _cloudMaterial;
        private static Texture2D _spot;

        private Transform _caster;
        private float _casterHeight;
        private Vector3 _strikePoint;
        private Vector3 _cloudCentre;
        private float _seconds;
        private bool _attached = true;
        private bool _blast = true;
        private bool _struck;
        private int _seed;
        private int _frame;
        private float _flicker = 1.0f;
        private AudioClip _thunder;

        private Mesh _boltMesh;
        private Mesh _cloudMesh;
        private Light _light;

        private readonly Vector3[] _trunk = new Vector3[SelfSmiteBoltShape.MaxPoints];
        private int _trunkCount;
        private readonly Vector3[][] _forks = new Vector3[SelfSmiteBoltShape.Forks][];
        private readonly int[] _forkCounts = new int[SelfSmiteBoltShape.Forks];
        private readonly Vector3[] _arc = new Vector3[ArcPoints];
        private readonly Vector3[] _ribbon = new Vector3[SelfSmiteBoltShape.MaxPoints];
        private readonly float[] _widths = new float[SelfSmiteBoltShape.MaxPoints];

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color32> _colours = new List<Color32>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _coreTriangles = new List<int>();
        private readonly List<int> _glowTriangles = new List<int>();
        private readonly List<int> _haloTriangles = new List<int>();

        /// <summary>Seconds since the cast.</summary>
        public float Seconds => _seconds;

        /// <summary>The cloud base's height.</summary>
        public float CloudBase => _cloudCentre.y;

        /// <summary>Where the bolt meets the caster (the top of the head), or met them.</summary>
        public Vector3 StrikePoint => _strikePoint;

        /// <summary>How high the bolt's tip is now, metres above <see cref="StrikePoint"/>.</summary>
        public float TipHeight => SelfSmiteMove.TipHeightAbove(_seconds, _cloudCentre.y - _strikePoint.y);

        /// <summary>The bolt is being drawn this frame (left the cloud, not yet faded).</summary>
        public bool BoltShown { get; private set; }

        /// <summary>The burst is being drawn this frame.</summary>
        public bool BlastShown { get; private set; }

        public bool CloudShown { get; private set; }

        /// <summary>
        /// A bolt for a caster of <paramref name="height"/> metres: the cloud forms <see cref="CloudLayer.HeightMetres"/> above the ground under
        /// them, and the bolt follows them down. <paramref name="thunder"/> plays as it strikes.
        /// </summary>
        public static SelfSmiteBolt Spawn(Transform caster, float height, AudioClip thunder, int seed, Material lightTemplate = null)
        {
            UseTemplate(lightTemplate);
            var go = new GameObject("SelfSmiteBolt");
            SelfSmiteBolt bolt = go.AddComponent<SelfSmiteBolt>();
            bolt.Begin(caster, height, thunder, seed);
            return bolt;
        }

        private void Begin(Transform caster, float height, AudioClip thunder, int seed)
        {
            _caster = caster;
            _casterHeight = Mathf.Max(0.5f, height);
            _thunder = thunder;
            _seed = seed;
            _strikePoint = HeadOf(caster);
            _cloudCentre = new Vector3(_strikePoint.x, CloudLayer.BaseOver(caster.position, caster), _strikePoint.z);
            // The meshes are built in world space, so this object stays at the origin, unturned.
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            _boltMesh = NewMesh("SelfSmiteBoltMesh");
            var bolt = new GameObject("Bolt");
            bolt.transform.SetParent(transform, false);
            bolt.AddComponent<MeshFilter>().sharedMesh = _boltMesh;
            var boltRenderer = bolt.AddComponent<MeshRenderer>();
            // The halo first (blended, so it stays yellow over a bright sky), then the additive glow and core on top.
            boltRenderer.sharedMaterials = new[] { HaloMaterial(), GlowMaterial(), CoreMaterial() };
            Quiet(boltRenderer);

            _cloudMesh = NewMesh("SelfSmiteCloudMesh");
            var cloud = new GameObject("Cloud");
            cloud.transform.SetParent(transform, false);
            cloud.AddComponent<MeshFilter>().sharedMesh = _cloudMesh;
            var cloudRenderer = cloud.AddComponent<MeshRenderer>();
            cloudRenderer.sharedMaterials = new[] { CloudMaterial(), CoreMaterial() };
            Quiet(cloudRenderer);

            var lightObject = new GameObject("StrikeLight");
            lightObject.transform.SetParent(transform, false);
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = LightColour;
            _light.range = LightRange;
            _light.intensity = 0.0f;
            _light.shadows = LightShadows.None;
            _light.enabled = false;

            for (int i = 0; i < _forks.Length; i++)
            {
                _forks[i] = new Vector3[SelfSmiteBoltShape.ForkPoints];
            }
        }

        /// <summary>The caster was knocked out of the move: the bolt comes down where they were, and no burst flares.</summary>
        public void Detach()
        {
            _attached = false;
            if (!_struck)
            {
                _blast = false;
            }
        }

        private Vector3 HeadOf(Transform caster)
        {
            return caster.position + Vector3.up * (_casterHeight * 0.97f);
        }

        private void LateUpdate()
        {
            _seconds += Time.deltaTime;
            _frame++;
            if (_attached && _caster != null && !_struck)
            {
                _strikePoint = HeadOf(_caster);
            }
            if (_seconds >= SelfSmiteMove.EffectSeconds)
            {
                Destroy(gameObject);
                return;
            }
            if (!_struck && _seconds >= SelfSmiteMove.StrikeSeconds)
            {
                Strike();
            }
            // Lightning stutters: a new flicker every frame, a new path every three.
            _flicker = 0.6f + 0.4f * Hash01(_seed + _frame * 13);
            if (_frame % 3 == 1 || _trunkCount == 0)
            {
                Reshape(_seed + _frame / 3);
            }

            Camera view = ViewCamera();
            Vector3 eye = view != null ? view.transform.position : _strikePoint + new Vector3(0.0f, 2.0f, -6.0f);
            BuildBolt(eye);
            BuildCloud(view);
            TickLight();
        }

        private void Strike()
        {
            _struck = true;
            Struck?.Invoke(_strikePoint, SkyFlashStrength);
            if (_thunder != null)
            {
                var sound = new GameObject("SelfSmiteThunder");
                sound.transform.position = _strikePoint;
                AudioSource source = sound.AddComponent<AudioSource>();
                source.clip = _thunder;
                source.spatialBlend = 0.75f;
                source.minDistance = 12.0f;
                source.maxDistance = 400.0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.volume = 1.0f;
                source.Play();
                Destroy(sound, _thunder.length + 0.1f);
            }
        }

        private void Reshape(int seed)
        {
            Vector3 top = new Vector3(_cloudCentre.x, _cloudCentre.y, _cloudCentre.z);
            _trunkCount = SelfSmiteBoltShape.Trunk(_strikePoint, top, seed, _trunk);
            for (int i = 0; i < _forks.Length; i++)
            {
                _forkCounts[i] = SelfSmiteBoltShape.Fork(_trunk, _trunkCount, seed, i, _forks[i]);
            }
        }

        // --- the bolt and the burst ---

        private void BuildBolt(Vector3 eye)
        {
            _vertices.Clear();
            _colours.Clear();
            _uvs.Clear();
            _coreTriangles.Clear();
            _glowTriangles.Clear();
            _haloTriangles.Clear();
            BoltShown = false;
            BlastShown = false;

            float t = _seconds;
            float sinceStrike = t - SelfSmiteMove.StrikeSeconds;
            // The bolt: from leaving the cloud until it fades, a beat after the caster can be hit again.
            float boltAlpha = t < SelfSmiteMove.BoltSeconds ? 0.0f
                : sinceStrike < 0.0f ? 1.0f
                : 1.0f - Mathf.Clamp01((t - SelfSmiteMove.IntangibleEndSeconds) / 0.15f);
            // A dark beat just after the strike: a real bolt is a stutter, not a steady line.
            if (sinceStrike > 0.07f && sinceStrike < 0.1f)
            {
                boltAlpha *= 0.15f;
            }
            if (boltAlpha > 0.0f && _trunkCount >= 2)
            {
                float tip = _struck ? 0.0f : TipHeight;
                int count = Clip(_trunk, _trunkCount, _strikePoint.y + tip, _ribbon);
                for (int i = 0; i < count; i++)
                {
                    _widths[i] = SelfSmiteBoltShape.WidthAt(_ribbon[i].y - _strikePoint.y);
                }
                if (count >= 2)
                {
                    if (!_struck)
                    {
                        // On its way down the bolt leads with a point and a flare, not a cut-off end.
                        _widths[0] *= 0.2f;
                        Billboard(_ribbon[0], 0.45f, eye, boltAlpha * _flicker, glow: false);
                        Billboard(_ribbon[0], 1.4f, eye, boltAlpha * _flicker * 0.7f, glow: true);
                    }
                    Ribbon(_ribbon, _widths, count, eye, boltAlpha * _flicker, halo: true);
                    BoltShown = true;
                }
                if (_struck)
                {
                    for (int f = 0; f < _forks.Length; f++)
                    {
                        if (_forkCounts[f] < 2 || Hash01(_seed + f * 71 + _frame) < 0.25f)
                        {
                            continue;   // forks flicker in and out
                        }
                        for (int i = 0; i < _forkCounts[f]; i++)
                        {
                            _widths[i] = SelfSmiteBoltShape.WidthAt(_forks[f][i].y - _strikePoint.y) * Mathf.Lerp(0.6f, 0.04f, i / (float)(_forkCounts[f] - 1));
                        }
                        Ribbon(_forks[f], _widths, _forkCounts[f], eye, boltAlpha * _flicker * 0.8f, halo: true);
                    }
                }
            }

            // The burst: out to its full size in three frames, a flaring ball of light shot through with arcs, gone by frame 50.
            if (_struck && _blast && sinceStrike < 0.3f)
            {
                float growth = SelfSmiteMove.BlastGrowth(t);
                float fade = 1.0f - Mathf.Clamp01((sinceStrike - 0.12f) / 0.18f);
                float radius = SelfSmiteMove.BlastRadius * (0.35f + 0.65f * growth);
                Vector3 centre = _strikePoint - Vector3.up * (_casterHeight * 0.45f);
                Billboard(centre, radius * 0.95f, eye, fade * 0.85f * _flicker, glow: true);
                Billboard(centre, radius * 0.45f, eye, fade * 0.6f * _flicker, glow: false);
                for (int a = 0; a < BlastArcs; a++)
                {
                    Arc(centre, radius, a, eye, fade * _flicker);
                }
                BlastShown = fade > 0.0f;
            }

            _boltMesh.Clear();
            if (_vertices.Count > 0)
            {
                _boltMesh.SetVertices(_vertices);
                _boltMesh.SetColors(_colours);
                _boltMesh.SetUVs(0, _uvs);
                _boltMesh.subMeshCount = 3;
                _boltMesh.SetTriangles(_haloTriangles, 0, false);
                _boltMesh.SetTriangles(_glowTriangles, 1, false);
                _boltMesh.SetTriangles(_coreTriangles, 2, false);
                _boltMesh.RecalculateBounds();
            }
        }

        /// <summary>The trunk's points from <paramref name="lowestY"/> up (the bolt still on its way down), the lowest cut where it is.</summary>
        private static int Clip(Vector3[] trunk, int count, float lowestY, Vector3[] into)
        {
            int written = 0;
            for (int i = 0; i < count; i++)
            {
                if (trunk[i].y < lowestY)
                {
                    if (i + 1 < count && trunk[i + 1].y > lowestY)
                    {
                        float k = Mathf.InverseLerp(trunk[i].y, trunk[i + 1].y, lowestY);
                        into[written++] = Vector3.Lerp(trunk[i], trunk[i + 1], k);
                    }
                    continue;
                }
                into[written++] = trunk[i];
            }
            return written;
        }

        /// <summary>One jagged arc of the burst, from the body's middle out toward the burst's edge.</summary>
        private void Arc(Vector3 centre, float radius, int index, Vector3 eye, float alpha)
        {
            int seed = _seed * 7 + index * 131 + _frame;
            float yaw = (index + Hash01(seed) * 0.8f) / BlastArcs * Mathf.PI * 2.0f;
            float pitch = (Hash01(seed + 3) - 0.35f) * 1.6f;
            Vector3 heading = new Vector3(Mathf.Cos(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Sin(yaw) * Mathf.Cos(pitch));
            float reach = radius * (0.6f + 0.4f * Hash01(seed + 5));
            Vector3 side = Vector3.Cross(heading, Vector3.up);
            side = side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.right;
            Vector3 lift = Vector3.Cross(side, heading).normalized;
            for (int i = 0; i < ArcPoints; i++)
            {
                float k = i / (float)(ArcPoints - 1);
                float stray = i == 0 ? 0.0f : reach * 0.12f;
                _arc[i] = centre + heading * (reach * k)
                          + (side * (Hash01(seed + 11 * i) * 2.0f - 1.0f) + lift * (Hash01(seed + 17 * i) * 2.0f - 1.0f)) * stray;
                _widths[i] = Mathf.Lerp(0.16f, 0.01f, k * k);   // to a point: a flat-ended arc reads as a stick
            }
            Ribbon(_arc, _widths, ArcPoints, eye, alpha);
        }

        /// <summary>A camera-facing ribbon through <paramref name="points"/>: the core, and the glow under it <see cref="GlowWidthFactor"/> wider.</summary>
        private void Ribbon(Vector3[] points, float[] widths, int count, Vector3 eye, float alpha, bool halo = false)
        {
            byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255.0f), 0, 255);
            if (a == 0)
            {
                return;
            }
            var colour = new Color32(255, 255, 255, a);
            for (int pass = 0; pass < 2; pass++)
            {
                bool glow = pass == 1;
                List<int> triangles = glow ? (halo ? _haloTriangles : _glowTriangles) : _coreTriangles;
                int first = _vertices.Count;
                for (int i = 0; i < count; i++)
                {
                    Vector3 along = points[Mathf.Min(i + 1, count - 1)] - points[Mathf.Max(i - 1, 0)];
                    Vector3 toEye = eye - points[i];
                    Vector3 side = Vector3.Cross(along, toEye);
                    side = side.sqrMagnitude > 1e-8f ? side.normalized : Vector3.right;
                    float half = widths[i] * 0.5f * (glow ? GlowWidthFactor : 1.0f);
                    _vertices.Add(points[i] - side * half);
                    _vertices.Add(points[i] + side * half);
                    _colours.Add(colour);
                    _colours.Add(colour);
                    _uvs.Add(new Vector2(0.5f, 0.0f));
                    _uvs.Add(new Vector2(0.5f, 1.0f));
                    if (i > 0)
                    {
                        int v = first + i * 2;
                        triangles.Add(v - 2);
                        triangles.Add(v - 1);
                        triangles.Add(v);
                        triangles.Add(v - 1);
                        triangles.Add(v + 1);
                        triangles.Add(v);
                    }
                }
            }
        }

        /// <summary>A soft round spot facing the camera, into the bolt mesh (glow or core).</summary>
        private void Billboard(Vector3 centre, float radius, Vector3 eye, float alpha, bool glow)
        {
            byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255.0f), 0, 255);
            if (a == 0)
            {
                return;
            }
            AddQuad(centre, radius, eye, new Color32(255, 255, 255, a), _vertices, _colours, _uvs, glow ? _glowTriangles : _coreTriangles);
        }

        private static void AddQuad(Vector3 centre, float radius, Vector3 eye, Color32 colour, List<Vector3> vertices, List<Color32> colours,
            List<Vector2> uvs, List<int> triangles)
        {
            Vector3 toEye = eye - centre;
            Vector3 forward = toEye.sqrMagnitude > 1e-6f ? toEye.normalized : Vector3.back;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            Vector3 up = Vector3.Cross(forward, right);
            int v = vertices.Count;
            vertices.Add(centre + (-right - up) * radius);
            vertices.Add(centre + (right - up) * radius);
            vertices.Add(centre + (right + up) * radius);
            vertices.Add(centre + (-right + up) * radius);
            for (int i = 0; i < 4; i++)
            {
                colours.Add(colour);
            }
            uvs.Add(new Vector2(0.0f, 0.0f));
            uvs.Add(new Vector2(1.0f, 0.0f));
            uvs.Add(new Vector2(1.0f, 1.0f));
            uvs.Add(new Vector2(0.0f, 1.0f));
            triangles.Add(v);
            triangles.Add(v + 2);
            triangles.Add(v + 1);
            triangles.Add(v);
            triangles.Add(v + 3);
            triangles.Add(v + 2);
        }

        // --- the cloud ---

        private readonly List<Vector3> _cloudVertices = new List<Vector3>();
        private readonly List<Color32> _cloudColours = new List<Color32>();
        private readonly List<Vector2> _cloudUvs = new List<Vector2>();
        private readonly List<int> _cloudTriangles = new List<int>();
        private readonly List<int> _cloudFlashTriangles = new List<int>();

        /// <summary>Dark puffs gathering 1.5 km up from frame 2, lit from inside while the bolt leaves, thinning away after.</summary>
        private void BuildCloud(Camera view)
        {
            _cloudVertices.Clear();
            _cloudColours.Clear();
            _cloudUvs.Clear();
            _cloudTriangles.Clear();
            _cloudFlashTriangles.Clear();
            float t = _seconds;
            float alpha = Mathf.Clamp01((t - SelfSmiteMove.CloudSeconds) / 0.12f) * (1.0f - Mathf.Clamp01((t - 0.75f) / 0.35f));
            CloudShown = alpha > 0.0f;
            if (CloudShown)
            {
                Vector3 eye = view != null ? view.transform.position : _strikePoint;
                var colour = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255.0f));
                for (int i = 0; i < CloudPuffs; i++)
                {
                    float angle = (i + Hash01(_seed + i * 5)) / CloudPuffs * Mathf.PI * 2.0f;
                    float spread = i == 0 ? 0.0f : CloudPuffSize * (0.5f + 0.4f * Hash01(_seed + i * 9));
                    Vector3 at = _cloudCentre + new Vector3(Mathf.Cos(angle) * spread, 40.0f * Hash01(_seed + i * 3), Mathf.Sin(angle) * spread);
                    float size = CloudPuffSize * (0.75f + 0.5f * Hash01(_seed + i * 7));
                    AddQuad(at, size, eye, colour, _cloudVertices, _cloudColours, _cloudUvs, _cloudTriangles);
                }
                // Lit from within while the bolt is leaving it.
                if (t >= SelfSmiteMove.BoltSeconds - 0.05f && t < SelfSmiteMove.StrikeSeconds + 0.1f)
                {
                    // A flicker of white inside the cloud, on and off: lightning seen through it, not a lamp.
                    bool lit = Hash01(_seed + _frame * 31) > 0.4f;
                    var flash = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(lit ? alpha * _flicker * 40.0f : 0.0f));
                    AddQuad(_cloudCentre - Vector3.up * 30.0f, CloudPuffSize * 1.3f, eye, flash, _cloudVertices, _cloudColours, _cloudUvs,
                        _cloudFlashTriangles);
                }
            }
            _cloudMesh.Clear();
            if (_cloudVertices.Count > 0)
            {
                _cloudMesh.SetVertices(_cloudVertices);
                _cloudMesh.SetColors(_cloudColours);
                _cloudMesh.SetUVs(0, _cloudUvs);
                _cloudMesh.subMeshCount = 2;
                _cloudMesh.SetTriangles(_cloudTriangles, 0, false);
                _cloudMesh.SetTriangles(_cloudFlashTriangles, 1, false);
                _cloudMesh.RecalculateBounds();
            }
        }

        private void TickLight()
        {
            if (!_struck)
            {
                return;
            }
            float since = _seconds - SelfSmiteMove.StrikeSeconds;
            float left = 1.0f - Mathf.Clamp01(since / LightSeconds);
            _light.enabled = left > 0.0f;
            _light.intensity = LightIntensity * left * left * left * _flicker;
            _light.transform.position = _strikePoint - Vector3.up * (_casterHeight * 0.3f);
        }

        private void OnDestroy()
        {
            if (_boltMesh != null)
            {
                Destroy(_boltMesh);
            }
            if (_cloudMesh != null)
            {
                Destroy(_cloudMesh);
            }
        }

        // --- shared pieces ---

        private static Camera ViewCamera()
        {
            Camera main = Camera.main;
            if (main != null)
            {
                return main;
            }
            return Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null;
        }

        private static float Hash01(int n)
        {
            unchecked
            {
                uint h = (uint)n * 747796405u + 2891336453u;
                h = ((h >> (int)((h >> 28) + 4u)) ^ h) * 277803737u;
                h = (h >> 22) ^ h;
                return (h & 0xFFFFFF) / 16777216.0f;
            }
        }

        private static Mesh NewMesh(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            return mesh;
        }

        private static void Quiet(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material _template;

        /// <summary>
        /// The material the light (the bolt, the burst) is made from, when the body has one (SelfSmiteAbility.LightMaterial, built by the editor
        /// with soft particles on, so a glow fades where it meets the street instead of being cut off along it, and so a build ships that shader
        /// variant). Without one, a plain runtime material does.
        /// </summary>
        private static void UseTemplate(Material template)
        {
            if (template == null || template == _template)
            {
                return;
            }
            _template = template;
            _coreMaterial = null;
            _glowMaterial = null;
            _haloMaterial = null;
        }

        private static Material CoreMaterial() => _coreMaterial != null ? _coreMaterial : (_coreMaterial = MakeMaterial("SelfSmiteCore", CoreColour, true, _template));

        private static Material GlowMaterial() => _glowMaterial != null ? _glowMaterial : (_glowMaterial = MakeMaterial("SelfSmiteGlow", GlowColour, true, _template));

        private static Material HaloMaterial() => _haloMaterial != null ? _haloMaterial : (_haloMaterial = MakeMaterial("SelfSmiteHalo", HaloColour, false, _template));

        private static Material CloudMaterial() =>
            _cloudMaterial != null ? _cloudMaterial : (_cloudMaterial = MakeMaterial("SelfSmiteCloud", CloudColour, false));

        /// <summary>
        /// URP's particle shader, unlit and see-through: additive for the light (the bolt, the burst), ordinary blending for the cloud. The blend
        /// states are set outright (the editor's material inspector would, but this is made at runtime), as the storm's own bolt material is.
        /// </summary>
        private static Material MakeMaterial(string name, Color colour, bool additive, Material template = null)
        {
            Material material;
            if (template != null)
            {
                material = new Material(template) { name = name, hideFlags = HideFlags.DontSave };
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }
                material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            }
            SeeThroughMaterial.Apply(material, additive);
            material.SetColor("_BaseColor", colour);
            material.color = colour;
            material.SetTexture("_BaseMap", Spot());
            material.mainTexture = Spot();
            return material;
        }

        /// <summary>
        /// A soft white spot, opaque in the middle and gone at the edge: across a ribbon it is the soft edge, on a quad a puff. (1 - d^2)^3 falls
        /// to nothing with no slope left at the rim, so a big glow shows no ring where its quad ends (rendered at night, 2026-10-15).
        /// </summary>
        private static Texture2D Spot()
        {
            if (_spot != null)
            {
                return _spot;
            }
            const int size = 64;
            _spot = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SelfSmiteSpot", hideFlags = HideFlags.DontSave };
            _spot.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2.0f - 1.0f;
                    float dy = (y + 0.5f) / size * 2.0f - 1.0f;
                    float d2 = Mathf.Clamp01(dx * dx + dy * dy);
                    float fall = 1.0f - d2;
                    float a = fall * fall * fall;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255.0f));
                }
            }
            _spot.SetPixels32(pixels);
            _spot.Apply(false, true);
            return _spot;
        }
    }
}
