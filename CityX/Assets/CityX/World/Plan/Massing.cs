using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>
    /// One box of a building's massing, in the lot rectangle's own frame (x along the frontage, z out of the front).
    ///
    /// A box can be TURNED: <see cref="TurnX"/>/<see cref="TurnZ"/> is the direction of its own +X in the lot frame, so a wing can face its door into a
    /// yard and a stage of a twisted tower can stand at an angle to the one below. Unturned (1, 0) is the old box, and <see cref="X0"/> .. <see cref="Z1"/>
    /// are its edges; for a turned box they are the edges it would have if it were not turned.
    /// </summary>
    public readonly struct MassVolume
    {
        public readonly double CentreX, CentreZ;

        /// <summary>Its size in its OWN frame: along its own +X, and along its own +Z (out of its front, where its door is).</summary>
        public readonly double Width, Depth;

        public readonly double Height;

        /// <summary>The direction of the box's own +X in the lot frame (unit). Its own +Z (its front) is this turned a quarter counter-clockwise.</summary>
        public readonly double TurnX, TurnZ;

        /// <summary>The index of the volume this one stands on, or -1 when it stands on the ground.</summary>
        public readonly int Support;

        /// <summary>
        /// Sealed: built as its outside only, at any distance -- no door, no floors, no rooms. A stage of a twisted or stacked tower overhangs and turns, so
        /// no door of it would open onto anything; it is sculpture on top of the building you can go into.
        /// </summary>
        public readonly bool Shell;

        /// <summary>A second block at street level (the leg of an L, the arms of a U): it dresses like the main one and has no shopfront.</summary>
        public readonly bool Wing;

        /// <summary>0 for a box; otherwise round, a regular polygon of this many sides whose corners are on the circle <see cref="Width"/> across (BuildingSite.Sides).</summary>
        public readonly int Sides;

        public MassVolume(double x0, double x1, double z0, double z1, double height)
            : this((x0 + x1) * 0.5, (z0 + z1) * 0.5, x1 - x0, z1 - z0, height, 1.0, 0.0, -1, false, false)
        {
        }

        public MassVolume(double centreX, double centreZ, double width, double depth, double height,
            double turnX, double turnZ, int support, bool shell, bool wing, int sides = 0)
        {
            Sides = sides;
            CentreX = centreX;
            CentreZ = centreZ;
            Width = width;
            Depth = depth;
            Height = height;
            TurnX = turnX;
            TurnZ = turnZ;
            Support = support;
            Shell = shell;
            Wing = wing;
        }

        public double X0 => CentreX - Width * 0.5;
        public double X1 => CentreX + Width * 0.5;
        public double Z0 => CentreZ - Depth * 0.5;
        public double Z1 => CentreZ + Depth * 0.5;

        public bool Turned => TurnX != 1.0 || TurnZ != 0.0;

        public MassVolume StandingOn(int support) =>
            new MassVolume(CentreX, CentreZ, Width, Depth, Height, TurnX, TurnZ, support, Shell, Wing, Sides);

        /// <summary>Its four corners in the lot frame.</summary>
        public void Corners(double[] xs, double[] zs)
        {
            double hx = Width * 0.5, hz = Depth * 0.5;
            // own +X = (TurnX, TurnZ); own +Z = (-TurnZ, TurnX)
            for (int i = 0; i < 4; i++)
            {
                double lx = (i == 0 || i == 3) ? -hx : hx;
                double lz = i < 2 ? -hz : hz;
                xs[i] = CentreX + lx * TurnX - lz * TurnZ;
                zs[i] = CentreZ + lx * TurnZ + lz * TurnX;
            }
        }

        /// <summary>A lot-frame point in this box's own frame, relative to its centre.</summary>
        public void ToOwn(double x, double z, out double ownX, out double ownZ)
        {
            double dx = x - CentreX, dz = z - CentreZ;
            ownX = dx * TurnX + dz * TurnZ;
            ownZ = -dx * TurnZ + dz * TurnX;
        }
    }

    /// <summary>The overall shape a building was given: what the city's variety is made of. Kept on the result so it can be counted and tested.</summary>
    public enum BuildingForm
    {
        Box = 0,
        /// <summary>A tower on a podium and/or a stepped crown (setbacks).</summary>
        Stepped = 1,
        /// <summary>A bar on the street and a wing going back from one end.</summary>
        LShape = 2,
        /// <summary>A bar on the street and a wing going back from its middle.</summary>
        TShape = 3,
        /// <summary>A bar on the street and a wing back from each end, a court between them.</summary>
        UShape = 4,
        /// <summary>Two blocks side by side, one set back from the street and a different height.</summary>
        Staggered = 5,
        /// <summary>Stages that turn a few degrees more at every one, all the way up.</summary>
        Twisted = 6,
        /// <summary>A pile of blocks, each a little turned and pushed off the one under it.</summary>
        Wonky = 7,
        /// <summary>A big block on top that hangs out over the street or the yard.</summary>
        Cantilever = 8,
        /// <summary>A round or many-sided tower (6 to 20 sides), sometimes with narrower round crowns on top.</summary>
        Round = 9,
    }

    /// <summary>
    /// What a building's SHAPE is: not one box, but often a stack of them -- a podium with a tower standing on it, a tower that steps in
    /// as it climbs, a crown that is narrower than the shaft.
    ///
    /// WHY THIS IS THE BIGGEST THING FOR THE SKYLINE. A field of identical extruded boxes is what makes a procedural city read as a toy;
    /// real skylines get their silhouette from setbacks (New York's 1916 zoning forced towers to step back as they rose, which made the
    /// wedding-cake skyline), from towers standing on wider podiums, and from crowns. Seen from the air, that is the whole look.
    ///
    /// EACH VOLUME IS ITS OWN BUILDING (a BuildingSite): own walls, windows, door, glass, floors, stairs. The upper volume stands on the
    /// roof of the one below, so everything that already works for a building works unchanged; the podium's roof hatch is how you get up
    /// to the tower's door. A pure function of the lot and a roll, so every client builds the same skyline.
    ///
    /// SHAPES BEYOND THE STACK (<see cref="Compose"/>, 2026-10-08, asked: "all sorts of different building shapes and sizes ... some normal some wonky
    /// some round some square some L shaped"). Every shape is still made of boxes, because the whole building generator (windows you can shoot,
    /// doors, stairs, rooms) works on a box: an L is a bar and a wing that touch, a twisted tower is a stack of turned boxes. Where two blocks touch,
    /// the walls between them are blank (BuildingSite.Contacts). Everything stays inside the lot rectangle, so nothing reaches a neighbour.
    ///
    /// DETERMINISM: double + - * / sqrt only, angles from the literal tangent table below (no sin/cos), as the whole plan is.
    /// </summary>
    public static class Massing
    {
        /// <summary>A storey: two window rows of 3.2 m. Volumes are whole storeys tall so floors and window rows keep lining up.</summary>
        public const double Storey = 6.4;

        /// <summary>The smallest a volume may be on a side, metres: smaller is a chimney, not a floor of a building.</summary>
        public const double MinSide = 9.0;

        /// <summary>The shortest a volume may be, metres: two storeys.</summary>
        public const double MinHeight = 2.0 * Storey;

        /// <summary>The narrowest yard a wing may open its door onto, metres (classic scale).</summary>
        public const double MinYard = 7.0;

        /// <summary>
        /// The chance a tower is one of the odd stacked shapes (twisted, wonky, cantilevered). Asked 2026-10-09, after seeing one in game: "make these
        /// weird stacked buildings super uncommon like 1% chance or something even if its just 1 stack".
        /// </summary>
        public const double OddStackChance = 0.01;

        /// <summary>How far one stage of a twisted tower turns from the one below: tan of 4..9 degrees.</summary>
        private static readonly double[] TwistTangents = { 0.0699268119, 0.0874886635, 0.1051042353, 0.1227845609, 0.1405408347, 0.1583844403 };

        /// <summary>How far a block of a wonky stack is turned off the lot: tan of -12..12 degrees in steps of 3.</summary>
        private static readonly double[] WonkyTangents =
            { -0.2125565617, -0.1583844403, -0.1051042353, -0.0524077793, 0.0, 0.0524077793, 0.1051042353, 0.1583844403, 0.2125565617 };

        public static List<MassVolume> Split(ZoneKind zone, BuildingMass mass, double width, double depth, double height, DeterministicRandom rng,
            out bool hasPodium, double scale = 1.0, double heightScale = 1.0)
        {
            // The thresholds and setbacks below are the classic city's metres; at a larger CityScale a building is judged by its size relative to that.
            double rh = height / heightScale;
            double rw = Math.Min(width, depth) / scale;
            hasPodium = false;
            double hx = width * 0.5;
            double hz = depth * 0.5;
            var whole = new MassVolume(-hx, hx, -hz, hz, height);
            var volumes = new List<MassVolume>();

            bool tall = mass == BuildingMass.Tower || mass == BuildingMass.Supertall;
            double podiumChance = tall ? 0.85
                : (mass == BuildingMass.MidRise && rh >= 44.0 && rw >= 22.0 ? 0.35
                : (mass == BuildingMass.Slab && rh >= 50.0 ? 0.20 : 0.0));
            if (zone == ZoneKind.Industrial || zone == ZoneKind.Outskirts)
            {
                podiumChance = 0.0;
            }
            double steppedChance = mass == BuildingMass.Supertall ? 1.0
                : (mass == BuildingMass.Tower ? 0.55
                : (mass == BuildingMass.MidRise && rh >= 36.0 ? 0.30
                : (mass == BuildingMass.Slab ? 0.15 : 0.0)));
            if (zone == ZoneKind.Industrial)
            {
                steppedChance = 0.0;
            }

            double roll = rng.NextDouble();
            double stepRoll = rng.NextDouble();

            MassVolume top = whole;   // the volume the crown (if any) will stand on
            if (roll < podiumChance && TryPodiumAndTower(whole, height, rng, scale, heightScale, out MassVolume podium, out MassVolume tower))
            {
                volumes.Add(podium);
                top = tower;
                hasPodium = true;
            }

            if (stepRoll < steppedChance)
            {
                AddSteppedTower(volumes, top, rng, mass == BuildingMass.Supertall, scale);
            }
            else
            {
                volumes.Add(top);
            }
            return volumes;
        }

        private static bool TryPodiumAndTower(MassVolume whole, double height, DeterministicRandom rng, double scale, double heightScale, out MassVolume podium, out MassVolume tower)
        {
            podium = default;
            tower = default;

            double storeys = Math.Round((2 + rng.Below(4)) * heightScale);   // 2..5 storeys, more on a taller building
            double podiumHeight = Math.Min(storeys * Storey, Math.Floor(height * 0.45 / Storey) * Storey);
            double towerHeight = height - podiumHeight;
            if (podiumHeight < MinHeight || towerHeight < MinHeight)
            {
                return false;
            }

            // The tower stands back from the street (+z) more than from the rest, and is rarely centred.
            double front = (2.0 + 6.0 * rng.NextDouble()) * scale;
            double back = (1.0 + 5.0 * rng.NextDouble()) * scale;
            double left = (1.5 + 5.5 * rng.NextDouble()) * scale;
            double right = (1.5 + 5.5 * rng.NextDouble()) * scale;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                if (whole.Width - left - right >= MinSide * scale && whole.Depth - front - back >= MinSide * scale)
                {
                    break;
                }
                front *= 0.7;
                back *= 0.7;
                left *= 0.7;
                right *= 0.7;
            }
            if (whole.Width - left - right < MinSide * scale || whole.Depth - front - back < MinSide * scale)
            {
                return false;
            }

            podium = new MassVolume(whole.X0, whole.X1, whole.Z0, whole.Z1, podiumHeight);
            tower = new MassVolume(whole.X0 + left, whole.X1 - right, whole.Z0 + back, whole.Z1 - front, towerHeight);
            return true;
        }

        /// <summary>
        /// A volume that steps in as it climbs: the shaft to some way up, then a narrower stage (and for a supertall another above that).
        /// Each stage is inset a few metres from the one below on every side, by a different amount, so it is never a tidy pyramid.
        /// </summary>
        private static void AddSteppedTower(List<MassVolume> volumes, MassVolume shaft, DeterministicRandom rng, bool twice, double scale)
        {
            MassVolume current = shaft;
            double remaining = shaft.Height;
            int stages = twice ? 2 : 1;
            for (int stage = 0; stage < stages; stage++)
            {
                double share = 0.60 + 0.20 * rng.NextDouble();
                double lowerHeight = Math.Floor(remaining * share / Storey) * Storey;
                double upperHeight = remaining - lowerHeight;
                double left = (2.0 + 3.5 * rng.NextDouble()) * scale;
                double right = (2.0 + 3.5 * rng.NextDouble()) * scale;
                double front = (1.5 + 3.5 * rng.NextDouble()) * scale;
                double back = (1.5 + 3.5 * rng.NextDouble()) * scale;
                if (lowerHeight < MinHeight || upperHeight < MinHeight
                    || current.Width - left - right < MinSide * scale || current.Depth - front - back < MinSide * scale)
                {
                    break;
                }

                volumes.Add(new MassVolume(current.X0, current.X1, current.Z0, current.Z1, lowerHeight));
                current = new MassVolume(current.X0 + left, current.X1 - right, current.Z0 + back, current.Z1 - front, upperHeight);
                remaining = upperHeight;
            }
            volumes.Add(current);
        }

        // ------------------------------------------------------------------ the wider family of shapes

        /// <summary>
        /// The whole shape of the building on a lot rectangle (width x depth, front at +depth/2), as volumes with their supports set. The first volume
        /// is the main block on the street; a volume's support always comes before it.
        /// </summary>
        public static List<MassVolume> Compose(ZoneKind zone, BuildingMass mass, double width, double depth, double height, DeterministicRandom rng,
            out bool hasPodium, out BuildingForm form, double scale = 1.0, double heightScale = 1.0)
        {
            // Drawn up front, whatever happens next, so a building's other rolls never shift with which shape it got.
            double formRoll = rng.NextDouble();
            double pickRoll = rng.NextDouble();
            var shapeRng = new DeterministicRandom(rng.Below(int.MaxValue));

            hasPodium = false;
            form = BuildingForm.Box;
            double rh = height / heightScale;

            bool tall = mass == BuildingMass.Tower || mass == BuildingMass.Supertall;
            bool plainZone = zone == ZoneKind.Industrial || zone == ZoneKind.Outskirts;

            // Sculpted towers, instead of the classic podium and setbacks: rarely twisted, wonky or cantilevered (OddStackChance), more often round.
            bool canSculpt = !plainZone && (tall || mass == BuildingMass.MidRise && rh >= 40.0 || mass == BuildingMass.Slab && rh >= 45.0);
            double oddChance = canSculpt ? OddStackChance : 0.0;
            double roundChance = plainZone ? 0.0 : tall ? 0.13 : (mass == BuildingMass.MidRise && rh >= 40.0 ? 0.07 : 0.0);
            double sculptChance = oddChance + roundChance;
            if (formRoll < sculptChance)
            {
                List<MassVolume> sculpted = null;
                if (formRoll >= oddChance)
                {
                    sculpted = Round(width, depth, height, shapeRng, scale);
                    form = BuildingForm.Round;
                }
                else if (pickRoll < 0.40)
                {
                    sculpted = Twisted(width, depth, height, shapeRng, scale);
                    form = BuildingForm.Twisted;
                }
                else if (pickRoll < 0.70)
                {
                    sculpted = Wonky(width, depth, height, shapeRng, scale);
                    form = BuildingForm.Wonky;
                }
                else
                {
                    sculpted = Cantilever(width, depth, height, shapeRng, scale);
                    form = BuildingForm.Cantilever;
                }
                if (sculpted != null)
                {
                    return sculpted;
                }
                form = BuildingForm.Box;
            }

            // Footprints with wings: low and mid-rise blocks of the city proper.
            bool canWing = !tall && zone != ZoneKind.Industrial && mass != BuildingMass.Warehouse;
            double wingChance = !canWing ? 0.0 : (zone == ZoneKind.Outskirts ? 0.25 : 0.48);
            if (formRoll >= sculptChance && formRoll < sculptChance + wingChance * (1.0 - sculptChance))
            {
                List<MassVolume> winged = null;
                if (pickRoll < 0.33)
                {
                    winged = LShape(width, depth, height, shapeRng, scale);
                    form = BuildingForm.LShape;
                }
                else if (pickRoll < 0.48)
                {
                    winged = TShape(width, depth, height, shapeRng, scale);
                    form = BuildingForm.TShape;
                }
                else if (pickRoll < 0.63)
                {
                    winged = UShape(width, depth, height, shapeRng, scale);
                    form = BuildingForm.UShape;
                }
                else
                {
                    winged = Staggered(width, depth, height, shapeRng, scale);
                    form = BuildingForm.Staggered;
                }
                if (winged != null)
                {
                    return winged;
                }
                form = BuildingForm.Box;
            }

            List<MassVolume> stack = Split(zone, mass, width, depth, height, rng, out hasPodium, scale, heightScale);
            for (int i = 1; i < stack.Count; i++)
            {
                stack[i] = stack[i].StandingOn(i - 1);
            }
            form = stack.Count > 1 ? BuildingForm.Stepped : BuildingForm.Box;
            return stack;
        }

        /// <summary>Whole storeys, at least two, at most <paramref name="max"/>.</summary>
        private static double Storeys(double metres, double max)
        {
            double h = Math.Floor(metres / Storey) * Storey;
            return Math.Min(Math.Max(h, MinHeight), max);
        }

        /// <summary>The bar along the street that a wing hangs off: full width, part of the depth. Null when the lot is too shallow for a wing behind it.</summary>
        private static bool FrontBar(double width, double depth, DeterministicRandom rng, double scale, out double barDepth)
        {
            double min = MinSide * scale;
            barDepth = Math.Max(min, depth * (0.40 + 0.20 * rng.NextDouble()));
            return depth - barDepth >= min && width >= 2.0 * min;
        }

        /// <summary>A wing: a box in the lot frame turned so its front faces <paramref name="faceX"/> (+1 lot +X, -1 lot -X).</summary>
        private static MassVolume SideFacingWing(double x0, double x1, double z0, double z1, double height, int faceX)
        {
            // own +Z = lot (faceX, 0)  =>  own +X = (0, -faceX); own width runs along lot z, own depth along lot x.
            return new MassVolume((x0 + x1) * 0.5, (z0 + z1) * 0.5, z1 - z0, x1 - x0, height, 0.0, -faceX, -1, false, true);
        }

        private static List<MassVolume> LShape(double width, double depth, double height, DeterministicRandom rng, double scale)
        {
            if (!FrontBar(width, depth, rng, scale, out double bar))
            {
                return null;
            }
            double min = MinSide * scale;
            double wing = Math.Max(min, width * (0.30 + 0.18 * rng.NextDouble()));
            if (width - wing < MinYard * scale)
            {
                return null;
            }
            bool left = rng.NextDouble() < 0.5;
            double hx = width * 0.5, hz = depth * 0.5;
            double wingHeight = Storeys(height * (0.45 + 0.55 * rng.NextDouble()), height);
            var list = new List<MassVolume> { new MassVolume(-hx, hx, hz - bar, hz, height) };
            list.Add(left
                ? SideFacingWing(-hx, -hx + wing, -hz, hz - bar, wingHeight, +1)
                : SideFacingWing(hx - wing, hx, -hz, hz - bar, wingHeight, -1));
            return list;
        }

        private static List<MassVolume> TShape(double width, double depth, double height, DeterministicRandom rng, double scale)
        {
            if (!FrontBar(width, depth, rng, scale, out double bar))
            {
                return null;
            }
            double min = MinSide * scale;
            double wing = Math.Max(min, width * (0.28 + 0.14 * rng.NextDouble()));
            if ((width - wing) * 0.5 < MinYard * scale)
            {
                return null;
            }
            double hx = width * 0.5, hz = depth * 0.5;
            double offset = (width - wing) * (rng.NextDouble() - 0.5) * 0.5;
            double wingHeight = Storeys(height * (0.50 + 0.50 * rng.NextDouble()), height);
            var list = new List<MassVolume> { new MassVolume(-hx, hx, hz - bar, hz, height) };
            list.Add(SideFacingWing(offset - wing * 0.5, offset + wing * 0.5, -hz, hz - bar, wingHeight, rng.NextDouble() < 0.5 ? -1 : +1));
            return list;
        }

        private static List<MassVolume> UShape(double width, double depth, double height, DeterministicRandom rng, double scale)
        {
            if (!FrontBar(width, depth, rng, scale, out double bar))
            {
                return null;
            }
            double min = MinSide * scale;
            double wing = Math.Max(min, width * (0.22 + 0.10 * rng.NextDouble()));
            if (width - 2.0 * wing < MinYard * scale * 1.3)
            {
                return null;
            }
            double hx = width * 0.5, hz = depth * 0.5;
            double wingHeight = Storeys(height * (0.55 + 0.45 * rng.NextDouble()), height);
            var list = new List<MassVolume> { new MassVolume(-hx, hx, hz - bar, hz, height) };
            list.Add(SideFacingWing(-hx, -hx + wing, -hz, hz - bar, wingHeight, +1));
            list.Add(SideFacingWing(hx - wing, hx, -hz, hz - bar, wingHeight, -1));
            return list;
        }

        /// <summary>Two blocks side by side along the street, the second set back and a different height: a row that steps.</summary>
        private static List<MassVolume> Staggered(double width, double depth, double height, DeterministicRandom rng, double scale)
        {
            double min = MinSide * scale;
            if (width < 2.0 * min)
            {
                return null;
            }
            double split = Math.Min(Math.Max(width * (0.35 + 0.30 * rng.NextDouble()), min), width - min);
            double setback = (2.5 + 5.5 * rng.NextDouble()) * scale;
            double backDepth = depth - setback;
            if (backDepth < min)
            {
                return null;
            }
            double hx = width * 0.5, hz = depth * 0.5;
            bool leftForward = rng.NextDouble() < 0.5;
            double otherHeight = Storeys(height * (0.45 + 0.75 * rng.NextDouble()), height * 1.25);
            double xSplit = -hx + split;
            var forward = leftForward ? new MassVolume(-hx, xSplit, -hz, hz, height) : new MassVolume(xSplit, hx, -hz, hz, height);
            var back = leftForward
                ? new MassVolume(xSplit, hx, -hz, hz - setback, otherHeight)
                : new MassVolume(-hx, xSplit, -hz, hz - setback, otherHeight);
            back = new MassVolume(back.CentreX, back.CentreZ, back.Width, back.Depth, back.Height, 1.0, 0.0, -1, false, true);
            return new List<MassVolume> { forward, back };
        }

        // ------------------------------------------------------------------ sculpted towers

        /// <summary>
        /// The base you walk into, then stages above it, each turned a few degrees further than the one below. Each stage is shrunk just enough that
        /// its turned corners stay inside the lot, so the tower tapers as it twists. Stages are sealed (Shell): they overhang the one below at the corners.
        /// </summary>
        private static List<MassVolume> Twisted(double width, double depth, double height, DeterministicRandom rng, double scale)
        {
            double min = MinSide * scale;
            double baseHeight = Storeys(height * (0.18 + 0.12 * rng.NextDouble()), height - MinHeight);
            double rest = height - baseHeight;
            int stages = (int)Math.Max(3.0, Math.Min(16.0, Math.Floor(rest / (Storey * (1 + rng.Below(2))))));
            double stageHeight = Math.Floor(rest / stages / Storey) * Storey;
            if (baseHeight < MinHeight || stageHeight < Storey)
            {
                return null;
            }
            double hx = width * 0.5, hz = depth * 0.5;
            // Square-ish stages read as a twist; a long slab only looks bent.
            double side = Math.Min(width, depth) * (0.80 + 0.12 * rng.NextDouble());
            double sideD = side * (0.82 + 0.18 * rng.NextDouble());
            double t = TwistTangents[rng.Below(TwistTangents.Length)];
            double norm = Math.Sqrt(1.0 + t * t);
            double stepC = 1.0 / norm, stepS = (rng.NextDouble() < 0.5 ? t : -t) / norm;

            var list = new List<MassVolume> { new MassVolume(-hx, hx, -hz, hz, baseHeight) };
            double c = 1.0, s = 0.0;
            double used = baseHeight;
            for (int i = 0; i < stages; i++)
            {
                double nc = c * stepC - s * stepS;
                double ns = c * stepS + s * stepC;
                c = nc;
                s = ns;
                double h = i == stages - 1 ? height - used : stageHeight;
                double w = side, d = sideD;
                // Shrink until the turned stage fits the lot (its axis-aligned box inside the rectangle).
                double ac = Math.Abs(c), asn = Math.Abs(s);
                double fitX = hx / (w * 0.5 * ac + d * 0.5 * asn);
                double fitZ = hz / (w * 0.5 * asn + d * 0.5 * ac);
                double k = Math.Min(1.0, Math.Min(fitX, fitZ)) * (1.0 - 0.012 * i);
                w *= k;
                d *= k;
                if (w < min * 0.8 || d < min * 0.8 || h < Storey)
                {
                    break;
                }
                list.Add(new MassVolume(0.0, 0.0, w, d, h, c, s, list.Count - 1, true, false));
                used += h;
            }
            if (list.Count >= 3 && height - used > 0.01)
            {
                MassVolume last = list[list.Count - 1];
                list[list.Count - 1] = new MassVolume(last.CentreX, last.CentreZ, last.Width, last.Depth, last.Height + (height - used),
                    last.TurnX, last.TurnZ, last.Support, last.Shell, last.Wing, last.Sides);
            }
            return list.Count >= 3 ? list : null;
        }

        /// <summary>
        /// A pile: the base you walk into, then blocks of two to four storeys, each turned a little off the lot and pushed a few metres off centre, so
        /// the silhouette leans and juts -- Kowloon Walled City in a single tower. Every block stays inside the lot and has its centre over the block below.
        /// </summary>
        private static List<MassVolume> Wonky(double width, double depth, double height, DeterministicRandom rng, double scale)
        {
            double min = MinSide * scale;
            double hx = width * 0.5, hz = depth * 0.5;
            double baseHeight = Storeys(height * (0.15 + 0.15 * rng.NextDouble()), height - MinHeight);
            if (baseHeight < MinHeight)
            {
                return null;
            }
            var list = new List<MassVolume> { new MassVolume(-hx, hx, -hz, hz, baseHeight) };
            double used = baseHeight;
            double[] xs = new double[4], zs = new double[4];
            for (int guard = 0; guard < 24 && height - used >= MinHeight; guard++)
            {
                MassVolume below = list[list.Count - 1];
                double storeys = 2 + rng.Below(3);
                double h = Math.Min(storeys * Storey, height - used);
                if (height - used - h < MinHeight)
                {
                    h = height - used;   // the last block takes what is left rather than leave a sliver
                }
                double t = WonkyTangents[rng.Below(WonkyTangents.Length)];
                double norm = Math.Sqrt(1.0 + t * t);
                double c = 1.0 / norm, s = t / norm;
                double w = Math.Max(min, below.Width * (0.78 + 0.30 * rng.NextDouble()));
                double d = Math.Max(min, below.Depth * (0.78 + 0.30 * rng.NextDouble()));
                if (below.Turned)
                {
                    // Measured in the lot frame, the block below is no longer its own width wide.
                    w = Math.Max(min, Math.Min(width, depth) * (0.55 + 0.25 * rng.NextDouble()));
                    d = Math.Max(min, Math.Min(width, depth) * (0.55 + 0.25 * rng.NextDouble()));
                }
                double cx = below.CentreX + (rng.NextDouble() - 0.5) * 6.0 * scale;
                double cz = below.CentreZ + (rng.NextDouble() - 0.5) * 6.0 * scale;

                var block = new MassVolume(cx, cz, w, d, h, c, s, list.Count - 1, true, false);
                if (!FitInLot(ref block, hx, hz, min, xs, zs) || !CentreOver(block, below, 2.0 * scale))
                {
                    continue;
                }
                list.Add(block);
                used += h;
            }
            if (height - used > 0.01)
            {
                // Could not place the rest: the last block keeps growing instead.
                MassVolume last = list[list.Count - 1];
                list[list.Count - 1] = new MassVolume(last.CentreX, last.CentreZ, last.Width, last.Depth, last.Height + (height - used),
                    last.TurnX, last.TurnZ, last.Support, last.Shell, last.Wing, last.Sides);
            }
            return list.Count >= 3 ? list : null;
        }

        /// <summary>
        /// A shaft you walk into, and on top of it one big block turned square across it or pushed to one side, hanging out past the shaft over the
        /// yard or the street: the block-on-a-stick of a lot of modern towers. The overhang has an underside you can see from below.
        /// </summary>
        private static List<MassVolume> Cantilever(double width, double depth, double height, DeterministicRandom rng, double scale)
        {
            double min = MinSide * scale;
            double hx = width * 0.5, hz = depth * 0.5;
            double topHeight = Storeys(height * (0.18 + 0.14 * rng.NextDouble()), height - MinHeight);
            double shaftHeight = height - topHeight;
            if (shaftHeight < MinHeight || topHeight < MinHeight)
            {
                return null;
            }
            // The shaft is narrower than the lot so the block above has somewhere to hang out to.
            double shaftW = Math.Min(width, Math.Max(min, width * (0.45 + 0.15 * rng.NextDouble())));
            double shaftD = Math.Min(depth, Math.Max(min, depth * (0.45 + 0.15 * rng.NextDouble())));
            double shaftX = (width - shaftW) * (rng.NextDouble() - 0.5);
            double shaftZ = (depth - shaftD) * (rng.NextDouble() - 0.5);
            var shaft = new MassVolume(shaftX - shaftW * 0.5, shaftX + shaftW * 0.5, shaftZ - shaftD * 0.5, shaftZ + shaftD * 0.5, shaftHeight);

            MassVolume block;
            double[] xs = new double[4], zs = new double[4];
            if (rng.NextDouble() < 0.5)
            {
                // Across: a long bar at right angles to the shaft's longer side, reaching most of the lot.
                bool alongX = width >= depth;
                double length = (alongX ? width : depth) * (0.85 + 0.15 * rng.NextDouble());
                double thick = Math.Max(min, Math.Min(shaftW, shaftD) * (0.80 + 0.25 * rng.NextDouble()));
                // Centred between the shaft and the middle of the lot, so it hangs out further on the open side.
                block = alongX
                    ? new MassVolume(shaftX * 0.4, shaftZ, length, thick, topHeight, 1.0, 0.0, 0, true, false)
                    : new MassVolume(shaftX, shaftZ * 0.4, thick, length, topHeight, 1.0, 0.0, 0, true, false);
            }
            else
            {
                // Pushed off: the same size as the shaft or bigger, slid toward the open side of the lot.
                double w = Math.Max(min, shaftW * (1.0 + 0.4 * rng.NextDouble()));
                double d = Math.Max(min, shaftD * (1.0 + 0.4 * rng.NextDouble()));
                double cx = shaftX + (shaftX <= 0.0 ? 1.0 : -1.0) * shaftW * (0.15 + 0.20 * rng.NextDouble());
                double cz = shaftZ + (shaftZ <= 0.0 ? 1.0 : -1.0) * shaftD * (0.05 + 0.15 * rng.NextDouble());
                block = new MassVolume(cx, cz, w, d, topHeight, 1.0, 0.0, 0, true, false);
            }
            if (!FitInLot(ref block, hx, hz, min, xs, zs) || !CentreOver(block, shaft, 1.0 * scale))
            {
                return null;
            }
            return new List<MassVolume> { shaft, block };
        }

        /// <summary>How many sides a round tower may have: a hexagon, an octagon, and twelve to twenty for a tower that reads as round.</summary>
        private static readonly int[] RoundSides = { 6, 8, 12, 16, 16, 20 };

        /// <summary>
        /// A round or many-sided tower: one volume whose outline is a regular polygon (MassVolume.Sides) inside the lot, stood toward the street so its door
        /// is near the pavement. Half of them carry one or two narrower round crowns, sealed, on top.
        /// </summary>
        private static List<MassVolume> Round(double width, double depth, double height, DeterministicRandom rng, double scale)
        {
            double min = MinSide * scale;
            int sides = RoundSides[rng.Below(RoundSides.Length)];
            double r = Math.Min(width, depth) * 0.5 * (0.86 + 0.14 * rng.NextDouble());
            if (r * 2.0 < min * 1.8)
            {
                return null;   // too small to be a round tower with a stair inside
            }
            double cz = depth * 0.5 - r;
            var list = new List<MassVolume> { new MassVolume(0.0, cz, r * 2.0, r * 2.0, height, 1.0, 0.0, -1, false, false, sides) };
            if (rng.NextDouble() < 0.5 && height >= 3.0 * MinHeight)
            {
                double baseHeight = Storeys(height * (0.62 + 0.18 * rng.NextDouble()), height - MinHeight);
                list[0] = new MassVolume(0.0, cz, r * 2.0, r * 2.0, baseHeight, 1.0, 0.0, -1, false, false, sides);
                double used = baseHeight;
                int crowns = height - baseHeight >= 2.0 * MinHeight && rng.NextDouble() < 0.5 ? 2 : 1;
                double cr = r;
                for (int i = 0; i < crowns; i++)
                {
                    cr *= 0.62 + 0.16 * rng.NextDouble();
                    if (cr * 2.0 < min)
                    {
                        break;
                    }
                    double h = i == crowns - 1 ? height - used : Storeys((height - used) * 0.55, height - used - MinHeight);
                    list.Add(new MassVolume(0.0, cz, cr * 2.0, cr * 2.0, h, 1.0, 0.0, list.Count - 1, true, false, sides));
                    used += h;
                }
                if (height - used > 0.01)
                {
                    MassVolume last = list[list.Count - 1];
                    list[list.Count - 1] = new MassVolume(last.CentreX, last.CentreZ, last.Width, last.Depth, last.Height + (height - used),
                        last.TurnX, last.TurnZ, last.Support, last.Shell, last.Wing, last.Sides);
                }
            }
            return list;
        }

        /// <summary>Shrinks (then slides) a turned block until its corners are all inside the lot rectangle. False if it would have to be too small.</summary>
        private static bool FitInLot(ref MassVolume block, double hx, double hz, double min, double[] xs, double[] zs)
        {
            double ac = Math.Abs(block.TurnX), asn = Math.Abs(block.TurnZ);
            double ex = block.Width * 0.5 * ac + block.Depth * 0.5 * asn;
            double ez = block.Width * 0.5 * asn + block.Depth * 0.5 * ac;
            double k = Math.Min(1.0, Math.Min(hx / ex, hz / ez));
            double w = block.Width * k, d = block.Depth * k;
            if (w < min * 0.8 || d < min * 0.8)
            {
                return false;
            }
            ex *= k;
            ez *= k;
            double cx = Math.Min(Math.Max(block.CentreX, -hx + ex), hx - ex);
            double cz = Math.Min(Math.Max(block.CentreZ, -hz + ez), hz - ez);
            block = new MassVolume(cx, cz, w, d, block.Height, block.TurnX, block.TurnZ, block.Support, block.Shell, block.Wing, block.Sides);
            block.Corners(xs, zs);
            for (int i = 0; i < 4; i++)
            {
                if (xs[i] < -hx - 1e-6 || xs[i] > hx + 1e-6 || zs[i] < -hz - 1e-6 || zs[i] > hz + 1e-6)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Whether a block's centre is over the block below it, at least <paramref name="inset"/> in from its edges: it stands on it, not beside it.</summary>
        public static bool CentreOver(MassVolume block, MassVolume below, double inset)
        {
            below.ToOwn(block.CentreX, block.CentreZ, out double ox, out double oz);
            return Math.Abs(ox) <= below.Width * 0.5 - inset && Math.Abs(oz) <= below.Depth * 0.5 - inset;
        }
    }
}
