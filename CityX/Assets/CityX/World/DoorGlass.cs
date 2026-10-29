using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// One sheet of a door's glass, where it stands now: in the building's own frame about its centre (BuildingSite: x along AxisX, z across it),
    /// heights above the building's floor. A sheet across the doorway stands parallel to its wall; an open leaf of a pair stands square to it.
    /// </summary>
    public readonly struct DoorPane
    {
        public readonly PaneKey Key;

        /// <summary>True: the sheet stands at a fixed x and runs in z; false: at a fixed z, running in x.</summary>
        public readonly bool RunsInZ;

        /// <summary>The sheet's x (RunsInZ) or z.</summary>
        public readonly float Plane;

        /// <summary>The glass along the sheet (z when RunsInZ, else x), and above the floor.</summary>
        public readonly float U0, U1, Y0, Y1;

        /// <summary>The sheet's normal in the building's frame (+-x or +-z): its wall's outward one for a sheet across the doorway.</summary>
        public readonly Vector3 Normal;

        /// <summary>Square to its wall (an open leaf of a pair).</summary>
        public readonly bool Turned;

        public DoorPane(PaneKey key, bool runsInZ, float plane, float u0, float u1, float y0, float y1, Vector3 normal, bool turned)
        {
            Key = key;
            RunsInZ = runsInZ;
            Plane = plane;
            U0 = u0;
            U1 = u1;
            Y0 = y0;
            Y1 = y1;
            Normal = normal;
            Turned = turned;
        }

        public float Width => U1 - U0;

        public float Height => Y1 - Y0;

        /// <summary>A point of the building's frame (x, height above the floor, z) as metres along the glass, up it, and off its plane.</summary>
        public Vector3 Local(Vector3 framePoint)
        {
            float u = RunsInZ ? framePoint.z : framePoint.x;
            float off = RunsInZ ? framePoint.x - Plane : framePoint.z - Plane;
            return new Vector3(u - U0, framePoint.y - Y0, off);
        }
    }

    /// <summary>
    /// THE GLASS IN THE DOORS (owner, 2026-10-16: "can you make the sliding glass doors fracturable like the glass windows"). A sliding door's two
    /// panels and a pair's two leaves (BalconyDoors, a footbridge's doorway) each hold a sheet of glass, and each sheet is a pane of its own in
    /// GlassDamage: shot, it takes a bullet hole and cracks, the fifth bullet or a blow brings it down, and something walking into a cracked one
    /// breaks it -- into its own pieces, like a window. Its damage is kept by its key, not by where it stands, so a cracked panel slides open
    /// cracked and a broken leaf swings open empty. A solid wooden door has no glass.
    ///
    /// The same layout CityMeshBuilder.AddBalconyDoor draws from (its panels and leaves, less the frame's bars round each sheet), so what a
    /// bullet hits, what is drawn and what falls in pieces agree. Pure, from the plan and BalconyDoorState.
    /// </summary>
    public static class DoorGlass
    {
        /// <summary>A door's sheets have keys past every window's column: PaneColumnBase + the door's unit x 2 + the panel (0 or 1).</summary>
        public const int PaneColumnBase = 1 << 20;

        /// <summary>The bars of a panel's or leaf's frame round its glass, metres (CityMeshBuilder.AddGlazedPanel).</summary>
        public const float Bar = 0.06f;

        /// <summary>How far off a sheet's plane a blow may land and still be on its glass, metres.</summary>
        public const float HitDepth = 0.2f;

        /// <summary>How far into the building an open leaf reaches, at most, metres (a blow further in is on no door's glass).</summary>
        public const float Reach = 1.6f;

        public static bool IsDoorPane(in PaneKey key) => key.Column >= PaneColumnBase && !BridgeGlass.IsBridgePane(key);

        public static PaneKey KeyOf(int building, in BalconyDoor door, int panel) =>
            new PaneKey(building, door.Wall, PaneColumnBase + door.Unit * 2 + panel, door.Row);

        public static int UnitOf(in PaneKey key) => (key.Column - PaneColumnBase) >> 1;

        public static int PanelOf(in PaneKey key) => (key.Column - PaneColumnBase) & 1;

        /// <summary>A sliding door's panels and a pair's leaves are glazed; a single door is a solid leaf.</summary>
        public static bool Glazed(in BalconyDoor door) => door.Kind == BalconyDoorKind.Sliding || door.Kind == BalconyDoorKind.Double;

        /// <summary>Whether a door stands open (it opens, and BalconyDoorState has it open).</summary>
        public static bool IsOpen(BuildingSite site, in BalconyDoor door) =>
            door.Opens && BalconyDoorState.Shared.IsOpen(site.Id, door.Unit, door.Row);

        /// <summary>
        /// The glass of a door's two panels or leaves as they stand (<paramref name="open"/> or shut), into <paramref name="into"/> (two long at
        /// least); returns how many: 2, or 0 for a solid door.
        /// </summary>
        public static int Panes(BuildingSite site, in BalconyDoor door, bool open, DoorPane[] into)
        {
            if (!Glazed(door))
            {
                return 0;
            }
            float width = door.Width;
            float clear = width - 2.0f * BalconyDoors.Jamb;
            float panelHeight = door.Height - BalconyDoors.HeadBar - BalconyDoors.Threshold;
            float y0 = door.Y0 + BalconyDoors.Threshold + Bar, y1 = door.Y0 + BalconyDoors.Threshold + panelHeight - Bar;
            bool lowSide = BalconyDoors.FixedOnLow(site, door.Unit);
            if (door.Kind == BalconyDoorKind.Sliding)
            {
                // The fixed panel on the outer track, the sliding one on the inner; open, the sliding one stands behind the fixed one.
                float panel = clear * 0.5f + BalconyDoors.SlidingOverlap * 0.5f;
                float fixedAt = lowSide ? BalconyDoors.Jamb : width - BalconyDoors.Jamb - panel;
                float slidingShut = lowSide ? width - BalconyDoors.Jamb - panel : BalconyDoors.Jamb;
                float slidingAt = open ? fixedAt + (lowSide ? BalconyDoors.SlidingStop : -BalconyDoors.SlidingStop) : slidingShut;
                into[0] = Across(site, door, 0, fixedAt + Bar, fixedAt + panel - Bar, BalconyDoors.OuterTrack, y0, y1);
                into[1] = Across(site, door, 1, slidingAt + Bar, slidingAt + panel - Bar, BalconyDoors.InnerTrack, y0, y1);
                return 2;
            }
            // A pair: each leaf turns about its own jamb; open, it stands against the jamb, square to the wall, into the room.
            float leaf = clear * 0.5f;
            float t = BalconyDoors.PanelThickness * 0.5f;
            float plane = BalconyDoors.LeafPlane;
            if (open)
            {
                into[0] = Square(site, door, 0, BalconyDoors.Jamb + t, plane + Bar, plane + leaf - Bar, y0, y1);
                into[1] = Square(site, door, 1, width - BalconyDoors.Jamb - t, plane + Bar, plane + leaf - Bar, y0, y1);
            }
            else
            {
                into[0] = Across(site, door, 0, BalconyDoors.Jamb + Bar, BalconyDoors.Jamb + leaf - Bar, plane, y0, y1);
                into[1] = Across(site, door, 1, width - BalconyDoors.Jamb - leaf + Bar, width - BalconyDoors.Jamb - Bar, plane, y0, y1);
            }
            return 2;
        }

        /// <summary>A sheet across the doorway, <paramref name="x0"/>..<paramref name="x1"/> along it from the low jamb, <paramref name="depth"/> in from the wall's face.</summary>
        private static DoorPane Across(BuildingSite site, in BalconyDoor door, int panel, float x0, float x1, float depth, float y0, float y1)
        {
            float hw = site.Params.Width * 0.5f, hd = site.Params.Depth * 0.5f;
            switch (door.Wall)
            {
                case WindowGrid.Wall.North:
                    return new DoorPane(KeyOf(site.Id, door, panel), false, hd - depth, door.U0 + x0, door.U0 + x1, y0, y1, Vector3.forward, false);
                case WindowGrid.Wall.South:
                    return new DoorPane(KeyOf(site.Id, door, panel), false, -hd + depth, door.U0 + x0, door.U0 + x1, y0, y1, Vector3.back, false);
                case WindowGrid.Wall.East:
                    return new DoorPane(KeyOf(site.Id, door, panel), true, hw - depth, door.U0 + x0, door.U0 + x1, y0, y1, Vector3.right, false);
                default:
                    return new DoorPane(KeyOf(site.Id, door, panel), true, -hw + depth, door.U0 + x0, door.U0 + x1, y0, y1, Vector3.left, false);
            }
        }

        /// <summary>A leaf standing square to the wall at <paramref name="x"/> along the doorway, its glass <paramref name="z0"/>..<paramref name="z1"/> in from the wall's face.</summary>
        private static DoorPane Square(BuildingSite site, in BalconyDoor door, int panel, float x, float z0, float z1, float y0, float y1)
        {
            float hw = site.Params.Width * 0.5f, hd = site.Params.Depth * 0.5f;
            switch (door.Wall)
            {
                case WindowGrid.Wall.North:
                    return new DoorPane(KeyOf(site.Id, door, panel), true, door.U0 + x, hd - z1, hd - z0, y0, y1, Vector3.right, true);
                case WindowGrid.Wall.South:
                    return new DoorPane(KeyOf(site.Id, door, panel), true, door.U0 + x, -hd + z0, -hd + z1, y0, y1, Vector3.right, true);
                case WindowGrid.Wall.East:
                    return new DoorPane(KeyOf(site.Id, door, panel), false, door.U0 + x, hw - z1, hw - z0, y0, y1, Vector3.forward, true);
                default:
                    return new DoorPane(KeyOf(site.Id, door, panel), false, door.U0 + x, -hw + z0, -hw + z1, y0, y1, Vector3.forward, true);
            }
        }

        [System.ThreadStatic] private static List<BalconyDoor> s_doors;
        [System.ThreadStatic] private static DoorPane[] s_panes;

        /// <summary>
        /// The glazed doors of a building: its flats' balcony doors (when it has balconies) and its footbridges' doors (once its bridges are
        /// known), into a list kept for the calling thread.
        /// </summary>
        private static List<BalconyDoor> DoorsOf(CityWorld world, BuildingSite site)
        {
            List<BalconyDoor> doors = s_doors ?? (s_doors = new List<BalconyDoor>());
            doors.Clear();
            if (site.Sides >= 3 || site.Shell)
            {
                return doors;
            }
            WindowPlan plan = world != null ? WindowPlan.For(world, site) : WindowPlan.For(site);
            if (ApartmentFront.For(site, plan).Balconies)
            {
                BalconyDoors.For(site, plan, doors);
            }
            Footbridge[] bridges = world != null ? Footbridges.For(world, site) : Footbridges.Cached(site);
            if (bridges.Length > 0)
            {
                Footbridges.DoorsOf(site, plan, bridges, doors);
            }
            return doors;
        }

        /// <summary>
        /// The sheet of door glass a blow at <paramref name="framePoint"/> lands on (the building's frame about its centre: x, height above the
        /// floor, z), if any: within <see cref="HitDepth"/> of a sheet's plane and over its glass, as the doors stand now.
        /// </summary>
        public static bool TryPaneAt(CityWorld world, BuildingSite site, Vector3 framePoint, out DoorPane pane)
        {
            pane = default;
            float hw = site.Params.Width * 0.5f, hd = site.Params.Depth * 0.5f;
            // Near a wall at all: a door's glass stands in its doorway or, open, a leaf's width into the room.
            if (Mathf.Abs(framePoint.x) > hw + HitDepth || Mathf.Abs(framePoint.z) > hd + HitDepth
                || (Mathf.Abs(framePoint.x) < hw - Reach && Mathf.Abs(framePoint.z) < hd - Reach))
            {
                return false;
            }
            List<BalconyDoor> doors = DoorsOf(world, site);
            DoorPane[] panes = s_panes ?? (s_panes = new DoorPane[2]);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < doors.Count; i++)
            {
                BalconyDoor door = doors[i];
                if (!Glazed(door) || framePoint.y < door.Y0 - 0.1f || framePoint.y > door.Y1 + 0.1f)
                {
                    continue;
                }
                int n = Panes(site, door, IsOpen(site, door), panes);
                for (int k = 0; k < n; k++)
                {
                    Vector3 p = panes[k].Local(framePoint);
                    float off = Mathf.Abs(p.z);
                    if (off > HitDepth || p.x < -0.02f || p.x > panes[k].Width + 0.02f || p.y < -0.02f || p.y > panes[k].Height + 0.02f || off >= best)
                    {
                        continue;
                    }
                    best = off;
                    pane = panes[k];
                    found = true;
                }
            }
            return found;
        }

        /// <summary>The sheet a door pane's key names, as its door stands now. False for a key that names no glazed door of the building.</summary>
        public static bool TryPane(CityWorld world, BuildingSite site, in PaneKey key, out DoorPane pane)
        {
            pane = default;
            if (!IsDoorPane(key) || key.Building != site.Id)
            {
                return false;
            }
            int unit = UnitOf(key), panel = PanelOf(key);
            List<BalconyDoor> doors = DoorsOf(world, site);
            DoorPane[] panes = s_panes ?? (s_panes = new DoorPane[2]);
            for (int i = 0; i < doors.Count; i++)
            {
                BalconyDoor door = doors[i];
                if (door.Unit != unit || door.Row != key.Row || door.Wall != key.Wall)
                {
                    continue;
                }
                int n = Panes(site, door, IsOpen(site, door), panes);
                if (panel < n)
                {
                    pane = panes[panel];
                    return true;
                }
                return false;
            }
            return false;
        }

        /// <summary>Where a point (the building's frame) lands on a sheet's glass, 0..1 along it and up it.</summary>
        public static Vector2 Fraction(in DoorPane pane, Vector3 framePoint)
        {
            Vector3 p = pane.Local(framePoint);
            return new Vector2(Mathf.Clamp01(p.x / Mathf.Max(1e-4f, pane.Width)), Mathf.Clamp01(p.y / Mathf.Max(1e-4f, pane.Height)));
        }
    }
}
