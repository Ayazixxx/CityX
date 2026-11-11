using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// Where the title's character stands: on the roof of a real building of the game's city, near its edge, with the city beyond it (owner,
    /// 2026-11-11: "make the city in the background look like the ingame cities that generate ingame"). Chosen from the plan alone, so it is
    /// the same every time for a world, and a new world (a city-generation change rerolls the seed) chooses again.
    ///
    /// A plain box of a building, of middling height, near where the game starts; looking out over one of its four edges. An edge with any of
    /// a building taller than the eye close in front is a wall in the face, and only chosen if every edge has one; the rest are scored by what
    /// they look at: towers rising above the roof ahead, nearer and taller more.
    /// </summary>
    public static class TitleViewpoint
    {
        /// <summary>How far from where the game starts a roof may be, metres.</summary>
        public const double SearchRadius = 450.0;

        /// <summary>How high the roof is above its street, metres: high enough to look out over the street, low enough to look up at towers.</summary>
        public const float MinReach = 22.0f;

        public const float MaxReach = 60.0f;

        /// <summary>The roof's shortest side, metres: room for the camera behind the character.</summary>
        public const float MinSide = 17.0f;

        /// <summary>The shortest side of a building the looser second search takes.</summary>
        public const float LooseSide = 10.0f;

        /// <summary>
        /// How far in from the front edge the character stands, and from the left one as the camera sees it, metres: near the corner, so the
        /// roof fills only the lower right of the view and the city drops away past its left edge (owner, 2026-11-11, of the first rooftop:
        /// "i liked this positioning better where it was just the side of the building instead of it being most of the screen").
        /// </summary>
        public const float EdgeGap = 4.2f;

        public const float SideGap = 2.4f;

        /// <summary>Past the left edge the view looks down: no building within this far that side reaches higher than this under the roof.</summary>
        public const double SideClearDistance = 30.0;

        public const double SideClearBelowRoof = 12.0;

        /// <summary>The title's rooftop stands this far over the highest roof with any of it this near the character, metres.</summary>
        public const double AroundRadius = 80.0;

        public const float AboveAround = 8.0f;

        /// <summary>The eye's height over the roof, metres, for what it can see over.</summary>
        public const float EyeHeight = 1.8f;

        /// <summary>The view scored: buildings between these distances, within this half angle either side of straight ahead.</summary>
        public const double ViewNear = 30.0;

        public const double ViewFar = 700.0;
        public const double ViewHalfAngleDegrees = 32.0;

        /// <summary>A building taller than the eye with any of it this close ahead, within this half angle (the view's own), blocks the view.</summary>
        public const double WallDistance = 70.0;

        public const double WallHalfAngleDegrees = 45.0;

        public struct Pick
        {
            public bool Found;
            public int Building;

            /// <summary>Where the character's feet go: on the roof, near the left end of the chosen edge (<see cref="EdgeGap"/>, <see cref="SideGap"/>).</summary>
            public Vector3 Feet;

            /// <summary>Level, out over the edge: the way the view looks.</summary>
            public Vector3 Out;

            public double Score;

            /// <summary>Every edge in reach had a wall in front: this is the best of those.</summary>
            public bool Walled;
        }

        /// <summary>The best roof near where the game starts in <paramref name="plan"/>.</summary>
        public static Pick Choose(PlanWorld plan)
        {
            double centreX = 0.0, centreZ = 0.0;
            if (plan.TryFindSpawn(out double spawnX, out double spawnZ))
            {
                centreX = spawnX;
                centreZ = spawnZ;
            }
            // Everything that can be seen from a roof in reach, once.
            double reach = SearchRadius + ViewFar;
            var near = new List<int>();
            BuildingSite[] buildings = plan.Buildings;
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].Exists && Distance(buildings[i], centreX, centreZ) < reach)
                {
                    near.Add(i);
                }
            }

            var best = new Pick();
            var walled = new Pick();   // the best edge with a wall in front, should every edge have one
            // The plain roofs near the start first; failing those (the game's bigger city can have none of that height), any box of a
            // building further out: the title stands its own roof over the spot anyway.
            for (int tier = 0; tier < 2 && !best.Found && !walled.Found; tier++)
            {
                foreach (int i in near)
                {
                    BuildingSite site = buildings[i];
                    bool candidate = tier == 0
                        ? Standable(site) && Distance(site, centreX, centreZ) <= SearchRadius
                        : Loose(site) && Distance(site, centreX, centreZ) <= SearchRadius * 2.0;
                    if (!candidate)
                    {
                        continue;
                    }
                    for (int side = 0; side < 4; side++)
                    {
                        // The way out over the edge, and the way to its left as the camera sees it, in the building's own frame; the feet just in
                        // from that front edge and from its left end (the corner), the camera behind them just inside the left edge.
                        double outX = side == 0 ? 1.0 : side == 1 ? -1.0 : 0.0;
                        double outZ = side == 2 ? 1.0 : side == 3 ? -1.0 : 0.0;
                        double leftX = -outZ, leftZ = outX;
                        double half = outX != 0.0 ? site.Params.Width * 0.5 : site.Params.Depth * 0.5;
                        double halfAlong = outX != 0.0 ? site.Params.Depth * 0.5 : site.Params.Width * 0.5;
                        double ahead = half - EdgeGap, aside = halfAlong - SideGap;
                        site.LocalToWorld(outX * ahead + leftX * aside, outZ * ahead + leftZ * aside, out double feetX, out double feetZ);
                        site.LocalToWorld(outX, outZ, out double tipX, out double tipZ);
                        site.LocalToWorld(leftX, leftZ, out double leftTipX, out double leftTipZ);
                        double dirX = tipX - site.CentreX, dirZ = tipZ - site.CentreZ;
                        double score = View(buildings, near, i, feetX, feetZ, dirX, dirZ, site.Top + EyeHeight, out bool blocked);
                        blocked |= SideBlocked(buildings, near, i, feetX, feetZ, leftTipX - site.CentreX, leftTipZ - site.CentreZ, site.Top);
                        ref Pick into = ref blocked ? ref walled : ref best;
                        if (!into.Found || score > into.Score)
                        {
                            into = new Pick
                            {
                                Found = true,
                                Building = i,
                                Feet = new Vector3((float)feetX, site.Top, (float)feetZ),
                                Out = new Vector3((float)dirX, 0.0f, (float)dirZ).normalized,
                                Score = score,
                                Walled = blocked,
                            };
                        }
                    }
                }
            }
            Pick pick = best.Found ? best : walled;
            if (!pick.Found)
            {
                // Nothing at all to stand over: where the game starts, looking north.
                var start = new Vector3((float)centreX, (float)plan.GroundHeight(centreX, centreZ), (float)centreZ);
                pick = new Pick { Found = true, Building = -1, Feet = start, Out = Vector3.forward };
            }
            // The title's own rooftop stands a little over everything close round it, so none of the real city comes up through it or beside
            // it: past its left edge the view only drops away.
            pick.Feet.y = Mathf.Max(pick.Feet.y, ClearOfAround(buildings, near, pick.Feet.x, pick.Feet.z) + AboveAround);
            return pick;
        }

        /// <summary>Any box of a building wide enough to stand a roof over.</summary>
        private static bool Loose(BuildingSite site)
        {
            return site.Exists && !site.Shell && site.Sides == 0 && site.Params.Width >= LooseSide && site.Params.Depth >= LooseSide;
        }

        /// <summary>A plain box of a building of the right height, whose roof is its own and has room on it.</summary>
        public static bool Standable(BuildingSite site)
        {
            return site.Exists && !site.Shell && !site.OnRoof && !site.Wing && !site.Params.IsSpecial && site.Sides == 0 && !site.HasRoofObstacle
                && !site.RoofSealed && site.Reach >= MinReach && site.Reach <= MaxReach
                && site.Params.Width >= MinSide && site.Params.Depth >= MinSide;
        }

        private static double View(BuildingSite[] buildings, List<int> near, int from, double x, double z, double dirX, double dirZ, double eye,
            out bool blocked)
        {
            double cosHalf = System.Math.Cos(ViewHalfAngleDegrees * System.Math.PI / 180.0);
            double cosWall = System.Math.Cos(WallHalfAngleDegrees * System.Math.PI / 180.0);
            double score = 0.0;
            blocked = false;
            foreach (int j in near)
            {
                if (j == from)
                {
                    continue;
                }
                BuildingSite other = buildings[j];
                double rise = other.Top - eye;
                if (rise <= 0.0)
                {
                    continue;
                }
                // A wall in the face: any of a taller building close in front, not only its middle (a broad tower beside the view fills
                // half of it).
                NearestPoint(other, x, z, out double nearX, out double nearZ);
                double ndx = nearX - x, ndz = nearZ - z;
                double nearDistance = System.Math.Sqrt(ndx * ndx + ndz * ndz);
                if (nearDistance < WallDistance && nearDistance > 1e-3 && (ndx * dirX + ndz * dirZ) / nearDistance > cosWall)
                {
                    blocked = true;
                    score -= 100.0;
                    continue;
                }
                double dx = other.CentreX - x, dz = other.CentreZ - z;
                double distance = System.Math.Sqrt(dx * dx + dz * dz);
                if (distance >= ViewNear && distance <= ViewFar && (dx * dirX + dz * dirZ) / distance >= cosHalf)
                {
                    score += System.Math.Min(rise, 150.0) / System.Math.Sqrt(distance / 100.0);
                }
            }
            return score;
        }

        /// <summary>A building close beside the roof on its left reaching up near its height: the view would look into it, not down past the edge.</summary>
        private static bool SideBlocked(BuildingSite[] buildings, List<int> near, int from, double x, double z, double leftX, double leftZ, double roof)
        {
            double length = System.Math.Sqrt(leftX * leftX + leftZ * leftZ);
            leftX /= length;
            leftZ /= length;
            double cosSide = System.Math.Cos(60.0 * System.Math.PI / 180.0);
            foreach (int j in near)
            {
                BuildingSite other = buildings[j];
                if (j == from || other.Top < roof - SideClearBelowRoof)
                {
                    continue;
                }
                NearestPoint(other, x, z, out double nearX, out double nearZ);
                double dx = nearX - x, dz = nearZ - z;
                double distance = System.Math.Sqrt(dx * dx + dz * dz);
                if (distance < SideClearDistance && distance > 1e-3 && (dx * leftX + dz * leftZ) / distance > cosSide)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The highest roof with any of it within <see cref="AroundRadius"/> of (x, z), the chosen building's own included.</summary>
        private static float ClearOfAround(BuildingSite[] buildings, List<int> near, double x, double z)
        {
            float highest = float.NegativeInfinity;
            foreach (int j in near)
            {
                NearestPoint(buildings[j], x, z, out double nearX, out double nearZ);
                double dx = nearX - x, dz = nearZ - z;
                if (dx * dx + dz * dz < AroundRadius * AroundRadius)
                {
                    highest = Mathf.Max(highest, buildings[j].Top);
                }
            }
            return highest;
        }

        /// <summary>The point of the building's footprint nearest (x, z).</summary>
        private static void NearestPoint(BuildingSite site, double x, double z, out double nearX, out double nearZ)
        {
            site.WorldToLocal(x, z, out double localX, out double localZ);
            double halfWidth = site.Params.Width * 0.5, halfDepth = site.Params.Depth * 0.5;
            localX = System.Math.Max(-halfWidth, System.Math.Min(halfWidth, localX));
            localZ = System.Math.Max(-halfDepth, System.Math.Min(halfDepth, localZ));
            site.LocalToWorld(localX, localZ, out nearX, out nearZ);
        }

        private static double Distance(BuildingSite site, double x, double z)
        {
            double dx = site.CentreX - x, dz = site.CentreZ - z;
            return System.Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
