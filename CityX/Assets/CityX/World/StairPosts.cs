using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>One post holding up a street stair: a square column from <see cref="Bottom"/> to <see cref="Top"/> (world), <see cref="Half"/> either side of its middle.</summary>
    public readonly struct StairPost
    {
        public readonly float X, Z, Bottom, Top, Half;

        public StairPost(float x, float z, float bottom, float top, float half)
        {
            X = x;
            Z = z;
            Bottom = bottom;
            Top = top;
            Half = half;
        }

        /// <summary>Whether <paramref name="p"/> is in it, or within <paramref name="margin"/> of it.</summary>
        public bool Contains(Vector3 p, float margin)
        {
            float h = Half + margin;
            return Mathf.Abs(p.x - X) <= h && Mathf.Abs(p.z - Z) <= h && p.y >= Bottom - margin && p.y <= Top + margin;
        }
    }

    /// <summary>
    /// THE POSTS UNDER A STREET STAIR (Footbridges.StreetStair: a bridge's stair down to the street, a building's up to its roof): under each
    /// flight's middle on its open side and under each landing's outer corners, down to the ground.
    ///
    /// A zig-zag tower's flights and landings stand one over another, so a post under one went up through every flight and landing under it, a
    /// column in the middle of the steps a body walked into, sprinting up, and stuck on (owner, 2026-11-12: "walking up this staircase is super
    /// glitchy and barely works"). A tower's posts stand OUTSIDE its open side instead, one column from the ground to the highest landing at each
    /// place, and no post goes up through anything walked on. The camera passes through them (<see cref="IsPostAt"/>; owner: "the camera should
    /// ignore the support beams").
    /// </summary>
    public static class StairPosts
    {
        /// <summary>How far in from a straight run's open edge its posts stand, metres (it has nothing under it to go through).</summary>
        private const float StraightInset = 0.15f;

        /// <summary>Half a post's width: a tower's, a straight run's.</summary>
        public const float TowerHalf = 0.12f;

        public const float StraightHalf = 0.1f;

        /// <summary>A post this little over the paving is not drawn: the landing on the paving stands on it.</summary>
        private const float ShortestPost = 0.9f;

        /// <summary>A post this little into a walk's edge only touches it, metres: a tower's posts stand against its open side.</summary>
        private const float Touching = 0.02f;

        /// <summary>How far under the paving a post's foot goes, so it never shows a gap on a slope.</summary>
        private const float Sunk = 0.3f;

        /// <summary>The stair's posts.</summary>
        public static List<StairPost> Of(CityWorld world, StreetStair stair)
        {
            float half = stair.Width * 0.5f;
            float post = stair.Switchback ? TowerHalf : StraightHalf;
            // Out from the open edge's line: inside it on a straight run, outside it on a tower.
            float edge = stair.Switchback ? half + post : half - StraightInset;
            var tops = new List<Vector3>();   // x, the underside's height, z
            foreach (StairFlight f in stair.Flights)
            {
                Vector3 mid = (f.From + f.To) * 0.5f;
                Vector2 o = stair.Out * edge;
                tops.Add(new Vector3(mid.x + o.x, Mathf.Min(f.From.y, f.To.y) - Footbridges.DeckThickness, mid.z + o.y));
            }
            foreach (StairLanding l in stair.Landings)
            {
                float outward = stair.Switchback ? l.HalfOut + post : l.HalfOut - StraightInset;
                foreach (float s in new[] { -1.0f, 1.0f })
                {
                    Vector2 corner = new Vector2(l.Centre.x, l.Centre.z) + l.Along * (s * (l.HalfAlong - StraightInset)) + l.Out * outward;
                    tops.Add(new Vector3(corner.x, l.Centre.y - Footbridges.DeckThickness, corner.y));
                }
            }

            var posts = new List<StairPost>();
            foreach (Vector3 t in tops)
            {
                float ground = (float)world.Plan.GroundHeight(t.x, t.z) - Sunk;
                if (t.y - ground < ShortestPost || InAWalk(stair, t, post))
                {
                    continue;
                }
                // Posts in one place (a tower's landings over one another) are one column, up to the highest.
                int same = posts.FindIndex(p => Mathf.Abs(p.X - t.x) < 0.05f && Mathf.Abs(p.Z - t.z) < 0.05f);
                if (same >= 0)
                {
                    if (posts[same].Top < t.y) posts[same] = new StairPost(t.x, t.z, ground, t.y, post);
                    continue;
                }
                posts.Add(new StairPost(t.x, t.z, ground, t.y, post));
            }
            return posts;
        }

        /// <summary>
        /// Whether a post up to <paramref name="top"/> there would stand in a flight or a landing of the stair: on a straight run one lower than
        /// its top (it goes up through it); on a tower any (a flight over it stood on it, a post in the other lane under it).
        /// </summary>
        private static bool InAWalk(StreetStair stair, Vector3 top, float post)
        {
            var p = new Vector2(top.x, top.z);
            float reach = post - Touching, half = stair.Width * 0.5f + reach;
            foreach (StairFlight f in stair.Flights)
            {
                if (!stair.Switchback && Mathf.Min(f.From.y, f.To.y) >= top.y + 0.05f) continue;
                Vector2 a = new Vector2(f.From.x, f.From.z), b = new Vector2(f.To.x, f.To.z);
                Vector2 dir = b - a;
                float length = dir.magnitude;
                if (length < 1e-4f) continue;
                dir /= length;
                Vector2 d = p - a;
                float along = Vector2.Dot(d, dir), across = Mathf.Abs(dir.x * d.y - dir.y * d.x);
                if (along > -reach && along < length + reach && across < half) return true;
            }
            foreach (StairLanding l in stair.Landings)
            {
                if (!stair.Switchback && l.Centre.y >= top.y + 0.05f) continue;
                Vector2 d = p - new Vector2(l.Centre.x, l.Centre.z);
                if (Mathf.Abs(Vector2.Dot(d, l.Along)) < l.HalfAlong + reach && Mathf.Abs(Vector2.Dot(d, l.Out)) < l.HalfOut + reach) return true;
            }
            return false;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, StairPost[]> BySite =
            new System.Collections.Concurrent.ConcurrentDictionary<long, StairPost[]>();

        private static CityWorld _cachedFor;

        /// <summary>How far out from its walls a building's stair posts can stand, metres (a tower's depth and a post).</summary>
        private const double Reach = 6.0;

        /// <summary>Whether <paramref name="point"/> is on or in a post of a street stair, within <paramref name="margin"/> (the camera passes through them).</summary>
        public static bool IsPostAt(CityWorld world, Vector3 point, float margin)
        {
            if (world == null)
            {
                return false;
            }
            if (!ReferenceEquals(_cachedFor, world))
            {
                BySite.Clear();
                _cachedFor = world;
            }
            int cx = Mathf.FloorToInt(point.x / CityLayout.ChunkSize), cy = Mathf.FloorToInt(point.z / CityLayout.ChunkSize);
            for (int dy = -3; dy <= 3; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    List<int> owned = world.BuildingsOwnedBy(cx + dx, cy + dy);
                    for (int i = 0; i < owned.Count; i++)
                    {
                        BuildingSite site = world.Building(owned[i]);
                        site.WorldToLocal(point.x, point.z, out double lx, out double lz);
                        if (System.Math.Abs(lx) > site.Params.Width * 0.5 + Reach || System.Math.Abs(lz) > site.Params.Depth * 0.5 + Reach)
                        {
                            continue;
                        }
                        foreach (StairPost post in PostsOf(world, site))
                        {
                            if (post.Contains(point, margin)) return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>Every post of the stairs a building draws: its roof stair and its bridges' stairs down to the street.</summary>
        private static StairPost[] PostsOf(CityWorld world, BuildingSite site)
        {
            long key = ((long)site.Seed << 32) ^ (uint)site.Id;
            if (BySite.TryGetValue(key, out StairPost[] posts))
            {
                return posts;
            }
            var all = new List<StairPost>();
            RoofStair roof = RoofStairs.For(world, site);
            if (roof != null) all.AddRange(Of(world, roof.Stair));
            foreach (Footbridge b in Footbridges.For(world, site))
            {
                if (b.SiteA == site.Id && b.Stair != null) all.AddRange(Of(world, b.Stair));
            }
            posts = all.ToArray();
            if (BySite.Count >= Footbridges.CacheLimit) BySite.Clear();
            BySite[key] = posts;
            return posts;
        }
    }
}
