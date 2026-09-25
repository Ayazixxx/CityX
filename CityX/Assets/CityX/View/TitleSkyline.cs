using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The live half of the title screen's skyline: the logo that hangs in the
    /// sky above the towers. The towers themselves are baked geometry (see
    /// <see cref="TitleSkylineMesh"/>) and need nothing from this.
    ///
    /// The API is title_screen_city.gd's own: <see cref="SetLogoFlicker"/> is
    /// called every frame with the failing-neon-sign opacity, and
    /// <see cref="SetLogoVisible"/> hides the logo when a full-screen panel
    /// (the character list) goes up over it.
    ///
    /// THE LOGO IS A QUAD IN THE WORLD, NOT A UI IMAGE, because in the
    /// original it stands BEHIND the buildings: the rooftop water tank is drawn
    /// over the foot of the X. A screen-space overlay could never be occluded.
    ///
    /// Opacity goes through a property block rather than the material, so
    /// flickering never edits the shared asset (and works in the editor, where
    /// touching a renderer's .material leaks instances).
    /// </summary>
    public sealed class TitleSkyline : MonoBehaviour
    {
        private static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");

        [Tooltip("The quad the logo is drawn on.")]
        public Renderer LogoRenderer;

        /// <summary>The logo's own tint at full opacity, so a dimmer or brighter look is one number.</summary>
        public Color LogoTint = Color.white;

        private MaterialPropertyBlock _block;

        /// <summary>Opacity last applied, 0..1 -- for tests.</summary>
        public float LogoOpacity { get; private set; } = 1.0f;

        public bool LogoVisible => LogoRenderer != null && LogoRenderer.gameObject.activeSelf;

        /// <summary>Flicker the logo like a failing sign: 1 is fully lit, lower is a dip.</summary>
        public void SetLogoFlicker(float opacity)
        {
            LogoOpacity = Mathf.Clamp01(opacity);
            if (LogoRenderer == null)
            {
                return;
            }
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }
            LogoRenderer.GetPropertyBlock(_block);
            Color tint = LogoTint;
            tint.a = LogoOpacity;
            _block.SetColor(BaseColourId, tint);
            LogoRenderer.SetPropertyBlock(_block);
        }

        public void SetLogoVisible(bool visible)
        {
            if (LogoRenderer != null)
            {
                LogoRenderer.gameObject.SetActive(visible);
            }
        }
    }
}
