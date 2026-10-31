using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The poured stream (docs/FIRE.md): one tube from the spout to the landing along the fall, thin, wavering, and flowing with time.</summary>
    public sealed class PourStreamTests
    {
        private static readonly Vector3 Spout = new Vector3(0.3f, 1.2f, 0.4f);
        private static readonly Vector3 Landing = new Vector3(0.2f, 0.0f, 1.6f);
        private const float Seconds = 0.4f;

        [Test]
        public void TheFallLeavesTheSpoutAndEndsAtTheLandingArchingOverTheStraightLine()
        {
            Assert.Less(Vector3.Distance(PourStream.PointOn(Spout, Landing, Seconds, 0.0f), Spout), 1e-5f);
            Assert.Less(Vector3.Distance(PourStream.PointOn(Spout, Landing, Seconds, 1.0f), Landing), 1e-4f);
            Vector3 middle = PourStream.PointOn(Spout, Landing, Seconds, 0.5f);
            // Gravity bends the fall down ever more steeply: it leaves the spout flatter than the straight line and arches over it.
            Assert.Greater(middle.y, (Spout.y + Landing.y) * 0.5f, "a poured stream arches over the straight line from spout to landing");
        }

        [Test]
        public void TheTubeFollowsTheFallAndThinsTowardsTheLanding()
        {
            var stream = new PourStream();
            stream.Fill(Spout, Landing, Seconds, 0.0f);
            float allowed = PourStream.SpoutRadius * (1.0f + PourStream.WobbleShare) + PourStream.SwayMetres + 1e-4f;
            for (int ring = 0; ring <= PourStream.Rings; ring++)
            {
                Vector3 on = PourStream.PointOn(Spout, Landing, Seconds, ring / (float)PourStream.Rings);
                for (int side = 0; side <= PourStream.Sides; side++)
                {
                    Vector3 v = stream.Vertices[ring * (PourStream.Sides + 1) + side];
                    Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), "a vertex is not a number");
                    Assert.Less(Vector3.Distance(v, on), allowed, $"ring {ring} side {side} strays from the fall");
                }
            }
            Assert.Less(Vector3.Distance(RingCentre(stream, 0), Spout), 0.01f, "the tube does not start at the spout");
            Assert.Less(Vector3.Distance(RingCentre(stream, PourStream.Rings), Landing), PourStream.SwayMetres + 0.01f, "the tube does not end at the landing");
            Assert.Less(RingRadius(stream, PourStream.Rings), RingRadius(stream, 0), "the stream should thin as it falls");
            foreach (int index in stream.Triangles)
            {
                Assert.That(index, Is.InRange(0, PourStream.VertexCount - 1));
            }
        }

        [Test]
        public void TheWaveRunsDownTheStreamWithTime()
        {
            var stream = new PourStream();
            stream.Fill(Spout, Landing, Seconds, 0.0f);
            float before = RingRadius(stream, 6);
            stream.Fill(Spout, Landing, Seconds, 1.0f / (PourStream.FlowPerSecond * 4.0f));   // a quarter wave later
            Assert.AreNotEqual(before, RingRadius(stream, 6), "the stream does not flow");
            Assert.AreEqual(PourStream.RadiusAt(0.0f, 0.0f), PourStream.RadiusAt(0.0f, 1.0f / PourStream.FlowPerSecond), 1e-5f, "a whole wave later it is the same again");
            Assert.That(PourStream.RadiusAt(0.37f, 0.21f), Is.InRange(PourStream.LandingRadius * (1.0f - PourStream.WobbleShare), PourStream.SpoutRadius * (1.0f + PourStream.WobbleShare)));
        }

        private static Vector3 RingCentre(PourStream stream, int ring)
        {
            Vector3 sum = Vector3.zero;
            for (int side = 0; side < PourStream.Sides; side++) sum += stream.Vertices[ring * (PourStream.Sides + 1) + side];
            return sum / PourStream.Sides;
        }

        private static float RingRadius(PourStream stream, int ring) => Vector3.Distance(stream.Vertices[ring * (PourStream.Sides + 1)], RingCentre(stream, ring));
    }
}
