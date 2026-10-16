using System.Collections.Generic;
using CityX.UI;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The map's GPS (RouteFinder) and the map's turned frame (MapFrame).</summary>
    public sealed class RouteFinderTests
    {
        /// <summary>A 3 x 3 grid of junctions 100 m apart, all joined, with the middle junction's roads removed: the route has to go round it.</summary>
        private static CityPlanData GridWithAHole()
        {
            var plan = new CityPlanData { Nodes = new List<RoadNode>(), Edges = new List<RoadEdge>() };
            var grid = new RoadNode[3, 3];
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    var node = new RoadNode { Id = plan.Nodes.Count, P = new Vec2(x * 100.0, y * 100.0) };
                    plan.Nodes.Add(node);
                    grid[x, y] = node;
                }
            }
            void Join(RoadNode a, RoadNode b)
            {
                bool middle = a == grid[1, 1] || b == grid[1, 1];
                var edge = new RoadEdge { Id = plan.Edges.Count, A = a, B = b, Alive = !middle };
                plan.Edges.Add(edge);
                a.Edges.Add(edge);
                b.Edges.Add(edge);
            }
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    if (x < 2) Join(grid[x, y], grid[x + 1, y]);
                    if (y < 2) Join(grid[x, y], grid[x, y + 1]);
                }
            }
            return plan;
        }

        [Test]
        public void TheRouteGoesAlongTheStreetsAroundWhatIsNotAStreet()
        {
            CityPlanData plan = GridWithAHole();
            // From beside the left middle junction to beside the right middle one: straight through the middle is 200 m, but the middle
            // junction has no roads, so the route must go round a corner: 100 up/down + 200 across + 100 back = 400 m between the junctions.
            List<Vec2> route = RouteFinder.Find(plan, new Vec2(-5.0, 100.0), new Vec2(205.0, 100.0));
            Assert.IsNotNull(route, "no route");
            Assert.AreEqual(-5.0, route[0].X, 1e-9, "the route does not start at the player");
            Assert.AreEqual(205.0, route[route.Count - 1].X, 1e-9, "the route does not end at the waypoint");
            double length = RouteFinder.Length(route);
            Assert.AreEqual(5.0 + 400.0 + 5.0, length, 1e-6, "not the shortest way along the streets");
            foreach (Vec2 p in route)
            {
                Assert.IsFalse(p.X == 100.0 && p.Y == 100.0, "the route went through the junction with no roads");
            }
        }

        [Test]
        public void InTheRealCityEveryStepOfTheRouteIsARoad()
        {
            PlanWorld world = PlanWorld.For(63428175);
            CityPlanData plan = world.Data;
            RoadNode from = RouteFinder.Nearest(plan, new Vec2(0.0, 0.0));
            RoadNode to = RouteFinder.Nearest(plan, new Vec2(900.0, -700.0));
            List<Vec2> route = RouteFinder.Find(plan, from.P, to.P);
            Assert.IsNotNull(route, "no route across the real city");
            Assert.Greater(route.Count, 3);
            // Between the two ends, each step joins two junctions that share a live road.
            var byPosition = new Dictionary<(double, double), RoadNode>();
            foreach (RoadNode n in plan.Nodes)
            {
                byPosition[(n.P.X, n.P.Y)] = n;
            }
            for (int i = 2; i < route.Count - 1; i++)
            {
                RoadNode a = byPosition[(route[i - 1].X, route[i - 1].Y)];
                RoadNode b = byPosition[(route[i].X, route[i].Y)];
                Assert.IsTrue(a.Edges.Exists(e => e.Alive && e.Other(a) == b), $"step {i} is not along a road");
            }
            Assert.GreaterOrEqual(RouteFinder.Length(route), (to.P - from.P).Length - 1e-6, "shorter than a straight line");
        }

        [Test]
        public void TheTurnedMapPutsTheHeadingUpAndAClickComesBackToTheSamePlace()
        {
            var heading = new Vector2(0.6f, 0.8f);
            var frame = new MapFrame(new Rect(0, 0, 200, 200), 50.0f, -30.0f, 2.0f, MapFrame.RotationPuttingUp(heading));
            Vector2 up = frame.TurnDirection(heading.normalized);
            Assert.AreEqual(0.0f, up.x, 1e-4f);
            Assert.AreEqual(-1.0f, up.y, 1e-4f, "the heading is not straight up the minimap");

            var world = new Vector2(73.0f, -12.5f);
            Vector2 back = frame.ToWorld(frame.ToPanel(world.x, world.y));
            Assert.AreEqual(world.x, back.x, 1e-3f);
            Assert.AreEqual(world.y, back.y, 1e-3f, "a click on the map does not come back to where it was drawn");

            Vector2 centre = frame.ToPanel(50.0f, -30.0f);
            Assert.AreEqual(100.0f, centre.x, 1e-4f, "the view point is not in the middle");
            Assert.AreEqual(100.0f, centre.y, 1e-4f);
        }
    }
}
