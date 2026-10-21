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
        /// <summary>The foot placement's state on the player (FootPlanting), live; /footik on|off switches it.</summary>
        private void FootIkCommand(string what)
        {
            if (_playerLocomotion == null)
            {
                Log("no player to look at");
                return;
            }
            if (what == "on" || what == "off")
            {
                _playerLocomotion.FootIkEnabled = what == "on";
                Log($"foot placement {what}");
                return;
            }
            CityX.Character.FootPlanting feet = _playerLocomotion.FootPlanting;
            string Foot(int i) => feet.IsLocked(i) ? "held" : feet.IsOnFloor(i) ? "on the floor" : "lifted";
            Log($"foot placement: {(_playerLocomotion.FootIkEnabled ? "on" : "OFF")}, {(_playerLocomotion.FootIkActive ? "placing" : "not placing (in the air, on a wall, in a vault, a seat or a dance)")}; " +
                $"hip drop {feet.HipDrop * 100:0.0} cm, landing dip {feet.LandingDip * 100:0.0} cm; left {Foot(0)} ({feet.Of(0).Weight:0.00}), right {Foot(1)} ({feet.Of(1).Weight:0.00}); " +
                $"clip {feet.ClipGroundSpeed:0.0} m/s against body {feet.BodyGroundSpeed:0.0} m/s ({(feet.LocksViable ? "feet can be held" : "too far apart to hold the feet")}), " +
                $"stride x{_playerLocomotion.StrideScale:0.00}, ground normal {_playerLocomotion.GroundNormal:F2}");
        }

        /// <summary>/posture: the shared posture every body stands in (BodyPostureSettings), shown or set live.</summary>
        private void PostureCommand(string what, string value)
        {
            var settings = CityX.Character.BodyPostureSettings.Current;
            if (string.IsNullOrEmpty(what) || what == "show")
            {
                Log($"posture: head {settings.HeadPitchUp:0.#} up, {settings.HeadTurnRight:0.#} turned right, {settings.HeadTiltRight:0.#} tilted right (degrees), legs {settings.LegsBack:0.###} m back, head moved {settings.HeadBack:0.###} m back and {settings.HeadUp:0.###} m up -- /posture head|turn|tilt <degrees> | /posture legs|headback|headup <metres>");
                return;
            }
            if (value == null || !float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float number))
            {
                Log(DevCommands.UsageFor("posture"));
                return;
            }
            switch (what)
            {
                case "head":
                    settings.HeadPitchUp = Mathf.Clamp(number, -30.0f, 30.0f);
                    break;
                case "turn":
                    settings.HeadTurnRight = Mathf.Clamp(number, -45.0f, 45.0f);
                    break;
                case "tilt":
                    settings.HeadTiltRight = Mathf.Clamp(number, -30.0f, 30.0f);
                    break;
                case "headback":
                    settings.HeadBack = Mathf.Clamp(number, -0.3f, 0.3f);
                    break;
                case "headup":
                    settings.HeadUp = Mathf.Clamp(number, -0.3f, 0.3f);
                    break;
                case "legs":
                    settings.LegsBack = Mathf.Clamp(number, -0.15f, 0.15f);
                    break;
                default:
                    Log(DevCommands.UsageFor("posture"));
                    return;
            }
            Log($"posture: head {settings.HeadPitchUp:0.#} up, {settings.HeadTurnRight:0.#} turned right, {settings.HeadTiltRight:0.#} tilted right, legs {settings.LegsBack:0.###} m back, head moved {settings.HeadBack:0.###} m back and {settings.HeadUp:0.###} m up");
        }

        /// <summary>
        /// "/skills": what the player has learnt, their skill points and their bar; "/skills points 5", "/skills reset", "/skills all",
        /// "/skills new" (a brand new character's: vaulting and the starting point) -- for trying the skill menu out.
        /// </summary>
        private void SkillsCommand(string verb, string amount)
        {
            PlayerSkills skills = PlayerSkills.Local;
            if (skills == null && Vitals != null)
            {
                skills = PlayerSkills.On(Vitals.gameObject);
            }
            if (skills == null)
            {
                Log("no body here");
                return;
            }
            PlayerVitals vitals = skills.GetComponent<PlayerVitals>();
            switch (verb)
            {
                case null:
                    break;
                case "points":
                    if (vitals == null || amount == null || !int.TryParse(amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int points))
                    {
                        Log(DevCommands.UsageFor("skills"));
                        return;
                    }
                    skills.SetPoints(vitals.SkillPoints + points);
                    break;
                case "reset":
                    Log($"forgot everything but vaulting: {skills.ResetAll()} point(s) back");
                    break;
                case "all":
                    skills.LearnEverything();
                    break;
                case "new":
                    skills.ResetAll();
                    skills.SetPoints(CityX.Core.CharacterSave.StartingSkillPoints);
                    break;
                default:
                    Log(DevCommands.UsageFor("skills"));
                    return;
            }

            var learnt = new System.Collections.Generic.List<string>(skills.Book.Learnt);
            learnt.Sort(System.StringComparer.Ordinal);
            var bar = new System.Text.StringBuilder();
            for (int slot = 0; slot < Hotbar.SlotCount; slot++)
            {
                bar.Append(Hotbar.KeyLabel(slot)).Append('=').Append(skills.Bar[slot] ?? "-").Append(slot < Hotbar.SlotCount - 1 ? "  " : string.Empty);
            }
            Log($"{skills.Points} skill point(s); learnt: {string.Join(", ", learnt)}");
            Log("bar: " + bar);
        }

        private void Experience(string amount)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            if (amount == null || !int.TryParse(amount, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int points) || points <= 0)
            {
                Log(DevCommands.UsageFor("xp"));
                return;
            }

            int levelBefore = Vitals.Level;
            Vitals.AddXp(points);
            int levelsGained = Vitals.Level - levelBefore;
            Log(levelsGained > 0
                ? $"+{points} xp -- +{levelsGained} level(s), now level {Vitals.Level} ({Vitals.StatPoints} point(s) to spend)"
                : $"+{points} xp -- {Vitals.Experience}/{Vitals.ExperienceToNextLevel} to next level");
        }

        /// <summary>"/level 20": the level outright, with the points reaching it brings (PlayerVitals.SetLevel).</summary>
        private void SetLevel(string value)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            if (value == null || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) || level < 1)
            {
                Log(DevCommands.UsageFor("level"));
                return;
            }
            int levelBefore = Vitals.Level;
            (int stat, int skill) = Vitals.SetLevel(level);
            // The skill menu and the bar redraw on a change of points.
            PlayerSkills skills = PlayerSkills.Local != null ? PlayerSkills.Local : PlayerSkills.On(Vitals.gameObject);
            skills?.SetPoints(Vitals.SkillPoints);
            Log($"level {levelBefore} -> {Vitals.Level}: {stat:+0;-0;0} stat point(s), {skill:+0;-0;0} skill point(s) " +
                $"(now {Vitals.StatPoints} stat, {Vitals.SkillPoints} skill to spend)");
        }
    }
}
