namespace CityX.Character
{
    /// <summary>
    /// /pushups (owner, 2026-10-14: "it does idle to push up then does push up on repeat until you press and movement key or spell or click (but
    /// you can move your mouse around) but once you move it does push up to idle then you can start moving freely again. but as your doing
    /// pushups your xp goes up 1 xp per push up"): down into a push-up (the pushup_enter clip), push-ups on repeat (pushup, one rep a loop), and
    /// when something calls it off, back up (pushup_exit) -- free to move only once standing. A call to stop while going down is kept: the body
    /// finishes going down, then gets up, with no rep.
    ///
    /// Pure: told how long each clip is, the time and whether something calls it off, it says what phase the body is in and counts the reps.
    /// PlayerInputReader drives it and pays a rep's XP; the Animator follows <see cref="AnimatorPhase"/> (sent to other players with the
    /// other parameters), so a copy goes down, reps and gets up the same.
    /// </summary>
    public sealed class PushUpEmote
    {
        public enum Phase
        {
            Off,
            GoingDown,
            PushingUp,
            GettingUp,
        }

        /// <summary>The experience a finished push-up is worth.</summary>
        public const int XpPerRep = 1;

        public Phase Current { get; private set; }

        /// <summary>Push-ups finished since this one started.</summary>
        public int Reps { get; private set; }

        public float EnterSeconds { get; private set; }
        public float RepSeconds { get; private set; }
        public float ExitSeconds { get; private set; }

        private float _time;
        private bool _stopAsked;

        public PushUpEmote(float enterSeconds, float repSeconds, float exitSeconds)
        {
            SetClipLengths(enterSeconds, repSeconds, exitSeconds);
        }

        /// <summary>The three clips' lengths, seconds (read from the Animator when it has them).</summary>
        public void SetClipLengths(float enterSeconds, float repSeconds, float exitSeconds)
        {
            EnterSeconds = UnityEngine.Mathf.Max(0.05f, enterSeconds);
            RepSeconds = UnityEngine.Mathf.Max(0.05f, repSeconds);
            ExitSeconds = UnityEngine.Mathf.Max(0.05f, exitSeconds);
        }

        /// <summary>Whether the body is held by it: no walking, no turning to the camera, no attacks (the camera still looks round).</summary>
        public bool HoldsTheBody => Current != Phase.Off;

        /// <summary>
        /// The Animator's PushUpPhase: 0 standing, 1 going down, 2 pushing up, 3 getting up (the states follow it; sent to other players with the
        /// other parameters). A number rather than a bool so the states can be entered from anywhere without restarting while it holds.
        /// </summary>
        public int AnimatorPhase => (int)Current;

        /// <summary>Going down or repping (not standing, not getting up).</summary>
        public bool AnimatorPushingUp => Current == Phase.GoingDown || Current == Phase.PushingUp;

        /// <summary>Starts going down; false (and nothing changes) when it is already going.</summary>
        public bool Start()
        {
            if (Current != Phase.Off)
            {
                return false;
            }
            Current = Phase.GoingDown;
            Reps = 0;
            _time = 0.0f;
            _stopAsked = false;
            return true;
        }

        /// <summary>Ends it at once, standing (a blow, death, a menu that takes the body): no getting-up clip.</summary>
        public void Abort()
        {
            Current = Phase.Off;
            _time = 0.0f;
            _stopAsked = false;
        }

        /// <summary>Advances by <paramref name="deltaTime"/>; <paramref name="stop"/> is a key or a click calling it off. Returns the push-ups finished in this step.</summary>
        public int Tick(float deltaTime, bool stop)
        {
            int finished = 0;
            switch (Current)
            {
                case Phase.GoingDown:
                    _stopAsked |= stop;
                    _time += deltaTime;
                    if (_time >= EnterSeconds)
                    {
                        _time -= EnterSeconds;
                        if (_stopAsked)
                        {
                            Current = Phase.GettingUp;
                            _time = 0.0f;
                        }
                        else
                        {
                            Current = Phase.PushingUp;
                        }
                    }
                    break;
                case Phase.PushingUp:
                    if (stop)
                    {
                        Current = Phase.GettingUp;
                        _time = 0.0f;
                        break;
                    }
                    _time += deltaTime;
                    while (_time >= RepSeconds)
                    {
                        _time -= RepSeconds;
                        Reps++;
                        finished++;
                    }
                    break;
                case Phase.GettingUp:
                    _time += deltaTime;
                    if (_time >= ExitSeconds)
                    {
                        Current = Phase.Off;
                        _time = 0.0f;
                    }
                    break;
            }
            return finished;
        }
    }
}
