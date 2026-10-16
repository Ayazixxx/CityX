using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A pedestrian street drawn (World/PedestrianStreets, docs/OPEN_SPACE.md 8): its ribbon is laid by BuildRoad in setts at the pavement's
    /// height with no paint; here its furniture -- bollards and planters across both ends, dead trees, tables, benches and lights down the
    /// middle -- goes into the chunks it stands in, with the open spaces' kit (DrawPiece, shared with the open blocks).
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>A pedestrian street's setts: the square's warm tone, varied by the edge.</summary>
        private static Color32 PedestrianStreetLook(int edgeId)
        {
            float h = LatticeHash(edgeId, 5, 29);
            return SurfaceLook.Pack(new Color(0.82f + 0.06f * h, 0.78f + 0.05f * h, 0.72f), SurfaceLook.Setts, 0.4f);
        }

        private static void BuildPedestrianStreet(Context ctx, EdgeGeometry g, MeshBuffer structures, MeshBuffer lit)
        {
            List<OpenPiece> pieces = PedestrianStreets.Furniture(ctx.Plan, g);
            if (pieces.Count == 0) return;
            bool powered = PedestrianStreets.Powered(ctx.Plan, g.Edge);
            Func<double, double, double> road = RoadHeight(ctx, g);
            foreach (OpenPiece p in pieces)
            {
                if (!ctx.Contains(p.At)) continue;
                DrawPiece(ctx, p, road(p.At.X, p.At.Y) + KerbHeight, structures, lit, powered);
            }
            structures.Look = SurfaceLook.Default;
        }

        /// <summary>One open-space piece standing at a world height, into the chunk's buffers (the open blocks and the pedestrian streets share it).</summary>
        private static void DrawPiece(Context ctx, OpenPiece p, double worldY, MeshBuffer structures, MeshBuffer lit, bool powered)
        {
            Vector3 at = ctx.Local(p.At.X, 0.0, p.At.Y);
            float y = ctx.LocalY(worldY);
            float cos = (float)p.Dir.X, sin = (float)p.Dir.Y;
            switch (p.Kind)
            {
                case OpenProp.Bench: CityProps.AddBench(structures, at.x, at.z, y, cos, sin); break;
                case OpenProp.SeatBlock: CityProps.AddSeatBlock(structures, at.x, at.z, y, cos, sin, p.Seed); break;
                case OpenProp.Planter: CityProps.AddPlanter(structures, at.x, at.z, y, (float)p.HalfX, (float)p.HalfZ, 0.55f, cos, sin); break;
                case OpenProp.TreePlanter: CityProps.AddTreePlanter(structures, at.x, at.z, y, cos, sin, p.Seed); break;
                case OpenProp.DeadTree: CityProps.AddDeadTree(structures, at.x, at.z, y, p.Seed); break;
                case OpenProp.ShadeSail: CityProps.AddShadeSail(structures, at.x, at.z, y, cos, sin, p.Seed, p.Fallen); break;
                case OpenProp.UmbrellaTable: CityProps.AddUmbrellaTable(structures, at.x, at.z, y, cos, sin, p.Seed, p.Fallen); break;
                case OpenProp.PicnicTable: CityProps.AddPicnicTable(structures, at.x, at.z, y, cos, sin); break;
                case OpenProp.CubeSeat: CityProps.AddCubeSeat(structures, at.x, at.z, y, cos, sin, p.Seed); break;
                case OpenProp.BollardLight: CityProps.AddBollardLight(structures, lit, at.x, at.z, y, powered); break;
                case OpenProp.Bollard: CityProps.AddBollard(structures, at.x, at.z, y); break;
                case OpenProp.Fountain: CityProps.AddFountain(structures, at.x, at.z, y, (float)p.Extra, !p.Fallen); break;
                case OpenProp.Tower: CityProps.AddTower(structures, at.x, at.z, y, (float)p.Extra); break;
                case OpenProp.Pergola: CityProps.AddPergola(structures, at.x, at.z, y, cos, sin); break;
                case OpenProp.Chessboard: CityProps.AddChessboard(structures, at.x, at.z, y, cos, sin); break;
                case OpenProp.ChessPiece: CityProps.AddChessPiece(structures, at.x, at.z, y + 0.025f, cos, sin, p.Seed, p.Fallen); break;
                case OpenProp.Kiosk: CityProps.AddKiosk(structures, at.x, at.z, y, cos, sin, p.Seed); break;
                case OpenProp.BikeRack: CityProps.AddBikeRack(structures, at.x, at.z, y, cos, sin); break;
                case OpenProp.NameWall: CityProps.AddNameWall(structures, at.x, at.z, y, cos, sin, p.Seed); break;
                case OpenProp.Rubble: CityProps.AddRubble(structures, at.x, at.z, y, (float)p.Extra, p.Seed); break;
                case OpenProp.Hoarding: CityProps.AddHoarding(structures, at.x, at.z, y, cos, sin, (float)p.HalfX, (float)p.Extra, p.Seed); break;
                case OpenProp.Chair: CityProps.AddChair(structures, at.x, at.z, y, cos, sin, p.Seed, p.Fallen); break;
                case OpenProp.Bin: CityProps.AddBin(structures, at.x, at.z, y, cos, sin); break;
                case OpenProp.RoundBench: CityProps.AddRoundBench(structures, at.x, at.z, y, p.Seed); break;
            }
        }
    }
}
