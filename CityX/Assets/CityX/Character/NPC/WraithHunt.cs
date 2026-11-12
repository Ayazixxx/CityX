using UnityEngine;

namespace CityX.Character
{
    /// <summary>What the Wraith is doing about the player near it.</summary>
    public enum WraithPhase
    {
        Drifting,
        Stalking,
        Lunging,
        Biting,
        Thrown,
        Recovering,

        /// <summary>The punch: drawn back, then swung over and down onto the prey (owner, 2026-11-11: "give the wraith a second attack where he punches").</summary>
        WindingUp,

        /// <summary>Through the punch and back to its stance.</summary>
        Punching,
    }

    /// <summary>Which attack the Wraith makes.</summary>
    public enum WraithAttack
    {
        Bite,
        Punch,
    }

    /// <summary>
    /// THE WRAITH'S HUNT (owner, 2026-11-05: "try to sneak up behind you and sink his teeth into your neck"). PURE: where it and its prey are go in; its phase, how it moves and the
    /// damage due come out. It only comes on while the prey's body is facing away: faced, it hangs back and goes round. It lunges only from behind and within reach, committed once it
    /// has, half a second the prey can hear and turn or dash in. Reading which way a body faces is not reading its input (the owner's rule on fair enemies). The numbers are a first go.
    /// </summary>
    public sealed class WraithHunt
    {
        /// <summary>It notices a player this near, and loses one this far off, metres.</summary>
        public const float NoticeMetres = 16.0f;

        public const float LoseMetres = 28.0f;

        /// <summary>How fast it comes on unseen, comes on while watched from far off, and goes round while watched close to, metres a second. A player walks at 4.</summary>
        public const float SneakSpeed = 3.2f;

        public const float WarySpeed = 1.0f;

        public const float CircleSpeed = 1.8f;

        /// <summary>The point it makes for: this far behind its prey, metres.</summary>
        public const float BehindMetres = 1.4f;

        /// <summary>Watched, it comes no nearer than this, metres.</summary>
        public const float KeepOffMetres = 5.0f;

        /// <summary>It counts as in front of a body whose facing is this close to it (the dot: within 60 degrees), and behind one this far off the other way.</summary>
        public const float SeenDot = 0.5f;

        public const float BehindDot = -0.3f;

        /// <summary>It lunges from within this, metres.</summary>
        public const float LungeMetres = 2.4f;

        /// <summary>The recording's segments, seconds.</summary>
        public const float LungeSeconds = 0.5f;

        public const float BiteSeconds = 1.9f;

        public const float ThrownSeconds = 0.77f;

        public const float RecoverSeconds = 0.6f;

        /// <summary>It will not hunt again for this long after a bite or a miss, seconds.</summary>
        public const float CooldownSeconds = 8.0f;

        /// <summary>This one's rest after a bite or a miss, seconds: <see cref="CooldownSeconds"/>, or a summon's shorter one.</summary>
        public float RestSeconds = CooldownSeconds;

        /// <summary>
        /// Goes straight in from any side (a summon fighting for its summoner): an enemy fighting it always turns to face it, so waiting for
        /// its back it would only ever circle.
        /// </summary>
        public bool Bold;

        /// <summary>The bite: what sinking its teeth in does, then every <see cref="BiteTickSeconds"/> while it holds on. Owner: "a lot" (a player has 100).</summary>
        public const float SinkDamage = 30.0f;

        public const float BiteTickDamage = 6.0f;

        public const float BiteTickSeconds = 0.45f;

        /// <summary>How long its bite stuns, each time, seconds.</summary>
        public const float BiteHitstunSeconds = 0.4f;

        /// <summary>
        /// The punch's recording (SourceArt/WraithPunch): drawn back and swung over and down onto the prey, then through and back to its
        /// stance, seconds. The blow lands as the swing ends.
        /// </summary>
        public const float PunchSwingSeconds = 0.5f;

        public const float PunchFollowSeconds = 0.517f;

