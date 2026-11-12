using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The row of characters on the belt (<see cref="BeltLine"/>): rolled one to the middle, set down there exactly, never past it.</summary>
    public sealed class BeltLineTests
    {
        private const float Frame = 1.0f / 60.0f;

        /// <summary>Steps until it stops (or a minute); the seconds it took.</summary>
        private static float RunToRest(BeltLine line, System.Action<float> eachStep = null)
        {
            float seconds = 0.0f;
            while (!line.Settled && seconds < 60.0f)
            {
                float moved = line.Step(Frame);
                eachStep?.Invoke(moved);
                seconds += Frame;
            }
            return seconds;
        }

        [Test]
        public void TheNextOneStandsOnTheRightAndIsRolledInByTheBeltRunningLeft()
        {
            var line = new BeltLine(3, 0);
            Assert.AreEqual(0.0f, line.At(0));
            Assert.AreEqual(line.Spacing, line.At(1), 1e-5f, "the next character does not stand on the right");
            line.RollTo(1);
            float run = 0.0f;
            float seconds = RunToRest(line, moved =>
            {
                run += moved;
                Assert.LessOrEqual(moved, 0.0f, "the belt ran right bringing in the one on the right");
            });
            Assert.AreEqual(1.0f, line.Scroll, 1e-6f, "not set down in the middle exactly");
            Assert.AreEqual(0.0f, line.At(1), 1e-5f);
            Assert.AreEqual(-line.Spacing, run, 1e-4f, "the belt's surface did not run one character's width");
            Assert.Less(seconds, 1.3f, "rolling to the next one takes too long");
            Assert.Greater(seconds, 0.6f, "rolling to the next one is a jump, not a roll");
        }

        [Test]
        public void ItNeverRunsPastItsCharacterNorFasterThanItsTopSpeed()
        {
            var line = new BeltLine(6, 0);
            line.RollTo(5);
            float last = line.Scroll;
            RunToRest(line, moved =>
            {
                Assert.LessOrEqual(Mathf.Abs(line.BeltSpeed), BeltLine.TopSpeed + 1e-4f);
                Assert.GreaterOrEqual(line.Scroll, last - 1e-6f, "it ran back");
                Assert.LessOrEqual(line.Scroll, 5.0f + 1e-6f, "it ran past the one it was rolling in");
                last = line.Scroll;
            });
            Assert.AreEqual(5, line.Target);
            Assert.IsTrue(line.Settled);
        }

        [Test]
        public void TurnedBackHalfWayItSlowsTurnsAndSetsTheOtherDown()
        {
            var line = new BeltLine(3, 1);
            line.RollTo(2);
            for (int i = 0; i < 20; i++)
            {
                line.Step(Frame);
            }
            Assert.Less(line.BeltSpeed, 0.0f, "setup: it was not rolling the next one in");
            line.RollTo(0);
            float fastest = 0.0f;
            RunToRest(line, moved => fastest = Mathf.Max(fastest, Mathf.Abs(line.BeltSpeed)));
            Assert.AreEqual(0.0f, line.Scroll, 1e-6f);
            Assert.LessOrEqual(fastest, BeltLine.TopSpeed + 1e-4f);
        }

        /// <summary>
        /// On a 4:3, 16:10, 16:9 or ultrawide screen the one beside the middle stands wholly in sight in the row, and wholly out of it once the view
        /// has closed in beside the card (where it stood in the card's way: the bin on its head).
        /// </summary>
        [TestCase(4.0f / 3.0f)]
        [TestCase(1.6f)]
        [TestCase(16.0f / 9.0f)]
        [TestCase(21.0f / 9.0f)]
        public void TheRowsSpacingKeepsTheNextOneInSightInTheRowAndOutOfItBesideTheCard(float aspect)
        {
            var host = new GameObject("View");
            try
            {
                Camera view = host.AddComponent<Camera>();
                view.fieldOfView = 75.0f;
                view.aspect = aspect;
                host.transform.localPosition = new Vector3(0.0f, 1.6f, 4.0f);
                float spacing = CharacterSelect.RowSpacing(view);
                float rowEdge = BeltIntro.EdgeOfScreen(view);
                float closedInEdge = Mathf.Abs(CharacterSelect.InspectView.x)
                                     + CharacterSelect.InspectView.z * Mathf.Tan(37.5f * Mathf.Deg2Rad) * aspect;
                Assert.Less(spacing + BeltLineup.HalfWidth, rowEdge, "the next one is cut by the edge of the row's view");
                Assert.Greater(spacing - BeltLineup.HalfWidth, closedInEdge, "the next one stands in sight beside the card");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// Owner, 2026-11-11: "make the scroll go the same speed as the first boot screen scroll, instead of it locking onto characters make it
        /// so you can freely scroll across".
        /// </summary>
        [Test]
        public void PushedItRunsAtTheFirstBootsSpeedAndStopsWhereItIsLetGo()
        {
            var line = new BeltLine(5, 0);
            line.Push(1.0f);
            float fastest = 0.0f;
            for (float t = 0.0f; t < 1.5f; t += Frame)
            {
                line.Step(Frame);
                fastest = Mathf.Max(fastest, Mathf.Abs(line.BeltSpeed));
            }
            Assert.AreEqual(BeltRide.Speed, fastest, 1e-3f, "the belt does not run at the first boot's speed");
            Assert.Less(line.BeltSpeed, 0.0f, "bringing the later ones in, the belt does not run left");
            line.Push(0.0f);
            float before = line.Scroll;
            RunToRest(line);
            float stopped = line.Scroll;
            Assert.Greater(stopped, before, "it stopped dead instead of slowing");
            Assert.Less((stopped - before) * line.Spacing, BeltRide.Speed * BeltRide.Speed / (2.0f * BeltRide.Acceleration) + 0.05f, "it ran on past slowing");
            Assert.Greater(Mathf.Abs(stopped - Mathf.Round(stopped)) * line.Spacing, 0.01f,
                "it locked onto a character (setup: let go between two, which a 1.5 s run at this speed does)");
        }

        [Test]
        public void PushedItNeverRunsPastEitherEnd()
        {
            var line = new BeltLine(3, 1);
            line.Push(1.0f);
            for (float t = 0.0f; t < 10.0f; t += Frame)
            {
                line.Step(Frame);
                Assert.LessOrEqual(line.Scroll, 2.0f + 1e-5f, "it ran past the last character");
            }
            Assert.AreEqual(2.0f, line.Scroll, 1e-4f, "held, it does not reach the last character");
            Assert.AreEqual(0.0f, line.BeltSpeed, 1e-4f);
            line.Push(-1.0f);
            for (float t = 0.0f; t < 10.0f; t += Frame)
            {
                line.Step(Frame);
                Assert.GreaterOrEqual(line.Scroll, -1e-5f, "it ran past the first character");
            }
            Assert.AreEqual(0.0f, line.Scroll, 1e-4f);
        }

        [Test]
        public void APushTakesTheBeltOverFromAGlide()
        {
            var line = new BeltLine(4, 0);
            line.RollTo(3);
            line.Step(Frame);
            Assert.IsTrue(line.Gliding);
            line.Push(-1.0f);
            Assert.IsFalse(line.Gliding, "a push did not take the belt over");
        }

        [Test]
        public void TheRowsEndsHold()
        {
            var line = new BeltLine(2, 0);
            line.RollTo(-3);
            Assert.AreEqual(0, line.Target);
            line.RollTo(9);
            Assert.AreEqual(1, line.Target);
            var empty = new BeltLine(0, 0);
            empty.RollTo(1);
            Assert.AreEqual(0.0f, empty.Step(Frame));
            Assert.IsTrue(empty.Settled);
        }
    }
}
