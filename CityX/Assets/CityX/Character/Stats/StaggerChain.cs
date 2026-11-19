namespace CityX.Character
{
    /// <summary>
    /// NO STUN-LOCK. Outside an attack with hyper-armour every blow staggers (PlayerVitals, Dark Souls 3's rule), and a weapon whose next blow
    /// lands before its last one's stun is over kept a body staggered for good: in the combat playtest (2026-11-18) the bat, the sledgehammer
    /// and the boomerang each beat a swordsman and Crazy Guy with every hit a stagger and the enemy never once swinging back (0 damage taken in
    /// six fights), and the fists did it to Crazy Guy (20 hits, 20 staggers).
    ///
    /// So a body staggered <see cref="MostInARow"/> times in a row, each blow landing before the last one's stun had worn off, shrugs off
    /// staggers until <see cref="ShrugSeconds"/> past the end of that last stun: the blows still land and hurt, they just do not hold it, and
    /// it gets its turn (to swing back, guard or dash out). A chain of three or four is untouched. Every body alike, a player too. Timed by
    /// the game clock passed in, not a clock of its own: not every body counts its stun down.
    /// </summary>
    public sealed class StaggerChain
    {
        /// <summary>How many staggers in a row a body takes before it shrugs the next ones off.</summary>
        public const int MostInARow = 4;

        /// <summary>A stagger this long after the last one's stun wore off still counts as the same chain.</summary>
        public const float InARowGraceSeconds = 0.35f;

        /// <summary>How long past the end of the last stun in the chain blows do not stagger.</summary>
        public const float ShrugSeconds = 1.0f;

        private float _stunEndsAt = float.NegativeInfinity;
        private float _shrugUntil = float.NegativeInfinity;
        private int _inARow;

        /// <summary>How many staggers the current chain holds.</summary>
        public int InARow => _inARow;

        /// <summary>True while blows land without staggering, at game time <paramref name="now"/> (seconds).</summary>
        public bool ShruggingAt(float now) => now < _shrugUntil;

        /// <summary>
        /// A blow at game time <paramref name="now"/> would stagger the body for <paramref name="stunSeconds"/>: true if it does (and it is
        /// counted), false while it is shrugged off.
        /// </summary>
        public bool TryStagger(float now, float stunSeconds)
        {
            if (ShruggingAt(now))
            {
                return false;
            }
            bool inARow = now <= _stunEndsAt + InARowGraceSeconds;
            _inARow = inARow ? _inARow + 1 : 1;
            // Longest wins, as the stun itself does (PlayerVitals.HitstunRemaining).
            _stunEndsAt = inARow ? System.Math.Max(_stunEndsAt, now + stunSeconds) : now + stunSeconds;
            if (_inARow >= MostInARow)
            {
                _shrugUntil = _stunEndsAt + ShrugSeconds;
                _inARow = 0;
                _stunEndsAt = float.NegativeInfinity;
            }
            return true;
        }

        public void Reset()
        {
            _stunEndsAt = float.NegativeInfinity;
            _shrugUntil = float.NegativeInfinity;
            _inARow = 0;
        }
    }
}
