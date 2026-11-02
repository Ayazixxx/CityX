using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// For the tests of a move that carries the body along (Octaslash's dash, the stab dash): points the player's camera along a level or downhill
    /// run from where it stands with no building in the next <c>metres</c>, through a test yaw object handed to the input reader (kept in
    /// <c>made</c> for the test to destroy), and returns that direction.
    /// </summary>
    internal static class OpenRun
    {
        /// <summary>Whether a body's run can pass over a place: no generated building within 3 m, nor Old Guy's house or the sky hub's landing, which are built by hand and not in the plan's list.</summary>
        public static bool ClearAt(CityWorld world, Vector3 p)
        {
            if (world.Plan.BuildingAt(p.x, p.z, 3.0) >= 0 || world.Plan.SkyHub.OnLanding(p.x, p.z, 2.0))
            {
                return false;
            }
            TutorialLot house = world.Plan.TutorialHouse;
            if (!house.Found)
            {
                return true;
            }
            house.ToLocal(p.x, p.z, out double x, out double z);
            const double margin = 3.0;
            return x < TutorialLot.HouseX0 - margin || x > TutorialLot.HouseX1 + margin || z < TutorialLot.HouseZ0 - margin || z > TutorialLot.HouseZ1 + margin;
        }

        public static Vector3 Face(PlayerVitals vitals, List<GameObject> made, float metres = 30.0f)
        {
            CityWorld world = CurrentWorld.Instance;
            Vector3 from = vitals.transform.position;
            float bestRise = float.MaxValue;
            Vector3 heading = Vector3.forward;
            for (int a = 0; a < 72; a++)
            {
                var dir = new Vector3(Mathf.Sin(a * 5.0f * Mathf.Deg2Rad), 0.0f, Mathf.Cos(a * 5.0f * Mathf.Deg2Rad));
                double h0 = world.Plan.GroundHeight(from.x, from.z);
                float rise = 0.0f;
                bool open = true;
                for (float run = 2.0f; run <= metres && open; run += 2.0f)
                {
                    Vector3 p = from + dir * run;
                    rise = Mathf.Max(rise, (float)(world.Plan.GroundHeight(p.x, p.z) - h0));   // only ground rising into the move matters
                    open = ClearAt(world, p);
                }
                if (open && rise < bestRise)
                {
                    bestRise = rise;
                    heading = dir;
                }
            }
            Assert.Less(bestRise, 0.6f, "setup: no level or downhill open run from the spawn");
            var yaw = new GameObject("TestYaw");
            made.Add(yaw);
            yaw.transform.position = vitals.transform.position + Vector3.up * 2.4f;
            yaw.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
            vitals.GetComponent<PlayerInputReader>().YawSource = yaw.transform;
            return heading;
        }
    }
}
