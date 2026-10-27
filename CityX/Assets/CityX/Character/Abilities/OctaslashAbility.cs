using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// OCTASLASH (key 6, sword in hand): Sephiroth's held up special. Press 6 and the body coils for up to 1 s, low, the blade drawn back to the
    /// hip and aimed ahead; then it dashes about 14 m wherever the camera is looking (up, down, anywhere), facing along it in a flying lunge (one
    /// knee driven forward, the other leg stretched back) while the long blade whips from side to side in front: a cut every two frames, seven
    /// light ones, then the blade swung up overhead and a heavy last one down the middle that throws what it hits on along the dash. Each cut
    /// leaves a slash of light trailing from the real blade (BladeTrail). A left click during the wind-up goes at once. The timing is
    /// OctaslashMove's.
    ///
    /// THE BODY IS ANIMATED IN BLENDER (CityX/SourceArt/Octaslash, from the two Smash GIFs, frame by frame) and played bone by bone after the
    /// Animator (BakedMotion, PlayerLocomotion.SetBakedPose): the coil, a hold that loops while the wind-up lasts, the 16-frame dash, the
    /// follow-through. The dash is authored level; it is turned about the body's middle to the real dash's pitch, and the body faces the dash.
    ///
    /// The dash moves the body through PlayerLocomotion.ForceVelocity (keys and gravity off), and stops against a wall, the remaining cuts
    /// landing where it stopped (as Sephiroth's do at an edge). The wind-up roots the body, and in the air it hangs there. Every cut goes through
    /// MeleeAttacker.LandSwordCut, so Strength and Bloodrage count, and other players are hit through their copies like any sword blow.
    ///
    /// MULTIPLAYER (IReplicatedAbility, id "octaslash"): the owner's copy fires as the wind-up starts, as the dash leaves (with its direction)
    /// and as it ends. Another player's copy of the body plays the same animation and slashes on the same timeline; its movement and facing
    /// arrive with the body's own transform. A copy hurts nothing.
    /// </summary>
    public sealed class OctaslashAbility : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "octaslash";

        public const int EventCharging = 0;
        public const int EventDashing = 1;
        public const int EventEnded = 2;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        [Tooltip("The streak material the slashes trailing from the blade are drawn with (the sword spin's own).")]
        public Material SlashMaterial;

        public enum Phase
        {
            Idle,
            Charging,
            Dashing,
            Recovering,
        }

        public Phase Current { get; private set; } = Phase.Idle;

        /// <summary>Seconds into the current phase.</summary>
        public float PhaseTime { get; private set; }

        /// <summary>How many of the eight cuts this dash has dealt (or shown, on a copy).</summary>
        public int CutsDone { get; private set; }

        /// <summary>How many targets the cuts have hit, all told (for tests).</summary>
        public int TargetsHit { get; private set; }

        public Vector3 DashDirection { get; private set; } = Vector3.forward;

        public bool IsActive => Current != Phase.Idle;

        public bool IsCharging => Current == Phase.Charging;

        public bool IsDashing => Current == Phase.Dashing;

        /// <summary>The animation frame being shown (null while idle, or on a body that cannot show one).</summary>
        public BakedPose ShownPose => _poseWeight > 0.0f ? _pose : null;

        /// <summary>The slashes of light behind the blade (null until the first dash).</summary>
        public BladeTrail Trail => _trail;

        private bool _remote;
        private bool _stopped;
        private float _cooldown;
        private Vector3 _aim = Vector3.forward;
        private BladeTrail _trail;
        private MeleeAttacker _melee;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private BakedMotion _motion;
        private BakedPose _pose;
        private float _poseWeight;

        private void Awake()
        {
            Bind();
        }

        public void Bind()
        {
            _melee = GetComponent<MeleeAttacker>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _vitals = GetComponent<PlayerVitals>();
            _motion = BakedMotion.Load(OctaslashMove.MotionResource);
            _pose = _motion != null ? new BakedPose(_motion.Bones) : null;
        }

        public bool CanStart => Current == Phase.Idle && _cooldown <= 0.0f && _melee != null && _melee.HoldsSword && !_melee.IsSwinging
                                && (_vitals == null || !_vitals.IsDead);

        /// <summary>Key 6: begins the wind-up. <paramref name="look"/> is where the camera points (and keeps being updated through Aim).</summary>
        public bool TryStart(Vector3 look)
        {
            if (!CanStart)
            {
                return false;
            }
            Aim(look);
            Enter(Phase.Charging);
            if (_locomotion != null)
            {
                _locomotion.MovementRooted = true;
                if (!_locomotion.IsGrounded)
                {
                    // In the air it hangs where it started, as Sephiroth does winding it up.
                    _locomotion.SetVerticalVelocity(0.0f);
                    _locomotion.GravityScale = 0.0f;
                }
            }
            Fired?.Invoke(new AbilityEvent(EventCharging, transform.position, transform.forward));
            return true;
        }

        /// <summary>Where the camera points, every frame of the wind-up: the dash goes that way when it leaves.</summary>
        public void Aim(Vector3 look)
        {
            if (look.sqrMagnitude > 1e-6f)
            {
                _aim = look.normalized;
            }
        }

        /// <summary>A left click during the wind-up: the dash goes now, where the camera points.</summary>
        public bool Release()
        {
            if (Current != Phase.Charging || _remote)
            {
                return false;
            }
            BeginDash(_aim);
            return true;
        }

        private void BeginDash(Vector3 direction)
        {
            DashDirection = direction.sqrMagnitude > 1e-6f ? direction.normalized : transform.forward;
            Enter(Phase.Dashing);
            CutsDone = 0;
            _stopped = false;
            if (_trail == null)
            {
                _trail = GetComponent<BladeTrail>();   // not ??: a missing component is a Unity "fake null" in the editor
                if (_trail == null)
                {
                    _trail = gameObject.AddComponent<BladeTrail>();
                }
            }
            _trail.Begin(HeldBlade(), SlashMaterial);
            if (!_remote && _locomotion != null)
            {
                _locomotion.MovementRooted = false;
                _locomotion.GravityScale = 1.0f;
                _locomotion.ForceVelocity(DashDirection * OctaslashMove.DashSpeed);
                // Face the dash for the whole move (the animation is authored facing it); straight up or down keeps the facing it had.
                if (FlatYaw(DashDirection, out float yaw))
                {
                    _locomotion.SetFacingOverride(yaw, OctaslashMove.SnapTurnDegreesPerSecond, OctaslashMove.DashSeconds + OctaslashMove.RecoverySeconds);
                }
                PassThroughBodiesAhead();
            }
            if (!_remote)
            {
                Fired?.Invoke(new AbilityEvent(EventDashing, transform.position, DashDirection));
            }
        }

        private void Enter(Phase phase)
        {
            Current = phase;
            PhaseTime = 0.0f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances the move by <paramref name="deltaTime"/>. Public so a test can step it.</summary>
        public void Tick(float deltaTime)
        {
            if (_cooldown > 0.0f)
            {
                _cooldown -= deltaTime;
            }
            if (Current == Phase.Idle)
            {
                return;
            }
            PhaseTime += deltaTime;
            if (!_remote && (_melee == null || !_melee.HoldsSword || (_vitals != null && _vitals.IsDead)))
            {
                End();   // the sword left the hand, or the body died: the move is over
                return;
            }

            switch (Current)
            {
                case Phase.Charging:
                    // The coil, then held (looping) until the dash goes, the body turning to face where it will go.
                    if (!_remote && _locomotion != null && FlatYaw(_aim, out float yaw))
                    {
                        _locomotion.SetFacingOverride(yaw, OctaslashMove.WindUpTurnDegreesPerSecond, 0.1f);
                    }
                    ShowWindUp();
                    if (!_remote && PhaseTime >= OctaslashMove.ChargeSeconds)
                    {
                        BeginDash(_aim);
                    }
                    else if (_remote && PhaseTime > OctaslashMove.ChargeSeconds + 1.0f)
                    {
                        End();   // the dash event never came: a lost message must not leave a copy frozen in the wind-up
                    }
                    break;

                case Phase.Dashing:
                    StopAtWalls();
                    Show(OctaslashMove.DashSegment, PhaseTime, false, 1.0f, DashTurn(PhaseTime));
                    int due = OctaslashMove.CutsDueBy(PhaseTime);
                    while (CutsDone < due)
                    {
                        Cut(CutsDone);
                        CutsDone++;
                    }
                    if (PhaseTime >= OctaslashMove.DashSeconds)
                    {
                        if (!_remote && _locomotion != null)
                        {
                            _locomotion.ReleaseForcedVelocity();
                            _locomotion.SetVerticalVelocity(0.0f);
                            _locomotion.MovementRooted = true;
                        }
                        Enter(Phase.Recovering);
                    }
                    break;

                case Phase.Recovering:
                    // The last cut carries on into the follow-through's first frames; after that the blade's path is no longer a slash.
                    if (PhaseTime >= OctaslashMove.TrailAfterDashSeconds && _trail != null)
                    {
                        _trail.Stop();
                    }
                    // The follow-through, let go of over its last fifth of a second (it ends close to the sword's own stance).
                    float letGo = Mathf.InverseLerp(OctaslashMove.RecoverySeconds - OctaslashMove.PoseBlendOutSeconds, OctaslashMove.RecoverySeconds, PhaseTime);
                    Show(OctaslashMove.RecoverSegment, PhaseTime, false, 1.0f - Mathf.SmoothStep(0.0f, 1.0f, letGo), Quaternion.identity);
                    if (PhaseTime >= OctaslashMove.RecoverySeconds)
                    {
                        End();
                    }
                    break;
            }
        }

        private void ShowWindUp()
        {
            float weight = Mathf.SmoothStep(0.0f, 1.0f, PhaseTime / OctaslashMove.PoseBlendInSeconds);
            float coil = _motion != null && _motion.HasSegment(OctaslashMove.CoilSegment) ? _motion.SegmentSeconds(OctaslashMove.CoilSegment) : 0.0f;
            if (PhaseTime < coil)
            {
                Show(OctaslashMove.CoilSegment, PhaseTime, false, weight, Quaternion.identity);
            }
            else
            {
                Show(OctaslashMove.HoldSegment, PhaseTime - coil, true, weight, Quaternion.identity);
            }
        }

        /// <summary>Puts a frame of the animation on the body (nothing on a body that cannot show it).</summary>
        private void Show(string segment, float seconds, bool loop, float weight, Quaternion turn)
        {
            if (_motion == null || _pose == null || !_motion.HasSegment(segment))
            {
                return;
            }
            _motion.Sample(segment, seconds, loop, _pose);
            _pose.Turn = turn;
            _pose.Pivot = OctaslashMove.FlightPivot;
            _poseWeight = weight;
            if (_locomotion != null)
            {
                _locomotion.SetBakedPose(_pose, weight);
            }
        }

        /// <summary>
        /// The animation's dash is level; the real one goes wherever the camera looked. The body is turned about its middle to point along it,
        /// fully until the last cut, which swings it back upright (the animation's own follow-through is upright).
        /// </summary>
        private Quaternion DashTurn(float seconds)
        {
            Vector3 local = Quaternion.Inverse(transform.rotation) * DashDirection;
            Quaternion full = Quaternion.FromToRotation(Vector3.forward, local);
            float upright = Mathf.InverseLerp(OctaslashMove.CutTime(OctaslashMove.CutCount - 2), OctaslashMove.DashSeconds, seconds);
            return Quaternion.Slerp(full, Quaternion.identity, Mathf.SmoothStep(0.0f, 1.0f, upright));
        }

        private static bool FlatYaw(Vector3 direction, out float yaw)
        {
            var flat = new Vector3(direction.x, 0.0f, direction.z);
            yaw = flat.sqrMagnitude > 1e-4f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : 0.0f;
            return flat.sqrMagnitude > 1e-4f;
        }

        /// <summary>Against a wall the dash stops where it is; the cuts it has left still land there.</summary>
        private void StopAtWalls()
        {
            if (_remote || _stopped || _locomotion == null || PhaseTime < 0.02f)
            {
                return;
            }
            CollisionFlags flags = _locomotion.LastMoveCollisionFlags;
            bool blocked = (flags & CollisionFlags.Sides) != 0 || ((flags & CollisionFlags.Above) != 0 && DashDirection.y > 0.0f)
                           || ((flags & CollisionFlags.Below) != 0 && DashDirection.y < -0.2f);
            if (blocked)
            {
                _stopped = true;
                _locomotion.ForceVelocity(Vector3.zero);
            }
        }

        private void Cut(int index)
        {
            bool final = OctaslashMove.IsFinal(index);
            if (!_remote && _melee != null)
            {
                Vector3 launch = final
                    ? new Vector3(DashDirection.x, 0.0f, DashDirection.z).normalized * OctaslashMove.FinalLaunchAlong + Vector3.up * OctaslashMove.FinalLaunchUp
                    : Vector3.zero;
                TargetsHit += _melee.LandSwordCut(OctaslashMove.CutScale(index), OctaslashMove.CutReachMetres, launch);
            }
            // What the cut looks like is the blade's own swing in the animation, and the light it trails (BladeTrail).
        }

        /// <summary>The sword in the right hand: the equipped one on the player's own body, the mounted model on another player's copy.</summary>
        private ISwordBlade HeldBlade() => _melee != null ? _melee.BladeInHand() : null;

        private readonly System.Collections.Generic.List<Collider> _passedThrough = new System.Collections.Generic.List<Collider>();

        /// <summary>
        /// THE DASH GOES THROUGH PEOPLE, cutting them, as Sephiroth's carries through whoever it hits; only the world stops it. Every body
        /// (anything that can be hurt) along the dash's whole path is set to not collide with this one until the move ends.
        /// </summary>
        private void PassThroughBodiesAhead()
        {
            var controller = GetComponent<CharacterController>();
            if (controller == null)
            {
                return;
            }
            Vector3 start = transform.position + Vector3.up * (controller.height * 0.5f);
            Vector3 end = start + DashDirection * (OctaslashMove.DashMetres + 2.0f);
            foreach (Collider other in Physics.OverlapCapsule(start, end, controller.radius + 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other == null || other == controller || other.transform.IsChildOf(transform) || other.GetComponentInParent<IDamageable>() == null)
                {
                    continue;
                }
                Physics.IgnoreCollision(controller, other, true);
                _passedThrough.Add(other);
            }
        }

        private void SolidAgain()
        {
            var controller = GetComponent<CharacterController>();
            foreach (Collider other in _passedThrough)
            {
                if (other != null && controller != null)
                {
                    Physics.IgnoreCollision(controller, other, false);
                }
            }
            _passedThrough.Clear();
        }

        /// <summary>A stagger cuts the wind-up short (2026-10-14, Stability): the move ends there, and other players see it end. The dash, once gone, goes on.</summary>
        public void Interrupt()
        {
            if (Current == Phase.Charging)
            {
                End();
            }
        }

        private void End()
        {
            SolidAgain();
            bool wasActive = Current != Phase.Idle;
            if (!_remote && _locomotion != null)
            {
                _locomotion.ReleaseForcedVelocity();
                _locomotion.MovementRooted = false;
                _locomotion.GravityScale = 1.0f;
            }
            _poseWeight = 0.0f;
            if (_locomotion != null)
            {
                _locomotion.SetBakedPose(null, 0.0f);
            }
            if (_trail != null)
            {
                _trail.Stop();
            }
            Current = Phase.Idle;
            PhaseTime = 0.0f;
            if (!_remote)
            {
                _cooldown = OctaslashMove.CooldownSeconds * PlayerVitals.CooldownFactorOf(_vitals);   // Cooldown Reduction shortens it
                if (wasActive)
                {
                    Fired?.Invoke(new AbilityEvent(EventEnded, transform.position, DashDirection));
                }
            }
        }

        /// <summary>Another player's Octaslash, shown on this copy of their body: the animation and slashes on the same timeline, hurting nothing.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            _remote = true;
            switch (evt.Kind)
            {
                case EventCharging:
                    Enter(Phase.Charging);
                    break;
                case EventDashing:
                    BeginDash(evt.Direction);
                    break;
                case EventEnded:
                    End();
                    break;
            }
        }
    }
}
