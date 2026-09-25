using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Guards the one property the facade depends on: cells divide a surface
    /// EXACTLY, leaving no sliver at the end.
    ///
    /// The Godot build shipped a bug from having two grids over one wall that
    /// agreed only by coincidence -- black and transparent windows mixed
    /// together, walls appearing to overlap. That is easy to reintroduce by
    /// "simplifying" this to nominal-sized cells with a remainder, and the
    /// result would not look like a maths error. It would look like a
    /// rendering bug, and get hunted in the wrong place for a long time.
    /// </summary>
    public class CellLayoutTests
    {
        /// <summary>
        /// The invariant with teeth. Buildings are 9-18m wide and 8-96m tall
        /// with continuous random dimensions, so this sweeps sizes that will
        /// really occur rather than convenient round numbers.
        /// </summary>
        [Test]
        public void CellsAlwaysDivideTheSurfaceExactly()
        {
            for (float dimension = 0.5f; dimension <= 100.0f; dimension += 0.13f)
            {
                foreach (float nominal in new[] { CellLayout.NominalColumnMetres, CellLayout.NominalFloorMetres })
                {
                    CellLayout layout = CellLayout.For(dimension, nominal);
                    float covered = layout.Size * layout.Count;

                    Assert.AreEqual(dimension, covered, 0.0005f,
                        $"{layout.Count} cells of {layout.Size:0.0000}m cover {covered:0.0000}m of a " +
                        $"{dimension:0.0000}m surface -- the leftover strip is where windows start " +
                        "disagreeing with each other");
                }
            }
        }

        [Test]
        public void ThereIsAlwaysAtLeastOneCellEvenOnTinySurfaces()
        {
            foreach (float dimension in new[] { 0.001f, 0.1f, 0.5f, 0.79f, 1.0f })
            {
                CellLayout layout = CellLayout.For(dimension, CellLayout.NominalColumnMetres);
                Assert.GreaterOrEqual(layout.Count, 1,
                    $"a {dimension}m surface produced {layout.Count} cells -- zero cells means a " +
                    "division by zero everywhere downstream");
                Assert.Greater(layout.Size, 0.0f, $"a {dimension}m surface produced a non-positive cell size");
            }
        }

        /// <summary>
        /// Cells stretch to fit, but must not stretch so far that the facade
        /// stops reading as windows. Snapping to the NEAREST whole count keeps
        /// the real size within half a cell of nominal.
        /// </summary>
        [Test]
        public void CellSizeStaysCloseToNominal()
        {
            // Below one nominal cell the surface cannot help but be a single
            // stretched cell, so start above that.
            for (float dimension = 2.0f; dimension <= 100.0f; dimension += 0.17f)
            {
                CellLayout layout = CellLayout.For(dimension, CellLayout.NominalColumnMetres);
                Assert.Greater(layout.Size, CellLayout.NominalColumnMetres * 0.66f,
                    $"{dimension:0.00}m gave {layout.Size:0.00}m cells -- windows are cramped");
                Assert.Less(layout.Size, CellLayout.NominalColumnMetres * 2.0f,
                    $"{dimension:0.00}m gave {layout.Size:0.00}m cells -- windows are stretched wide");
            }
        }

        /// <summary>
        /// Two surfaces of the same size must produce the identical grid.
        /// Sounds trivial; it is the whole reason a door's height can be
        /// snapped to floor rows and still line up with the windows beside it.
        /// </summary>
        [Test]
        public void TheSameDimensionAlwaysProducesTheSameGrid()
        {
            for (float dimension = 1.0f; dimension <= 60.0f; dimension += 0.37f)
            {
                CellLayout a = CellLayout.For(dimension, CellLayout.NominalFloorMetres);
                CellLayout b = CellLayout.For(dimension, CellLayout.NominalFloorMetres);
                Assert.AreEqual(a.Count, b.Count);
                Assert.AreEqual(a.Size, b.Size);
            }
        }

        [Test]
        public void CellStartsMarchAcrossTheSurfaceWithoutGaps()
        {
            CellLayout layout = CellLayout.For(14.3f, CellLayout.NominalColumnMetres);
            for (int i = 0; i < layout.Count; i++)
            {
                float start = layout.StartOf(i);
                float end = start + layout.Size;
                Assert.AreEqual(layout.StartOf(i + 1 <= layout.Count ? i + 1 : i), i + 1 <= layout.Count ? end : start,
                    0.0005f, $"cell {i} ends at {end:0.000} but cell {i + 1} starts elsewhere");
            }
            Assert.AreEqual(14.3f, layout.StartOf(layout.Count), 0.0005f,
                "the last cell does not end at the surface edge");
        }

        [Test]
        public void PaneIsInsetInsideItsCell()
        {
            CellLayout layout = CellLayout.For(16.0f, CellLayout.NominalColumnMetres);
            Assert.Less(layout.PaneSize, layout.Size, "the window pane is not inset -- there is no frame");
            Assert.Greater(layout.PaneSize, 0.0f, "the window pane has no size at all");
            // 14% margin each side leaves 72% of the cell as glass.
            Assert.AreEqual(layout.Size * 0.72f, layout.PaneSize, 0.0001f);
        }

        /// <summary>
        /// A nonsense nominal must not produce NaN. NaN would propagate
        /// silently into every position derived from the grid and show up as
        /// invisible geometry rather than an error.
        /// </summary>
        [Test]
        public void ADegenerateNominalDoesNotProduceNaN()
        {
            foreach (float nominal in new[] { 0.0f, -1.0f })
            {
                CellLayout layout = CellLayout.For(12.0f, nominal);
                Assert.AreEqual(1, layout.Count);
                Assert.IsFalse(float.IsNaN(layout.Size), "cell size is NaN");
                Assert.IsFalse(float.IsInfinity(layout.Size), "cell size is infinite");
                Assert.AreEqual(12.0f, layout.Size, 0.0001f);
            }
        }
    }
}
