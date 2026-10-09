using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>One room of one floor of one building: the thing a light switch switches. Room 0 is the floor's open hall, room 1 its corner room.</summary>
    public readonly struct RoomKey : IEquatable<RoomKey>
    {
        public readonly int Building;
        public readonly int Floor;
        public readonly int Room;

        public RoomKey(int building, int floor, int room)
        {
            Building = building;
            Floor = floor;
            Room = room;
        }

        public bool Equals(RoomKey other) => Building == other.Building && Floor == other.Floor && Room == other.Room;
        public override bool Equals(object obj) => obj is RoomKey other && Equals(other);
        public override int GetHashCode() => unchecked((Building * 397) ^ (Floor * 31) ^ Room);
        public override string ToString() => $"building {Building} floor {Floor} room {Room}";
    }

    /// <summary>
    /// The kinds of ceiling light a room can have. From what games and real interiors use (2026-10-09 research): offices are lit by recessed panel
    /// grids and rows of fluorescent tubes (cool white, even, a little harsh); homes and old buildings by a central fitting or pendants on cords (warm,
    /// pooled); shops and lobbies by many small downlights (neutral, bright). Each has its own light colour, so a lit window says what is inside.
    /// </summary>
    public enum LightPattern
    {
        PanelGrid = 0,
        FluorescentRows = 1,
        Pendants = 2,
        CentreFitting = 3,
        Downlights = 4,
    }

    /// <summary>One light fitting: its housing (what is built, lit or not) and the glowing face under it, in the building's own frame (x, z relative to
    /// its centre; y in metres above sea level, like the builder's).</summary>
    public readonly struct Fixture
    {
        public readonly Vector3 HousingMin;
        public readonly Vector3 HousingMax;

        /// <summary>The cord or rods it hangs by (zero size when it is flush with the ceiling).</summary>
        public readonly Vector3 CordMin;
        public readonly Vector3 CordMax;

        /// <summary>The glowing face: the underside of the housing, a hair below it.</summary>
        public readonly Vector3 GlowMin;
        public readonly Vector3 GlowMax;

        public Fixture(Vector3 housingMin, Vector3 housingMax, Vector3 cordMin, Vector3 cordMax)
        {
            HousingMin = housingMin;
            HousingMax = housingMax;
            CordMin = cordMin;
            CordMax = cordMax;
            GlowMin = new Vector3(housingMin.x + 0.02f, housingMin.y - 0.01f, housingMin.z + 0.02f);
            GlowMax = new Vector3(housingMax.x - 0.02f, housingMin.y - 0.01f, housingMax.z - 0.02f);
        }

        public bool HasCord => CordMax.y > CordMin.y;
    }

    /// <summary>Everything about one room's lighting: its fittings, where the real (point) lights go, what colour, and where its switch is.</summary>
    public sealed class RoomLightPlan
    {
        public RoomKey Key;
        public LightPattern Pattern;
        public Color Colour;
        public readonly List<Fixture> Fixtures = new List<Fixture>();

        /// <summary>Where the room's real lights stand (few: the fittings glow, these light the room), in the building's frame.</summary>
        public readonly List<Vector3> LightPoints = new List<Vector3>();
        public float LightRange;
        public float LightIntensity;

        /// <summary>The switch plate's centre, on the wall, and the way it faces (into the room), in the building's frame.</summary>
        public Vector3 Switch;
        public Vector3 SwitchFacing;
    }

    /// <summary>
    /// Room lights and their switches, as a pure function of the building and its rooms (the builder draws the fittings; CityStreamer lights them).
    /// Every room -- each floor's open hall and its corner room -- has a switch by its door and lights on its ceiling. ONLY ONE BUILDING IN TEN HAS
    /// POWER (<see cref="PoweredShare"/>); in the rest the switch clicks and nothing happens. <see cref="PowerGrid"/> is where power is decided, so
    /// a later "turn the electricity back on" only has to add to it.
    /// </summary>
    public static class RoomLighting
    {
        /// <summary>The share of buildings that have electricity (owner, 2026-10-09: "10% of the buildings").</summary>
        public const double PoweredShare = 0.10;

        /// <summary>How high a switch is on the wall, above the floor (a 2.6 m player: about 1.3x a real 1.1 m).</summary>
        public const float SwitchHeight = 1.45f;

        /// <summary>The switch plate: square, and how far it stands off the wall.</summary>
        public const float SwitchSize = 0.22f;
        public const float SwitchDepth = 0.04f;

        /// <summary>How far beside the doorway the switch is, on the latch side.</summary>
        public const float SwitchBesideDoor = 0.45f;

        /// <summary>No room has more fittings than this (a 40 m office floor gets wider spacing, not hundreds of panels).</summary>
        public const int MaxFixtures = 16;

        /// <summary>No room has more real lights than this.</summary>
        public const int MaxLightPoints = 6;

        /// <summary>Nothing hangs lower than this above the floor (a 2.6 m player, with room to jump a little).</summary>
        public const float HeadroomMetres = 3.2f;

        /// <summary>Fittings keep this far from the walls.</summary>
        public const float WallMargin = 1.0f;

        private const ulong PatternSalt = 0x4C49474854UL;   // "LIGHT"

        /// <summary>The light each kind gives: cool fluorescent, warm tungsten, neutral LED.</summary>
        public static Color ColourOf(LightPattern pattern)
        {
            switch (pattern)
            {
                case LightPattern.PanelGrid: return new Color(0.90f, 0.95f, 1.00f);
                case LightPattern.FluorescentRows: return new Color(0.84f, 0.95f, 0.98f);
                case LightPattern.Pendants: return new Color(1.00f, 0.74f, 0.45f);
                case LightPattern.CentreFitting: return new Color(1.00f, 0.86f, 0.66f);
                default: return new Color(1.00f, 0.94f, 0.84f);
            }
        }

        /// <summary>The kind of lighting a room has: by what the building is (its zone) and rolled from its seed, the hall and the corner room apart.</summary>
        public static LightPattern PatternFor(BuildingSite site, int floor, int room)
        {
            double r = WorldHash.Unit01(floor, room, PatternSalt, unchecked((ulong)(long)site.Seed));
            LightPattern[] choices;
            switch (site.Zone)
            {
                case ZoneKind.Core:
                    choices = new[] { LightPattern.PanelGrid, LightPattern.PanelGrid, LightPattern.Downlights, LightPattern.FluorescentRows };
                    break;
                case ZoneKind.Urban:
                    choices = new[] { LightPattern.PanelGrid, LightPattern.FluorescentRows, LightPattern.Downlights, LightPattern.CentreFitting };
                    break;
                case ZoneKind.Industrial:
                    choices = new[] { LightPattern.FluorescentRows, LightPattern.FluorescentRows, LightPattern.Pendants };
                    break;
                case ZoneKind.Residential:
                case ZoneKind.OldTown:
                    choices = new[] { LightPattern.CentreFitting, LightPattern.Pendants, LightPattern.CentreFitting, LightPattern.Downlights };
                    break;
                default:
                    choices = new[] { LightPattern.CentreFitting, LightPattern.Pendants, LightPattern.FluorescentRows };
                    break;
            }
            return choices[Math.Min(choices.Length - 1, (int)(r * choices.Length))];
        }

        /// <summary>
        /// The lighting of one room. <paramref name="area"/> is its floor plan in the building's frame (x, z relative to the centre); fittings keep
        /// out of <paramref name="avoid"/> (the corner room, for the hall; the stair's hole in the ceiling). <paramref name="floorY"/> and
        /// <paramref name="ceilingY"/> are the heights of the floor and the underside of the ceiling. The switch is given by the caller (by the door).
        /// </summary>
        public static RoomLightPlan Plan(BuildingSite site, int floor, int room, Rect area, IList<Rect> avoid, float floorY, float ceilingY,
            Vector3 switchAt, Vector3 switchFacing)
        {
            var plan = new RoomLightPlan
            {
                Key = new RoomKey(site.Id, floor, room),
                Pattern = PatternFor(site, floor, room),
                Switch = switchAt,
                SwitchFacing = switchFacing,
            };
            plan.Colour = ColourOf(plan.Pattern);

            Rect inner = Rect.MinMaxRect(area.xMin + WallMargin, area.yMin + WallMargin, area.xMax - WallMargin, area.yMax - WallMargin);
            if (inner.width <= 0.0f || inner.height <= 0.0f)
            {
                inner = Rect.MinMaxRect(area.center.x - 0.1f, area.center.y - 0.1f, area.center.x + 0.1f, area.center.y + 0.1f);
            }

            // The size of one fitting and how far apart they stand, by kind.
            float sx, sz, thick, drop, spacing;
            switch (plan.Pattern)
            {
                case LightPattern.PanelGrid: sx = 1.2f; sz = 0.6f; thick = 0.06f; drop = 0.0f; spacing = 4.0f; break;
                case LightPattern.FluorescentRows: sx = 2.4f; sz = 0.2f; thick = 0.1f; drop = 0.9f; spacing = 3.2f; break;
                case LightPattern.Pendants: sx = 0.42f; sz = 0.42f; thick = 0.3f; drop = 2.2f; spacing = 4.2f; break;
                case LightPattern.Downlights: sx = 0.28f; sz = 0.28f; thick = 0.05f; drop = 0.0f; spacing = 2.6f; break;
                default: sx = 0.9f; sz = 0.9f; thick = 0.12f; drop = 0.0f; spacing = 6.0f; break;
            }
            // A small room gets smaller fittings: a 2.4 m tube does not fit a 2.4 m room.
            sx = Mathf.Max(0.2f, Mathf.Min(sx, area.width - 0.6f));
            sz = Mathf.Max(0.2f, Mathf.Min(sz, area.height - 0.6f));

            // A ceiling 6 m up (a storey is two window rows) would leave a flush fitting lost in the dark: everything hangs a little.
            float underside = ceilingY - Mathf.Min(drop, Mathf.Max(0.0f, ceilingY - floorY - HeadroomMetres)) - thick;

            // Where the fittings' CENTRES may go: the inner rectangle shrunk by half a fitting, so a whole fitting stays inside it.
            float px = Mathf.Max(0.0f, (inner.width - sx) * 0.5f), pz = Mathf.Max(0.0f, (inner.height - sz) * 0.5f);
            inner = Rect.MinMaxRect(inner.center.x - px, inner.center.y - pz, inner.center.x + px, inner.center.y + pz);

            int cols = Mathf.Max(1, Mathf.FloorToInt(inner.width / spacing) + 1);
            int rows = Mathf.Max(1, Mathf.FloorToInt(inner.height / spacing) + 1);
            if (plan.Pattern == LightPattern.CentreFitting)
            {
                cols = Mathf.Clamp(cols, 1, 2);
                rows = Mathf.Clamp(rows, 1, 2);
            }
            while (cols * rows > MaxFixtures)
            {
                if (cols >= rows) cols--; else rows--;
            }

            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    float x = cols == 1 ? inner.center.x : Mathf.Lerp(inner.xMin, inner.xMax, i / (float)(cols - 1));
                    float z = rows == 1 ? inner.center.y : Mathf.Lerp(inner.yMin, inner.yMax, j / (float)(rows - 1));
                    var foot = Rect.MinMaxRect(x - sx * 0.5f - 0.3f, z - sz * 0.5f - 0.3f, x + sx * 0.5f + 0.3f, z + sz * 0.5f + 0.3f);
                    if (Blocked(foot, avoid))
                    {
                        continue;
                    }
                    var min = new Vector3(x - sx * 0.5f, underside, z - sz * 0.5f);
                    var max = new Vector3(x + sx * 0.5f, underside + thick, z + sz * 0.5f);
                    Vector3 cordMin = Vector3.zero, cordMax = Vector3.zero;
                    if (underside + thick < ceilingY - 0.02f)
                    {
                        cordMin = new Vector3(x - 0.02f, underside + thick, z - 0.02f);
                        cordMax = new Vector3(x + 0.02f, ceilingY, z + 0.02f);
                    }
                    plan.Fixtures.Add(new Fixture(min, max, cordMin, cordMax));
                }
            }

            // Every spot of the grid fell on the corner room or the stair hole: one fitting at the free spot nearest the middle, so no room is dark.
            if (plan.Fixtures.Count == 0)
            {
                float bestDistance = float.MaxValue;
                Vector2 best = Vector2.zero;
                bool any = false;
                for (float x = inner.xMin; x <= inner.xMax + 0.01f; x += 0.5f)
                {
                    for (float z = inner.yMin; z <= inner.yMax + 0.01f; z += 0.5f)
                    {
                        var foot = Rect.MinMaxRect(x - sx * 0.5f - 0.3f, z - sz * 0.5f - 0.3f, x + sx * 0.5f + 0.3f, z + sz * 0.5f + 0.3f);
                        float d = (new Vector2(x, z) - area.center).sqrMagnitude;
                        if (d < bestDistance && !Blocked(foot, avoid))
                        {
                            bestDistance = d;
                            best = new Vector2(x, z);
                            any = true;
                        }
                    }
                }
                if (any)
                {
                    var min = new Vector3(best.x - sx * 0.5f, underside, best.y - sz * 0.5f);
                    var max = new Vector3(best.x + sx * 0.5f, underside + thick, best.y + sz * 0.5f);
                    bool cord = underside + thick < ceilingY - 0.02f;
                    plan.Fixtures.Add(new Fixture(min, max,
                        cord ? new Vector3(best.x - 0.02f, underside + thick, best.y - 0.02f) : Vector3.zero,
                        cord ? new Vector3(best.x + 0.02f, ceilingY, best.y + 0.02f) : Vector3.zero));
                }
            }

            // The real lights: one per cell of a 1x1, 2x2 or 3x3 split of the room (by its size) that has fittings in it (at most
            // MaxLightPoints), at the middle of those fittings, a little under them. A big open-plan floor needs more than four to read as lit.
            float span = Mathf.Max(area.width, area.height);
            int split = span > 24.0f ? 3 : (span > 10.0f ? 2 : 1);
            for (int qz = 0; qz < split; qz++)
            {
                for (int qx = 0; qx < split; qx++)
                {
                    Vector3 sum = Vector3.zero;
                    int n = 0;
                    foreach (Fixture f in plan.Fixtures)
                    {
                        Vector3 c = (f.HousingMin + f.HousingMax) * 0.5f;
                        int cx = Mathf.Clamp(Mathf.FloorToInt((c.x - area.xMin) / area.width * split), 0, split - 1);
                        int cz = Mathf.Clamp(Mathf.FloorToInt((c.z - area.yMin) / area.height * split), 0, split - 1);
                        bool inX = cx == qx;
                        bool inZ = cz == qz;
                        if (inX && inZ)
                        {
                            sum += c;
                            n++;
                        }
                    }
                    if (n > 0 && plan.LightPoints.Count < MaxLightPoints)
                    {
                        plan.LightPoints.Add(sum / n + Vector3.down * 0.4f);
                    }
                }
            }
            float reach = Mathf.Max(area.width, area.height) / split;
            plan.LightRange = Mathf.Clamp(reach * 1.3f + (underside - floorY), 7.0f, 28.0f);
            plan.LightIntensity = plan.Pattern == LightPattern.Pendants || plan.Pattern == LightPattern.CentreFitting ? 4.0f : 5.5f;
            return plan;
        }

        /// <summary>
        /// Every room of a building, lit: each floor's hall (its switch on the front wall beside where the entrance is) and its corner room (its switch
        /// inside, beside the doorway). The same rooms CityMeshBuilder.BuildBuilding builds (BuildingStairs.RoomFor, the stair holes), in the building's
        /// frame. Empty for a building with no inside to go into (a sealed sculpture, a round tower).
        /// </summary>
        public static List<RoomLightPlan> PlansFor(BuildingSite site)
        {
            var plans = new List<RoomLightPlan>();
            if (!site.Exists || site.Shell || site.Sides >= 3)
            {
                return plans;
            }
            float height = site.Params.Height;
            float storey = BuildingInterior.StoreyHeight(height);
            int floors = BuildingInterior.FloorCountFor(height);
            float halfX = site.Params.Width * 0.5f;
            float halfZ = site.Params.Depth * 0.5f;
            float wall = BuildingInterior.WallThickness;
            BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
            var avoid = new List<Rect>(3);

            for (int floor = 0; floor < floors; floor++)
            {
                float floorY = site.BaseHeight + floor * storey;
                float ceilingY = site.BaseHeight + BuildingInterior.FloorTop(floor, floors, height, storey) - BuildingInterior.SlabThickness;
                StairPlan flight = stairs.Floors[floor];
                StairPlan arriving = floor > 0 ? stairs.Floors[floor - 1] : stairs.Basement;
                BuildingInterior.Room room = BuildingStairs.RoomFor(floor, halfX, halfZ, flight, arriving);

                // The hall: the whole floor inside the outer walls, round the corner room and the stair's hole.
                avoid.Clear();
                if (room.Exists) avoid.Add(room.Plan);
                if (flight != null) avoid.Add(flight.Hole);
                if (arriving != null) avoid.Add(arriving.Hole);
                var hall = Rect.MinMaxRect(-halfX + wall, -halfZ + wall, halfX - wall, halfZ - wall);
                var hallSwitch = new Vector3(BuildingInterior.DoorWidth * 0.5f + SwitchBesideDoor, floorY + SwitchHeight, halfZ - wall - SwitchDepth * 0.5f);
                plans.Add(Plan(site, floor, 0, hall, avoid, floorY, ceilingY, hallSwitch, Vector3.back));

                if (room.Exists)
                {
                    // Inside the corner room, on the wall with its doorway, just past the doorway's far jamb.
                    bool roomIsWest = room.Plan.center.x < room.DoorWallX;
                    float sx = room.DoorWallX + (roomIsWest ? -1.0f : 1.0f) * (SwitchDepth * 0.5f + 0.01f);
                    var roomSwitch = new Vector3(sx, floorY + SwitchHeight, Mathf.Min(room.DoorZ1 + SwitchBesideDoor, room.Plan.yMax - 0.3f));
                    avoid.Clear();
                    if (flight != null) avoid.Add(flight.Hole);
                    if (arriving != null) avoid.Add(arriving.Hole);
                    plans.Add(Plan(site, floor, 1, room.Plan, avoid, floorY, ceilingY, roomSwitch, roomIsWest ? Vector3.left : Vector3.right));
                }
            }
            return plans;
        }

        private static bool Blocked(Rect foot, IList<Rect> avoid)
        {
            if (avoid == null)
            {
                return false;
            }
            for (int i = 0; i < avoid.Count; i++)
            {
                if (avoid[i].width > 0.0f && avoid[i].Overlaps(foot))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Which buildings have electricity. One in ten, rolled once per building from its seed; <see cref="Restore"/> is the hook for turning a
    /// building's power back on later (a generator, a repaired substation -- owner, 2026-10-09: "we will make a way to turn the electricity back on later").
    /// </summary>
    public static class PowerGrid
    {
        private const ulong PowerSalt = 0x504F574552UL;   // "POWER"

        private static readonly HashSet<int> Restored = new HashSet<int>();

        /// <summary>Whether the building was built with power (before anything is restored).</summary>
        public static bool HasPowerFromTheStart(BuildingSite site)
        {
            return WorldHash.Unit01(site.Id, 0, PowerSalt, unchecked((ulong)(long)site.Seed)) < RoomLighting.PoweredShare;
        }

        /// <summary>Whether a building's lights work now.</summary>
        public static bool IsOn(BuildingSite site)
        {
            lock (Restored)
            {
                return Restored.Contains(site.Id) || HasPowerFromTheStart(site);
            }
        }

        /// <summary>Turns a building's power on (the future "electricity back on" mechanic calls this).</summary>
        public static void Restore(int building)
        {
            lock (Restored)
            {
                Restored.Add(building);
            }
        }

        /// <summary>Forgets every restored building (a new world).</summary>
        public static void Reset()
        {
            lock (Restored)
            {
                Restored.Clear();
            }
        }
    }

    /// <summary>
    /// Which rooms have their lights switched on: shared by everyone in a session (RoomLightsNetwork keeps every machine's copy the same). Pure state
    /// and an event; nothing here draws anything.
    /// </summary>
    public static class RoomLights
    {
        private static readonly Dictionary<RoomKey, bool> On = new Dictionary<RoomKey, bool>();

        /// <summary>A switch was set (by anyone, on any machine). Raised on the main thread.</summary>
        public static event Action<RoomKey, bool> Changed;

        /// <summary>When a session is running, a flip goes through this so every machine hears of it; null in singleplayer.</summary>
        public static IRoomLightRelay Relay;

        public static bool IsOn(RoomKey key) => On.TryGetValue(key, out bool on) && on;

        /// <summary>What the player does at a switch: flips it (through the session when there is one).</summary>
        public static void Flip(RoomKey key)
        {
            bool now = !IsOn(key);
            Set(key, now);
            Relay?.Send(key, now);
        }

        /// <summary>Sets a switch (a flip here, or one heard from another machine). Raises <see cref="Changed"/> when it moved.</summary>
        public static void Set(RoomKey key, bool on)
        {
            if (IsOn(key) == on)
            {
                return;
            }
            if (on)
            {
                On[key] = true;
            }
            else
            {
                On.Remove(key);
            }
            Changed?.Invoke(key, on);
        }

        /// <summary>Every switch that is on (what a player joining a session is told).</summary>
        public static List<RoomKey> AllOn() => new List<RoomKey>(On.Keys);

        public static void Clear()
        {
            var was = new List<RoomKey>(On.Keys);
            On.Clear();
            foreach (RoomKey key in was)
            {
                Changed?.Invoke(key, false);
            }
        }
    }

    /// <summary>Carries a switch flip to the other machines in a session (RoomLightsNetwork).</summary>
    public interface IRoomLightRelay
    {
        void Send(RoomKey key, bool on);
    }
}
