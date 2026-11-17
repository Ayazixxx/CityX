using System;

namespace CityX.Character
{
    /// <summary>The tutorial's steps after Old Guy's offer, in order (docs/TUTORIAL.md).</summary>
    public enum TutorialStep
    {
        /// <summary>Not taken up: Old Guy's offer stands, or was turned down.</summary>
        Offered,
        GetIn,
        FindWeapon,
        Intruder,

        /// <summary>Talk to Old Guy in his den.</summary>
        ReportBack,
        Power,

        /// <summary>Talk to Old Guy: the level-up, stats and skills.</summary>
        BackToOldGuy,
        Stats,
        Skill,

        /// <summary>Talk to Old Guy: spells, the faction, claiming, and "safe now".</summary>
        LastTalk,

        /// <summary>The bullets through the windows, the fade, the hub.</summary>
        Ambush,
        Done,
    }

    /// <summary>What the world shows this frame, as far as the tutorial asks.</summary>
    public struct TutorialFacts
    {
        public bool Inside;
        public bool HasWeapon;
        public bool IntruderDown;
        public bool PowerOn;
        public bool StatPointsLeft;
        public bool SkillPointsLeft;
    }

    /// <summary>
    /// Where the player is in the tutorial. A step done by doing (getting in, the power) passes as soon as the world shows it done, even
    /// if it was done early; a step done by talking passes when that conversation with Old Guy ends.
    /// </summary>
    public sealed class TutorialProgress
    {
        public TutorialStep Step { get; private set; } = TutorialStep.Offered;

        /// <summary>The player opened Old Guy's boxes in the cellar after he said not to: a karma choice (GAME_DESIGN.md).</summary>
        public bool OpenedTheBoxes { get; set; }

        /// <summary>The answer to starting a faction with Old Guy; null until it is given.</summary>
        public bool? StartedAFaction { get; set; }

        public event Action<TutorialStep> StepChanged;

        /// <summary>Taken up and not over yet.</summary>
        public bool Running => Step != TutorialStep.Offered && Step != TutorialStep.Done;

        public static bool IsTalk(TutorialStep step) =>
            step == TutorialStep.ReportBack || step == TutorialStep.BackToOldGuy || step == TutorialStep.LastTalk;

        public void Accept()
        {
            if (Step == TutorialStep.Offered)
            {
                GoTo(TutorialStep.GetIn);
            }
        }

        /// <summary>A conversation with Old Guy has ended: a talk step is passed.</summary>
        public void Talked()
        {
            if (IsTalk(Step))
            {
                GoTo(Step + 1);
            }
        }

        /// <summary>
        /// Passes the current step if the world shows it done. One step a call: whatever a new step sets going (the level-up before the stat
        /// points) is seen in the next call's facts, not judged on this call's.
        /// </summary>
        public void Observe(in TutorialFacts facts)
        {
            if (Done(Step, facts))
            {
                GoTo(Step + 1);
            }
        }

        public static bool Done(TutorialStep step, in TutorialFacts facts)
        {
            switch (step)
            {
                case TutorialStep.GetIn: return facts.Inside || facts.IntruderDown;
                case TutorialStep.FindWeapon: return facts.HasWeapon || facts.IntruderDown;
                case TutorialStep.Intruder: return facts.IntruderDown;
                case TutorialStep.Power: return facts.PowerOn;
                case TutorialStep.Stats: return !facts.StatPointsLeft;
                case TutorialStep.Skill: return !facts.SkillPointsLeft;
                default: return false;
            }
        }

        /// <summary>The ending has played: the player is at the hub.</summary>
        public void Finish()
        {
            if (Step == TutorialStep.Ambush)
            {
                GoTo(TutorialStep.Done);
            }
        }

        /// <summary>Straight to a step (the console's /tutorial, to try one part).</summary>
        public void JumpTo(TutorialStep step) => GoTo(step);

        private void GoTo(TutorialStep step)
        {
            if (step == Step)
            {
                return;
            }
            Step = step;
            StepChanged?.Invoke(step);
        }
    }
}
