using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// WHERE A PRESSED DESIGN GOES ON A SHIRT (docs/HEAT_PRESS.md): one upright rectangle on the front and one on the back, the same size, centred
    /// on the middle line, from just under the front of the collar down to near the hem, and nearly side seam to side seam. A design is one
    /// picture for each, <see cref="Width"/> by <see cref="Height"/> pixels (about 770 a metre, near the shirt texture's own 960).
    /// </summary>
    public static class ShirtDesignArea
    {
        public const int Width = 448;
        public const int Height = 560;

        public const float WidthMetres = 0.58f;
        public const float HeightMetres = 0.725f;

        /// <summary>The rectangle's middle, above (+) or below the chest's middle (ShirtPrintLayout.MiddleHeight), metres: its top clear of the collar's dip.</summary>
        public const float UpMetres = -0.11f;

        public static float PixelsPerMetre => Width / WidthMetres;

        /// <summary>The rectangle's middle on the body, bind pose: its height.</summary>
        public static float MiddleHeight => ShirtPrintLayout.MiddleHeight + UpMetres;

        /// <summary>Where its middle lands on a shirt's texture, the side's middle and scale given (TShirt's fit).</summary>
        public static Vector2 CentreUv(Vector2 sideMiddleUv, float uvPerMetre) => ShirtPrintLayout.CentreUv(sideMiddleUv, uvPerMetre, 0.0f, UpMetres);
    }
}
