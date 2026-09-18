using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Verifies the street grid, partly against output dumped from the
    /// ACTUAL Godot build (tests/scratch_city_golden_dump.gd) and partly
    /// against the new district/neighbourhood behaviour Godot never had.
    ///
    /// NO LONGER A FULL-PARITY TEST. EXPLICIT ASK: "separate areas by
    /// neighborhood ... not everything should be a blocky grid" --
    /// CityLayout.IsStreetChunk now intentionally diverges from
    /// city_chunk.gd's own single global period inside any district whose
    /// own LocalStreetPeriodFor rolls something other than 4. What still
    /// has to hold, and what StreetGridMatchesGodotInsidePeriodFourDistricts
    /// actually checks, is that every golden chunk whose OWN district still
    /// happens to roll the original period (about 2 in 5, by
    /// LocalStreetPeriods' own weighting) still agrees with Godot exactly --
    /// proving the new grid is a real superset of the old one rather than an
    /// unrelated rewrite that happens to look similar.
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
        public void StreetGridMatchesGodotInsidePeriodFourDistricts()
        {
            GoldenData golden = LoadGolden();
            Assert.IsNotNull(golden.street_chunks, "golden data has no street_chunks");
            Assert.Greater(golden.street_chunks.Length, 0, "golden street data is empty");

            Assert.AreEqual(golden.street_period, CityLayout.StreetPeriod,
                "the ported StreetPeriod disagrees with the Godot original");

            List<string> mismatches = new List<string>();
            int checkedCount = 0;
            foreach (GoldenStreetChunk chunk in golden.street_chunks)
            {
                int districtX = CityLayout.DistrictIndex(chunk.x);
                int districtY = CityLayout.DistrictIndex(chunk.y);
                if (CityLayout.LocalStreetPeriodFor(districtX, districtY) != CityLayout.StreetPeriod)
                {
                    // This chunk's own neighbourhood intentionally rolled a
                    // different block size -- see this class's own doc
                    // comment for why that is expected, not a bug.
                    continue;
                }

                checkedCount++;
                bool ported = CityLayout.IsStreetChunk(chunk.x, chunk.y);
                if (ported != chunk.is_street)
                {
                    mismatches.Add($"({chunk.x},{chunk.y}) godot={chunk.is_street} unity={ported}");
                }
            }

            Assert.Greater(checkedCount, 0, "no golden chunk fell in a period-4 district -- nothing was actually checked");
            Assert.IsEmpty(mismatches,
                $"{mismatches.Count}/{checkedCount} period-4-district chunks disagree with the Godot " +
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

        /// <summary>
        /// EXPLICIT BUG REPORT (SECOND ONE): "this highway is blocking this
        /// road make this impossible." The first fix (a two-piece curve
        /// that still let clearance reach a true 0 in a short final stretch
        /// tightly confined around the exit's own centre) turned out to be
        /// wrong in a way no tighter radius could fix: `HighwayExitChunks`
        /// is a whole multiple of `DistrictChunks`, which makes EVERY exit
        /// candidate row also a real arterial crossing by construction --
        /// confirmed directly with a scratch diagnostic that checked every
        /// real exit along a real line and found 100% of them sitting on a
        /// genuine cross-arterial. A true ground touchdown at an exit was
        /// therefore always a solid slab on top of a live street's own
        /// lanes. This test checks the actual fix: the floor now holds
        /// EVERYWHERE, including at the exit's own centre -- there is no
        /// longer a special case that can ever reach a true 0 clearance.
        /// </summary>
        [Test]
        public void HighwayClearanceNeverDropsBelowTheFloorAnywhereIncludingRightAtAnExit()
        {
            const int laneKey = 0;
            const float minimumFraction = 5.0f / 14.0f;   // matches CityMeshBuilder's own real ratio

            int exitChunk = -1;
            for (int candidate = 0; candidate < 4000; candidate++)
            {
                if (CityLayout.HighwayHasExitAt(candidate, laneKey))
                {
                    exitChunk = candidate;
                    break;
                }
            }
            Assert.AreNotEqual(-1, exitChunk, "no highway exit found on this lane within a reasonable search -- test setup is broken");

            // EVERY REAL HIGHWAY EXIT COINCIDES WITH A REAL CROSS-STREET --
            // this is the actual root cause, not an occasional coincidence.
            // Asserted here so this test would fail loudly if that
            // structural fact of HighwayExitChunks/DistrictChunks ever
            // changed without anyone noticing the reasoning above no
            // longer applies.
            Assert.IsTrue(
                CityLayout.IsStreetChunk(laneKey - 1, exitChunk) && CityLayout.IsStreetChunk(laneKey + 1, exitChunk),
                "expected every highway exit to sit on a real cross-arterial by construction -- if this is no " +
                "longer true, HighwayClearanceTaperAt's own floor-everywhere design may not need to be this strict");

            double exitCentre = CityLayout.SpanCentre(exitChunk, 1);

            // Right at the exit's own centre: NEVER a true 0 anymore.
            float atExit = CityLayout.HighwayClearanceTaperAt(laneKey, exitCentre, minimumFraction);
            Assert.GreaterOrEqual(atExit, minimumFraction - 0.001f,
                "the exit's own centre -- which is always a real cross-street -- must stay at or above the floor");

            // One whole chunk away, and far away: still the same invariant,
            // easing up toward full clearance.
            float oneChunkAway = CityLayout.HighwayClearanceTaperAt(
                laneKey, exitCentre + CityLayout.ChunkSize, minimumFraction);
            Assert.GreaterOrEqual(oneChunkAway, minimumFraction - 0.001f);

            float farAway = CityLayout.HighwayClearanceTaperAt(
                laneKey, exitCentre + CityLayout.HighwayExitTaperDistanceMetres * 2.0, minimumFraction);
            Assert.AreEqual(1.0f, farAway, 0.001f, "far from any exit should be full clearance");

            // Monotonic across the whole approach -- no dip back down partway.
            float previous = minimumFraction;
            for (double distance = 0.0; distance <= CityLayout.HighwayExitTaperDistanceMetres * 1.5;
                 distance += CityLayout.ChunkSize * 0.25)
            {
                float value = CityLayout.HighwayClearanceTaperAt(laneKey, exitCentre + distance, minimumFraction);
                Assert.GreaterOrEqual(value, minimumFraction - 0.0001f,
                    $"clearance dropped below the floor at {distance}m from the exit");
                Assert.GreaterOrEqual(value, previous - 0.0001f,
                    $"clearance taper dipped back down at {distance}m from the exit");
                previous = value;
            }
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
