using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A picture printed on the T-Shirt, and where: change a number in its Inspector and the shirt's texture is printed again (in Play mode
    /// too, on the shirt being worn). Distances are on the shirt, in centimetres, as you look at the printed side.
    /// </summary>
    [CreateAssetMenu(menuName = "CityX/Shirt Print", fileName = "ShirtPrint")]
    public sealed class ShirtPrint : ScriptableObject
    {
        public enum ShirtSide
        {
            Front,
            Back,
        }

        [Tooltip("The picture: a PNG with a transparent background.")]
        public Texture2D Logo;

        [Tooltip("Cover the whole shirt with the picture, repeated edge to edge (an all-over print such as camo), instead of printing it once. " +
                 "Width, turn and the cm across and up then size, turn and slide the pattern.")]
        public bool CoverTheWholeShirt;

        [Tooltip("Which side of the shirt it is printed on (not used when it covers the whole shirt).")]
        public ShirtSide Side = ShirtSide.Front;

        [Tooltip("How far right of the middle its centre is, in cm, as you look at that side (negative: left). Covering the whole shirt: slides the pattern.")]
        public float RightCm;

        [Tooltip("How far above the middle of the chest (or the back) its centre is, in cm (negative: below). Covering the whole shirt: slides the pattern.")]
        public float UpCm;

        [Tooltip("How wide it is printed, in cm; its height follows the picture's shape. Covering the whole shirt: how wide one copy of the " +
                 "picture is (larger zooms the pattern in).")]
        [Min(1.0f)]
        public float WidthCm = 34.0f;

        [Tooltip("How far it is turned, in degrees, anticlockwise as you look at it (the whole pattern, covering the whole shirt).")]
        [Range(-180.0f, 180.0f)]
        public float RotationDegrees;
    }
}
