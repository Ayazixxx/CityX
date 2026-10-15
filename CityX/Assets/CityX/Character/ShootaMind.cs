using UnityEngine;

namespace CityX.Character
{
    /// <summary>What a Shoota is doing.</summary>
    public enum ShootaState
    {
        /// <summary>Standing about: nobody seen yet.</summary>
        Idle,
        /// <summary>Facing the target and shooting in bursts.</summary>
        Engage,
        /// <summary>Running to cover: hurt, or out of rounds.</summary>
        TakeCover,
        /// <summary>Changing the magazine (in cover, if there was any).</summary>
        Reload,
        /// <summary>Lost sight of the target: going to where they were last seen.</summary>
        Chase,
        Dead,
    }

    /// <summary>
    /// THE SHOOTA'S HEAD (owner, 2026-10-15: "a smart hostile npc so if you are nearby and in sight of him he starts shooting at you but also runs
    /// to cover if he is getting damaged and when he needs to reload and stuff like that"). Pure: what it sees, its rounds and its hurts go in
    /// each frame (<see cref="Senses"/>), what to do comes out (<see cref="State"/>, <see cref="ShouldFire"/>); the body (Weapons' ShootaBrain)
    /// does the seeing, the moving and the shooting. The pattern is the classic cover shooter's (Gears of War, The Last of Us): fire from the
    /// open in bursts, break for cover when hurt or dry, reload behind it, stay down a beat, then lean back out.
    /// </summary>
    public sealed class ShootaMind
    {
        /// <summary>How far he notices you, metres, and how wide he looks (degrees either side); once alerted he sees all round.</summary>
        public const float SightRange = 45.0f;

        public const float SightHalfAngle = 75.0f;

        /// <summary>From first seeing you to the first shot: a beat to react, so a target coming round a corner is not shot that very frame.</summary>
        public const float ReactionSeconds = 0.55f;

        /// <summary>A hit this recently sends him to cover.</summary>
        public const float HurtMemorySeconds = 1.2f;

        /// <summary>The least he stays down once in cover, seconds, before leaning back out (longer while reloading).</summary>
        public const float CoverStaySeconds = 2.0f;

        /// <summary>After breaking cover he will not run for it again for this long, unless he runs dry: no ducking at every hit.</summary>
        public const float CoverCooldownSeconds = 4.0f;

        /// <summary>With this few rounds left and in cover anyway, he changes the magazine.</summary>
        public const int LowAmmo = 10;

        /// <summary>Unseen this long, he goes to where you were last seen; this long after that, he gives up.</summary>
        public const float LoseSightSeconds = 2.5f;

        public const float GiveUpSeconds = 15.0f;

        /// <summary>A burst: this many rounds at least and at most, then a pause.</summary>
        public const int BurstMin = 3;

        public const int BurstMax = 7;

        public const float BurstPauseMin = 0.35f;

        public const float BurstPauseMax = 0.85f;

        /// <summary>What the body tells the mind each frame.</summary>
        public struct Senses
        {
            public bool HasTarget;
            public bool SeesTarget;
            public float TargetDistance;
            public int Magazine;
            public int MagazineSize;
            public bool Reloading;
            public bool TookDamage;
            public bool HasCover;
            public bool AtCover;
            public bool Dead;
        }

        public ShootaState State { get; private set; } = ShootaState.Idle;

        /// <summary>Why the current trip to cover was made.</summary>
        public bool CoverForReload { get; private set; }

        /// <summary>Seconds since the target was last seen (very large if never).</summary>
        public float SinceSeen { get; private set; } = 1e6f;

        /// <summary>Rounds left in the current burst, and the pause before the next.</summary>
        public int BurstLeft { get; private set; }

        public float PauseLeft { get; private set; }

        /// <summary>The trigger is pulled this frame (the body fires at the gun's rate while it is).</summary>
        public bool ShouldFire { get; private set; }

        private float _sinceHurt = 1e6f;
        private float _reaction;
        private float _inCover;
        private float _coverCooldown;
        private uint _random;

        public ShootaMind(int seed = 1)
        {
            _random = (uint)seed * 2654435761u + 12345u;
            if (_random == 0)
            {
                _random = 1;
            }
        }

