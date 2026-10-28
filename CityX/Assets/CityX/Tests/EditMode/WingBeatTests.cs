using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The wings' rhythm (docs/WINGS.md): the beat follows what the body does -- slow hovering, quick climbing, few gliding at speed, more
    /// while speed is gained -- a take-off starts a downstroke at once, a climb begun mid-upstroke hurries the upstroke, the downstroke is the
    /// quicker half, and on the ground the wings fold and only breathe.
    /// </summary>
    public sealed class WingBeatTests
    {
        private const float Step = 1.0f / 60.0f;

        private static WingBeatInput Flying(bool climbing = false, bool descending = false, float speed = 0.0f, float acceleration = 0.0f) =>
            new WingBeatInput { Flying = true, Climbing = climbing, Descending = descending, SpeedFraction = speed, ForwardAcceleration = acceleration };

        private static float BeatsASecond(WingBeat beat, WingBeatInput input)
        {
            int downstrokes = 0;
            for (int i = 0; i < 600; i++)
            {
                beat.Tick(input, Step);
                if (beat.DownstrokeBegan) downstrokes++;
            }
            return downstrokes / 10.0f;
        }

        [Test]
        public void TheBeatIsSlowHoveringQuickClimbingAndAlmostStillGlidingAtSpeed()
        {
            var beat = new WingBeat();
            beat.Tick(new WingBeatInput { Flying = true, TookOff = true }, Step);
            float hover = BeatsASecond(beat, Flying());
            float climb = BeatsASecond(beat, Flying(climbing: true));
            float glide = BeatsASecond(beat, Flying(speed: 1.0f));
            float descend = BeatsASecond(beat, Flying(descending: true));
            Assert.AreEqual(WingBeat.HoverHz, hover, 0.15f, "hovering");
            Assert.AreEqual(WingBeat.ClimbHz, climb, 0.15f, "climbing");
            Assert.AreEqual(WingBeat.GlideHz, glide, 0.15f, "gliding");
            Assert.Greater(climb, hover, "a climb should beat quicker than a hover");
            Assert.Less(glide, hover * 0.4f, "a glide should all but stop beating");
            Assert.Less(descend, hover, "a descent beats less than a hover");
        }

        [Test]
        public void GainingSpeedAddsBeatsAndAGlideSweepsTheWingsBack()
        {
            var beat = new WingBeat();
            beat.Tick(new WingBeatInput { Flying = true, TookOff = true }, Step);
            float steady = BeatsASecond(beat, Flying(speed: 0.5f));
            float accelerating = BeatsASecond(beat, Flying(speed: 0.5f, acceleration: 8.0f));
            Assert.Greater(accelerating, steady + 0.5f, "working up to speed should add beats");
            for (int i = 0; i < 120; i++) beat.Tick(Flying(speed: 1.0f), Step);
            Assert.Greater(beat.Glide, 0.95f, "at speed the wings should be swept into a glide");
            Assert.Less(beat.Strength, WingBeat.HoverStrength, "a glide's beats are shallow");
            for (int i = 0; i < 120; i++) beat.Tick(Flying(climbing: true, speed: 1.0f), Step);
            Assert.Less(beat.Glide, 0.05f, "a climb un-sweeps them");
        }

        [Test]
        public void ATakeoffStartsAStrongDownstrokeAtOnceAndTheWingsOpen()
        {
            var beat = new WingBeat();
            for (int i = 0; i < 60; i++) beat.Tick(default, Step);
            Assert.AreEqual(1.0f, beat.Fold, 1e-3f, "on the ground the wings are folded");
            Assert.Less(Mathf.Abs(beat.Angle), 3.0f, "folded they only breathe");
            for (int i = 0; i < 20; i++) beat.Tick(Flying(), Step);   // a phase somewhere mid-cycle before the take-off
            beat.Tick(new WingBeatInput { Flying = true, TookOff = true }, Step);
            Assert.Less(beat.Phase, 0.05f, "the take-off should begin a downstroke");
            Assert.IsTrue(beat.DownstrokeBegan);
            Assert.AreEqual(WingBeat.TakeoffStrength, beat.Strength, 0.05f, "the first beat is the strongest");
            for (int i = 0; i < 30; i++) beat.Tick(Flying(), Step);
            Assert.AreEqual(0.0f, beat.Fold, 1e-3f, "the wings should be open within a quarter second");
        }

        [Test]
        public void AClimbBegunMidUpstrokeHurriesTheUpstrokeSoItsDownstrokeComesWithTheKey()
        {
            var slow = new WingBeat();
            var hurried = new WingBeat();
            slow.Tick(new WingBeatInput { Flying = true, TookOff = true }, Step);
            hurried.Tick(new WingBeatInput { Flying = true, TookOff = true }, Step);
            // Into the upstroke (past the downstroke's share of the cycle), hovering.
            while (slow.Phase < 0.6f)
            {
                slow.Tick(Flying(), Step);
                hurried.Tick(Flying(), Step);
            }
            int ticksToDownstroke = 0;
            WingBeatInput climbBegins = Flying(climbing: true);
            climbBegins.ClimbBegan = true;
            hurried.Tick(climbBegins, Step);
            while (!hurried.DownstrokeBegan && ticksToDownstroke < 120)
            {
                hurried.Tick(Flying(climbing: true), Step);
                ticksToDownstroke++;
            }
            int ticksUnhurried = 0;
            while (!slow.DownstrokeBegan && ticksUnhurried < 120)
            {
                slow.Tick(Flying(), Step);
                ticksUnhurried++;
            }
            Assert.Less(ticksToDownstroke, ticksUnhurried / 2, "the hurried upstroke should finish much sooner");
            Assert.Less(ticksToDownstroke * Step, 0.15f, "the first climbing downstroke should come within 0.15 s of the key");
        }

        [Test]
        public void TheDownstrokeIsTheQuickerHalfAndLiftsOnlyOnTheWayDown()
        {
            Assert.AreEqual(1.0f, WingBeat.Stroke(0.0f), 1e-4f, "the top");
            Assert.AreEqual(-1.0f, WingBeat.Stroke(WingBeat.DownstrokeShare), 1e-4f, "the bottom, early in the cycle");
            Assert.AreEqual(1.0f, WingBeat.Stroke(1.0f), 1e-4f);
            Assert.Less(WingBeat.DownstrokeShare, 0.5f);
            Assert.Greater(WingBeat.LiftAt(WingBeat.DownstrokeShare * 0.5f), 0.99f, "most lift mid-downstroke");
            Assert.AreEqual(0.0f, WingBeat.LiftAt(0.8f), 1e-4f, "none on the upstroke");
        }

        [Test]
        public void DescendingTucksTheWingsAndLandingFoldsThem()
        {
            var beat = new WingBeat();
            beat.Tick(new WingBeatInput { Flying = true, TookOff = true }, Step);
            for (int i = 0; i < 60; i++) beat.Tick(Flying(descending: true), Step);
            Assert.Greater(beat.Tuck, 0.95f);
            for (int i = 0; i < 60; i++) beat.Tick(default, Step);
            Assert.Greater(beat.Fold, 0.95f, "landed, the wings should fold within the second");
            Assert.Less(beat.Tuck, 0.05f);
        }
    }
}
