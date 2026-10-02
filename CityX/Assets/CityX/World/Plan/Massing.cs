using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>One box of a building's massing, in the lot rectangle's own frame (x along the frontage, z out of the front).</summary>
    public readonly struct MassVolume
    {
        public readonly double X0, X1, Z0, Z1, Height;

        public MassVolume(double x0, double x1, double z0, double z1, double height)
        {
            X0 = x0;
            X1 = x1;
            Z0 = z0;
            Z1 = z1;
            Height = height;
        }

        public double Width => X1 - X0;
        public double Depth => Z1 - Z0;
        public double CentreX => (X0 + X1) * 0.5;
        public double CentreZ => (Z0 + Z1) * 0.5;
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
    /// </summary>
    public static class Massing
    {
        /// <summary>A storey: two window rows of 3.2 m. Volumes are whole storeys tall so floors and window rows keep lining up.</summary>
        public const double Storey = 6.4;

        /// <summary>The smallest a volume may be on a side, metres: smaller is a chimney, not a floor of a building.</summary>
        public const double MinSide = 9.0;

        /// <summary>The shortest a volume may be, metres: two storeys.</summary>
        public const double MinHeight = 2.0 * Storey;

        public static List<MassVolume> Split(ZoneKind zone, BuildingMass mass, double width, double depth, double height, DeterministicRandom rng,
            out bool hasPodium)
        {
            hasPodium = false;
            double hx = width * 0.5;
            double hz = depth * 0.5;
            var whole = new MassVolume(-hx, hx, -hz, hz, height);
            var volumes = new List<MassVolume>();

            bool tall = mass == BuildingMass.Tower || mass == BuildingMass.Supertall;
            double podiumChance = tall ? 0.85
                : (mass == BuildingMass.MidRise && height >= 44.0 && Math.Min(width, depth) >= 22.0 ? 0.35
                : (mass == BuildingMass.Slab && height >= 50.0 ? 0.20 : 0.0));
            if (zone == ZoneKind.Industrial || zone == ZoneKind.Outskirts)
            {
                podiumChance = 0.0;
            }
            double steppedChance = mass == BuildingMass.Supertall ? 1.0
                : (mass == BuildingMass.Tower ? 0.55
                : (mass == BuildingMass.MidRise && height >= 36.0 ? 0.30
                : (mass == BuildingMass.Slab ? 0.15 : 0.0)));
            if (zone == ZoneKind.Industrial)
            {
                steppedChance = 0.0;
            }

            double roll = rng.NextDouble();
            double stepRoll = rng.NextDouble();

            MassVolume top = whole;   // the volume the crown (if any) will stand on
            if (roll < podiumChance && TryPodiumAndTower(whole, height, rng, out MassVolume podium, out MassVolume tower))
            {
                volumes.Add(podium);
                top = tower;
                hasPodium = true;
            }

            if (stepRoll < steppedChance)
            {
                AddSteppedTower(volumes, top, rng, mass == BuildingMass.Supertall);
            }
            else
            {
                volumes.Add(top);
            }
            return volumes;
        }

        private static bool TryPodiumAndTower(MassVolume whole, double height, DeterministicRandom rng, out MassVolume podium, out MassVolume tower)
        {
            podium = default;
            tower = default;

            double storeys = 2 + rng.Below(4);                       // 2..5
            double podiumHeight = Math.Min(storeys * Storey, Math.Floor(height * 0.45 / Storey) * Storey);
            double towerHeight = height - podiumHeight;
            if (podiumHeight < MinHeight || towerHeight < MinHeight)
            {
                return false;
            }

            // The tower stands back from the street (+z) more than from the rest, and is rarely centred.
            double front = 2.0 + 6.0 * rng.NextDouble();
            double back = 1.0 + 5.0 * rng.NextDouble();
            double left = 1.5 + 5.5 * rng.NextDouble();
            double right = 1.5 + 5.5 * rng.NextDouble();
            for (int attempt = 0; attempt < 6; attempt++)
            {
                if (whole.Width - left - right >= MinSide && whole.Depth - front - back >= MinSide)
                {
                    break;
                }
                front *= 0.7;
                back *= 0.7;
                left *= 0.7;
                right *= 0.7;
            }
            if (whole.Width - left - right < MinSide || whole.Depth - front - back < MinSide)
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
        private static void AddSteppedTower(List<MassVolume> volumes, MassVolume shaft, DeterministicRandom rng, bool twice)
        {
            MassVolume current = shaft;
            double remaining = shaft.Height;
            int stages = twice ? 2 : 1;
            for (int stage = 0; stage < stages; stage++)
            {
                double share = 0.60 + 0.20 * rng.NextDouble();
                double lowerHeight = Math.Floor(remaining * share / Storey) * Storey;
                double upperHeight = remaining - lowerHeight;
                double left = 2.0 + 3.5 * rng.NextDouble();
                double right = 2.0 + 3.5 * rng.NextDouble();
                double front = 1.5 + 3.5 * rng.NextDouble();
                double back = 1.5 + 3.5 * rng.NextDouble();
                if (lowerHeight < MinHeight || upperHeight < MinHeight
                    || current.Width - left - right < MinSide || current.Depth - front - back < MinSide)
                {
                    break;
                }

                volumes.Add(new MassVolume(current.X0, current.X1, current.Z0, current.Z1, lowerHeight));
                current = new MassVolume(current.X0 + left, current.X1 - right, current.Z0 + back, current.Z1 - front, upperHeight);
                remaining = upperHeight;
            }
            volumes.Add(current);
        }
    }
}
