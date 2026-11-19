using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The held guard knocked to a new angle by each bullet it turns aside (SwordStrikes.PickDeflect, docs/BULLET_DEFLECTION.md), on paper:
    /// the names travel and come back, a bullet always moves the blade (over the head for a shot from above, low for one from below), every
    /// angle keeps the blade across the front of the head or the body, every angle is one the eye tells apart from the rest, within the arm's
    /// reach and with the tip off the ground, and the snap is quick, overshoots a little and lands.
    /// </summary>
    public sealed class SwordDeflectTests
    {
        private static readonly bool[] Holds = { true, false };

        /// <summary>The blade from the wrist, metres: the longest one-handed (the combat sword) and two-handed ones, measured on the body.</summary>
        private static float BladeLength(bool oneHanded) => oneHanded ? 1.8f : 2.2f;

        private static readonly Vector3 Ahead = new Vector3(0.0f, 0.0f, 10.0f);
        private static readonly Vector3 FromTheRight = new Vector3(8.0f, 0.0f, 5.0f);
        private static readonly Vector3 FromAbove = new Vector3(0.0f, 8.0f, 6.0f);
        private static readonly Vector3 FromBelow = new Vector3(0.0f, -8.0f, 6.0f);

        /// <summary>
        /// The front of the body as a shooter sees it, in the body's standing frame (metres): as wide as the head, from the hips to the top
        /// of the head, and in front of the chest.
        /// </summary>
        private const float BodyHalfWidth = FusedBodyShape.HeadRadius;

        private const float BodyBottom = 0.95f;
        private const float BodyFront = 0.3f;

        /// <summary>How much of the blade must lie across that front at every angle, metres.</summary>
        private const float CoveringMetres = 0.4f;

        [Test]
        public void EveryAnglesNameTravelsAndComesBackAndNothingElseReadsAsOne()
        {
            for (int pose = 0; pose < SwordStrikes.DeflectPoseCount; pose++)
            {
                Assert.IsTrue(SwordStrikes.TryGetDeflect(SwordStrikes.DeflectName(pose), out int back), SwordStrikes.DeflectName(pose));
                Assert.AreEqual(pose, back);
                Assert.IsFalse(SwordStrikes.Has(SwordStrikes.DeflectName(pose)), "a deflection is not a strike or a guard of its own");
            }
            Assert.IsFalse(SwordStrikes.TryGetDeflect(SwordStrikes.Guard1HName, out _));
            Assert.IsFalse(SwordStrikes.TryGetDeflect(BlockDeflectEffect.StateName, out _));
            Assert.IsFalse(SwordStrikes.TryGetDeflect("sword_deflect_99", out _));
            Assert.IsFalse(SwordStrikes.TryGetDeflect(null, out _));
        }

        [Test]
        public void ABulletAlwaysMovesTheBladeOverTheHeadFromAboveAndLowFromBelow()
        {
            foreach (bool oneHanded in Holds)
            {
                for (int current = 0; current < SwordStrikes.DeflectPoseCount; current++)
                {
                    for (float roll = 0.0f; roll <= 1.0f; roll += 0.05f)
                    {
                        string at = $"{(oneHanded ? "one" : "two")}-handed, from angle {current}, roll {roll:0.00}";
                        Assert.AreNotEqual(current, SwordStrikes.PickDeflect(oneHanded, current, Ahead, roll), $"{at}: the blade stayed where it was");
                        Assert.AreNotEqual(current, SwordStrikes.PickDeflect(oneHanded, current, FromTheRight, roll), $"{at}: the blade stayed where it was");
                        int high = SwordStrikes.PickDeflect(oneHanded, current, FromAbove, roll);
                        Assert.AreNotEqual(current, high, $"{at}: the blade stayed where it was");
                        Assert.GreaterOrEqual(SwordStrikes.DeflectHeight(oneHanded, high), 0, $"{at}: a shot from above sent the blade low");
                        int low = SwordStrikes.PickDeflect(oneHanded, current, FromBelow, roll);
                        Assert.AreNotEqual(current, low, $"{at}: the blade stayed where it was");
                        Assert.LessOrEqual(SwordStrikes.DeflectHeight(oneHanded, low), 0, $"{at}: a shot from below sent the blade over the head");
                    }
                }
            }
        }

        [Test]
        public void AVolleyFromStraightAheadVisitsEveryAngle()
        {
            foreach (bool oneHanded in Holds)
            {
                var seen = new bool[SwordStrikes.DeflectPoseCount];
                int current = 0;
                var random = new System.Random(7);
                for (int shot = 0; shot < 200; shot++)
                {
                    current = SwordStrikes.PickDeflect(oneHanded, current, Ahead, (float)random.NextDouble());
                    seen[current] = true;
                }
                for (int pose = 0; pose < seen.Length; pose++)
                {
                    Assert.IsTrue(seen[pose], $"{(oneHanded ? "one" : "two")}-handed: angle {pose} was never used");
                }
            }
        }

        [Test]
        public void TheHeightOfAShotIsReadFromItsWay()
        {
            Assert.AreEqual(0, SwordStrikes.HeightOf(Ahead));
            Assert.AreEqual(0, SwordStrikes.HeightOf(Ahead + Vector3.up * 2.0f), "a gun at the height of the head is level");
            Assert.AreEqual(1, SwordStrikes.HeightOf(FromAbove));
            Assert.AreEqual(-1, SwordStrikes.HeightOf(FromBelow));
        }

        /// <summary>
        /// Owner, 2026-11-19, of a draft angle with the sword held up by the right shoulder: "make it so all the blocking positions have either
        /// the sword blocking the head or body otherwise it will just feel like bullshit". At every angle, with the longest blades, at least
        /// <see cref="CoveringMetres"/> of the blade lies across the front of the body or the head.
        /// </summary>
        [Test]
        public void AtEveryAngleTheBladeIsAcrossTheFrontOfTheHeadOrTheBody()
        {
            foreach (bool oneHanded in Holds)
            {
                float top = BodyRig.StandingHeight;
                for (int a = 0; a < SwordStrikes.DeflectPoseCount; a++)
                {
                    SwordFrame frame = SwordStrikes.Guard(oneHanded, a);
                    Vector3 hand = SwordStrikes.HandPoint(frame) - Vector3.up * frame.HipDrop;
                    const float Step = 0.02f;
                    float covering = 0.0f;
                    for (float along = 0.1f; along <= BladeLength(oneHanded); along += Step)
                    {
                        Vector3 point = hand + frame.Blade * along;
                        if (Mathf.Abs(point.x) <= BodyHalfWidth && point.y >= BodyBottom && point.y <= top && point.z >= BodyFront)
                        {
                            covering += Step;
                        }
                    }
                    Assert.GreaterOrEqual(covering, CoveringMetres - 1e-3f,
                        $"{(oneHanded ? "one" : "two")}-handed angle {a}: only {covering * 100.0f:0} cm of the blade is across the front of the body");
                }
            }
        }

        /// <summary>
        /// Every angle is one the eye tells from every other (and from the guard's own): the blade turned 30 degrees or more, or moved 30 cm or
        /// more where it crosses the body (40 cm out from the hand). The hand within the arm's reach with the elbow still bent, and the tip well
        /// off the ground.
        /// </summary>
        [Test]
        public void EveryAngleIsItsOwnWithinReachAndOffTheGround()
        {
            foreach (bool oneHanded in Holds)
            {
                string hold = oneHanded ? "one-handed" : "two-handed";
                float blade = BladeLength(oneHanded);
                for (int a = 0; a < SwordStrikes.DeflectPoseCount; a++)
                {
                    SwordFrame frame = SwordStrikes.Guard(oneHanded, a);
                    Assert.LessOrEqual(frame.Hand.magnitude, 0.92f, $"{hold} angle {a}: the hand is out of the arm's reach");
                    Assert.GreaterOrEqual(frame.Hand.magnitude, 0.35f, $"{hold} angle {a}: the hand is jammed against the shoulder");
                    Assert.AreEqual(0.0f, Vector3.Dot(frame.Blade, frame.Edge), 1e-3f, $"{hold} angle {a}: the edge is not across the blade");
                    Vector3 tip = SwordStrikes.HandPoint(frame) + frame.Blade * blade - Vector3.up * frame.HipDrop;
                    Assert.Greater(tip.y, 0.25f, $"{hold} angle {a}: the tip drags along the ground");
                    for (int b = 0; b < a; b++)
                    {
                        SwordFrame other = SwordStrikes.Guard(oneHanded, b);
                        float turned = Vector3.Angle(frame.Blade, other.Blade);
                        float moved = Vector3.Distance(SwordStrikes.HandPoint(frame) + frame.Blade * 0.4f, SwordStrikes.HandPoint(other) + other.Blade * 0.4f);
                        Assert.IsTrue(turned >= 30.0f || moved >= 0.3f,
                            $"{hold} angles {a} and {b} look alike: the blade turned {turned:0} degrees and moved {moved * 100.0f:0} cm across the body");
                    }
                }
                Assert.Less(Vector3.Angle(SwordStrikes.Guard(oneHanded).Blade, SwordStrikes.Guard(oneHanded, 0).Blade), 0.01f, "angle 0 is not the guard's own");
                Assert.Less(Vector3.Distance(SwordStrikes.Guard(oneHanded).Hand, SwordStrikes.Guard(oneHanded, SwordStrikes.DeflectPoseCount).Hand), 1e-4f,
                    "an angle out of range is not the guard's own");
            }
        }

        [Test]
        public void TheSnapIsQuickGoesALittlePastAndLands()
        {
            Assert.LessOrEqual(SwordStrikes.DeflectSnapSeconds, 0.12f, "a snap slower than a few frames reads as a swing");
            SwordFrame from = SwordStrikes.Guard(true, 0);
            SwordFrame to = SwordStrikes.Guard(true, 3);
            Vector3 travel = to.Hand - from.Hand;
            Assert.Less(Vector3.Distance(from.Hand, SwordStrikes.DeflectSnap(from, to, 0.0f).Hand), 1e-4f, "the snap does not start where the blade is");
            Assert.Less(Vector3.Distance(to.Hand, SwordStrikes.DeflectSnap(from, to, 1.0f).Hand), 1e-4f, "the snap does not land on the new angle");
            Assert.Less(Vector3.Angle(to.Blade, SwordStrikes.DeflectSnap(from, to, 1.0f).Blade), 0.01f, "the snap does not land on the new angle");
            float most = 0.0f;
            float arrived = -1.0f;
            for (float t = 0.0f; t <= 1.0f; t += 0.01f)
            {
                SwordFrame frame = SwordStrikes.DeflectSnap(from, to, t);
                float share = Vector3.Dot(frame.Hand - from.Hand, travel) / travel.sqrMagnitude;
                most = Mathf.Max(most, share);
                if (arrived < 0.0f && share >= 0.95f)
                {
                    arrived = t;
                }
                Assert.AreEqual(1.0f, frame.Blade.magnitude, 1e-3f, $"the blade is not a direction at {t:0.00}");
                Assert.AreEqual(0.0f, Vector3.Dot(frame.Blade, frame.Edge), 1e-3f, $"the edge is not across the blade at {t:0.00}");
            }
            Assert.That(arrived, Is.InRange(0.0f, 0.4f), "the blade is not most of the way there in the first third of the snap");
            Assert.That(most, Is.InRange(1.05f, 1.2f), "the snap does not carry a little past and settle back");
        }

        [Test]
        public void PickingAnAngleAndPosingItMakeNoGarbage()
        {
            SwordStrikes.PickDeflect(true, 0, Ahead, 0.5f);
            SwordStrikes.PickDeflect(false, 0, Ahead, 0.5f);
            SwordStrikes.DeflectName(3);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            int pose = 0;
            int posed = 0;
            for (int i = 0; i < 100; i++)
            {
                pose = SwordStrikes.PickDeflect(i % 2 == 0, pose, FromAbove, i / 100.0f);
                SwordFrame frame = SwordStrikes.DeflectSnap(SwordStrikes.Guard(true, 0), SwordStrikes.Guard(true, pose), 0.5f);
                posed += frame.Active && SwordStrikes.DeflectName(pose) != null ? 1 : 0;
            }
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(100, posed);
            Assert.AreEqual(0, allocated, "a bullet turned aside made garbage");
        }
    }
}
