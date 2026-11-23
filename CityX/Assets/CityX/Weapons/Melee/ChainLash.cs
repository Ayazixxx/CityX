using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE FREE LENGTH OF A CHAIN, link by link (docs/CHAIN.md): hung from the fist, thrown where a swing throws it, fallen where it falls. Pure:
    /// positions in and positions out, stepped by a time, so it is tested without a scene; ChainWeapon gives it the fist and draws its links.
    ///
    /// HOW A SWING MOVES IT. An attack says which way the chain is THROWN from the fist at each moment (SwordFrame.Blade, the game's own keys).
    /// Each link is pulled toward its place on a line drawn that way from the fist, but the line each link follows is the one the fist drew a
    /// little EARLIER, later and later toward the far end (<see cref="TipLagSeconds"/>): so through a swing the chain bows back against the
    /// way it is going, the far end last and fastest, as a whip does, and seen from behind a lash is one clean arc. How hard the links are
    /// pulled is the swing's (<see cref="Pull"/>): all the way through the strike, loosely in the wind-up, and not at all once it is over.
    ///
    /// EVERYTHING ELSE IS A HANGING CHAIN. Each link keeps the speed it had (a swing's included: let go of, the chain carries on round and
    /// falls), drops under gravity, stays within a link's length of its neighbours and lies on the ground rather than in it. Between swings it
    /// hangs from the fist and drags behind a body that walks.
    ///
    /// PAID OUT OF THE FIST (owner, 2026-11-22: "the chain can appear shorter [in] your hand and then extend really longer for the attacks ...
    /// im pretty sure in infamous second son and hunter x hunter the length of the chain extends they just hide it cleanly"). The links not yet
    /// paid out sit in the fist, unseen; the far end is always the same link.
    /// </summary>
    public sealed class ChainLash
    {
        /// <summary>The far end follows the line the fist drew this long ago, seconds; a link halfway along, half as long ago.</summary>
        public const float TipLagSeconds = 0.05f;

        /// <summary>
        /// The chain is thrown where the attack will throw it this much later, seconds: the links arrive late (they are pulled to the line, and the
        /// far ones to an earlier line), and a lash seen landing a moment after its blow has landed reads as a miss that hit.
        /// </summary>
        public const float ThrownAheadSeconds = 0.045f;

        /// <summary>How quickly a link closes on its place on the line at full pull, per second: within a frame or two.</summary>
        public const float PullRate = 55.0f;

        public const float Gravity = 19.0f;

        /// <summary>The share of its speed a link keeps each second in the air: a chain is heavy and swings on, but does not swing for ever.</summary>
        public const float SpeedKeptPerSecond = 0.25f;

        /// <summary>And along the ground: it scrapes to a stop.</summary>
        public const float SpeedKeptOnGroundPerSecond = 0.002f;

        /// <summary>The longest step taken at once, seconds: a slow frame is taken in several.</summary>
        public const float StepSeconds = 1.0f / 120.0f;

        public const int MostSteps = 6;
        public const int ConstraintPasses = 12;

        /// <summary>How many moments of the fist's line are remembered: enough for <see cref="TipLagSeconds"/> at any frame rate.</summary>
        private const int Remembered = 48;

        private readonly Vector3[] _at;
        private readonly Vector3[] _was;
        private readonly Vector3[] _line = new Vector3[Remembered];
        private readonly float[] _lineTime = new float[Remembered];
        private int _lineHead = -1;
        private int _lineCount;
        private float _clock;

        public ChainLash(int links, float pitch, float linkRadius)
        {
            Links = links;
            Pitch = pitch;
            LinkRadius = linkRadius;
            _at = new Vector3[links];
            _was = new Vector3[links];
        }

        /// <summary>How many links the free length has, the fist's first and the far end last.</summary>
        public int Links { get; }

        /// <summary>From one link's middle to the next, metres.</summary>
        public float Pitch { get; }

        public float LinkRadius { get; }

        /// <summary>Where each link is (world).</summary>
        public Vector3[] At => _at;

        /// <summary>The fastest any link met the ground or scraped along it in the last <see cref="Step"/>, m/s, and where; 0 when none did.</summary>
        public float GroundBlowSpeed { get; private set; }

        public Vector3 GroundBlowAt { get; private set; }

        /// <summary>The first link out of the fist when <paramref name="paidOut"/> links are out: those before it are in the fist.</summary>
        public int FirstOut(float paidOut) => Mathf.Clamp(Links - Mathf.CeilToInt(paidOut), 0, Links - 1);

        /// <summary>Every link at <paramref name="point"/> and still (the chain put in the fist).</summary>
        public void Gather(Vector3 point)
        {
            for (int i = 0; i < Links; i++)
            {
                _at[i] = point;
                _was[i] = point;
            }
            _lineCount = 0;
            _lineHead = -1;
        }

        /// <summary>Link <paramref name="index"/> put at <paramref name="point"/> and held there this frame (a link still wound on the hand).</summary>
        public void Hold(int index, Vector3 point)
        {
            _at[index] = point;
            _was[index] = point;
        }

        /// <summary>Link <paramref name="index"/> drawn <paramref name="share"/> of the way to <paramref name="point"/>, keeping its speed (a link coming off the hand, or going onto it).</summary>
        public void Draw(int index, Vector3 point, float share)
        {
            Vector3 moved = (point - _at[index]) * Mathf.Clamp01(share);
            _at[index] += moved;
            _was[index] += moved;
        }

        /// <summary>
        /// How hard a swing pulls the chain to its line <paramref name="seconds"/> into an attack of these phases, 0 to 1: coming on through the
        /// wind-up (the chain is flung back, loosely), whole from the hold to the end of the follow-through, and let go of through the settle, so
        /// the recovery is a chain falling.
        /// </summary>
        public static float Pull(float windUp, float hold, float strike, float follow, float settle, float seconds)
        {
            float held = windUp + hold;
            float followed = held + strike + follow;
            if (seconds <= 0.0f)
            {
                return 0.0f;
            }
            if (seconds < windUp)
            {
                return WindUpPull * Mathf.SmoothStep(0.0f, 1.0f, seconds / Mathf.Max(windUp, 1e-4f));
            }
            if (seconds < held)
            {
                return Mathf.Lerp(WindUpPull, 1.0f, (seconds - windUp) / Mathf.Max(hold, 1e-4f));
            }
            if (seconds <= followed)
            {
                return 1.0f;
            }
            return 1.0f - Mathf.SmoothStep(0.0f, 1.0f, (seconds - followed) / Mathf.Max(settle, 1e-4f));
        }

        /// <summary>How hard the chain is pulled to its line at the top of the wind-up.</summary>
        public const float WindUpPull = 0.10f;

        /// <summary>
        /// Moves the chain on by <paramref name="deltaTime"/>: hung from <paramref name="fist"/>, pulled by <paramref name="pull"/> (0 to 1)
        /// toward the line thrown along <paramref name="thrown"/> (a unit vector, world), <paramref name="paidOut"/> links of it out of the
        /// fist, over ground at <paramref name="groundY"/>.
        /// </summary>
        public void Step(Vector3 fist, Vector3 thrown, float pull, float paidOut, float groundY, float deltaTime)
        {
            GroundBlowSpeed = 0.0f;
            if (deltaTime <= 0.0f)
            {
                return;
            }
            int steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / StepSeconds), 1, MostSteps);
            float dt = deltaTime / steps;
            Vector3 fistWas = _lineCount > 0 ? _fistWas : fist;
            for (int s = 1; s <= steps; s++)
            {
                StepOnce(Vector3.Lerp(fistWas, fist, s / (float)steps), thrown, pull, paidOut, groundY, dt);
            }
            _fistWas = fist;
        }

        private Vector3 _fistWas;

        private void StepOnce(Vector3 fist, Vector3 thrown, float pull, float paidOut, float groundY, float dt)
        {
            _clock += dt;
            Remember(thrown.sqrMagnitude > 1e-6f ? thrown.normalized : Vector3.down);
            int first = FirstOut(paidOut);
            float floor = groundY + LinkRadius;
            float kept = Mathf.Pow(SpeedKeptPerSecond, dt), keptOnGround = Mathf.Pow(SpeedKeptOnGroundPerSecond, dt);
            for (int i = 0; i < Links; i++)
            {
                if (i <= first)
                {
                    _at[i] = fist;
                    _was[i] = fist;
                    continue;
                }
                bool lying = _at[i].y <= floor + 1e-3f;
                Vector3 speed = (_at[i] - _was[i]) * (lying ? keptOnGround : kept);
                _was[i] = _at[i];
                _at[i] += speed + Vector3.down * (Gravity * dt * dt);
            }

            if (pull > 0.0f)
            {
                // The line, link by link from the fist: each stretch of it laid the way the fist threw it that much earlier.
                float share = 1.0f - Mathf.Exp(-PullRate * pull * dt);
                int out_ = Links - 1 - first;
                Vector3 place = fist;
                for (int i = first + 1; i < Links; i++)
                {
                    float along = (i - first) / (float)Mathf.Max(out_, 1);
                    place += ThrownAgo(TipLagSeconds * along) * Pitch;
                    _at[i] += (place - _at[i]) * share;
                }
            }

            for (int pass = 0; pass < ConstraintPasses; pass++)
            {
                // No further than a link's length from the one before it (the fist's does not give), and on the ground rather than in it.
                // Nearer is slack, as a chain bunches: pushed apart to a link's length instead, links let out of the fist together folded back
                // up on each other and stayed so.
                for (int i = first + 1; i < Links; i++)
                {
                    Vector3 between = _at[i] - _at[i - 1];
                    float length = between.magnitude;
                    if (length <= Pitch)
                    {
                        continue;
                    }
                    Vector3 correction = between * ((length - Pitch) / length);
                    if (i - 1 == first)
                    {
                        _at[i] -= correction;
                    }
                    else
                    {
                        _at[i - 1] += correction * 0.5f;
                        _at[i] -= correction * 0.5f;
                    }
                }
                for (int i = first + 1; i < Links; i++)
                {
                    if (_at[i].y < floor)
                    {
                        if (pass == 0)
                        {
                            float blow = (_was[i].y - _at[i].y) / dt;
                            float across = new Vector2(_at[i].x - _was[i].x, _at[i].z - _was[i].z).magnitude / dt;
                            float met = Mathf.Max(blow, across);
                            if (met > GroundBlowSpeed)
                            {
                                GroundBlowSpeed = met;
                                GroundBlowAt = new Vector3(_at[i].x, floor, _at[i].z);
                            }
                        }
                        _at[i].y = floor;
                    }
                }
            }
        }

        private void Remember(Vector3 thrown)
        {
            _lineHead = (_lineHead + 1) % Remembered;
            _line[_lineHead] = thrown;
            _lineTime[_lineHead] = _clock;
            _lineCount = Mathf.Min(_lineCount + 1, Remembered);
        }

        /// <summary>The way the fist threw the chain <paramref name="seconds"/> ago (the oldest remembered, further back than that).</summary>
        private Vector3 ThrownAgo(float seconds)
        {
            float when = _clock - seconds;
            Vector3 later = _line[_lineHead];
            float laterTime = _lineTime[_lineHead];
            for (int n = 1; n < _lineCount; n++)
            {
                int index = (_lineHead - n + Remembered) % Remembered;
                if (_lineTime[index] <= when)
                {
                    float u = Mathf.InverseLerp(_lineTime[index], laterTime, when);
                    return Vector3.Slerp(_line[index], later, u).normalized;
                }
                later = _line[index];
                laterTime = _lineTime[index];
            }
            return later;
        }
    }
}
