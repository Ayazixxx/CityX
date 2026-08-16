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
            "celshade",
            "equip",
            "unequip",
        };

        public static string UsageFor(string verb)
        {
            switch (verb)
            {
                case "help": return "/help -- this list";
                case "weather": return "/weather <rain|clear>";
                case "time":
                    return "/time <sunrise|morning|noon|afternoon|sunset|night|midnight|0.0-1.0>";
                case "fly": return "/fly -- toggles the free camera";
                case "infinitehealth": return "/infinitehealth -- toggles";
                case "infinitesprint": return "/infinitesprint -- toggles";
                case "xp": return "/xp <amount>";
                case "spawn": return "/spawn <dummy|civilian|radio|bottle|jeans|cloak|grapple>";
                case "celshade": return "/celshade -- toggles toon shading on every character skin";
                case "equip": return "/equip <bottle|jeans|cloak|grapple>";
                case "unequip": return "/unequip <hand_r|legs|top>";
                default: return null;
            }
        }
    }
}
