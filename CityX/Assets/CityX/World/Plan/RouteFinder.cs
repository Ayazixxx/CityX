using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>
    /// THE MAP'S GPS: the shortest way along the streets from one point to another, the way GTA's GPS draws its purple line (a shortest-path
    /// search over the road graph). A* over the city plan's road nodes, the cost being each road's length and the guess the straight-line
    /// distance, which never overestimates, so the route is the shortest there is. The route starts at the road node nearest the start and ends
    /// at the one nearest the goal, with the two real points added at the ends, so the line leaves from the player and arrives at the pin.
    ///
    /// Pure: the plan is a function of the world seed, so the same route on every machine, and testable with no scene.
    /// </summary>
    public static class RouteFinder
    {
        /// <summary>
        /// The route from <paramref name="from"/> to <paramref name="to"/> as a polyline, or null when there are no roads at all, or no road
        /// path joins the two (the city's networks are joined, so in practice that only means a broken plan).
        /// </summary>
        public static List<Vec2> Find(CityPlanData plan, Vec2 from, Vec2 to)
        {
            RoadNode start = Nearest(plan, from);
            RoadNode goal = Nearest(plan, to);
            if (start == null || goal == null)
            {
                return null;
            }

            var cost = new Dictionary<int, double> { [start.Id] = 0.0 };
            var cameFrom = new Dictionary<int, RoadNode>();
            var open = new MinHeap();
            open.Push(start, (goal.P - start.P).Length);
            var closed = new HashSet<int>();

            while (open.Count > 0)
            {
                RoadNode node = open.Pop();
                if (node == goal)
                {
                    return Polyline(cameFrom, goal, from, to);
                }
                if (!closed.Add(node.Id))
                {
                    continue;
                }
                double here = cost[node.Id];
                foreach (RoadEdge edge in node.Edges)
                {
                    if (!edge.Alive)
                    {
                        continue;
                    }
                    RoadNode next = edge.Other(node);
                    if (closed.Contains(next.Id))
                    {
                        continue;
                    }
                    double through = here + edge.Length;
                    if (cost.TryGetValue(next.Id, out double known) && known <= through)
                    {
                        continue;
                    }
                    cost[next.Id] = through;
                    cameFrom[next.Id] = node;
                    open.Push(next, through + (goal.P - next.P).Length);
                }
            }
            return null;
        }

        /// <summary>The road node nearest a point, among the nodes that still have a road (a dead end left over from growth has none).</summary>
        public static RoadNode Nearest(CityPlanData plan, Vec2 point)
        {
            RoadNode best = null;
            double bestDistance = double.MaxValue;
            foreach (RoadNode node in plan.Nodes)
            {
                if (!HasLiveRoad(node))
                {
                    continue;
                }
                double distance = (node.P - point).Length;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                }
            }
            return best;
        }

        /// <summary>How long a polyline is, metres.</summary>
        public static double Length(List<Vec2> route)
        {
            double total = 0.0;
            for (int i = 1; route != null && i < route.Count; i++)
            {
                total += (route[i] - route[i - 1]).Length;
            }
            return total;
        }

        private static bool HasLiveRoad(RoadNode node)
        {
            foreach (RoadEdge edge in node.Edges)
            {
                if (edge.Alive)
                {
                    return true;
                }
            }
            return false;
        }

        private static List<Vec2> Polyline(Dictionary<int, RoadNode> cameFrom, RoadNode goal, Vec2 from, Vec2 to)
        {
            var nodes = new List<Vec2>();
            for (RoadNode at = goal; at != null; at = cameFrom.TryGetValue(at.Id, out RoadNode previous) ? previous : null)
            {
                nodes.Add(at.P);
            }
            nodes.Reverse();
            var route = new List<Vec2>(nodes.Count + 2) { from };
            route.AddRange(nodes);
            route.Add(to);
            return route;
        }

        /// <summary>A plain binary min-heap of nodes by priority (a node may be in it more than once; the stale copies are skipped on pop).</summary>
        private sealed class MinHeap
        {
            private readonly List<(RoadNode Node, double Priority)> _items = new List<(RoadNode, double)>();

            public int Count => _items.Count;

            public void Push(RoadNode node, double priority)
            {
                _items.Add((node, priority));
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_items[parent].Priority <= _items[i].Priority)
                    {
                        break;
                    }
                    (_items[parent], _items[i]) = (_items[i], _items[parent]);
                    i = parent;
                }
            }

            public RoadNode Pop()
            {
                RoadNode top = _items[0].Node;
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _items.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1;
                    int right = left + 1;
                    int smallest = i;
                    if (left < _items.Count && _items[left].Priority < _items[smallest].Priority) smallest = left;
                    if (right < _items.Count && _items[right].Priority < _items[smallest].Priority) smallest = right;
                    if (smallest == i)
                    {
                        break;
                    }
                    (_items[smallest], _items[i]) = (_items[i], _items[smallest]);
                    i = smallest;
                }
                return top;
            }
        }
    }
}
