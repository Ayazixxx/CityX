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

        /// <summary>The bite: what sinking its teeth in does, then every <see cref="BiteTickSeconds"/> while it holds on. Owner: "a lot" (a player has 100).</summary>
        public const float SinkDamage = 30.0f;

        public const float BiteTickDamage = 6.0f;

        public const float BiteTickSeconds = 0.45f;

        /// <summary>How long its bite stuns, each time, seconds.</summary>
        public const float BiteHitstunSeconds = 0.4f;

        /// <summary>The prey as the Wraith sees it.</summary>
        public struct Prey
        {
            public bool Exists;
            public Vector3 Position;
            public Vector3 Forward;

            /// <summary>Beyond its teeth just now: dashing, or otherwise not to be hit.</summary>
            public bool Evading;
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
        /// One tick. <paramref name="mouthOnNeck"/>: its teeth have reached the prey's neck.
        /// </summary>
        public void Tick(float deltaTime, Vector3 self, in Prey prey, bool mouthOnNeck)
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
                        if (mouthOnNeck && prey.Exists && !prey.Evading)
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
                        Cooldown = CooldownSeconds;
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
            if (faced <= BehindDot && distance <= LungeMetres)
            {
                Enter(WraithPhase.Lunging);
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
