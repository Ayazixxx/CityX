using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The creator's zoom toward the mouse (docs/CHARACTER_CREATOR.md): what is under the mouse stays there, and zooming out brings the whole body back.</summary>
    public sealed class CreatorZoomTests
    {
        private static readonly Vector3 Home = new Vector3(0.0f, 1.6f, 4.0f);
        private const float Nearest = 0.9f, Farthest = 8.0f;

        private static Vector3 In(Vector3 camera, Vector3 focus) => CreatorZoom.Step(camera, focus, true, Home, Nearest, Farthest);

        private static Vector3 Out(Vector3 camera, Vector3 focus) => CreatorZoom.Step(camera, focus, false, Home, Nearest, Farthest);

        [Test]
        public void ZoomingAtAPointOffToASideOnlyRisesSinksAndComesIn()
        {
            Vector3 hand = new Vector3(1.3f, 2.2f, 0.0f);
            Vector3 camera = Home;
            for (int i = 0; i < 12; i++)
            {
                camera = In(camera, CreatorZoom.UpAndDownOnly(hand, Home));
            }
            Assert.AreEqual(Home.x, camera.x, 1e-4f, "the camera slid off to the side");
            Assert.Greater(camera.y, Home.y, "it did not rise toward a point above");
            Assert.Less(camera.z, Home.z, "it did not come in");
        }

        [TestCase(0.0f, 0.1f, 0.0f, TestName = "The feet")]
        [TestCase(0.0f, 2.5f, 0.3f, TestName = "The head")]
        [TestCase(1.25f, 1.9f, 0.0f, TestName = "A hand")]
        public void WhatIsUnderTheMouseStaysAlongTheSameLine(float x, float y, float z)
        {
            var focus = new Vector3(x, y, z);
            Vector3 way = (focus - Home).normalized;
            Vector3 camera = Home;
            for (int i = 0; i < 30; i++)
            {
                Vector3 next = In(camera, focus);
                Assert.LessOrEqual(Vector3.Distance(next, focus), Vector3.Distance(camera, focus) + 1e-4f, "a notch in went further away");
                camera = next;
                Assert.Less(Vector3.Angle((focus - camera).normalized, way), 0.5f, $"notch {i} slid off the line: the part moved on the screen");
            }
            Assert.Less(Vector3.Distance(camera, focus), Vector3.Distance(Home, focus) * 0.5f, "it barely came in");
        }

        [Test]
        public void ItStopsShortOfThePartAndAtItsNearestDepth()
        {
            var nose = new Vector3(0.0f, 2.2f, 0.4f);
            Vector3 camera = Home;
            for (int i = 0; i < 60; i++)
            {
                camera = In(camera, nose);
            }
            Assert.GreaterOrEqual(Vector3.Distance(camera, nose), CreatorZoom.NearestToFocusMetres - 1e-3f, "into the face");
            Assert.GreaterOrEqual(camera.z, Nearest - 1e-3f, "past the nearest depth");
        }

        [Test]
        public void ZoomingOutWhereverTheMouseIsComesBackToTheMiddle()
        {
            Vector3 camera = Home;
            for (int i = 0; i < 20; i++)
            {
                camera = In(camera, new Vector3(0.0f, 0.1f, 0.0f));
            }
            Assert.Less(camera.y, Home.y - 0.5f, "it did not go down to the feet");

            var elsewhere = new Vector3(2.0f, 3.0f, 0.0f);
            for (int i = 0; i < 60; i++)
            {
                camera = Out(camera, elsewhere);
            }
            Assert.AreEqual(Home.x, camera.x, 1e-3f);
            Assert.AreEqual(Home.y, camera.y, 1e-3f);
            Assert.AreEqual(Farthest, camera.z, 1e-3f, "it never reached its farthest");
        }

        [Test]
        public void ANotchWorksFromTheOtherSideOfTheBodyToo()
        {
            var home = new Vector3(0.0f, 1.6f, -4.0f);
            Vector3 camera = CreatorZoom.Step(home, new Vector3(0.0f, 2.2f, 0.0f), true, home, Nearest, Farthest);
            Assert.Greater(camera.z, home.z, "it went away from the body");
            Assert.Less(camera.z, 0.0f, "it went through to the other side");
        }

        [Test]
        public void KeptHoldsTheDepthAndPullsInTheOffset()
        {
            Assert.AreEqual(Nearest, CreatorZoom.Kept(new Vector3(0.0f, 1.6f, 0.2f), Home, Nearest, Farthest).z, 1e-4f);
            Assert.AreEqual(Farthest, CreatorZoom.Kept(new Vector3(0.0f, 1.6f, 20.0f), Home, Nearest, Farthest).z, 1e-4f);
            Assert.AreEqual(Home.y, CreatorZoom.Kept(new Vector3(0.0f, 0.2f, 4.0f), Home, Nearest, Farthest).y, 1e-4f, "off the middle at its starting distance");
        }
    }
}
