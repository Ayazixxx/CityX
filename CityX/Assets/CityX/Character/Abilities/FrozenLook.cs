using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A frozen body's look (docs/FROST.md; owner, 2026-10-29: "instead of it being frozen in like a cylinder, make it so its just his body
    /// freezes in the position its in and has a slightly light blue tinge to it"): a thin see-through layer of pale icy blue drawn over every
    /// part of the body, and what it holds, on its own surface (the same mesh in the same pose, one more material on each renderer), coming
    /// on in a moment and going on the thaw. The pose itself is held by <see cref="Chill"/> (the Animator stopped). A layer, not the body's
    /// own colour pulled toward blue: a painted body's colour is its texture, and a black one stays black under any colour it is multiplied by.
    /// </summary>
    public sealed class FrozenLook : MonoBehaviour
    {
        /// <summary>The icy blue laid over the body, and how much of it shows at full (a tinge: the body still shows through).</summary>
        public static readonly Color Ice = new Color(0.72f, 0.87f, 1.0f);

        public const float SheenAlpha = 0.35f;

        /// <summary>How long the tinge takes to come on, and to go, seconds.</summary>
        public const float FreezeSeconds = 0.2f;

        public const float ThawSeconds = 0.35f;

        private static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");
        private static Material _sheen;

        private struct Layered
        {
            public Renderer Renderer;
            public int Index;   // the layer's slot, kept: reading a renderer's materials makes a new array every time
        }

        private readonly List<Layered> _layered = new List<Layered>();
        private MaterialPropertyBlock _block;
        private float _strength;
        private bool _on;

        /// <summary>0 to 1, how far the tinge has come on.</summary>
        public float Strength => _strength;

        public bool IsShowing => _on || _strength > 0.0f;

        private MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        /// <summary>The layer's material, shared by every frozen body (its strength is each renderer's own, through a property block).</summary>
        public static Material Sheen
        {
            get
            {
                if (_sheen == null)
                {
                    _sheen = BloodSplats.AlphaBlended("FrostSheen");
                    _sheen.SetColor(BaseColourId, new Color(Ice.r, Ice.g, Ice.b, SheenAlpha));
                }
                return _sheen;
            }
        }

        /// <summary><paramref name="materials"/> without the layer (a piece cut off a frozen body is not frozen).</summary>
        public static Material[] WithoutSheen(Material[] materials)
        {
            if (_sheen == null || System.Array.IndexOf(materials, _sheen) < 0)
            {
                return materials;
            }
            var kept = new List<Material>(materials.Length);
            foreach (Material material in materials)
            {
                if (material != _sheen)
                {
                    kept.Add(material);
                }
            }
            return kept.ToArray();
        }

        /// <summary>Lays the tinge on (or lets it go).</summary>
        public void Show(bool on)
        {
            if (on && !IsShowing)
            {
                Layer();
            }
            _on = on;
            enabled = true;
        }

        private void LateUpdate()
        {
            float rate = _on ? 1.0f / FreezeSeconds : 1.0f / ThawSeconds;
            _strength = Mathf.MoveTowards(_strength, _on ? 1.0f : 0.0f, rate * Time.deltaTime);
            if (_strength <= 0.0f && !_on)
            {
                Unlayer();
                enabled = false;
                return;
            }
            Apply(_strength);
        }

        private void OnDisable()
        {
            if (!IsShowing)
            {
                return;
            }
            Unlayer();
            _strength = 0.0f;
            _on = false;
        }

        /// <summary>The layer added to every solid part of the body (not its effects: particles, trails, lines), drawn clear until it comes on.</summary>
        private void Layer()
        {
            _layered.Clear();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                {
                    continue;
                }
                Material[] materials = renderer.sharedMaterials;
                if (materials.Length == 0 || System.Array.IndexOf(materials, Sheen) >= 0)
                {
                    continue;
                }
                System.Array.Resize(ref materials, materials.Length + 1);
                materials[materials.Length - 1] = Sheen;
                renderer.sharedMaterials = materials;
                _layered.Add(new Layered { Renderer = renderer, Index = materials.Length - 1 });
            }
            Apply(0.0f);
        }

        private void Apply(float strength)
        {
            foreach (Layered layered in _layered)
            {
                if (layered.Renderer == null)
                {
                    continue;
                }
                layered.Renderer.GetPropertyBlock(Block, layered.Index);
                Block.SetColor(BaseColourId, new Color(Ice.r, Ice.g, Ice.b, SheenAlpha * strength));
                layered.Renderer.SetPropertyBlock(Block, layered.Index);
            }
        }

        /// <summary>The layer taken off again; whatever else a renderer's materials became meanwhile (a garment put on) is kept.</summary>
        private void Unlayer()
        {
            foreach (Layered layered in _layered)
            {
                Renderer renderer = layered.Renderer;
                if (renderer == null)
                {
                    continue;
                }
                Material[] materials = renderer.sharedMaterials;
                int last = materials.Length - 1;
                if (last >= 0 && materials[last] == _sheen)
                {
                    Block.Clear();
                    renderer.SetPropertyBlock(Block, last);
                    renderer.sharedMaterials = WithoutSheen(materials);
                }
            }
            _layered.Clear();
        }
    }
}
