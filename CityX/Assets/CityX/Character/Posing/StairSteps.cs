using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE FEET ON A STAIR (owner, 2026-11-03, of the walk clip's legs bent onto the steps: "this looks all fucked up ... look up how to do good stairs animations with IK";
    /// docs/STAIRS.md). A clip made for flat ground cannot be warped onto stairs on legs this short: it swings a foot 0.6 m either way and kicks it up behind, and held to the
    /// steps that folds the knee and curls the shin. So on a stair the clip keeps the arms and the legs are all IK, stepped like this (a working stair stepper's design, and
    /// SwordFootwork's): each step is planned onto a real tread, the ball of the foot on it and the heel over the edge; the TRAILING foot goes next, once the body has gone far
    /// enough past it; it goes in an arc that rises before it travels going up, and travels before it falls going down, so it clears the edges; a planted foot never moves, it
    /// rolls up onto its ball as the body leaves it; and the hips come down only as far as the feet need.
    ///
    /// PURE: no scene, no bones. The foot IK hands in the body and a way to read the ground, and puts the ankles where this says: so a copy of another player's body steps too,
    /// with nothing sent.
    /// </summary>
    public sealed class StairSteps
    {
        /// <summary>Reads the ground's height under a point, near the point's own height.</summary>
        public delegate bool GroundAt(Vector3 at, out float height);

        /// <summary>The legs are taken from the clip and given back over this long, seconds.</summary>
        public const float FadeSeconds = 0.12f;

        /// <summary>How long a step is in the air at a walk and at a sprint, seconds. At these speeds, on these legs, it is a jog: both feet are off the ground for a moment.</summary>
        public const float WalkStepSeconds = 0.2f;

        public const float SprintStepSeconds = 0.17f;

        /// <summary>The speeds those belong to, metres a second.</summary>
        public const float WalkSpeed = 4.0f;

        public const float SprintSpeed = 7.5f;

        /// <summary>Slower than this the body is standing: a planted foot stays planted.</summary>
        public const float StandingSpeed = 0.3f;

        /// <summary>Where a step lands, the ball of the foot this far past the hips when it lands. Going down less: the leg reaches down as well as forward, and only so far.</summary>
        public const float LandAheadGoingUp = 0.4f;

        public const float LandAheadGoingDown = 0.25f;

        /// <summary>The trailing foot sets off once the hips have gone this far past its ball. Going up the leg is soon straight; going down it soon folds behind the body.</summary>
        public const float LiftBehindGoingUp = 0.15f;

        public const float LiftBehindGoingDown = 0.12f;

        /// <summary>A planted ankle this far past what its leg can reach lifts at once, metres: held, it has left its tread.</summary>
        public const float OutOfReachBy = 0.01f;

        /// <summary>A step lands at least this far past the foot left behind: never on the same tread.</summary>
        public const float LeastStepPast = 0.25f;

        /// <summary>The other foot may set off once a step is this far through...</summary>
        public const float OverlapShare = 0.5f;

        /// <summary>...or whatever the step, once it is this much further behind than it should have lifted, metres.</summary>
        public const float LiftAnywayPast = 0.1f;

        /// <summary>How high the arc goes over the straight line between the treads, metres.</summary>
        public const float ArcHeight = 0.1f;

        /// <summary>How much of a step the height takes: going up it is done by then, going down it starts that much before the end.</summary>
        public const float HeightShare = 0.8f;

        /// <summary>The most a planted foot rolls up onto its ball, degrees, and how far behind the hips it has fully (metres): a foot behind the body is on its toes.</summary>
        public const float MostRollDegrees = 50.0f;

        public const float FullRollBehind = 0.45f;

        /// <summary>A foot coming down has its toes this far up going up and this far down going down, degrees: toes first down a stair.</summary>
        public const float LandingPitchGoingUp = -8.0f;

        public const float LandingPitchGoingDown = 30.0f;

        /// <summary>Going down, a planted foot this far ahead of the hips is on its toes by that much (metres).</summary>
        public const float ToesFirstAhead = 0.25f;

        /// <summary>How quickly a foot rolls, seconds.</summary>
        private const float RollSeconds = 0.04f;

        /// <summary>The hips are always this low on a stair, metres, the legs a little bent, and at most this: the legs are only 0.77 m.</summary>
        public const float LeastDrop = 0.06f;

        /// <summary>Going down lower: the leading leg reaches down to a tread below the hips' line.</summary>
        public const float LeastDropGoingDown = 0.1f;

        public const float MostDrop = 0.22f;

        /// <summary>How quickly the hips come down and go back up, seconds: slowly enough not to bob with each step.</summary>
        private const float DropSeconds = 0.1f;

        private const float RiseSeconds = 0.2f;

        /// <summary>A leg reaches this share of its length.</summary>
        public const float ReachShare = 0.985f;

        /// <summary>An ankle stays this share of the leg below its hip: the knee does not fold past about 75 degrees (the shin curled up against the thigh, the look to avoid).</summary>
        public const float LeastFoldShare = 0.6f;

        /// <summary>A foot this far from the hips (leg lengths: a teleport, a shove) is put in place rather than stepped.</summary>
        public const float LostDistance = 1.8f;

        /// <summary>A landing is moved this far at a time, this many times, until the foot's uphill end is on its tread (within this of its height), metres.</summary>
        public const float TreadFitStep = 0.05f;

        public const int TreadFitTries = 8;

        public const float TreadFitWithin = 0.01f;

        /// <summary>The body this frame.</summary>
        public struct Body
        {
            /// <summary>Between the hips along the ground, at the height of the floor they are over.</summary>
            public Vector3 Centre;

            /// <summary>The way the body is going along the ground (a unit vector; the way it faces when it is standing), and how fast.</summary>
            public Vector3 Along;

            public float Speed;

            /// <summary>The ground's rise per metre along that way: positive going up.</summary>
            public float Slope;

            /// <summary>The way the body faces, degrees about the vertical.</summary>
            public float Yaw;

            /// <summary>The hip joints before any drop from here, and each foot's distance out from the middle.</summary>
            public Vector3 LeftHip;

            public Vector3 RightHip;

            public float HalfWidth;

            public float LegLength;

            /// <summary>The ankle above a flat sole, and the ball of the foot in front of the ankle, metres.</summary>
            public float SoleHeight;

            public float BallForward;

            /// <summary>From the ball of the foot to the tip of the toes and back to the heel, metres.</summary>
            public float ToeTip;

            public float HeelBack;

            /// <summary>Where the clip has the ankles: where the feet start from when this takes them.</summary>
            public Vector3 ClipLeft;

            public Vector3 ClipRight;
        }

        /// <summary>One foot's answer.</summary>
        public struct Foot
        {
            /// <summary>Where the ankle goes, in the world.</summary>
            public Vector3 Ankle;

            /// <summary>Toes down from flat, degrees (the heel up when positive).</summary>
            public float Pitch;

            /// <summary>Which way the toes point along the ground.</summary>
            public Vector3 Toes;

            /// <summary>True while it is in the air.</summary>
            public bool Stepping;

            /// <summary>The ball of the foot, where it is planted.</summary>
            public Vector3 Ball;
        }

        private readonly Vector3[] _ball = new Vector3[2];
        private readonly bool[] _stepping = new bool[2];
        private readonly Vector3[] _from = new Vector3[2];
        private readonly Vector3[] _to = new Vector3[2];
        private readonly float[] _fromPitch = new float[2];
        private readonly float[] _progress = new float[2];
        private readonly float[] _seconds = new float[2];
        private readonly float[] _pitch = new float[2];
        private readonly Foot[] _foot = new Foot[2];
        private float _weight;
        private bool _started;
        private float _drop;

        /// <summary>0 to 1: how much the legs are this stepper's rather than the clip's.</summary>
        public float Weight => _weight;

        /// <summary>How far the hips are down, metres.</summary>
        public float Drop => _drop;

        /// <summary>Steps begun so far (for tests and captures).</summary>
        public int StepsTaken { get; private set; }

        /// <summary>0 = the left foot, 1 = the right.</summary>
        public Foot Of(int foot) => _foot[foot];

        /// <summary>Forgets the feet (a teleport): the next frame starts from the clip's.</summary>
        public void Reset()
        {
            _weight = 0.0f;
            _started = false;
            _stepping[0] = _stepping[1] = false;
            _drop = 0.0f;
        }

        /// <summary>
        /// One frame. <paramref name="wanted"/> (0 to 1) is how much the legs should be this stepper's: 1 on a stair, walking or standing on it, on the ground.
        /// </summary>
        public void Step(float deltaTime, float wanted, in Body body, GroundAt ground)
        {
            if (deltaTime <= 0.0f)
            {
                return;
            }
            _weight = Mathf.MoveTowards(_weight, Mathf.Clamp01(wanted), deltaTime / FadeSeconds);
            if (_weight <= 0.0f && wanted <= 0.0f)
            {
                _started = false;
                _stepping[0] = _stepping[1] = false;
                _drop = 0.0f;
                return;
            }
            Vector3 toes = Quaternion.Euler(0.0f, body.Yaw, 0.0f) * Vector3.forward;
            if (!_started)
            {
                _started = true;
                // Both known first: where one lands depends on where the other is.
                _ball[0] = body.ClipLeft + toes * body.BallForward;
                _ball[1] = body.ClipRight + toes * body.BallForward;
                _stepping[0] = _stepping[1] = false;
                _foot[0] = new Foot { Ankle = body.ClipLeft, Toes = toes, Ball = _ball[0] };
                _foot[1] = new Foot { Ankle = body.ClipRight, Toes = toes, Ball = _ball[1] };
                StartAt(0, body.ClipLeft, toes, body, ground);
                StartAt(1, body.ClipRight, toes, body, ground);
            }
            bool up = body.Slope > 0.0f;

            for (int i = 0; i < 2; i++)
            {
                if (_stepping[i])
                {
                    _progress[i] += deltaTime / _seconds[i];
                    if (_progress[i] >= 1.0f)
                    {
                        _stepping[i] = false;
                        _ball[i] = _to[i];
                    }
                }
                else if (Flat(_ball[i] - body.Centre).magnitude > LostDistance * body.LegLength)
                {
                    _ball[i] = Landing(i, body, 0.0f, up, ground);
                }
            }

            ChooseStep(body, up, toes, ground);

            float wantedDrop = up ? LeastDrop : LeastDropGoingDown;
            for (int i = 0; i < 2; i++)
            {
                Vector3 hip = i == 0 ? body.LeftHip : body.RightHip;
                _foot[i] = _stepping[i] ? InTheAir(i, toes, up, body) : Planted(i, toes, hip, body, deltaTime);
                // What this foot asks of the hips: planted, or about to be. In the air it can wait.
                if (!_stepping[i] || _progress[i] > 0.75f)
                {
                    Vector3 toFoot = _foot[i].Ankle - hip;
                    float along = Flat(toFoot).magnitude;
                    float reach = body.LegLength * ReachShare;
                    if (along < reach)
                    {
                        wantedDrop = Mathf.Max(wantedDrop, hip.y - _foot[i].Ankle.y - Mathf.Sqrt(reach * reach - along * along));
                    }
                }
            }
            wantedDrop = Mathf.Min(wantedDrop, MostDrop);
            _drop = Mathf.Lerp(_drop, wantedDrop, 1.0f - Mathf.Exp(-deltaTime / (wantedDrop > _drop ? DropSeconds : RiseSeconds)));

            // A foot in the air stays far enough under its hip for the knee not to fold shut. (A planted one stays on its tread, and lifts: ChooseStep.)
            for (int i = 0; i < 2; i++)
            {
                if (!_stepping[i])
                {
                    continue;
                }
                float hipY = (i == 0 ? body.LeftHip.y : body.RightHip.y) - _drop;
                Foot foot = _foot[i];
                foot.Ankle.y = Mathf.Min(foot.Ankle.y, hipY - LeastFoldShare * body.LegLength);
                _foot[i] = foot;
            }
        }

        /// <summary>Where a flat foot's ankle is, its ball on <paramref name="ball"/>.</summary>
        public static Vector3 FlatAnkle(Vector3 ball, Vector3 toes, in Body body)
        {
            return ball - toes * body.BallForward + Vector3.up * body.SoleHeight;
        }

        /// <summary>Where the ankle is with the foot rolled about its ball, the heel up by <paramref name="pitch"/> degrees.</summary>
        public static Vector3 RolledAnkle(Vector3 ball, Vector3 toes, float pitch, in Body body)
        {
            float radians = pitch * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians), cos = Mathf.Cos(radians);
            return ball + toes * (-body.BallForward * cos + body.SoleHeight * sin) + Vector3.up * (body.BallForward * sin + body.SoleHeight * cos);
        }

        private void StartAt(int i, Vector3 clipAnkle, Vector3 toes, in Body body, GroundAt ground)
        {
            Vector3 ball = clipAnkle + toes * body.BallForward;
            ball.y = clipAnkle.y - body.SoleHeight;
            _pitch[i] = 0.0f;
            if (ground(ball, out float height))
            {
                if (ball.y - height > 0.06f)
                {
                    // In the air when it was taken: it comes down from where it is.
                    _stepping[i] = true;
                    _progress[i] = 0.0f;
                    _seconds[i] = WalkStepSeconds;
                    _from[i] = clipAnkle;
                    _fromPitch[i] = 0.0f;
                    _to[i] = Landing(i, body, WalkStepSeconds, body.Slope > 0.0f, ground);
                    StepsTaken++;
                    return;
                }
                ball.y = height;
            }
            _stepping[i] = false;
            _ball[i] = ball;
        }

        /// <summary>The trailing foot sets off when the hips have gone far enough past it, or its leg can reach it no longer, and the other is planted or well through its step.</summary>
        private void ChooseStep(in Body body, bool up, Vector3 toes, GroundAt ground)
        {
            if (body.Speed < StandingSpeed)
            {
                return;
            }
            float liftAt = up ? LiftBehindGoingUp : LiftBehindGoingDown;
            int best = -1;
            float furthest = float.MinValue;
            for (int i = 0; i < 2; i++)
            {
                if (_stepping[i])
                {
                    continue;
                }
                int other = 1 - i;
                float behind = Vector3.Dot(body.Centre - _ball[i], body.Along);
                // (One the leg can no longer reach, or with the knee folding shut over it, goes at once, the other foot still in the air: a run's flight, not a drag.)
                Vector3 hip = (i == 0 ? body.LeftHip : body.RightHip) + Vector3.down * _drop;
                bool folding = hip.y - _foot[i].Ankle.y < LeastFoldShare * body.LegLength;
                bool outOfReach = (_foot[i].Ankle - hip).magnitude > body.LegLength * ReachShare + OutOfReachBy;
                bool mustGo = behind > 0.0f && (folding || outOfReach);
                if (_stepping[other] && _progress[other] < OverlapShare && behind < liftAt + LiftAnywayPast && !mustGo)
                {
                    continue;
                }
                if ((behind >= liftAt || mustGo) && behind > furthest)
                {
                    furthest = behind;
                    best = i;
                }
            }
            if (best < 0)
            {
                return;
            }
            float seconds = Mathf.Lerp(WalkStepSeconds, SprintStepSeconds, Mathf.InverseLerp(WalkSpeed, SprintSpeed, body.Speed));
            _stepping[best] = true;
            _progress[best] = 0.0f;
            _seconds[best] = seconds;
            _from[best] = _foot[best].Ankle;
            _fromPitch[best] = _pitch[best];
            _to[best] = Landing(best, body, seconds, up, ground);
            StepsTaken++;
        }

        /// <summary>Where a step should land: past the hips as they will be when it lands, on its own side, on a tread and past the other foot.</summary>
        private Vector3 Landing(int i, in Body body, float seconds, bool up, GroundAt ground)
        {
            Vector3 right = Quaternion.Euler(0.0f, body.Yaw, 0.0f) * Vector3.right;
            float side = i == 0 ? -1.0f : 1.0f;
            float ahead = (up ? LandAheadGoingUp : LandAheadGoingDown) + body.Speed * seconds;
            Vector3 at = body.Centre + body.Along * ahead + right * (side * body.HalfWidth);
            int other = 1 - i;
            float past = Vector3.Dot(at - (_stepping[other] ? _to[other] : _ball[other]), body.Along);
            if (past < LeastStepPast)
            {
                at += body.Along * (LeastStepPast - past);
            }
            // Read near where the stair will be there: well ahead at a run it is a metre above or below the hips' floor.
            at.y += body.Slope * Vector3.Dot(at - body.Centre, body.Along);

            // Fitted to a tread the nearer way, back toward the body or on: going down the heel needs nearly the whole tread behind the ball, so a fit can be most of a
            // tread away either way, and moved back it landed behind the other foot, which then stepped twice running.
            Vector3 back = OnATread(at, -body.Along, body, ground);
            Vector3 on = OnATread(at, body.Along, body, ground);
            bool backFits = Vector3.Dot(back - (_stepping[other] ? _to[other] : _ball[other]), body.Along) >= LeastStepPast;
            return backFits && Vector3.Distance(back, at) <= Vector3.Distance(on, at) ? back : on;
        }

        /// <summary>
        /// The ball of the foot on a tread, with the foot's uphill end clear of the riser behind it. A foot is about as long as a tread: going up that end is the toes, going down the
        /// heel, and the other end may hang over the edge. Moved along <paramref name="shift"/> a little at a time until it fits, which may be onto the next tread.
        /// </summary>
        private static Vector3 OnATread(Vector3 at, Vector3 shift, in Body body, GroundAt ground)
        {
            Vector3 toes = Quaternion.Euler(0.0f, body.Yaw, 0.0f) * Vector3.forward;
            Vector3 uphill = body.Slope > 0.0f ? body.Along : -body.Along;
            Vector3 uphillEnd = Vector3.Dot(toes, uphill) > 0.0f ? toes * body.ToeTip : -toes * body.HeelBack;
            for (int i = 0; i < TreadFitTries; i++)
            {
                if (!ground(at, out float here))
                {
                    return at;
                }
                at.y = here;
                if (!ground(at + uphillEnd, out float end) || end - here <= TreadFitWithin)
                {
                    return at;
                }
                at += shift * TreadFitStep;
            }
            return at;
        }

        /// <summary>A planted foot never moves: it rolls up onto its ball as the hips leave it, the further behind them and the less the leg can reach it, the more.</summary>
        private Foot Planted(int i, Vector3 toes, Vector3 hip, in Body body, float deltaTime)
        {
            float behind = Vector3.Dot(body.Centre - _ball[i], body.Along);
            float roll = Mathf.Clamp01(behind / FullRollBehind) * MostRollDegrees;
            if (behind < 0.0f && body.Slope < 0.0f)
            {
                // Going down the foot ahead stands on its toes, the leg nearly straight, and lowers its heel as the hips come over it.
                roll = Mathf.Clamp01(-behind / ToesFirstAhead) * LandingPitchGoingDown;
            }
            float reach = body.LegLength * ReachShare;
            Vector3 lowHip = hip + Vector3.down * _drop;
            while (roll < MostRollDegrees && (RolledAnkle(_ball[i], toes, roll, body) - lowHip).magnitude > reach)
            {
                roll += 5.0f;
            }
            _pitch[i] = Mathf.Lerp(_pitch[i], roll, 1.0f - Mathf.Exp(-deltaTime / RollSeconds));
            return new Foot { Ankle = RolledAnkle(_ball[i], toes, _pitch[i], body), Pitch = _pitch[i], Toes = toes, Stepping = false, Ball = _ball[i] };
        }

        /// <summary>A foot in the air: along the ground in an ease, and in height ahead of that going up, behind it going down; an arc over the straight line.</summary>
        private Foot InTheAir(int i, Vector3 toes, bool up, in Body body)
        {
            float p = Mathf.Clamp01(_progress[i]);
            float s = Ease(p);
            Vector3 to = FlatAnkle(_to[i], toes, body);
            float heightShare = up ? Mathf.Clamp01(p / HeightShare) : Mathf.Clamp01((p - (1.0f - HeightShare)) / HeightShare);
            Vector3 at = Vector3.Lerp(_from[i], to, s);
            at.y = Mathf.Lerp(_from[i].y, to.y, Ease(heightShare)) + 4.0f * p * (1.0f - p) * ArcHeight;
            float pitch = Mathf.Lerp(_fromPitch[i], up ? LandingPitchGoingUp : LandingPitchGoingDown, Ease(Mathf.Clamp01(p * 1.4f)));
            _pitch[i] = pitch;
            return new Foot { Ankle = at, Pitch = pitch, Toes = toes, Stepping = true, Ball = _to[i] };
        }

        private static float Ease(float t) => t * t * (3.0f - 2.0f * t);

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);
    }
}
