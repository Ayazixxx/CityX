using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What keeps a held staff honest (StreamFeed, StreamTicks, SpellTicks.Rest): a stream that ran dry stays off until a second of its mana
    /// is back, and letting go and starting again never brings a tick forward. On the real staffs: HeldStreamManaPlayTests.
    /// </summary>
    public sealed class HeldStreamTests
    {
        private const float Step = 1.0f / 60.0f;
        private const float Cost = 6.0f;

        private GameObject _body;
        private PlayerVitals _vitals;

        private sealed class Body : IDamageable
        {
            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds) => HitResponse.Landed;
        }

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _vitals = _body.AddComponent<PlayerVitals>();
            _vitals.Revive();   // (EditMode never runs Awake: this fills the bars)
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_body);
        }

        [Test]
        public void AFedStreamTakesItsManaAFrameAtATime()
        {
            var feed = new StreamFeed();
            float before = _vitals.Mana;
            for (int i = 0; i < 60; i++)
            {
                Assert.IsTrue(feed.Draw(_vitals, Cost, Step), "a stream with a full bar was not fed");
            }
            Assert.AreEqual(before - Cost, _vitals.Mana, 0.01f, "a second of the stream did not cost a second of its mana");
            Assert.IsFalse(feed.IsDry);
        }

        [Test]
        public void ABodyWithoutVitalsPaysNothing()
        {
            var feed = new StreamFeed();
            Assert.IsTrue(feed.Draw(null, Cost, Step));
            Assert.IsTrue(feed.Ready(null, Cost, Step));
        }

        /// <summary>The bug this is for: mana trickling back started an empty stream again every few frames, each start landing a tick at once.</summary>
        [Test]
        public void RunDryItStaysOffUntilASecondOfItsManaIsBackThenRunsInOneStretch()
        {
            var feed = new StreamFeed();
            _vitals.TrySpendMana(_vitals.Mana);
            int starts = 0;
            bool running = false;
            for (int i = 0; i < 600; i++)   // ten seconds with the click kept down
            {
                _vitals.TickMana(Step);
                float mana = _vitals.Mana;
                bool fed = feed.Draw(_vitals, Cost, Step);
                if (fed && !running)
                {
                    starts++;
                    Assert.GreaterOrEqual(mana, Cost * StreamFeed.RestartSeconds - 1e-3f, "it ran again before its mana was back");
                }
                running = fed;
            }
            // Mana comes back at 4 a second and the stream drinks 6: a second and a half to wait, three seconds of stream, and again.
            Assert.GreaterOrEqual(starts, 1, "its mana back, it never ran again");
            Assert.LessOrEqual(starts, 3, "it stuttered on and off");
        }

        [Test]
        public void LettingGoDoesNotBringADryStreamBack()
        {
            var feed = new StreamFeed();
            _vitals.TrySpendMana(_vitals.Mana);
            Assert.IsFalse(feed.Draw(_vitals, Cost, Step));
            Assert.IsTrue(feed.IsDry);
            // Half a second with the click up (nothing is drawn), then pressed again: still not a second's worth.
            for (int i = 0; i < 30; i++)
            {
                _vitals.TickMana(Step);
            }
            Assert.IsFalse(feed.Ready(_vitals, Cost, Step), "pressing the click again started a stream that had run dry");
            Assert.IsFalse(feed.Draw(_vitals, Cost, Step));
        }

        [Test]
        public void FindingTooLittleSpendsNothing()
        {
            var feed = new StreamFeed();
            _vitals.TrySpendMana(_vitals.Mana - 0.05f);
            float before = _vitals.Mana;
            Assert.IsFalse(feed.Draw(_vitals, Cost, Step), "it ran a frame it could not pay for");
            Assert.AreEqual(before, _vitals.Mana, 1e-5f);
        }

        [Test]
        public void ABodysNextTickOutlastsTheStream()
        {
            var ticks = new StreamTicks();
            var body = new Body();
            var other = new Body();
            Assert.IsTrue(ticks.Due(body, 10.0f, 0.3f), "the first tick lands at once");
            Assert.IsFalse(ticks.Due(body, 10.1f, 0.3f), "a second tick landed before its time");
            Assert.IsTrue(ticks.Due(other, 10.1f, 0.3f), "each body has its own beat");

            // The stream stops and starts again a moment later: who is still waiting for a tick goes on waiting.
            ticks.Forget(10.2f);
            Assert.AreEqual(2, ticks.Waiting);
            Assert.IsFalse(ticks.Due(body, 10.2f, 0.3f), "starting the stream again brought the tick forward");
            Assert.IsTrue(ticks.Due(body, 10.3f, 0.3f));

            // Long after, nobody is kept.
            ticks.Forget(20.0f);
            Assert.AreEqual(0, ticks.Waiting, "bodies whose tick came due long ago were kept for ever");
        }

        [Test]
        public void ARestingClockRunsOnButNoFurtherThanOneTick()
        {
            const float Tick = 0.2f;
            float since = Tick;
            Assert.AreEqual(1, SpellTicks.Due(ref since, Step, Tick), "the first tick lands at once");

            // Off for a frame and on again, thirty times a second: no oftener than the tick.
            int landed = 0;
            for (int i = 0; i < 120; i++)
            {
                if (i % 2 == 0)
                {
                    landed += SpellTicks.Due(ref since, Step, Tick);
                }
                else
                {
                    SpellTicks.Rest(ref since, Step, Tick);
                }
            }
            Assert.AreEqual(2.0f / Tick, landed, 1.0f, "two seconds of tapping should land two seconds of ticks");

            // Left off for a long while, the next start lands one tick at once, not all the ticks missed.
            for (int i = 0; i < 600; i++)
            {
                SpellTicks.Rest(ref since, Step, Tick);
            }
            Assert.AreEqual(1, SpellTicks.Due(ref since, Step, Tick));
        }
    }
}
