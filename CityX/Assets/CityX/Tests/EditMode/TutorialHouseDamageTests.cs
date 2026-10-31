using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The record of what is broken in Old Guy's house (World.TutorialHouseDamage): a piece broken here is kept, heard once and told to the other
    /// machines once; one told by another machine is kept and heard but not sent back; a piece the house does not have is ignored.
    /// </summary>
    public sealed class TutorialHouseDamageTests
    {
        private sealed class Recorder : ITutorialHouseRelay
        {
            public readonly List<int> Sent = new List<int>();

            public void Send(int piece) => Sent.Add(piece);
        }

        private TutorialHouseDamage _damage;
        private List<int> _heard;
        private Recorder _relay;

        [SetUp]
        public void SetUp()
        {
            _damage = new TutorialHouseDamage();
            _heard = new List<int>();
            _relay = new Recorder();
            _damage.Relay = _relay;
            _damage.PieceBroken += _heard.Add;
        }

        [Test]
        public void APieceBrokenHereIsKeptHeardAndToldOnce()
        {
            _damage.Break(2);
            _damage.Break(2);
            Assert.IsTrue(_damage.IsBroken(2));
            CollectionAssert.AreEqual(new[] { 2 }, _heard);
            CollectionAssert.AreEqual(new[] { 2 }, _relay.Sent);
        }

        [Test]
        public void APieceBrokenElsewhereIsHeardButNotSentBack()
        {
            _damage.Told(OldGuysHouse.FrontDoorPlanks);
            _damage.Told(OldGuysHouse.FrontDoorPlanks);
            Assert.IsTrue(_damage.IsBroken(OldGuysHouse.FrontDoorPlanks));
            CollectionAssert.AreEqual(new[] { OldGuysHouse.FrontDoorPlanks }, _heard);
            Assert.IsEmpty(_relay.Sent);
            _damage.Break(OldGuysHouse.FrontDoorPlanks);
            Assert.IsEmpty(_relay.Sent, "a piece already broken was told again");
        }

        [Test]
        public void APieceTheHouseDoesNotHaveIsIgnored()
        {
            _damage.Told(-1);
            _damage.Break(OldGuysHouse.BreakableCount);
            Assert.IsEmpty(_heard);
            Assert.IsEmpty(_damage.All());
        }
    }
}
