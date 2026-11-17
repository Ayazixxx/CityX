namespace CityX.Character
{
    /// <summary>
    /// Every word of the tutorial in one place: each step's objective and hint, and everything Old Guy says. A first draft for the owner to
    /// rewrite (docs/TUTORIAL.md walks through it); change the words here and nothing else needs to change.
    /// </summary>
    public static class TutorialScript
    {
        public static string Objective(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.GetIn: return "Get into Old Guy's house";
                case TutorialStep.FindWeapon: return "Find something to fight with";
                case TutorialStep.Intruder: return "Get rid of the intruder upstairs";
                case TutorialStep.ReportBack: return "Tell Old Guy the house is clear";
                case TutorialStep.Power: return "Turn the power back on";
                case TutorialStep.BackToOldGuy: return "Talk to Old Guy";
                case TutorialStep.Stats: return "Spend your stat points";
                case TutorialStep.Skill: return "Learn a skill";
                case TutorialStep.LastTalk: return "Talk to Old Guy";
                default: return "";
            }
        }

        public static string Hint(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.GetIn: return "Break the boards on the front door, or a window.";
                case TutorialStep.FindWeapon: return "Look around the house. E picks things up and opens drawers.";
                case TutorialStep.Intruder: return "Left click attacks. Double tap Shift to dash.";
                case TutorialStep.ReportBack: return "He's in his den on the main floor. E talks.";
                case TutorialStep.Power: return "The fuse box is in the workshop, by the garage.";
                case TutorialStep.BackToOldGuy: return "He's in his den.";
                case TutorialStep.Stats: return "Press C, then Player, then Stats.";
                case TutorialStep.Skill: return "Press C, then Player, then Skills.";
                case TutorialStep.LastTalk: return "He's in his den.";
                default: return "";
            }
        }

        // ---- Old Guy, outside, before you take him up on it ----

        public const string FallLine = "Dude I just saw you fall from the sky";
        public const string Offer = "I can show you the ropes around here if you'd like";
        public const string OfferYes = "Sure";
        public const string OfferNo = "I'll figure things out myself";
        public const string TurnedDown = "Oh alright then";

        // ---- once you say yes ----

        public static readonly string[] Accepted =
        {
            "Alright. See this house? It's mine.",
            "Some guy with a sword broke in a few days ago and boarded me out of my own place.",
            "Get in there and get rid of him for me. Bust the boards off the front door, or smash a window, I don't care.",
            "There's stuff lying around in there you can fight with. I'll wait out here.",
        };

        public static readonly string[] WhileOutside =
        {
            "He's upstairs somewhere. Go on, I'll be right here.",
        };

        // ---- in his den, the house cleared ----

        public static readonly string[] HouseCleared =
        {
            "You did it! Ha, I knew you had it in you.",
            "Thing is, the power's been out since he showed up.",
            "The fuse box is down in the workshop, by the garage. Flip it back on for me.",
            "And stay out of the boxes in the cellar. That's my private stuff.",
        };

        public static readonly string[] PowerReminder =
        {
            "The fuse box is in the workshop, downstairs by the garage.",
        };

        /// <summary>Said first if you opened his boxes in the cellar anyway.</summary>
        public const string SawTheBoxes = "Were you going through my boxes? I told you to leave those alone...";

        public static readonly string[] PowerBack =
        {
            "Lights are back on! Look at you, you're getting stronger already.",
            "Every time you level up you get stat points. Press C, go to Player, then Stats, and spend them.",
            "Skill points go in your skill tree. That's where you learn new moves and spells. Same menu, under Skills.",
        };

        public static readonly string[] StatsReminder =
        {
            "Press C, go to Player, then Stats.",
        };

        public static readonly string[] SkillReminder =
        {
            "Press C, go to Player, then Skills.",
        };

        // ---- the last talk ----

        public static readonly string[] BeforeTheFaction =
        {
            "There you go. Now you know enough to get yourself killed out there.",
            "Spells you learn go on your hotbar. The number keys cast them.",
        };

        public const string FactionAsk = "People round here stick together in factions. What do you say, want to start one with me?";
        public const string FactionYes = "Let's do it";
        public const string FactionNo = "Not really";
        public const string FactionYesAnswer = "Ha! Alright partner, we'll make it official later.";
        public const string FactionNoAnswer = "Suit yourself. The offer stands.";

        public static readonly string[] Farewell =
        {
            "One more thing. Find yourself a room somewhere, put a bed in it, and it's yours. That's how claiming works around here.",
            "Alright, I think we're safe now.",
        };

        // ---- the hub ----

        public const string HubBanner = "Welcome to the hub";
    }
}
