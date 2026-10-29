using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHAT A FIGHT LEAVES ON THE GROUND (owner, 2026-10-17: "make the blood effects better too"): every wound puts a splat on the floor under
    /// it, another where the spray lands, and one on a wall close behind the body. They stay most of a minute, then fade. Drawn in code: four
    /// splat shapes made once (a blob with droplets flung out one way and a drip or two), a flat quad laid a centimetre off the surface, alpha
    /// blended, unlit. At most <see cref="MaxSplats"/> at once; the oldest goes when a new one needs the room. Games that are remembered for
    /// their blood leave it on the walls and the floor; a burst alone is gone in a second.
    /// </summary>
    public static class BloodSplats
    {
        public const int MaxSplats = 48;
        public const float LifeSeconds = 45.0f;
        public const float FadeSeconds = 10.0f;

        /// <summary>How far below a wound the floor may be, and how far behind the body a wall may be, for a splat.</summary>
        public const float FloorReachMetres = 3.0f;

        public const float WallReachMetres = 1.8f;

        /// <summary>A splat sits this far off its surface, so it draws over the floor instead of fighting it.</summary>
        public const float SurfaceOffset = 0.012f;

        public const int TextureSize = 96;
        public const int Variants = 4;

        public static readonly Color Colour = new Color(0.30f, 0.012f, 0.015f, 1.0f);

        private static readonly List<BloodSplat> Live = new List<BloodSplat>();
        private static Texture2D[] _textures;
        private static Material _material;
        private static Mesh _quad;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>How many splats are on the ground and the walls right now.</summary>
        public static int Count => Live.Count;

        /// <summary>How many wounds have been marked (for tests).</summary>
        public static int Marked { get; private set; }

        /// <summary>
        /// Marks a wound at <paramref name="point"/>, the blow having gone <paramref name="away"/> (from the striker through the body): the
        /// floor under the wound, the floor where the spray lands, and a wall behind within reach. <paramref name="strength"/> as the burst's.
        /// </summary>
        public static void Mark(Vector3 point, Vector3 away, float strength, Transform body)
        {
            Marked++;
            strength = Mathf.Clamp(float.IsNaN(strength) ? 1.0f : strength, 0.3f, 3.0f);
            Vector3 flat = away;
            flat.y = 0.0f;
            if (flat.sqrMagnitude < 1e-6f)
            {
                flat = body != null ? -body.forward : Vector3.forward;
            }
            flat.Normalize();
            var random = new System.Random(unchecked((int)(Time.frameCount * 7919 + point.x * 131.0f + point.z * 17.0f)));
            float bodyRadius = 0.3f;

            // Under the wound: the body's own blood, round.
            if (Surface(point + Vector3.up * 0.2f, Vector3.down, FloorReachMetres + 0.2f, body, out RaycastHit floor))
            {
                Place(floor.point, floor.normal, (0.42f + 0.16f * (float)random.NextDouble()) * Mathf.Sqrt(strength), 1.15f, flat, random);
            }
            // Where the spray comes down: further along the blow, bigger, stretched the way it flew.
            Vector3 landing = point + flat * ((0.7f + 0.5f * (float)random.NextDouble()) * strength) + Vector3.up * 0.3f;
            if (Surface(landing, Vector3.down, FloorReachMetres + 0.3f, body, out RaycastHit far))
            {
                Place(far.point, far.normal, (0.55f + 0.3f * (float)random.NextDouble()) * Mathf.Sqrt(strength), 1.6f, flat, random);
            }
            // A wall behind the body, if one is close: the spray on it.
            if (Surface(point - flat * bodyRadius, flat, WallReachMetres + bodyRadius, body, out RaycastHit wall) && Mathf.Abs(wall.normal.y) < 0.5f)
            {
                Place(wall.point, wall.normal, (0.4f + 0.25f * (float)random.NextDouble()) * Mathf.Sqrt(strength), 1.3f, Vector3.up, random);
            }
        }

        /// <summary>Takes every splat away (a test, a new fight).</summary>
        public static void Clear()
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                if (Live[i] != null)
                {
                    Object.Destroy(Live[i].gameObject);
                }
            }
            Live.Clear();
        }

        /// <summary>
        /// The nearest surface along the ray that is not a body (its own capsule, a ragdoll's limbs) and not something loose (a shard). Shared
        /// with the fire (FireScene finds its floors and the jerry can its landing with it).
        /// </summary>
        public static bool Surface(Vector3 origin, Vector3 direction, float reach, Transform body, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(origin, direction, Hits, reach, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.distance >= nearest || hit.collider == null)
                {
                    continue;
                }
                if (body != null && (hit.transform == body || hit.transform.IsChildOf(body)))
                {
                    continue;
                }
                if (hit.collider.GetComponentInParent<IDamageable>() != null)
                {
                    continue;
                }
                Rigidbody loose = hit.collider.attachedRigidbody;
                if (loose != null && !loose.isKinematic)
                {
                    continue;
                }
                nearest = hit.distance;
                best = hit;
            }
            return nearest < float.MaxValue;
        }

        private static void Place(Vector3 point, Vector3 normal, float size, float stretch, Vector3 along, System.Random random)
        {
            while (Live.Count >= MaxSplats)
            {
                BloodSplat oldest = Live[0];
                Live.RemoveAt(0);
                if (oldest != null)
                {
                    Object.Destroy(oldest.gameObject);
                }
            }
            var splat = new GameObject("BloodSplat");
            // Laid on the surface, its long side the way the blood flew, turned a little at random.
            Vector3 lie = Vector3.ProjectOnPlane(along, normal);
            if (lie.sqrMagnitude < 1e-4f)
            {
                lie = Vector3.ProjectOnPlane(Vector3.forward, normal);
                if (lie.sqrMagnitude < 1e-4f)
                {
                    lie = Vector3.ProjectOnPlane(Vector3.right, normal);
                }
            }
            Quaternion onSurface = Quaternion.LookRotation(lie.normalized, normal);
            float spin = ((float)random.NextDouble() - 0.5f) * 50.0f;
            splat.transform.SetPositionAndRotation(point + normal * SurfaceOffset, onSurface * Quaternion.AngleAxis(spin, Vector3.up));
            splat.transform.localScale = new Vector3(size, 1.0f, size * stretch);

            MeshFilter filter = splat.AddComponent<MeshFilter>();
            filter.sharedMesh = Quad();
            MeshRenderer renderer = splat.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            BloodSplat marker = splat.AddComponent<BloodSplat>();
            marker.Begin(renderer, Textures()[random.Next(Variants)]);
            Live.Add(marker);
        }

        internal static void Forget(BloodSplat splat)
        {
            Live.Remove(splat);
        }

        /// <summary>An unlit, alpha-blended material that works at runtime without a material inspector to set the blend up for it (the fire's marks use it too).</summary>
        public static Material AlphaBlended(string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = name, color = Color.white };
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1.0f);   // transparent
                material.SetFloat("_Blend", 0.0f);     // alpha
                material.SetFloat("_ZWrite", 0.0f);
                material.SetFloat("_AlphaClip", 0.0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_SrcBlendAlpha"))
                {
                    material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                    material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return material;
        }

        private static Material Material()
        {
            if (_material == null)
            {
                _material = AlphaBlended("BloodSplat");
            }
            return _material;
        }

        /// <summary>A unit quad on the XZ plane, facing up, its long side along +Z (every mark laid on the ground is one).</summary>
        public static Mesh Quad()
        {
            if (_quad != null)
            {
                return _quad;
            }
            _quad = new Mesh { name = "BloodSplatQuad" };
            _quad.SetVertices(new[]
            {
                new Vector3(-0.5f, 0.0f, -0.5f), new Vector3(0.5f, 0.0f, -0.5f), new Vector3(0.5f, 0.0f, 0.5f), new Vector3(-0.5f, 0.0f, 0.5f),
            });
            _quad.SetUVs(0, new[] { new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f) });
            _quad.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            _quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _quad.RecalculateBounds();
            return _quad;
        }

        /// <summary>The splat shapes: a ragged blob, droplets flung out along +V (the way the blood flew), a drip or two.</summary>
        internal static Texture2D[] Textures()
        {
            if (_textures != null)
            {
                return _textures;
            }
            _textures = new Texture2D[Variants];
            for (int v = 0; v < Variants; v++)
            {
                _textures[v] = SplatTexture(v);
            }
            return _textures;
        }

        private static Texture2D SplatTexture(int variant)
        {
            var random = new System.Random(1000 + variant * 7);
            int size = TextureSize;
            var alpha = new float[size * size];

            // The blob: a radius that wanders round the rim.
            float[] harmonic = { (float)random.NextDouble() * 6.28f, (float)random.NextDouble() * 6.28f, (float)random.NextDouble() * 6.28f };
            void Blob(float cx, float cy, float radius, float ragged)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt((cx - radius * 1.4f) * size));
                int x1 = Mathf.Min(size - 1, Mathf.CeilToInt((cx + radius * 1.4f) * size));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((cy - radius * 1.4f) * size));
                int y1 = Mathf.Min(size - 1, Mathf.CeilToInt((cy + radius * 1.4f) * size));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = (x + 0.5f) / size - cx;
                        float dy = (y + 0.5f) / size - cy;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Atan2(dy, dx);
                        float edge = radius * (1.0f + ragged * (0.5f * Mathf.Sin(3.0f * a + harmonic[0]) + 0.3f * Mathf.Sin(5.0f * a + harmonic[1])
                            + 0.2f * Mathf.Sin(8.0f * a + harmonic[2])));
                        float cover = Mathf.Clamp01((edge - r) / (1.5f / size) + 0.5f);
                        int i = y * size + x;
                        alpha[i] = Mathf.Max(alpha[i], cover);
                    }
                }
            }

            Blob(0.5f, 0.42f, 0.2f + 0.05f * (float)random.NextDouble(), 0.35f);
            // Droplets flung out, mostly along +V (up the texture), a few anywhere.
            int droplets = 7 + random.Next(6);
            for (int d = 0; d < droplets; d++)
            {
                bool flung = random.NextDouble() < 0.65;
                float angle = flung ? Mathf.PI * 0.5f + ((float)random.NextDouble() - 0.5f) * 1.2f : (float)random.NextDouble() * 6.2831f;
                float distance = 0.26f + (float)random.NextDouble() * 0.2f;
                float radius = 0.015f + (float)random.NextDouble() * 0.035f;
                Blob(0.5f + Mathf.Cos(angle) * distance, 0.42f + Mathf.Sin(angle) * distance, radius, 0.2f);
            }
            // A drip or two, down the texture (-V): on a wall they run, on the floor they read as a smear.
            int drips = 1 + random.Next(2);
            for (int d = 0; d < drips; d++)
            {
                float x = 0.44f + (float)random.NextDouble() * 0.12f;
                float length = 0.12f + (float)random.NextDouble() * 0.16f;
                for (float t = 0.0f; t < length; t += 0.01f)
                {
                    Blob(x, 0.42f - 0.1f - t, 0.012f * (1.0f - t / length * 0.6f), 0.0f);
                }
            }

            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                float a = alpha[i];
                // A little darker in the middle, a touch of grain so no two pixels are the same flat red.
                float grain = 0.92f + 0.08f * (float)random.NextDouble();
                pixels[i] = new Color(Colour.r * grain, Colour.g * grain, Colour.b * grain, a);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BloodSplat" + variant, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }

    /// <summary>One splat on a surface: lives <see cref="BloodSplats.LifeSeconds"/>, fades over the last <see cref="BloodSplats.FadeSeconds"/>, then goes.</summary>
    public sealed class BloodSplat : MonoBehaviour
    {
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _age;

        /// <summary>How long this splat has been on the surface, seconds.</summary>
        public float Age => _age;

        public void Begin(MeshRenderer renderer, Texture2D texture)
        {
            _renderer = renderer;
            _block = new MaterialPropertyBlock();
            _block.SetTexture("_BaseMap", texture);
            _block.SetTexture("_MainTex", texture);
            _block.SetColor("_BaseColor", Color.white);
            _block.SetColor("_Color", Color.white);
            _renderer.SetPropertyBlock(_block);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= BloodSplats.LifeSeconds)
            {
                Destroy(gameObject);
                return;
            }
            float left = BloodSplats.LifeSeconds - _age;
            if (left < BloodSplats.FadeSeconds && _renderer != null)
            {
                var faded = new Color(1.0f, 1.0f, 1.0f, Mathf.Clamp01(left / BloodSplats.FadeSeconds));
                _block.SetColor("_BaseColor", faded);
                _block.SetColor("_Color", faded);
                _renderer.SetPropertyBlock(_block);
            }
        }

        private void OnDestroy()
        {
            BloodSplats.Forget(this);
        }
    }
}
