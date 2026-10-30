using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A spell's light, drawn in code: a soft glow of <see cref="Colour"/> and a white-hot middle of <see cref="HotColour"/>, each an additive
    /// material made on first use (SoftGlow), copied from the editor-made light material once one is given (soft particles, shipped in a
    /// build). The arcane spells' is magenta (ArcaneVisuals.Arcane), the Magic Wand's dust white (MagicSpeck.Palette); ArcaneVisuals draws
    /// sprites, trails, motes and bursts in either.
    /// </summary>
    public sealed class GlowPalette
    {
        public readonly Color Colour;

        public readonly Color HotColour;

        private readonly string _name;
        private Material _template;
        private Material _glow;
        private Material _hot;

        /// <summary>Its materials are named <paramref name="name"/>Glow and <paramref name="name"/>Hot.</summary>
        public GlowPalette(string name, Color colour, Color hotColour)
        {
            _name = name;
            Colour = colour;
            HotColour = hotColour;
        }

        public Material Glow => _glow != null ? _glow : (_glow = SoftGlow.Make(_name + "Glow", Colour, true, _template));

        public Material Hot => _hot != null ? _hot : (_hot = SoftGlow.Make(_name + "Hot", HotColour, true, _template));

        /// <summary>Makes the materials from <paramref name="template"/> from now on; false when there is none, or it already did.</summary>
        public bool UseTemplate(Material template)
        {
            if (template == null || template == _template)
            {
                return false;
            }
            _template = template;
            _glow = null;
            _hot = null;
            return true;
        }
    }
}
