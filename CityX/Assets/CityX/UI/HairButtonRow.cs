using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// Where the hairstyle buttons go in the creator's tool column: one row, as many
    /// buttons as there are styles, narrowing to fit.
    ///
    /// The column is 200 units wide and each button used to be a fixed 58, which fits three.
    /// A fourth style would have hung off the edge of the panel. The title's creator and the
    /// in-game redraw creator both lay this row out, so the numbers live here once.
    ///
    /// One row is a limit, not a design: past five or six styles the buttons are too small to
    /// read and this wants to become a scrolling list or a picker.
    /// </summary>
    public static class HairButtonRow
    {
        /// <summary>The width the buttons have to share: the column less a margin at each end.</summary>
        public const float Available = 184.0f;

        public const float Gap = 4.0f;

        /// <summary>The widest a button gets, when there are few enough styles for it.</summary>
        public const float MaxWidth = 58.0f;

        public const float Height = 30.0f;

        /// <summary>A button's width when there are <paramref name="count"/> of them.</summary>
        public static float ButtonWidth(int count)
        {
            count = Mathf.Max(count, 1);
            return Mathf.Min(MaxWidth, (Available - (count - 1) * Gap) / count);
        }

        /// <summary>The x of the <paramref name="index"/>th button's centre, from the middle of the column.</summary>
        public static float CentreX(int index, int count)
        {
            return (index - (Mathf.Max(count, 1) - 1) * 0.5f) * (ButtonWidth(count) + Gap);
        }

        /// <summary>A caption size that fits the button: the usual 13 while they are wide, smaller once they are not.</summary>
        public static int FontSize(int count)
        {
            return ButtonWidth(count) >= 50.0f ? 13 : 11;
        }
    }
}
