namespace CityX.Weapons
{
    /// <summary>
    /// A staff's left click, as time (docs/STAFFS.md): pressed, it starts charging (when the caster allows: mana, a shot not too soon after the
    /// last); held, the charge grows; let go, the shot leaves <see cref="ReleaseDelaySeconds"/> later, as Shadow Flare's surge appears 8 frames
    /// after its charge is released (the staff is up by then); a click is a shot with almost no charge. Every staff shares it; what a charge
    /// buys is the staff's own business. Plain class, tested without a scene.
    /// </summary>
    public sealed class StaffCharge
    {
        public enum Step
        {
            None,
            /// <summary>The click went down: charging from now.</summary>
            Started,
            /// <summary>The shot leaves now, with <see cref="ReleasedSeconds"/> of charge.</summary>
            Fire,
        }

        public const float ReleaseDelaySeconds = 8.0f / 60.0f;

        /// <summary>How long the staff stays thrust out after a shot, seconds.</summary>
        public const float FollowThroughSeconds = 0.35f;

        /// <summary>The quickest one shot follows another, seconds from a release to the next charge.</summary>
        public readonly float ShotSeconds;

        private float _releaseIn = -1.0f;
        private float _ready;
        private float _followThrough;

        public StaffCharge(float shotSeconds)
        {
            ShotSeconds = shotSeconds;
        }

        public bool Charging { get; private set; }

        /// <summary>How long this charge has been held, seconds.</summary>
        public float Seconds { get; private set; }

        /// <summary>The charge the shot on its way out was let go with.</summary>
        public float ReleasedSeconds { get; private set; }

        /// <summary>Let go, the shot about to leave.</summary>
        public bool Releasing => _releaseIn >= 0.0f;

        /// <summary>Charging, about to shoot, or just after a shot: the staff is held out.</summary>
        public bool Busy => Charging || Releasing || _followThrough > 0.0f;

        /// <summary>Runs one frame: <paramref name="held"/> is the click; <paramref name="canStart"/> whether a charge may begin.</summary>
        public Step Tick(bool held, bool canStart, float deltaTime)
        {
            if (_ready > 0.0f)
            {
                _ready -= deltaTime;
            }
            if (_followThrough > 0.0f)
            {
                _followThrough -= deltaTime;
            }
            if (Releasing)
            {
                _releaseIn -= deltaTime;
                if (_releaseIn >= 0.0f)
                {
                    return Step.None;
                }
                _releaseIn = -1.0f;
                _followThrough = FollowThroughSeconds;
                return Step.Fire;
            }
            if (!Charging)
            {
                if (!held || !canStart || _ready > 0.0f)
                {
                    return Step.None;
                }
                Charging = true;
                Seconds = 0.0f;
                return Step.Started;
            }
            if (held)
            {
                Seconds += deltaTime;
                return Step.None;
            }
            Charging = false;
            ReleasedSeconds = Seconds;
            _releaseIn = ReleaseDelaySeconds;
            _ready = ShotSeconds;
            return Step.None;
        }

        /// <summary>Drops a charge, or a shot not yet gone, without shooting: true if there was one.</summary>
        public bool Cancel()
        {
            bool had = Charging || Releasing;
            Charging = false;
            _releaseIn = -1.0f;
            return had;
        }
    }
}