        /// <summary>What the punch does, and how long it stuns. The bite (30, then 6 a time) is the worse to be caught by.</summary>
        public const float PunchDamage = 20.0f;

        public const float PunchHitstunSeconds = 0.5f;

        /// <summary>The share of attacks after the first that are punches (owner, 2026-11-11: "after that it is 50/50").</summary>
        public const double PunchShare = 0.5;

        /// <summary>
        /// Which attack it makes (owner, 2026-11-11: "he always uses the bite as his first attack but after that it is 50/50 whether he does
        /// the punch or bite, but make it so if he is fighting a large opponent like crimson claw for example then he only uses the punch").
        /// <paramref name="madeOnThisPrey"/>: attacks already made on this prey; <paramref name="roll"/>: 0 to 1.
        /// </summary>
        public static WraithAttack Choose(int madeOnThisPrey, bool largePrey, double roll)
        {
            if (largePrey)
            {
                return WraithAttack.Punch;
            }
            if (madeOnThisPrey == 0)
            {
                return WraithAttack.Bite;
            }
            return roll < PunchShare ? WraithAttack.Punch : WraithAttack.Bite;
        }

        /// <summary>The dice its 50/50 is rolled on.</summary>
        public System.Random Dice = new System.Random();

        private int _madeOnThisPrey;

        /// <summary>The attack under way, or the last made.</summary>
        public WraithAttack Attack { get; private set; }

        /// <summary>A different prey: its first attack is a bite again.</summary>
        public void NewPrey()
        {
            _madeOnThisPrey = 0;
        }

        /// <summary>The prey as the Wraith sees it.</summary>
        public struct Prey
        {
            public bool Exists;
            public Vector3 Position;
            public Vector3 Forward;

            /// <summary>Beyond its teeth just now: dashing, or otherwise not to be hit.</summary>
            public bool Evading;

            /// <summary>A large body (the Crimson Claw): only punched.</summary>
            public bool Large;

            /// <summary>How far its body reaches out from where it stands, metres: it is lunged at from that much further off.</summary>
            public float Radius;
        }

        private float _sinceBiteTick;

        public WraithPhase Phase { get; private set; } = WraithPhase.Drifting;

        /// <summary>How long it has been in this phase, seconds.</summary>
        public float PhaseSeconds { get; private set; }

        /// <summary>How long until it will hunt again, seconds.</summary>
        public float Cooldown { get; private set; }

        /// <summary>True on the tick the phase changed.</summary>
        public bool Changed { get; private set; }

        /// <summary>How it goes this tick while stalking, metres a second.</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>The damage its teeth do this tick.</summary>
        public float DamageDue { get; private set; }

        /// <summary>Whether it should go looking for prey at all (a search costs).</summary>
        public bool Hunting => Phase != WraithPhase.Drifting || Cooldown <= 0.0f;

