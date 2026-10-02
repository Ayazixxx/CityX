using System.Globalization;
using System.Text;

namespace CityX.World
{
    /// <summary>
    /// Draws a region of the city as a top-down plan, in SVG.
    ///
    /// WHY THIS EXISTS. Terrain, block massing, building mass and the walkway
    /// lattice are all pure data with no geometry behind them yet, which means
    /// there is no way to LOOK at any of it. Tests can prove the numbers are
    /// self-consistent; they cannot tell you the city is boring, or that every
    /// tower ended up in a line, or that the terraces are too coarse to read.
    /// Those are the failures that actually matter and none of them throw.
    ///
    /// So: a plan view straight from the same functions the game will use, with
    /// no renderer, no scene and no engine in the way. It goes wrong in exactly
    /// the same places the real city would.
    ///
    /// Returns a string rather than writing a file -- pure, so it can be tested,
    /// and the caller decides where it lands.
    /// </summary>
    public static class CityPlanSvg
    {
        private const int PixelsPerChunk = 12;

        /// <summary>
        /// Renders chunks [minChunk, maxChunk] on both axes. North is up, so
        /// world +Z maps to screen -Y.
        /// </summary>
        public static string Render(CityWorld world, SkybridgeLayout bridges, int minChunk, int maxChunk)
        {
            int span = maxChunk - minChunk + 1;
            int size = span * PixelsPerChunk;

            var svg = new StringBuilder(1 << 20);
            svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(size)
               .Append("\" height=\"").Append(size)
               .Append("\" viewBox=\"0 0 ").Append(size).Append(' ').Append(size).Append("\">\n");
            svg.Append("<rect width=\"100%\" height=\"100%\" fill=\"#0d0f12\"/>\n");

            // --- ground: one tile per chunk, shaded by terrace level ---
            svg.Append("<g>\n");
            for (int cx = minChunk; cx <= maxChunk; cx++)
            {
                for (int cy = minChunk; cy <= maxChunk; cy++)
                {
                    int level = LevelOf(world, cx, cy);
                    // Lighter with height, like a contour map. Streets are
                    // pulled a shade darker so the grid stays legible.
                    int tone = 26 + level * 9;
                    if (CityLayout.IsStreetChunk(cx, cy))
                    {
                        tone -= 8;
                    }
                    AppendRect(svg, ScreenX(cx, minChunk), ScreenY(cy, minChunk, maxChunk),
                        PixelsPerChunk, PixelsPerChunk, Grey(tone), null);
                }
            }
            svg.Append("</g>\n");

            // --- buildings ---
            svg.Append("<g>\n");
            for (int cx = minChunk; cx <= maxChunk; cx++)
            {
                for (int cy = minChunk; cy <= maxChunk; cy++)
                {
                    BuildingSite site = world.BuildingAt(cx, cy);
                    if (!site.Exists || site.AnchorX != cx || site.AnchorY != cy)
                    {
                        continue;
                    }

                    // Footprint in chunk units, centred in its span.
                    double halfW = site.Params.Width / (2.0 * CityLayout.ChunkSize);
                    double halfD = site.Params.Depth / (2.0 * CityLayout.ChunkSize);
                    double centreCx = cx + site.Role.SpanX * 0.5 + site.Params.OffsetX / CityLayout.ChunkSize;
                    double centreCy = cy + site.Role.SpanY * 0.5 + site.Params.OffsetZ / CityLayout.ChunkSize;

                    double left = (centreCx - halfW - minChunk) * PixelsPerChunk;
                    double top = (maxChunk + 1 - (centreCy + halfD)) * PixelsPerChunk;
                    double w = halfW * 2.0 * PixelsPerChunk;
                    double h = halfD * 2.0 * PixelsPerChunk;

                    AppendRect(svg, left, top, w, h, MassFill(site.Params.Mass), "#000000");
                }
            }
            svg.Append("</g>\n");

            // --- walkways ---
            svg.Append("<g stroke=\"#e8b06a\" stroke-opacity=\"0.75\" stroke-width=\"1\">\n");
            for (int cx = minChunk; cx <= maxChunk; cx++)
            {
                for (int cy = minChunk; cy <= maxChunk; cy++)
                {
                    BuildingSite site = world.BuildingAt(cx, cy);
                    if (!site.Exists || site.AnchorX != cx || site.AnchorY != cy)
                    {
                        continue;
                    }

                    foreach (BuildingSite neighbour in new[]
                             {
                                 SkybridgeLayout.EastNeighbour(world, site),
                                 SkybridgeLayout.NorthNeighbour(world, site),
                             })
                    {
                        if (!neighbour.Exists)
                        {
                            continue;
                        }
                        int count = bridges.BridgesBetween(site, neighbour).Count;
                        if (count == 0)
                        {
                            continue;
                        }

                        double x1 = (site.CentreX / CityLayout.ChunkSize - minChunk) * PixelsPerChunk;
                        double y1 = (maxChunk + 1 - site.CentreZ / CityLayout.ChunkSize) * PixelsPerChunk;
                        double x2 = (neighbour.CentreX / CityLayout.ChunkSize - minChunk) * PixelsPerChunk;
                        double y2 = (maxChunk + 1 - neighbour.CentreZ / CityLayout.ChunkSize) * PixelsPerChunk;

                        // More walkways on a pair reads as a heavier line.
                        svg.Append("<line x1=\"").Append(F(x1)).Append("\" y1=\"").Append(F(y1))
                           .Append("\" x2=\"").Append(F(x2)).Append("\" y2=\"").Append(F(y2))
                           .Append("\" stroke-width=\"").Append(F(0.6 + count * 0.5)).Append("\"/>\n");
                    }
                }
            }
            svg.Append("</g>\n");

            svg.Append("</svg>\n");
            return svg.ToString();
        }

