using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// FOOT PLACEMENT, the bookkeeping behind the leg IK (owner, 2026-10-13: "foot IK with ground detection, hip drop, and stride adjustment so the
    /// character walks/runs smoothly on slopes and stairs without sliding feet"). Research (foot-placement IK write-ups, Opsive's controller, a parkour
    /// developer's stairs notes): every frame, find the ground under each foot; LOWER THE HIPS so the leg that has to reach furthest down can; solve each
    /// leg so its sole sits on its own ground; tilt the sole to the surface; and while a foot is PLANTED, hold it where it first touched (a lock) so it
    /// cannot slide, letting it go smoothly when the clip lifts it or the body outruns it.
    ///
    /// A foot can only be HELD when the clip moves its feet through the body about as fast as the body moves (<see cref="LocksViable"/>): the clips and
    /// the body's speeds are matched as far as the research allows (ClipCadence: play rate and stride within 20%, the jog at walking speed) and the
    /// locks take up the small remainder. Where a clip is still far slower than the body (a strafe at 4 m/s), holding a foot would drag it to the
    /// stretch limit every few frames, so then the feet are placed on the ground but not held.
    ///
    /// PURE: no scene, no bones. <see cref="PlayerLocomotion"/> samples the clip's ankle positions and the ground under them, hands them here, and turns
    /// the answers into two-bone IK after the Animator. Everything is in metres and seconds.
    /// </summary>
    public sealed class FootPlanting
    {
        /// <summary>What the clip and the ground say about one foot this frame.</summary>
        public struct Sample
        {
            /// <summary>The ankle (the foot bone) where the clip put it, in the world, BEFORE any hip drop (after any stride warping).</summary>
            public Vector3 Animated;

            /// <summary>Whether ground was found under the foot (within reach: a cliff edge has none).</summary>
            public bool HasGround;

            /// <summary>The ground under the foot (under where it is being held, when it is locked).</summary>
            public Vector3 Ground;

            /// <summary>The ground's normal there.</summary>
            public Vector3 Normal;

            /// <summary>
            /// How far the foot's LOWEST point (the toe when the foot points down, the sole when flat) is below the ankle this frame; 0 means
            /// "use the flat sole offset". A running foot strikes toe first: its ankle is 7 cm higher than a flat foot's while its toe is on the ground.
            /// </summary>
            public float SoleBelowAnkle;

            /// <summary>The hip joint, in the world, and the leg's length (hip to knee to ankle): for dipping the hips when a stride reaches far. 0 length: not known.</summary>
            public Vector3 Hip;

            public float LegLength;
        }

        /// <summary>Where one foot should go this frame.</summary>
        public struct Placement
        {
            /// <summary>The ankle's target, in the world (after the hip drop is applied to the body).</summary>
            public Vector3 Target;

            /// <summary>The ground normal to lay the sole on.</summary>
            public Vector3 Normal;

            /// <summary>How much of the IK to apply, 0 (the clip as is) to 1.</summary>
            public float Weight;

            /// <summary>True while the foot is held where it landed.</summary>
            public bool Locked;
        }

        /// <summary>
        /// A foot whose clip lift above the CLIP'S OWN FLOOR is under this is planted. The floor is the lowest either foot has been lately
        /// (<see cref="FloorLift"/>): the walk puts a planted ankle exactly one sole up, the run never brings its feet quite that low.
        /// </summary>
        public const float PlantBelow = 0.035f;

        /// <summary>A planted foot lifted past this is in its swing and is let go (hysteresis over <see cref="PlantBelow"/>).</summary>
        public const float UnplantAbove = 0.07f;

        /// <summary>How fast the remembered floor rises back toward the feet when they stop coming so low (a walk becoming a run), metres per second.</summary>
        public const float FloorLiftRiseSpeed = 0.15f;

        /// <summary>A planted foot pulled further than this from where the clip has it is let go: the body has outrun the clip (a sprint's last 25% lives here).</summary>
        public const float MaxLockStretch = 0.35f;

        /// <summary>
        /// A planted foot the clip has moved this far AHEAD of its lock, the way the body is going, is in its swing and is let go whatever its height:
        /// a shuffling walk (a sword guard's) never lifts its feet past <see cref="UnplantAbove"/>, and a lock that waited for that dragged the foot.
        /// </summary>
        public const float SwingReleaseMetres = 0.08f;

        /// <summary>A let-go lock blends back to the clip over this long.</summary>
        public const float LockFadeSeconds = 0.1f;

        /// <summary>The IK's own weight fades in and out over this long (stepping off an edge, landing).</summary>
        public const float WeightFadeSeconds = 0.12f;

        /// <summary>The hips never drop further than this to reach a low foot (a kerb, a stair, the downhill side of a slope, a long stride).</summary>
        public const float MaxHipDrop = 0.5f;

        /// <summary>The ground under a foot may be at most this much ABOVE the floor to be stood on (higher is a wall, not a step).</summary>
        public const float MaxFootRaise = 0.45f;

        /// <summary>How quickly the hips follow a drop (fast, so a foot does not hover over a step) and come back up.</summary>
        public const float HipDropDownSeconds = 0.04f;

        public const float HipDropUpSeconds = 0.12f;

        /// <summary>The sole is held a hair above the surface, so it never z-fights with it.</summary>
        public const float SoleLift = 0.004f;

        /// <summary>A landing dips the hips by this much per metre per second of impact speed (the knees take the fall), up to <see cref="MaxLandingDip"/>.</summary>
        public const float LandingDipPerMetrePerSecond = 0.04f;

        public const float MaxLandingDip = 0.3f;

        /// <summary>Slower landings than this are steps, not falls: no dip.</summary>
        public const float LandingMinSpeed = 3.0f;

        /// <summary>The landing dip's spring (critically damped: it settles in about a quarter of a second without bouncing).</summary>
        public const float LandingStiffness = 400.0f;

        /// <summary>A foot is held only while the clip's planted feet move through the body between these fractions of the body's own speed.</summary>
        public const float LockPaceMin = 0.7f;

        public const float LockPaceMax = 1.4f;

        /// <summary>Below this body speed the pace does not matter (standing: the feet are simply held).</summary>
        public const float LockPaceAboveSpeed = 0.5f;

        /// <summary>How long the measured paces are averaged over.</summary>
        public const float PaceSmoothingSeconds = 0.25f;

        /// <summary>A leg reaches at most this much of its length (the solver's own limit); past it the hips dip so a long stride can land.</summary>
        public const float ReachShare = 0.995f;

        /// <summary>The hips dip at most this much for a stride (a run's heel strike is already at the leg's limit; a deeper dip every step is a bob).</summary>
        public const float MaxReachDip = 0.05f;

        private readonly bool[] _locked = new bool[2];
        private readonly Vector3[] _lockPoint = new Vector3[2];
        private readonly float[] _lockWeight = new float[2];
        private readonly float[] _weight = new float[2];
        private readonly Placement[] _placement = new Placement[2];
        private readonly float[] _lastLift = new float[2];
        private readonly Vector3[] _lastAnimated = new Vector3[2];
        private int _animatedFrames;
        private float _hipDrop;
        private float _floorLift;
        private bool _hasFloorLift;
        private float _dip;
        private float _dipVelocity;
        private float _clipPace;
        private float _bodyPace;

        /// <summary>How far the hips are lowered for the feet to reach their ground, metres (0 on flat ground).</summary>
        public float HipDrop => _hipDrop;

        /// <summary>The extra dip from a landing, metres, decaying.</summary>
        public float LandingDip => _dip;

        /// <summary>Everything the body is lowered by this frame.</summary>
        public float TotalDrop => _hipDrop + _dip;

        /// <summary>0 = the left foot, 1 = the right.</summary>
        public Placement Of(int foot) => _placement[foot];

        public bool IsLocked(int foot) => _locked[foot];

        /// <summary>The clip's lift of a foot above its floor in the last step (for diagnosing why a foot does or does not plant).</summary>
        public float LastLift(int foot) => _lastLift[foot];

        /// <summary>Where the clip had the ankle in the last step, before any placement (for measuring the clip against the body's speed).</summary>
        public Vector3 LastAnimated(int foot) => _lastAnimated[foot];

        /// <summary>The lowest lift either foot has had lately: where this clip's feet are when they are on the ground.</summary>
        public float FloorLift => _hasFloorLift ? _floorLift : 0.0f;

        /// <summary>True while the clip has a foot on its own floor (within <see cref="PlantBelow"/> of <see cref="FloorLift"/>).</summary>
        public bool IsOnFloor(int foot) => _lastLift[foot] - FloorLift < PlantBelow;

        /// <summary>How fast the clip's planted feet move through the body, metres per second, smoothed (0 until measured).</summary>
        public float ClipGroundSpeed => _clipPace;

        /// <summary>The body's own speed over the ground, smoothed.</summary>
        public float BodyGroundSpeed => _bodyPace;

        /// <summary>True while a planted foot can be held in place: the clip's pace is near enough the body's (or the body is all but still).</summary>
        public bool LocksViable { get; private set; } = true;

        /// <summary>How many locks have been let go, and why (diagnostics).</summary>
        public int ReleasedBySwing { get; private set; }

        public int ReleasedByStretch { get; private set; }

        public int ReleasedByLift { get; private set; }

        /// <summary>Where the ground should be probed for a foot: under its lock while it is held, else under the clip's ankle.</summary>
        public Vector3 ProbeAt(int foot, Vector3 animated)
        {
            return _locked[foot] ? new Vector3(_lockPoint[foot].x, animated.y, _lockPoint[foot].z) : animated;
        }

        /// <summary>Forgets every lock and drop (a teleport, a respawn).</summary>
        public void Reset()
        {
            for (int i = 0; i < 2; i++)
            {
                _locked[i] = false;
                _lockWeight[i] = 0.0f;
                _weight[i] = 0.0f;
                _placement[i] = default;
            }
            _animatedFrames = 0;
            _hipDrop = 0.0f;
            _dip = 0.0f;
            _dipVelocity = 0.0f;
            _hasFloorLift = false;
            _clipPace = 0.0f;
            _bodyPace = 0.0f;
            LocksViable = true;
        }

        /// <summary>The body just landed at <paramref name="impactSpeed"/> metres per second: the knees give.</summary>
        public void Land(float impactSpeed)
        {
            if (impactSpeed < LandingMinSpeed)
            {
                return;
            }
            // A downward impulse into the spring; the dip itself follows over the next frames.
            float dip = Mathf.Min(impactSpeed * LandingDipPerMetrePerSecond, MaxLandingDip);
            _dipVelocity += dip * Mathf.Sqrt(LandingStiffness);
        }

        private static float SoleOf(in Sample sample, float soleOffset) => sample.SoleBelowAnkle > 0.0f ? sample.SoleBelowAnkle : soleOffset;

        /// <summary>
        /// One frame. <paramref name="active"/> is false in the air and in any state that should keep the clip's feet (then everything eases to
        /// nothing). <paramref name="floorY"/> is the height the clip's feet stand on (the controller's bottom); <paramref name="soleOffset"/> how far a
        /// flat foot's sole sits below the ankle. <paramref name="releaseLocks"/> lets every planted foot go this frame (the body turned on the spot:
        /// feet held in the world while the body spins would twist the legs). <paramref name="bodyVelocity"/> is the body's own, measured.
        /// </summary>
        public void Step(float deltaTime, bool active, in Sample left, in Sample right, float floorY, float soleOffset, bool releaseLocks = false,
            Vector3 bodyVelocity = default)
        {
            if (deltaTime <= 0.0f)
            {
                return;
            }
            var flatVelocity = new Vector3(bodyVelocity.x, 0.0f, bodyVelocity.z);
            float bodySpeed = flatVelocity.magnitude;
            Vector3 heading = bodySpeed > 0.2f ? flatVelocity / bodySpeed : Vector3.zero;   // under 0.2 m/s the body has no heading to swing along

            // THE CLIP'S PACE AGAINST THE BODY'S: how fast its planted feet move through the body, against how fast the body goes. Only when they
            // are near each other can a foot be held (LocksViable); the clips are matched to the body as far as they can be (ClipCadence).
            float smoothing = 1.0f - Mathf.Exp(-deltaTime / PaceSmoothingSeconds);
            if (_animatedFrames > 0 && bodySpeed > LockPaceAboveSpeed)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (_lastLift[i] - FloorLift >= PlantBelow)
                    {
                        continue;
                    }
                    Vector3 through = (i == 0 ? left.Animated : right.Animated) - _lastAnimated[i] - flatVelocity * deltaTime;
                    through.y = 0.0f;
                    _clipPace = Mathf.Lerp(_clipPace, through.magnitude / deltaTime, smoothing);
                }
            }
            _bodyPace = Mathf.Lerp(_bodyPace, bodySpeed, smoothing);
            float pace = _bodyPace > 0.01f ? _clipPace / _bodyPace : 1.0f;
            LocksViable = _bodyPace < LockPaceAboveSpeed || (_clipPace > 0.1f && pace >= LockPaceMin && pace <= LockPaceMax);
            _animatedFrames++;

            // THE CLIP'S OWN FLOOR: the lowest either foot has been lately, rising slowly when the feet stop coming so low (a new clip).
            float lowest = Mathf.Min(left.Animated.y - SoleOf(left, soleOffset), right.Animated.y - SoleOf(right, soleOffset)) - floorY;
            if (!_hasFloorLift || lowest < _floorLift)
            {
                _floorLift = lowest;
                _hasFloorLift = true;
            }
            else
            {
                _floorLift = Mathf.Min(lowest, _floorLift + FloorLiftRiseSpeed * deltaTime);
            }
            _floorLift = Mathf.Clamp(_floorLift, -0.3f, 0.3f);

            StepFoot(0, deltaTime, active, left, floorY, soleOffset, releaseLocks, heading, out float needLeft);
            StepFoot(1, deltaTime, active, right, floorY, soleOffset, releaseLocks, heading, out float needRight);

            // THE HIPS DROP for the foot that must reach furthest below where the clip has it (or furthest along a stride), never further than the legs can give.
            float wanted = active ? Mathf.Clamp(Mathf.Max(needLeft, needRight), 0.0f, MaxHipDrop) : 0.0f;
            float seconds = wanted > _hipDrop ? HipDropDownSeconds : HipDropUpSeconds;
            _hipDrop = Mathf.Lerp(_hipDrop, wanted, 1.0f - Mathf.Exp(-deltaTime / seconds));
            if (Mathf.Abs(_hipDrop - wanted) < 0.0005f)
            {
                _hipDrop = wanted;
            }

            // The landing dip: a critically damped spring back to zero, stepped in closed form (x = (x0 + (v0 + w x0) t) e^-wt): at 60 fps a plain
            // Euler step spent most of the landing impulse on the damping in its first frame and the knees barely moved.
            float omega = Mathf.Sqrt(LandingStiffness);
            float decay = Mathf.Exp(-omega * deltaTime);
            float c2 = _dipVelocity + omega * _dip;
            float dip = (_dip + c2 * deltaTime) * decay;
            _dipVelocity = (c2 - omega * (_dip + c2 * deltaTime)) * decay;
            _dip = Mathf.Max(0.0f, dip);
            if (!active)
            {
                _dip = 0.0f;
                _dipVelocity = 0.0f;
            }
            if (_dip < 0.0005f && Mathf.Abs(_dipVelocity) < 0.01f)
            {
                _dip = 0.0f;
                _dipVelocity = 0.0f;
            }

            // The targets are handed out in the world AFTER the drop: the ground does not move when the hips do.
            float drop = TotalDrop;
            for (int i = 0; i < 2; i++)
            {
                _placement[i].Weight = _weight[i];
                _placement[i].Locked = _locked[i];
                if (_weight[i] <= 0.0f)
                {
                    // Nothing to do: the clip's foot, where the drop put it.
                    _placement[i].Target = (i == 0 ? left.Animated : right.Animated) + Vector3.down * drop;
                    _placement[i].Normal = Vector3.up;
                }
            }
        }

        private void StepFoot(int i, float deltaTime, bool active, in Sample sample, float floorY, float soleOffsetDefault, bool releaseLocks, Vector3 heading,
            out float need)
        {
            need = 0.0f;
            float soleOffset = SoleOf(sample, soleOffsetDefault);
            bool usable = active && sample.HasGround && sample.Ground.y - floorY <= MaxFootRaise && floorY - sample.Ground.y <= MaxHipDrop + 0.15f;
            _weight[i] = Mathf.MoveTowards(_weight[i], usable ? 1.0f : 0.0f, deltaTime / WeightFadeSeconds);
            if (!active)
            {
                _locked[i] = false;
                _lockWeight[i] = 0.0f;
                _lastLift[i] = sample.Animated.y - floorY - soleOffset;
                _lastAnimated[i] = sample.Animated;
                return;
            }

            float rawLift = sample.Animated.y - floorY - soleOffset;
            _lastLift[i] = rawLift;
            _lastAnimated[i] = sample.Animated;
            // Everything is measured over the CLIP'S OWN floor (a run never brings its feet quite down to the walk's floor). A foot on that floor is
            // PRESSED onto the ground (no lift kept); a lifted foot keeps the clip's lift.
            float overFloor = rawLift - FloorLift;
            bool onFloor = overFloor < PlantBelow;
            float lift = onFloor ? 0.0f : Mathf.Max(0.0f, overFloor);
            // Where the sole should be, and how far below the clip's ankle that is (what the hips will give): known before the lock is judged,
            // since a lock's reach is judged with the hips where they are about to be, not where the clip has them.
            float groundTargetY = usable ? sample.Ground.y + soleOffset + lift + SoleLift : sample.Animated.y;
            float verticalNeed = usable ? Mathf.Max(0.0f, sample.Animated.y - groundTargetY) : 0.0f;
            float hipDropComing = Mathf.Clamp(Mathf.Max(_hipDrop, verticalNeed), 0.0f, MaxHipDrop);
            bool planted = usable && (_locked[i] ? overFloor < UnplantAbove : onFloor) && !releaseLocks;
            if (planted && !_locked[i] && LocksViable)
            {
                _locked[i] = true;
                _lockPoint[i] = sample.Animated;
                _lockWeight[i] = 1.0f;
            }
            if (_locked[i])
            {
                Vector3 pull = sample.Animated - _lockPoint[i];
                pull.y = 0.0f;
                bool swinging = Vector3.Dot(pull, heading) > SwingReleaseMetres;
                // Outrun: the lock is further from the clip's foot than allowed, or further from the hip than the leg can reach on the ground
                // (a held foot the leg cannot get down to would hover).
                bool outrun = pull.magnitude > MaxLockStretch;
                if (!outrun && usable && sample.LegLength > 0.1f)
                {
                    Vector3 toLock = _lockPoint[i] - sample.Hip;
                    float horizontal = new Vector3(toLock.x, 0.0f, toLock.z).magnitude;
                    float height = sample.Hip.y - hipDropComing - groundTargetY - MaxReachDip;
                    float reach = sample.LegLength * ReachShare;
                    outrun = horizontal * horizontal + height * height > reach * reach;
                }
                if (!planted || swinging || outrun)
                {
                    _locked[i] = false;   // the clip lifted it or swung it forward, or the body outran it: let go and fade back to the clip
                    if (!planted) ReleasedByLift++;
                    else if (swinging) ReleasedBySwing++;
                    else ReleasedByStretch++;
                }
            }
            if (!_locked[i])
            {
                _lockWeight[i] = Mathf.MoveTowards(_lockWeight[i], 0.0f, deltaTime / LockFadeSeconds);
            }

            Vector3 target = sample.Animated;
            if (_lockWeight[i] > 0.0f)
            {
                target.x = Mathf.Lerp(sample.Animated.x, _lockPoint[i].x, _lockWeight[i]);
                target.z = Mathf.Lerp(sample.Animated.z, _lockPoint[i].z, _lockWeight[i]);
            }
            Vector3 normal = Vector3.up;
            if (usable)
            {
                // The sole on the real ground, keeping whatever lift the clip gives the foot above its own floor.
                target.y = groundTargetY;
                // How far below the clip's ankle that is: what the hips must give for this leg to reach (a planted leg is straight; it cannot reach lower by itself).
                need = sample.Animated.y - target.y;
                // A LONG STRIDE (stride warping, a step down and forward): the hips dip so the leg can reach a target far out horizontally.
                if (sample.LegLength > 0.1f)
                {
                    Vector3 toTarget = target - sample.Hip;
                    float horizontal = new Vector3(toTarget.x, 0.0f, toTarget.z).magnitude;
                    float reach = sample.LegLength * ReachShare;
                    if (horizontal < reach * 0.95f)
                    {
                        float height = sample.Hip.y - target.y;
                        float highest = Mathf.Sqrt(Mathf.Max(reach * reach - horizontal * horizontal, 0.0f));
                        if (height > highest)
                        {
                            need = Mathf.Max(need, Mathf.Min(height - highest, MaxReachDip));
                        }
                    }
                    // STILL OUT OF REACH with the hips as low as they will go for this foot (a walk's warped stride on a long leg): a free foot is pulled
                    // in toward the hip along the ground, so it lands short rather than hanging in the air (the "floating" the owner saw).
                    if (!_locked[i])
                    {
                        float h = sample.Hip.y - Mathf.Clamp(need, 0.0f, MaxHipDrop) - target.y;
                        Vector3 flat = new Vector3(target.x - sample.Hip.x, 0.0f, target.z - sample.Hip.z);
                        float hz = flat.magnitude;
                        float r = reach * 0.99f;
                        if (h > 0.0f && h < r && hz > 1e-4f && hz * hz + h * h > r * r)
                        {
                            float most = Mathf.Sqrt(r * r - h * h);
                            target.x = sample.Hip.x + flat.x * (most / hz);
                            target.z = sample.Hip.z + flat.z * (most / hz);
                        }
                    }
                }
                normal = sample.Normal.sqrMagnitude > 0.5f ? sample.Normal.normalized : Vector3.up;
            }
            _placement[i].Target = target;
            _placement[i].Normal = normal;
        }
    }
}
