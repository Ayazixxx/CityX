using System;

namespace CityX.World
{
    /// <summary>
    /// How a building's surface is divided into window cells.
    ///
    /// Three lines of arithmetic that the entire facade depends on, ported
    /// from city_chunk.gd's <c>_cell_layout</c>. It earns its own file and its
    /// own tests because of what the Godot original went through to arrive at
    /// it.
    ///
    /// THE BUG THIS SHAPE EXISTS TO PREVENT. An earlier version of the Godot
    /// build had TWO independent grids over the same wall: a window rectangle
    /// painted into the facade texture at its own tiling resolution, and the
    /// real glass geometry laid out against the building's actual dimensions.
    /// They lined up only while their maths happened to agree. Any drift -- a
    /// panel boundary that was not a whole multiple of a texture tile, a
    /// rounding error at a repeat seam, a UV origin that did not match a
    /// column start -- showed up as, in the report's own words, black windows
    /// and transparent windows combined and walls overlapping each other. The
    /// fix was to delete the second grid entirely so only one system can ever
    /// draw something window-shaped.
    ///
    /// The lesson carried into the port: there is ONE layout function, every
    /// surface asks it, and nothing recomputes a grid of its own.
    /// </summary>
    public readonly struct CellLayout
    {
        /// <summary>How many whole cells fit across the dimension. Always at least 1.</summary>
        public readonly int Count;

        /// <summary>
        /// Actual cell size. Chosen so Count * Size == the original dimension
        /// EXACTLY, rather than being the nominal size with a ragged remainder
        /// left at the end.
        /// </summary>
        public readonly float Size;

        public CellLayout(int count, float size)
        {
            Count = count;
            Size = size;
        }

        /// <summary>Nominal window column width, in metres.</summary>
        public const float NominalColumnMetres = 1.6f;

        /// <summary>Nominal floor height, in metres.</summary>
        public const float NominalFloorMetres = 3.2f;

        /// <summary>Fraction of a cell left as frame around each window pane.</summary>
        public const float WindowMarginRatio = 0.14f;

        /// <summary>
        /// Fits whole cells across <paramref name="dimension"/>, snapping to
        /// the nearest whole count of <paramref name="nominal"/>-sized cells
        /// and then stretching them to divide the dimension exactly.
        ///
        /// The exact division is the point. If cells were simply laid at their
        /// nominal size, the last one would be a sliver -- an edge slicing
        /// through the middle of a window -- and every system keying off this
        /// grid would disagree about where that partial cell ended.
        /// </summary>
        public static CellLayout For(float dimension, float nominal)
        {
            if (nominal <= 0.0f)
            {
                // A zero or negative nominal would divide by zero and produce a
                // NaN cell size that silently poisons every position computed
                // from it. One cell spanning the whole surface is wrong, but
                // it is visibly wrong rather than invisibly corrupt.
                return new CellLayout(1, dimension);
            }

            int count = Math.Max(1, (int)Math.Round(dimension / nominal, MidpointRounding.AwayFromZero));
            return new CellLayout(count, dimension / count);
        }

        /// <summary>Start offset of cell <paramref name="index"/> along the dimension.</summary>
        public float StartOf(int index)
        {
            return Size * index;
        }

        /// <summary>The window pane inside a cell, inset by the frame margin.</summary>
        public float PaneSize => Size * (1.0f - WindowMarginRatio * 2.0f);
    }
}