        private static int LevelOf(CityWorld world, int cx, int cy)
        {
            return (int)(world.Terrain.PadHeightFor(cx, cy) / TerrainField.TerraceStep + 0.5f);
        }

        /// <summary>
        /// Mass reads as weight: the taller the class, the darker and heavier
        /// the block, with the rare supertall picked out in the one warm tone
        /// on the page so it is findable at a glance.
        /// </summary>
        private static string MassFill(BuildingMass mass)
        {
            switch (mass)
            {
                case BuildingMass.LowRise: return "#6f757c";
                case BuildingMass.MidRise: return "#9aa2ab";
                case BuildingMass.Slab: return "#c3cad2";
                case BuildingMass.Tower: return "#e6ebf0";
                case BuildingMass.Supertall: return "#ffb454";
                default: return "#9aa2ab";
            }
        }

        private static double ScreenX(int chunkX, int minChunk)
        {
            return (chunkX - minChunk) * PixelsPerChunk;
        }

        private static double ScreenY(int chunkY, int minChunk, int maxChunk)
        {
            return (maxChunk - chunkY) * PixelsPerChunk;
        }

        private static void AppendRect(StringBuilder svg, double x, double y, double w, double h,
            string fill, string stroke)
        {
            svg.Append("<rect x=\"").Append(F(x)).Append("\" y=\"").Append(F(y))
               .Append("\" width=\"").Append(F(w)).Append("\" height=\"").Append(F(h))
               .Append("\" fill=\"").Append(fill).Append('"');
            if (stroke != null)
            {
                svg.Append(" stroke=\"").Append(stroke).Append("\" stroke-opacity=\"0.45\" stroke-width=\"0.4\"");
            }
            svg.Append("/>\n");
        }

        private static string Grey(int tone)
        {
            if (tone < 0) tone = 0;
            if (tone > 255) tone = 255;
            string hex = tone.ToString("x2", CultureInfo.InvariantCulture);
            return "#" + hex + hex + hex;
        }

        /// <summary>
        /// InvariantCulture, deliberately. On a machine with a comma decimal
        /// separator the default would emit x="12,5" and the whole plan would
        /// silently render as garbage.
        /// </summary>
        private static string F(double v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
