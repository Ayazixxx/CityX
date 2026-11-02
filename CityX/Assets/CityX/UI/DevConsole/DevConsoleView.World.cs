using System.Collections.Generic;
using System.Globalization;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Net;
using CityX.Vehicles;
using CityX.View;
using CityX.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FlightFeel = CityX.Weapons.FlightFeel;

namespace CityX.UI
{
    public sealed partial class DevConsoleView
    {
        private void Weather(string which, string strength)
        {
            if (Rain == null)
            {
                Log("no rain in this scene");
                return;
            }
            switch (which)
            {
                case "rain":
                    if (!CityX.World.Weather.TryParseIntensity(strength, out float intensity))
                    {
                        Log(DevCommands.UsageFor("weather"));
                        return;
                    }
                    Rain.SetRain(intensity);
                    Log(intensity >= CityX.World.Weather.HeavyRain ? "raining" : $"raining ({intensity:0.00})");
                    return;
                case "clear":
                    Rain.SetRain(0.0f);
                    Log("clear -- the streets dry out slowly (/weather dry for at once)");
                    return;
                case "dry":
                    Rain.Dry();
                    Log("dry");
                    return;
                case "lightning":
                {
                    float metres = 500.0f;
                    if (!string.IsNullOrEmpty(strength) &&
                        !float.TryParse(strength, NumberStyles.Float, CultureInfo.InvariantCulture, out metres))
                    {
                        Log(DevCommands.UsageFor("weather"));
                        return;
                    }
                    metres = Mathf.Max(30.0f, metres);
                    Rain.StrikeLightning(metres);
                    Log($"lightning {metres:0}m away -- thunder in {CityX.World.Weather.ThunderDelaySeconds(metres):0.0}s");
                    return;
                }
                case "status":
                {
                    Vector3 at = (Vitals != null ? Vitals.transform.position : transform.position) + Vector3.up * 1.6f;
                    Log($"rain {Rain.Intensity:0.00} (asked {Rain.TargetIntensity:0.00}), wetness {Rain.Wetness:0.00}, " +
                        $"cover over you {CurrentWeather.Shelter(at):0.00}, rain on you {CurrentWeather.RainOn(at):0.00}");
                    return;
                }
                default:
                    Log(DevCommands.UsageFor("weather"));
                    return;
            }
        }

        private void Time_(string which)
        {
            if (Cycle == null)
            {
                Log("no day/night cycle in this scene");
                return;
            }
            if (which == null || !DevCommands.TryTimeOfDay(
                    which, SkyCycle.DaySeconds, SkyCycle.CycleSeconds, out float seconds))
            {
                Log(DevCommands.UsageFor("time"));
                return;
            }

            Cycle.TimeOfDay = seconds;
            // Applied now rather than next frame, so the console reports a
            // state that is already on screen.
            Cycle.Apply(SkyCycle.At(seconds));
            Log($"time set to {which} -- {seconds / 60.0f:0.0} min into the " +
                $"{SkyCycle.CycleSeconds / 60.0f:0} min cycle");
        }

        private void Flying()
        {
            if (Fly == null)
            {
                Log("no free camera in this scene");
                return;
            }
            Fly.enabled = !Fly.enabled;
            Log("fly " + (Fly.enabled ? "on" : "off"));
        }

        /// <summary>
        /// Teleports to the nearest real example of a generated feature --
        /// Requested, after repeated reports of "there are no sloped
        /// roads" that kept turning out to mean "I have not walked to one
        /// yet": rather than hand out chunk coordinates and hope, jump the
        /// player there directly so the feature can be checked on the spot.
        /// </summary>
        private void Goto(string what)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            if (Streamer == null)
            {
                Log("no city streamer in this scene");
                return;
            }
            if (what != "slope" && what != "highway" && what != "district")   // "district": the highway's old name here, kept
            {
                Log(DevCommands.UsageFor("goto"));
                return;
            }

            Vector3 from = Vitals.transform.position;
            var world = new CityWorld(Streamer.WorldSeed);

            Vector3 destination;
            string found;
            if (what == "slope")
            {
                if (!TryFindNearestSlope(world, from, out destination))
                {
                    Log("no sloped road found -- try again from somewhere else");
                    return;
                }
                found = "sloped road";
            }
            else
            {
                if (!TryFindNearestHighway(world, from, out destination))
                {
                    Log("no highway found");
                    return;
                }
                found = "highway";
            }

            Streamer.StreamAround(destination, 800);
            Physics.SyncTransforms();

            CharacterController controller = Vitals.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }
            Vitals.transform.position = destination;
            if (controller != null)
            {
                controller.enabled = true;
            }
            Physics.SyncTransforms();

