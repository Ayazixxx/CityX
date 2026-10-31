using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The open spaces (World/OpenSpace.cs, docs/OPEN_SPACE.md): every plaza block gets a role and a layout of pieces that keep a body's width
    /// apart and off the walks from kerb to kerb; a real share of the blocks stay empty; the same block lays out the same every time.
    /// </summary>
    public sealed class OpenSpaceTests
    {
        private const int Seed = 61256619;
        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));
        private static PlanWorld Plan => World.Plan;

        private static IEnumerable<(int index, PlanBlock block, OpenSpaceLayout layout)> Layouts()
        {
            foreach (int index in Plan.Data.PlazaBlocks)
            {
                OpenSpaceLayout layout = OpenSpace.LayoutFor(Plan, index);
                if (layout != null)
                {
                    yield return (index, Plan.Data.Blocks[index], layout);
                }
            }
        }

        [Test]
        public void TheRolesAreMixedAndAGoodShareOfTheBlocksStayEmpty()
        {
            var counts = new int[6];
            int total = 0;
            foreach ((_, _, OpenSpaceLayout layout) in Layouts())
            {
                counts[(int)layout.Role]++;
                total++;
            }
            Assert.Greater(total, 20, "too few plaza blocks to mean anything");
            TestContext.WriteLine($"{total} open blocks: plaza {counts[0]}, pocket park {counts[1]}, promenade {counts[2]}, bare yard {counts[3]}, vacant lot {counts[4]}, quiet court {counts[5]}");
            Assert.Greater(counts[(int)OpenSpaceRole.Plaza], 0);
            Assert.Greater(counts[(int)OpenSpaceRole.PocketPark], 0);
            Assert.Greater(counts[(int)OpenSpaceRole.BareYard], 0);
            Assert.Less(counts[(int)OpenSpaceRole.Plaza] / (double)total, 0.5, "every open space is a furnished plaza");
            double empty = (counts[(int)OpenSpaceRole.BareYard] + counts[(int)OpenSpaceRole.VacantLot] + counts[(int)OpenSpaceRole.QuietCourt]) / (double)total;
            Assert.Greater(empty, 0.25, "too few empty-feeling blocks (owner: 'there should still be some more empty feeling areas')");
        }

        [Test]
        public void EveryPieceStandsInsideItsBlockClearOfTheOthers()
        {
            int pieces = 0;
            foreach ((int index, PlanBlock block, OpenSpaceLayout layout) in Layouts())
            {
                List<Vec2> ring = block.Inner;
                for (int i = 0; i < layout.Pieces.Count; i++)
                {
                    OpenPiece p = layout.Pieces[i];
                    pieces++;
                    double margin = p.Kind == OpenProp.Hoarding ? 0.3 : OpenSpace.KerbMargin;
                    Assert.IsTrue(OpenSpace.Inside(ring, p.A, margin + p.HalfZ - 1e-6) && OpenSpace.Inside(ring, p.B, margin + p.HalfZ - 1e-6),
                        $"block {index}: {p.Kind} at {p.At} stands over the kerb");
                    if (!p.Blocks) continue;
                    for (int j = 0; j < i; j++)
                    {
                        OpenPiece q = layout.Pieces[j];
                        if (!q.Blocks) continue;
                        double need = p.HalfZ + q.HalfZ + OpenSpace.Clearance - 1e-6;
                        double have = System.Math.Min(System.Math.Min(PlanMath.DistanceToSegment(p.A, q.A, q.B, out _), PlanMath.DistanceToSegment(p.B, q.A, q.B, out _)),
                            System.Math.Min(PlanMath.DistanceToSegment(q.A, p.A, p.B, out _), PlanMath.DistanceToSegment(q.B, p.A, p.B, out _)));
                        Assert.GreaterOrEqual(have, need, $"block {index}: {p.Kind} and {q.Kind} stand too close for a body between them");
                    }
                }
                foreach (Vec2[] lawn in layout.Lawns)
                {
                    foreach (Vec2 corner in lawn)
                    {
                        Assert.IsTrue(OpenSpace.Inside(ring, corner, 1.0), $"block {index}: a lawn reaches the kerb at {corner}");
                    }
                }
            }
            Assert.Greater(pieces, 100, "the open blocks hold almost nothing");
        }

        [Test]
        public void EveryOpenBlockCanBeCrossedOnFootFromAnyKerbToAnyOther()
        {
            int checkedBlocks = 0, shut = 0;
            var firsts = new List<string>();
            foreach ((int index, PlanBlock block, OpenSpaceLayout layout) in Layouts())
            {
                checkedBlocks++;
                if (!OpenSpace.Walkable(layout, block.Inner, 0.45, out double share))
                {
                    shut++;
                    if (firsts.Count < 5) firsts.Add($"block {index} ({layout.Role}, {layout.Pieces.Count} pieces): {share:P0} of the free ground reached");
                }
            }
            TestContext.WriteLine($"{shut} of {checkedBlocks} open blocks cannot be crossed");
            foreach (string line in firsts) TestContext.WriteLine(line);
            Assert.AreEqual(0, shut, $"open blocks a body cannot cross: {string.Join("; ", firsts)}");
        }

        [Test]
        public void TheSameBlockLaysOutTheSameTwice()
        {
            int compared = 0;
            foreach (int index in Plan.Data.PlazaBlocks)
            {
                OpenSpaceLayout a = OpenSpace.LayoutFor(Plan, index), b = OpenSpace.LayoutFor(Plan, index);
                Assert.AreEqual(a.Role, b.Role);
                Assert.AreEqual(a.Pieces.Count, b.Pieces.Count);
                for (int i = 0; i < a.Pieces.Count; i++)
                {
                    Assert.AreEqual(a.Pieces[i].Kind, b.Pieces[i].Kind);
                    Assert.AreEqual(a.Pieces[i].At.X, b.Pieces[i].At.X, 1e-9);
                    Assert.AreEqual(a.Pieces[i].At.Y, b.Pieces[i].At.Y, 1e-9);
                    Assert.AreEqual(a.Pieces[i].Seed, b.Pieces[i].Seed);
                }
                Assert.AreEqual(a.Lawns.Count, b.Lawns.Count);
                if (++compared >= 40) break;
            }
            Assert.Greater(compared, 10);
        }

        [Test]
        public void EachRoleHasItsOwnThings()
        {
            bool sawPlaza = false, sawPark = false, sawBare = false;
            foreach ((int index, PlanBlock block, OpenSpaceLayout layout) in Layouts())
            {
                var kinds = new HashSet<OpenProp>();
                foreach (OpenPiece p in layout.Pieces) kinds.Add(p.Kind);
                double extent = System.Math.Sqrt(System.Math.Abs(PlanMath.SignedArea(block.Inner)));
                switch (layout.Role)
                {
                    case OpenSpaceRole.Plaza:
                        sawPlaza = true;
                        Assert.IsTrue(kinds.Contains(OpenProp.Fountain) || kinds.Contains(OpenProp.Tower) || kinds.Contains(OpenProp.Pergola) || kinds.Contains(OpenProp.Chessboard),
                            $"block {index}: a plaza with no focal piece");
                        Assert.IsTrue(kinds.Contains(OpenProp.SeatBlock) || kinds.Contains(OpenProp.Bench), $"block {index}: a plaza with nothing to sit on");
                        if (extent >= 22.0)
                        {
                            Assert.IsTrue(kinds.Contains(OpenProp.ShadeSail) || kinds.Contains(OpenProp.UmbrellaTable), $"block {index} ({extent:F0} m across): a plaza with no shade");
                        }
                        Assert.IsTrue(layout.KerbBollards);
                        break;
                    case OpenSpaceRole.PocketPark:
                        sawPark = true;
                        Assert.IsTrue(kinds.Contains(OpenProp.Chair), $"block {index}: a pocket park with no chairs");
                        break;
                    case OpenSpaceRole.BareYard:
                        sawBare = true;
                        Assert.LessOrEqual(layout.Pieces.Count, 1, $"block {index}: a bare yard with {layout.Pieces.Count} things on it");
                        Assert.IsFalse(layout.KerbBollards);
                        break;
                    case OpenSpaceRole.VacantLot:
                        foreach (OpenPiece p in layout.Pieces)
                        {
                            Assert.IsTrue(p.Kind == OpenProp.Rubble || p.Kind == OpenProp.Hoarding, $"block {index}: a {p.Kind} on a vacant lot");
                        }
                        break;
                    case OpenSpaceRole.QuietCourt:
                        foreach (OpenPiece p in layout.Pieces)
                        {
                            Assert.AreEqual(OpenProp.Planter, p.Kind, $"block {index}: a {p.Kind} in a quiet court");
                        }
                        break;
                }
            }
            Assert.IsTrue(sawPlaza && sawPark && sawBare, "the city is missing one of the main roles");
        }

        [Test]
        public void ABlockThatIsNoPlazaHasNoLayout()
        {
            int index = -1;
            for (int i = 0; i < Plan.Data.Blocks.Count; i++)
            {
                if (!Plan.Data.Blocks[i].Plaza && Plan.Data.Blocks[i].Inner != null) { index = i; break; }
            }
            Assert.GreaterOrEqual(index, 0);
            Assert.IsNull(OpenSpace.LayoutFor(Plan, index));
        }
    }
}
