using System;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The water in the sewers (docs/SEWERS.md; owner, 2026-11-09: water "ankle deep mostly, up to waist deep in some areas"; 2026-11-13: "add
    /// water to the sewers right now there isnt any"): poured by the plan alone, ankle deep through most of the network, waist deep through a
    /// few neighbourhoods, never a step where a tunnel meets a room, under the street and never on it, and drawn where it is.
    /// </summary>
    public sealed class SewerWaterTests
    {
        private const int Seed = CitySeed.Default;

        private static CityWorld _world;

        private static PlanWorld Plan => (_world ??= new CityWorld(Seed)).Plan;

        private static SewerPlan Sewers => SewerPlan.For(Plan);

        private static IEnumerable<SewerTube> Tubes()
        {
            foreach (RoadEdge edge in Plan.Data.Edges)
            {
                SewerTube tube = Sewers.Tube(edge.Id);
                if (tube != null) yield return tube;
            }
        }

        [Test]
        public void MostOfTheSewersAreAnkleDeepAndSomeNeighbourhoodsWaistDeep()
        {
            int rooms = 0, waist = 0;
            foreach (SewerRoom room in Sewers.Rooms)
            {
                rooms++;
                Assert.IsTrue(room.Water == SewerPlan.AnkleDeep || room.Water == SewerPlan.WaistDeep, $"room {room.Node.Id} holds {room.Water} m");
                if (room.Water == SewerPlan.WaistDeep) waist++;
            }
            float share = waist / (float)rooms;
            Debug.Log($"CityX sewer water: {waist} of {rooms} rooms waist deep ({share:P0})");
            Assert.Greater(share, 0.05f, "no neighbourhood is flooded waist deep");
            Assert.Less(share, 0.4f, "the sewers are not ankle deep mostly");
        }

        [Test]
        public void ATunnelsWaterMeetsTheRoomsAtItsEndsWithoutAStep()
        {
            int checkedEnds = 0;
            foreach (RoadEdge edge in Plan.Data.Edges)
            {
                SewerTube tube = Sewers.Tube(edge.Id);
                if (tube == null) continue;
                SewerRoom a = Sewers.Room(edge.A.Id), b = Sewers.Room(edge.B.Id);
                if (a != null && !tube.WallAtStart)
                {
                    Assert.AreEqual(a.Water, tube.WaterStart, 1e-9, $"road {edge.Id}: a step in the water at its start");
                    checkedEnds++;
                }
                if (b != null && !tube.WallAtEnd)
                {
                    Assert.AreEqual(b.Water, tube.WaterEnd, 1e-9, $"road {edge.Id}: a step in the water at its end");
                    checkedEnds++;
                }
            }
            Assert.Greater(checkedEnds, 500);
        }

        [Test]
        public void TheWaterIsInTheTunnelsUnderTheStreetAndNotOnIt()
        {
            int tried = 0;
            foreach (SewerTube tube in Tubes())
            {
                if (tried++ > 200) break;
                double along = (tube.Start + tube.End) * 0.5;
                Vec2 middle = tube.At(along);
                double floor = tube.FloorAt(along), water = SewerPlan.WaterAlong(tube, along);

                // In the channel: the water over the channel's bed.
                Assert.IsTrue(Sewers.TryWaterAt(middle.X, floor - 0.2, middle.Y, out double surface, out double bottom), "no water in the middle of a tunnel");
                Assert.AreEqual(floor + water, surface, 1e-6);
                Assert.AreEqual(floor - tube.Section.ChannelDepth, bottom, 1e-6);

                // On a walkway: the water over the walkway.
                Vec2 walkway = middle + tube.Dir.Perp * (tube.Section.ChannelHalfWidth + 0.5);
                Assert.IsTrue(Sewers.TryWaterAt(walkway.X, floor, walkway.Y, out surface, out bottom), "no water over a walkway");
                Assert.AreEqual(floor, bottom, 1e-6);
                Assert.AreEqual(water, surface - floor, 1e-6);

                // The street over it: far above the surface.
                double street = Plan.GroundHeight(middle.X, middle.Y);
                Assert.Greater(street - (floor + water), SewerPlan.Depth - SewerPlan.WaistDeep - 0.01, "the water is not down in the tunnel");
            }
        }

        [Test]
        public void ARoomsWaterLiesOverItsOwnFloor()
        {
            int tried = 0;
            foreach (SewerRoom room in Sewers.Rooms)
            {
                if (tried++ > 200) break;
                Assert.IsTrue(SewerPlan.TryRoomFloor(room, room.Node.P, out double floor), $"room {room.Node.Id}: its middle is not in it");
                Assert.AreEqual(room.CentreFloor, floor, 1e-6);
                Assert.IsTrue(Sewers.TryWaterAt(room.Node.P.X, floor, room.Node.P.Y, out double surface, out _));
                Assert.AreEqual(room.Water, surface - floor, 1e-6, $"room {room.Node.Id}");
            }
        }

        [Test]
        public void AMouthsWaterDrainsToItsChannelByTheHeadwall()
        {
            int mouths = 0;
            foreach (SewerOutfall outfall in Sewers.AllOutfalls)
            {
                mouths++;
                SewerTube tube = outfall.Tube;
                Assert.Less(tube.WaterEnd, 0.0, "a mouth's water stands over its walkways at the headwall");
                Assert.Greater(tube.WaterEnd, -tube.Section.ChannelDepth, "a mouth's channel is dry at the headwall");
                Vec2 walkway = tube.At(tube.End - 0.3) + tube.Dir.Perp * (tube.Section.ChannelHalfWidth + 0.5);
                if (Sewers.TryWaterAt(walkway.X, tube.FloorAt(tube.End), walkway.Y, out double surface, out _))
                {
                    Assert.LessOrEqual(surface, tube.FloorAt(tube.End) + 1e-6, "the walkway by the headwall is under water");
                }
            }
            Assert.Greater(mouths, 0, "no mouth in this world to try");
        }

        [Test]
        public void TheWaterReachesTheWallsAtAnyDepthAndTheChannelUnderTheWalkways()
        {
            for (int kind = 0; kind < 3; kind++)
            {
                for (int style = 0; style < 4; style++)
                {
                    SewerSection section = SewerSection.For((SewerKind)kind, (SewerStyle)style);
                    Assert.AreEqual(section.ChannelHalfWidth, section.HalfWidthAt(-0.1), 1e-9);
                    Assert.AreEqual(section.FloorHalfWidth, section.HalfWidthAt(1e-4), 0.05, $"{section.Kind} {section.Style}: ankle water does not reach the walls");
                    Assert.Greater(section.HalfWidthAt(SewerPlan.WaistDeep), section.FloorHalfWidth - 0.01, $"{section.Kind} {section.Style}: waist water is narrower than the floor");
                }
            }
        }

        [Test]
        public void TheDrawnWaterLiesOnTheWaterFacingUp()
        {
            Plan.TryFindSpawn(out double sx, out double sz);
            var vertices = new List<Vector3>();
            var flows = new List<Vector2>();
            var triangles = new List<int>();
            SewerWaterMesh.Build(Plan, new Vector2((float)sx, (float)sz), 90.0f, vertices, flows, triangles);
            Assert.Greater(triangles.Count / 3, 50, "hardly any water drawn round the spawn");
            Assert.AreEqual(vertices.Count, flows.Count);
            int onTheWater = 0, triangleCount = triangles.Count / 3;
            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                Assert.Greater(Vector3.Cross(b - a, c - a).y, 0.0f, "a piece of water faces down");
                Vector3 middle = (a + b + c) / 3.0f;
                if (Sewers.TryWaterAt(middle.x, middle.y, middle.z, out double surface, out _) && Mathf.Abs((float)surface - middle.y) < 0.05f)
                {
                    onTheWater++;
                }
            }
            Assert.Greater(onTheWater, triangleCount * 0.9f, "the drawn water is not where the water is");
        }

        /// <summary>Owner, 2026-11-13: "if theres a current make it so you can feel it but not to a point where its just annoying".</summary>
        [Test]
        public void ATunnelsCurrentRunsDownhillAndOutOfAMouthAndARoomIsStill()
        {
            int sloping = 0;
            foreach (SewerTube tube in Tubes())
            {
                Vec2 middle = tube.At((tube.Start + tube.End) * 0.5);
                double floor = tube.FloorAt((tube.Start + tube.End) * 0.5);
                Assert.IsTrue(Sewers.TryWaterAt(middle.X, floor, middle.Y, out _, out _, out Vec2 flow));
                Assert.AreEqual(SewerPlan.CurrentSpeed, flow.Length, 1e-6);
                double fall = tube.Floors[0] - tube.Floors[tube.Floors.Length - 1];
                if (Math.Abs(fall) < 0.05) continue;
                sloping++;
                Vec2 downhill = fall > 0.0 ? tube.Dir : -tube.Dir;
                Assert.Greater(Vec2.Dot(flow, downhill), 0.0, "a tunnel's water runs uphill");
            }
            Assert.Greater(sloping, 100);
            foreach (SewerOutfall outfall in Sewers.AllOutfalls)
            {
                Vec2 inside = outfall.Tube.At(outfall.Portal * 0.5);
                Assert.IsTrue(Sewers.TryWaterAt(inside.X, outfall.Floor - 0.3, inside.Y, out _, out _, out Vec2 flow));
                Assert.Greater(Vec2.Dot(flow, outfall.Tube.Dir), 0.0, "a mouth's water runs back in, not out into the daylight");
            }
            foreach (SewerRoom room in Sewers.Rooms)
            {
                Assert.IsTrue(Sewers.TryWaterAt(room.Node.P.X, room.CentreFloor, room.Node.P.Y, out _, out _, out Vec2 flow));
                Assert.AreEqual(0.0, flow.Length, 1e-9, $"room {room.Node.Id}'s water runs");
                break;
            }
            // Felt, never holding a body back: the most it carries is under half the slowest wade.
            Assert.Less(SewerPlan.CurrentSpeed, 4.0 * Wading.SlowestWade * 0.55, "against the current in a flooded channel a body hardly moves");
            Assert.Greater(SewerPlan.CurrentSpeed * Wading.FlowCarry((float)(SewerPlan.WaistDeep / 2.6), false), 0.3, "waist deep the current is not felt");
        }

        [Test]
        public void TheRingsAndDropsOfWadingAreDarkInTheSewersAndBrightOnTheStreet()
        {
            CityWorld was = CurrentWorld.Instance;
            CurrentWorld.Instance = _world ??= new CityWorld(Seed);
            try
            {
                SewerTube tube = null;
                foreach (SewerTube t in Tubes())
                {
                    tube = t;
                    break;
                }
                double along = (tube.Start + tube.End) * 0.5;
                Vec2 at = tube.At(along);
                var water = new Vector3((float)at.X, (float)(tube.FloorAt(along) + SewerPlan.WaterAlong(tube, along)), (float)at.Y);
                var street = new Vector3((float)at.X, (float)Plan.GroundHeight(at.X, at.Y) + 0.1f, (float)at.Y);
                var white = new Color(0.9f, 0.9f, 0.9f, 0.6f);
                Color down = WaterSplashes.InTheDark(white, water), up = WaterSplashes.InTheDark(white, street);
                Assert.AreEqual(white.r * WaterSplashes.UndergroundShade, down.r, 1e-4f, "a ring shines like a lamp in the dark");
                Assert.AreEqual(white.a, down.a, 1e-4f);
                Assert.AreEqual(white, up, "a ring on the street is dimmed");
            }
            finally
            {
                CurrentWorld.Instance = was;
            }
        }

        [Test]
        public void TheWaterShaderCompilesLitByLampsOrNot()
        {
            ShaderData data = ShaderUtil.GetShaderData(Shader.Find("CityX/Water"));
            var failures = new List<string>();
            string[][] keywordSets =
            {
                new string[0],
                new[] { "_ADDITIONAL_LIGHTS" },
                new[] { "_ADDITIONAL_LIGHTS", "_CLUSTER_LIGHT_LOOP" },
                new[] { "_ADDITIONAL_LIGHTS", "_CLUSTER_LIGHT_LOOP", "_LIGHT_LAYERS", "_ADDITIONAL_LIGHT_SHADOWS" },
                new[] { "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS_CASCADE" },
            };
            foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
            {
                foreach (string[] keywords in keywordSets)
                {
                    ShaderData.VariantCompileInfo variant = data.GetSerializedSubshader(0).GetPass(0)
                        .CompileVariant(stage, keywords, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                    if (!variant.Success)
                    {
                        failures.Add($"{stage} {string.Join(" ", keywords)}: {string.Join("; ", System.Linq.Enumerable.Select(variant.Messages, m => m.message))}");
                    }
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
