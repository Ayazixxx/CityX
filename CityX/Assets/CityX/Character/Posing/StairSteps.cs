using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE FEET ON A STAIR (owner, 2026-11-03, of the walk clip's legs bent onto the steps: "this looks all fucked up ... look up how to do good stairs animations with IK";
    /// docs/STAIRS.md). The recorded stair clips step 0.25 m on 0.90 m legs and our stairs want 0.36 m on 0.77 m, so no clip's feet land on our treads. So the ankles go where
    /// this says, with the recording's timing and foot path (StairGait): each step is planned onto a real tread, the ball of the foot on it and the heel over the edge; the
    /// TRAILING foot goes next, once the body has gone far enough along; a planted foot never moves; and the hips come down only as far as the feet need. The clips give the rest of
    /// the body, in time with these steps (<see cref="Phase"/>).
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

        /// <summary>Slower than this the body is standing: a planted foot stays planted.</summary>
        public const float StandingSpeed = 0.3f;

        /// <summary>A planted ankle this far past what its leg can reach lifts at once, metres: held, it has left its tread.</summary>
        public const float OutOfReachBy = 0.01f;

        /// <summary>A step lands at least this far past the foot left behind: never on the same tread.</summary>
        public const float LeastStepPast = 0.25f;

        /// <summary>The other foot may set off once a step is this far through...</summary>
        public const float OverlapShare = 0.5f;

        /// <summary>...or whatever the step, once it is this much further behind than it should have lifted, metres.</summary>
        public const float LiftAnywayPast = 0.1f;

        /// <summary>A step lands an even step past the other foot if that is within this of where the hips want it, metres.</summary>
        public const float EvenStepWithin = 0.2f;

        /// <summary>A step with no rise in it (off the end of a flight) still lifts the foot this far, metres: the recorded path's height is a share of the rise.</summary>
        public const float FlatStepArc = 0.1f;

        /// <summary>A rise of this many metres or more clears its edges by itself.</summary>
        public const float RiseNeedsNoArc = 0.3f;

        /// <summary>The most a planted foot rolls up onto its ball, degrees, and how far behind the hips it has fully (metres): a foot behind the body is on its toes.</summary>
        public const float MostRollDegrees = 50.0f;

        public const float FullRollBehind = 0.45f;

        /// <summary>Going down, a planted foot this far ahead of the hips is on its toes by that much (metres).</summary>
        public const float ToesFirstAhead = 0.25f;

        /// <summary>How quickly a foot rolls, seconds.</summary>
        private const float RollSeconds = 0.04f;

        /// <summary>The clips' phase follows the steps: a late or early step's difference is taken up over this long, seconds.</summary>
        private const float PhaseCatchUpSeconds = 0.12f;

        /// <summary>The hips are always this low on a stair, metres, and at most this: the legs are only 0.77 m. Going up the recording's leg all but straightens under the hips.</summary>
        public const float LeastDrop = 0.02f;

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
        private float _phase;
        private float _phaseBehind;
        private bool _backward;

        /// <summary>0 to 1: how much the legs are this stepper's rather than the clip's.</summary>
        public float Weight => _weight;

        /// <summary>Where the two-step cycle is, 0 to 1: 0 as the left foot leaves its tread, 0.5 as the right does. The stair clips play to this.</summary>
        public float Phase => _phase;

        /// <summary>True while the body faces up the stair, whichever way it is going: backing down it is an up clip played in reverse.</summary>
        public bool FacesUphill { get; private set; }

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
            _backward = Vector3.Dot(body.Along, toes) < 0.0f;
            FacesUphill = up != _backward;
            StepPhase(body, deltaTime);

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

        /// <summary>The cycle goes on with the body's travel, backward when it backs along the stair, and catches up with the feet when a step starts.</summary>
        private void StepPhase(in Body body, float deltaTime)
        {
            if (body.Speed < StandingSpeed)
            {
                return;
            }
            float caughtUp = _phaseBehind * (1.0f - Mathf.Exp(-deltaTime / PhaseCatchUpSeconds));
            _phaseBehind -= caughtUp;
            float rate = body.Speed / StairGait.StrideMetres;
            _phase = Mathf.Repeat(_phase + (_backward ? -rate : rate) * deltaTime + caughtUp, 1.0f);
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
                    bool up = body.Slope > 0.0f;
                    float seconds = StairGait.SwingSeconds(up, body.Speed);
                    _stepping[i] = true;
                    _progress[i] = 0.0f;
                    _seconds[i] = seconds;
                    _from[i] = clipAnkle;
                    _fromPitch[i] = 0.0f;
                    _to[i] = Landing(i, body, seconds, up, ground);
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
            float liftAt = StairGait.LiftBehind(up, body.Speed);
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
            float seconds = StairGait.SwingSeconds(up, body.Speed);
            _stepping[best] = true;
            _progress[best] = 0.0f;
            _seconds[best] = seconds;
            _from[best] = _foot[best].Ankle;
            _fromPitch[best] = _pitch[best];
            _to[best] = Landing(best, body, seconds, up, ground);
            StepsTaken++;

            // Forward a foot leaving is where the clip lifts it. In reverse, where the clip would have set it down.
            float clipAt = (best == 0 ? 0.0f : 0.5f) + (_backward ? StairGait.SwingShare(up, body.Speed) : 0.0f);
            _phaseBehind = Mathf.DeltaAngle(_phase * 360.0f, clipAt * 360.0f) / 360.0f;
        }

        /// <summary>Where a step should land: past the hips as they will be when it lands, on its own side, on a tread and past the other foot.</summary>
        private Vector3 Landing(int i, in Body body, float seconds, bool up, GroundAt ground)
        {
            Vector3 right = Quaternion.Euler(0.0f, body.Yaw, 0.0f) * Vector3.right;
            float side = i == 0 ? -1.0f : 1.0f;
            float ahead = StairGait.LandAhead(up, body.Speed) + body.Speed * seconds;
            Vector3 at = body.Centre + body.Along * ahead + right * (side * body.HalfWidth);
            int other = 1 - i;
            Vector3 otherFoot = _stepping[other] ? _to[other] : _ball[other];
            // AN EVEN RHYTHM: one step past the other foot, as long as that is about where the hips want it. Left to the hips alone, each foot keeps its own stride, and two that
            // started close together go on short step, long step, a limp.
            float evenBy = StairGait.StrideMetres * 0.5f - Vector3.Dot(at - otherFoot, body.Along);
            if (Mathf.Abs(evenBy) <= EvenStepWithin)
            {
                at += body.Along * evenBy;
            }
            float past = Vector3.Dot(at - otherFoot, body.Along);
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
                roll = Mathf.Clamp01(-behind / ToesFirstAhead) * StairGait.LandingPitchGoingDown;
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

        /// <summary>A foot in the air, on the recording's path (StairGait) from where it left to where it lands, its heel as the recording has it.</summary>
        private Foot InTheAir(int i, Vector3 toes, bool up, in Body body)
        {
            float p = Mathf.Clamp01(_progress[i]);
            StairGait.Swing(up, p, out float along, out float height, out float heel);
            Vector3 to = RolledAnkle(_to[i], toes, StairGait.LandingPitch(up), body);
            Vector3 at = Vector3.Lerp(_from[i], to, along);
            float rise = Mathf.Abs(to.y - _from[i].y);
            at.y = Mathf.LerpUnclamped(_from[i].y, to.y, height) + 4.0f * p * (1.0f - p) * FlatStepArc * (1.0f - Mathf.Clamp01(rise / RiseNeedsNoArc));
            float pitch = Mathf.Lerp(_fromPitch[i], heel, Ease(Mathf.Clamp01(p * 3.0f)));
            _pitch[i] = pitch;
            return new Foot { Ankle = at, Pitch = pitch, Toes = toes, Stepping = true, Ball = _to[i] };
        }

        private static float Ease(float t) => t * t * (3.0f - 2.0f * t);

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);
    }
}
