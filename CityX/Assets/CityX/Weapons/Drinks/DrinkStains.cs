using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// WHAT A THROWN FULL BOTTLE LEAVES (docs/PHIZZ_CAN.md; owner, 2026-11-23, with a photo of a wet stain on concrete: "make a stain like this appear
    /// on the floor when you throw a full phizz bottle and have it fade within a minute"). Where the bottle bursts, the floor under it is stained:
    /// a dark wet patch, a ragged blob with a fainter run-off trailing the way the bottle was going, a little speckled.
    /// Drawn in code as the blood's splats are (BloodSplats): one texture, a flat quad laid just off the floor; but it DARKENS the floor it lies on
    /// (a multiply), as a wet patch does, so it is never lighter than the ground, by night as by day (the owner's rule for gore at night: no pale
    /// bits shining in the dark). It stays dark
    /// <see cref="HoldSeconds"/>, then dries away and is gone at <see cref="LifeSeconds"/>. Every machine runs the bottle's own flight, so every
    /// player sees it where it burst.
    /// </summary>
    public static class DrinkStains
    {
        public const float LifeSeconds = 60.0f;
        public const float HoldSeconds = 15.0f;
        public const int MaxStains = 16;

        /// <summary>Across, metres on the 2.6 m body, and how much longer it runs the way the bottle went.</summary>
        public const float Size = 1.1f;
        public const float Stretch = 1.35f;

        /// <summary>How far below where it burst the floor may be (it bursts on a wall, the drink runs down it).</summary>
        public const float FloorReachMetres = 3.0f;

        /// <summary>Just off the floor, under the blood's splats (BloodSplats.SurfaceOffset), which draw over it.</summary>
        public const float SurfaceOffset = 0.009f;

        public const int TextureSize = 128;

        /// <summary>What the wet patch multiplies the ground by: darker, toward a yellow brown (the soda); its run-off less.</summary>
        public static readonly Color Tint = new Color(0.58f, 0.48f, 0.24f, 1.0f);
        public const float PatchStrength = 0.9f;
        public const float RunOffStrength = 0.45f;

        /// <summary>The ground's grain through the wet: some specks darker still.</summary>
        public const float SpeckShare = 0.05f;
        public const float SpeckDarker = 0.75f;

        private static readonly List<DrinkStain> Live = new List<DrinkStain>();
        private static Texture2D _texture;
        private static Material _material;

        /// <summary>How many stains are on the ground now.</summary>
        public static int Count => Live.Count;

        /// <summary>How dark a stain is <paramref name="age"/> seconds after it was left, 1 to 0: dark at first, drying away, gone at a minute.</summary>
        public static float AlphaAt(float age) =>
            age <= HoldSeconds ? 1.0f : Mathf.Clamp01(1.0f - (age - HoldSeconds) / (LifeSeconds - HoldSeconds));

        /// <summary>
        /// Stains the floor under <paramref name="point"/>, the drink going <paramref name="along"/> (its run-off trails that way); nothing when there
        /// is no floor within reach. <paramref name="ignore"/> is the bottle itself.
        /// </summary>
        public static bool Leave(Vector3 point, Vector3 along, Transform ignore)
        {
            if (!BloodSplats.Surface(point + Vector3.up * 0.3f, Vector3.down, FloorReachMetres + 0.3f, ignore, out RaycastHit floor))
            {
                return false;
            }
            while (Live.Count >= MaxStains)
            {
                DrinkStain oldest = Live[0];
                Live.RemoveAt(0);
                if (oldest != null)
                {
                    Object.Destroy(oldest.gameObject);
                }
            }
            Vector3 lie = Vector3.ProjectOnPlane(along, floor.normal);
            if (lie.sqrMagnitude < 1e-4f)
            {
                lie = Vector3.ProjectOnPlane(Vector3.forward, floor.normal);
            }
            var stain = new GameObject("DrinkStain");
            stain.transform.SetPositionAndRotation(floor.point + floor.normal * SurfaceOffset, Quaternion.LookRotation(lie.normalized, floor.normal));
            stain.transform.localScale = new Vector3(Size, 1.0f, Size * Stretch);
            stain.AddComponent<MeshFilter>().sharedMesh = BloodSplats.Quad();
            MeshRenderer renderer = stain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            DrinkStain marker = stain.AddComponent<DrinkStain>();
            marker.Begin(renderer, Texture());
            Live.Add(marker);
            return true;
        }

        /// <summary>Takes every stain away (a test).</summary>
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

        internal static void Forget(DrinkStain stain)
        {
            Live.Remove(stain);
        }

        private static Material Material()
        {
            if (_material == null)
            {
                // A multiply with its strength in alpha: the floor times lerp(1, tint, alpha) (the texture's colour is the tint times its alpha, so
                // src * dst + dst * (1 - alpha) is that). The fade scales both.
                _material = BloodSplats.AlphaBlended("DrinkStain");
                _material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.DstColor);
                _material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (_material.HasProperty("_SrcBlendAlpha"))
                {
                    _material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.Zero);
                    _material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                }
            }
            return _material;
        }

        /// <summary>The photo's stain: a ragged blob near the back, and fainter run-off from it along +V, the way the drink went, some specks darker.</summary>
        internal static Texture2D Texture()
        {
            if (_texture != null)
            {
                return _texture;
            }
            int size = TextureSize;
            var random = new System.Random(4242);
            var alpha = new float[size * size];
            void Blob(float cx, float cy, float radius, float ragged, float strength, float phase)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size - cx, dy = (y + 0.5f) / size - cy;
                        float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                        float edge = radius * (1.0f + ragged * (0.45f * Mathf.Sin(3.0f * a + phase) + 0.3f * Mathf.Sin(5.0f * a + phase * 1.7f)
                            + 0.2f * Mathf.Sin(9.0f * a + phase * 2.3f)));
                        float cover = Mathf.Clamp01((edge - r) / (2.5f / size) + 0.5f) * strength;
                        int i = y * size + x;
                        alpha[i] = Mathf.Max(alpha[i], cover);
                    }
                }
            }
            // The patch, then its run-off: lobes trailing from it, each fainter.
            Blob(0.5f, 0.34f, 0.24f, 0.32f, PatchStrength, 0.6f);
            Blob(0.47f, 0.58f, 0.17f, 0.4f, RunOffStrength, 2.1f);
            Blob(0.55f, 0.72f, 0.12f, 0.45f, RunOffStrength * 0.85f, 4.0f);
            Blob(0.5f, 0.84f, 0.07f, 0.5f, RunOffStrength * 0.7f, 5.3f);

            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color tint = random.NextDouble() < SpeckShare ? Tint * SpeckDarker : Tint;
                float a = alpha[i];
                pixels[i] = new Color(tint.r * a, tint.g * a, tint.b * a, a);
            }
            _texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DrinkStain", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            _texture.SetPixels(pixels);
            _texture.Apply(false, false);
            return _texture;
        }
    }

    /// <summary>One stain on the floor: dark, then drying away (<see cref="DrinkStains.AlphaAt"/>), gone at a minute.</summary>
    public sealed class DrinkStain : MonoBehaviour
    {
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _age;

        /// <summary>How long it has been on the floor, seconds.</summary>
        public float Age => _age;

        public void Begin(MeshRenderer renderer, Texture2D texture)
        {
            _renderer = renderer;
            _block = new MaterialPropertyBlock();
            _block.SetTexture("_BaseMap", texture);
            _block.SetTexture("_MainTex", texture);
            Show(1.0f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= DrinkStains.LifeSeconds)
            {
                Destroy(gameObject);
                return;
            }
            if (_age > DrinkStains.HoldSeconds)
            {
                Show(DrinkStains.AlphaAt(_age));
            }
        }

        private void Show(float alpha)
        {
            var tint = new Color(alpha, alpha, alpha, alpha);
            _block.SetColor("_BaseColor", tint);
            _block.SetColor("_Color", tint);
            _renderer.SetPropertyBlock(_block);
        }

        private void OnDestroy()
        {
            DrinkStains.Forget(this);
        }
    }
}
