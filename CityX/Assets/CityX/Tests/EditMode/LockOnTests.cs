using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Which target a press picks, and how cycling ends. The swinging is the
    /// camera's job; this is the part that decides who you are fighting.
    /// </summary>
    public sealed class LockOnTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        private Transform At(Vector3 position)
        {
            var go = new GameObject("Target");
            go.transform.position = position;
            _made.Add(go);
            return go.transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        /// <summary>
        /// A press grabs whoever you are LOOKING AT, not whoever is nearest.
        /// Otherwise turning to face one enemy while another stands closer
        /// behind you locks the wrong one, which is worse than not locking.
        /// </summary>
        [Test]
        public void ItPicksWhoeverIsNearestTheCrosshairNotNearestTheBody()
        {
            Transform ahead = At(new Vector3(0.0f, 0.0f, 8.0f));
            Transform closerButBehind = At(new Vector3(0.0f, 0.0f, -2.0f));

            Transform picked = LockOn.Acquire(Vector3.zero, Vector3.forward,
                new List<Transform> { closerButBehind, ahead });

            Assert.AreSame(ahead, picked);
        }

        [Test]
        public void NothingNearTheCrosshairMeansNoLock()
        {
            Transform behind = At(new Vector3(0.0f, 0.0f, -8.0f));

            Transform picked = LockOn.Acquire(Vector3.zero, Vector3.forward,
                new List<Transform> { behind });

            Assert.IsNull(picked, "it locked onto something behind the player");
        }

        [Test]
        public void OnlyThingsInRangeAreCandidates()
        {
            var near = new GameObject("Near").AddComponent<SparringDummy>();
            var far = new GameObject("Far").AddComponent<SparringDummy>();
            _made.Add(near.gameObject);
            _made.Add(far.gameObject);
            near.transform.position = new Vector3(0.0f, 0.0f, 5.0f);
            far.transform.position = new Vector3(0.0f, 0.0f, LockOn.Range + 5.0f);

            List<Transform> candidates = LockOn.Candidates(
                Vector3.zero, Vector3.forward, new IDamageable[] { near, far });

            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(near.transform, candidates[0]);
        }

        private SparringDummy DummyAt(Vector3 position)
        {
            var dummy = new GameObject("Dummy").AddComponent<SparringDummy>();
            _made.Add(dummy.gameObject);
            dummy.transform.position = position;
            dummy.Reset();
            return dummy;
        }

        /// <summary>
        /// Pressing again steps through the enemies in a stable left-to-right
        /// sweep, not nearest-first -- otherwise two presses can bounce
        /// between the same two enemies and never reach the third.
        /// </summary>
        [Test]
        public void CandidatesAreOrderedLeftToRightAroundTheLookDirection()
        {
            SparringDummy right = DummyAt(new Vector3(6.0f, 0.0f, 6.0f));
            SparringDummy left = DummyAt(new Vector3(-6.0f, 0.0f, 9.0f));
            SparringDummy centre = DummyAt(new Vector3(0.0f, 0.0f, 12.0f));

            List<Transform> candidates = LockOn.Candidates(
                Vector3.zero, Vector3.forward, new IDamageable[] { right, centre, left });

            Assert.AreEqual(3, candidates.Count);
            Assert.AreSame(left.transform, candidates[0], "leftmost should come first");
            Assert.AreSame(centre.transform, candidates[1]);
            Assert.AreSame(right.transform, candidates[2], "rightmost should come last");
        }

        [Test]
        public void TheLeftToRightOrderFollowsWhereYouAreLooking()
        {
            SparringDummy east = DummyAt(new Vector3(8.0f, 0.0f, 0.0f));
            SparringDummy north = DummyAt(new Vector3(0.0f, 0.0f, 8.0f));

            // Looking east, north is on the LEFT and east is straight ahead.
            List<Transform> candidates = LockOn.Candidates(
                Vector3.zero, Vector3.right, new IDamageable[] { east, north });

            Assert.AreSame(north.transform, candidates[0]);
            Assert.AreSame(east.transform, candidates[1]);
        }

        [Test]
        public void TheDeadAreNeverCandidates()
        {
            SparringDummy alive = DummyAt(new Vector3(0.0f, 0.0f, 5.0f));
            SparringDummy dead = DummyAt(new Vector3(2.0f, 0.0f, 5.0f));
            ((IDamageable)dead).ApplyHit(dead.MaxHealth * 10.0f, Vector3.zero, 0.0f);
            Assert.IsFalse(dead.IsAlive, "setup: the dummy should be dead");

            List<Transform> candidates = LockOn.Candidates(
                Vector3.zero, Vector3.forward, new IDamageable[] { alive, dead });

            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(alive.transform, candidates[0]);
        }

        /// <summary>
        /// The user's ask, end to end through the pure functions: press to
        /// lock, keep pressing to rotate through every enemy, and the press
        /// after the last one lets go.
        /// </summary>
        [Test]
        public void PressingRepeatedlyVisitsEveryEnemyOnceThenReleases()
        {
            SparringDummy a = DummyAt(new Vector3(-5.0f, 0.0f, 8.0f));
            SparringDummy b = DummyAt(new Vector3(0.0f, 0.0f, 8.0f));
            SparringDummy c = DummyAt(new Vector3(5.0f, 0.0f, 8.0f));
            List<Transform> candidates = LockOn.Candidates(
                Vector3.zero, Vector3.forward, new IDamageable[] { c, a, b });

            // Press 1: locks whoever is dead ahead.
            Transform locked = LockOn.Acquire(Vector3.zero, Vector3.forward, candidates);
            Assert.AreSame(b.transform, locked);

            var visited = new HashSet<Transform> { locked };
            int presses = 1;
            while (locked != null && presses < 10)
            {
                presses++;
                locked = LockOn.Cycle(locked, candidates, presses);
                if (locked != null)
                {
                    visited.Add(locked);
                }
            }

            Assert.AreEqual(3, visited.Count, "some enemy was never reached by cycling");
            Assert.IsNull(locked, "cycling never released");
            Assert.AreEqual(4, presses, "3 enemies should take: lock, cycle, cycle, release");
        }

        /// <summary>
        /// Pressing through the whole list lets go rather than looping. One
        /// button that acquires, cycles and cancels is what makes it usable
        /// mid-fight; a loop leaves no way to stop looking.
        /// </summary>
        [Test]
        public void CyclingPastTheLastTargetReleases()
        {
            Transform a = At(new Vector3(0.0f, 0.0f, 3.0f));
            Transform b = At(new Vector3(0.0f, 0.0f, 4.0f));
            var candidates = new List<Transform> { a, b };

            // Two targets: acquire, cycle to the second, then let go. The
            // visit count is the press number, so releasing at exactly Count
            // would skip the second target entirely.
            Assert.AreSame(b, LockOn.Cycle(a, candidates, 2));
            Assert.IsNull(LockOn.Cycle(b, candidates, 3), "cycling looped instead of releasing");
        }

        /// <summary>
        /// A target that died or left range this very instant is no longer in
        /// the list -- and looking it up must not index with -1.
        /// </summary>
        [Test]
        public void CyclingFromATargetThatHasVanishedFallsBackRatherThanThrowing()
        {
            Transform gone = At(new Vector3(0.0f, 0.0f, 99.0f));
            Transform present = At(new Vector3(0.0f, 0.0f, 3.0f));

            Assert.AreSame(present, LockOn.Cycle(gone, new List<Transform> { present }, 0));
        }

        [Test]
        public void CyclingWithNothingLeftReleases()
        {
            Assert.IsNull(LockOn.Cycle(null, new List<Transform>(), 0));
        }

        [Test]
        public void TheBearingPointsAtTheTarget()
        {
            LockOn.Bearing(Vector3.zero, new Vector3(0.0f, 0.0f, 5.0f),
                out float yaw, out float pitch);
            Assert.AreEqual(0.0f, yaw, 0.01f);
            Assert.AreEqual(0.0f, pitch, 0.01f);

            LockOn.Bearing(Vector3.zero, new Vector3(5.0f, 0.0f, 0.0f), out yaw, out _);
            Assert.AreEqual(90.0f, yaw, 0.01f);
        }

        /// <summary>
        /// THE ONE THAT PUT THE CAMERA ON THE PAVEMENT. A boom camera pitching
        /// up swings its far end down and under the player, so a tall target at
        /// arm's length -- where the horizontal distance shrinks toward zero
        /// and the height does not -- sends the raw angle toward vertical and
        /// the camera under your own feet, looking up at you.
        /// </summary>
        [Test]
        public void ATallTargetAtArmsLengthDoesNotFlipTheCameraUnderTheGround()
        {
            LockOn.Bearing(Vector3.zero, new Vector3(0.0f, 6.0f, 0.2f),
                out _, out float pitch);

            Assert.GreaterOrEqual(pitch, -LockOn.MaxPitchDegrees - 0.01f);
            Assert.LessOrEqual(pitch, LockOn.MaxPitchDegrees + 0.01f);
        }

        [Test]
        public void ATargetInTheSameSpotDoesNotProduceNaN()
        {
            LockOn.Bearing(Vector3.zero, Vector3.zero, out float yaw, out float pitch);

            Assert.IsFalse(float.IsNaN(yaw));
            Assert.IsFalse(float.IsNaN(pitch));
        }
    }
}
