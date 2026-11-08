using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What dismemberment leaves (docs/DISMEMBERMENT.md), drawn in code. A burst throws pieces of the part itself (<see cref="GoreShatter"/>:
    /// its own skin and clothes outside, fat and meat behind) with what was inside it (<see cref="GoreAnatomy"/>: bone, ribs, organs, eyes),
    /// each tumbling off and leaving a smear where it lands (<see cref="GoreChunk"/>); a stump where a part came off (a wet red cap ringed with
    /// dark blood on the skin, a disc of bone in it, a broken bone sticking out after a burst, spurting: <see cref="StumpBleed"/>); and the
    /// blood (<see cref="BloodBurst"/>, <see cref="BloodSplats"/>). Everything lies a while, then shrinks away (<see cref="Debris"/>). Made with
    /// the game's lit shader, so the cel look takes it.
    /// </summary>
    public static class Gore
    {
        /// <summary>How fast pieces leave a burst, m/s at the body's scale, and how fast they tumble, radians a second (100 to 500 degrees).</summary>
        public const float SpeedMin = 2.5f;

        public const float SpeedMax = 6.5f;
        public const float SpinMin = 1.8f;
        public const float SpinMax = 8.7f;

        /// <summary>How much a piece's way strays from straight out of the burst, and how much the blow and the air push it.</summary>
        public const float Scatter = 0.45f;

        public const float AlongBlow = 0.6f;
        public const float Upward = 0.75f;

        /// <summary>How heavy flesh is, kg a cubic metre, for a piece's weight; and the lightest and heaviest piece, kg.</summary>
        public const float FleshDensity = 1050.0f;

        public const float MinMass = 0.08f;
        public const float MaxMass = 4.0f;

        /// <summary>How many pieces of a burst leave a smear where they land: a part, the whole body.</summary>
        public const int PartSmears = 4;

        public const int BodySmears = 7;

        /// <summary>A blood trail on every other piece in the air: how wide it starts, m at the body's scale, and how long it hangs, s.</summary>
        public const float TrailWidth = 0.05f;

        public const float TrailSeconds = 0.35f;

        /// <summary>The bone out of a burst stump: how long (over the stump's radius, part of it inside) and how thick, and the ring of blood round it.</summary>
        public const float BrokenBoneLength = 2.4f;

        public const float BrokenBoneWidth = 0.28f;
        public static readonly Color Collar = new Color(0.42f, 0.02f, 0.025f);

        /// <summary>
        /// A stump's red cap's radius over the limb's where it was cut: just past the skin, enough to hide the cut's ragged edge and no more
        /// (owner, 2026-11-07: the cap was "way larger than the circumference of the limb").
        /// </summary>
        public const float CapOverLimb = 1.06f;

        /// <summary>The dark ring of blood on the skin round the cap: its width over the cap's.</summary>
        public const float CollarOverCap = 1.1f;

        private static readonly Dictionary<(GoreAnatomy.Tissue, int), Material> Materials = new Dictionary<(GoreAnatomy.Tissue, int), Material>();
        private static Material _trail;
        private static Material _collar;
        private static PhysicsMaterial _wet;
        private static Mesh _sphere;

        /// <summary>One surface a burst breaks up: where it is now, what it is (skin, clothes, hair), and how it was drawn.</summary>
        public sealed class Source
        {
            public GoreShatter.Surface Surface;
            public string Name;
            public Material[] Materials;
            public MaterialPropertyBlock Block;
            public UnityEngine.Rendering.ShadowCastingMode Shadows = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// <summary>Iris colours an eye can have.</summary>
        private static readonly Color[] Irises =
        {
            new Color(0.36f, 0.2f, 0.08f), new Color(0.23f, 0.42f, 0.68f), new Color(0.3f, 0.52f, 0.3f), new Color(0.5f, 0.55f, 0.6f),
        };

        /// <summary>
        /// No tissue is paler than this, the player's skin (0.72): in the noon sun a near-white eye, tooth or bone came out twice as bright as
        /// the bloom's threshold, and small on a dark body the bloom spread it into a dot of glowing light (owner, 2026-11-07: "small dots of
        /// glowing light").
        /// </summary>
        public const float Palest = 0.7f;

        /// <summary>What each tissue looks like: its colour, never paler than <see cref="Palest"/>.</summary>
        public static Color ColourOf(GoreAnatomy.Tissue tissue, int variant = 0)
        {
            switch (tissue)
            {
                case GoreAnatomy.Tissue.Meat: return new Color(0.58f, 0.06f, 0.05f);
                case GoreAnatomy.Tissue.DarkMeat: return new Color(0.32f, 0.03f, 0.03f);
                case GoreAnatomy.Tissue.Fat: return new Color(0.7f, 0.6f, 0.38f);
                case GoreAnatomy.Tissue.Bone: return new Color(0.7f, 0.66f, 0.56f);
                case GoreAnatomy.Tissue.Marrow: return new Color(0.48f, 0.05f, 0.06f);
                case GoreAnatomy.Tissue.Tooth: return new Color(0.7f, 0.69f, 0.64f);
                case GoreAnatomy.Tissue.Lung: return new Color(0.7f, 0.4f, 0.42f);
                case GoreAnatomy.Tissue.Heart: return new Color(0.45f, 0.04f, 0.05f);
                case GoreAnatomy.Tissue.Vessel: return new Color(0.7f, 0.21f, 0.25f);
                case GoreAnatomy.Tissue.Liver: return new Color(0.36f, 0.07f, 0.05f);
                case GoreAnatomy.Tissue.Brain: return new Color(0.7f, 0.53f, 0.53f);
                case GoreAnatomy.Tissue.Guts: return new Color(0.7f, 0.41f, 0.38f);
                case GoreAnatomy.Tissue.EyeWhite: return new Color(0.7f, 0.7f, 0.68f);
                case GoreAnatomy.Tissue.Iris: return Irises[Mathf.Abs(variant) % Irises.Length];
                default: return new Color(0.03f, 0.03f, 0.03f);
            }
        }

        /// <summary>The material of a tissue (made once; an iris has one per colour).</summary>
        public static Material MaterialOf(GoreAnatomy.Tissue tissue, int variant = 0)
        {
            variant = tissue == GoreAnatomy.Tissue.Iris ? Mathf.Abs(variant) % Irises.Length : 0;
            if (Materials.TryGetValue((tissue, variant), out Material material) && material != null)
            {
                return material;
            }
            material = Matte("Gore" + tissue + (variant > 0 ? variant.ToString() : ""), ColourOf(tissue, variant));
            Materials[(tissue, variant)] = material;
            return material;
        }

        /// <summary>
        /// Throws what a burst leaves, out of <paramref name="middle"/> and along <paramref name="away"/>: the <paramref name="sources"/> broken
        /// into <paramref name="chunks"/> pieces of themselves, and <paramref name="bits"/> of what was inside (<see cref="GoreAnatomy.BitsOf"/>),
        /// the first <paramref name="smears"/> to land leaving a smear. The same <paramref name="seed"/> throws the same pieces.
        /// <paramref name="carried"/> is the body's own speed; nothing thrown knocks into <paramref name="ignore"/>. Returns how many pieces flew.
        /// </summary>
        public static int Burst(IList<Source> sources, IList<GoreAnatomy.Bit> bits, Vector3 middle, Vector3 away, float scale, int chunks, int seed,
            int smears, Collider[] ignore, Vector3 carried)
        {
            var random = new System.Random(seed);
            var surfaces = new List<GoreShatter.Surface>(sources.Count);
            foreach (Source source in sources)
            {
                surfaces.Add(source.Surface);
            }
            var smearsLeft = new GoreChunk.Smears(smears);
            int thrown = 0;
            foreach (GoreShatter.Chunk chunk in GoreShatter.Shatter(surfaces, chunks, scale, random))
            {
                var piece = new GameObject("GoreChunk");
                piece.transform.position = chunk.Centre;
                var made = new List<Mesh>();
                foreach (GoreShatter.Patch patch in chunk.Patches)
                {
                    Source source = sources[patch.Surface];
                    MeshRenderer renderer = Part(piece.transform, source.Name, patch.Mesh, Fit(source.Materials, patch.Mesh.subMeshCount));
                    if (source.Block != null)
                    {
                        renderer.SetPropertyBlock(source.Block);
                    }
                    renderer.shadowCastingMode = source.Shadows;
                    made.Add(patch.Mesh);
                }
                if (chunk.Meat != null)
                {
                    Part(piece.transform, "Meat", chunk.Meat, new[]
                    {
                        MaterialOf(GoreAnatomy.Tissue.Fat), MaterialOf(GoreAnatomy.Tissue.Meat), MaterialOf(GoreAnatomy.Tissue.DarkMeat),
                    });
                    made.Add(chunk.Meat);
                }
                var box = piece.AddComponent<BoxCollider>();
                box.center = chunk.Bounds.center;
                box.size = Vector3.Max(chunk.Bounds.size * 0.85f, Vector3.one * 0.03f * scale);
                Throw(piece, box, chunk.Area * chunk.Thickness * FleshDensity, chunk.Centre - middle, away, scale, random, ignore, carried, smearsLeft,
                    thrown % 2 == 0);
                piece.AddComponent<Debris>().Own(made);
                thrown++;
            }
            foreach (GoreAnatomy.Bit bit in bits)
            {
                Bit(bit, middle, away, scale, random, ignore, carried, smearsLeft, thrown % 2 == 0);
                thrown++;
            }
            return thrown;
        }

        /// <summary>One of the bits inside a body thrown out of <paramref name="middle"/>: its shape at the body's scale, a box round it.</summary>
        private static void Bit(GoreAnatomy.Bit bit, Vector3 middle, Vector3 away, float scale, System.Random random, Collider[] ignore,
            Vector3 carried, GoreChunk.Smears smears, bool trail)
        {
            Mesh mesh = GoreAnatomy.MeshOf(bit, random.Next(GoreAnatomy.Variants));
            var piece = new GameObject("Gore" + bit);
            Vector3 offset = RandomInBall(random) * 0.15f * scale;
            piece.transform.SetPositionAndRotation(middle + offset, RandomTurn(random));
            piece.transform.localScale = Vector3.one * scale;
            GoreAnatomy.Tissue[] tissues = GoreAnatomy.TissuesOf(bit);
            int iris = random.Next(4);
            var materials = new Material[tissues.Length];
            for (int i = 0; i < tissues.Length; i++)
            {
                materials[i] = MaterialOf(bit == GoreAnatomy.Bit.Meat && random.NextDouble() < 0.4 ? GoreAnatomy.Tissue.DarkMeat : tissues[i], iris);
            }
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterials = materials;
            var box = piece.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = Vector3.Max(mesh.bounds.size * 0.9f, Vector3.one * 0.02f);
            Vector3 size = Vector3.Scale(mesh.bounds.size, Vector3.one * scale);
            Throw(piece, box, size.x * size.y * size.z * 0.5f * FleshDensity, offset, away, scale, random, ignore, carried, smears, trail);
            piece.AddComponent<Debris>();
        }

        /// <summary>
        /// Sends a piece off: out from the burst along <paramref name="outward"/>, with the blow and upward, a little astray, on top of the body's
        /// own speed; tumbling; wet (it slides and stops rather than bouncing); never into <paramref name="ignore"/>.
        /// </summary>
        private static void Throw(GameObject piece, Collider collider, float mass, Vector3 outward, Vector3 away, float scale, System.Random random,
            Collider[] ignore, Vector3 carried, GoreChunk.Smears smears, bool trail)
        {
            collider.sharedMaterial = Wet();
            if (ignore != null)
            {
                foreach (Collider other in ignore)
                {
                    if (other != null)
                    {
                        Physics.IgnoreCollision(collider, other, true);
                    }
                }
            }
            var body = piece.AddComponent<Rigidbody>();
            body.mass = Mathf.Clamp(mass, MinMass, MaxMass);
            body.linearDamping = 0.05f;
            body.angularDamping = 0.4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Vector3 way = (outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.zero) + away * AlongBlow + Vector3.up * Upward
                + RandomInBall(random) * Scatter;
            float speed = Mathf.Lerp(SpeedMin, SpeedMax, (float)random.NextDouble()) * Mathf.Sqrt(Mathf.Max(0.2f, scale));
            body.linearVelocity = carried + (way.sqrMagnitude > 1e-6f ? way.normalized : Vector3.up) * speed;
            body.angularVelocity = RandomInBall(random).normalized * Mathf.Lerp(SpinMin, SpinMax, (float)random.NextDouble());
            if (trail)
            {
                Trail(piece, scale);
            }
            piece.AddComponent<GoreChunk>().Bind(smears, scale);
        }

        /// <summary>A short streak of blood behind a piece in the air (it stops when the piece lands).</summary>
        private static void Trail(GameObject piece, float scale)
        {
            var trail = piece.AddComponent<TrailRenderer>();
            trail.time = TrailSeconds;
            trail.minVertexDistance = 0.04f * scale;
            trail.widthMultiplier = TrailWidth * scale;
            trail.widthCurve = AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.0f);
            var colours = new Gradient();
            colours.SetKeys(
                new[] { new GradientColorKey(BloodSplats.Colour * 1.6f, 0.0f), new GradientColorKey(BloodSplats.Colour, 1.0f) },
                new[] { new GradientAlphaKey(0.9f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
            trail.colorGradient = colours;
            trail.sharedMaterial = TrailMaterial();
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        /// <summary>
        /// A stump over a cut: a wet red cap as wide as the limb (<paramref name="radius"/>: the limb's there) at <paramref name="at"/>, facing
        /// <paramref name="outward"/>, a disc of bone in it, a ring of dark blood round it on the skin, riding <paramref name="parent"/> (the bone
        /// above the cut, or the cut-off part), spurting for a few seconds. After a burst (<paramref name="broken"/>) the bone sticks out of it,
        /// broken.
        /// </summary>
        public static GameObject Stump(Transform parent, Vector3 at, Vector3 outward, float radius, bool broken = false)
        {
            var stump = new GameObject("Stump");
            Vector3 axis = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.up;
            stump.transform.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, axis));
            stump.transform.SetParent(parent, true);

            float across = 2.0f * radius * CapOverLimb;
            float collar = across * CollarOverCap;
            Ball(stump.transform, "Collar", new Vector3(0.0f, -radius * 0.25f, 0.0f), new Vector3(collar, radius * 0.5f, collar), CollarMaterial());
            Ball(stump.transform, "Cap", Vector3.zero, new Vector3(across, radius * 0.9f, across), MaterialOf(GoreAnatomy.Tissue.Meat));
            Ball(stump.transform, "Bone", Vector3.up * radius * 0.3f, new Vector3(radius * 0.7f, radius * 0.5f, radius * 0.7f), MaterialOf(GoreAnatomy.Tissue.Bone));
            if (broken)
            {
                var bone = new GameObject("BrokenBone");
                bone.transform.SetParent(stump.transform, false);
                Mesh shard = GoreAnatomy.MeshOf(GoreAnatomy.Bit.Forearm);
                float length = shard.bounds.size.y;
                float along = BrokenBoneLength * radius / Mathf.Max(1e-4f, length);
                float thick = BrokenBoneWidth * radius / GoreAnatomy.ForearmShaft;
                // The jagged end out, the knob end hidden in the body.
                bone.transform.localPosition = Vector3.up * (length * along * 0.5f - 0.6f * radius);
                bone.transform.localRotation = Quaternion.Euler(8.0f, 0.0f, 6.0f);
                bone.transform.localScale = new Vector3(thick, along, thick);
                bone.AddComponent<MeshFilter>().sharedMesh = shard;
                bone.AddComponent<MeshRenderer>().sharedMaterials = new[] { MaterialOf(GoreAnatomy.Tissue.Bone), MaterialOf(GoreAnatomy.Tissue.Marrow) };
            }

            stump.AddComponent<StumpBleed>();
            return stump;
        }

        /// <summary>A wound's worth of blood: a spray along <paramref name="away"/>, one up, and the splats on the floor and a wall behind.</summary>
        public static void Blood(Vector3 at, Vector3 away, float strength, Transform body)
        {
            BloodBurst.Spawn(at, away, strength);
            BloodBurst.Spawn(at, Vector3.up + Random.insideUnitSphere * 0.5f, strength * 0.7f);
            BloodSplats.Mark(at, away, strength, body);
        }

        private static MeshRenderer Part(Transform parent, string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            return renderer;
        }

        private static void Ball(Transform parent, string name, Vector3 at, Vector3 size, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = Sphere();
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>A renderer's materials, one for each submesh (a short list repeats its last).</summary>
        private static Material[] Fit(Material[] materials, int count)
        {
            var fitted = new Material[Mathf.Max(1, count)];
            for (int i = 0; i < fitted.Length; i++)
            {
                fitted[i] = materials != null && materials.Length > 0 ? materials[Mathf.Min(i, materials.Length - 1)] : MaterialOf(GoreAnatomy.Tissue.Meat);
            }
            return fitted;
        }

        private static Vector3 RandomInBall(System.Random random)
        {
            while (true)
            {
                var p = new Vector3((float)random.NextDouble() * 2.0f - 1.0f, (float)random.NextDouble() * 2.0f - 1.0f, (float)random.NextDouble() * 2.0f - 1.0f);
                if (p.sqrMagnitude <= 1.0f)
                {
                    return p;
                }
            }
        }

        private static Quaternion RandomTurn(System.Random random)
        {
            return Quaternion.Euler((float)random.NextDouble() * 360.0f, (float)random.NextDouble() * 360.0f, (float)random.NextDouble() * 360.0f);
        }

        private static Mesh Sphere()
        {
            if (_sphere == null)
            {
                GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _sphere = primitive.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(primitive);
            }
            return _sphere;
        }

        /// <summary>Wet: grips (pieces slide to a stop instead of rolling away) and hardly bounces.</summary>
        private static PhysicsMaterial Wet()
        {
            if (_wet == null)
            {
                _wet = new PhysicsMaterial("GoreWet")
                {
                    dynamicFriction = 0.8f,
                    staticFriction = 0.9f,
                    bounciness = 0.08f,
                    frictionCombine = PhysicsMaterialCombine.Maximum,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
                };
            }
            return _wet;
        }

        private static Material TrailMaterial() => _trail != null ? _trail : _trail = BloodSplats.AlphaBlended("GoreTrail");

        private static Material CollarMaterial() => _collar != null ? _collar : _collar = Matte("GoreCollar", Collar);

        /// <summary>
        /// Matte, as the skin is (LitShader.MakeMatte): glossy, a piece lying in the sun or under a lamp caught it as a pinpoint thousands of
        /// times brighter than white, which the bloom spread into a flashlight's glare (owner, 2026-11-06: "bright flashing lights").
        /// </summary>
        private static Material Matte(string name, Color colour)
        {
            var material = new Material(CityX.Core.LitShader.Find()) { name = name };
            material.SetColor("_BaseColor", colour);
            CityX.Core.LitShader.MakeMatte(material);
            return material;
        }
    }
}
