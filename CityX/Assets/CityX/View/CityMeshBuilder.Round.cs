using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Round and many-sided buildings (BuildingSite.Sides, RoundFootprint). Asked 2026-10-08: "some round some square". See docs/BUILDING_SHAPES.md.
    ///
    /// EVERY SIDE IS A SHORT FLAT WALL BUILT BY THE ORDINARY WALL CODE. The buffers' frame is turned to each side in turn (the building's own turn, then the
    /// side's), so the side is a north wall at z = 0 running from -half to +half -- and AddWindowedWall, AddDoorRow, glass, frames, reveals, shopfronts and
    /// cracked panes all work on it unchanged. Floors are the outline cut into a middle square (where the stairs are, with their openings) and a ring of plain
    /// quads round it. Floors are open plan: no partitions, which would end at the square and leave gaps to the curved wall.
    /// </summary>
    public static partial class CityMeshBuilder
    {
        private static void BuildRound(CityWorld world, BuildingSite site, Vector3 origin, Vector3 offset,
            MeshBuffer structures, MeshBuffer concrete, Glazing glazing, SiteLooks looks, bool outsideOnly)
        {
            var fp = RoundFootprint.Of(site);
            float half = fp.SideLength * 0.5f;
            float podiumY = site.PodiumBase - origin.y;
            float lobbyY = site.BaseHeight - origin.y;
            float height = site.Params.Height;
            float roofY = lobbyY + height;
            WindowPlan plan = WindowPlan.For(world, site);
            CellLayout rows = WindowGrid.RowsFor(height);
            float doorU0 = -BuildingInterior.DoorWidth * 0.5f;
            float doorU1 = BuildingInterior.DoorWidth * 0.5f;
            float wallT = BuildingInterior.WallThickness;
            bool sealedBlock = site.Shell;

            BuildingStairs.Layout stairs = outsideOnly ? null : BuildingStairs.PlanFor(site);
            int floors = BuildingInterior.FloorCountFor(height);
            float storey = BuildingInterior.StoreyHeight(height);
            int detailRows = 0;
            if (!outsideOnly)
            {
                detailRows = stairs.SealedTop ? Mathf.Min(rows.Count, floors * BuildingInterior.StoreyHeightRows) : rows.Count;
            }
            var detailed = new CellLayout(detailRows, rows.Size);
            float detailTop = detailRows < rows.Count ? lobbyY + detailRows * rows.Size : roofY;
            float baseTop = outsideOnly ? Mathf.Min(roofY, lobbyY + BuildingInterior.RowHeight(height)) : lobbyY;
            float doorTop = lobbyY + BuildingInterior.DoorHeight(height);

            if (!outsideOnly)
            {
                BuildingDecor.EntranceStyle entrance = BuildingDecor.EntranceStyleFor(site.BaseHeight - site.DoorGroundHeight);
                if (entrance.NeedsStairs)
                {
                    AddEntranceStairs(concrete, new Vector3(0.0f, lobbyY, fp.Apothem), entrance.Rise);
                }
            }

            var outer = new Wall(false, 0.0f, -half, half);
            var inner = new Wall(false, -wallT, -half, half);
            for (int side = 0; side < fp.Sides; side++)
            {
                int firstOpening = glazing.Openings != null ? glazing.Openings.Count : 0;
                TurnToSide(site, fp, side, offset, structures, concrete, glazing, out float sideCos, out float sideSin);
                WindowGrid.Wall which = WindowGrid.PolygonSide(side);
                bool door = side == 0 && !sealedBlock;

                // The podium (and, for the distant version, the whole ground row) in the base material.
                structures.Look = looks.Base;
                AddWallPanel(structures, outer, -half, half, podiumY, baseTop, Vector3.forward);

                if (!outsideOnly)
                {
                    AddWindowedWall(structures, glazing, site, outer, -half, half, lobbyY, detailTop, Vector3.forward, plan, which, detailed,
                        doorU0, doorU1, door, looks);
                    AddWindowedWall(concrete, Glazing.OpeningsOnly, site, inner, -half, half, lobbyY, detailTop, Vector3.back, plan, which, detailed,
                        doorU0, doorU1, door);
                }
                if (detailRows < rows.Count)
                {
                    structures.Look = looks.Wall;
                    AddWallPanel(structures, outer, -half, half, Mathf.Max(detailTop, baseTop), roofY, Vector3.forward);
                    if (glazing.HangsGlass)
                    {
                        Color32 shellGlass = SurfaceLook.PackGlass(looks.GlassTint, 0, 0.0f);
                        glazing.Lit.Look = shellGlass;
                        glazing.Dark.Look = shellGlass;
                        AddShellGlass(glazing, site, outer, -half, half, lobbyY, Vector3.forward, plan, which, rows,
                            door ? doorU0 : 0.0f, door ? doorU1 : 0.0f, detailRows);
                    }
                }

                // The parapet along this side, and its coping a little proud. Each is made a corner's worth longer so neighbouring sides close up.
                float corner = ParapetWallThickness * 0.6f;
                structures.Look = looks.Wall;
                structures.AddBox(new Vector3(-half - corner, roofY, -ParapetWallThickness), new Vector3(half + corner, roofY + ParapetWallHeight, 0.0f));
                structures.Look = looks.Trim;
                structures.AddBox(new Vector3(-half - corner - CopingOverhang, roofY + ParapetWallHeight, -ParapetWallThickness - CopingOverhang),
                    new Vector3(half + corner + CopingOverhang, roofY + ParapetWallHeight + CopingHeight, CopingOverhang));

                // A cracked pane's trigger box was written in the side's frame: put it in the building's, turned with the side.
                if (glazing.Openings != null)
                {
                    fp.SideNormal(side, out float nx, out float nz);
                    float cx = nx * fp.Apothem, cz = nz * fp.Apothem;
                    var turn = Quaternion.Euler(0.0f, Mathf.Atan2(-sideSin, sideCos) * Mathf.Rad2Deg, 0.0f);
                    for (int i = firstOpening; i < glazing.Openings.Count; i++)
                    {
                        CrackedOpening o = glazing.Openings[i];
                        var c = new Vector3(cx + o.Centre.x * sideCos - o.Centre.z * sideSin, o.Centre.y, cz + o.Centre.x * sideSin + o.Centre.z * sideCos);
                        glazing.Openings[i] = new CrackedOpening(o.Key, c, o.Size, turn);
                    }
                }
                SetFrameAll(offset, site.AxisX, site.AxisZ, structures, concrete, glazing);
            }

            float innerHalf = fp.InnerHalf;
            if (outsideOnly)
            {
                structures.Look = looks.Roof;
                AddRoundSlabFace(structures, fp, Rect.zero, roofY, Vector3.up);
                AddRoofPlant(structures, site, -innerHalf, innerHalf, -innerHalf, innerHalf, roofY, looks, Rect.zero);
                if (sealedBlock && site.OnRoof)
                {
                    structures.Look = looks.Trim;
                    AddRoundSlabFace(structures, fp, Rect.zero, lobbyY, Vector3.down);
                }
                structures.Look = SurfaceLook.Default;
                return;
            }

            // The floors: the lobby, then each storey's ceiling, the last of which is the roof unless the top is sealed.
            AddRoundSlabFace(concrete, fp, Rect.zero, lobbyY, Vector3.up);
            Rect hatch = Rect.zero;
            for (int floor = 0; floor < floors; floor++)
            {
                float bottom = floor * storey;
                float top = BuildingInterior.FloorTop(floor, floors, height, storey);
                StairPlan flight = stairs.Floors[floor];
                Rect hole = flight != null ? flight.Hole : Rect.zero;
                bool isRoof = floor == floors - 1 && !stairs.SealedTop;
                if (isRoof)
                {
                    structures.Look = looks.Roof;
                    hatch = hole;
                }
                AddRoundSlab(concrete, fp, hole, lobbyY + top, isRoof ? structures : null);
                if (flight != null)
                {
                    AddStairs(concrete, flight, lobbyY + bottom, lobbyY + top - BuildingInterior.SlabThickness, 0.0f, 0.0f, doorTop - lobbyY);
                }
            }
            if (stairs.SealedTop)
            {
                structures.Look = looks.Roof;
                AddRoundSlab(concrete, fp, Rect.zero, roofY, structures);
            }
            structures.Look = looks.Roof;
            AddRoofPlant(structures, site, -innerHalf, innerHalf, -innerHalf, innerHalf, roofY, looks, hatch);
            structures.Look = SurfaceLook.Default;
        }

        /// <summary>
        /// Turns every buffer's frame to side <paramref name="side"/>: the building's own frame (<paramref name="offset"/>, its axis), then the side's turn and its
        /// place out at the apothem, so the side is a north wall at local z = 0 whose outside faces local +Z. Returns the side's own turn (cos, sin).
        /// </summary>
        private static void TurnToSide(BuildingSite site, RoundFootprint fp, int side, Vector3 offset, MeshBuffer structures, MeshBuffer concrete,
            Glazing glazing, out float sideCos, out float sideSin)
        {
            fp.SideNormal(side, out float nx, out float nz);
            // The side's turn takes local +Z to the side's outward normal: (cos, sin) = (nz, -nx).
            sideCos = nz;
            sideSin = -nx;
            float cb = (float)site.AxisX, sb = (float)site.AxisZ;
            float c = cb * sideCos - sb * sideSin;
            float s = sb * sideCos + cb * sideSin;
            float px = nx * fp.Apothem, pz = nz * fp.Apothem;
            Vector3 o = offset + new Vector3(px * cb - pz * sb, 0.0f, px * sb + pz * cb);
            SetFrameAll(o, c, s, structures, concrete, glazing);
        }

        private static void SetFrameAll(Vector3 offset, double axisX, double axisZ, MeshBuffer structures, MeshBuffer concrete, Glazing glazing)
        {
            structures.SetFrame(offset, axisX, axisZ);
            concrete.SetFrame(offset, axisX, axisZ);
            glazing.Lit?.SetFrame(offset, axisX, axisZ);
            glazing.Dark?.SetFrame(offset, axisX, axisZ);
            glazing.Cracked?.SetFrame(offset, axisX, axisZ);
            glazing.Decals?.SetFrame(offset, axisX, axisZ);
        }

        /// <summary>A floor or ceiling slab the shape of the outline: its top face, its underside a slab's thickness down, and the collar round any opening.</summary>
        private static void AddRoundSlab(MeshBuffer buffer, RoundFootprint fp, Rect hole, float topY, MeshBuffer topBuffer = null)
        {
            float bottomY = topY - BuildingInterior.SlabThickness;
            AddRoundSlabFace(topBuffer ?? buffer, fp, hole, topY, Vector3.up);
            AddRoundSlabFace(buffer, fp, hole, bottomY, Vector3.down);
            if (hole.width > 0.0f && hole.height > 0.0f)
            {
                AddHoleCollar(buffer, hole, bottomY, topY);
            }
        }

        /// <summary>
        /// One flat face the shape of the outline: the middle square (with the opening in it, if any), then the ring between the square and the outline, cut
        /// along rays through every corner of either so that each piece between two rays is a plain quad.
        /// </summary>
        private static void AddRoundSlabFace(MeshBuffer buffer, RoundFootprint fp, Rect hole, float y, Vector3 normal)
        {
            float h = fp.InnerHalf;
            AddSlabFace(buffer, -h, h, -h, h, hole, y, normal);

            var angles = new List<double>(fp.Sides + 4);
            for (int k = 0; k < fp.Sides; k++)
            {
                angles.Add(Wrap(fp.CornerAngle(k)));
            }
            for (int q = 0; q < 4; q++)
            {
                angles.Add(Wrap(System.Math.PI * 0.25 + q * System.Math.PI * 0.5));
            }
            angles.Sort();
            for (int i = 0; i < angles.Count; i++)
            {
                double a0 = angles[i];
                double a1 = i + 1 < angles.Count ? angles[i + 1] : angles[0] + 2.0 * System.Math.PI;
                if (a1 - a0 < 1e-5)
                {
                    continue;
                }
                AddFace(buffer, normal, SquareAt(h, a0, y), OutlineAt(fp, a0, y), OutlineAt(fp, a1, y), SquareAt(h, a1, y));
            }
        }

        private static double Wrap(double a)
        {
            double twoPi = 2.0 * System.Math.PI;
            a %= twoPi;
            return a < 0.0 ? a + twoPi : a;
        }

        private static Vector3 OutlineAt(RoundFootprint fp, double angle, float y)
        {
            float r = fp.BoundaryRadius(angle);
            return new Vector3(r * (float)System.Math.Cos(angle), y, r * (float)System.Math.Sin(angle));
        }

        private static Vector3 SquareAt(float h, double angle, float y)
        {
            float c = (float)System.Math.Cos(angle), s = (float)System.Math.Sin(angle);
            float r = h / Mathf.Max(Mathf.Abs(c), Mathf.Abs(s));
            return new Vector3(r * c, y, r * s);
        }
    }
}
