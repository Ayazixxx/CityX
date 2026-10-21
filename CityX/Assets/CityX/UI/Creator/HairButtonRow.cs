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
    /// Past four styles the row wraps (see PerRow); past eight or so this wants to become a
    /// scrolling list or a picker.
    /// </summary>
    public static class HairButtonRow
    {
        /// <summary>The width the buttons have to share: the column less a margin at each end.</summary>
        public const float Available = 184.0f;

        public const float Gap = 4.0f;

        /// <summary>The widest a button gets, when there are few enough styles for it.</summary>
        public const float MaxWidth = 58.0f;

        public const float Height = 30.0f;

        /// <summary>Most buttons in one row; more styles than this wrap onto further rows.</summary>
        public const int PerRow = 4;

        /// <summary>Distance between rows.</summary>
        public const float RowPitch = 34.0f;

        /// <summary>How many rows <paramref name="count"/> buttons take.</summary>
        public static int Rows(int count)
        {
            return Mathf.Max(1, (Mathf.Max(count, 1) + PerRow - 1) / PerRow);
        }

        /// <summary>A button's width when there are <paramref name="count"/> of them (rows hold at most <see cref="PerRow"/>).</summary>
        public static float ButtonWidth(int count)
        {
            int inRow = Mathf.Clamp(count, 1, PerRow);
            return Mathf.Min(MaxWidth, (Available - (inRow - 1) * Gap) / inRow);
        }

        /// <summary>The x of the <paramref name="index"/>th button's centre, from the middle of the column; each row is centred on its own.</summary>
        public static float CentreX(int index, int count)
        {
            count = Mathf.Max(count, 1);
            int row = index / PerRow;
            int inThisRow = Mathf.Min(PerRow, count - row * PerRow);
            int column = index % PerRow;
            return (column - (inThisRow - 1) * 0.5f) * (ButtonWidth(count) + Gap);
        }

        /// <summary>How far below the first row the <paramref name="index"/>th button sits (zero or negative).</summary>
        public static float RowOffsetY(int index)
        {
            return -(index / PerRow) * RowPitch;
        }

        /// <summary>A caption size that fits the button: the usual 13 while they are wide, smaller once they are not.</summary>
        public static int FontSize(int count)
        {
            return ButtonWidth(count) >= 50.0f ? 13 : 11;
        }
    }
}
