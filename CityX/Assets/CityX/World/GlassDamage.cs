using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>How much of a window is left.</summary>
    public enum PaneState
    {
        /// <summary>Whole. Solid, and lit if its cell is a lit one.</summary>
        Intact = 0,

        /// <summary>
        /// Cracked open. WALKABLE IMMEDIATELY -- city_chunk.gd's building_glass
        /// takes the collision away on the first hit, matching the user's own
        /// "you can now just walk through it", and only swaps the look to
        /// frosted. A window you have to hit twice before you can climb in
        /// would read as the first hit having done nothing.
        /// </summary>
        Cracked = 1,

        /// <summary>Gone. No geometry at all.</summary>
        Shattered = 2,
    }

    /// <summary>
    /// Something a punch can go through rather than land on.
    ///
    /// SEPARATE FROM IDamageable ON PURPOSE. That one carries damage, a
    /// direction and hitstun, and returns nothing; glass has no health and
    /// needs to say whether the blow found any. They also live at opposite ends
    /// of the project -- a body is a Character, a window is a chunk -- and the
    /// interface sits here, beside the damage model, so neither assembly has to
    /// learn about the other.
    /// </summary>
    public interface IBreakableSurface
    {
        /// <summary>
        /// A blow at a world point. True if it found something breakable, which
        /// is what tells a swing it connected.
        /// </summary>
        bool TakePunch(Vector3 point);

        /// <summary>
        /// A blow of a KNOWN kind, and which way it was travelling (zero when unknown). A fist or a bullet
        /// only cracks a whole pane; a blade, a thrown bottle or a body at speed takes it out at once --
        /// the same difference The Last of Us Part II makes between a melee strike and a thrown brick.
        /// Implementers that do not care keep the plain-punch behaviour.
        /// </summary>
        bool TakeBlow(Vector3 point, Vector3 direction, BlowStrength strength) => TakePunch(point);
    }

    /// <summary>How hard a blow to glass is.</summary>
    public enum BlowStrength
    {
        /// <summary>A fist: a whole pane cracks first, and a second blow shatters it.</summary>
        Light = 0,

        /// <summary>A sword, a thrown bottle, a sprinting shoulder: the pane goes at once.</summary>
        Heavy = 1,

        /// <summary>
        /// A bullet: it makes a hole (and a crack pattern round it) and the pane STAYS, so a burst leaves a
        /// hole per shot. Bullets never bring a window down -- only something knocking into it does.
        /// </summary>
        Bullet = 2,
    }

    /// <summary>One window in the world: which building, which wall of it, which cell.</summary>
    public readonly struct PaneKey : IEquatable<PaneKey>
    {
        /// <summary>The building (<see cref="BuildingSite.Id"/>).</summary>
        public readonly int Building;
        public readonly WindowGrid.Wall Wall;
        public readonly int Column;
        public readonly int Row;

        public PaneKey(int building, WindowGrid.Wall wall, int column, int row)
        {
            Building = building;
            Wall = wall;
            Column = column;
            Row = row;
        }

        public bool Equals(PaneKey other)
        {
            return Building == other.Building && Wall == other.Wall && Column == other.Column && Row == other.Row;
        }

        public override bool Equals(object obj)
        {
            return obj is PaneKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Building;
                hash = hash * 397 ^ (int)Wall;
                hash = hash * 397 ^ Column;
                return hash * 397 ^ Row;
            }
        }

        public override string ToString()
        {
            return $"#{Building} {Wall} c{Column} r{Row}";
        }
    }

    /// <summary>
    /// Which windows in the city are broken.
    ///
    /// OUTSIDE THE CHUNK, DELIBERATELY. A chunk is thrown away and rebuilt
    /// whenever the player walks far enough, so anything remembered on the
    /// chunk is forgotten the moment it streams out -- windows would repair
    /// themselves behind your back. Damage lives here, the mesh builder reads
    /// it, and a rebuilt chunk comes back exactly as broken as it was left.
    ///
    /// It is also the shape multiplayer will want: a list of pane keys and
    /// states is the whole of what a joining player needs to see the same
    /// broken city, and none of it depends on chunks being loaded anywhere.
    ///
    /// ONLY BROKEN PANES ARE STORED. An intact window is the default and there
    /// are millions of them.
    /// </summary>
    public sealed class GlassDamage
    {
        private readonly Dictionary<PaneKey, PaneState> _broken =
            new Dictionary<PaneKey, PaneState>();

        /// <summary>
        /// Where, within a CRACKED pane's own rectangle (0..1 on each axis),
        /// the blow that cracked it actually landed -- so the mesh builder can
        /// put a real hole there instead of just frosting the whole pane.
        /// Only ever meaningful for Cracked; a Shattered pane draws nothing at
        /// all, so nobody reads this once a pane gets that far.
        /// </summary>
        private readonly Dictionary<PaneKey, List<Vector2>> _impactPoints =
            new Dictionary<PaneKey, List<Vector2>>();

        /// <summary>
        /// How many bullet holes a pane keeps. Every bullet makes its own hole and the pane STAYS standing --
        /// bullets never bring a window down, only something knocking into it does. Past this many the oldest
        /// hole is dropped to make room (a pane is one small rectangle: a dozen holes is a window that has been
        /// properly shot up, and more would cost geometry for nothing you could tell apart).
        /// </summary>
        public const int MaxBulletHoles = 12;

        /// <summary>Raised after a pane changes state, so a chunk can rebuild.</summary>
        // Chunk generation reads this from worker threads while the game thread may be changing it (a window
        // breaking), so every access goes through this lock. The Changed event is raised OUTSIDE it.
        private readonly object _gate = new object();

        public event Action<PaneKey, PaneState> Changed;

        public int BrokenCount { get { lock (_gate) { return _broken.Count; } } }

        public PaneState StateOf(PaneKey key)
        {
            lock (_gate)
            {
                return _broken.TryGetValue(key, out PaneState state) ? state : PaneState.Intact;
            }
        }

        /// <summary>The most recent hole on a pane (see <see cref="TryGetImpacts"/> for all of them).</summary>
        public bool TryGetImpact(PaneKey key, out Vector2 localUV)
        {
            lock (_gate)
            {
                if (_impactPoints.TryGetValue(key, out List<Vector2> list) && list.Count > 0)
                {
                    localUV = list[list.Count - 1];
                    return true;
                }
            }
            localUV = default;
            return false;
        }

        /// <summary>Every hole on a pane, in the order they were made, as a snapshot (a chunk may be generating on a worker).</summary>
        public bool TryGetImpacts(PaneKey key, out Vector2[] localUVs)
        {
            lock (_gate)
            {
                if (_impactPoints.TryGetValue(key, out List<Vector2> list) && list.Count > 0)
                {
                    localUVs = list.ToArray();
                    return true;
                }
            }
            localUVs = null;
            return false;
        }

        /// <summary>How many holes a pane has.</summary>
        public int HoleCount(PaneKey key)
        {
            lock (_gate)
            {
                return _impactPoints.TryGetValue(key, out List<Vector2> list) ? list.Count : 0;
            }
        }

        /// <summary>
        /// A BULLET: makes its own hole at <paramref name="impactUV"/> and leaves the pane standing (cracked),
        /// up to <see cref="MaxBulletHoles"/>, then the oldest makes way -- it never brings the pane down. Returns the
        /// state the pane is in now; <paramref name="changed"/> is false only for a pane that was already gone.
        /// <see cref="Changed"/> fires for every new hole (the pane's STATE did not move, but what it looks like
        /// did), so the chunk is rebuilt with the new hole in it.
        /// </summary>
        public PaneState Shoot(PaneKey key, Vector2 impactUV, out bool changed)
        {
            PaneState now;
            lock (_gate)
            {
                PaneState was = StateOf(key);
                if (was == PaneState.Shattered)
                {
                    changed = false;
                    return PaneState.Shattered;
                }

                // A bullet never brings the pane down: it adds a hole, and once the pane is full the oldest hole
                // makes way for the newest.
                now = PaneState.Cracked;
                if (!_impactPoints.TryGetValue(key, out List<Vector2> list))
                {
                    list = new List<Vector2>();
                    _impactPoints[key] = list;
                }
                if (list.Count >= MaxBulletHoles)
                {
                    list.RemoveAt(0);
                }
                list.Add(impactUV);
                _broken[key] = now;
            }
            changed = true;
            Changed?.Invoke(key, now);
            return now;
        }

        /// <summary>
        /// Where a world point actually landed within a pane's own rectangle,
        /// 0..1 on each axis. Called only AFTER TryPaneAt has already
        /// confirmed `key` is real, so it re-derives the same row/column
        /// geometry from `key` itself rather than threading an extra out
        /// parameter through TryPaneAt's own eight call sites, most of which
        /// (GlassDamageTests included) only ever care whether a punch landed
        /// on glass at all, not where on it.
        /// </summary>
        public static Vector2 FractionWithinPane(CityWorld world, BuildingSite site, PaneKey key, Vector3 point)
        {
            // The building is turned in the world; its windows are laid out in its own unturned frame (BuildingSite).
            site.ToBuilderFrame(point.x, point.z, out double frameX, out double frameZ);
            point = new Vector3((float)frameX, point.y, (float)frameZ);
            if (site.Sides >= 3)
            {
                RoundAlong(site, point, out _, out float roundAlong, out _);
                CellLayout roundRows = WindowGrid.RowsFor(site.Params.Height);
                CellLayout roundColumns = WindowPlan.For(world, site).ColumnsFor(RoundFootprint.Of(site).SideLength);
                return new Vector2(
                    Mathf.Clamp01((roundAlong - key.Column * roundColumns.Size) / roundColumns.Size),
                    Mathf.Clamp01((point.y - site.BaseHeight - key.Row * roundRows.Size) / roundRows.Size));
            }
            bool runsInZ = key.Wall == WindowGrid.Wall.West || key.Wall == WindowGrid.Wall.East;
            float span = runsInZ ? site.Params.Depth : site.Params.Width;
            float along = runsInZ ? point.z - (float)site.MinZ : point.x - (float)site.MinX;
            float aboveFloor = point.y - site.BaseHeight;

            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            CellLayout columns = WindowPlan.For(world, site).ColumnsFor(span);

            float u = Mathf.Clamp01((along - key.Column * columns.Size) / columns.Size);
            float v = Mathf.Clamp01((aboveFloor - key.Row * rows.Size) / rows.Size);
            return new Vector2(u, v);
        }

        /// <summary>
        /// One hit. Intact cracks, cracked shatters, shattered stays gone.
        ///
        /// Returns the state it moved TO, or Shattered again if it was already
        /// there -- callers use the change to decide whether to make a noise,
        /// so <see cref="Changed"/> only fires when something actually moved.
        ///
        /// `impactUV`, WHEN GIVEN, records where on the pane this particular
        /// blow landed (see FractionWithinPane) -- optional so every existing
        /// caller and test that only cares about the state machine keeps
        /// compiling unchanged.
        /// </summary>
        public PaneState Hit(PaneKey key, Vector2? impactUV = null, bool shatterOutright = false)
        {
            PaneState now;
            lock (_gate)
            {
                PaneState was = StateOf(key);
                if (was == PaneState.Shattered)
                {
                    return PaneState.Shattered;
                }

                now = NextState(was, shatterOutright ? BlowStrength.Heavy : BlowStrength.Light);
                _broken[key] = now;
                if (now == PaneState.Shattered)
                {
                    _impactPoints.Remove(key);   // nothing left to have a hole in
                }
                else if (impactUV.HasValue)
                {
                    if (!_impactPoints.TryGetValue(key, out List<Vector2> list))
                    {
                        list = new List<Vector2>();
                        _impactPoints[key] = list;
                    }
                    list.Add(impactUV.Value);
                }
            }
            Changed?.Invoke(key, now);
            return now;
        }

        /// <summary>
        /// What a blow leaves of a pane: a light one takes it one step (whole to cracked, cracked to gone), a
        /// heavy one takes it all the way.
        /// </summary>
        public static PaneState NextState(PaneState was, BlowStrength strength)
        {
            if (was == PaneState.Shattered || strength == BlowStrength.Heavy)
            {
                return PaneState.Shattered;
            }
            if (strength == BlowStrength.Bullet)
            {
                return PaneState.Cracked;   // a bullet leaves a hole, not a gap (the hole count decides when it goes: see Shoot)
            }
            return was == PaneState.Intact ? PaneState.Cracked : PaneState.Shattered;
        }

        /// <summary>
        /// Puts a pane straight into a state, for loading a saved or replicated
        /// city rather than for play. Fires <see cref="Changed"/> like a hit,
        /// so whatever is on screen catches up.
        /// </summary>
        public void Set(PaneKey key, PaneState state)
        {
            if (StateOf(key) == state)
            {
                return;
            }
            lock (_gate)
            {
                if (state == PaneState.Intact)
                {
                    _broken.Remove(key);
                    _impactPoints.Remove(key);
                }
                else
                {
                    _broken[key] = state;
                }
            }
            Changed?.Invoke(key, state);
        }

        /// <summary>Every broken pane, for saving or for sending to a joining peer.</summary>
        public IEnumerable<KeyValuePair<PaneKey, PaneState>> Broken
        {
            get
            {
                lock (_gate)
                {
                    return new List<KeyValuePair<PaneKey, PaneState>>(_broken);   // a snapshot: the caller may enumerate it while a chunk generates
                }
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _broken.Clear();
                _impactPoints.Clear();
            }
        }

        // --- addressing ---

        /// <summary>
        /// How far from a wall a hit can land and still count as hitting its
        /// glass. A punch lands on the pane's own surface, but a swing's
        /// contact point is the nearest point on a collider and physics
        /// contacts sit a skin width proud.
        /// </summary>
        public const float HitDepth = 0.35f;

        /// <summary>
        /// Which window a point on a building is, if it is one.
        ///
        /// Works off the point alone rather than off a surface normal.
        /// A punch resolves to a contact somewhere near the wall, and reading
        /// the normal of whatever collider the sweep happened to return would
        /// mean trusting a number that belongs to the chunk's single merged
        /// mesh -- one collider for the road, the roofs and every wall in the
        /// chunk. The point says which wall it is near; nothing else has to.
        /// </summary>
        public static bool TryPaneAt(CityWorld world, BuildingSite site, Vector3 point,
            out PaneKey key)
        {
            key = default;
            if (site.Exists)
            {
                site.ToBuilderFrame(point.x, point.z, out double frameX, out double frameZ);
                point = new Vector3((float)frameX, point.y, (float)frameZ);
            }
            // A sealed block's glass is the distant kind (flat strips over a solid wall): there is no pane in it to break.
            if (!site.Exists || site.Shell)
            {
                return false;
            }

            // Below the lobby floor is podium, above the roof is nothing.
            float aboveFloor = point.y - site.BaseHeight;
            if (aboveFloor < 0.0f || aboveFloor > site.Params.Height)
            {
                return false;
            }

            if (site.Sides >= 3)
            {
                return TryRoundPaneAt(world, site, point, aboveFloor, out key);
            }

            // The nearest of the four wall planes, and how far off it the point
            // is. A point well inside the building belongs to no wall.
            float toWest = Mathf.Abs(point.x - (float)site.MinX);
            float toEast = Mathf.Abs(point.x - (float)site.MaxX);
            float toSouth = Mathf.Abs(point.z - (float)site.MinZ);
            float toNorth = Mathf.Abs(point.z - (float)site.MaxZ);

            WindowGrid.Wall wall = WindowGrid.Wall.West;
            float nearest = toWest;
            if (toEast < nearest) { nearest = toEast; wall = WindowGrid.Wall.East; }
            if (toSouth < nearest) { nearest = toSouth; wall = WindowGrid.Wall.South; }
            if (toNorth < nearest) { nearest = toNorth; wall = WindowGrid.Wall.North; }

            if (nearest > HitDepth)
            {
                return false;
            }

            bool runsInZ = wall == WindowGrid.Wall.West || wall == WindowGrid.Wall.East;
            float span = runsInZ ? site.Params.Depth : site.Params.Width;
            float along = runsInZ
                ? point.z - (float)site.MinZ
                : point.x - (float)site.MinX;
            if (along < 0.0f || along > span)
            {
                return false;
            }

            WindowPlan plan = WindowPlan.For(world, site);
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            CellLayout columns = plan.ColumnsFor(span);

            int row = Mathf.Clamp(Mathf.FloorToInt(aboveFloor / rows.Size), 0, rows.Count - 1);
            int column = Mathf.Clamp(Mathf.FloorToInt(along / columns.Size), 0, columns.Count - 1);

            if (!plan.IsGlazed(wall, column, row))
            {
                return false;
            }
            if (IsDoorCell(site, wall, columns, column, row))
            {
                return false;
            }

            key = new PaneKey(site.Id, wall, column, row);
            return true;
        }

        /// <summary>
        /// The pane of a round building under a point (in the builder's frame): the side the point is nearest, how far along it, and the cell there.
        /// Each side is laid out like a north wall, from -half to +half along the side's own +X (RoundFootprint, CityMeshBuilder.Round).
        /// </summary>
        private static bool TryRoundPaneAt(CityWorld world, BuildingSite site, Vector3 framePoint, float aboveFloor, out PaneKey key)
        {
            key = default;
            if (!RoundAlong(site, framePoint, out int side, out float along, out float off) || Mathf.Abs(off) > HitDepth)
            {
                return false;
            }
            var fp = RoundFootprint.Of(site);
            WindowPlan plan = WindowPlan.For(world, site);
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            CellLayout columns = plan.ColumnsFor(fp.SideLength);
            int row = Mathf.Clamp(Mathf.FloorToInt(aboveFloor / rows.Size), 0, rows.Count - 1);
            int column = Mathf.Clamp(Mathf.FloorToInt(along / columns.Size), 0, columns.Count - 1);
            WindowGrid.Wall which = WindowGrid.PolygonSide(side);
            if (!plan.IsGlazed(which, column, row) || IsDoorCell(site, which, columns, column, row))
            {
                return false;
            }
            key = new PaneKey(site.Id, which, column, row);
            return true;
        }

        /// <summary>
        /// Where a point (in the builder's frame) is against a round building's sides: which side, how far along it from its start (0 .. side length),
        /// and how far out of the wall's plane. False when it is past either end of the side.
        /// </summary>
        private static bool RoundAlong(BuildingSite site, Vector3 framePoint, out int side, out float along, out float off)
        {
            var fp = RoundFootprint.Of(site);
            float lx = framePoint.x - (float)site.CentreX;
            float lz = framePoint.z - (float)site.CentreZ;
            side = fp.SideAt(lx, lz);
            fp.SideNormal(side, out float nx, out float nz);
            off = lx * nx + lz * nz - fp.Apothem;
            // The side's own +X is (nz, -nx); its column 0 starts half a side back from the middle.
            along = lx * nz - lz * nx + fp.SideLength * 0.5f;
            return along >= 0.0f && along <= fp.SideLength;
        }

        /// <summary>
        /// A cell the entrance passes through. It never had a window cut in it,
        /// so it cannot be broken -- the same whole-cell exclusion the mesh
        /// builder uses when it builds the door row.
        /// </summary>
        public static bool IsDoorCell(BuildingSite site, WindowGrid.Wall wall,
            CellLayout columns, int column, int row)
        {
            if (wall != WindowGrid.Wall.North || row != 0)
            {
                return false;
            }
            if (site.Sides >= 3)
            {
                // The front side of a round building, laid out from -half to +half with the door in the middle.
                if (site.Shell)
                {
                    return false;
                }
                float start = -RoundFootprint.Of(site).SideLength * 0.5f;
                float cell0 = start + column * columns.Size;
                return cell0 + columns.Size > -BuildingInterior.DoorWidth * 0.5f && cell0 < BuildingInterior.DoorWidth * 0.5f;
            }

            float centreX = (float)site.CentreX;
            float doorU0 = centreX - BuildingInterior.DoorWidth * 0.5f;
            float doorU1 = centreX + BuildingInterior.DoorWidth * 0.5f;
            float cellU0 = (float)site.MinX + column * columns.Size;
            return cellU0 + columns.Size > doorU0 && cellU0 < doorU1;
        }
    }
}
