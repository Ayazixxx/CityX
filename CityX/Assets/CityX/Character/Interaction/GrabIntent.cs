namespace CityX.Character
{
    /// <summary>
    /// Whether the player MEANS to grab a wall: the grab key (Space) has been held for a moment, and the last grab
    /// is not still fresh.
    ///
    /// WHY THIS EXISTS. Climbing used to start the instant Shift and W were held against anything within a metre
    /// -- and Shift+W is simply how the player RUNS. So it fired running up a stair (the wall at the top),
    /// jumping through a window (the frame) and running through a doorway (the jamb). Every game that gets this
    /// right separates "moving" from "asking to climb": Assassin's Creed starts a climb only while a dedicated
    /// parkour-up button is HELD (a tap is a jump; holding it "like a gas pedal" is what it calls the wrong way to
    /// play), and ledge-grab systems built in Unity gate on a deliberate press as well. Here Space is that button:
    /// a tap is a jump, a hold at a wall is a grab.
    ///
    /// Pure, so it can be tested without a scene.
    /// </summary>
    public sealed class GrabIntent
    {
        /// <summary>How long Space must be held before it counts as a grab rather than a jump.</summary>
        public const float HoldSeconds = 0.2f;

        /// <summary>After a grab ends (hop-off, exhaustion, topping out) another cannot start for this long.</summary>
        public const float CooldownSeconds = 0.6f;

        private float _held;
        private float _cooldown;
        private bool _needsRelease;

        /// <summary>How long the key has been held for this grab attempt (0 once it was used or released).</summary>
        public float Held => _held;

        /// <summary>True when the player is asking to grab: held long enough, released since last time, not cooling down.</summary>
        public bool Ready => !_needsRelease && _cooldown <= 0.0f && _held >= HoldSeconds;

        /// <summary>Call once a frame with whether the grab key is down.</summary>
        public void Update(bool keyHeld, float deltaTime)
        {
            if (_cooldown > 0.0f)
            {
                _cooldown -= deltaTime;
            }

            if (!keyHeld)
            {
                _held = 0.0f;
                _needsRelease = false;   // let go: the next hold is a new attempt
                return;
            }
            if (!_needsRelease)
            {
                _held += deltaTime;
            }
        }

        /// <summary>A grab began: the same hold cannot start another, and it has to be released before the next.</summary>
        public void Consume()
        {
            _held = 0.0f;
            _needsRelease = true;
        }

        /// <summary>A grab ended: no new one for <see cref="CooldownSeconds"/>, so a hop-off is not undone by the next frame.</summary>
        public void BeginCooldown()
        {
            _cooldown = CooldownSeconds;
            _held = 0.0f;
            _needsRelease = true;
        }

        /// <summary>For tests: an intent that has already been held long enough.</summary>
        public static GrabIntent AlreadyHeld()
        {
            var intent = new GrabIntent();
            intent.Update(true, HoldSeconds + 0.01f);
            return intent;
        }
    }
}