            Log($"teleported to the nearest {found}");
        }

        /// <summary>The middle of the nearest road that climbs at least 4% (else the steepest in the city), a couple of metres up.</summary>
        private static bool TryFindNearestSlope(CityWorld world, Vector3 from, out Vector3 destination)
        {
            destination = Vector3.zero;
            double bestNear = double.MaxValue, bestSteep = 0.0;
            CityX.World.Plan.EdgeGeometry near = null, steepest = null;
            foreach (CityX.World.Plan.EdgeGeometry g in world.Plan.Geometry.Edges)
            {
                if (g == null || g.Edge.Bridge || g.Length < 20.0)
                {
                    continue;
                }
                double grade = System.Math.Abs(world.Plan.GroundHeight(g.A.X, g.A.Y) - world.Plan.GroundHeight(g.B.X, g.B.Y)) / g.Length;
                double mx = (g.A.X + g.B.X) * 0.5 - from.x, mz = (g.A.Y + g.B.Y) * 0.5 - from.z;
                double d = mx * mx + mz * mz;
                if (grade >= 0.04 && d < bestNear)
                {
                    bestNear = d;
                    near = g;
                }
                if (grade > bestSteep)
                {
                    bestSteep = grade;
                    steepest = g;
                }
            }
            CityX.World.Plan.EdgeGeometry pick = near ?? steepest;
            if (pick == null)
            {
                return false;
            }
            double x = (pick.A.X + pick.B.X) * 0.5, z = (pick.A.Y + pick.B.Y) * 0.5;
            destination = new Vector3((float)x, (float)world.Terrain.ContinuousHeightAt(x, z) + 2.0f, (float)z);
            return true;
        }

        /// <summary>The nearest point of an expressway's main deck, in the middle of its nearer carriageway, a couple of metres up.</summary>
        private static bool TryFindNearestHighway(CityWorld world, Vector3 from, out Vector3 destination)
        {
            destination = Vector3.zero;
            double best = double.MaxValue;
            foreach (CityX.World.Plan.ViaductPath path in world.Plan.Data.Expressways.Paths)
            {
                if (path.Kind != CityX.World.Plan.ViaductKind.Mainline) continue;
                for (int i = 0; i < path.Count; i++)
                {
                    double dx = path.P[i].X - from.x, dz = path.P[i].Y - from.z;
                    double d = dx * dx + dz * dz;
                    if (d >= best) continue;
                    best = d;
                    CityX.World.Plan.Vec2 lane = path.P[i] + path.N[i] * (path.HalfLeft[i] * 0.5);
                    destination = new Vector3((float)lane.X, (float)path.Top[i] + 2.0f, (float)lane.Y);
                }
            }
            return best < double.MaxValue;
        }

        /// <summary>
        /// "/car pose" -- ported from dev_console.gd's own _car_command.
        /// Logs the nearest car's own DriverSeat/PassengerSeat local
        /// position/rotation so they can be nudged directly by dragging
        /// the two child objects in the Editor on Mustang.prefab (or on a
        /// spawned instance) and re-checking here, the same "cheap to
        /// correct once actually seen" workflow the Godot source's own
        /// comment describes for its own Remote-tab equivalent.
        /// </summary>
        private void CarCommand(string sub)
        {
            if (sub != "pose")
            {
                Log(DevCommands.UsageFor("car"));
                return;
            }

            Transform origin = Vitals != null ? Vitals.transform : transform;
            Car nearest = null;
            float nearestDistanceSquared = float.MaxValue;
            foreach (Car candidate in FindObjectsByType<Car>(FindObjectsSortMode.None))
            {
                float distanceSquared = (candidate.transform.position - origin.position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearest = candidate;
                }
            }
            if (nearest == null)
            {
                Log("no car nearby -- spawn one with /spawn mustang first");
                return;
            }

            Log($"DriverSeat local pos {FormatVector3(nearest.DriverSeat.localPosition)}");
            Log($"PassengerSeat local pos {FormatVector3(nearest.PassengerSeat.localPosition)}");
        }

        /// <summary>/celshade [on|off]: the cel look (CelShading: the toon light and the black lines) on or off; with no word, the other way.</summary>
        private void CelShadeCommand(string which)
        {
            bool on = which == "on" || (which != "off" && !CelShading.Enabled);
            CelShading.Set(on);
            Log("cel shading " + (on ? "on" : "off"));
        }

        /// <summary>/grapple [ragdoll|classic]: the swing hangs the body limp from the rope (GrappleSwing.RagdollMode) or keeps the upright swing; with no word, the other way.</summary>
        private void GrappleCommand(string which)
        {
            bool ragdoll = which == "ragdoll" || (which != "classic" && !CityX.Character.GrappleSwing.RagdollMode);
            CityX.Character.GrappleSwing.RagdollMode = ragdoll;
            Log("grapple swing " + (ragdoll ? "ragdoll" : "classic"));
        }

        /// <summary>/movement [gta|classic]: the body turns to where it walks (PlayerLocomotion.FaceMovement) or always faces the camera; with no word, the other way.</summary>
        private void MovementCommand(string which)
        {
            bool gta = which == "gta" || (which != "classic" && !CityX.Character.PlayerLocomotion.FaceMovement);
            CityX.Character.PlayerLocomotion.FaceMovement = gta;
            Log("movement " + (gta ? "gta" : "classic"));
        }

        /// <summary>/outlines &lt;pixels&gt;: the black lines' width on a screen 1080 tall (scaled to this one); 0 draws none.</summary>
        private void OutlinesCommand(string pixels)
        {
            if (pixels == null || !float.TryParse(pixels, NumberStyles.Float, CultureInfo.InvariantCulture, out float width) || width < 0.0f)
            {
                Log(DevCommands.UsageFor("outlines"));
                return;
            }
            CelShading.SetOutlineWidth(width);
            Log($"outlines {width.ToString("0.##", CultureInfo.InvariantCulture)} px");
        }
    }
}
