using System.Globalization;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// A colour as a code (owner, 2026-11-17: "make the color code visible at the bottom and it lets you enter color codes to get an exact hue"):
    /// the six-digit web code, #RRGGBB, as every paint program writes it.
    /// </summary>
    public static class ColourCode
    {
        /// <summary>The colour's code: # and six hex digits, upper case.</summary>
        public static string Of(Color colour) => "#" + ColorUtility.ToHtmlStringRGB(colour);

        /// <summary>
        /// The colour a typed code means: six hex digits or three (each doubled, as on the web), with or without the #, any case, spaces
        /// ignored. False for anything else.
        /// </summary>
        public static bool TryRead(string typed, out Color colour)
        {
            colour = Color.white;
            if (string.IsNullOrEmpty(typed))
            {
                return false;
            }
            string digits = typed.Replace(" ", string.Empty).TrimStart('#');
            if (digits.Length == 3)
            {
                digits = new string(new[] { digits[0], digits[0], digits[1], digits[1], digits[2], digits[2] });
            }
            if (digits.Length != 6 || !int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            {
                return false;
            }
            colour = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            return true;
        }
    }
}
