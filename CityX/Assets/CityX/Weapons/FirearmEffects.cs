using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE THINGS A GUN THROWS: a spent case out of the ejection port on every shot, the empty magazine when it is dropped in a reload, and the
    /// small mechanical sounds of a reload (the magazine out, in with a click, the action racked). From the research: every shooter that feels
    /// good lets the gun MOVE and SPEAK -- brass flying, a magazine hitting the floor, a click when it seats.
    ///
    /// No physics bodies: a case or a magazine flies on its own simple arc to the ground found under it when it was thrown (with the shooter's
    /// own body skipped), tumbles, lands, lies there a while, and is recycled. Nothing it does can block a shot or nudge a body, and a long
    /// burst costs a handful of transforms. The sounds are synthesised once, like GunFeel's hit tick: nothing shipped, nothing to license.
    /// </summary>
    public sealed class FirearmEffects : MonoBehaviour
    {
        public const int MaxCasings = 40;

        public const int MaxMagazines = 6;

        public const float CasingLifeSeconds = 4.0f;

        public const float MagazineLifeSeconds = 12.0f;

        /// <summary>How fast a case leaves the port, metres per second (the body is 2.6 m, the world 1.3x).</summary>
        public const float CasingSpeed = 3.2f;

        private sealed class Flyer
        {
            public Transform Transform;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Ground;
            public float Age;
            public float Life;
            public bool Landed;
            public int Bounces;
        }

        private static FirearmEffects _instance;
        private readonly List<Flyer> _casings = new List<Flyer>();
        private readonly List<Flyer> _magazines = new List<Flyer>();
        private int _nextCasing;
        private static Mesh _casingMesh;
        private static Material _brass;

        /// <summary>How many cases are in flight or lying about (for tests).</summary>
        public static int ActiveCasings
        {
            get
            {
                if (_instance == null)
                {
                    return 0;
                }
                int n = 0;
                foreach (Flyer f in _instance._casings)
                {
                    if (f.Transform != null && f.Transform.gameObject.activeSelf)
                    {
                        n++;
                    }
                }
                return n;
            }
        }

        public static int ActiveMagazines => _instance == null ? 0 : _instance._magazines.Count;

        private static FirearmEffects Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("FirearmEffects");
                    _instance = go.AddComponent<FirearmEffects>();
                }
                return _instance;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Builds what the first shot and the first reload would otherwise build mid-fight (the effects object, the case mesh and its brass
        /// material, the reload sounds): called when a gun is drawn.
        /// </summary>
        public static void Warm()
        {
            _ = Instance;
            CasingMesh();
            Brass();
            _ = MagazineOut;
            _ = MagazineIn;
            _ = Rack;
        }

        /// <summary>A spent case out of the port: <paramref name="direction"/> is the gun's ejection direction in the world.</summary>
        public static void EjectCasing(Vector3 port, Quaternion gunRotation, Vector3 direction, float length, float radius, GameObject wielder)
        {
            FirearmEffects fx = Instance;
            Flyer f = fx.NextCasing(length, radius);
            f.Transform.position = port;
            f.Transform.rotation = gunRotation * Quaternion.Euler(0.0f, 90.0f, 0.0f);
            // A little scatter, deterministic per case so a burst does not look like a hose.
            float jitter = ((fx._nextCasing * 37) % 11) / 10.0f - 0.5f;
            f.Velocity = (direction.normalized + gunRotation * new Vector3(0.0f, jitter * 0.25f, jitter * 0.3f)) * CasingSpeed;
            f.Spin = new Vector3(720.0f + jitter * 300.0f, 120.0f, 1080.0f);
            f.Ground = GroundUnder(port, wielder);
            f.Age = 0.0f;
            f.Life = CasingLifeSeconds;
            f.Landed = false;
            f.Bounces = 0;
            f.Transform.gameObject.SetActive(true);
        }

        /// <summary>
        /// Drops a copy of <paramref name="magazine"/> (the gun's magazine part, as it is now): it falls from where it is, pushed along
        /// <paramref name="outDirection"/>, and lies on the ground. The part itself is hidden by the reload; this is what is left behind.
        /// </summary>
        public static void DropMagazine(Transform magazine, Vector3 outDirection, GameObject wielder)
        {
            if (magazine == null)
            {
                return;
            }
            FirearmEffects fx = Instance;
            if (fx._magazines.Count >= MaxMagazines)
            {
                Flyer oldest = fx._magazines[0];
                fx._magazines.RemoveAt(0);
                if (oldest.Transform != null)
                {
                    Destroy(oldest.Transform.gameObject);
                }
            }
            var copy = new GameObject("DroppedMagazine");
            copy.transform.SetPositionAndRotation(magazine.position, magazine.rotation);
            copy.transform.localScale = magazine.lossyScale;
            foreach (MeshFilter filter in magazine.GetComponentsInChildren<MeshFilter>(true))
            {
                var piece = new GameObject(filter.name);
                piece.transform.SetParent(copy.transform, false);
                piece.transform.localPosition = magazine.InverseTransformPoint(filter.transform.position);
                piece.transform.localRotation = Quaternion.Inverse(magazine.rotation) * filter.transform.rotation;
                piece.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                MeshRenderer source = filter.GetComponent<MeshRenderer>();
                MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
                if (source != null)
                {
                    renderer.sharedMaterials = source.sharedMaterials;
                }
            }
            copy.transform.SetParent(fx.transform, true);
            fx._magazines.Add(new Flyer
            {
                Transform = copy.transform,
                Velocity = outDirection.normalized * 1.2f,
                Spin = new Vector3(140.0f, 40.0f, 80.0f),
                Ground = GroundUnder(magazine.position, wielder),
                Life = MagazineLifeSeconds,
            });
        }


        // ---- a shot on ANOTHER player's copy: its gun's scripts are switched off (NetworkedEquipmentSync), so the flash is put out and the
        // ---- slide or bolt cycled from here ----

        private sealed class Timed
        {
            public GameObject Flash;
            public Transform Part;
            public Vector3 Rest;
            public float Travel;
            public float Seconds;
            public float Age;
        }

        private readonly List<Timed> _timed = new List<Timed>();

        /// <summary>Turns <paramref name="flash"/> on for <paramref name="seconds"/> (a copy's muzzle flash).</summary>
        public static void Flash(GameObject flash, float seconds)
        {
            if (flash == null)
            {
                return;
            }
            flash.SetActive(true);
            Instance._timed.Add(new Timed { Flash = flash, Seconds = seconds });
        }

        /// <summary>Runs <paramref name="part"/> back <paramref name="travel"/> metres and home again over <paramref name="seconds"/> (a copy's slide or bolt).</summary>
        public static void Cycle(Transform part, Vector3 rest, float travel, float seconds)
        {
            if (part == null || seconds <= 0.0f)
            {
                return;
            }
            Instance._timed.Add(new Timed { Part = part, Rest = rest, Travel = travel, Seconds = seconds });
        }

        private void TickTimed(float dt)
        {
            for (int i = _timed.Count - 1; i >= 0; i--)
            {
                Timed t = _timed[i];
                t.Age += dt;
                bool done = t.Age >= t.Seconds;
                if (t.Flash != null && done)
                {
                    t.Flash.SetActive(false);
                }
                if (t.Part != null)
                {
                    float x = Mathf.Clamp01(t.Age / t.Seconds);
                    float back = x < 0.35f ? x / 0.35f : 1.0f - (x - 0.35f) / 0.65f;
                    t.Part.localPosition = t.Rest + Vector3.back * (t.Travel * (done ? 0.0f : back));
                }
                if (done || (t.Flash == null && t.Part == null))
                {
                    _timed.RemoveAt(i);
                }
            }
        }
        private Flyer NextCasing(float length, float radius)
        {
            Flyer f;
            if (_casings.Count < MaxCasings)
            {
                var go = new GameObject("Casing");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = CasingMesh();
                go.AddComponent<MeshRenderer>().sharedMaterial = Brass();
                f = new Flyer { Transform = go.transform };
                _casings.Add(f);
            }
            else
            {
                f = _casings[_nextCasing % MaxCasings];
            }
            _nextCasing++;
            // The shared mesh is a unit case (1 m long, 1 m across); each gun's size is its scale.
            f.Transform.localScale = new Vector3(radius * 2.0f, radius * 2.0f, length);
            return f;
        }

        /// <summary>The ground under a point, skipping the shooter's own body and anything a bullet ignores. Far below when there is none.</summary>
        private static float GroundUnder(Vector3 point, GameObject wielder)
        {
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 0.05f, Vector3.down, 30.0f, ~0, QueryTriggerInteraction.Ignore);
            float best = point.y - 30.0f;
            float nearest = float.MaxValue;
            PlayerVitals own = wielder != null ? wielder.GetComponent<PlayerVitals>() : null;
            foreach (RaycastHit hit in hits)
            {
                if (own != null && hit.collider.GetComponentInParent<PlayerVitals>() == own)
                {
                    continue;
                }
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    best = hit.point.y;
                }
            }
            return best;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            TickTimed(dt);
            Vector3 gravity = Physics.gravity;
            foreach (Flyer f in _casings)
            {
                if (f.Transform == null || !f.Transform.gameObject.activeSelf)
                {
                    continue;
                }
                Step(f, dt, gravity, 0.006f);
                if (f.Age >= f.Life)
                {
                    f.Transform.gameObject.SetActive(false);
                }
            }
            for (int i = _magazines.Count - 1; i >= 0; i--)
            {
                Flyer f = _magazines[i];
                if (f.Transform == null)
                {
                    _magazines.RemoveAt(i);
                    continue;
                }
                Step(f, dt, gravity, 0.02f);
                if (f.Age >= f.Life)
                {
                    Destroy(f.Transform.gameObject);
                    _magazines.RemoveAt(i);
                }
            }
        }

        /// <summary>One step of a thrown thing: gravity and tumble until it reaches the ground, a bounce or two, then it lies flat.</summary>
        private static void Step(Flyer f, float dt, Vector3 gravity, float restHeight)
        {
            f.Age += dt;
            if (f.Landed)
            {
                return;
            }
            f.Velocity += gravity * dt;
            Vector3 p = f.Transform.position + f.Velocity * dt;
            f.Transform.rotation = Quaternion.Euler(f.Spin * dt) * f.Transform.rotation;
            if (p.y <= f.Ground + restHeight && f.Velocity.y < 0.0f)
            {
                p.y = f.Ground + restHeight;
                if (f.Bounces < 2 && f.Velocity.y < -1.0f)
                {
                    f.Bounces++;
                    f.Velocity = new Vector3(f.Velocity.x * 0.45f, -f.Velocity.y * 0.3f, f.Velocity.z * 0.45f);
                    f.Spin *= 0.5f;
                }
                else
                {
                    f.Landed = true;
                    // Lying on its side: the long axis level, turned to wherever it was heading.
                    Vector3 along = f.Transform.forward;
                    along.y = 0.0f;
                    if (along.sqrMagnitude < 1e-4f)
                    {
                        along = Vector3.forward;
                    }
                    f.Transform.rotation = Quaternion.LookRotation(along.normalized, Vector3.up) * Quaternion.Euler(0.0f, 0.0f, 90.0f);
                }
            }
            f.Transform.position = p;
        }

        private static Mesh CasingMesh()
        {
            if (_casingMesh != null)
            {
                return _casingMesh;
            }
            // A unit cylinder along z (from -0.5 to 0.5), eight sides: at a case's size, more would not show.
            const int sides = 8;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int end = 0; end < 2; end++)
            {
                float z = end == 0 ? -0.5f : 0.5f;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2.0f / sides;
                    vertices.Add(new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, z));
                }
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                triangles.AddRange(new[] { i, sides + i, sides + j, i, sides + j, j });
            }
            for (int i = 1; i < sides - 1; i++)
            {
                triangles.AddRange(new[] { 0, i + 1, i, sides, sides + i, sides + i + 1 });
            }
            _casingMesh = new Mesh { name = "Casing" };
            _casingMesh.SetVertices(vertices);
            _casingMesh.SetTriangles(triangles, 0);
            _casingMesh.RecalculateNormals();
            _casingMesh.RecalculateBounds();
            return _casingMesh;
        }

        private static Material Brass()
        {
            if (_brass != null)
            {
                return _brass;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _brass = new Material(shader) { name = "CasingBrass" };
            var colour = new Color(0.78f, 0.58f, 0.24f);
            _brass.color = colour;
            if (_brass.HasProperty("_BaseColor"))
            {
                _brass.SetColor("_BaseColor", colour);
            }
            if (_brass.HasProperty("_Metallic"))
            {
                _brass.SetFloat("_Metallic", 0.9f);
            }
            if (_brass.HasProperty("_Smoothness"))
            {
                _brass.SetFloat("_Smoothness", 0.6f);
            }
            return _brass;
        }

        // ---- the reload's sounds, synthesised (deterministic: no Random) --------------------------------------------------------------

        private const int SampleRate = 44100;
        private static AudioClip _magOut;
        private static AudioClip _magIn;
        private static AudioClip _rack;

        /// <summary>The magazine released and pulled: a dull double clack.</summary>
        public static AudioClip MagazineOut => _magOut != null ? _magOut : (_magOut = Synthesise("gun-mag-out", 0.12f, (t, noise) =>
            Clack(t, 0.0f, 900.0f, 70.0f, noise) * 0.7f + Clack(t, 0.055f, 650.0f, 60.0f, noise) * 0.5f));

        /// <summary>The magazine seated: a sharp click, then the thud of the hand.</summary>
        public static AudioClip MagazineIn => _magIn != null ? _magIn : (_magIn = Synthesise("gun-mag-in", 0.14f, (t, noise) =>
            Clack(t, 0.0f, 2600.0f, 160.0f, noise) * 0.9f + Clack(t, 0.03f, 380.0f, 45.0f, noise) * 0.6f));

        /// <summary>The slide or bolt racked: a scrape back, then the slam home.</summary>
        public static AudioClip Rack => _rack != null ? _rack : (_rack = Synthesise("gun-rack", 0.22f, (t, noise) =>
            (t < 0.07f ? noise * 0.25f * Mathf.Sin(t / 0.07f * Mathf.PI) : 0.0f) + Clack(t, 0.09f, 1500.0f, 90.0f, noise) * 1.0f
            + Clack(t, 0.1f, 520.0f, 50.0f, noise) * 0.5f));

        private static float Clack(float t, float at, float pitch, float decay, float noise)
        {
            if (t < at)
            {
                return 0.0f;
            }
            float u = t - at;
            float envelope = Mathf.Exp(-u * decay);
            return (Mathf.Sin(2.0f * Mathf.PI * pitch * u) * 0.7f + noise * 0.5f) * envelope;
        }

        private static AudioClip Synthesise(string name, float seconds, System.Func<float, float, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            uint state = 2463534242u;
            for (int i = 0; i < count; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                float noise = (state / (float)uint.MaxValue) * 2.0f - 1.0f;
                data[i] = Mathf.Clamp(sample(i / (float)SampleRate, noise), -1.0f, 1.0f) * 0.6f;
            }
            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
