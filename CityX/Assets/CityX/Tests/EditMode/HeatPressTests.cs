using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The heat press (docs/HEAT_PRESS.md; owner, 2026-11-24: "when initiated, have it clamp down for 3 seconds and then steam puffs out of it and
    /// the shirt is done"): its pressing as time goes, and its shape (the plates meet closed, it opens front up, it stands on its table).
    /// </summary>
    public sealed class HeatPressTests
    {
        [Test]
        public void ItClampsForThreeSecondsThenSteamsAndTheShirtIsDone()
        {
            HeatPressCycle.Pose rest = HeatPressCycle.At(-1.0f);
            Assert.AreEqual(HeatPressCycle.Phase.Resting, rest.Phase);
            Assert.AreEqual(HeatPressShape.OpenDegrees, rest.LidDegrees, "at rest the lid is not open");
            Assert.AreEqual(0.0f, rest.DrawerOut, "at rest the drawer is out");

            float clamped = 0.0f, firstClamp = -1.0f;
            bool loaded = false;
            const float step = 0.01f;
            for (float t = 0.0f; t < HeatPressCycle.TotalSeconds; t += step)
            {
                HeatPressCycle.Pose pose = HeatPressCycle.At(t);
                if (pose.Phase == HeatPressCycle.Phase.Clamped)
                {
                    Assert.AreEqual(0.0f, pose.LidDegrees, "clamped, the lid is not down");
                    Assert.AreEqual(0.0f, pose.DrawerOut, "clamped with the drawer out");
                    clamped += step;
                    if (firstClamp < 0.0f) firstClamp = t;
                }
                if (pose.DrawerOut > 0.0f)
                {
                    Assert.AreEqual(HeatPressShape.OpenDegrees, pose.LidDegrees, 1e-3f, "the drawer moves under a lid that is not open");
                }
                loaded |= pose.DrawerOut >= HeatPressShape.DrawerTravel - 1e-3f && firstClamp < 0.0f;
            }
            Assert.IsTrue(loaded, "the drawer never came out for the shirt");
            Assert.AreEqual(HeatPressCycle.ClampSeconds, clamped, 0.02f, "the clamp does not last 3 seconds");
            Assert.AreEqual(3.0f, HeatPressCycle.ClampSeconds);
            Assert.AreEqual(firstClamp + HeatPressCycle.ClampSeconds, HeatPressCycle.SteamAt, 0.02f, "the steam does not come as the clamp ends");
            Assert.AreEqual(HeatPressCycle.Phase.Opening, HeatPressCycle.At(HeatPressCycle.SteamAt + 0.01f).Phase);
            Assert.Greater(HeatPressCycle.At(HeatPressCycle.SteamAt + HeatPressCycle.OpenSeconds + 0.3f).DrawerOut, 0.0f, "the shirt does not come out done");
            Assert.AreEqual(HeatPressCycle.Phase.Resting, HeatPressCycle.At(HeatPressCycle.TotalSeconds + 0.1f).Phase, "it never comes to rest");
        }

        [Test]
        public void ClosedThePlatesMeetAndOpenItsFrontIsUp()
        {
            Mesh lid = HeatPressShape.Lid(), drawer = HeatPressShape.Drawer();
            Assert.AreEqual(HeatPressShape.PadTop, drawer.bounds.max.y, 1e-4f, "the pad's top is not where a shirt lies");
            float heaterBottom = float.MaxValue;
            foreach (int v in lid.GetTriangles((int)HeatPressShape.Finish.Heater))
            {
                heaterBottom = Mathf.Min(heaterBottom, lid.vertices[v].y);
            }
            Assert.AreEqual(HeatPressShape.PadTop, heaterBottom, 1e-4f, "closed, the heating plate does not rest on the pad");

            // Open, the lid's front edge stands well clear of the pad, and its handle points up.
            Quaternion open = HeatPressShape.LidTurn(HeatPressShape.OpenDegrees);
            Vector3 front = HeatPressShape.Pivot + open * (new Vector3(0.0f, HeatPressShape.PadTop, HeatPressShape.PadHalf.y) - HeatPressShape.Pivot);
            Assert.Greater(front.y - HeatPressShape.PadTop, 0.3f, "open, there is no room to lay a shirt");
            Vector3 grip = HeatPressShape.Pivot + open * (lid.bounds.max - HeatPressShape.Pivot);
            Assert.Greater(grip.y, HeatPressShape.Pivot.y, "open, the handle is not up");
        }

        [Test]
        public void ItStandsOnItsTable()
        {
            Assert.AreEqual(HeatPressShape.TableHeight, HeatPressShape.Table().bounds.max.y, 1e-4f);
            foreach (Mesh part in new[] { HeatPressShape.Base(), HeatPressShape.Drawer() })
            {
                Bounds b = part.bounds;
                Assert.AreEqual(0.0f, Mathf.Min(0.0f, b.min.y), 1e-4f, $"{part.name} sinks into the table");
                Vector3 back = HeatPressShape.OnTheTable + new Vector3(0.0f, 0.0f, b.min.z);
                Assert.GreaterOrEqual(back.z, -HeatPressShape.TableHalf.y, $"{part.name} hangs off the table's back");
                Assert.LessOrEqual(Mathf.Max(-b.min.x, b.max.x), HeatPressShape.TableHalf.x, $"{part.name} hangs off the table's side");
            }
        }
    }
}
