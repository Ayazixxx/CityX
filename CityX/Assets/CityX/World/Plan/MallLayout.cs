using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    public static partial class Amenities
    {
        /// <summary>What one box of a mall complex is (MallLayout).</summary>
        public enum MallPart { Concourse, Anchor, Pad, Garage }

        /// <summary>One box of a mall complex, in the lot's frame (x along the frontage, +z toward the street).</summary>
        public readonly struct MallPiece
        {
            public readonly MassVolume Volume;
            public readonly MallPart Part;

            public MallPiece(MassVolume volume, MallPart part)
            {
                Volume = volume;
                Part = part;
            }
        }

        /// <summary>
        /// How a mall stands on its block. The owner (2026-10-12) sent a photo of a real mall in its car park, a sheet of mall footprints and a real
        /// mall's floor plan: "a large mall should have a large parking lot", "the malls dont have to be a giant square they can be L shaped or some
        /// type of trapazoid", "they are often times wacky with a few large stores and a ton of smaller ones".
        ///
        /// So a mall is a little NETWORK of corridors, each lined both sides with small shops on two or three levels (CityMeshBuilder.BuildMall):
        /// a long SPINE across the back of the lot, ANCHOR department stores capping its ends; off its sides, branches -- an anchor straight on the
        /// spine's flank, a side corridor ending in an anchor or in an entrance, or a side corridor that turns into a WING leading to a big box (a
        /// cinema, say), the way the real plan's east wing runs off to the AMC. Where two pieces meet they share a wall and a passage opens through it.
        /// Pieces meet square (an angled wing would leave a wedge of gap or overlap in a building made of boxes). The complex sits at the back of the
        /// block so the car park fills the front and sides; now and then a multi-storey garage stands beside it (MallParking), and small "pad" buildings
        /// (a restaurant, a bank) stand out by the road. Everything else on the block is the open-air car park, fitted round them
        /// (CitySurfaceBuilder.BuildMallGrounds).
        /// </summary>
        public static class MallLayout
        {
            public const double BackMargin = 14.0;
            public const double SideMargin = 10.0;

            /// <summary>Lot depth kept clear in front of the complex for the car park, at least.</summary>
            public const double MinFrontParking = 35.0;

            /// <summary>One corridor's depth across: a 14 m concourse, 20 m of shops each side, the walls.</summary>
            public const double SegmentDepth = 56.0;

            public const double MinSpine = 90.0;
            public const double MaxSpine = 220.0;
            public const double MinBranch = 30.0;
            public const double MaxBranch = 95.0;
            public const double AnchorWidth = 50.0;

            /// <summary>Pieces that do not join stand at least this far apart (a drive between them), so no two touch by accident.</summary>
            public const double Clearance = 12.0;

            public const double PadWidth = 22.0;
            public const double PadDepth = 16.0;
            public const double PadHeight = 6.5;

            /// <summary>The garage, when the mall has one: this wide at most, and at least.</summary>
            public const double GarageWidth = 52.0;
            public const double MinGarageWidth = 36.0;

            private struct Box
            {
                public double X0, X1, Z0, Z1, H;
                public MallPart Part;
                public bool AlongZ;
                public int Parent;

                public Box(double x0, double x1, double z0, double z1, double h, MallPart part, bool alongZ, int parent)
                {
                    X0 = x0;
                    X1 = x1;
                    Z0 = z0;
                    Z1 = z1;
                    H = h;
                    Part = part;
                    AlongZ = alongZ;
                    Parent = parent;
                }
            }

            /// <summary>A stream of numbers 0..1 from a hash, integer arithmetic only (server and clients must agree).</summary>
            private sealed class Dice
            {
                private uint _state;

                public Dice(uint seed)
                {
                    _state = seed;
                }

                public double Next()
                {
                    _state = PlanNoise.Mix(_state, 0x9E3779B9U);
                    return _state / 4294967296.0;
                }
            }

            /// <summary>
            /// The boxes of one mall in a <paramref name="width"/> x <paramref name="depth"/> lot rectangle, centred on it, front at +z; the first is
            /// the spine. <paramref name="height"/> is the corridors'; <paramref name="hash"/> picks the shape and the extras; a garage stands
            /// beside the mall unless <paramref name="parking"/> is an open lot (and the lot has the room).
            /// </summary>
            public static List<MallPiece> Compose(double width, double depth, double s, double height, uint hash, MallParking parking = MallParking.OpenLot)
            {
                double r = Math.Sqrt(s);
                var dice = new Dice(hash);
                // Cross-sections stay human-sized in every district (the player is the same size everywhere; a 112 m deep corridor would be empty
                // space behind 20 m shops): only lengths, and the car park in front, grow with the district's scale.
                double sd = Math.Min(SegmentDepth, depth * 0.4);
                double aw = AnchorWidth * Math.Min(r, 1.3);
                double gap = Clearance;
                double xMin = -width * 0.5 + SideMargin, xMax = width * 0.5 - SideMargin;
                double zMin = -depth * 0.5 + BackMargin, zMax = depth * 0.5 - MinFrontParking * r;
                double lotFront = depth * 0.5 - SideMargin;
                double anchorHeight = height + (dice.Next() < 0.5 ? 0.0 : 3.0);
                var boxes = new List<Box>();

                // How far forward the spine stands: room behind it for a branch, sometimes.
                double room = zMax - zMin - sd;
                double backLen = 0.0;
                if (room >= 2.0 * MinBranch * r + sd && dice.Next() < 0.45)
                {
                    backLen = Math.Min(room * (0.3 + 0.2 * dice.Next()), MaxBranch * r);
                }
                if (backLen < MinBranch * r)
                {
                    backLen = 0.0;
                }
                double zs = zMin + backLen;

                // The spine, and its end anchors.
                bool west = dice.Next() < 0.8, east = dice.Next() < 0.8;
                double avail = xMax - xMin;
                double want = (MinSpine + (MaxSpine - MinSpine) * dice.Next()) * r;
                double spineLen = Math.Min(want, avail - (west ? aw : 0.0) - (east ? aw : 0.0));
                if (spineLen < MinSpine * r && east)
                {
                    east = false;
                    spineLen = Math.Min(want, avail - (west ? aw : 0.0));
                }
                if (spineLen < MinSpine * r && west)
                {
                    west = false;
                    spineLen = Math.Min(want, avail);
                }
                spineLen = Math.Max(Math.Min(spineLen, avail), Math.Min(MinSpine * r, avail));
                double total = spineLen + (west ? aw : 0.0) + (east ? aw : 0.0);
                double startX = xMin + Math.Max(0.0, avail - total) * dice.Next();
                double sx0 = startX + (west ? aw : 0.0), sx1 = sx0 + spineLen;
                double spineFront = Math.Min(zs + sd, zMax);
                boxes.Add(new Box(sx0, sx1, zs, spineFront, height, MallPart.Concourse, false, -1));

                double endDepth = sd * (1.2 + 0.4 * dice.Next());
                for (int end = 0; end < 2; end++)
                {
                    if (end == 0 ? !west : !east) continue;
                    Box anchor = EndAnchor(end == 0 ? sx0 - aw : sx1, end == 0 ? sx0 : sx1 + aw, boxes[0], endDepth, zMin, zMax, anchorHeight, dice);
                    TryAdd(boxes, anchor, 0, gap, xMin, xMax, zMin, zMax);
                }

                // Branches off the spine's sides.
                int branches = 1 + (int)(dice.Next() * 2.6);   // one to three: "wacky, with a few large stores and a ton of smaller ones"
                if (backLen > 0.0)
                {
                    branches = Math.Max(branches, 1);
                }
                bool usedBack = false;
                for (int attempt = 0, placed = 0; attempt < 6 && placed < branches; attempt++)
                {
                    bool back = backLen > 0.0 && !usedBack;
                    double reach = back ? zs - zMin : zMax - spineFront;
                    if (reach < MinBranch * r)
                    {
                        continue;
                    }
                    double xc = sx0 + sd * 0.5 + 6.0 + Math.Max(0.0, spineLen - sd - 12.0) * dice.Next();
                    var branch = new List<Box>();
                    double kind = dice.Next();
                    double dir = back ? -1.0 : 1.0;
                    double root = back ? zs : spineFront;   // where the branch leaves the spine
                    if (kind < 0.3 || reach < MinBranch * r + gap + 20.0)
                    {
                        // An anchor straight on the spine's flank.
                        double w2 = aw * (1.0 + 0.3 * dice.Next());
                        double d2 = Math.Min(reach, aw * (0.8 + 0.4 * dice.Next()));
                        branch.Add(Span(xc - w2 * 0.5, xc + w2 * 0.5, root, root + dir * d2, anchorHeight, MallPart.Anchor, false, 0));
                    }
                    else
                    {
                        // A side corridor: then an anchor at its end, a wing turning off it, or just an entrance.
                        double endKind = dice.Next();
                        double anchorD = aw * (0.75 + 0.3 * dice.Next());
                        bool withAnchor = endKind < 0.5 && reach >= MinBranch * r + gap + anchorD;
                        bool withWing = !withAnchor && endKind < 0.85 && reach >= sd + gap + MinBranch * r;
                        double len = withAnchor
                            ? Math.Min(reach - anchorD, (MinBranch + (MaxBranch - MinBranch) * dice.Next()) * r)
                            : Math.Min(reach, (MinBranch + (MaxBranch - MinBranch) * dice.Next()) * r);
                        if (withWing)
                        {
                            len = Math.Max(len, sd + gap + 4.0);
                            len = Math.Min(len, reach);
                        }
                        len = Math.Max(len, Math.Max(gap + 2.0, MinBranch * r * 0.8));
                        double far = root + dir * len;
                        int corridor = boxes.Count;
                        branch.Add(Span(xc - sd * 0.5, xc + sd * 0.5, root, far, height, MallPart.Concourse, true, 0));
                        if (withAnchor)
                        {
                            double w2 = Math.Max(aw, sd + 12.0);
                            branch.Add(Span(xc - w2 * 0.5, xc + w2 * 0.5, far, far + dir * anchorD, anchorHeight, MallPart.Anchor, false, corridor));
                        }
                        else if (withWing)
                        {
                            // The wing leaves the corridor's side at its far end and runs off along x to a big box.
                            double side = dice.Next() < 0.5 ? -1.0 : 1.0;
                            double wingLen = (60.0 + 70.0 * dice.Next()) * r;
                            double wz0 = Math.Min(far, far - dir * sd), wz1 = Math.Max(far, far - dir * sd);
                            double wx0 = side > 0.0 ? xc + sd * 0.5 : xc - sd * 0.5 - wingLen;
                            double wx1 = side > 0.0 ? xc + sd * 0.5 + wingLen : xc - sd * 0.5;
                            int wing = corridor + 1;
                            branch.Add(new Box(wx0, wx1, wz0, wz1, height, MallPart.Concourse, false, corridor));
                            double boxW = aw * 1.15;
                            double bx0 = side > 0.0 ? wx1 : wx0 - boxW;
                            Box big = EndAnchor(bx0, bx0 + boxW, branch[branch.Count - 1], sd * (1.3 + 0.4 * dice.Next()), zMin, zMax, anchorHeight + 3.0, dice);
                            big.Parent = wing;
                            branch.Add(big);
                        }
                    }

                    // The whole branch goes in, or none of it.
                    int before = boxes.Count;
                    bool ok = true;
                    foreach (Box b in branch)
                    {
                        if (!TryAdd(boxes, b, b.Parent, gap, xMin, xMax, zMin, zMax))
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (!ok)
                    {
                        boxes.RemoveRange(before, boxes.Count - before);
                        continue;
                    }
                    placed++;
                    usedBack |= back;
                }

                // The garage, when the mall has one: attached, the way a mall's deck stands across the ring road from an anchor's doors -- beside a
                // piece of the mall, one drive's width from it, at the sides and back first so the front lot stays open, the front as a last resort.
                if (parking == MallParking.GarageAndLot)
                {
                    double gd = Math.Min(sd, 50.0);
                    int floors = MinGarageLevels + (int)(dice.Next() * (MaxGarageLevels - MinGarageLevels + 1));
                    double garageHeight = floors * GarageFloorHeight;
                    double[] widths = { GarageWidth, GarageWidth * 0.8, MinGarageWidth };
                    int mallBoxes = boxes.Count;
                    int firstBox = (int)(dice.Next() * mallBoxes);
                    bool placedGarage = false;
                    foreach (double gw in widths)
                    {
                        // Sides of each piece in turn: west, east, back, front.
                        for (int side = 0; side < 4 && !placedGarage; side++)
                        {
                            for (int k = 0; k < mallBoxes && !placedGarage; k++)
                            {
                                Box m = boxes[(firstBox + k) % mallBoxes];
                                double x0, z0;
                                switch (side)
                                {
                                    case 0: x0 = m.X0 - gap - gw; z0 = Math.Max(zMin, m.Z0); break;
                                    case 1: x0 = m.X1 + gap; z0 = Math.Max(zMin, m.Z0); break;
                                    case 2: x0 = (m.X0 + m.X1 - gw) * 0.5; z0 = m.Z0 - gap - gd; break;
                                    default: x0 = (m.X0 + m.X1 - gw) * 0.5; z0 = m.Z1 + gap; break;
                                }
                                placedGarage = TryAdd(boxes, new Box(x0, x0 + gw, z0, z0 + gd, garageHeight, MallPart.Garage, false, -1), -1, gap, xMin, xMax, zMin, lotFront);
                            }
                        }
                        if (placedGarage) break;
                    }
                }

                // Pads out by the road, in the front parking: one to three small single-storey buildings where they are clear of everything.
                double pw = PadWidth, pd = PadDepth;
                double padZ1 = lotFront - 4.0, padZ0 = padZ1 - pd;
                int pads = 1 + (int)(dice.Next() * (width > 260.0 * r ? 3.0 : 2.0));
                double[] spots = { 0.1, 0.9, 0.5, 0.3, 0.7 };
                int padsPlaced = 0;
                foreach (double f in spots)
                {
                    if (padsPlaced >= pads) break;
                    double cx = xMin + pw * 0.5 + (avail - pw) * f;
                    if (TryAdd(boxes, new Box(cx - pw * 0.5, cx + pw * 0.5, padZ0, padZ1, PadHeight, MallPart.Pad, false, -1), -1, gap * 2.0, xMin, xMax, zMin, lotFront))
                    {
                        padsPlaced++;
                    }
                }

                var pieces = new List<MallPiece>(boxes.Count);
                for (int i = 0; i < boxes.Count; i++)
                {
                    Box b = boxes[i];
                    bool wing = i > 0 && (b.Part == MallPart.Concourse || b.Part == MallPart.Anchor);
                    double cx = (b.X0 + b.X1) * 0.5, cz = (b.Z0 + b.Z1) * 0.5;
                    MassVolume v = b.AlongZ
                        // A side corridor runs along the lot's z: its own +x (the corridor's length) is the lot's +z.
                        ? new MassVolume(cx, cz, b.Z1 - b.Z0, b.X1 - b.X0, b.H, 0.0, 1.0, -1, false, wing)
                        : new MassVolume(cx, cz, b.X1 - b.X0, b.Z1 - b.Z0, b.H, 1.0, 0.0, -1, false, wing);
                    pieces.Add(new MallPiece(v, b.Part));
                }
                return pieces;
            }

            /// <summary>A box between two z values given either way round.</summary>
            private static Box Span(double x0, double x1, double za, double zb, double h, MallPart part, bool alongZ, int parent) =>
                new Box(x0, x1, Math.Min(za, zb), Math.Max(za, zb), h, part, alongZ, parent);

            /// <summary>An anchor at the end of a corridor running along x: as deep as <paramref name="depth"/>, covering the corridor's whole depth, pushed out front or back.</summary>
            private static Box EndAnchor(double x0, double x1, Box corridor, double depth, double zMin, double zMax, double h, Dice dice)
            {
                double sd = corridor.Z1 - corridor.Z0;
                depth = Math.Max(depth, sd);
                double lo = Math.Max(zMin, corridor.Z1 - depth), hi = Math.Min(corridor.Z0, zMax - depth);
                double z0 = lo <= hi ? lo + (hi - lo) * dice.Next() : corridor.Z0;
                if (lo > hi) depth = sd;
                return new Box(x0, x1, z0, z0 + depth, h, MallPart.Anchor, false, -1);
            }

            /// <summary>
            /// Adds a box if it is inside the bounds, clear by <paramref name="gap"/> of every box but the one it joins, and touching (not overlapping) that one.
            /// </summary>
            private static bool TryAdd(List<Box> boxes, Box b, int parent, double gap, double xMin, double xMax, double zMin, double zMax)
            {
                const double eps = 1e-6;
                if (b.X1 - b.X0 < 1.0 || b.Z1 - b.Z0 < 1.0) return false;
                if (b.X0 < xMin - eps || b.X1 > xMax + eps || b.Z0 < zMin - eps || b.Z1 > zMax + eps) return false;
                for (int j = 0; j < boxes.Count; j++)
                {
                    if (j == parent)
                    {
                        if (Overlap(b, boxes[j], -0.01)) return false;
                        continue;
                    }
                    if (Overlap(b, boxes[j], gap)) return false;
                }
                b.Parent = parent;
                boxes.Add(b);
                return true;
            }

            private static bool Overlap(Box a, Box b, double gap) =>
                a.X0 < b.X1 + gap && a.X1 > b.X0 - gap && a.Z0 < b.Z1 + gap && a.Z1 > b.Z0 - gap;
        }
    }
}
