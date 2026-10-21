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

        /// <summary>
        /// Swaps every eligible renderer's shader between the normal URP
        /// Lit and CityX/ToonLit -- EVERY ONE OF THEM, not just characters.
        /// Character skins and the whole city (ground, pavement, concrete,
        /// doors, guard rails) are built on the exact same "Universal
        /// Render Pipeline/Lit" shader asset (confirmed directly: every one
        /// of those .mat files points at the identical shader GUID), so
        /// asking "is this material currently Lit" is what "all the
        /// graphics" actually means here -- a name-based allowlist
        /// (formerly PlayerSkin/CivilianSkin only) would need updating by
        /// hand for every new material the city ever adds, and would have
        /// silently stayed character-only forever if nobody remembered to.
        ///
        /// OPAQUE SURFACES ONLY. Glass (CityGlassDark/Lit/Cracked) is built
        /// on this SAME shader with its Surface Type property set to
        /// Transparent rather than a different shader asset -- ToonLit has
        /// no Blend state at all (Tags declare it Opaque, "Queue" =
        /// "Geometry"), so toon-shading a window would not restyle it, it
        /// would make it suddenly solid, undoing the real work that went
        /// into making a building's glass see-through. _Surface (0 =
        /// Opaque, 1 = Transparent) is the same URP Lit property that
        /// decision is already stored in, so checking it is enough to skip
        /// every window without needing a name check for them either.
        ///
        /// WALKS LIVE RENDERERS, NOT A WIRED Material[] -- a material
        /// asset like PlayerSkin.mat is only what a renderer STARTS on.
        /// CharacterIdentity.Recolour/ApplyPaint read `renderer.material`
        /// (deliberately, to tint one body without recolouring everyone
        /// sharing the asset), and that auto-instances a private copy the
        /// first time it's touched -- which for any character who has ever
        /// been painted or recoloured is well before this ever runs. Toggling
        /// the shared asset's own .shader missed that copy entirely, which is
        /// why this used to look like it did nothing on a painted player
        /// while still working on an untouched civilian.
        /// </summary>
        private void ToggleCelShade()
        {
            if (_litShader == null) _litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_toonShader == null) _toonShader = Shader.Find("CityX/ToonLit");
            if (_litShader == null || _toonShader == null)
            {
                Log("cel shade shader missing");
                return;
            }

            _celShadeOn = !_celShadeOn;
            Shader target = _celShadeOn ? _toonShader : _litShader;

            int changed = 0;
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                // Read sharedMaterial to test it -- it returns whatever is
                // actually bound (instance or asset) without instancing
                // anything just to look. Only a match then goes through
                // .material, so an untouched civilian population (or an
                // untouched building) still gets one shared swap instead of
                // a thousand new per-renderer clones.
                Material current = renderer.sharedMaterial;
                if (current == null || !IsToonableMaterial(current))
                {
                    continue;
                }
                renderer.material.shader = target;
                changed++;
            }

            Log("cel shading " + (_celShadeOn ? "on" : "off") + $" ({changed} renderer(s))");
        }

        /// <summary>Public, not private, so a test can drive this exact rule directly.</summary>
        public static bool IsToonableMaterial(Material material)
        {
            if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
            {
                return false;
            }
            // Missing the property at all means it was never given a
            // surface-type choice, which URP Lit only omits for a material
            // stuck on its own default (Opaque) -- treated as opaque rather
            // than excluded, so a plain fresh material is never silently
            // skipped just for not being touched yet.
            return !material.HasProperty("_Surface") || material.GetFloat("_Surface") == 0.0f;
        }
    }
}