        /// <summary>One frame: the senses in, the state (and whether to pull the trigger) out.</summary>
        public ShootaState Tick(in Senses senses, float deltaTime)
        {
            ShouldFire = false;
            if (senses.Dead)
            {
                return State = ShootaState.Dead;
            }
            _sinceHurt = senses.TookDamage ? 0.0f : _sinceHurt + deltaTime;
            SinceSeen = senses.SeesTarget ? 0.0f : SinceSeen + deltaTime;
            _coverCooldown = Mathf.Max(0.0f, _coverCooldown - deltaTime);
            bool hurt = _sinceHurt < HurtMemorySeconds;
            bool dry = senses.Magazine <= 0;

            switch (State)
            {
                case ShootaState.Idle:
                    if (senses.HasTarget && (senses.SeesTarget || senses.TookDamage))
                    {
                        Go(ShootaState.Engage);
                    }
                    break;

                case ShootaState.Engage:
                    if (dry)
                    {
                        GoToCoverOrReload(senses, forReload: true);
                    }
                    else if (hurt && senses.HasCover && _coverCooldown <= 0.0f)
                    {
                        GoToCover(forReload: false);
                    }
                    else if (!senses.HasTarget || SinceSeen > LoseSightSeconds)
                    {
                        Go(senses.HasTarget ? ShootaState.Chase : ShootaState.Idle);
                    }
                    else if (senses.SeesTarget)
                    {
                        _reaction += deltaTime;
                        if (_reaction >= ReactionSeconds)
                        {
                            Trigger(deltaTime);
                        }
                    }
                    break;

                case ShootaState.TakeCover:
                    if (!senses.HasCover)
                    {
                        // Nowhere to hide after all: change the magazine where he stands, or fight on.
                        GoToCoverOrReload(senses, forReload: dry || CoverForReload);
                        if (State == ShootaState.TakeCover)
                        {
                            Go(ShootaState.Engage);
                        }
                    }
                    else if (senses.AtCover)
                    {
                        _inCover += deltaTime;
                        bool shortOfRounds = senses.Magazine < senses.MagazineSize && (CoverForReload || senses.Magazine <= LowAmmo);
                        if (shortOfRounds && !senses.Reloading)
                        {
                            Go(ShootaState.Reload);
                        }
                        else if (_inCover >= CoverStaySeconds && !hurt)
                        {
                            LeaveCover();
                        }
                    }
                    break;

                case ShootaState.Reload:
                    if (hurt && senses.HasCover && !senses.AtCover && _coverCooldown <= 0.0f)
                    {
                        GoToCover(forReload: true);   // caught changing the magazine in the open: run with it
                    }
                    else if (!senses.Reloading && senses.Magazine >= senses.MagazineSize)
                    {
                        if (senses.AtCover)
                        {
                            _inCover += deltaTime;
                            if (_inCover >= CoverStaySeconds && !hurt)
                            {
                                LeaveCover();
                            }
                        }
                        else
                        {
                            Go(ShootaState.Engage);
                        }
                    }
                    break;

                case ShootaState.Chase:
                    if (dry)
                    {
                        GoToCoverOrReload(senses, forReload: true);
                    }
                    else if (senses.SeesTarget)
                    {
                        Go(ShootaState.Engage);
                    }
                    else if (!senses.HasTarget || SinceSeen > LoseSightSeconds + GiveUpSeconds)
                    {
                        Go(ShootaState.Idle);
                    }
                    break;
            }
            return State;
        }

        /// <summary>A round went (the body fired one): counts the burst down, and pauses at its end.</summary>
        public void Fired()
        {
            BurstLeft = Mathf.Max(0, BurstLeft - 1);
            if (BurstLeft == 0)
            {
                PauseLeft = Mathf.Lerp(BurstPauseMin, BurstPauseMax, Next());
            }
        }

        private void Trigger(float deltaTime)
        {
            if (PauseLeft > 0.0f)
            {
                PauseLeft -= deltaTime;
                return;
            }
            if (BurstLeft <= 0)
            {
                BurstLeft = BurstMin + (int)(Next() * (BurstMax - BurstMin + 1));
                BurstLeft = Mathf.Clamp(BurstLeft, BurstMin, BurstMax);
            }
            ShouldFire = true;
        }

        private void GoToCoverOrReload(in Senses senses, bool forReload)
        {
            if (senses.HasCover && !senses.AtCover)
            {
                GoToCover(forReload);
            }
            else if (forReload)
            {
                Go(ShootaState.Reload);
            }
        }

        private void GoToCover(bool forReload)
        {
            CoverForReload = forReload;
            Go(ShootaState.TakeCover);
        }

        private void LeaveCover()
        {
            _coverCooldown = CoverCooldownSeconds;
            Go(ShootaState.Engage);
        }

        private void Go(ShootaState state)
        {
            if (state != ShootaState.TakeCover && state != ShootaState.Reload)
            {
                _inCover = 0.0f;
            }
            if (state == ShootaState.TakeCover)
            {
                _inCover = 0.0f;
            }
            if (state == ShootaState.Engage && State != ShootaState.Engage)
            {
                _reaction = 0.0f;
                BurstLeft = 0;
                PauseLeft = 0.0f;
            }
            State = state;
        }

        private float Next()
        {
            _random ^= _random << 13;
            _random ^= _random >> 17;
            _random ^= _random << 5;
            return (_random >> 8) / 16777216.0f;
        }

        /// <summary>
        /// How far off the aim a round may go, degrees: a little at any range, more the further and the faster the target moves, and more as a
        /// burst climbs. A standing target at 20 m is mostly hit; a sprinting one at 40 m mostly missed.
        /// </summary>
        public static float SpreadDegrees(float distance, float targetSpeed, int roundInBurst)
        {
            return Mathf.Min(9.0f, 1.2f + Mathf.Max(0.0f, distance) * 0.035f + Mathf.Max(0.0f, targetSpeed) * 0.55f + roundInBurst * 0.45f);
        }
    }
}
