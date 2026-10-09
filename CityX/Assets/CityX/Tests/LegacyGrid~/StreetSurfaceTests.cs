using System.Collections.Generic;
using CityX.View;
using CityX.ViewEditor;
using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// What a street's mesh looks like from above, measured over a real stretch of the game's own city.
    /// </summary>
    public sealed class StreetSurfaceTests
    {
        /// <summary>The game's own world seed (CityMassingSceneBuilder.WorldSeed): the one the reports came from.</summary>
        private const int GameSeed = 61839025;

        private const int Radius = 24;

        /// <summary>An elevated highway or bridge deck is this high above the ground, on purpose.</summary>
        private const float DeckMinimumMetres = 3.5f;

        /// <summary>
        /// "LONG SLOPED PARTS POKING THROUGH SOME STREETS." A hillside ramp was built off the street's
        /// height at the chunk border and reached a few metres into the chunk, where the road is often
        /// lower -- so its grades stood up out of the road: 0.3-1m wedges on ~5% of street chunks. No
        /// ground surface may stand above the terrain's own height on an ordinary street chunk.
        ///
        /// What is left out is above the road ON PURPOSE: an elevated highway or bridge deck (a
        /// deck is metres up), and a junction's closed-arm treatment (a plaza's kerb, a skatepark).
        /// </summary>
        [Test]
        public void NoStreetSurfaceStandsUpThroughTheRoad()
        {
            var world = new CityWorld(GameSeed);
            var offenders = new List<string>();
            int streets = 0;

            for (int cx = -Radius; cx <= Radius; cx++)
            {
                for (int cy = -Radius; cy <= Radius; cy++)
                {
                    if (!CityLayout.IsStreetChunk(cx, cy) || CityLayout.IsHighwayChunk(cx, cy))
                    {
                        continue;
                    }
                    bool junction = CityLayout.IsStreetChunk(cx - 1, cy) && CityLayout.IsStreetChunk(cx + 1, cy)
                                    && CityLayout.IsStreetChunk(cx, cy - 1) && CityLayout.IsStreetChunk(cx, cy + 1);
                    if (junction)
                    {
                        continue;
                    }
                    streets++;

                    StreetAnomalyScan.BuildStreetSurface(world, cx, cy, out MeshBuffer ground, out MeshBuffer pavement);
                    float above = StreetAnomalyScan.HighestGroundAboveTheRoad(world, cx, cy, ground, out string detail, out _, pavement);
                    if (above > StreetAnomalyScan.AboveTheRoadMetres && above < DeckMinimumMetres)
                    {
                        offenders.Add($"({cx},{cy}) {StreetAnomalyScan.Classify(world, cx, cy)}: {detail}");
                    }
                }
            }

            Assert.Greater(streets, 200, "the scan looked at hardly any streets -- the test is not testing anything");
            Assert.IsEmpty(offenders,
                $"{offenders.Count} of {streets} street chunks have ground poking up through the road:\n  " +
                string.Join("\n  ", offenders.GetRange(0, System.Math.Min(15, offenders.Count))));
        }
    }
}
