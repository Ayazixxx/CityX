using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>What closes a tunnel's mouth. All of them can be walked through for now; a locked one comes with keys and explosives.</summary>
    public enum SewerGate
    {
        /// <summary>Nothing: an open mouth, as Las Vegas's flood tunnels have.</summary>
        None = 0,

        /// <summary>Bars across the mouth with a barred door in the middle, standing ajar (the owner's pictures).</summary>
        DoorAjar = 1,

        /// <summary>Bars across the mouth with a few rusted through.</summary>
        Gap = 2,
    }

    /// <summary>
    /// A length of tunnel: rings of one section along a straight line, each at its own floor height. A tunnel under a road and the buried
    /// part of an outfall are both one of these.
    /// </summary>
    public sealed class SewerTube
    {
        public SewerSection Section;

        /// <summary>Where distance 0 is, and which way the distances run.</summary>
        public Vec2 Origin;
        public Vec2 Dir;

        /// <summary>The rings: how far along each is, and the height of the walkways there.</summary>
        public double[] Stations;
        public double[] Floors;

        /// <summary>Whether each end is walled off (it opens into a room, or out of a headwall, otherwise).</summary>
        public bool WallAtStart;
        public bool WallAtEnd;

        /// <summary>Which of its material's colours it is built in (SewerPlan.ToneAt).</summary>
        public int Tone;

        public Vec2 At(double distance) => Origin + Dir * distance;

        public double Start => Stations[0];

        public double End => Stations[Stations.Length - 1];

        /// <summary>The walkways' height at a distance along it.</summary>
        public double FloorAt(double distance)
        {
            int last = Stations.Length - 1;
            if (distance <= Stations[0]) return Floors[0];
            if (distance >= Stations[last]) return Floors[last];
            for (int i = 0; i < last; i++)
            {
                if (distance <= Stations[i + 1])
                {
                    double t = (distance - Stations[i]) / Math.Max(1e-9, Stations[i + 1] - Stations[i]);
                    return Floors[i] + (Floors[i + 1] - Floors[i]) * t;
                }
            }
            return Floors[last];
        }
    }

    /// <summary>The way up from a room to the street: a neck through the road over the middle of the junction, a ladder down it to the room's floor.</summary>
    public sealed class SewerShaft
    {
        public Vec2 Centre;

        /// <summary>Level, from the ladder toward whoever is on it.</summary>
        public Vec2 Normal;

        /// <summary>The neck's outline in plan (also the hole in the road when the lid is off).</summary>
        public Vec2[] Ring;

        /// <summary>Whether the lid is off: an open manhole is a way in.</summary>
        public bool Open;

        /// <summary>Where the ladder stands on the room's floor, and the street's height where it is stepped off at the top.</summary>
        public double Bottom;
        public double Top;
    }

    /// <summary>The room where tunnels meet under a junction: the junction's own shape at the tunnels' widths, floor and ceiling following the ground.</summary>
    public sealed class SewerRoom
    {
        public RoadNode Node;
        public Vec2[] Plate;

        /// <summary>The floor and the ceiling at each point of the plate, and at the middle.</summary>
        public double[] Floor;
        public double[] Ceiling;
        public double CentreFloor;
        public double CentreCeiling;

        /// <summary>For each side of the plate (from that point to the next): the section of the tunnel that opens there, or null for a wall.</summary>
        public SewerSection[] Opening;

        /// <summary>The way up, where the room has space for one.</summary>
        public SewerShaft Shaft;

        /// <summary>Which of its material's colours it is built in (SewerPlan.ToneAt).</summary>
        public int Tone;
    }

    /// <summary>
    /// Where a tunnel comes out into a dry channel under the road's bridge: its buried part runs level from the room at the bank's top to a
    /// headwall where the bank has fallen to its crown, and an open cut between two wing walls carries on to where the bank meets its floor.
    /// </summary>
    public sealed class SewerOutfall
    {
        public RoadEdge Bridge;

        /// <summary>The junction whose room it comes out of.</summary>
        public RoadNode From;

        /// <summary>The buried part, distance 0 at the room.</summary>
        public SewerTube Tube;

        /// <summary>How far along the headwall stands, and where the open cut's floor runs out onto the bank.</summary>
        public double Portal;
        public double End;

        public double Floor;
        public SewerGate Gate;
    }

    /// <summary>
    /// THE SEWERS UNDER A CITY, as a pure function of its plan (docs/SEWERS.md): a tunnel under every road, sized by the road as Paris
    /// sizes its galleries, a room under every junction with a shaft up to a manhole, and a mouth wherever a road's bridge crosses a dry
    /// channel. Every machine works out the same ones from the same plan, so nothing of them is ever sent.
    /// </summary>
    public sealed class SewerPlan
    {
        /// <summary>How far under the ground the walkways are, all along a road.</summary>
        public const double Depth = 8.6;

        /// <summary>How far under the ground a room's ceiling is: the road's slab, which the shaft's neck goes through.</summary>
        public const double RoomCover = 1.2;

        /// <summary>Half the width of a shaft's neck (its flat sides), and how much clear room round the middle a room needs to have one.</summary>
        public const double NeckHalfWidth = 0.62;
        public const double ShaftClearance = 1.7;

        /// <summary>A junction whose road rises or falls more than this within two metres of its middle has no shaft: a ladder wants a level street at its top.</summary>
        public const double ShaftLevelWithin = 0.3;

        /// <summary>The share of manholes whose lid is off. High while a shut one cannot be opened (explosives and Strength are to come).</summary>
        public const double OpenShare = 0.3;

        /// <summary>A tunnel stops at least this far short of a junction, so there is a room there even where a road runs straight through.</summary>
        public const double LeastRoomTrim = 1.5;

        /// <summary>About how far apart a tunnel's rings are.</summary>
        public const double RingSpacing = 6.0;

        /// <summary>A road shorter than this between its rooms has no tunnel of its own.</summary>
        public const double ShortestTube = 2.0;

        /// <summary>How much ground is left over an outfall's crown where its headwall stands, and the nearest to its room a headwall may be.</summary>
        public const double PortalCover = 0.6;
        public const double NearestPortal = 6.0;

        /// <summary>How far an outfall may run looking for daylight.</summary>
        public const double LongestOutfall = 160.0;

        private const ulong Salt = 0x5345574552UL;   // "SEWER"

        private static readonly ConditionalWeakTable<PlanWorld, SewerPlan> Plans = new ConditionalWeakTable<PlanWorld, SewerPlan>();

        private static readonly SewerOutfall[] NoOutfalls = new SewerOutfall[0];

        private readonly PlanWorld _plan;
        private readonly SewerTube[] _tubes;
        private readonly SewerRoom[] _rooms;
        private readonly SewerOutfall[][] _outfalls;

        public static SewerPlan For(PlanWorld plan) => Plans.GetValue(plan, p => new SewerPlan(p));

        /// <summary>The tunnel under a road (null: none).</summary>
        public SewerTube Tube(int edgeId) => edgeId >= 0 && edgeId < _tubes.Length ? _tubes[edgeId] : null;

        /// <summary>The room under a junction (null: none).</summary>
        public SewerRoom Room(int nodeId) => nodeId >= 0 && nodeId < _rooms.Length ? _rooms[nodeId] : null;

        /// <summary>The mouths under a bridge, one from each bank at most.</summary>
        public SewerOutfall[] Outfalls(int edgeId) => edgeId >= 0 && edgeId < _outfalls.Length && _outfalls[edgeId] != null ? _outfalls[edgeId] : NoOutfalls;

        public IEnumerable<SewerRoom> Rooms
        {
            get
            {
                foreach (SewerRoom room in _rooms)
                {
                    if (room != null) yield return room;
                }
            }
        }

        public IEnumerable<SewerOutfall> AllOutfalls
        {
            get
            {
                foreach (SewerOutfall[] pair in _outfalls)
                {
                    if (pair == null) continue;
                    foreach (SewerOutfall outfall in pair) yield return outfall;
                }
            }
        }

        public static SewerKind KindOf(RoadClass roadClass) =>
            roadClass == RoadClass.Boulevard ? SewerKind.Trunk : roadClass == RoadClass.Arterial ? SewerKind.Collector : SewerKind.Gallery;

        private double Unit(int id, int what) => WorldHash.Unit01(id, what, Salt, unchecked((ulong)(long)_plan.Seed));

        /// <summary>
        /// What a road's tunnel is built as. Neighbourhoods share a style (a few hundred metres of the same), with the odd tunnel its own; the
        /// flood tunnels under the boulevards are concrete.
        /// </summary>
        public SewerStyle StyleOf(RoadEdge edge)
        {
            SewerKind kind = KindOf(edge.Class);
            Vec2 middle = (edge.A.P + edge.B.P) * 0.5;
            double cell = 320.0 * _plan.Data.Config.Scale;
            int cx = (int)Math.Floor(middle.X / cell), cy = (int)Math.Floor(middle.Y / cell);
            double pick = Unit(edge.Id, 11) < 0.2 ? Unit(edge.Id, 12) : WorldHash.Unit01(cx, cy, Salt, unchecked((ulong)(long)_plan.Seed));
            if (kind == SewerKind.Trunk)
            {
                return pick < 0.7 ? SewerStyle.ConcreteBox : SewerStyle.RoundPipe;
            }
            return pick < 0.45 ? SewerStyle.BrickVault : pick < 0.75 ? SewerStyle.StoneArch : SewerStyle.RoundPipe;
        }

        public SewerSection SectionOf(RoadEdge edge) => SewerSection.For(KindOf(edge.Class), StyleOf(edge));

        /// <summary>How many colours a tunnel's material comes in (the mesh maps a tone onto what it has).</summary>
        public const int Tones = 4;

        /// <summary>
        /// Which of its material's colours a place is built in (0 to <see cref="Tones"/> - 1): the same across a neighbourhood, as bricks
        /// from one yard are, and another in the next.
        /// </summary>
        public int ToneAt(Vec2 place)
        {
            double cell = 260.0 * _plan.Data.Config.Scale;
            int cx = (int)Math.Floor(place.X / cell), cy = (int)Math.Floor(place.Y / cell);
            return Math.Min(Tones - 1, (int)(WorldHash.Unit01(cx, cy, Salt + 7UL, unchecked((ulong)(long)_plan.Seed)) * Tones));
        }

        private SewerPlan(PlanWorld plan)
        {
            _plan = plan;
            CityPlanData data = plan.Data;
            _tubes = new SewerTube[data.Edges.Count];
            _rooms = new SewerRoom[data.Nodes.Count];
            _outfalls = new SewerOutfall[data.Edges.Count][];

            // The junction's shape at the tunnels' widths. A road too short to hold a tunnel between its two rooms has none, which changes
            // the rooms at its ends, so they are laid out again without it.
            var left = new bool[data.Edges.Count];
            foreach (RoadEdge e in data.Edges)
            {
                left[e.Id] = !e.Alive || StepStreets.Is(plan, e);   // steps climb too steeply for a tunnel to follow (a cascade, to come)
            }
            PlanGeometry laid = null;
            for (int pass = 0; pass < 4; pass++)
            {
                laid = PlanGeometry.Build(data, e => left[e.Id] ? 0.0 : SewerSection.HalfWidthOf(KindOf(e.Class)), LeastRoomTrim);
                bool changed = false;
                foreach (EdgeGeometry g in laid.Edges)
                {
                    if (g == null || left[g.Edge.Id]) continue;
                    if (g.Length - MouthAt(laid, g, true) - MouthAt(laid, g, false) < ShortestTube)
                    {
                        left[g.Edge.Id] = true;
                        changed = true;
                    }
                }
                if (!changed) break;
            }

            foreach (EdgeGeometry g in laid.Edges)
            {
                if (g == null || left[g.Edge.Id]) continue;
                if (g.Edge.Bridge)
                {
                    LayOutfalls(laid, g);
                }
                else
                {
                    _tubes[g.Edge.Id] = LayTube(laid, g);
                }
            }
            foreach (JunctionGeometry j in laid.Junctions)
            {
                if (HasRoom(j))
                {
                    _rooms[j.Node.Id] = LayRoom(j);
                }
            }
        }

        /// <summary>Whether tunnels meet at a junction in a room: two or more of them, their plate a real area.</summary>
        private static bool HasRoom(JunctionGeometry j) => j?.Plate != null && Math.Abs(PlanMath.SignedArea(j.Plate)) > 1.0;

        private double Ground(Vec2 p) => _plan.GroundHeight(p.X, p.Y);

        /// <summary>How far from a road's end (its A end, or its B end) its tunnel's mouth into the room there is: 0 with no room.</summary>
        private static double MouthAt(PlanGeometry laid, EdgeGeometry g, bool atA)
        {
            RoadNode node = atA ? g.Edge.A : g.Edge.B;
            JunctionGeometry j = laid.Junctions[node.Id];
            if (!HasRoom(j)) return 0.0;
            for (int i = 0; i < j.Plate.Length; i++)
            {
                if (j.PlateEdge[i] != g.Edge.Id) continue;
                Vec2 middle = (j.Plate[i] + j.Plate[(i + 1) % j.Plate.Length]) * 0.5;
                return (middle - node.P).Length;
            }
            return 0.0;
        }

        private SewerTube LayTube(PlanGeometry laid, EdgeGeometry g)
        {
            double from = MouthAt(laid, g, true), to = g.Length - MouthAt(laid, g, false);
            int pieces = Math.Max(1, (int)Math.Ceiling((to - from) / RingSpacing));
            var tube = new SewerTube
            {
                Section = SectionOf(g.Edge),
                Origin = g.A,
                Dir = g.Dir,
                Stations = new double[pieces + 1],
                Floors = new double[pieces + 1],
                WallAtStart = !HasRoom(laid.Junctions[g.Edge.A.Id]),
                WallAtEnd = !HasRoom(laid.Junctions[g.Edge.B.Id]),
                Tone = ToneAt((g.A + g.B) * 0.5),
            };
            for (int i = 0; i <= pieces; i++)
            {
                tube.Stations[i] = from + (to - from) * i / pieces;
                tube.Floors[i] = Ground(tube.At(tube.Stations[i])) - Depth;
            }
            return tube;
        }

        /// <summary>
        /// The mouths under a bridge. From the room at each bank's top the tunnel runs level out under the deck; where the falling bank has
        /// come down to its crown the headwall stands, and the open cut goes on to where the bank meets the floor. A bank that never
        /// comes down that far (the channel's bed is higher than the tunnel) has no mouth, and its room is walled on that side.
        /// </summary>
        private void LayOutfalls(PlanGeometry laid, EdgeGeometry g)
        {
            var found = new List<SewerOutfall>(2);
            SewerSection section = SectionOf(g.Edge);
            for (int end = 0; end < 2; end++)
            {
                bool atA = end == 0;
                double mouth = MouthAt(laid, g, atA);
                if (mouth <= 0.0) continue;   // no room to come out of
                Vec2 origin = atA ? g.A + g.Dir * mouth : g.B - g.Dir * mouth;
                Vec2 dir = atA ? g.Dir : -g.Dir;
                double floor = Ground(origin) - Depth;
                double reach = Math.Min(LongestOutfall, g.Length * 0.5 - mouth - 1.0);
                double portal = -1.0, finish = -1.0;
                for (double s = 0.0; s <= reach; s += 1.0)
                {
                    double ground = Ground(origin + dir * s);
                    if (portal < 0.0)
                    {
                        if (ground > floor + section.Crown + PortalCover) continue;
                        if (s < NearestPortal) break;   // the bank falls away at the room's own wall: no length of tunnel to come out of
                        portal = s;
                    }
                    else if (ground <= floor + 0.1)
                    {
                        finish = s;
                        break;
                    }
                }
                if (portal < 0.0 || finish < 0.0) continue;

                int pieces = Math.Max(1, (int)Math.Ceiling(portal / RingSpacing));
                var tube = new SewerTube
                {
                    Section = section,
                    Origin = origin,
                    Dir = dir,
                    Stations = new double[pieces + 1],
                    Floors = new double[pieces + 1],
                    Tone = ToneAt(origin),
                };
                for (int i = 0; i <= pieces; i++)
                {
                    tube.Stations[i] = portal * i / pieces;
                    tube.Floors[i] = floor;
                }
                double gate = Unit(g.Edge.Id, 20 + end);
                found.Add(new SewerOutfall
                {
                    Bridge = g.Edge,
                    From = atA ? g.Edge.A : g.Edge.B,
                    Tube = tube,
                    Portal = portal,
                    End = finish,
                    Floor = floor,
                    Gate = gate < 0.3 ? SewerGate.None : gate < 0.75 ? SewerGate.DoorAjar : SewerGate.Gap,
                });
            }
            if (found.Count > 0)
            {
                _outfalls[g.Edge.Id] = found.ToArray();
            }
        }

        /// <summary>Whether a bridge has a mouth coming out of the room at this node.</summary>
        private bool HasOutfallFrom(RoadEdge bridge, RoadNode node)
        {
            foreach (SewerOutfall outfall in Outfalls(bridge.Id))
            {
                if (outfall.From == node) return true;
            }
            return false;
        }

        private SewerRoom LayRoom(JunctionGeometry j)
        {
            int n = j.Plate.Length;
            var room = new SewerRoom
            {
                Node = j.Node,
                Plate = j.Plate,
                Floor = new double[n],
                Ceiling = new double[n],
                Opening = new SewerSection[n],
            };
            double centre = Ground(j.Node.P);
            room.Tone = ToneAt(j.Node.P);
            room.CentreFloor = centre - Depth;
            room.CentreCeiling = centre - RoomCover;
            for (int i = 0; i < n; i++)
            {
                double ground = Ground(j.Plate[i]);
                room.Floor[i] = ground - Depth;
                room.Ceiling[i] = ground - RoomCover;
            }
            int firstOpening = -1;
            for (int i = 0; i < n; i++)
            {
                int edgeId = j.PlateEdge[i];
                if (edgeId < 0) continue;
                int next = (i + 1) % n;
                // Both feet of a mouth stand at its tunnel's own end, so the tunnel's last ring meets the room's wall exactly.
                double ground = Ground((j.Plate[i] + j.Plate[next]) * 0.5);
                room.Floor[i] = room.Floor[next] = ground - Depth;
                room.Ceiling[i] = room.Ceiling[next] = ground - RoomCover;
                RoadEdge edge = _plan.Data.Edges[edgeId];
                bool open = edge.Bridge ? HasOutfallFrom(edge, j.Node) : _tubes[edgeId] != null;
                if (open)
                {
                    room.Opening[i] = SectionOf(edge);
                    if (firstOpening < 0 || edgeId < j.PlateEdge[firstOpening]) firstOpening = i;
                }
            }
            if (firstOpening >= 0 && ClearRound(j) >= ShaftClearance && RiseRound(j.Node.P, 2.0) <= ShaftLevelWithin)
            {
                room.Shaft = LayShaft(room, firstOpening);
            }
            return room;
        }

        /// <summary>The most the ground rises or falls from a point to anywhere a distance round it (eight ways).</summary>
        private double RiseRound(Vec2 centre, double distance)
        {
            double middle = Ground(centre), most = 0.0;
            for (int i = 0; i < 8; i++)
            {
                most = Math.Max(most, Math.Abs(Ground(centre + new Vec2(RingCos[i], RingSin[i]) * distance) - middle));
            }
            return most;
        }

        /// <summary>How far the nearest side of a junction's plate is from its middle.</summary>
        private static double ClearRound(JunctionGeometry j)
        {
            double nearest = double.MaxValue;
            for (int i = 0; i < j.Plate.Length; i++)
            {
                nearest = Math.Min(nearest, PlanMath.DistanceToSegment(j.Node.P, j.Plate[i], j.Plate[(i + 1) % j.Plate.Length], out _));
            }
            return PlanMath.PointInPolygon(j.Node.P, j.Plate) ? nearest : 0.0;
        }

        /// <summary>The shaft over the middle of a room, its ladder facing down the tunnel that opens on side <paramref name="toward"/>.</summary>
        private SewerShaft LayShaft(SewerRoom room, int toward)
        {
            Vec2 centre = room.Node.P;
            Vec2 mouth = (room.Plate[toward] + room.Plate[(toward + 1) % room.Plate.Length]) * 0.5;
            Vec2 normal = (mouth - centre).Normalized;
            // Eight sides, one of them flat across the ladder's back.
            var ring = new Vec2[8];
            double corner = NeckHalfWidth / 0.9238795325112867;   // cos 22.5 degrees
            for (int i = 0; i < 8; i++)
            {
                ring[i] = centre + (-normal).Rotated(RingCos[i], RingSin[i]) * corner;
            }
            Vec2 foot = centre - normal * NeckHalfWidth;
            Vec2 stand = foot - normal * 0.75;
            return new SewerShaft
            {
                Centre = centre,
                Normal = normal,
                Ring = ring,
                Open = Unit(room.Node.Id, 30) < OpenShare,
                Bottom = room.CentreFloor,
                Top = Ground(stand) + PlanGeometry.RoadLift,
            };
        }

        // The ring's corners stand at 22.5 degrees and every 45 after, so its sides are flat fore and aft and to each side.
        private static readonly double[] RingCos =
        {
            0.9238795325112867, 0.3826834323650898, -0.3826834323650898, -0.9238795325112867,
            -0.9238795325112867, -0.3826834323650898, 0.3826834323650898, 0.9238795325112867,
        };

        private static readonly double[] RingSin =
        {
            0.3826834323650898, 0.9238795325112867, 0.9238795325112867, 0.3826834323650898,
            -0.3826834323650898, -0.9238795325112867, -0.9238795325112867, -0.3826834323650898,
        };

        // ------------------------------------------------------------------ where a point is

        /// <summary>
        /// How far out of the sky's light a point is, 0 (under the open sky) to 1 (deep in a tunnel): 1 inside a tunnel or a room, rising
        /// down a shaft's neck and in from an outfall's headwall, 0 anywhere else.
        /// </summary>
        public double DarknessAt(double x, double y, double z)
        {
            var p = new Vec2(x, z);
            int cx = PlanWorld.ChunkOf(x), cy = PlanWorld.ChunkOf(z);
            double best = 0.0;
            foreach (int nodeId in _plan.JunctionsIn(cx, cy))
            {
                SewerRoom room = Room(nodeId);
                if (room == null) continue;
                if (room.Shaft != null && (p - room.Shaft.Centre).Length <= NeckHalfWidth + 0.4 && y > room.CentreCeiling - 0.2)
                {
                    // In the neck: darker the further down it.
                    double neck = Math.Max(0.5, room.Shaft.Top - room.CentreCeiling);
                    best = Math.Max(best, PlanMath.Clamp01((room.Shaft.Top - y) / neck));
                    continue;
                }
                // A room's floor and ceiling follow the ground over it.
                double ground = _plan.GroundHeight(x, z);
                if (y >= ground - Depth - 1.0 && y <= ground - RoomCover + 0.3 && PlanMath.PointInPolygon(p, room.Plate))
                {
                    return 1.0;
                }
            }
            foreach (int edgeId in _plan.EdgesIn(cx, cy))
            {
                SewerTube tube = Tube(edgeId);
                if (tube != null && InTube(tube, p, y, out _))
                {
                    return 1.0;
                }
                foreach (SewerOutfall outfall in Outfalls(edgeId))
                {
                    if (InTube(outfall.Tube, p, y, out double along))
                    {
                        // Daylight reaches a few tunnel-heights in from the mouth.
                        best = Math.Max(best, PlanMath.Clamp01((outfall.Portal - along) / (outfall.Tube.Section.Crown * 2.5)));
                    }
                }
            }
            return best;
        }

        private static bool InTube(SewerTube tube, Vec2 p, double y, out double along)
        {
            along = Vec2.Dot(p - tube.Origin, tube.Dir);
            if (along < tube.Start - 0.5 || along > tube.End + 0.5) return false;
            double across = Math.Abs(Vec2.Cross(tube.Dir, p - tube.Origin));
            if (across > tube.Section.HalfWidth + 0.6) return false;
            double floor = tube.FloorAt(along);
            return y >= floor - tube.Section.ChannelDepth - 1.5 && y <= floor + tube.Section.Crown + 1.0;
        }
    }
}
