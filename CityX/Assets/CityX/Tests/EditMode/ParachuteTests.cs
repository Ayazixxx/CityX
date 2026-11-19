using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// THE PARACHUTE'S JUMP (owner, 2026-11-19: "press space to open it when your falling in mid air. make so it doesnt just activate off jumping
    /// and pressing space quickly ... you have to be falling in mid air for like 2 seconds before you can activate parachute ... research other
    /// games parachuting physics and implement what you learn"; docs/PARACHUTE.md), on the pure model ParachuteFlight: the two seconds of falling,
    /// the skydiver's terminal speed, the opening's brake, the canopy's glide, the turns sinking it, the flare, the landings, the cut-away; the
    /// canopy's shape and the rider's word to other players.
    /// </summary>
    public sealed class ParachuteTests
    {
        private const float Step = 1.0f / 60.0f;

        /// <summary>A body falling under plain gravity, for <paramref name="seconds"/>, from <paramref name="upward"/> m/s up, Space pressed at <paramref name="pressAt"/>.</summary>
        private static ParachuteFlight Fall(float seconds, float upward = 0.0f, float pressAt = -1.0f)
        {
            var flight = new ParachuteFlight();
            Vector3 velocity = Vector3.up * upward;
            for (float t = 0.0f; t < seconds; t += Step)
            {
                flight.Tick(Frame(velocity, pressAt >= 0.0f && t <= pressAt && pressAt < t + Step), Step);
                velocity = flight.DrivesBody ? flight.Velocity : velocity + Vector3.down * ParachuteFlight.Gravity * Step;
            }
            return flight;
        }

        private static ChuteInput Frame(Vector3 velocity, bool space = false, Vector2 move = default, bool airborne = true) => new ChuteInput
        {
            Airborne = airborne, SpacePressed = space, Move = move, BodyVelocity = velocity, Continuous = true,
        };

        /// <summary>Falls until it can open, opens it and flies until it is open.</summary>
        private static ParachuteFlight Opened()
        {
            ParachuteFlight flight = Fall(ParachuteFlight.ReadySeconds + 0.15f);
            Assert.AreEqual(ChuteState.Freefall, flight.State);
            flight.Tick(Frame(flight.Velocity, space: true), Step);
            Assert.AreEqual(ChuteState.Opening, flight.State);
            Fly(flight, ParachuteFlight.OpeningSeconds + 0.1f, Vector2.zero);
            Assert.AreEqual(ChuteState.Open, flight.State);
            return flight;
        }

        private static void Fly(ParachuteFlight flight, float seconds, Vector2 move)
        {
            for (float t = 0.0f; t < seconds; t += Step)
            {
                flight.Tick(Frame(flight.Velocity, move: move), Step);
            }
        }

        [Test]
        public void AJumpAndAQuickSpaceNeverOpenIt()
        {
            // A jump off the ground: up at 4.5 m/s and back down, Space pressed at the top and again on the way down.
            var flight = new ParachuteFlight();
            Vector3 velocity = Vector3.up * 4.5f;
            for (float t = 0.0f; t < 0.95f; t += Step)
            {
                bool space = Mathf.Abs(t - 0.46f) < Step * 0.5f || Mathf.Abs(t - 0.8f) < Step * 0.5f;
                flight.Tick(Frame(velocity, space), Step);
                Assert.IsFalse(flight.DrivesBody, $"a jump's fall had it out at {t:0.00} s");
                velocity += Vector3.down * ParachuteFlight.Gravity * Step;
            }
        }

        [Test]
        public void SpaceDoesNothingUntilTheBodyHasFallenTwoSeconds()
        {
            ParachuteFlight early = Fall(1.6f, pressAt: 1.5f);
            Assert.AreEqual(ChuteState.Packed, early.State, "Space opened it after a second and a half of falling");
            Assert.AreEqual(2.0f, ParachuteFlight.ReadySeconds, "the owner's number: about two seconds of falling");
            ParachuteFlight ready = Fall(ParachuteFlight.ReadySeconds + 0.15f);
            Assert.AreEqual(ChuteState.Freefall, ready.State, "two seconds of falling did not spread the body out to open it");
            ParachuteFlight opened = Fall(ParachuteFlight.ReadySeconds + 0.2f, pressAt: ParachuteFlight.ReadySeconds + 0.1f);
            Assert.AreEqual(ChuteState.Opening, opened.State, "Space after two seconds of falling did not open it");
        }

        [Test]
        public void GoingUpDoesNotCountAsFalling()
        {
            // Thrown high: two seconds rising, then a second falling -- not three seconds of fall.
            ParachuteFlight flight = Fall(3.0f, upward: ParachuteFlight.Gravity * 2.0f);
            Assert.AreEqual(ChuteState.Packed, flight.State);
            Assert.Less(flight.FallSeconds, 1.1f);
        }

        [Test]
        public void TheGroundOrAFrameOnAWallStartsTheCountAgain()
        {
            ParachuteFlight flight = Fall(1.5f);
            flight.Tick(Frame(Vector3.down * 15.0f, airborne: false), Step);
            Assert.AreEqual(0.0f, flight.FallSeconds);
            flight = Fall(ParachuteFlight.ReadySeconds + 0.5f);
            Assert.AreEqual(ChuteState.Freefall, flight.State);
            var broken = Frame(flight.Velocity);
            broken.Continuous = false;
            flight.Tick(broken, Step);
            Assert.AreEqual(ChuteState.Packed, flight.State, "a frame on a wall or knocked down left it spread out");
            Assert.Less(flight.FallSeconds, 0.1f);
        }

        [Test]
        public void InFreefallTheAirHoldsTheBodyNearASkydiversSpeed()
        {
            ParachuteFlight flight = Fall(ParachuteFlight.ReadySeconds + 0.15f);
            Fly(flight, 12.0f, Vector2.zero);
            Assert.AreEqual(ParachuteFlight.TerminalSpeed, -flight.Velocity.y, 1.5f, "spread out it does not settle near its terminal speed");
            Fly(flight, 10.0f, new Vector2(0.0f, 1.0f));
            Assert.Greater(-flight.Velocity.y, ParachuteFlight.TerminalSpeed + 10.0f, "W does not dive it faster");
            Assert.Greater(new Vector2(flight.Velocity.x, flight.Velocity.z).magnitude, 10.0f, "diving does not carry it forward");
            Fly(flight, 10.0f, new Vector2(0.0f, -1.0f));
            Assert.Less(-flight.Velocity.y, ParachuteFlight.TerminalSpeed - 5.0f, "S does not spread it out slower");
            float heading = flight.HeadingDegrees;
            Fly(flight, 1.0f, new Vector2(1.0f, 0.0f));
            Assert.Greater(Mathf.DeltaAngle(heading, flight.HeadingDegrees), 45.0f, "D does not turn a body in freefall");
        }

        [Test]
        public void OpeningBrakesTheFallHardOverAboutASecond()
        {
            ParachuteFlight flight = Fall(ParachuteFlight.ReadySeconds + 0.15f);
            Fly(flight, 6.0f, Vector2.zero);
            float before = -flight.Velocity.y;
            flight.Tick(Frame(flight.Velocity, space: true), Step);
            Fly(flight, ParachuteFlight.PullSeconds - Step, Vector2.zero);
            Assert.Greater(-flight.Velocity.y, before * 0.9f, "the fall was braked before the pilot chute was out");
            Fly(flight, ParachuteFlight.InflateSeconds + 0.1f, Vector2.zero);
            Assert.AreEqual(ChuteState.Open, flight.State);
            Assert.AreEqual(ParachuteFlight.GlideSink, -flight.Velocity.y, 0.5f, "open, it does not sink at the canopy's own rate");
            Assert.That(ParachuteFlight.OpeningSeconds, Is.InRange(0.8f, 2.0f));
        }

        [Test]
        public void TheCanopyGlidesForwardAboutThreeMetresForEveryOneItSinks()
        {
            ParachuteFlight flight = Opened();
            Fly(flight, 3.0f, Vector2.zero);
            float glide = flight.Airspeed / flight.Sink;
            Assert.That(glide, Is.InRange(2.3f, 3.3f), $"glides {glide:0.0} to 1");
            Vector3 flat = new Vector3(flight.Velocity.x, 0.0f, flight.Velocity.z);
            Assert.AreEqual(flight.Airspeed, flat.magnitude, 0.01f);
            Assert.Greater(Vector3.Dot(flat, ParachuteFlight.Forward(flight.HeadingDegrees)), 0.0f, "it does not fly the way it faces");
        }

        [Test]
        public void WDivesItAndSSlowsIt()
        {
            ParachuteFlight flight = Opened();
            Fly(flight, 3.0f, new Vector2(0.0f, 1.0f));
            Assert.Greater(flight.Airspeed, ParachuteFlight.GlideSpeed + 3.0f, "W does not speed it up");
            Assert.Greater(flight.Sink, ParachuteFlight.GlideSink + 2.0f, "W does not dive it");
            Fly(flight, 6.0f, new Vector2(0.0f, -1.0f));
            Assert.Less(flight.Airspeed, ParachuteFlight.GlideSpeed - 4.0f, "S does not slow it");
            Assert.Less(flight.Sink, ParachuteFlight.GlideSink, "held S does not sink it slower than the glide");
            Assert.Greater(flight.LeftToggle, 0.9f);
            Assert.Greater(flight.RightToggle, 0.9f, "S does not pull both toggles down");
        }

        [Test]
        public void AAndDTurnItAndATurnSinksItFaster()
        {
            ParachuteFlight flight = Opened();
            float heading = flight.HeadingDegrees;
            Fly(flight, 1.0f, new Vector2(-1.0f, 0.0f));
            Assert.Less(Mathf.DeltaAngle(heading, flight.HeadingDegrees), -30.0f, "A does not turn it left");
            Assert.Greater(flight.LeftToggle, 0.9f, "A does not pull the left toggle");
            Assert.Less(flight.RightToggle, 0.1f);
            Assert.Less(flight.BankDegrees, -10.0f, "a left turn does not bank it left");
            Assert.Greater(flight.Sink, ParachuteFlight.GlideSink + 1.5f, "a turn does not sink it faster");
            heading = flight.HeadingDegrees;
            Fly(flight, 2.0f, new Vector2(1.0f, 0.0f));
            Assert.Greater(Mathf.DeltaAngle(heading, flight.HeadingDegrees), 30.0f, "D does not turn it right");
        }

        [Test]
        public void AFlareFromAGlideAlmostStopsTheSinkForAMomentThenItFades()
        {
            ParachuteFlight flight = Opened();
            Fly(flight, 3.0f, Vector2.zero);
            float least = float.MaxValue;
            for (float t = 0.0f; t < 1.5f; t += Step)
            {
                flight.Tick(Frame(flight.Velocity, move: new Vector2(0.0f, -1.0f)), Step);
                least = Mathf.Min(least, flight.Sink);
            }
            Assert.Less(least, ParachuteFlight.SoftLandingSink * 0.5f, "pulling S from a full glide does not flare it");
            Fly(flight, 6.0f, new Vector2(0.0f, -1.0f));
            Assert.Greater(flight.Sink, ParachuteFlight.SoftLandingSink, "the flare's lift lasts for ever: held brakes must not land it softly");
        }

        [Test]
        public void ItLandsSoftFlaredFirmGlidingAndHardDivingOrTurningLow()
        {
            Assert.AreEqual(ChuteLanding.Soft, LandAfter(new Vector2(0.0f, -1.0f), 0.5f), "landed in the flare");
            Assert.AreEqual(ChuteLanding.Firm, LandAfter(Vector2.zero, 2.0f), "landed gliding");
            Assert.AreEqual(ChuteLanding.Hard, LandAfter(new Vector2(0.0f, 1.0f), 2.0f), "landed diving");
            Assert.AreEqual(ChuteLanding.Hard, LandAfter(new Vector2(1.0f, 0.0f), 1.5f), "landed in a turn");
            Assert.AreEqual(0.0f, ParachuteFlight.DamageOf(ChuteLanding.Firm, 5.0f));
            Assert.Greater(ParachuteFlight.DamageOf(ChuteLanding.Hard, 9.0f), ParachuteFlight.DamageOf(ChuteLanding.Hard, 7.0f));
        }

        private static ChuteLanding LandAfter(Vector2 move, float seconds)
        {
            ParachuteFlight flight = Opened();
            Fly(flight, 3.0f, Vector2.zero);
            Fly(flight, seconds, move);
            flight.Tick(Frame(flight.Velocity, airborne: false), Step);
            Assert.IsTrue(flight.LandedThisFrame);
            Assert.AreEqual(ChuteState.Packed, flight.State, "landed, it is not put away");
            return flight.Landing;
        }

        [Test]
        public void SpaceCutsItAwayOnlyOnceItHasFlownAMoment()
        {
            ParachuteFlight flight = Opened();
            flight.Tick(Frame(flight.Velocity, space: true), Step);
            Assert.AreEqual(ChuteState.Open, flight.State, "Space at once after the opening cut it away");
            Fly(flight, ParachuteFlight.CutAwayAfter, Vector2.zero);
            flight.Tick(Frame(flight.Velocity, space: true), Step);
            Assert.AreEqual(ChuteState.Packed, flight.State, "Space under the canopy did not cut it away");
            Assert.IsTrue(flight.LostThisFrame);
            Assert.AreEqual(0.0f, flight.FallSeconds, "after a cut-away it opens again only after another two seconds of falling");
        }

        [Test]
        public void IntoAWallAtSpeedTheCanopyCollapses()
        {
            ParachuteFlight flight = Opened();
            var hit = Frame(flight.Velocity);
            hit.HitWall = true;
            flight.Tick(hit, Step);
            Assert.AreEqual(ChuteState.Packed, flight.State);
            Assert.IsTrue(flight.LostThisFrame);
        }

        [Test]
        public void TheCanopyIsAnArchedWingOverTheShoulders()
        {
            Mesh mesh = CanopyShape.Build();
            try
            {
                Assert.AreEqual(3, mesh.subMeshCount);
                Bounds bounds = mesh.bounds;
                Assert.AreEqual(CanopyShape.Span, bounds.size.x, 1.0f, "the span");
                Assert.AreEqual(CanopyShape.Chord, bounds.size.z, 0.1f, "the chord");
                Assert.Greater(bounds.min.y, CanopyShape.LineLength - 1.5f, "the tips come down to the shoulders");
                Assert.Less(bounds.max.y, CanopyShape.LineLength + 0.6f);
                Assert.Less(CanopyShape.ArchPoint(1.0f).y, CanopyShape.ArchPoint(0.0f).y - 0.5f, "the span is not arched");
                Assert.AreEqual(1.0f, CanopyShape.UpperShare(0.25f), 1e-4f, "thickest a quarter back");
                Assert.AreEqual(0.0f, CanopyShape.UpperShare(1.0f), 1e-3f, "thin at the tail");
                // A closed shell with every face looking out of it: the volume its faces enclose, each counted by the way it faces, is the wing's own
                // (span x chord x about 0.6 of its thickest); faces looking in would take theirs away.
                Vector3[] v = mesh.vertices;
                float volume = 0.0f;
                for (int m = 0; m < mesh.subMeshCount; m++)
                {
                    int[] faces = mesh.GetTriangles(m);
                    for (int i = 0; i < faces.Length; i += 3)
                    {
                        volume += Vector3.Dot(v[faces[i]], Vector3.Cross(v[faces[i + 1]], v[faces[i + 2]])) / 6.0f;
                    }
                }
                float expected = CanopyShape.Span * CanopyShape.Chord * CanopyShape.Thickness * 0.6f;
                Assert.That(volume, Is.InRange(expected * 0.75f, expected * 1.3f), $"the shell holds {volume:0.00} m3: faces of it look in, or it is open");
                Assert.AreEqual(3, CanopyShape.LineAnchors(true).Count, "three suspension lines a side (owner: \"lower it to maybe 8 total\")");
                Assert.AreEqual(1, CanopyShape.SteeringAnchors(true).Count);
                Assert.Greater(CanopyShape.LineAnchors(true)[0].x, 0.0f);
                Assert.Less(CanopyShape.LineAnchors(false)[0].x, 0.0f);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void AStrangersPacketIsTolerated()
        {
            var body = new GameObject("Copy");
            try
            {
                var rider = body.AddComponent<ParachuteRider>();
                rider.PlayRemote(new AbilityEvent(ParachuteRider.StateChanged, Vector3.zero, Vector3.forward, n: -3));
                rider.PlayRemote(new AbilityEvent(ParachuteRider.StateChanged, Vector3.zero, Vector3.forward, n: 9999));
                rider.PlayRemote(new AbilityEvent(42, Vector3.zero, Vector3.forward, n: 1));
                Assert.AreEqual(ChuteState.Packed, rider.State);
                rider.PlayRemote(new AbilityEvent(ParachuteRider.StateChanged, Vector3.zero, Vector3.forward, n: (int)ChuteState.Open + 4 * ParachuteRider.KeyA));
                Assert.AreEqual(ChuteState.Open, rider.State, "a copy was not told the canopy is open");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }
    }
}
