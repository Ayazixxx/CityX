using System.IO;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Renders plan views of the city and writes them next to the test results.
    ///
    /// This one is unusual for this suite and worth being honest about: its
    /// real job is to produce something a HUMAN can look at. The assertions
    /// here only catch the ways the exporter itself can break -- they cannot
    /// tell you the city looks good. That judgement is the point of the file
    /// it writes.
    /// </summary>
    public class CityPlanSvgTests
    {
        private static string OutputDirectory
        {
            get
            {
                // Alongside the Unity project rather than inside Assets, so the
                // importer does not try to make an asset out of every render.
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(projectRoot, "Logs", "CityPlans");
            }
        }

        private static string Write(int seed, int minChunk, int maxChunk, string label)
        {
            CityWorld world = new CityWorld(seed);
            SkybridgeLayout bridges = new SkybridgeLayout(seed);
            string svg = CityPlanSvg.Render(world, bridges, minChunk, maxChunk);

            Directory.CreateDirectory(OutputDirectory);
            string path = Path.Combine(OutputDirectory, $"plan-{label}-seed{seed}.svg");
            File.WriteAllText(path, svg);
            TestContext.WriteLine($"wrote {path} ({svg.Length / 1024}kb)");
            return svg;
        }

        [Test]
        public void RenderPlansForInspection()
        {
            // A district, close enough in to read individual buildings.
            string district = Write(20260810, -24, 24, "district");

            // Wider, to judge whether towers clump or spread and whether the
            // terracing reads at all at city scale.
            Write(20260810, -80, 80, "wide");

            // A second seed, because a single seed that happens to look fine
            // proves nothing about the generator.
            Write(77, -24, 24, "district-b");

            Assert.IsTrue(district.StartsWith("<svg"), "output is not SVG");
            Assert.IsTrue(district.TrimEnd().EndsWith("</svg>"), "SVG is truncated");
            StringAssert.Contains("<line", district, "no walkways were drawn at all");
            StringAssert.Contains("#ffb454", Write(20260810, -120, 120, "supertalls"),
                "no supertall appeared in a 240-chunk plan -- the landmark tier is unreachable");
        }

        /// <summary>
        /// Number formatting must not follow the machine's locale. On a comma-
        /// decimal machine the default would emit x="12,5" and every plan would
        /// render as garbage -- on someone else's computer, not this one.
        /// </summary>
        [Test]
        public void CoordinatesUseInvariantDecimalSeparators()
        {
            System.Globalization.CultureInfo original = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture =
                    new System.Globalization.CultureInfo("de-DE");

                CityWorld world = new CityWorld(1);
                string svg = CityPlanSvg.Render(world, new SkybridgeLayout(1), -8, 8);

                Assert.IsFalse(svg.Contains(",\""),
                    "a coordinate ended in a comma -- the exporter is following the machine locale");
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
