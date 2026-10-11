using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// OCTASLASH (key 6, sword in hand): Sephiroth's held up special. Press 6 and the body winds up for up to 3 s, low, the blade drawn back;
    /// then it dashes about 14 m wherever the camera is looking (up, down, anywhere), the long blade cutting all round it eight times on the way:
    /// seven light cuts and a heavy last one that throws what it hits on along the dash. A left click during the wind-up goes at once. The timing
    /// is OctaslashMove's.
    ///
    /// The dash moves the body through PlayerLocomotion.ForceVelocity (keys and gravity off), and stops against a wall, the remaining cuts
    /// landing where it stopped (as Sephiroth's do at an edge). The wind-up roots the body, and in the air it hangs there. Every cut goes through
    /// MeleeAttacker.LandSwordCut, so Strength and Bloodrage count, and other players are hit through their copies like any sword blow.
    ///
    /// MULTIPLAYER (IReplicatedAbility, id "octaslash"): the owner's copy fires as the wind-up starts, as the dash leaves (with its direction)
    /// and as it ends. Another player's copy of the body shows the pose, the lean and the slashes on the same timeline; its movement arrives
    /// with the body's own transform. A copy hurts nothing.
    /// </summary>
    public sealed class OctaslashAbility : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "octaslash";

        public const int EventCharging = 0;
        public const int EventDashing = 1;
        public const int EventEnded = 2;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        [Tooltip("The streak material the slashes are drawn with (the sword spin's own).")]
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

        private bool _remote;
        private bool _stopped;
        private float _cooldown;
        private Vector3 _aim = Vector3.forward;
        private MeleeAttacker _melee;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private Transform _visualRoot;

        private void Awake()
        {
            Bind();
        }

        public void Bind()
        {
            _melee = GetComponent<MeleeAttacker>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _vitals = GetComponent<PlayerVitals>();
            Animator animator = GetComponentInChildren<Animator>();
            _visualRoot = animator != null ? animator.transform : null;
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
            if (!_remote && _locomotion != null)
            {
                _locomotion.MovementRooted = false;
                _locomotion.GravityScale = 1.0f;
                _locomotion.ForceVelocity(DashDirection * OctaslashMove.DashSpeed);
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
                    // Sephiroth's coil: the blade drawn back and low behind, the body leaning forward over it toward where it will go.
                    Pose(1.0f, WindUpBladeBackDegrees, WindUpBladeDownDegrees);
                    Lean(Mathf.Clamp01(PhaseTime / 0.25f) * WindUpLean, _remote ? transform.forward : _aim);
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
                    Pose(1.0f, 0.0f);
                    Lean(1.0f, DashDirection);
                    StopAtWalls();
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
                    float back = Mathf.Clamp01(PhaseTime / OctaslashMove.RecoverySeconds);
                    Pose(1.0f - back, 0.0f);
                    Lean(1.0f - back, DashDirection);
                    if (PhaseTime >= OctaslashMove.RecoverySeconds)
                    {
                        End();
                    }
                    break;
            }
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
            float height = _locomotion != null ? _locomotion.StandingHeight : 2.6f;
            OctaslashSlash.Spawn(SlashMaterial, transform.position + Vector3.up * (height * 0.55f), DashDirection, index, final);
        }

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
            Pose(0.0f, 0.0f);
            Lean(0.0f, DashDirection);
            Current = Phase.Idle;
            PhaseTime = 0.0f;
            if (!_remote)
            {
                _cooldown = OctaslashMove.CooldownSeconds;
                if (wasActive)
                {
                    Fired?.Invoke(new AbilityEvent(EventEnded, transform.position, DashDirection));
                }
            }
        }

        /// <summary>How far round behind the blade is drawn in the wind-up (0 = straight out to the right), and how far below level.</summary>
        public const float WindUpBladeBackDegrees = 110.0f;

        public const float WindUpBladeDownDegrees = -35.0f;

        /// <summary>How much of a dash's lunge the body already leans in the wind-up, toward where it is aimed.</summary>
        public const float WindUpLean = 0.4f;

        /// <summary>The one-handed sword pose the spin uses (PlayerLocomotion.ApplySwordSpinPose): arm out, the blade laid along it.</summary>
        private void Pose(float weight, float bladeBackDegrees, float bladePitchDegrees = 0.0f)
        {
            if (_locomotion != null)
            {
                _locomotion.SetSwordSpinPose(weight, bladeBackDegrees, bladePitchDegrees, _melee != null ? _melee.SwordBlade : null);
            }
        }

        /// <summary>
        /// The whole body tipped toward <paramref name="direction"/> (OctaslashMove.LeanUp) by <paramref name="amount"/>, on the visual root: the
        /// same transform the jetpack tilts and the running lean adds to.
        /// </summary>
        private void Lean(float amount, Vector3 direction)
        {
            if (_visualRoot == null)
            {
                return;
            }
            Quaternion tip = Quaternion.FromToRotation(Vector3.up, OctaslashMove.LeanUp(direction));
            Quaternion world = Quaternion.Slerp(Quaternion.identity, tip, amount);
            _visualRoot.localRotation = Quaternion.Inverse(transform.rotation) * world * transform.rotation;
        }

        /// <summary>Another player's Octaslash, shown on this copy of their body: pose, lean and slashes on the same timeline, hurting nothing.</summary>
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