        /// <summary>
        /// One tick. <paramref name="reached"/>: its teeth have reached the prey's neck (a bite), or its fist the prey (a punch).
        /// </summary>
        public void Tick(float deltaTime, Vector3 self, in Prey prey, bool reached)
        {
            Changed = false;
            DamageDue = 0.0f;
            Velocity = Vector3.zero;
            PhaseSeconds += deltaTime;
            Cooldown = Mathf.Max(0.0f, Cooldown - deltaTime);

            switch (Phase)
            {
                case WraithPhase.Drifting:
                    if (Cooldown <= 0.0f && prey.Exists && Flat(prey.Position - self).magnitude <= NoticeMetres)
                    {
                        Enter(WraithPhase.Stalking);
                    }
                    break;
                case WraithPhase.Stalking:
                    Stalk(self, prey);
                    break;
                case WraithPhase.Lunging:
                    if (PhaseSeconds >= LungeSeconds)
                    {
                        if (reached && prey.Exists && !prey.Evading)
                        {
                            Enter(WraithPhase.Biting);
                            DamageDue = SinkDamage;
                            _sinceBiteTick = 0.0f;
                        }
                        else
                        {
                            Enter(WraithPhase.Thrown);
                        }
                    }
                    break;
                case WraithPhase.Biting:
                    if (!prey.Exists || PhaseSeconds >= BiteSeconds)
                    {
                        Enter(WraithPhase.Thrown);
                        break;
                    }
                    _sinceBiteTick += deltaTime;
                    if (_sinceBiteTick >= BiteTickSeconds && !prey.Evading)
                    {
                        _sinceBiteTick -= BiteTickSeconds;
                        DamageDue = BiteTickDamage;
                    }
                    break;
                case WraithPhase.Thrown:
                    if (PhaseSeconds >= ThrownSeconds)
                    {
                        Enter(WraithPhase.Recovering);
                    }
                    break;
                case WraithPhase.Recovering:
                    if (PhaseSeconds >= RecoverSeconds)
                    {
                        Enter(WraithPhase.Drifting);
                        Cooldown = RestSeconds;
                    }
                    break;
                case WraithPhase.WindingUp:
                    if (PhaseSeconds >= PunchSwingSeconds)
                    {
                        if (reached && prey.Exists && !prey.Evading)
                        {
                            DamageDue = PunchDamage;
                        }
                        Enter(WraithPhase.Punching);   // through, hit or miss
                    }
                    break;
                case WraithPhase.Punching:
                    if (PhaseSeconds >= PunchFollowSeconds)
                    {
                        // The follow-through ends in its stance: no getting back up, as after being thrown off a bite.
                        Enter(WraithPhase.Drifting);
                        Cooldown = RestSeconds;
                    }
                    break;
            }
        }

        private void Stalk(Vector3 self, in Prey prey)
        {
            if (!prey.Exists || Flat(prey.Position - self).magnitude > LoseMetres)
            {
                Enter(WraithPhase.Drifting);
                return;
            }
            Vector3 fromPrey = Flat(self - prey.Position);
            float distance = fromPrey.magnitude;
            Vector3 away = distance > 1e-4f ? fromPrey / distance : -Flat(prey.Forward).normalized;
            Vector3 facing = Flat(prey.Forward).normalized;
            float faced = Vector3.Dot(facing, away);
            if ((faced <= BehindDot || Bold) && distance <= LungeMetres + prey.Radius)
            {
                Attack = Choose(_madeOnThisPrey++, prey.Large, Dice.NextDouble());
                Enter(Attack == WraithAttack.Bite ? WraithPhase.Lunging : WraithPhase.WindingUp);
                return;
            }
            if (Bold)
            {
                Velocity = Toward(-fromPrey, SneakSpeed);
                return;
            }

            Vector3 toGoal = Flat(prey.Position - facing * BehindMetres - self);
            if (faced >= SeenDot)
            {
                if (distance <= KeepOffMetres)
                {
                    // Round the shorter way toward its back, and out to where it keeps off.
                    float side = Vector3.Cross(facing, away).y >= 0.0f ? 1.0f : -1.0f;
                    Vector3 round = Vector3.Cross(Vector3.up, away) * side;
                    Velocity = (round + away * Mathf.Clamp01((KeepOffMetres - distance) / KeepOffMetres)).normalized * CircleSpeed;
                }
                else
                {
                    Velocity = Toward(toGoal, WarySpeed);
                }
                return;
            }
            // Alongside, it would cut across the prey's front to make for its back: it swings wide of the body.
            if (faced > BehindDot && distance < KeepOffMetres * 0.5f)
            {
                float side = Vector3.Cross(facing, away).y >= 0.0f ? 1.0f : -1.0f;
                Velocity = (Vector3.Cross(Vector3.up, away) * side + toGoal.normalized).normalized * SneakSpeed;
                return;
            }
            Velocity = Toward(toGoal, SneakSpeed);
        }

        private static Vector3 Toward(Vector3 flat, float speed)
        {
            return flat.sqrMagnitude > 1e-4f ? flat.normalized * speed : Vector3.zero;
        }

        private void Enter(WraithPhase phase)
        {
            Phase = phase;
            PhaseSeconds = 0.0f;
            Changed = true;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);
    }
}
