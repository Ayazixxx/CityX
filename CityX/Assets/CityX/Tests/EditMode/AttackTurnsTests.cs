using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>Whose turn it is to attack (owner, 2026-10-24: enemies "dont just both push you at the same time completely ignoring eachother").</summary>
    public sealed class AttackTurnsTests
    {
        private readonly object _player = new object();
        private readonly object _a = new object();
        private readonly object _b = new object();
        private readonly object _c = new object();

        [Test]
        public void OneSwingsAtATimeAndTwoShoot()
        {
            var turns = new AttackTurns();
            Assert.IsTrue(turns.Ask(_a, _player, TurnKind.Melee, 0.0f));
            Assert.IsFalse(turns.Ask(_b, _player, TurnKind.Melee, 0.0f), "two swung at once");
            Assert.IsTrue(turns.Ask(_a, _player, TurnKind.Melee, 0.1f), "the one swinging lost its turn");

            Assert.IsTrue(turns.Ask(_b, _player, TurnKind.Ranged, 0.2f));
            Assert.IsTrue(turns.Ask(_c, _player, TurnKind.Ranged, 0.2f));
            Assert.AreEqual(2, turns.Holders(_player, TurnKind.Ranged));
            Assert.AreEqual(1, turns.Holders(_player, TurnKind.Melee));
        }

        [Test]
        public void AGivenBackTurnGoesToTheNextInLineWhileTheGiverRests()
        {
            var turns = new AttackTurns();
            Assert.IsTrue(turns.Ask(_a, _player, TurnKind.Melee, 0.0f));
            Assert.IsFalse(turns.Ask(_b, _player, TurnKind.Melee, 0.1f));
            Assert.IsFalse(turns.Ask(_c, _player, TurnKind.Melee, 0.2f));
            turns.GiveBack(_a, 0.3f);
            Assert.IsFalse(turns.Ask(_a, _player, TurnKind.Melee, 0.3f), "the giver took it straight back while others waited");
            Assert.IsFalse(turns.Ask(_c, _player, TurnKind.Melee, 0.3f), "the later asker went first");
            Assert.IsTrue(turns.Ask(_b, _player, TurnKind.Melee, 0.3f), "the first in line did not get it");
        }

        [Test]
        public void ALoneAttackerIsNeverKeptWaiting()
        {
            var turns = new AttackTurns();
            Assert.IsTrue(turns.Ask(_a, _player, TurnKind.Melee, 0.0f));
            turns.GiveBack(_a, 0.5f);
            Assert.IsTrue(turns.Ask(_a, _player, TurnKind.Melee, 0.5f), "alone, it had to rest before attacking again");
        }

        [Test]
        public void ATurnIsForgottenWhenItsHolderStopsAskingAndTakenBackWhenHeldTooLong()
        {
            var turns = new AttackTurns();
            Assert.IsTrue(turns.Ask(_a, _player, TurnKind.Melee, 0.0f));
            Assert.IsFalse(turns.Ask(_b, _player, TurnKind.Melee, 0.1f));
            Assert.IsTrue(turns.Ask(_b, _player, TurnKind.Melee, 0.1f + AttackTurns.ForgetSeconds + 0.1f), "a holder that went quiet kept its turn");

            var held = new AttackTurns();
            float t = 0.0f;
            for (; t <= AttackTurns.LongestTurnSeconds; t += 0.1f)
            {
                Assert.IsTrue(held.Ask(_a, _player, TurnKind.Melee, t));
                held.Ask(_b, _player, TurnKind.Melee, t);
            }
            Assert.IsFalse(held.Ask(_a, _player, TurnKind.Melee, t + 0.1f), "a turn was held past the longest");
            Assert.IsTrue(held.Ask(_b, _player, TurnKind.Melee, t + 0.1f));
        }

        [Test]
        public void EachTargetHasItsOwnTurnsAndLeavingFreesOne()
        {
            var turns = new AttackTurns();
            var other = new object();
            Assert.IsTrue(turns.Ask(_a, _player, TurnKind.Melee, 0.0f));
            Assert.IsTrue(turns.Ask(_b, other, TurnKind.Melee, 0.0f), "another player's turn was taken by the first's attacker");
            turns.Leave(_a);
            Assert.IsTrue(turns.Ask(_c, _player, TurnKind.Melee, 0.1f), "a departed attacker's turn was not freed");
        }
    }
}
