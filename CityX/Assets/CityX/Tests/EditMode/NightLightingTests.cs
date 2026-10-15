using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The 2026-10-09 lighting pass (owner: "the lighting really doesn't look good both indoors and looking from outdoors during nighttime"):
    /// the inside of a building is pale and marked indoors (SurfaceLook, CityMeshBuilder.Interior.cs) so the sky's light is dimmed on it and only
    /// its own lights reach it; a room is lit by lamps shining down and their bounce shining up (World.RoomLighting), never through the floor into
    /// another storey; a window with a real room behind it is clear glass; a powered building has a lamp over its door at night
    /// (World.EntranceLighting); and the night has stars, a stronger moon and its own grade (DayNightCycle).
    /// </summary>
    public sealed class NightLightingTests
    {
        private const int Seed = 70485213;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        [TearDown]
        public void TearDown()
        {
            PowerGrid.Reset();
            DayNightCycle.Daylight = 1.0f;
        }

        private static IEnumerable<BuildingSite> Interiors(int count)
        {
            int n = 0;
            foreach (BuildingSite s in TestSites.NearSpawn(World, 12))
            {
                if (s.Shell || s.Sides >= 3 || s.Params.IsSpecial)   // a garage, car park or mall has no rooms
                {
                    continue;
                }
                yield return s;
                if (++n >= count)
                {
                    yield break;
                }
            }
        }

        private static Vector3 OriginOf(BuildingSite site)
        {
            return new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
        }

        // ---- indoors ----

        [Test]
        public void AnIndoorLookSurvivesBeingPackedAndLeavesTheRestOfTheLookAlone()
        {
            foreach (bool indoors in new[] { false, true })
            {
                for (int slice = 0; slice < 12; slice++)
                {
                    foreach (float wear in new[] { 0.0f, 0.5f, 1.0f })
                    {
                        Color32 packed = SurfaceLook.Pack(new Color32(200, 120, 40, 255), slice, wear, indoors);
                        SurfaceLook.Unpack(packed, out Color32 tint, out int gotSlice, out float gotWear, out bool gotIndoors);
                        Assert.AreEqual(slice, gotSlice);
                        Assert.AreEqual(wear, gotWear, 1.0f / (SurfaceLook.WearLevels - 1));
                        Assert.AreEqual(indoors, gotIndoors);
                        Assert.AreEqual(indoors, SurfaceLook.IsIndoors(packed));
                        Assert.AreEqual(200, tint.r);
                    }
                }
            }
            Assert.IsFalse(SurfaceLook.IsIndoors(SurfaceLook.Default), "an unstyled surface is outdoors");
        }

        [Test]
        public void TheInsideOfABuildingIsMarkedIndoorsAndItsOutsideIsNot()
        {
            int inside = 0;
            foreach (BuildingSite site in Interiors(12))
            {
                var structures = new MeshBuffer();
                var concrete = new MeshBuffer();
                Vector3 origin = OriginOf(site);
                CityMeshBuilder.BuildSite(World, site, origin, structures, concrete, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                    null, null, new MeshBuffer(), false);

                foreach (Color32 look in structures.Colors)
                {
                    Assert.IsFalse(SurfaceLook.IsIndoors(look), $"building {site.Id}: part of its outside is marked indoors");
                }

                float wall = BuildingInterior.WallThickness;
                float halfX = site.Params.Width * 0.5f, halfZ = site.Params.Depth * 0.5f;
                for (int i = 0; i < concrete.Vertices.Count; i++)
                {
                    Vector3 v = concrete.Vertices[i] + origin;
                    site.WorldToLocal(v.x, v.z, out double lx, out double lz);
                    bool withinWalls = System.Math.Abs(lx) < halfX - wall - 0.05 && System.Math.Abs(lz) < halfZ - wall - 0.05;
                    bool outsideFootprint = System.Math.Abs(lx) > halfX + 0.05 || System.Math.Abs(lz) > halfZ + 0.05;
                    if (withinWalls)
                    {
                        Assert.IsTrue(SurfaceLook.IsIndoors(concrete.Colors[i]), $"building {site.Id}: a surface inside it is not marked indoors at {v}");
                        inside++;
                    }
                    else if (outsideFootprint)
                    {
                        Assert.IsFalse(SurfaceLook.IsIndoors(concrete.Colors[i]), $"building {site.Id}: its entrance steps are marked indoors at {v}");
                    }
                }
            }
            Assert.Greater(inside, 1000, "need a real sample");
        }

        [Test]
        public void RoomsArePaleInside()
        {
            // A lit room only reads as lit if its walls and ceiling give the light back (dark concrete made every lit room murky).
            foreach (BuildingSite site in Interiors(20))
            {
                var concrete = new MeshBuffer();
                CityMeshBuilder.BuildSite(World, site, OriginOf(site), new MeshBuffer(), concrete, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                    null, null, new MeshBuffer(), false);
                foreach (Color32 look in concrete.Colors)
                {
                    SurfaceLook.Unpack(look, out Color32 tint, out int slice, out _, out bool indoors);
                    if (indoors && slice == (int)WallMaterial.Plaster)
                    {
                        float luma = (0.299f * tint.r + 0.587f * tint.g + 0.114f * tint.b) / 255.0f;
                        Assert.Greater(luma, 0.7f, $"building {site.Id}: a dark plaster wall or ceiling inside");
                    }
                }
            }
        }

        // ---- room lights ----

        [Test]
        public void LampsShineOnTheirOwnFloorAndBouncesOnTheirOwnCeilingAndNeitherReachesAnotherStorey()
        {
            int lamps = 0;
            foreach (BuildingSite site in Interiors(40))
            {
                float storey = BuildingInterior.StoreyHeight(site.Params.Height);
                foreach (RoomLightPlan plan in RoomLighting.PlansFor(site))
                {
                    float floorY = site.BaseHeight + plan.Key.Floor * storey;
                    Assert.AreEqual(plan.LightPoints.Count, plan.BouncePoints.Count, $"{plan.Key}: a lamp without its bounce");
                    for (int i = 0; i < plan.LightPoints.Count; i++)
                    {
                        lamps++;
                        Vector3 lamp = plan.LightPoints[i];
                        Vector3 bounce = plan.BouncePoints[i];
                        float lampHeight = lamp.y - floorY;
                        Assert.Greater(lampHeight, BodyRig.StandingHeight, $"{plan.Key}: a lamp at head height");
                        Assert.Less(bounce.y, floorY, $"{plan.Key}: a bounce above the floor would light the floor it stands on");
                        Assert.Greater(bounce.y, floorY - BuildingInterior.SlabThickness * 0.5f,
                            $"{plan.Key}: a bounce under the slab stands in the room below and burns its ceiling");
                        Assert.Greater(plan.LightRange, lampHeight * 1.2f, $"{plan.Key}: a lamp whose light does not reach its floor");
                        Assert.Less(plan.LightRange, lampHeight + storey - BuildingInterior.SlabThickness,
                            $"{plan.Key}: a lamp that lights the floor of the storey below");
                        float toOwnCeiling = storey - BuildingInterior.SlabThickness - (bounce.y - floorY);
                        Assert.Greater(plan.BounceRange, toOwnCeiling, $"{plan.Key}: a bounce that does not reach its ceiling");
                        if (plan.Key.Floor < BuildingInterior.FloorCountFor(site.Params.Height) - 1)   // the top floor has the roof over it, and may be taller
                        {
                            Assert.Less(plan.BounceRange, toOwnCeiling + storey, $"{plan.Key}: a bounce that lights the ceiling of the storey above");
                        }
                    }
                    Assert.Greater(plan.LightIntensity, 0.0f);
                    Assert.Greater(plan.BounceIntensity, 0.0f);
                    Assert.Less(plan.BounceIntensity, plan.LightIntensity * 2.0f, $"{plan.Key}: a bounce brighter than the light it bounces");
                }
            }
            Assert.Greater(lamps, 100, "need a real sample");
        }

        [Test]
        public void NoPartOfABigRoomIsLeftWithoutALamp()
        {
            // One lamp for every cell of the room's split that has fittings over it. The cap was 6 of a 3x3 split's 9: the last third of every big
            // hall had fittings glowing over a dark floor.
            int bigRooms = 0;
            foreach (BuildingSite site in Interiors(80))
            {
                foreach (RoomLightPlan plan in RoomLighting.PlansFor(site))
                {
                    Rect area = plan.Area;
                    float span = Mathf.Max(area.width, area.height);
                    int split = RoomLighting.SplitFor(span);
                    var cells = new HashSet<int>();
                    foreach (Fixture f in plan.Fixtures)
                    {
                        Vector3 c = (f.HousingMin + f.HousingMax) * 0.5f;
                        int cx = Mathf.Clamp(Mathf.FloorToInt((c.x - area.xMin) / area.width * split), 0, split - 1);
                        int cz = Mathf.Clamp(Mathf.FloorToInt((c.z - area.yMin) / area.height * split), 0, split - 1);
                        cells.Add(cz * split + cx);
                    }
                    Assert.AreEqual(cells.Count, plan.LightPoints.Count, $"{plan.Key}: part of the room has fittings and no lamp");
                    if (split >= 3) bigRooms++;
                }
            }
            Assert.Greater(bigRooms, 5, "need some big halls");
        }

        [Test]
        public void ARoomLightIsMarkedInsideAndAnEntranceLampOutside()
        {
            var go = new GameObject("Lights");
            try
            {
                Light inside = go.AddComponent<Light>();
                CityLightLayers.MarkInside(inside);
                Assert.AreNotEqual(0u, CityLightLayers.Of(inside) & CityLightLayers.Interior);
                Assert.AreEqual(0u, CityLightLayers.Of(inside) & CityLightLayers.Exterior);
                Assert.AreNotEqual(0u, CityLightLayers.Of(inside) & CityLightLayers.Default, "it must still light the player standing in the room");

                var other = new GameObject("Other");
                other.transform.SetParent(go.transform);
                Light outside = other.AddComponent<Light>();
                CityLightLayers.MarkOutside(outside);
                Assert.AreNotEqual(0u, CityLightLayers.Of(outside) & CityLightLayers.Exterior);
                Assert.AreEqual(0u, CityLightLayers.Of(outside) & CityLightLayers.Interior);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---- windows ----

        [Test]
        public void AWindowWithARoomBehindItIsClearGlassAndADistantBuildingsIsNot()
        {
            int clear = 0;
            foreach (BuildingSite site in Interiors(10))
            {
                var dark = new MeshBuffer();
                CityMeshBuilder.BuildSite(World, site, OriginOf(site), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), dark, new MeshBuffer(),
                    null, null, new MeshBuffer(), false);
                foreach (Color32 look in dark.Colors)
                {
                    if (look.r == 255 && look.b != 255) clear++;
                }

                var far = new MeshBuffer();
                CityMeshBuilder.BuildSite(World, site, OriginOf(site), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), far, null,
                    null, null, null, true);
                foreach (Color32 look in far.Colors)
                {
                    Assert.IsFalse(look.r == 255 && look.b != 255, $"building {site.Id}: clear glass on a distant shell, with nothing behind it");
                }
            }
            Assert.Greater(clear, 100, "no window shows the room behind it");
        }

        [Test]
        public void ADeadBuildingsSignsAreDark()
        {
            // Blade signs glow only where there is power: a dead building's lit-glass buffer holds nothing.
            int dead = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 14))
            {
                if (site.Shell || PowerGrid.IsOn(site)) continue;
                var lit = new MeshBuffer();
                CityMeshBuilder.BuildSite(World, site, OriginOf(site), new MeshBuffer(), new MeshBuffer(), lit, new MeshBuffer(), new MeshBuffer(),
                    null, null, new MeshBuffer(), false);
                Assert.IsTrue(lit.IsEmpty, $"building {site.Id} has no power and something on it glows");
                if (++dead >= 60) break;
            }
            Assert.Greater(dead, 20);
        }

        // ---- the lamp over a powered door ----

        [Test]
        public void OnlyAPoweredBuildingHasALampOverItsDoorAndItShinesOnTheStreetAtNight()
        {
            BuildingSite powered = default, deadSite = default;
            foreach (BuildingSite s in Interiors(400))
            {
                if (s.OnRoof) continue;
                if (PowerGrid.IsOn(s) && !powered.Exists) powered = s;
                if (!PowerGrid.IsOn(s) && !deadSite.Exists) deadSite = s;
            }
            Assert.IsTrue(powered.Exists && deadSite.Exists, "need one of each");
            Assert.IsTrue(EntranceLighting.Has(powered));
            Assert.IsFalse(EntranceLighting.Has(deadSite));

            EntranceLighting.Place(powered, out Vector3 at, out Vector3 shining, out float intensity, out float range);
            powered.WorldToLocal(at.x, at.z, out double lx, out double lz);
            Assert.AreEqual(0.0, lx, 0.01, "not over the door");
            Assert.Greater(lz, powered.FrontHalfDepth, "not in front of the wall");
            Assert.Greater(at.y, powered.DoorGroundHeight + 2.0f, "lower than a head");
            Assert.Less(shining.y, -0.8f, "it shines down");
            Assert.Greater(range, at.y - powered.DoorGroundHeight, "its light does not reach the street");
            Assert.Greater(intensity, 0.0f);

            Assert.IsTrue(EntranceLighting.IsLitAt(0.0f), "dark at night");
            Assert.IsFalse(EntranceLighting.IsLitAt(1.0f), "lit by day");
        }

        // ---- the night ----

        [Test]
        public void AwayFromItsLampsTheNightIsAlmostPitchBlack()
        {
            // Owner, 2026-10-09: "at nighttime it needs to be darker ... get rid of this greyish hue that appears when you distance yourself from
            // light", "it should be almost pitch black at night like minecraft". A pale wall in the open at midnight, lit by the moon at its
            // highest and the night sky's fill, against the pavement under a powered door's lamp.
            SkyCycle.State midnight = SkyCycle.At(SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f);
            const float paleAlbedo = 0.6f;
            float openWall = paleAlbedo * (midnight.MoonIntensity + midnight.AmbientColour.linear.maxColorComponent);   // ambient colours are gamma
            Assert.Less(openWall, 0.13f, "the dark between the lights is a grey wash, not night");
            float underTheLamp = paleAlbedo * EntranceLighting.GroundLight;
            Assert.Greater(underTheLamp / openWall, 4.0f, "a lamp at night has to stand out of the dark");
            Assert.Less(midnight.SkyEnergy * Mathf.Max(SkyCycle.SkyHorizonNight.r, SkyCycle.SkyHorizonNight.g, SkyCycle.SkyHorizonNight.b), 0.01f,
                "the night sky (and the haze, which takes its colour) is near black");
            Assert.GreaterOrEqual(DayNightCycle.IndoorShadeNight, 0.9f, "a dark room at night is dark");
        }

        [Test]
        public void TheCharacterAndItsHairHaveNoShine()
        {
            // Owner, 2026-10-09: "get rid of the shinyness that is appearing on the character and the hair".
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                material.SetFloat("_Smoothness", 0.5f);
                CityX.CharacterEditor.CharacterSkin.MakeMatte(material);
                Assert.AreEqual(0.0f, material.GetFloat("_Smoothness"), 1e-4f);
                Assert.IsTrue(material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
            foreach (string path in new[] { "Assets/CityX/Character/Materials/PlayerSkin.mat", "Assets/CityX/Character/Materials/Hair.mat" })
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(asset, path);
                Assert.AreEqual(0.0f, asset.GetFloat("_Smoothness"), 1e-4f, $"{path} is glossy");
                Assert.IsTrue(asset.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"), $"{path} catches every lamp in a highlight");
            }
        }

        [Test]
        public void TheNightHasStarsAStrongerMoonADimmerInsideAndItsOwnGrade()
        {
            var go = new GameObject("Cycle");
            var gradeObject = new GameObject("NightGrade");
            var dome = new Material(Shader.Find("CityX/ProceduralSky"));
            Color oldFog = RenderSettings.fogColor;
            Color oldAmbient = RenderSettings.ambientLight;
            float oldReflection = RenderSettings.reflectionIntensity;
            var oldAmbientMode = RenderSettings.ambientMode;
            try
            {
                var cycle = go.AddComponent<DayNightCycle>();
                cycle.SkyDome = dome;
                cycle.NightGrade = gradeObject.AddComponent<UnityEngine.Rendering.Volume>();

                cycle.Apply(SkyCycle.At(SkyCycle.DaySeconds * 0.5f));
                Assert.AreEqual(0.0f, dome.GetFloat("_StarEnergy"), 1e-4f, "stars by day");
                Assert.AreEqual(0.0f, cycle.NightGrade.weight, 1e-3f, "the night grade by day");
                Assert.AreEqual(DayNightCycle.IndoorShadeDay, Shader.GetGlobalFloat("_CityXIndoorShade"), 1e-4f);
                Assert.AreEqual(1.0f, DayNightCycle.Daylight, 1e-3f);
                Assert.AreEqual(1.0f, RenderSettings.reflectionIntensity, 1e-3f, "the sky's reflection by day");

                SkyCycle.State midnight = SkyCycle.At(SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f);
                cycle.Apply(midnight);
                Assert.Greater(dome.GetFloat("_StarEnergy"), 0.5f, "no stars at night");
                Assert.AreEqual(1.0f, cycle.NightGrade.weight, 1e-3f);
                Assert.AreEqual(DayNightCycle.IndoorShadeNight, Shader.GetGlobalFloat("_CityXIndoorShade"), 1e-4f);
                Assert.Less(DayNightCycle.Daylight, 0.01f);
                Assert.Less(RenderSettings.reflectionIntensity, 0.1f, "a daytime sky reflected in every window at midnight");
                Assert.Less(midnight.MoonIntensity, 0.1f, "night is almost pitch black (owner): the moon only outlines things");

                cycle.Overcast = 1.0f;
                cycle.Apply(midnight);
                Assert.AreEqual(0.0f, dome.GetFloat("_StarEnergy"), 1e-4f, "stars through a cloud lid");
            }
            finally
            {
                RenderSettings.fogColor = oldFog;
                RenderSettings.ambientMode = oldAmbientMode;
                RenderSettings.ambientLight = oldAmbient;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(gradeObject);
                Object.DestroyImmediate(dome);
                Shader.SetGlobalFloat("_CityXIndoorShade", 0.0f);
                RenderSettings.reflectionIntensity = oldReflection;
            }
        }
    }
}
