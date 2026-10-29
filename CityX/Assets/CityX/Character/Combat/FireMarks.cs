using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHAT FIRE LEAVES ON THE GROUND (docs/FIRE.md): a dark glossy puddle under every cell with gasoline on it (so the trail you pour is
    /// there to see), and the black burned spot under every cell that has burned, darkening as the cell burns and staying
    /// <see cref="FireRules.ScorchLifeSeconds"/>. Drawn the blood splats' way: a flat quad a centimetre over the floor, alpha blended, unlit,
    /// its picture made in code.
    /// </summary>
    public static class FireMarks
    {
        public const int TextureSize = 96;
        public const int Variants = 3;

        /// <summary>A mark sits this far off the floor, over the blood (so a scorch covers a splat, not the other way round).</summary>
        public const float SurfaceOffset = 0.016f;

        public const float ScorchAlpha = 0.88f;

        /// <summary>A scorch is this many cells across (they overlap into one burned patch, not a row of spots), a little bigger or smaller each.</summary>
        public const float ScorchCells = 2.0f;

        public static readonly Color ScorchColour = new Color(0.035f, 0.03f, 0.025f, 1.0f);
        public static readonly Color GasolineColour = new Color(0.26f, 0.19f, 0.07f, 1.0f);

        private static readonly Dictionary<FireCell, GroundMark> Gasoline = new Dictionary<FireCell, GroundMark>();
        private static readonly Dictionary<FireCell, GroundMark> Scorch = new Dictionary<FireCell, GroundMark>();
        private static Texture2D[] _scorches;
        private static Texture2D[] _puddles;
        private static Material _material;
        private static FireField _watched;

        public static int GasolineCount => Gasoline.Count;

        public static int ScorchCount => Scorch.Count;

        /// <summary>Follows a field: puddles as it is poured on, scorches as it burns.</summary>
        public static void Watch(FireField field)
        {
            Unwatch();
            _watched = field;
            field.Poured += OnPoured;
            field.Ignited += OnIgnited;
            field.BurnedOut += OnBurnedOut;
        }

        private static void Unwatch()
        {
            if (_watched == null)
            {
                return;
            }
            _watched.Poured -= OnPoured;
            _watched.Ignited -= OnIgnited;
            _watched.BurnedOut -= OnBurnedOut;
            _watched = null;
        }

        private static void OnPoured(FireCell cell)
        {
            if (!cell.HasGasoline)
            {
                return;
            }
            if (!Gasoline.TryGetValue(cell, out GroundMark mark) || mark == null)
            {
                mark = Place("GasolineMark", cell, Puddles()[Variant(cell)], GasolineColour);
                Gasoline[cell] = mark;
            }
            ShowPuddle(cell, mark);
        }

        private static void OnIgnited(FireCell cell)
        {
            if (!Scorch.TryGetValue(cell, out GroundMark mark) || mark == null)
            {
                mark = Place("ScorchMark", cell, Scorches()[Variant(cell)], ScorchColour);
                mark.SetSize(FireRules.CellMetres * ScorchCells * (0.85f + 0.3f * Variant(cell) / (Variants - 1.0f)));
                mark.SetAlpha(0.0f);
                Scorch[cell] = mark;
            }
            mark.Hold();   // a spot still burning does not fade
        }

        private static void OnBurnedOut(FireCell cell)
        {
            if (Scorch.TryGetValue(cell, out GroundMark mark) && mark != null)
            {
                mark.SetAlpha(ScorchAlpha);
                mark.FadeAfter(FireRules.ScorchLifeSeconds, FireRules.ScorchFadeSeconds);
            }
            if (Gasoline.TryGetValue(cell, out GroundMark puddle))
            {
                Gasoline.Remove(cell);
                if (puddle != null)
                {
                    Object.Destroy(puddle.gameObject);
                }
            }
        }

        /// <summary>Each frame: the puddles shrink as their gasoline burns off, the burning spots darken.</summary>
        public static void Tick(FireField field)
        {
            foreach (FireCell cell in field.Burning)
            {
                if (Scorch.TryGetValue(cell, out GroundMark mark) && mark != null)
                {
                    mark.SetAlpha(ScorchAlpha * Mathf.Pow(cell.BurnedShare, 0.5f));
                }
                if (cell.Gasoline && Gasoline.TryGetValue(cell, out GroundMark puddle) && puddle != null)
                {
                    ShowPuddle(cell, puddle);
                }
            }
        }

        private static void ShowPuddle(FireCell cell, GroundMark mark)
        {
            float litres = Mathf.Clamp01(cell.Litres);
            mark.SetSize(FireRules.CellMetres * (1.15f + 0.55f * litres));
            mark.SetAlpha(Mathf.Lerp(0.3f, 0.75f, litres));
        }

        private static int Variant(FireCell cell)
        {
            return Mathf.Abs(cell.Column * 7 + cell.Row * 13) % Variants;
        }

        private static GroundMark Place(string name, FireCell cell, Texture2D texture, Color tint)
        {
            var go = new GameObject(name);
            float spin = Mathf.Abs(cell.Column * 31 + cell.Row * 17) % 360;
            go.transform.SetPositionAndRotation(cell.Centre + Vector3.up * SurfaceOffset, Quaternion.Euler(0.0f, spin, 0.0f));
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BloodSplats.Quad();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            GroundMark mark = go.AddComponent<GroundMark>();
            mark.Begin(renderer, texture, tint);
            mark.SetSize(FireRules.CellMetres * 1.3f);
            return mark;
        }

        private static Material Material()
        {
            if (_material == null)
            {
                _material = BloodSplats.AlphaBlended("FireMark");
            }
            return _material;
        }

        public static void Clear()
        {
            Unwatch();
            foreach (GroundMark mark in Gasoline.Values)
            {
                if (mark != null)
                {
                    Object.Destroy(mark.gameObject);
                }
            }
            foreach (GroundMark mark in Scorch.Values)
            {
                if (mark != null)
                {
                    Object.Destroy(mark.gameObject);
                }
            }
            Gasoline.Clear();
            Scorch.Clear();
        }

        /// <summary>The burned spots: a ragged black blob, sooty, no two alike.</summary>
        private static Texture2D[] Scorches()
        {
            if (_scorches == null)
            {
                _scorches = new Texture2D[Variants];
                for (int v = 0; v < Variants; v++)
                {
                    _scorches[v] = Blob("Scorch" + v, 100 + v * 17, 0.36f, 0.12f, 0.22f, ScorchColour);
                }
            }
            return _scorches;
        }

        /// <summary>The puddles: a smooth-edged pool, a little uneven.</summary>
        private static Texture2D[] Puddles()
        {
            if (_puddles == null)
            {
                _puddles = new Texture2D[Variants];
                for (int v = 0; v < Variants; v++)
                {
                    _puddles[v] = Blob("Puddle" + v, 300 + v * 23, 0.40f, 0.06f, 0.0f, GasolineColour);
                }
            }
            return _puddles;
        }

        /// <summary>
        /// A blob of <paramref name="colour"/>: a disc of about <paramref name="radius"/> (of the texture) whose rim wanders by
        /// <paramref name="ragged"/> round it, with <paramref name="grain"/> of noise inside (soot) and a soft edge.
        /// </summary>
        private static Texture2D Blob(string name, int seed, float radius, float ragged, float grain, Color colour)
        {
            int size = TextureSize;
            var pixels = new Color[size * size];
            float sx = seed * 0.37f, sy = seed * 0.61f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx);
                    float rim = radius + ragged * (Mathf.PerlinNoise(sx + Mathf.Cos(a) * 1.3f, sy + Mathf.Sin(a) * 1.3f) - 0.5f) * 2.0f;
                    float cover = Mathf.Clamp01((rim - r) / 0.05f);
                    float soot = 1.0f - grain * Mathf.PerlinNoise(sx + x * 0.11f, sy + y * 0.11f);
                    pixels[y * size + x] = new Color(colour.r, colour.g, colour.b, cover * soot);
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }

    /// <summary>One mark on the ground: shown at an alpha, held, or fading away after a while and then gone.</summary>
    public sealed class GroundMark : MonoBehaviour
    {
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _alpha;
        private float _holdSeconds = -1.0f;
        private float _fadeSeconds;
        private float _age;

        public float Alpha => _alpha;

        public void Begin(MeshRenderer renderer, Texture2D texture, Color tint)
        {
            _renderer = renderer;
            _block = new MaterialPropertyBlock();
            _block.SetTexture("_BaseMap", texture);
            _block.SetTexture("_MainTex", texture);
            _block.SetColor("_BaseColor", Color.white);
            _block.SetColor("_Color", Color.white);
            _renderer.SetPropertyBlock(_block);
        }

        public void SetSize(float metres)
        {
            transform.localScale = new Vector3(metres, 1.0f, metres);
        }

        public void SetAlpha(float alpha)
        {
            if (Mathf.Approximately(alpha, _alpha) || _renderer == null)
            {
                return;
            }
            _alpha = alpha;
            var faded = new Color(1.0f, 1.0f, 1.0f, Mathf.Clamp01(alpha));
            _block.SetColor("_BaseColor", faded);
            _block.SetColor("_Color", faded);
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>Stays as it is until told otherwise.</summary>
        public void Hold()
        {
            _holdSeconds = -1.0f;
            _age = 0.0f;
        }

        /// <summary>Stays <paramref name="holdSeconds"/>, fades over <paramref name="fadeSeconds"/>, then goes.</summary>
        public void FadeAfter(float holdSeconds, float fadeSeconds)
        {
            _holdSeconds = holdSeconds;
            _fadeSeconds = fadeSeconds;
            _age = 0.0f;
        }

        private void Update()
        {
            if (_holdSeconds < 0.0f)
            {
                return;
            }
            _age += Time.deltaTime;
            float past = _age - _holdSeconds;
            if (past <= 0.0f)
            {
                return;
            }
            if (past >= _fadeSeconds)
            {
                Destroy(gameObject);
                return;
            }
            SetAlpha(FireMarks.ScorchAlpha * (1.0f - past / _fadeSeconds));
        }
    }
}
