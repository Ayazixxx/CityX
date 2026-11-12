using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The posts under the street stairs (World/StairPosts; owner, 2026-11-12: "walking up this staircase is super glitchy and barely works", "the
    /// camera should ignore the support beams"): none goes up through a flight or a landing of its stair, a tower's stand outside its walk, one
    /// column a place, and the camera knows them.
    /// </summary>
    public sealed class StairPostsTests
    {
        private const int Seed = 48398181;

        private static List<StreetStair> StairsNearSpawn(CityWorld world)
        {
            var stairs = new List<StreetStair>();
            foreach (BuildingSite site in TestSites.NearSpawn(world, 30))
            {
                RoofStair roof = RoofStairs.For(world, site);
                if (roof != null) stairs.Add(roof.Stair);
                foreach (Footbridge b in Footbridges.For(world, site))
                {
                    if (b.SiteA == site.Id && b.Stair != null) stairs.Add(b.Stair);
                }
            }
            Assert.Greater(stairs.Count, 5, "too few street stairs near the spawn to test");
            return stairs;
        }

        /// <summary>Whether a post's square meets a flight's or a landing's footprint, and that walk is lower than the post's top.</summary>
        private static string WalkItGoesThrough(StreetStair stair, StairPost post, bool atAnyHeight)
        {
            var p = new Vector2(post.X, post.Z);
            for (int i = 0; i < stair.Flights.Length; i++)
            {
                StairFlight f = stair.Flights[i];
                if (!atAnyHeight && Mathf.Min(f.From.y, f.To.y) >= post.Top) continue;
                Vector2 a = new Vector2(f.From.x, f.From.z), b = new Vector2(f.To.x, f.To.z);
                Vector2 dir = (b - a).normalized;
                Vector2 d = p - a;
                float along = Vector2.Dot(d, dir), across = Mathf.Abs(dir.x * d.y - dir.y * d.x);
                if (along > -post.Half && along < (b - a).magnitude + post.Half && across < stair.Width * 0.5f + post.Half - 0.01f)
                {
                    return $"flight {i} ({Mathf.Min(f.From.y, f.To.y):F2} m up)";
                }
            }
            for (int i = 0; i < stair.Landings.Length; i++)
            {
                StairLanding l = stair.Landings[i];
                if (!atAnyHeight && l.Centre.y >= post.Top) continue;
                Vector2 d = p - new Vector2(l.Centre.x, l.Centre.z);
                if (Mathf.Abs(Vector2.Dot(d, l.Along)) < l.HalfAlong + post.Half - 0.01f && Mathf.Abs(Vector2.Dot(d, l.Out)) < l.HalfOut + post.Half - 0.01f)
                {
                    return $"landing {i} ({l.Centre.y:F2} m up)";
                }
            }
            return null;
        }

        [Test]
        public void NoPostGoesUpThroughAFlightOrALandingOfItsStair()
        {
            var world = new CityWorld(Seed);
            int posts = 0;
            foreach (StreetStair stair in StairsNearSpawn(world))
            {
                foreach (StairPost post in StairPosts.Of(world, stair))
                {
                    posts++;
                    string through = WalkItGoesThrough(stair, post, false);
                    Assert.IsNull(through, $"the stair of building {stair.Site} on its {stair.Wall} wall has a post at ({post.X:F2}, {post.Z:F2}) " +
                        $"up to {post.Top:F2} m through its {through}");
                }
            }
            Assert.Greater(posts, 20, "the stairs near the spawn have hardly any posts");
        }

        /// <summary>A tower's posts stand clear of every flight and landing of it at any height: nothing to walk into on its steps.</summary>
        [Test]
        public void ATowersPostsStandOutsideWhereItIsWalked()
        {
            var world = new CityWorld(Seed);
            int towers = 0;
            foreach (StreetStair stair in StairsNearSpawn(world))
            {
                if (!stair.Switchback) continue;
                towers++;
                List<StairPost> posts = StairPosts.Of(world, stair);
                Assert.Greater(posts.Count, 0, $"the tower of building {stair.Site} stands on nothing");
                for (int i = 0; i < posts.Count; i++)
                {
                    string on = WalkItGoesThrough(stair, posts[i], true);
                    Assert.IsNull(on, $"a post of the tower of building {stair.Site} at ({posts[i].X:F2}, {posts[i].Z:F2}), up to {posts[i].Top:F2} m, stands in its {on}");
                    for (int j = 0; j < i; j++)
                    {
                        Assert.IsFalse(Mathf.Abs(posts[i].X - posts[j].X) < 0.05f && Mathf.Abs(posts[i].Z - posts[j].Z) < 0.05f,
                            $"two posts of the tower of building {stair.Site} stand in one place: one column up to the highest");
                    }
                }
            }
            Assert.Greater(towers, 3, "too few stair towers near the spawn to test");
        }

        [Test]
        public void TheCameraKnowsAPostFromWhatIsAroundIt()
        {
            var world = new CityWorld(Seed);
            foreach (StreetStair stair in StairsNearSpawn(world))
            {
                if (!stair.Switchback) continue;
                List<StairPost> posts = StairPosts.Of(world, stair);
                if (posts.Count == 0) continue;
                StairPost post = posts[0];
                float y = (post.Bottom + post.Top) * 0.5f;
                Assert.IsTrue(StairPosts.IsPostAt(world, new Vector3(post.X + post.Half, y, post.Z), 0.05f), "a point on a post's face is not on a post");
                Assert.IsFalse(StairPosts.IsPostAt(world, new Vector3(post.X + post.Half + 0.5f, y, post.Z + 0.5f), 0.05f), "half a metre off a post is on one");
                StairLanding landing = stair.Landings[0];
                Assert.IsFalse(StairPosts.IsPostAt(world, landing.Centre + Vector3.up * 1.0f, 0.05f), "over a landing's middle is on a post");
                return;
            }
            Assert.Fail("no stair tower with posts near the spawn");
        }
    }
}
