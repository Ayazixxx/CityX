using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE FEET IN A SWORD FIGHT (owner, 2026-10-16: "make sure his feet dont lock up during combat animations while moving"). Swinging on the
    /// move used to leave the legs to the walk clip slowed to 40%: a blend of the idle and the walk that barely lifts its feet, so they slid
    /// metres along the ground and took two real steps in three seconds (SwordWalkSwingCapture measured it), and every lunge dragged them.
    ///
    /// Here the feet are planted and step, the way a fighter's do (the research: feet latched where they land, a step only once a foot has
    /// been left too far from where it should be, the step's lift and length scaled to the move): each foot has a place in a STANCE around the
    /// body (shoulder width, one foot forward by the frame's <see cref="SwordFrame.StanceLead"/> and <see cref="SwordFrame.StanceDepth"/>, the
    /// back foot turned out), a PLANTED foot never moves, and when one has been left more than <see cref="StepTrigger"/> leg lengths from its
    /// place (the body walked on, lunged into a cut, or turned toward the next one) it takes a quick STEP there, landing a little ahead of
    /// where its place is going so the next is not due at once. One foot steps at a time (the other may set off once a step is well under
    /// way), the feet never cross, and a step lifts the foot in an arc.
    ///
    /// PURE: no scene, no bones. PlayerLocomotion (the foot IK) hands in where the body is, its stance and where the clip has the feet, and
    /// puts the ankles where this says: so a copy of another player's body, moved by the network, steps the same way.
    /// </summary>
    public sealed class SwordFootwork
    {
        /// <summary>Each foot's place is this far out from the body's centre line, in leg lengths (a little wider than the hips).</summary>
        public const float HalfWidth = 0.27f;

        /// <summary>With the reach known: a foot's place is kept within this share of it from its hip...</summary>
        public const float PlaceReachShare = 0.7f;

        /// <summary>...a step lands within this share...</summary>
        public const float LandReachShare = 0.85f;

        /// <summary>...and a planted foot further than this share from its hip must step (the leg is nearly straight reaching it).</summary>
        public const float MustStepReachShare = 0.95f;

        /// <summary>A planted foot this far from its place (leg lengths, along the ground) steps...</summary>
        public const float StepTrigger = 0.18f;

        /// <summary>...or, on the move, as far as the body goes in this long (seconds), so a walking fighter takes longer steps rather than more of them...</summary>
        public const float StepTriggerPerSpeed = 0.12f;

        /// <summary>...but never further than this (leg lengths): past it the leg could not reach the foot from the hip.</summary>
        public const float MaxStepTrigger = 0.40f;

        /// <summary>A step lands at most this far (leg lengths) ahead of its place, however fast the body goes: still under the hip's reach.</summary>
        public const float MaxLandAhead = 0.45f;

        /// <summary>...or turned this far from its place's heading, degrees.</summary>
        public const float StepTriggerDegrees = 40.0f;

        /// <summary>A step takes this long, seconds.</summary>
        public const float StepSeconds = 0.17f;

        /// <summary>How high the foot lifts at the middle of a full step, leg lengths.</summary>
        public const float LiftShare = 0.13f;

        /// <summary>A step this short (leg lengths) lifts in proportion; longer ones lift the full height.</summary>
        public const float FullLiftDistance = 0.35f;

        /// <summary>A step lands this share of its own travel time AHEAD of where its place will be, so a moving body is not due another at once.</summary>
        public const float LandAhead = 0.5f;

        /// <summary>The other foot may set off once a step is this far through (a run of quick steps); before that one foot steps at a time.</summary>
        public const float OverlapShare = 0.6f;

        /// <summary>The feet's places are taken and given back over this long, seconds.</summary>
        public const float FadeSeconds = 0.25f;   // 0.15 until 2026-10-17 (owner: "make it flow between the animations and the attack animations smoothly")

        /// <summary>The front foot's toes turn out this much, the back foot's this much (a fighting stance), degrees.</summary>
        public const float FrontToeOutDegrees = 10.0f;

        public const float BackToeOutDegrees = 35.0f;

        /// <summary>Each foot stays at least this far on its own side of the body's centre line (leg lengths): they never cross.</summary>
        public const float MinSideGap = 0.12f;

        /// <summary>A foot this far from its place (leg lengths: a teleport, a shove) is put there at once rather than stepped.</summary>
        public const float LostDistance = 1.6f;

        /// <summary>
        /// Steps keep up with the body turning this fast, degrees per second; faster (the swing's quick turn to the camera's aim) the planted feet
        /// PIVOT with the body for the rest, swivelling where they stand, rather than staying put while the hips spin round over them.
        /// </summary>
        public const float SteppedTurnDegreesPerSecond = 240.0f;

        /// <summary>The body this frame and the stance it is in.</summary>
        public struct Body
        {
            /// <summary>The body's own point on the ground (the controller's bottom), metres.</summary>
            public Vector3 Ground;

            /// <summary>The way the body faces, degrees about the vertical.</summary>
            public float Yaw;

            /// <summary>How the body is moving (measured, so a network copy's is the same), metres per second.</summary>
            public Vector3 Velocity;

            /// <summary>Hip to ankle, metres.</summary>
            public float LegLength;

            /// <summary>The stance: which foot is forward (-1 left, 1 right) and how far apart front to back (leg lengths).</summary>
            public float Lead;

            public float Depth;

            /// <summary>
            /// The hip joints, and how far out from under its hip a foot can stand on the ground with the hips as low as they are (metres, along
            /// the ground; 0: not known, no limit). A short-legged body standing tall reaches very little: the places, the landings and the
            /// planted feet are all kept inside it, or the leg IK could not get the foot there and would drag it along instead.
            /// </summary>
            public Vector3 LeftHip;

            public Vector3 RightHip;

            public float Reach;

            /// <summary>Where the walk clip has the ankles, and which way its feet point (degrees): where the feet start from when the footwork takes them.</summary>
            public Vector3 ClipLeft;

            public Vector3 ClipRight;

            public float ClipLeftYaw;

            public float ClipRightYaw;
        }

        /// <summary>One foot's answer.</summary>
        public struct Foot
        {
            /// <summary>Where the ankle goes along the ground (at the body's ground height: the foot IK finds the real ground under it).</summary>
            public Vector3 Position;

            /// <summary>How far the foot is lifted off the ground, metres (0 when planted).</summary>
            public float Lift;

            /// <summary>Which way the toes point, degrees about the vertical.</summary>
            public float Yaw;

            /// <summary>True while it is in the air, stepping.</summary>
            public bool Stepping;
        }

        private readonly Vector3[] _anchor = new Vector3[2];
        private readonly float[] _yaw = new float[2];
        private readonly bool[] _stepping = new bool[2];
        private readonly Vector3[] _from = new Vector3[2];
        private readonly Vector3[] _to = new Vector3[2];
        private readonly float[] _fromYaw = new float[2];
        private readonly float[] _toYaw = new float[2];
        private readonly float[] _progress = new float[2];
        private readonly Foot[] _foot = new Foot[2];
        private float _weight;
        private bool _started;
        private float _lastYaw;

        /// <summary>0 to 1: how much the feet are this stepper's rather than the clip's.</summary>
        public float Weight => _weight;

        /// <summary>Steps begun so far (for tests and captures).</summary>
        public int StepsTaken { get; private set; }

        /// <summary>0 = the left foot, 1 = the right.</summary>
        public Foot Of(int foot) => _foot[foot];

        public bool IsStepping(int foot) => _stepping[foot];

        /// <summary>Forgets the feet (a teleport): the next frame starts from the clip's.</summary>
        public void Reset()
        {
            _weight = 0.0f;
            _started = false;
            _stepping[0] = _stepping[1] = false;
        }

        /// <summary>Where a foot belongs in the stance (along the ground), and which way its toes point.</summary>
        public static Vector3 Place(int foot, in Body body, out float yaw)
        {
            float side = foot == 0 ? -1.0f : 1.0f;
            float lead = Mathf.Clamp(body.Lead, -1.0f, 1.0f);
            // lead -1: the left foot half the depth forward and the right half back; lead 1 the other way round.
            float forward = side * lead * Mathf.Max(0.0f, body.Depth) * 0.5f;
            Quaternion facing = Quaternion.Euler(0.0f, body.Yaw, 0.0f);
            Vector3 place = body.Ground + facing * (new Vector3(side * HalfWidth, 0.0f, forward) * body.LegLength);
            float front = Mathf.Clamp01((side * lead + 1.0f) * 0.5f);   // 1 for the leading foot
            yaw = body.Yaw + side * Mathf.Lerp(BackToeOutDegrees, FrontToeOutDegrees, front);
            return place;
        }

        /// <summary>
        /// One frame. <paramref name="wanted"/> (0 to 1) is how much the feet should be this stepper's: the sword frame's footwork, 0 in the air.
        /// </summary>
        public void Step(float deltaTime, float wanted, in Body body)
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
                return;
            }
            float leg = Mathf.Max(body.LegLength, 0.1f);
            if (!_started)
            {
                // Taken from wherever the clip has the feet, so nothing jumps; the stance is stepped into from there.
                _started = true;
                _lastYaw = body.Yaw;
                StartAt(0, body.ClipLeft, body.ClipLeftYaw, body.Ground.y);
                StartAt(1, body.ClipRight, body.ClipRightYaw, body.Ground.y);
            }

            // A turn faster than steps can follow: the feet pivot with the body for the part beyond it (on the balls of the feet).
            float turned = Mathf.DeltaAngle(_lastYaw, body.Yaw);
            _lastYaw = body.Yaw;
            float steppable = SteppedTurnDegreesPerSecond * deltaTime;
            if (Mathf.Abs(turned) > steppable)
            {
                float swivel = turned - Mathf.Sign(turned) * steppable;
                Quaternion pivot = Quaternion.Euler(0.0f, swivel, 0.0f);
                for (int i = 0; i < 2; i++)
                {
                    if (_stepping[i])
                    {
                        _from[i] = body.Ground + pivot * (_from[i] - body.Ground);
                        _fromYaw[i] += swivel;
                    }
                    else
                    {
                        _anchor[i] = body.Ground + pivot * (_anchor[i] - body.Ground);
                        _yaw[i] += swivel;
                    }
                }
            }

            var flatVelocity = new Vector3(body.Velocity.x, 0.0f, body.Velocity.z);
            for (int i = 0; i < 2; i++)
            {
                Vector3 place = PlaceInReach(i, body, out float placeYaw);
                if (_stepping[i])
                {
                    _progress[i] += deltaTime / StepSeconds;
                    // Steered while it is in the air: where its place will be when it lands (a turning, lunging body moves it).
                    if (_progress[i] < 0.75f)
                    {
                        _to[i] = Landing(i, place, flatVelocity, (1.0f - _progress[i]) * StepSeconds, body);
                        _toYaw[i] = placeYaw;
                    }
                    if (_progress[i] >= 1.0f)
                    {
                        _stepping[i] = false;
                        _anchor[i] = _to[i];
                        _yaw[i] = _toYaw[i];
                    }
                }
                else
                {
                    _anchor[i].y = body.Ground.y;
                    if (Flat(_anchor[i] - place).magnitude > LostDistance * leg)
                    {
                        _anchor[i] = place;   // knocked or carried far away: put it back rather than take a giant stride
                        _yaw[i] = placeYaw;
                    }
                }
            }

            // Which foot steps: the planted one furthest from its place, once past the trigger, and only while the other is planted or well into
            // its own step.
            float trigger = Mathf.Min(Mathf.Max(StepTrigger * leg, flatVelocity.magnitude * StepTriggerPerSpeed), MaxStepTrigger * leg);
            int best = -1;
            float bestNeed = 1.0f;
            for (int i = 0; i < 2; i++)
            {
                if (_stepping[i])
                {
                    continue;
                }
                int other = 1 - i;
                // (A foot at the very end of its leg's reach goes at once, even with the other still in the air: a run's flight, not a drag.)
                bool atReachEnd = body.Reach > 0.0f && Flat(_anchor[i] - Hip(i, body)).magnitude > body.Reach;
                if (_stepping[other] && _progress[other] < OverlapShare && !atReachEnd)
                {
                    continue;
                }
                Vector3 place = PlaceInReach(i, body, out float placeYaw);
                float need = Mathf.Max(Flat(_anchor[i] - place).magnitude / trigger,
                    Mathf.Abs(Mathf.DeltaAngle(_yaw[i], placeYaw)) / StepTriggerDegrees);
                if (body.Reach > 0.0f)
                {
                    // Nearly out of the leg's reach: it must step now, whatever the trigger says (a foot the leg cannot get to is dragged).
                    float fromHip = Flat(_anchor[i] - Hip(i, body)).magnitude / (MustStepReachShare * body.Reach);
                    need = Mathf.Max(need, fromHip);
                }
                if (need >= bestNeed)
                {
                    bestNeed = need;
                    best = i;
                }
            }
            if (best >= 0)
            {
                Vector3 place = PlaceInReach(best, body, out float placeYaw);
                _stepping[best] = true;
                _progress[best] = 0.0f;
                _from[best] = _anchor[best];
                _fromYaw[best] = _yaw[best];
                _to[best] = Landing(best, place, flatVelocity, StepSeconds, body);
                _toYaw[best] = placeYaw;
                StepsTaken++;
            }

            for (int i = 0; i < 2; i++)
            {
                if (_stepping[i])
                {
                    float p = Mathf.Clamp01(_progress[i]);
                    float s = p * p * (3.0f - 2.0f * p);
                    float length = Flat(_to[i] - _from[i]).magnitude;
                    float lift = 4.0f * p * (1.0f - p) * LiftShare * leg * Mathf.Clamp01(length / (FullLiftDistance * leg));
                    Vector3 at = Vector3.Lerp(_from[i], _to[i], s);
                    at.y = body.Ground.y;
                    _foot[i] = new Foot { Position = at, Lift = lift, Yaw = Mathf.LerpAngle(_fromYaw[i], _toYaw[i], s), Stepping = true };
                }
                else
                {
                    _foot[i] = new Foot { Position = _anchor[i], Lift = 0.0f, Yaw = _yaw[i], Stepping = false };
                }
            }
        }

        private void StartAt(int i, Vector3 clipAnkle, float clipYaw, float groundY)
        {
            _anchor[i] = new Vector3(clipAnkle.x, groundY, clipAnkle.z);
            _yaw[i] = clipYaw;
            _stepping[i] = false;
            _progress[i] = 0.0f;
        }

        /// <summary>Where a step should land: its place as it will be in <paramref name="seconds"/>, a little further on, kept on its own side of the body.</summary>
        private Vector3 Landing(int i, Vector3 place, Vector3 velocity, float seconds, in Body body)
        {
            float leg = Mathf.Max(body.LegLength, 0.1f);
            // Where the place will be when the foot lands, plus the lead: to where the place has gone, the lead capped so the leg can still reach.
            Vector3 travel = velocity * seconds;
            Vector3 ahead = Vector3.ClampMagnitude(velocity * (StepSeconds * LandAhead), MaxLandAhead * leg);
            Vector3 to = place + travel + ahead;
            // Never across the centre line: in the body's own frame, the left foot stays left and the right foot right.
            Quaternion facing = Quaternion.Euler(0.0f, body.Yaw, 0.0f);
            Vector3 local = Quaternion.Inverse(facing) * (to - body.Ground);
            float side = i == 0 ? -1.0f : 1.0f;
            float least = MinSideGap * leg;
            if (side * local.x < least)
            {
                local.x = side * least;
            }
            to = body.Ground + facing * local;
            to.y = body.Ground.y;
            if (body.Reach > 0.0f)
            {
                // Where the leg can reach from where its hip will be when the foot lands.
                to = WithinReach(to, Hip(i, body) + travel, LandReachShare * body.Reach, body.Ground.y);
            }
            return to;
        }

        /// <summary>A foot's place in the stance, brought in under its hip when the stance is wider than the leg can reach from there.</summary>
        private static Vector3 PlaceInReach(int i, in Body body, out float yaw)
        {
            Vector3 place = Place(i, body, out yaw);
            return body.Reach > 0.0f ? WithinReach(place, Hip(i, body), PlaceReachShare * body.Reach, body.Ground.y) : place;
        }

        private static Vector3 Hip(int i, in Body body) => i == 0 ? body.LeftHip : body.RightHip;

        /// <summary><paramref name="point"/> moved in along the ground toward <paramref name="hip"/> until it is within <paramref name="reach"/> of it.</summary>
        private static Vector3 WithinReach(Vector3 point, Vector3 hip, float reach, float groundY)
        {
            Vector3 offset = Flat(point - hip);
            if (offset.magnitude > reach)
            {
                offset = offset.normalized * reach;
            }
            return new Vector3(hip.x + offset.x, groundY, hip.z + offset.z);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);
    }
}
