using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What dismemberment leaves (docs/DISMEMBERMENT.md), drawn in code: chunks thrown out by a burst (a few lumpy shapes made once, raw red and
    /// dark red, every fourth a pale shard of bone), a stump where a part came off (a wet red cap with a nub of bone, spurting:
    /// <see cref="StumpBleed"/>), and the blood (<see cref="BloodBurst"/>, <see cref="BloodSplats"/>). Chunks lie a while, then shrink away
    /// (<see cref="Debris"/>). Made with the game's lit shader, so the cel look takes them.
    /// </summary>
    public static class Gore
    {
        public const int GibShapes = 4;

        /// <summary>One chunk in this many is a shard of bone.</summary>
        public const int BoneEvery = 4;

        public const float GibSpeedMin = 3.0f;
        public const float GibSpeedMax = 7.5f;
        public const float GibMass = 0.3f;

        public static readonly Color Flesh = new Color(0.55f, 0.06f, 0.05f);
        public static readonly Color DarkFlesh = new Color(0.32f, 0.03f, 0.03f);
        public static readonly Color BoneWhite = new Color(0.86f, 0.82f, 0.72f);

        private static Mesh[] _gibs;
        private static Mesh _sphere;
        private static Material _flesh;
        private static Material _darkFlesh;
        private static Material _bone;

        /// <summary>
        /// Throws <paramref name="count"/> chunks about <paramref name="size"/> across out from <paramref name="at"/>, mostly along
        /// <paramref name="away"/> and up, never knocking into <paramref name="ignore"/> (the body they came out of). Returns how many.
        /// </summary>
        public static int Gibs(Vector3 at, Vector3 away, int count, float size, Collider[] ignore)
        {
            Mesh[] shapes = GibMeshes();
            for (int i = 0; i < count; i++)
            {
                bool bone = i % BoneEvery == BoneEvery - 1;
                var gib = new GameObject(bone ? "GoreBone" : "GoreChunk");
                gib.transform.SetPositionAndRotation(at + Random.insideUnitSphere * size * 1.5f, Random.rotation);
                float scale = size * Random.Range(0.6f, 1.3f);
                gib.transform.localScale = bone ? new Vector3(scale * 0.45f, scale * 1.6f, scale * 0.45f) : Vector3.one * scale;
                gib.AddComponent<MeshFilter>().sharedMesh = shapes[Random.Range(0, shapes.Length)];
                gib.AddComponent<MeshRenderer>().sharedMaterial = bone ? BoneMaterial() : Random.value < 0.5f ? FleshMaterial() : DarkFleshMaterial();
                var collider = gib.AddComponent<SphereCollider>();
                collider.radius = 0.45f;
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
                var body = gib.AddComponent<Rigidbody>();
                body.mass = GibMass;
                body.linearDamping = 0.3f;
                body.angularDamping = 0.6f;
                Vector3 direction = (away * 0.8f + Random.onUnitSphere + Vector3.up * 0.9f).normalized;
                body.linearVelocity = direction * Random.Range(GibSpeedMin, GibSpeedMax);
                body.angularVelocity = Random.onUnitSphere * 10.0f;
                gib.AddComponent<Debris>();
            }
            return count;
        }

        /// <summary>
        /// A stump over a cut: a wet red cap <paramref name="radius"/> across at <paramref name="at"/>, facing <paramref name="outward"/>, with a
        /// nub of bone in it, riding <paramref name="parent"/> (the bone above the cut, or the cut-off part), spurting for a few seconds.
        /// </summary>
        public static GameObject Stump(Transform parent, Vector3 at, Vector3 outward, float radius)
        {
            var stump = new GameObject("Stump");
            Vector3 axis = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.up;
            stump.transform.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, axis));
            stump.transform.SetParent(parent, true);

            var cap = new GameObject("Cap");
            cap.transform.SetParent(stump.transform, false);
            float across = radius * 2.2f;
            cap.transform.localScale = new Vector3(across, radius * 0.9f, across);
            cap.AddComponent<MeshFilter>().sharedMesh = Sphere();
            cap.AddComponent<MeshRenderer>().sharedMaterial = FleshMaterial();

            var nub = new GameObject("Bone");
            nub.transform.SetParent(stump.transform, false);
            nub.transform.localPosition = Vector3.up * radius * 0.3f;
            nub.transform.localScale = new Vector3(radius * 0.7f, radius * 0.5f, radius * 0.7f);
            nub.AddComponent<MeshFilter>().sharedMesh = Sphere();
            nub.AddComponent<MeshRenderer>().sharedMaterial = BoneMaterial();

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

        private static Mesh[] GibMeshes()
        {
            if (_gibs != null)
            {
                return _gibs;
            }
            _gibs = new Mesh[GibShapes];
            Mesh sphere = Sphere();
            Vector3[] round = sphere.vertices;
            for (int shape = 0; shape < GibShapes; shape++)
            {
                var lumpy = new Vector3[round.Length];
                float seed = shape * 17.3f;
                for (int i = 0; i < round.Length; i++)
                {
                    Vector3 p = round[i];
                    float bump = Mathf.PerlinNoise(p.x * 2.3f + seed, p.y * 2.3f + p.z * 1.7f + seed);
                    lumpy[i] = p * (0.7f + 0.6f * bump);
                }
                var mesh = new Mesh { name = "GoreChunk" + shape };
                mesh.SetVertices(lumpy);
                mesh.SetTriangles(sphere.triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                _gibs[shape] = mesh;
            }
            return _gibs;
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

        private static Material FleshMaterial() => _flesh != null ? _flesh : _flesh = Lit("GoreFlesh", Flesh, 0.7f);

        private static Material DarkFleshMaterial() => _darkFlesh != null ? _darkFlesh : _darkFlesh = Lit("GoreDarkFlesh", DarkFlesh, 0.75f);

        private static Material BoneMaterial() => _bone != null ? _bone : _bone = Lit("GoreBone", BoneWhite, 0.25f);

        private static Material Lit(string name, Color colour, float smoothness)
        {
            var material = new Material(CityX.Core.LitShader.Find()) { name = name };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }
    }
}
