using System;
using CityX.World.Plan;

namespace CityX.World
{
    public enum SignalLamp : byte
    {
        Red = 0,
        Yellow = 1,
        Green = 2,
    }

    public enum WalkLamp : byte
    {
        /// <summary>The steady orange hand: do not step off.</summary>
        Hand = 0,

        /// <summary>The white walking figure.</summary>
        Walk = 1,

        /// <summary>The flashing orange hand: do not start, finish crossing.</summary>
        FlashingHand = 2,
    }

    /// <summary>A stretch of a signal's cycle: from <see cref="Start"/> seconds into it, for <see cref="Length"/>, running on round the cycle's end.</summary>
    public readonly struct SignalWindow
    {
        public readonly double Start, Length;

        public SignalWindow(double start, double length)
        {
            Start = start;
            Length = length;
        }

        public bool Holds(double phase, double cycle) => SignalTiming.Mod(phase - Start, cycle) < Length;
    }

    /// <summary>
    /// ONE JUNCTION'S LIGHTS, TIMED AS AMERICAN CITIES TIME THEM (docs/TRAFFIC_CONTROL.md; owner, 2026-11-05: "realistically time the traffic lights like how
    /// they are in american cities"). Fixed time, as downtowns run them: the main street's green, yellow and an all-red, then the side street's. A light is
    /// a pure function of the shared clock, so every machine shows the same one with nothing sent.
    ///
    ///  - Cycle: 90 s on the main roads, 60 s where two side streets cross (NACTO: 60 to 90 s is the urban ideal). Every cycle divides an hour.
    ///  - Yellow: 3 s on a street, 3.5 on an arterial, 4 on a boulevard (MUTCD 4D.26: 3 to 6 s, longer for faster roads). All-red: 1 to 2 s by the width cleared.
    ///  - Greens share what is left by the roads' classes, neither shorter than a person needs to cross the other road: 7 s of WALK (MUTCD 4E.06) and
    ///    the crossing at 3.5 ft/s.
    ///  - WALK runs with the green alongside; the hand flashes for the crossing time up to that green's end, and the yellow and all-red are its buffer.
    ///  - Offset: the main street's green starts later the further down the road the junction stands, at the road's speed, so a car at that speed
    ///    meets green after green (one way down it, as a two-way street's wave must choose).
    ///  - Late at night the lights of a side street's crossing flash: yellow for the main road, red for the side street (MUTCD 4D.30), once a second.
    /// </summary>
    public sealed class SignalTiming
    {
        public const double MainRoadCycle = 90.0;
        public const double SideStreetCycle = 60.0;

        /// <summary>Every cycle divides this, so a clock wrapped at it never jumps a light.</summary>
        public const double ClockWrap = 3600.0;

        public const double WalkSeconds = 7.0;

        /// <summary>3.5 ft/s at the city's 1.3 times life.</summary>
        public const double CrossingSpeed = 1.39;

        /// <summary>Flashes a second in flashing operation (MUTCD 4D.28: 50 to 60 a minute), lit for half of each.</summary>
        public const double FlashHz = 1.0;

        public double Cycle;

        /// <summary>The cycle is this far on when the shared clock reads zero.</summary>
        public double Offset;

        public double GreenMajor, YellowMajor, AllRedMajor;
        public double GreenMinor, YellowMinor, AllRedMinor;

        /// <summary>How long the hand flashes for those crossing the side street (with the main street's green) and the main street (with the side street's).</summary>
        public double ClearSideCrossing, ClearMainCrossing;

        /// <summary>Whether these lights go to flashing late at night.</summary>
        public bool NightFlash;

        public double MinorStart => GreenMajor + YellowMajor + AllRedMajor;

        public static double Mod(double value, double modulus)
        {
            double r = value % modulus;
            return r < 0.0 ? r + modulus : r;
        }

        /// <summary>How far into its cycle the junction is at a reading of the shared clock.</summary>
        public double PhaseAt(double clock) => Mod(clock + Offset, Cycle);

        /// <summary>When in the cycle a lamp facing the main street's traffic, or the side street's, is lit.</summary>
        public SignalWindow Vehicle(bool major, SignalLamp lamp)
        {
            double start = major ? 0.0 : MinorStart;
            double green = major ? GreenMajor : GreenMinor;
            double yellow = major ? YellowMajor : YellowMinor;
            switch (lamp)
            {
                case SignalLamp.Green: return new SignalWindow(start, green);
                case SignalLamp.Yellow: return new SignalWindow(start + green, yellow);
                default: return new SignalWindow(Mod(start + green + yellow, Cycle), Cycle - green - yellow);
            }
        }

