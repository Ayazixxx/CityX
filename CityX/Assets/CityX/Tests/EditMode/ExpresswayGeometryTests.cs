using System;
using CityX.World.Plan;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The pure geometry under the expressways: DMath (trig from plain arithmetic, the same on every machine) and the Dubins paths every ramp
    /// and connector is laid along (a right turn is a quarter circle, a cloverleaf's loop turns three quarters, every word lands on its target).
    /// </summary>
    public sealed class ExpresswayGeometryTests
    {
        [Test]
        public void DMathAgreesWithTheSystemTrig()
        {
            for (double a = -20.0; a <= 20.0; a += 0.0137)
            {
                Assert.AreEqual(Math.Sin(a), DMath.Sin(a), 1e-12, $"sin {a}");
                Assert.AreEqual(Math.Cos(a), DMath.Cos(a), 1e-12, $"cos {a}");
            }
            for (double y = -3.0; y <= 3.0; y += 0.173)
            {
                for (double x = -3.0; x <= 3.0; x += 0.191)
                {
                    Assert.AreEqual(Math.Atan2(y, x), DMath.Atan2(y, x), 1e-12, $"atan2 {y} {x}");
                }
            }
        }

        private static Pose At(double x, double y, double degrees) => new Pose(new Vec2(x, y), DMath.Dir(degrees * Math.PI / 180.0));

        [Test]
        public void EveryDubinsWordEndsOnItsTarget()
        {
            Pose from = At(-300.0, -20.0, 0.0);
            Pose to = At(-20.0, -300.0, -90.0);
            int words = 0;
            foreach (DubinsPath.Word w in Enum.GetValues(typeof(DubinsPath.Word)))
            {
                DubinsPath p = DubinsPath.Solve(from, to, 150.0, w);
                if (p == null) continue;
                words++;
                Pose end = p.At(p.Length);
                Assert.Less((end.P - to.P).Length, 1e-6, $"{w} ends {(end.P - to.P).Length:F3} m off its target");
                Assert.Greater(Vec2.Dot(end.Dir, to.Dir), 1.0 - 1e-9, $"{w} ends facing the wrong way");
            }
            Assert.GreaterOrEqual(words, 4);
        }

        [Test]
        public void ARightTurnIsAQuarterCircleAndALoopThreeQuarters()
        {
            // An outer connector of a square cloverleaf: east on one road, south on the other, a quarter turn.
            DubinsPath right = DubinsPath.Shortest(At(-300.0, -20.0, 0.0), At(-20.0, -300.0, -90.0), 150.0);
            Assert.AreEqual(DubinsPath.Word.RSR, right.Kind);
            Assert.AreEqual(0.5 * Math.PI, right.TotalTurn, 1e-6);
            // A loop: leaving east after the crossing, joining northbound before it, turning right the whole way round.
            DubinsPath loop = DubinsPath.Shortest(At(80.0, -20.0, 0.0), At(20.0, -80.0, 90.0), 60.0, 1.85 * Math.PI, DubinsPath.Word.RSR);
            Assert.IsNotNull(loop);
            Assert.AreEqual(1.5 * Math.PI, loop.TotalTurn, 1e-6, "the loop does not turn three quarters of a circle");
        }

        [Test]
        public void ASplineThroughControlPointsIsEvenAndPassesThroughThem()
        {
            var ctrl = new System.Collections.Generic.List<Vec2> { new Vec2(0, 0), new Vec2(300, 40), new Vec2(600, -30), new Vec2(900, 0) };
            var pts = ExpresswayPlanner.Spline(ctrl, false, 6.0);
            for (int i = 1; i < pts.Count; i++)
            {
                Assert.AreEqual(6.0, (pts[i] - pts[i - 1]).Length, 0.35, "uneven spacing");
            }
            foreach (Vec2 c in ctrl)
            {
                double nearest = double.MaxValue;
                foreach (Vec2 p in pts) nearest = Math.Min(nearest, (p - c).Length);
                Assert.Less(nearest, 3.5, "the spline misses a control point");
            }
        }

        /// <summary>A straight main line along +x from the origin, 24 m wide, 20 m up: the network the lot tests cut against.</summary>
        private static ExpresswayNetwork StraightDeck()
        {
            const int n = 51;
            var path = new ViaductPath
            {
                Id = 0,
                Kind = ViaductKind.Mainline,
                P = new Vec2[n], N = new Vec2[n], S = new double[n], Top = new double[n], HalfLeft = new double[n], HalfRight = new double[n],
                ParapetLeft = new bool[n - 1], ParapetRight = new bool[n - 1], Slab = 1.4,
            };
            for (int i = 0; i < n; i++)
            {
                path.P[i] = new Vec2(6.0 * i, 0.0);
                path.N[i] = new Vec2(0.0, 1.0);
                path.S[i] = 6.0 * i;
                path.Top[i] = 20.0;
                path.HalfLeft[i] = path.HalfRight[i] = 12.0;
            }
            var xw = new ExpresswayNetwork();
            xw.Paths.Add(path);
            return xw;
        }

        private static System.Collections.Generic.List<Vec2> Square(double x0, double y0, double x1, double y1) =>
            new System.Collections.Generic.List<Vec2> { new Vec2(x0, y0), new Vec2(x1, y0), new Vec2(x1, y1), new Vec2(x0, y1) };

        [Test]
        public void ALotBesideADeckIsCutBackToTheMarginNotLost()
        {
            ExpresswayNetwork xw = StraightDeck();
            var lot = Square(100.0, 5.0, 160.0, 65.0);   // its bottom 17 m reach under the deck and its margin
            var front = new System.Collections.Generic.List<bool> { true, true, true, true };
            Assert.IsTrue(xw.CutBack(lot, front, out var cut, out var cutFront), "a lot beside a deck was lost");
            double minY = double.MaxValue;
            foreach (Vec2 v in cut) minY = Math.Min(minY, v.Y);
            Assert.AreEqual(12.0 + ExpresswayNetwork.LotMargin, minY, 0.5, "not cut back to the deck's edge plus the margin");
            Assert.AreEqual(60.0 * (65.0 - minY), Math.Abs(PlanMath.SignedArea(cut)), 1.0, "it lost more than the strip by the deck");
            // The cut edge faces the expressway, not a street; the other three still front theirs.
            int fronts = 0;
            for (int i = 0; i < cut.Count; i++)
            {
                Vec2 a = cut[i], b = cut[(i + 1) % cut.Count];
                bool alongCut = Math.Abs(a.Y - minY) < 1e-6 && Math.Abs(b.Y - minY) < 1e-6;
                Assert.AreEqual(!alongCut, cutFront[i], $"edge {i} from ({a.X:F0},{a.Y:F1}) to ({b.X:F0},{b.Y:F1})");
                if (cutFront[i]) fronts++;
            }
            Assert.AreEqual(3, fronts);
            // The lists given are left alone.
            Assert.AreEqual(5.0, lot[0].Y, 1e-9);
        }

        [Test]
        public void ALotUnderADeckOrReachedOnlyFromUnderItIsLostAndOneClearOfItUntouched()
        {
            ExpresswayNetwork xw = StraightDeck();
            var all = new System.Collections.Generic.List<bool> { true, true, true, true };
            Assert.IsFalse(xw.CutBack(Square(100.0, -10.0, 140.0, 10.0), all, out _, out _), "a lot under the deck survived");
            // Its only street edge is the one the deck takes.
            var onlyTheBottom = new System.Collections.Generic.List<bool> { true, false, false, false };
            Assert.IsFalse(xw.CutBack(Square(100.0, 5.0, 160.0, 65.0), onlyTheBottom, out _, out _), "a lot reached only from under the deck survived");
            var clear = Square(100.0, 30.0, 160.0, 90.0);
            Assert.IsTrue(xw.CutBack(clear, all, out var same, out var sameFront));
            Assert.AreSame(clear, same, "an untouched lot was copied");
            Assert.AreSame(all, sameFront);
        }

        [Test]
        public void ALotReachingIntoAnInfieldIsCutAlongItsSide()
        {
            ExpresswayNetwork xw = StraightDeck();
            // The land a ramp cuts off above the deck: a triangle from the deck's edge up to y = 120.
            xw.Infields.Add(new[] { new Vec2(200.0, 15.0), new Vec2(290.0, 15.0), new Vec2(200.0, 120.0) });
            xw.Index();
            var lot = Square(150.0, 40.0, 210.0, 100.0);   // its right part reaches over the infield's left side (x = 200)
            var front = new System.Collections.Generic.List<bool> { true, true, true, true };
            Assert.IsTrue(xw.CutBack(lot, front, out var cut, out _));
            double maxX = double.MinValue;
            foreach (Vec2 v in cut) maxX = Math.Max(maxX, v.X);
            Assert.AreEqual(200.0 - ExpresswayNetwork.LotMargin, maxX, 1e-6, "not cut along the infield's side with the margin");
            Assert.IsFalse(PlanMath.PolygonsIntersect(cut, xw.Infields[0]));
        }

        [Test]
        public void AProfileMeetsEveryNeedAndClimbsNoSteeperThanItMay()
        {
            int n = 200;
            var need = new double[n];
            var s = new double[n];
            for (int i = 0; i < n; i++)
            {
                s[i] = i * 6.0;
                need[i] = (i == 60 || i == 140) ? 11.0 : 1.0;   // two roads to clear, open ground between
            }
            double[] top = ExpresswayPlanner.SmoothProfile(need, s, 0.04, false, 4);
            for (int i = 0; i < n; i++)
            {
                Assert.GreaterOrEqual(top[i], need[i] - 1e-9);
                if (i > 0) Assert.LessOrEqual(Math.Abs(top[i] - top[i - 1]) / 6.0, 0.04 + 1e-9);
            }
            Assert.Less(top[100], 11.0, "the deck never comes down between two crossings 480 m apart");
        }
    }
}
