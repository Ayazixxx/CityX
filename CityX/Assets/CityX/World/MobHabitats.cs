using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>What kind of thing an enemy is (owner, 2026-11-14: "monster and ghost type enemies spawn outside and humanoid enemies spawn indoors for starters").</summary>
    public enum MobFamily
    {
        Ghost,
        Monster,
        Humanoid,

        /// <summary>A hobo (docs/HOBO.md): out of doors, minding his own business until hurt.</summary>
        Hobo,
    }

    /// <summary>What a spot in the city is, to an enemy looking for somewhere to appear.</summary>
    public enum MobHabitat
    {
        /// <summary>Nowhere an enemy appears: in a wall, a car park, a mall, a sealed block, or too near Old Guy's house.</summary>
        None,

        /// <summary>Open ground outside every building: a street, a pavement, a square, an empty lot. Ghosts and monsters.</summary>
        Outdoors,

        /// <summary>On a floor of an ordinary building. Humanoids.</summary>
        Indoors,
    }

    /// <summary>
    /// WHAT MAY APPEAR WHERE (docs/MOB_SPAWNING.md). Enemies have no set places (owner, 2026-11-14: "it's not that the mobs have set spawn
    /// locations right? it should be more akin to minecraft where they spawn at random"): the spawner rolls a spot near a player, and this
    /// says what that spot is, from the plan alone: open ground, where a ghost or a monster may appear, the inside of an ordinary building,
    /// where a humanoid may, or nowhere. Pure: no scene, the same answer on every machine.
    /// </summary>
    public static class MobHabitats
    {
        /// <summary>The share of the outdoor enemies that are monsters (the hobos are next, then the ghosts).</summary>
        public const float MonsterShare = 0.25f;

        /// <summary>The share of the outdoor enemies that are hobos (docs/HOBO.md, mine); after the monsters, the rest are ghosts.</summary>
        public const float HoboShare = 0.4f;

        /// <summary>How far in from a building's outside walls an enemy appears at the least, metres, and how far out from them outdoors.</summary>
        public const double IndoorWallMarginMetres = 2.5;

        public const double OutdoorWallMarginMetres = 1.0;

        /// <summary>A building narrower than this either way holds nobody, metres: no room to stand clear of its walls and its stair.</summary>
        public const double SmallestOccupiedSide = 9.0;

        /// <summary>No enemy appears this near Old Guy's house, metres: a new character starts there with nothing in its hands.</summary>
        public const double TutorialSafeMetres = 120.0;

        /// <summary>
        /// The most enemies alive near one place, out of doors and indoors: the budget. Bodies are what a crowd costs (docs/CROWD_TEST.md:
        /// about 0.65 ms a frame each on the owner's laptop), so these want measuring before they are raised. Players standing together
        /// share them (EnemyNetwork counts what is alive within <see cref="CrowdMetres"/> of whoever asks).
        /// </summary>
        public const int MostOutdoors = 4;

        public const int MostIndoors = 6;

        /// <summary>An enemy within this of a player counts toward that player's budget, metres; further, it is taken out of the world.</summary>
        public const float CrowdMetres = 130.0f;

        /// <summary>
        /// A CLEARED BUILDING STAYS EMPTY (owner, 2026-11-14: "Cleared buildings should stop spawning enemies"): once the last enemy alive in
        /// a building has been killed, nothing appears in it again for this long, seconds: a day (owner, 2026-10-22: "Unclaimed buildings
        /// get new enemies and loot at random, about once a day").
        /// </summary>
        public const float ClearedSeconds = 86400.0f;

        /// <summary>The most alive near one place in <paramref name="habitat"/>.</summary>
        public static int Most(MobHabitat habitat) => habitat == MobHabitat.Indoors ? MostIndoors : habitat == MobHabitat.Outdoors ? MostOutdoors : 0;

        /// <summary>
        /// What the spot (<paramref name="x"/>, <paramref name="z"/>) is, and for <see cref="MobHabitat.Indoors"/> the building it is in.
        /// </summary>
        public static MobHabitat At(PlanWorld plan, double x, double z, out BuildingSite site)
        {
            site = BuildingSite.None;
            if (plan.OnTutorialLot(x, z, TutorialSafeMetres))
            {
                return MobHabitat.None;
            }
            int building = plan.BuildingAt(x, z);
            if (building < 0)
            {
                // Open ground, if it is clear of every wall.
                return plan.BuildingAt(x, z, OutdoorWallMarginMetres) < 0 ? MobHabitat.Outdoors : MobHabitat.None;
            }
            site = plan.Buildings[building];
            if (!Holds(site))
            {
                return MobHabitat.None;
            }
            site.WorldToLocal(x, z, out double u, out double v);
            bool clearOfWalls = System.Math.Abs(u) <= site.Params.Width * 0.5 - IndoorWallMarginMetres
                && System.Math.Abs(v) <= site.Params.Depth * 0.5 - IndoorWallMarginMetres;
            return clearOfWalls ? MobHabitat.Indoors : MobHabitat.None;
        }

        /// <summary>
        /// The buildings that can hold enemies whose chunks are within <paramref name="reach"/> chunks of chunk
        /// (<paramref name="chunkX"/>, <paramref name="chunkY"/>), added to <paramref name="into"/> by their ids. (The insides of
        /// buildings are a few parts in a hundred of the ground: a spot rolled over the ground, as for the open air, would hardly ever
        /// be indoors. So a humanoid's spot is rolled in a building rolled from these.)
        /// </summary>
        public static void BuildingsNear(PlanWorld plan, int chunkX, int chunkY, int reach, System.Collections.Generic.List<int> into)
        {
            for (int cx = chunkX - reach; cx <= chunkX + reach; cx++)
            {
                for (int cy = chunkY - reach; cy <= chunkY + reach; cy++)
                {
                    foreach (int building in plan.BuildingsOwnedBy(cx, cy))
                    {
                        if (Holds(plan.Buildings[building]))
                        {
                            into.Add(building);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A spot inside <paramref name="site"/>, clear of its outside walls, by two rolls of 0 to 1 (where across it, where along it).
        /// False for a building that holds nobody, or a spot too near Old Guy's house.
        /// </summary>
        public static bool TryIndoorSpot(PlanWorld plan, in BuildingSite site, float across, float along, out double x, out double z)
        {
            x = 0.0;
            z = 0.0;
            if (!Holds(site))
            {
                return false;
            }
            double u = (across * 2.0 - 1.0) * (site.Params.Width * 0.5 - IndoorWallMarginMetres);
            double v = (along * 2.0 - 1.0) * (site.Params.Depth * 0.5 - IndoorWallMarginMetres);
            site.LocalToWorld(u, v, out x, out z);
            return !plan.OnTutorialLot(x, z, TutorialSafeMetres);
        }

        /// <summary>Whether <paramref name="site"/> is a building enemies can stand in: an ordinary one with floors, big enough.</summary>
        public static bool Holds(in BuildingSite site)
        {
            return site.Exists && !site.IsOpenGround && !site.IsParkingGarage && !site.IsMall && !site.IsMallAnchor
                && !site.Shell && !site.Wing && !site.OnRoof
                && site.Params.Width >= SmallestOccupiedSide && site.Params.Depth >= SmallestOccupiedSide;
        }

        /// <summary>How many floors <paramref name="site"/> has to stand on.</summary>
        public static int Floors(in BuildingSite site) => BuildingInterior.FloorCountFor(site.Params.Height);

        /// <summary>The height of the top of floor <paramref name="floor"/> of <paramref name="site"/> (0 the ground floor), where feet stand.</summary>
        public static float FloorY(in BuildingSite site, int floor)
        {
            float height = site.Params.Height;
            return site.BaseHeight + (floor <= 0 ? 0.0f : BuildingInterior.FloorTop(floor - 1, Floors(site), height, BuildingInterior.StoreyHeight(height)));
        }

        /// <summary>The family that appears in <paramref name="habitat"/>, by a roll of 0 to 1: indoors a humanoid, outdoors a monster a quarter of the time and a ghost the rest.</summary>
        public static MobFamily FamilyFor(MobHabitat habitat, float roll)
        {
            if (habitat == MobHabitat.Indoors)
            {
                return MobFamily.Humanoid;
            }
            return roll < MonsterShare ? MobFamily.Monster : roll < MonsterShare + HoboShare ? MobFamily.Hobo : MobFamily.Ghost;
        }
    }
}
