using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The knife's heavy, the icepick stab (owner, 2026-11-15: "a downward stab motion holding the knife upside down", "make the flip to upside
    /// down quick and natural"; docs/KNIFE.md): the knife spins upside down in the fist quickly as the hand is raised, over the knuckles, about
    /// the handle it is held by; the fist goes up beside the head with the point down and is driven down in front; the knife spins back after.
    /// </summary>
    public sealed class KnifeStabTests
    {
        private const SwordStrikeKind Stab = SwordStrikeKind.Thrust1H;

        /// <summary>From the fist to the knife's point, metres: the guard 9 cm from the fist and the kitchen knife's 26 cm blade.</summary>
        private const float FistToPoint = 0.35f;

        [Test]
        public void OnlyTheKnifesStabTurnsTheGripOver()
        {
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                Assert.AreEqual(kind == Stab, SwordStrikes.ReversesGrip(kind), $"{kind}");
                if (kind == Stab)
                {
                    continue;
                }
                for (int i = 0; i <= 40; i++)
                {
                    Assert.AreEqual(0.0f, SwordStrikes.GripFlip(kind, SwordStrikes.Seconds(kind) * i / 40.0f), $"{kind} spins the sword in the hand");
                }
            }
        }

        [Test]
        public void TheKnifeSpinsOverQuicklyInTheRaiseAndBackOnceTheStabHasLanded()
        {
            SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(Stab);
            float over = SwordStrikes.GripFlipFromSeconds + SwordStrikes.GripFlipSeconds;
            float landed = timing.Contact + timing.Follow + timing.Settle;
            Assert.AreEqual(0.0f, SwordStrikes.GripFlip(Stab, 0.0f), "the knife starts the stab already turned over");
            Assert.Less(over, timing.WindUp, "the knife is still spinning when the hand reaches the top");
            Assert.LessOrEqual(SwordStrikes.GripFlipSeconds / KnifeRules.SwingSpeed, 0.1f, "the spin is not quick");
            // A flick: well over halfway a third of the way in, a little past upside down, settled by the end.
            Assert.Greater(SwordStrikes.GripFlip(Stab, SwordStrikes.GripFlipFromSeconds + SwordStrikes.GripFlipSeconds / 3.0f), 0.5f, "the spin is slow off the mark");
            float most = 0.0f;
            for (float s = 0.0f; s <= timing.Total; s += 0.005f)
            {
                most = Mathf.Max(most, SwordStrikes.GripFlip(Stab, s));
            }
            Assert.Greater(most, 1.01f, "the spin stops dead at upside down instead of settling into it");
            Assert.Less(most, 1.1f, "the spin goes on too far past upside down");
            for (float s = over; s <= landed; s += 0.01f)
            {
                Assert.AreEqual(1.0f, SwordStrikes.GripFlip(Stab, s), 0.001f, $"the knife is not upside down {s:0.00} s in, from the top of the raise until the stab has landed");
            }
            Assert.AreEqual(0.0f, SwordStrikes.GripFlip(Stab, landed + SwordStrikes.GripFlipSeconds), 0.001f, "the knife is not back in its hold");
            Assert.Less(landed + SwordStrikes.GripFlipSeconds, timing.Total, "the knife is still upside down when the stab is over");
        }

        [Test]
        public void TheKnifeIsRaisedAboveTheHeadPointDownThenDrivenDownInFront()
        {
            SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(Stab);
            float top = (timing.WindUp + timing.Hold * 0.5f) / timing.Total;
            SwordFrame raised = SwordStrikes.Evaluate(Stab, top);
            Vector3 fist = SwordStrikes.HandPoint(raised);
            Vector3 knife = SwordStrikes.HeldBladeAt(Stab, top);
            Assert.Greater(fist.y, BodyRig.ShoulderY + 0.45f, "the fist is not raised above the head");
            Assert.Greater(fist.x, BodyRig.ShoulderX * 0.8f, "the fist is not out beside the head, where the game's camera sees it past the head");
            Assert.Less(knife.y, -0.6f, "the raised knife's point is not down");
            Assert.Greater(knife.z, 0.0f, "the raised knife does not point at the one in front");
            Assert.Greater(raised.FreeHand.z, 0.5f, "the free hand does not reach out ahead in the raise");

            float contact = SwordStrikes.ContactFraction(Stab);
            SwordFrame struck = SwordStrikes.Evaluate(Stab, contact);
            Vector3 hit = SwordStrikes.HandPoint(struck);
            Vector3 point = hit + SwordStrikes.HeldBladeAt(Stab, contact) * FistToPoint;
            Assert.Greater(hit.z, 0.6f, "the stab does not land in front");
            Assert.Less(SwordStrikes.HeldBladeAt(Stab, contact).y, -0.7f, "the knife is not driven point first, down");
            Assert.That(point.y, Is.InRange(1.5f, 2.1f), "the point lands somewhere other than a standing body's chest");
            Assert.Less(struck.FreeHand.z, 0.0f, "the free arm is not thrown back by the blow");

            // Down all the way through the strike and the follow-through.
            float from = SwordStrikes.StrikeStart(Stab), to = (timing.Contact + timing.Follow) / timing.Total;
            float last = float.MaxValue;
            for (int i = 0; i <= 20; i++)
            {
                float y = SwordStrikes.HandPoint(SwordStrikes.Evaluate(Stab, Mathf.Lerp(from, to, i / 20.0f))).y;
                Assert.LessOrEqual(y, last + 0.01f, $"the fist rises again {i * 5}% of the way through the strike");
                last = y;
            }
            Assert.Less(last, fist.y - 0.7f, "the fist is not driven down");
        }

        [Test]
        public void TheKnifeTurnedOverIsHeldByTheSameStretchOfHandleTheOtherWayUp()
        {
            var made = new GameObject("Knife");
            try
            {
                var knife = made.AddComponent<SwordWeapon>();
                knife.OneHanded = true;
                knife.Knife = true;
                knife.ApplyGrip();
                Vector3 position = made.transform.localPosition;
                Quaternion rotation = made.transform.localRotation;
                Vector3 blade = rotation * Vector3.up, edge = rotation * knife.EdgeLocal;
                Vector3 fist = SwordWeapon.FistPoint;

                knife.HoldReversed(1.0f);
                Vector3 turned = made.transform.localRotation * Vector3.up;
                Assert.Less(Vector3.Angle(turned, -blade), 0.5f, "turned over, the blade does not point the other way");
                Assert.Less(Vector3.Angle(made.transform.localRotation * knife.EdgeLocal, -edge), 0.5f, "turned over, the edge does not face the other way");
                // The fist holds the same stretch of handle: as far from the guard, and the handle as close to the palm.
                Assert.AreEqual(Vector3.Dot(fist - position, blade), Vector3.Dot(fist - made.transform.localPosition, turned), 0.001f, "the fist slid along the handle");
                Assert.AreEqual(Vector3.Cross(fist - position, blade).magnitude, Vector3.Cross(fist - made.transform.localPosition, turned).magnitude, 0.001f,
                    "the handle left the fist");
                Assert.Less(Quaternion.Angle(knife.GripFlipInHand * rotation, made.transform.localRotation), 0.1f, "the spin it reports is not the one it is held in");

                // Halfway over, the blade is out past the knuckles: it spins over the fingers, not back through the wrist.
                knife.HoldReversed(0.5f);
                Vector3 halfway = made.transform.localRotation * Vector3.up;
                Assert.Greater(Vector3.Dot(halfway, edge), 0.95f, "the spin does not take the blade over the edge's side");
                Assert.Greater(halfway.x, 0.5f, "halfway over, the blade is not out past the knuckles");

                knife.HoldReversed(0.0f);
                Assert.Less(Vector3.Distance(position, made.transform.localPosition), 1e-4f, "spun back, the knife is not where its hold has it");
                Assert.Less(Quaternion.Angle(rotation, made.transform.localRotation), 0.01f, "spun back, the knife is not turned as its hold has it");
                Assert.Less(Quaternion.Angle(Quaternion.identity, knife.GripFlipInHand), 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(made);
            }
        }
    }
}