        public SignalLamp VehicleAt(bool major, double clock)
        {
            double phase = PhaseAt(clock);
            if (Vehicle(major, SignalLamp.Green).Holds(phase, Cycle)) return SignalLamp.Green;
            return Vehicle(major, SignalLamp.Yellow).Holds(phase, Cycle) ? SignalLamp.Yellow : SignalLamp.Red;
        }

        /// <summary>When in the cycle a walk signal's lamp is lit, for those crossing the main street or the side street. The flashing hand's window is when it flashes.</summary>
        public SignalWindow Pedestrian(bool crossesMain, WalkLamp lamp)
        {
            // Crossing the main street goes with the side street's green, and the other way round.
            double start = crossesMain ? MinorStart : 0.0;
            double green = crossesMain ? GreenMinor : GreenMajor;
            double clear = crossesMain ? ClearMainCrossing : ClearSideCrossing;
            switch (lamp)
            {
                case WalkLamp.Walk: return new SignalWindow(start, green - clear);
                case WalkLamp.FlashingHand: return new SignalWindow(start + green - clear, clear);
                default: return new SignalWindow(Mod(start + green, Cycle), Cycle - green);
            }
        }

        public WalkLamp PedestrianAt(bool crossesMain, double clock)
        {
            double phase = PhaseAt(clock);
            if (Pedestrian(crossesMain, WalkLamp.Walk).Holds(phase, Cycle)) return WalkLamp.Walk;
            return Pedestrian(crossesMain, WalkLamp.FlashingHand).Holds(phase, Cycle) ? WalkLamp.FlashingHand : WalkLamp.Hand;
        }

        /// <summary>Whether a flashing lamp is in the lit half of its flash at a reading of the clock.</summary>
        public static bool FlashLit(double clock) => Mod(clock * FlashHz, 1.0) < 0.5;

        public static double YellowFor(RoadClass road) => road == RoadClass.Boulevard ? 4.0 : road == RoadClass.Arterial ? 3.5 : 3.0;

        /// <summary>The all-red after a green, by the width of the road its last cars are still crossing: about that width at the road's speed, 1 to 2 s.</summary>
        public static double AllRedFor(double widthCleared) => Math.Max(1.0, Math.Min(2.0, Math.Round(widthCleared / 13.0 * 2.0) / 2.0));

        /// <summary>The speed a road's green wave travels at, m/s: 25, 30 and 35 mph at the city's 1.3 times life.</summary>
        public static double ProgressionSpeed(RoadClass road) => road == RoadClass.Boulevard ? 20.3 : road == RoadClass.Arterial ? 17.4 : 14.5;

        private static double Weight(RoadClass road) => 1.0 + 0.6 * (int)road;

        /// <summary>The timing of a junction's lights, standing <paramref name="alongItsRoad"/> metres down its main street from the first lights on it (TrafficPlan).</summary>
        public static SignalTiming For(TrafficJunction junction, double alongItsRoad)
        {
            RoadClass main = junction.ClassOf(true), side = junction.ClassOf(false);
            double mainWidth = junction.WidthOf(true), sideWidth = junction.WidthOf(false);
            var timing = new SignalTiming
            {
                Cycle = main == RoadClass.Street ? SideStreetCycle : MainRoadCycle,
                YellowMajor = YellowFor(main),
                YellowMinor = YellowFor(side),
                AllRedMajor = AllRedFor(sideWidth),
                AllRedMinor = AllRedFor(mainWidth),
                NightFlash = side == RoadClass.Street,
            };

            // What is left of the cycle after the yellows and all-reds, shared by the roads' classes; neither green shorter than its walkers need.
            double greens = timing.Cycle - timing.YellowMajor - timing.AllRedMajor - timing.YellowMinor - timing.AllRedMinor;
            timing.ClearSideCrossing = Math.Ceiling(sideWidth / CrossingSpeed);
            timing.ClearMainCrossing = Math.Ceiling(mainWidth / CrossingSpeed);
            double leastMajor = WalkSeconds + timing.ClearSideCrossing, leastMinor = WalkSeconds + timing.ClearMainCrossing;
            // Rounded up, so that of two roads of one class the main street's green is never the shorter.
            double major = Math.Ceiling(greens * Weight(main) / (Weight(main) + Weight(side)));
            major = Math.Min(major, greens - leastMinor);
            major = Math.Max(major, Math.Min(leastMajor, greens * 0.5));
            timing.GreenMajor = major;
            timing.GreenMinor = greens - major;
            timing.ClearSideCrossing = Math.Min(timing.ClearSideCrossing, timing.GreenMajor - WalkSeconds);
            timing.ClearMainCrossing = Math.Min(timing.ClearMainCrossing, timing.GreenMinor - WalkSeconds);

            // The green wave: the main street's green starts as a car at the road's speed, coming down the road, arrives.
            timing.Offset = Mod(-alongItsRoad / ProgressionSpeed(main), timing.Cycle);
            return timing;
        }
    }
}
