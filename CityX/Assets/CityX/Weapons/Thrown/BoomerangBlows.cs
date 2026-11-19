namespace CityX.Weapons
{
    /// <summary>
    /// HOW MANY PEOPLE A THROW MAY STILL HIT GOING OUT (docs/BOOMERANG.md; owner, 2026-11-18: "return to the thrower on the first enemy it hits instead of
    /// traveling through a bunch of them", going on through people being something to unlock and upgrade later). Going out a throw hits one body, and one more for
    /// each it may pass through, and turns back off the last. Coming home is not counted: it hits everyone it passes ("it want it to hit people on the way back").
    /// </summary>
    public sealed class BoomerangBlows
    {
        /// <summary>It goes on through everyone, as the giant one did until 2026-11-18.</summary>
        public const int Unlimited = int.MaxValue;

        private int _left;

        /// <param name="passesThrough">How many bodies it goes on through after the first it hits.</param>
        public BoomerangBlows(int passesThrough)
        {
            _left = passesThrough >= Unlimited ? Unlimited : System.Math.Max(0, passesThrough) + 1;
        }

        /// <summary>It has hit all it may going out.</summary>
        public bool Spent => _left <= 0;

        /// <summary>It struck a body. True if that was its last: going out, it turns for home there.</summary>
        public bool Strike()
        {
            if (_left != Unlimited && _left > 0)
            {
                _left--;
            }
            return Spent;
        }
    }
}
