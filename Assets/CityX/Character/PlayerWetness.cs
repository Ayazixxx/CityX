using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The player getting wet -- and dry again. Standing in the rain soaks the
    /// body and everything worn; under cover it dries, faster than in the open.
    /// Skin and clothes darken and pick up a sheen as they soak (the same
    /// look wet ground gets: darker, glossier, the gloss going out first as it
    /// dries), so someone who has been out in a storm LOOKS like it.
    ///
    /// The approach is the one the "Soaking Wet" wetness mods for Skyrim and
    /// most character-wetness systems use: no new assets, just a wetness value
    /// that rises while exposed and falls while sheltered, pushed into the
    /// character's materials as extra gloss and a darker base colour.
    ///
    /// NEVER TOUCHES THE SHARED MATERIALS. Every character of a kind shares one
    /// skin asset, and editing it would soak (and, in the editor, permanently
    /// re-save) everybody's. Each renderer gets a per-renderer property block
    /// instead, layered over whatever block a garment tint already set, and
    /// removed again once dry.
    ///
    /// Added at runtime by PlayerInputReader -- only the real, local player
    /// has one -- so no prefab needed rebuilding.
    /// </summary>
    public sealed class PlayerWetness : MonoBehaviour
    {
        /// <summary>How much of its colour a fully soaked surface keeps.</summary>
        public const float SoakedColourMultiplier = 0.62f;

        /// <summary>The gloss a soaked surface reaches.</summary>
        public const float SoakedSmoothness = 0.55f;

        /// <summary>How often the list of renderers to wet is rebuilt: garments come and go.</summary>
        public const float RefreshSeconds = 2.0f;

        private static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private struct Target
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public Color OriginalColour;
            public float OriginalSmoothness;
        }

        private readonly List<Target> _targets = new List<Target>();
        // Made on first use: a MaterialPropertyBlock cannot be constructed in a field
        // initializer (a MonoBehaviour is built on a loading thread).
        private MaterialPropertyBlock _blockInstance;
        private MaterialPropertyBlock _block => _blockInstance ?? (_blockInstance = new MaterialPropertyBlock());
        private float _refreshTimer;
        private float _appliedWetness = -1.0f;
        private bool _applied;

        /// <summary>0..1: 0 dry, 1 soaked to the skin.</summary>
        public float Wetness { get; private set; }

        /// <summary>Puts the body at a wetness -- for tests and the console.</summary>
        public void SetWetness(float wetness)
        {
            Wetness = Mathf.Clamp01(wetness);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            Vector3 chest = transform.position + Vector3.up * 1.2f;
            float rain = CurrentWeather.RainOn(chest);
            float shelter = CurrentWeather.Shelter(chest);
            Wetness = Weather.StepBodyWetness(Wetness, rain, shelter, deltaTime);

            if (Wetness <= 0.0f && !_applied)
            {
                return;
            }

            _refreshTimer -= deltaTime;
            if (_refreshTimer <= 0.0f || _targets.Count == 0)
            {
                Refresh();
                _refreshTimer = RefreshSeconds;
                _appliedWetness = -1.0f;
            }
            Apply();
        }

        /// <summary>Rebuilds the list of things to wet: every lit renderer on the body, and what it looked like dry.</summary>
        private void Refresh()
        {
            // Put anything that has since left the list back to dry first.
            RestoreAll();
            _targets.Clear();

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                {
                    continue;
                }
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null || !material.HasProperty(BaseColourId) || !material.HasProperty(SmoothnessId))
                    {
                        continue;
                    }

                    // What it looks like dry: the material's own values, unless
                    // something (a garment tint) already put a block on it.
                    renderer.GetPropertyBlock(_block, i);
                    Color colour = _block.HasColor(BaseColourId) ? _block.GetColor(BaseColourId) : material.GetColor(BaseColourId);
                    float smoothness = _block.HasFloat(SmoothnessId) ? _block.GetFloat(SmoothnessId) : material.GetFloat(SmoothnessId);
                    _targets.Add(new Target
                    {
                        Renderer = renderer,
                        MaterialIndex = i,
                        OriginalColour = colour,
                        OriginalSmoothness = smoothness,
                    });
                }
            }
        }

        private void Apply()
        {
            if (Mathf.Approximately(Wetness, _appliedWetness))
            {
                return;
            }
            _appliedWetness = Wetness;

            if (Wetness <= 0.0f)
            {
                RestoreAll();
                _applied = false;
                return;
            }

            float darken = Mathf.Lerp(1.0f, SoakedColourMultiplier, Wetness);
            float sheen = Weather.SheenFactor(Wetness);
            for (int i = 0; i < _targets.Count; i++)
            {
                Target target = _targets[i];
                if (target.Renderer == null)
                {
                    continue;
                }
                target.Renderer.GetPropertyBlock(_block, target.MaterialIndex);
                Color dry = target.OriginalColour;
                _block.SetColor(BaseColourId, new Color(dry.r * darken, dry.g * darken, dry.b * darken, dry.a));
                _block.SetFloat(SmoothnessId, Mathf.Lerp(target.OriginalSmoothness, SoakedSmoothness, sheen));
                target.Renderer.SetPropertyBlock(_block, target.MaterialIndex);
            }
            _applied = true;
        }

        /// <summary>Puts every wetted renderer back to exactly how it looked dry.</summary>
        private void RestoreAll()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                Target target = _targets[i];
                if (target.Renderer == null)
                {
                    continue;
                }
                target.Renderer.GetPropertyBlock(_block, target.MaterialIndex);
                _block.SetColor(BaseColourId, target.OriginalColour);
                _block.SetFloat(SmoothnessId, target.OriginalSmoothness);
                target.Renderer.SetPropertyBlock(_block, target.MaterialIndex);
            }
        }

        private void OnDisable()
        {
            RestoreAll();
            _applied = false;
            _appliedWetness = -1.0f;
        }
    }
}
