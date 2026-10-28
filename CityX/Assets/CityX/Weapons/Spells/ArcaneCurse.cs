using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The cursed orbs one caster has on one target, as numbers (ArcaneOrbRules): how many, how long until they strike, and whether they are
    /// striking. A hit adds orbs and starts the clock (or, on a target already cursed, pushes the strike back, to no later than 7.5 seconds
    /// from the first hit); nothing is added once they strike. Plain class: <see cref="CursedOrbs"/> shows it.
    /// </summary>
    public sealed class ArcaneCurse
    {
        public int Orbs { get; private set; }

        /// <summary>Until the strike, seconds.</summary>
        public float SecondsLeft { get; private set; }

        /// <summary>Since the first hit, seconds.</summary>
        public float Age { get; private set; }

        public bool Striking { get; private set; }

        /// <summary>A hit by a shot carrying <paramref name="orbsInShot"/>: how many of them were placed (none once striking or full).</summary>
        public int Hit(int orbsInShot)
        {
            if (Striking || orbsInShot <= 0)
            {
                return 0;
            }
            if (Orbs == 0)
            {
                Age = 0.0f;
                SecondsLeft = ArcaneOrbRules.StrikeDelaySeconds;
            }
            else
            {
                SecondsLeft = Mathf.Min(SecondsLeft + ArcaneOrbRules.ExtensionFor(orbsInShot), ArcaneOrbRules.LatestStrikeSeconds - Age);
            }
            int placed = Mathf.Clamp(Mathf.Min(orbsInShot, ArcaneOrbRules.MaxOrbsPerShot), 0, ArcaneOrbRules.MaxOrbsOnOne - Orbs);
            Orbs += placed;
            return placed;
        }

        /// <summary>Another machine's word for this curse (a copy shows the caster's): its orbs and how long they have left.</summary>
        public void Match(int orbs, float secondsLeft)
        {
            if (Striking)
            {
                return;
            }
            if (Orbs == 0)
            {
                Age = 0.0f;
            }
            Orbs = Mathf.Clamp(orbs, 0, ArcaneOrbRules.MaxOrbsOnOne);
            SecondsLeft = Mathf.Clamp(secondsLeft, 0.0f, ArcaneOrbRules.LatestStrikeSeconds);
        }

        /// <summary>Runs the clock: true on the step the orbs begin to strike.</summary>
        public bool Tick(float deltaTime)
        {
            if (Orbs == 0 || Striking)
            {
                return false;
            }
            Age += deltaTime;
            SecondsLeft -= deltaTime;
            if (SecondsLeft > 0.0f)
            {
                return false;
            }
            SecondsLeft = 0.0f;
            Striking = true;
            return true;
        }
    }
}
