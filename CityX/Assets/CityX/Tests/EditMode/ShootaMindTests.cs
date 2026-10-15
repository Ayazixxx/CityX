using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// THE SHOOTA'S HEAD (owner, 2026-10-15: "if you are nearby and in sight of him he starts shooting at you but also runs to cover if he is
    /// getting damaged and when he needs to reload"), and how he picks cover. Pure: the senses are made up frame by frame.
    /// </summary>
    public sealed class ShootaMindTests
    {
        private const float Frame = 1.0f / 60.0f;

        private static ShootaMind.Senses Seeing(int magazine = 30) => new ShootaMind.Senses
        {
            HasTarget = true, SeesTarget = true, TargetDistance = 15.0f, Magazine = magazine, MagazineSize = 30,
        };

        private static void Run(ShootaMind mind, ShootaMind.Senses senses, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Frame)
            {
                mind.Tick(senses, Frame);
            }
        }

        [Test]
        public void HeOpensFireABeatAfterSeeingYouInBurstsWithPausesBetween()
        {
            var mind = new ShootaMind(7);
            Assert.AreEqual(ShootaState.Idle, mind.Tick(new ShootaMind.Senses { HasTarget = true, Magazine = 30, MagazineSize = 30 }, Frame));
            Assert.AreEqual(ShootaState.Engage, mind.Tick(Seeing(), Frame), "seeing you, he engages");
            bool firedEarly = false;
            for (float t = 0.0f; t < ShootaMind.ReactionSeconds - 0.05f; t += Frame)
            {
                mind.Tick(Seeing(), Frame);
                firedEarly |= mind.ShouldFire;
            }
            Assert.IsFalse(firedEarly, "no shot before he has had a moment to react");

            // A burst: the trigger stays pulled while he fires its rounds, then lets go for a pause.
            int rounds = 0;
            int pauses = 0;
            bool wasFiring = false;
            for (int frame = 0; frame < 600; frame++)
            {
                mind.Tick(Seeing(), Frame);
                if (mind.ShouldFire && frame % 6 == 0)
                {
                    mind.Fired();   // the gun fires at 10 a second
                    rounds++;
                }
                if (wasFiring && !mind.ShouldFire)
                {
                    pauses++;
                }
                wasFiring = mind.ShouldFire;
            }
            Assert.Greater(rounds, 10, "he hardly fired");
            Assert.Greater(pauses, 2, "he never paused between bursts");
        }

        [Test]
        public void OutOfRoundsHeRunsForCoverReloadsThereStaysDownABeatThenLeansOut()
        {
            var mind = new ShootaMind(3);
            Run(mind, Seeing(), 1.0f);
            ShootaMind.Senses dry = Seeing(0);
            dry.HasCover = true;
            Assert.AreEqual(ShootaState.TakeCover, mind.Tick(dry, Frame), "dry with cover near: he runs for it");
            Assert.IsTrue(mind.CoverForReload);

            dry.AtCover = true;
            Assert.AreEqual(ShootaState.Reload, mind.Tick(dry, Frame), "in cover he changes the magazine");

            dry.Reloading = true;
            Run(mind, dry, 2.0f);
            Assert.AreEqual(ShootaState.Reload, mind.State, "still reloading");

            ShootaMind.Senses full = dry;
            full.Reloading = false;
            full.Magazine = 30;
            Run(mind, full, ShootaMind.CoverStaySeconds * 0.5f);
            Assert.AreEqual(ShootaState.Reload, mind.State, "he stays down a beat after reloading");
            Run(mind, full, ShootaMind.CoverStaySeconds);
            Assert.AreEqual(ShootaState.Engage, mind.State, "then leans back out to fight");
        }

        [Test]
        public void HurtHeTakesCoverButDoesNotDuckAtEveryHitAndWithNoCoverHeFightsOn()
        {
            var mind = new ShootaMind(5);
            Run(mind, Seeing(), 1.0f);
            ShootaMind.Senses hit = Seeing(20);
            hit.HasCover = true;
            hit.TookDamage = true;
            Assert.AreEqual(ShootaState.TakeCover, mind.Tick(hit, Frame), "a hit sends him for cover");
            Assert.IsFalse(mind.CoverForReload);

            ShootaMind.Senses inCover = Seeing(20);
            inCover.HasCover = true;
            inCover.AtCover = true;
            Run(mind, inCover, ShootaMind.CoverStaySeconds + 0.2f);
            Assert.AreEqual(ShootaState.Engage, mind.State, "unhurt for a while in cover, he comes back out");

            ShootaMind.Senses hitAgain = inCover;
            hitAgain.AtCover = false;
            hitAgain.TookDamage = true;
            Assert.AreEqual(ShootaState.Engage, mind.Tick(hitAgain, Frame), "just out of cover, a hit does not send him straight back");

            var exposed = new ShootaMind(9);
            Run(exposed, Seeing(), 1.0f);
            ShootaMind.Senses nowhere = Seeing(20);
            nowhere.TookDamage = true;
            Assert.AreEqual(ShootaState.Engage, exposed.Tick(nowhere, Frame), "no cover anywhere: he fights on");
            ShootaMind.Senses dryNowhere = Seeing(0);
            Assert.AreEqual(ShootaState.Reload, exposed.Tick(dryNowhere, Frame), "dry with no cover: he reloads where he stands");
        }

        [Test]
        public void LosingYouHeGoesWhereYouWereLastSeenThenGivesUp()
        {
            var mind = new ShootaMind(11);
            Run(mind, Seeing(), 1.0f);
            ShootaMind.Senses gone = Seeing();
            gone.SeesTarget = false;
            Run(mind, gone, ShootaMind.LoseSightSeconds + 0.1f);
            Assert.AreEqual(ShootaState.Chase, mind.State, "out of sight a while: he goes looking");
            Assert.AreEqual(ShootaState.Engage, mind.Tick(Seeing(), Frame), "seeing you again, he fights");
            Run(mind, gone, ShootaMind.LoseSightSeconds + ShootaMind.GiveUpSeconds + 0.5f);
            Assert.AreEqual(ShootaState.Idle, mind.State, "and in the end he gives up");
        }

        [Test]
        public void HisAimIsWorseFurtherOffAgainstAMovingTargetAndAsABurstClimbs()
        {
            Assert.Less(ShootaMind.SpreadDegrees(10.0f, 0.0f, 0), ShootaMind.SpreadDegrees(40.0f, 0.0f, 0));
            Assert.Less(ShootaMind.SpreadDegrees(20.0f, 0.0f, 0), ShootaMind.SpreadDegrees(20.0f, 7.0f, 0));
            Assert.Less(ShootaMind.SpreadDegrees(20.0f, 0.0f, 0), ShootaMind.SpreadDegrees(20.0f, 0.0f, 5));
            Assert.LessOrEqual(ShootaMind.SpreadDegrees(1000.0f, 50.0f, 50), 9.0f, "never wild");
            Assert.Less(ShootaMind.SpreadDegrees(15.0f, 0.0f, 0), 2.5f, "a standing target at 15 m is mostly hit");
        }

        // --- cover, in a made-up world: flat ground and one crate between the threat and the fighter's left ---

        private static readonly Bounds Crate = new Bounds(new Vector3(-4.0f, 1.0f, 3.0f), new Vector3(3.0f, 2.0f, 1.0f));

        private static bool BlockedByCrate(Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            float length = along.magnitude;
            return length > 1e-4f && Crate.IntersectRay(new Ray(from, along / length), out float distance) && distance <= length;
        }

        private static bool FlatGround(Vector3 above, out Vector3 point)
        {
            point = new Vector3(above.x, 0.0f, above.z);
            return !Crate.Contains(new Vector3(above.x, 0.5f, above.z));
        }

        [Test]
        public void CoverIsTheNearestSpotBehindSomethingSolidThatHeCanRunStraightTo()
        {
            Vector3 fighter = Vector3.zero;
            Vector3 threatEye = new Vector3(-4.0f, 2.4f, 20.0f);
            Assert.IsTrue(CoverPicker.TryPick(fighter, 2.6f, threatEye, BlockedByCrate, FlatGround, out Vector3 cover), "no cover found");
            Assert.IsTrue(CoverPicker.IsHidden(cover, 2.6f, threatEye, BlockedByCrate), "the spot can be seen from the threat");
            Assert.Less(cover.z, Crate.min.z, "the spot is on the far side of the crate from the threat");
            Assert.Less(Vector3.Distance(cover, fighter), 8.0f, "not the nearest cover");
            Assert.Less(Mathf.Abs(cover.x - Crate.center.x), Crate.extents.x + 1.0f, "tucked in behind the crate, not out in its shadow");

            Assert.IsFalse(CoverPicker.TryPick(fighter, 2.6f, new Vector3(30.0f, 2.4f, -20.0f), BlockedByCrate, FlatGround, out _),
                "with the threat on the crate's own side there is nowhere to hide");
        }

        // --- a city block's corner: the fighter on the pavement beside it, the threat up the street ---

        private static readonly Bounds Block = new Bounds(new Vector3(-10.5f, 15.0f, 15.0f), new Vector3(19.0f, 30.0f, 30.0f));

        private static bool BlockedByBlock(Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            float length = along.magnitude;
            return length > 1e-4f && Block.IntersectRay(new Ray(from, along / length), out float distance) && distance <= length;
        }

        private static bool StreetGround(Vector3 above, out Vector3 point)
        {
            point = new Vector3(above.x, 0.0f, above.z);
            return !Block.Contains(new Vector3(above.x, 0.5f, above.z));
        }

        [Test]
        public void InTheCityHeTakesCoverRoundTheCornerOfABlockByWayOfTheCorner()
        {
            Vector3 fighter = new Vector3(0.0f, 0.0f, 2.0f);
            Vector3 threatEye = new Vector3(0.0f, 2.4f, 40.0f);
            Assert.IsTrue(CoverPicker.TryPick(fighter, 2.6f, threatEye, BlockedByBlock, StreetGround, out Vector3 cover, out Vector3 via),
                "no cover round the corner");
            Assert.IsTrue(CoverPicker.IsHidden(cover, 2.6f, threatEye, BlockedByBlock), "the spot can be seen up the street");
            Assert.Less(cover.x, Block.max.x, "the spot is not round the corner");
            Assert.AreNotEqual(cover, via, "no straight way round a corner: he should turn at one");
            Assert.IsFalse(BlockedByBlock(fighter + Vector3.up, via + Vector3.up), "he cannot run straight to the turn");
            Assert.IsFalse(BlockedByBlock(via + Vector3.up, cover + Vector3.up), "he cannot run from the turn to the spot");
            Assert.LessOrEqual(CoverPicker.LastQueries, CoverPicker.MaxQueries, "the search asked too much");
        }
    }
}
