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

            /// <summary>
            /// What this class last put in the renderer's block. If the block still holds exactly
            /// this, nobody has touched it since and the original above is still its dry look; if
            /// it holds anything else, somebody (a hair dye, a garment tint) changed it, and what
            /// it holds now IS its dry look.
            /// </summary>
            public bool HasWritten;
            public Color WrittenColour;
            public float WrittenSmoothness;
        }

        private readonly List<Target> _targets = new List<Target>();
        // Made on first use: a MaterialPropertyBlock cannot be constructed in a field
        // initializer (a MonoBehaviour is built on a loading thread).
        private MaterialPropertyBlock _blockInstance;
        private MaterialPropertyBlock _block => _blockInstance ?? (_blockInstance = new MaterialPropertyBlock());
        private float _refreshTimer;
        private PlayerLocomotion _locomotion;
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
            // Standing in water soaks the body too, and far faster (Wading.SoakedBy); it dries the rain's way.
            if (_locomotion == null)
            {
                _locomotion = GetComponent<PlayerLocomotion>();
            }
            if (_locomotion != null && _locomotion.WaterDepth > 0.0f)
            {
                float soaked = Wading.SoakedBy(_locomotion.WaterDepthShare);
                if (soaked > Wetness)
                {
                    Wetness = Mathf.MoveTowards(Wetness, soaked, Wading.SoakRate * deltaTime);
                }
            }

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

        /// <summary>
        /// Asks for the list of things to wet to be rebuilt on the next frame, not in up to two
        /// seconds. Something that has just changed a renderer's colour (hair being dyed) says so,
        /// so a body already wet does not show the new colour undarkened for a moment.
        /// </summary>
        public void Invalidate()
        {
            _refreshTimer = 0.0f;
        }

        /// <summary>
        /// Rebuilds the list of things to wet: every lit renderer on the body, and what it looked like dry.
        ///
        /// WHAT "DRY" MEANS IS DECIDED BY LOOKING, NOT REMEMBERED. The first version put everything
        /// back to the colours it had recorded and then read the blocks afresh. That was wrong twice
        /// over: a colour somebody CHANGED in between (a dye, a tint) was overwritten by the old one,
        /// and one that was never in the per-material block at all (the hair's dye used to live in
        /// the whole-renderer block) was recorded as the material's default -- so hair turned back
        /// to the default colour the moment it rained. A block that still holds exactly what this
        /// class wrote keeps its remembered dry look; anything else is taken as the new dry look.
        /// </summary>
        private void Refresh()
        {
            var previous = new Dictionary<(Renderer, int), Target>();
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i].Renderer != null)
                {
                    previous[(_targets[i].Renderer, _targets[i].MaterialIndex)] = _targets[i];
                }
            }
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

                    // What it looks like now: the block's own values, else the material's.
                    renderer.GetPropertyBlock(_block, i);
                    Color colour = _block.HasColor(BaseColourId) ? _block.GetColor(BaseColourId) : material.GetColor(BaseColourId);
                    float smoothness = _block.HasFloat(SmoothnessId) ? _block.GetFloat(SmoothnessId) : material.GetFloat(SmoothnessId);

                    var target = new Target
                    {
                        Renderer = renderer,
                        MaterialIndex = i,
                        OriginalColour = colour,
                        OriginalSmoothness = smoothness,
                    };
                    if (previous.TryGetValue((renderer, i), out Target old))
                    {
                        previous.Remove((renderer, i));
                        if (old.HasWritten && Same(colour, old.WrittenColour) && Mathf.Approximately(smoothness, old.WrittenSmoothness))
                        {
                            // Still exactly what this class wrote: the dry look is the one it remembered.
                            target = old;
                        }
                    }
                    _targets.Add(target);
                }
            }

            // Anything that has since left the list goes back to dry.
            foreach (Target gone in previous.Values)
            {
                Restore(gone);
            }
        }

        private static bool Same(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g)
                   && Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);
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
                var wet = new Color(dry.r * darken, dry.g * darken, dry.b * darken, dry.a);
                float wetSmoothness = Mathf.Lerp(target.OriginalSmoothness, SoakedSmoothness, sheen);
                _block.SetColor(BaseColourId, wet);
                _block.SetFloat(SmoothnessId, wetSmoothness);
                target.Renderer.SetPropertyBlock(_block, target.MaterialIndex);

                target.HasWritten = true;
                target.WrittenColour = wet;
                target.WrittenSmoothness = wetSmoothness;
                _targets[i] = target;
            }
            _applied = true;
        }

        /// <summary>Puts every wetted renderer back to exactly how it looked dry.</summary>
        private void RestoreAll()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                Target target = _targets[i];
                Restore(target);
                // Back to its dry look, so nothing of this class's is left in the block to be mistaken for its own.
                target.HasWritten = false;
                _targets[i] = target;
            }
        }

        private void Restore(Target target)
        {
            if (target.Renderer == null)
            {
                return;
            }
            target.Renderer.GetPropertyBlock(_block, target.MaterialIndex);
            _block.SetColor(BaseColourId, target.OriginalColour);
            _block.SetFloat(SmoothnessId, target.OriginalSmoothness);
            target.Renderer.SetPropertyBlock(_block, target.MaterialIndex);
        }

        private void OnDisable()
        {
            RestoreAll();
            _applied = false;
            _appliedWetness = -1.0f;
        }
    }
}
