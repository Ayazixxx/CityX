using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Verifies the ported street grid against output dumped from the ACTUAL
    /// Godot build (tests/scratch_city_golden_dump.gd), rather than against
    /// my reading of the original.
    ///
    /// This is the part of Phase 2 where the Godot project earns its keep as
    /// PORT_PLAN describes it -- "a spec that answers 'how was this supposed
    /// to behave?'". Street layout is pure integer maths, so it can be
    /// compared exactly; there is no floating-point wiggle room to hide a
    /// disagreement in.
    /// </summary>
    public class CityLayoutTests
    {
        private const string GoldenResourcePath = "CityX.Golden/city_golden";

        private static GoldenData LoadGolden()
        {
            TextAsset asset = Resources.Load<TextAsset>(GoldenResourcePath);
            Assert.IsNotNull(asset,
                $"missing golden data at Resources/{GoldenResourcePath}.json -- regenerate with " +
                "tools/Godot_v4.7.1-stable_win64.exe --headless --path . -s tests/scratch_city_golden_dump.gd");
            return JsonUtility.FromJson<GoldenData>(asset.text);
        }

        [Test]
        public void StreetGridMatchesTheGodotBuildExactly()
        {
            GoldenData golden = LoadGolden();
            Assert.IsNotNull(golden.street_chunks, "golden data has no street_chunks");
            Assert.Greater(golden.street_chunks.Length, 0, "golden street data is empty");

            Assert.AreEqual(golden.street_period, CityLayout.StreetPeriod,
                "the ported StreetPeriod disagrees with the Godot original");

            List<string> mismatches = new List<string>();
            foreach (GoldenStreetChunk chunk in golden.street_chunks)
            {
                bool ported = CityLayout.IsStreetChunk(chunk.x, chunk.y);
                if (ported != chunk.is_street)
                {
                    mismatches.Add($"({chunk.x},{chunk.y}) godot={chunk.is_street} unity={ported}");
                }
            }

            Assert.IsEmpty(mismatches,
                $"{mismatches.Count}/{golden.street_chunks.Length} chunks disagree with the Godot " +
                $"original. First few: {string.Join(", ", mismatches.GetRange(0, Mathf.Min(6, mismatches.Count)))}");
        }

        /// <summary>
        /// Streets repeat correctly into negative space.
        ///
        /// WHAT THIS DOES NOT PROVE, stated because the obvious reading is
        /// wrong: it does NOT distinguish true modulo from C#'s remainder.
        /// `x % P == 0` and `posmod(x, P) == 0` are equivalent for every sign,
        /// because both are just testing divisibility. Checked against the
        /// Godot dump -- 0 of 361 coordinates disagree between the two forms.
        ///
        /// So the street grid would survive a naive port. The modulo VALUE is
        /// a different matter, and that is what the second half of this test
        /// actually guards -- it is the part with teeth, and the part the rest
        /// of the generator will depend on for window columns and room
        /// indices.
        /// </summary>
        [Test]
        public void StreetsRepeatIntoNegativeSpaceAndPositiveModReturnsTrueModulo()
        {
            Assert.IsTrue(CityLayout.IsStreetChunk(-4, -6));
            Assert.IsFalse(CityLayout.IsStreetChunk(-1, -1));
            Assert.IsFalse(CityLayout.IsStreetChunk(-2, -3));
            Assert.IsTrue(CityLayout.IsStreetChunk(-8, -5));

            // These DO discriminate: C#'s % returns -1, -2, -3 here.
            Assert.AreEqual(0, CityLayout.PositiveMod(-4, 4));
            Assert.AreEqual(3, CityLayout.PositiveMod(-1, 4));
            Assert.AreEqual(2, CityLayout.PositiveMod(-2, 4));
            Assert.AreEqual(1, CityLayout.PositiveMod(-7, 4));
            Assert.AreEqual(1, CityLayout.PositiveMod(1, 4));
            Assert.AreEqual(0, CityLayout.PositiveMod(0, 4));
        }

        // --- golden file shape (JsonUtility needs plain serializable classes) ---

        [System.Serializable]
        private class GoldenData
        {
            public float chunk_size;
            public int street_period;
            public GoldenStreetChunk[] street_chunks;
        }

        [System.Serializable]
        private class GoldenStreetChunk
        {
            public int x;
            public int y;
            public bool is_street;
        }
    }
}
