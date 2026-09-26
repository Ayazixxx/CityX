using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// What a dev console command does, separated from typing it.
    ///
    /// PURE, AND WITH NO UI AND NO SCENE ANYWHERE IN IT. Parsing a line into a
    /// verb and arguments, and deciding what "/time sunset" means, is the part
    /// that can be wrong in a way a person would not spot -- and it is the part
    /// a test can reach. What is left over is putting text in a box.
    /// </summary>
    public static class DevCommands
    {
        /// <summary>A parsed command line.</summary>
        public readonly struct Line
        {
            /// <summary>Lower case, with any leading slash removed.</summary>
            public readonly string Verb;

            public readonly string[] Arguments;

            public Line(string verb, string[] arguments)
            {
                Verb = verb;
                Arguments = arguments;
            }

            public bool IsEmpty => string.IsNullOrEmpty(Verb);

            public string Argument(int index)
            {
                return Arguments != null && index < Arguments.Length ? Arguments[index] : null;
            }
        }

        /// <summary>
        /// Splits a typed line. The leading slash is optional -- Godot's own
        /// trim_prefix does the same, and somebody who has just been typing in
        /// chat will type one out of habit.
        /// </summary>
        public static Line Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return default;
            }

            string[] parts = text.Trim().Split(
                new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return default;
            }

            string verb = parts[0].ToLowerInvariant().TrimStart('/');
            var arguments = new string[parts.Length - 1];
            Array.Copy(parts, 1, arguments, 0, arguments.Length);
            return new Line(verb, arguments);
        }

        /// <summary>
        /// A named time of day, as seconds into the cycle.
        ///
        /// The full cycle is 34 real minutes, so without this, seeing what a
        /// sunset actually looks like means waiting up to twenty of them.
        ///
        /// THE NAMES ARE DERIVED FROM THE CYCLE'S OWN SPLIT rather than typed
        /// in: the sun crosses the horizon at zero and again at exactly
        /// DaySeconds, so that is where the warm light really is. Retuning the
        /// day/night balance moves these with it.
        /// </summary>
        public static bool TryTimeOfDay(string which, float daySeconds, float cycleSeconds,
            out float seconds)
        {
            float night = cycleSeconds - daySeconds;
            switch (which)
            {
                case "sunrise":
                case "dawn":
                    seconds = 0.0f;
                    return true;
                case "morning":
                    seconds = daySeconds * 0.25f;
                    return true;
                case "noon":
                case "midday":
                case "day":
                    seconds = daySeconds * 0.5f;
                    return true;
                case "afternoon":
                    seconds = daySeconds * 0.75f;
                    return true;
                case "sunset":
                case "dusk":
                case "golden":
                    seconds = daySeconds;
                    return true;
                case "night":
                case "evening":
                    seconds = daySeconds + night * 0.25f;
                    return true;
                case "midnight":
                    seconds = daySeconds + night * 0.5f;
                    return true;
            }

            // A bare fraction of the whole cycle, for anywhere the names do not
            // reach.
            if (float.TryParse(which, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float fraction))
            {
                seconds = Mathf.Clamp01(fraction) * cycleSeconds;
                return true;
            }

            seconds = 0.0f;
            return false;
        }

        /// <summary>Every verb the console answers to, for /help and for a test to sweep.</summary>
        public static readonly IReadOnlyList<string> Verbs = new[]
        {
            "help",
            "weather",
            "time",
            "fly",
            "infinitehealth",
            "infinitesprint",
            "xp",
            "spawn",
            "car",
            "celshade",
            "equip",
            "unequip",
            "goto",
            "jacket",
            "pistol",
            "aim",
            "sword",
            "gun",
            "flight",
        };

        public static string UsageFor(string verb)
        {
            switch (verb)
            {
                case "help": return "/help -- this list";
                case "weather": return "/weather <rain [light|moderate|heavy|0.1-1]|clear|dry|lightning [metres]|status>";
                case "time":
                    return "/time <sunrise|morning|noon|afternoon|sunset|night|midnight|0.0-1.0>";
                case "fly": return "/fly -- toggles the free camera";
                case "infinitehealth": return "/infinitehealth -- toggles";
                case "infinitesprint": return "/infinitesprint -- toggles";
                case "xp": return "/xp <amount>";
                case "spawn": return "/spawn <dummy|civilian|flyer|crimsonclaw|radio|bottle|jeans|cloak|coat|jacket|" +
                    "grapple|pistol|sword|flamethrower|jetpack|parachute|top_hat|farmer_hat|mustang|book>";
                case "car": return "/car pose -- logs the nearest car's seat positions (drag them in the Editor to nudge)";
                case "celshade": return "/celshade -- toggles toon shading on every character skin";
                case "equip": return "/equip <bottle|jeans|cloak|coat|jacket|grapple|pistol|sword|flamethrower|" +
                    "jetpack|parachute|top_hat|farmer_hat>";
                case "unequip": return "/unequip <hand_r|legs|top|back|helmet>";
                case "goto": return "/goto <slope|district> -- teleports to the nearest one";
                case "jacket": return "/jacket <scale|offset> <value>, or /jacket pose -- " +
                    "live-tunes the equipped top's own size/vertical position (wear only one top while tuning)";
                case "pistol": return "/pistol <pos|rot> <x> <y> <z>, /pistol scale <value>, or /pistol pose -- " +
                    "live-tunes the equipped pistol's own grip in the hand";
                case "aim": return "/aim offset <metres>, /aim yaw <degrees>, /aim armyaw <degrees>, or /aim pose -- " +
                    "live-tunes the aim camera's shoulder offset while aiming. /aim yaw rotates the WHOLE " +
                    "character (body, arm, and head together); /aim armyaw nudges just the arm/gun's own " +
                    "angle without moving the body";
                case "sword": return "/sword -- lists every sword-combat number; /sword <name> <value> sets one " +
                    "(e.g. /sword BufferSeconds 0.3, /sword Heavy.Startup 0.8, /sword Light1.Recovery 0.5). " +
                    "Needs a sword equipped. Startup/Active/Recovery are seconds; Active is when the blade can hurt";
                case "gun": return "/gun -- lists the pistol's feel numbers; /gun <name> <value> sets one: " +
                    "recoil (camera kick, degrees), adsrecoil (aiming multiplier), shake (roll jolt), " +
                    "kick / kickangle (the pistol's own snap-back), recover (camera settle rate), " +
                    "sens (mouse scale while aiming), aimspeed (walk-speed scale while aiming). " +
                    "/gun shoulder swaps the camera shoulder (Q). Needs a pistol equipped";
                case "flight": return "/flight -- lists the flying-feel numbers; /flight <name> <value> sets one: " +
                    "fov (degrees the view widens at full speed), kick (extra degrees on a boost), " +
                    "kickseconds, pullback (metres the camera falls back), kickpullback, " +
                    "smooth (camera spring, seconds), streaks (speed lines per second, 0 = off), " +
                    "start / full (the speeds the effects begin and peak at, m/s). Works while flying";
                default: return null;
            }
        }
    }
}
