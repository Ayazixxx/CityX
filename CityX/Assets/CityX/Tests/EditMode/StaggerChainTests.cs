using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    public sealed class StaggerChainTests
    {
        private const float Stun = 0.7f;

        [Test]
        public void FourStaggersInARowLandThenTheNextAreShruggedOffUntilASecondAfterTheLastStun()
        {
            var chain = new StaggerChain();
            float t = 0.0f;
            for (int i = 0; i < StaggerChain.MostInARow; i++, t += 0.5f)
            {
                Assert.IsTrue(chain.TryStagger(t, Stun), $"stagger {i + 1} of a chain should still land");
            }
            float lastStunEnds = 1.5f + Stun;
            Assert.IsFalse(chain.TryStagger(t, Stun), "the fifth blow in a row should not stagger");
            Assert.IsTrue(chain.ShruggingAt(lastStunEnds + StaggerChain.ShrugSeconds - 0.01f));
            Assert.IsTrue(chain.TryStagger(lastStunEnds + StaggerChain.ShrugSeconds + 0.01f, Stun), "after the shrug it staggers again");
            Assert.AreEqual(1, chain.InARow, "and starts a new chain");
        }

        [Test]
        public void BlowsSpacedPastTheStunAndItsGraceNeverMakeAChain()
        {
            var chain = new StaggerChain();
            float gap = Stun + StaggerChain.InARowGraceSeconds + 0.05f;
            for (int i = 0; i < 12; i++)
            {
                Assert.IsTrue(chain.TryStagger(i * gap, Stun), $"blow {i + 1}, given time to recover, should stagger");
                Assert.AreEqual(1, chain.InARow);
            }
        }

        [Test]
        public void AShortStunInsideALongOneDoesNotCutTheChainShort()
        {
            var chain = new StaggerChain();
            Assert.IsTrue(chain.TryStagger(0.0f, 1.5f));
            Assert.IsTrue(chain.TryStagger(0.2f, 0.1f));
            Assert.IsTrue(chain.TryStagger(1.6f, 0.5f), "1.6 s is inside the first blow's stun and its grace");
            Assert.AreEqual(3, chain.InARow);
        }

        [Test]
        public void ResetForgetsTheChainAndTheShrug()
        {
            var chain = new StaggerChain();
            for (int i = 0; i < StaggerChain.MostInARow; i++)
            {
                chain.TryStagger(i * 0.3f, Stun);
            }
            Assert.IsTrue(chain.ShruggingAt(1.0f));
            chain.Reset();
            Assert.IsFalse(chain.ShruggingAt(1.0f));
            Assert.IsTrue(chain.TryStagger(1.0f, Stun));
        }
    }
}
