using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// An enemy with nothing to fight wanders (owner, 2026-11-14: "make sure that the mobs have patrol behaviors or in other words they
    /// wander around in a natural way rather than awkwardly stand still"): NpcWander, walked here over an empty plain and inside a walled
    /// yard with nothing but its own rules and a body that does what it says.
    /// </summary>
    public sealed class NpcWanderTests
    {
        private const float Dt = 1.0f / 60.0f;
        private const float WalkMetresPerSecond = 4.0f;

        /// <summary>A body that does what the wander says, in a square yard of <paramref name="halfSide"/> (walls all round; 0: an endless plain).</summary>
        private sealed class Walker
        {
            public Vector3 Position;
            public float Yaw;
            public float Walked, LongestStill, MostTurnedInAFrame, FurthestFromStart;

            /// <summary>How far the walls have had to hold it back, metres (a body that curves as it walks can brush one it set off beside; the wall stops it).</summary>
            public float HeldBackByWalls;

            /// <summary>The walls stop the body, as a real one's do; false: touching one fails the test.</summary>
            public bool WallsAreSolid;
            public int Strolls;
            private float _still;
            private bool _wasWalking;

            public void Run(NpcWander wander, float seconds, float halfSide, Vector3 pit = default, float pitRadius = 0.0f)
            {
                Vector3 start = Position;
                for (float t = 0.0f; t < seconds; t += Dt)
                {
                    Vector3 forward = Quaternion.Euler(0.0f, Yaw, 0.0f) * Vector3.forward;
                    float clear = NpcWander.ProbeMetres;
                    if (halfSide > 0.0f)
                    {
                        // How far along the way it faces a body of its width can go before it touches a wall.
                        float room = halfSide - NpcWanderProbe.WalkerRadius;
                        if (forward.x > 1e-4f) clear = Mathf.Min(clear, (room - Position.x) / forward.x);
                        if (forward.x < -1e-4f) clear = Mathf.Min(clear, (-room - Position.x) / forward.x);
                        if (forward.z > 1e-4f) clear = Mathf.Min(clear, (room - Position.z) / forward.z);
                        if (forward.z < -1e-4f) clear = Mathf.Min(clear, (-room - Position.z) / forward.z);
                    }
                    bool ground = pitRadius <= 0.0f || Vector3.Distance(Position + forward * NpcWanderProbe.StrideMetres, pit) > pitRadius;
                    NpcWander.Step step = wander.Tick(new NpcWander.Senses { Position = Position, YawDegrees = Yaw, ClearAheadMetres = clear, GroundAhead = ground }, Dt);
                    MostTurnedInAFrame = Mathf.Max(MostTurnedInAFrame, Mathf.Abs(Mathf.DeltaAngle(Yaw, step.YawDegrees)));
                    Yaw = step.YawDegrees;
                    Assert.That(step.Move, Is.InRange(0.0f, NpcWander.StrollShare + 1e-4f), "it walks faster than a stroll");
                    float moved = step.Move * WalkMetresPerSecond * Dt;
                    Position += Quaternion.Euler(0.0f, Yaw, 0.0f) * Vector3.forward * moved;
                    Walked += moved;
                    bool walking = step.Move > 0.0f;
                    if (walking && !_wasWalking) Strolls++;
                    _wasWalking = walking;
                    _still = walking ? 0.0f : _still + Dt;
                    LongestStill = Mathf.Max(LongestStill, _still);
                    FurthestFromStart = Mathf.Max(FurthestFromStart, Vector3.Distance(Position, start));
                    if (halfSide > 0.0f && WallsAreSolid)
                    {
                        float room = halfSide - NpcWanderProbe.WalkerRadius;
                        var held = new Vector3(Mathf.Clamp(Position.x, -room, room), Position.y, Mathf.Clamp(Position.z, -room, room));
                        HeldBackByWalls += Vector3.Distance(held, Position);
                        Position = held;
                    }
                    else if (halfSide > 0.0f)
                    {
                        Assert.Less(Mathf.Max(Mathf.Abs(Position.x), Mathf.Abs(Position.z)), halfSide - NpcWanderProbe.WalkerRadius + 0.01f, $"it walked into a wall at {t:0.0} s");
                    }
                    if (pitRadius > 0.0f)
                    {
                        Assert.Greater(Vector3.Distance(Position, pit), pitRadius - 0.05f, $"it walked into the drop at {t:0.0} s");
                    }
                }
            }
        }

        [Test]
        public void ItStrollsAndStandsByTurnsTurningBeforeItWalksAndStaysAboutWhereItBegan()
        {
            foreach (int seed in new[] { 1, 7, 42, 2026 })
            {
                var walker = new Walker { Yaw = 30.0f };
                walker.Run(new NpcWander(seed), 180.0f, 0.0f);
                Assert.Greater(walker.Walked, 40.0f, $"seed {seed}: it hardly moved in three minutes: it stands about");
                Assert.Greater(walker.Strolls, 12, $"seed {seed}: it set off only {walker.Strolls} times");
                Assert.Greater(walker.LongestStill, NpcWander.PauseSecondsMin - 0.1f, $"seed {seed}: it never stood a moment: it marches");
                Assert.Less(walker.LongestStill, NpcWander.PauseSecondsMax + 2.5f, $"seed {seed}: it stood {walker.LongestStill:0.0} s: it stands about");
                Assert.LessOrEqual(walker.MostTurnedInAFrame, NpcWander.TurnDegreesPerSecond * Dt + 0.01f, $"seed {seed}: it snapped round");
                // A leash, not a wall: a stroll begun near the edge can carry it a little past before it turns back.
                Assert.Less(walker.FurthestFromStart, NpcWander.HomeRadiusMetres * 1.5f + NpcWander.WalkSecondsMax * NpcWander.StrollShare * WalkMetresPerSecond,
                    $"seed {seed}: it strayed {walker.FurthestFromStart:0.0} m from where it began");
                Assert.Greater(walker.FurthestFromStart, 3.0f, $"seed {seed}: it paces on the spot");
            }
        }

        [Test]
        public void ItTurnsAsideFromAWallAndFromADropAndGoesOn()
        {
            // Both kinds of body: one that turns where it stands and then goes, and one that comes round in a curve as it walks.
            foreach (bool turnsOnTheSpot in new[] { true, false })
            {
                foreach (int seed in new[] { 3, 11, 99 })
                {
                    string which = $"seed {seed}, {(turnsOnTheSpot ? "turning on the spot" : "curving")}";
                    // A yard 12 m square: smaller than its leash, so the walls are what keep it in.
                    // (One that turns on the spot never touches a wall. One that curves can brush a wall it set off beside, which stops
                    // it as a wall does: it must not go on walking into it.)
                    var yard = new Walker { Yaw = 0.0f, WallsAreSolid = !turnsOnTheSpot };
                    yard.Run(new NpcWander(seed, turnsOnTheSpot), 240.0f, 6.0f);
                    Assert.Greater(yard.Walked, 60.0f, $"{which}: boxed in, it gave up walking");
                    Assert.Less(yard.HeldBackByWalls, 1.5f, $"{which}: the walls held it back {yard.HeldBackByWalls:0.0} m in four minutes: it walks into them");

                    // A pit 2 m across, 3 m in front of it on an open plain: it never steps in.
                    var plain = new Walker { Yaw = 0.0f };
                    plain.Run(new NpcWander(seed, turnsOnTheSpot), 240.0f, 0.0f, new Vector3(0.0f, 0.0f, 3.0f), 1.0f);
                    Assert.Greater(plain.Walked, 60.0f, $"{which}: beside a drop, it gave up walking");
                }
            }
        }

        [Test]
        public void AfterAFightItWandersFromWhereverItIs()
        {
            var wander = new NpcWander(5);
            var walker = new Walker();
            walker.Run(wander, 30.0f, 0.0f);
            // Carried far off by a chase, then nothing to fight again.
            walker.Position += new Vector3(200.0f, 0.0f, 0.0f);
            wander.Reset();
            Vector3 newHome = walker.Position;
            walker.FurthestFromStart = 0.0f;
            walker.Run(wander, 120.0f, 0.0f);
            Assert.Less(Vector3.Distance(walker.Position, newHome), NpcWander.HomeRadiusMetres * 3.0f, "it walked back to where it was before the fight");
        }
    }
}
