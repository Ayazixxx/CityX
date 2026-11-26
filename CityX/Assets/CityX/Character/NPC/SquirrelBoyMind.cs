namespace CityX.Character
{
    /// <summary>What Squirrel Boy is doing.</summary>
    public enum SquirrelBoyState
    {
        /// <summary>Minding his own business: nobody has hurt him.</summary>
        Calm,

        /// <summary>Fists up, after whoever is nearest.</summary>
        Fight,

        /// <summary>Hurt badly: running for a wall (or just away, with none near).</summary>
        Flee,

        /// <summary>Going up the wall, hand over hand.</summary>
        Climb,

        /// <summary>Up where he climbed to, out of reach, watching.</summary>
        Perched,
    }

    /// <summary>
    /// SQUIRREL BOY'S MIND (owner, 2026-11-25: "he is non hostile until struck and then he fights unarmed with fists only for now. he is also able to
    /// crawl up walls and will run away and crawl up a wall when low health"; docs/SQUIRREL_BOY.md). Pure, so it is tested without a scene: the
    /// body (SquirrelBoyBrain) tells it what it senses each frame and does what it decides.
    /// <list type="bullet">
    /// <item>Calm until struck; struck, he fights whoever is nearest, with his fists: he closes in (running when far), then throws a combo of
    /// two to four punches, now and then a heavy one, and pauses before the next.</item>
    /// <item>Below <see cref="FleeBelowHealth"/> of his health he runs for the nearest wall he can climb (or straight away from his foe when
    /// there is none) and goes up it; at the top he stays, out of reach. Followed up there, he runs again.</item>
    /// </list>
    /// </summary>
    public sealed class SquirrelBoyMind
    {
        /// <summary>Below this share of his health he runs for a wall.</summary>
        public const float FleeBelowHealth = 0.3f;

        /// <summary>Within this of his foe he punches, metres (a fist reaches 2.5 m: MeleeAttacker.Range).</summary>
        public const float PunchWithinMetres = 2.0f;

        /// <summary>Further than this he runs at his foe rather than walks, metres.</summary>
        public const float RunBeyondMetres = 5.0f;

        /// <summary>Punches a combo, at least and at most, and the share of them that are heavy.</summary>
        public const int FewestPunches = 2;
        public const int MostPunches = 4;
        public const float HeavyShare = 0.2f;

        /// <summary>Seconds between punches of a combo, and his pause after one.</summary>
        public const float BetweenPunches = 0.35f;
        public const float PauseAfterCombo = 0.9f;

        /// <summary>Within this of the wall he runs into it to climb, metres (PlayerClimbing starts on a wall right ahead).</summary>
        public const float AtTheWallMetres = 1.2f;

        /// <summary>Perched, a foe closer than this has followed him up: he runs again, metres.</summary>
        public const float FollowedWithinMetres = 4.0f;

        public struct Senses
        {
            /// <summary>Somebody has hurt him (he stays provoked).</summary>
            public bool Struck;

            public bool HasFoe;
            public float FoeDistance;

            /// <summary>His health, a share of its most.</summary>
            public float Health;

            /// <summary>Knocked off his feet or stunned: he can do nothing this frame.</summary>
            public bool Helpless;

            /// <summary>A punch is still going (the next waits for it).</summary>
            public bool Punching;

            public bool Climbing;

            /// <summary>He has climbed up and off the top of a wall (topped out) since he started fleeing.</summary>
            public bool ClimbedOver;

            public bool HasWall;
            public float WallDistance;
        }

        /// <summary>Where he goes.</summary>
        public enum Goal { Stay, Foe, Wall, AwayFromFoe }

        public struct Intent
        {
            public Goal Goal;
            public bool Run;
            public bool Punch;
            public bool Heavy;

            /// <summary>Into the wall at a run, to start climbing it (PlayerClimbing.TryStartAuto).</summary>
            public bool StartClimb;

            /// <summary>On the wall: up.</summary>
            public bool ClimbUp;
        }

        public SquirrelBoyState State { get; private set; } = SquirrelBoyState.Calm;

        private readonly System.Random _random;
        private int _punchesLeft;
        private float _wait;

        public SquirrelBoyMind(int seed = 1)
        {
            _random = new System.Random(seed);
        }

        public Intent Tick(Senses senses, float deltaTime)
        {
            _wait -= deltaTime;
            Decide(senses);
            if (senses.Helpless)
            {
                return new Intent { Goal = Goal.Stay };
            }
            switch (State)
            {
                case SquirrelBoyState.Fight:
                    return Fight(senses);
                case SquirrelBoyState.Flee:
                    if (!senses.HasWall)
                    {
                        return new Intent { Goal = senses.HasFoe ? Goal.AwayFromFoe : Goal.Stay, Run = senses.HasFoe };
                    }
                    return new Intent { Goal = Goal.Wall, Run = true, StartClimb = senses.WallDistance <= AtTheWallMetres };
                case SquirrelBoyState.Climb:
                    return new Intent { Goal = Goal.Stay, ClimbUp = true };
                default:
                    return new Intent { Goal = Goal.Stay };
            }
        }

        private void Decide(Senses senses)
        {
            bool low = senses.Health < FleeBelowHealth;
            switch (State)
            {
                case SquirrelBoyState.Calm:
                    if (senses.Struck)
                    {
                        State = low ? SquirrelBoyState.Flee : SquirrelBoyState.Fight;
                    }
                    break;
                case SquirrelBoyState.Fight:
                    if (low)
                    {
                        State = SquirrelBoyState.Flee;
                    }
                    break;
                case SquirrelBoyState.Flee:
                    if (senses.Climbing)
                    {
                        State = SquirrelBoyState.Climb;
                    }
                    break;
                case SquirrelBoyState.Climb:
                    if (senses.ClimbedOver)
                    {
                        State = SquirrelBoyState.Perched;
                    }
                    else if (!senses.Climbing)
                    {
                        State = SquirrelBoyState.Flee;   // fell off, or let go: back to the wall
                    }
                    break;
                case SquirrelBoyState.Perched:
                    if (senses.HasFoe && senses.FoeDistance < FollowedWithinMetres)
                    {
                        State = SquirrelBoyState.Flee;   // followed up: off again
                    }
                    break;
            }
        }

        private Intent Fight(Senses senses)
        {
            if (!senses.HasFoe)
            {
                return new Intent { Goal = Goal.Stay };
            }
            if (senses.FoeDistance > PunchWithinMetres)
            {
                _punchesLeft = 0;
                return new Intent { Goal = Goal.Foe, Run = senses.FoeDistance > RunBeyondMetres };
            }
            var intent = new Intent { Goal = Goal.Stay };
            if (senses.Punching || _wait > 0.0f)
            {
                return intent;
            }
            if (_punchesLeft <= 0)
            {
                _punchesLeft = _random.Next(FewestPunches, MostPunches + 1);
            }
            _punchesLeft--;
            intent.Punch = true;
            intent.Heavy = _random.NextDouble() < HeavyShare;
            _wait = _punchesLeft > 0 ? BetweenPunches : PauseAfterCombo;
            return intent;
        }
    }
}
