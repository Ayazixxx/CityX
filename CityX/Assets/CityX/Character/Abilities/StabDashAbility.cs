using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE STAB DASH (Sword tree, from a slot on the bar, sword in hand): the Mii Swordfighter's grounded Power Thrust from Smash Ultimate (owner,
    /// 2026-10-18, with SmashWiki's hitbox GIF: "make a hotbar skill for the swordsman called stab dash where it pretty much goes the same distance
    /// as a dash but he does a stab like the gif attached"). The body sinks into a deep squat with the sword drawn back past the shoulder, then
    /// lunges: the blade thrust out straight ahead, the body sliding two dashes' 9.6 m where the camera looks, off fast and slowing to a stop, held
    /// low in the lunge until it rises back into the stance. The thrust runs through what is ahead of it, each target once, hardest at its very
    /// start. The timing is StabDashMove's.
    ///
    /// THE BODY IS ANIMATED IN BLENDER (CityX/SourceArt/StabDash, from the GIF frame by frame) and played bone by bone after the Animator
    /// (BakedMotion, PlayerLocomotion.SetBakedPose): its frame f is the move's frame f.
    ///
    /// THE SLIDE MOVES THE BODY ITSELF, by what the curve still owes each frame (as a sword swing's lunge does), so a long frame cannot carry it
    /// past 9.6 m, and gravity keeps it on a hill; a wall, or a body it runs into, stops it there, and the thrust still hurts until its window
    /// ends. The body faces the stab. Every blow goes through MeleeAttacker.LandSwordCut, so Strength and Bloodrage count and other players are
    /// hit through their copies. From the key to the end it HOLDS THE BODY (PlayerInputReader reads nothing else), as Smash's commits.
    ///
    /// MULTIPLAYER (IReplicatedAbility, id "stab-dash"): the owner sends the start and the end. Another player's copy plays the same animation on
    /// the same timeline from the start; its slide and facing arrive with the body's own transform. A copy hurts nothing.
    /// </summary>
    public sealed class StabDashAbility : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "stab-dash";

        public const int EventStarted = 0;
        public const int EventEnded = 1;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        /// <summary>Whether the move is running (on this body, or shown on another player's copy).</summary>
        public bool IsActive { get; private set; }

        /// <summary>Seconds since the key: the move's own clock, which StabDashMove's times are on.</summary>
        public float Elapsed { get; private set; }

        /// <summary>From the key to the end, on the body it belongs to: nothing else is read meanwhile.</summary>
        public bool HoldsTheBody => IsActive && !_remote;

        /// <summary>Drawing back, before the lunge leaves: the body still turns to where the camera looks.</summary>
        public bool IsWindingUp => IsActive && Elapsed < StabDashMove.SlideStartSeconds;

        /// <summary>Which way the stab goes: where the camera looked when the lunge left.</summary>
        public Vector3 Direction { get; private set; } = Vector3.forward;

        /// <summary>How far the slide has carried the body, metres.</summary>
        public float Slid { get; private set; }

        /// <summary>Whether a wall (or a body) stopped the slide short.</summary>
        public bool Stopped { get; private set; }

        /// <summary>How many targets the thrusts have run through, all told (for tests).</summary>
        public int TargetsHit { get; private set; }

        /// <summary>The animation frame being shown (null while idle, or on a body that cannot show one).</summary>
        public BakedPose ShownPose => _poseWeight > 0.0f ? _pose : null;

        public bool CanStart => !IsActive && _cooldown <= 0.0f && _melee != null && _melee.HoldsSword && !_melee.IsSwinging
                                && (_vitals == null || !_vitals.IsDead);

        private readonly HashSet<IDamageable> _hit = new HashSet<IDamageable>();
        private bool _remote;
        private bool _lunging;
        private float _cooldown;
        private Vector3 _aim = Vector3.forward;
        private MeleeAttacker _melee;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private CharacterController _controller;
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
            _controller = GetComponent<CharacterController>();
            _motion = BakedMotion.Load(StabDashMove.MotionResource);
            _pose = _motion != null ? _motion.NewPose() : null;
            if (_pose != null)
            {
                _pose.ThrustGrip = true;
            }
        }

        /// <summary>The key: draws back, turning to where <paramref name="look"/> points (kept up to date through Aim until the lunge leaves).</summary>
        public bool TryStart(Vector3 look)
        {
            if (!CanStart)
            {
                return false;
            }
            Aim(look);
            Begin();
            if (_locomotion != null)
            {
                _locomotion.MovementRooted = true;
            }
            Fired?.Invoke(new AbilityEvent(EventStarted, transform.position, _aim));
            return true;
        }

        /// <summary>Where the camera points, every frame of the wind-up: the stab goes that way, level, when the lunge leaves.</summary>
        public void Aim(Vector3 look)
        {
            look.y = 0.0f;
            if (look.sqrMagnitude > 1e-6f)
            {
                _aim = look.normalized;
            }
        }

        private void Begin()
        {
            IsActive = true;
            Elapsed = 0.0f;
            Slid = 0.0f;
            Stopped = false;
            _lunging = false;
            _hit.Clear();
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
            if (!IsActive)
            {
                return;
            }
            Elapsed += deltaTime;
            if (!_remote)
            {
                if (_melee == null || !_melee.HoldsSword || (_vitals != null && _vitals.IsDead))
                {
                    End();   // the sword left the hand, or the body died: the move is over
                    return;
                }
                Steer();
                Slide();
                Strike();
            }
            Show();
            if (Elapsed >= StabDashMove.CommitSeconds)
            {
                End();
            }
        }

        /// <summary>Turns to where the camera looks while drawing back; when the lunge leaves, locks the way it goes and faces it to the end.</summary>
        private void Steer()
        {
            if (_locomotion == null)
            {
                return;
            }
            if (Elapsed < StabDashMove.SlideStartSeconds)
            {
                _locomotion.SetFacingOverride(YawOf(_aim), StabDashMove.WindUpTurnDegreesPerSecond, 0.1f);
                return;
            }
            if (!_lunging)
            {
                _lunging = true;
                Direction = _aim;
                _locomotion.SetFacingOverride(YawOf(Direction), StabDashMove.SnapTurnDegreesPerSecond, StabDashMove.CommitSeconds - Elapsed + 0.1f);
            }
        }

        private void Slide()
        {
            if (!_lunging || Stopped || _controller == null)
            {
                return;
            }
            float owed = StabDashMove.SlideDistance(Elapsed) - Slid;
            if (owed <= 0.0f)
            {
                return;
            }
            CollisionFlags flags = _controller.Move(Direction * owed);
            Slid += owed;
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Stopped = true;
            }
        }

        private void Strike()
        {
            float scale = StabDashMove.DamageScale(Elapsed);
            if (scale <= 0.0f || _melee == null)
            {
                return;
            }
            Vector3 launch = StabDashMove.IsClean(Elapsed)
                ? Direction * StabDashMove.CleanLaunchAlong + Vector3.up * StabDashMove.CleanLaunchUp
                : Vector3.zero;
            TargetsHit += _melee.LandSwordCut(scale, StabDashMove.ReachMetres, launch, StabDashMove.HalfArcDegrees, _hit);
        }

        /// <summary>Puts the move's frame on the body, taken over in 3 frames and handed back over the last 8.</summary>
        private void Show()
        {
            if (_motion == null || _pose == null || _locomotion == null || !_motion.HasSegment(StabDashMove.Segment))
            {
                return;
            }
            float taking = Mathf.SmoothStep(0.0f, 1.0f, Elapsed / StabDashMove.PoseBlendInSeconds);
            float giving = Mathf.SmoothStep(0.0f, 1.0f, (StabDashMove.CommitSeconds - Elapsed) / StabDashMove.PoseBlendOutSeconds);
            _poseWeight = Mathf.Min(taking, giving);
            _motion.Sample(StabDashMove.Segment, Elapsed, false, _pose);
            _pose.Turn = Quaternion.identity;
            _locomotion.SetBakedPose(_pose, _poseWeight);
        }

        private static float YawOf(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        /// <summary>A stagger cuts the wind-up short (as it does Octaslash's): the move ends there, and other players see it end. The lunge, once gone, goes on.</summary>
        public void Interrupt()
        {
            if (IsWindingUp && !_remote)
            {
                End();
            }
        }

        private void End()
        {
            bool wasActive = IsActive;
            IsActive = false;
            _poseWeight = 0.0f;
            if (_locomotion != null)
            {
                if (!_remote)
                {
                    _locomotion.MovementRooted = false;
                }
                _locomotion.SetBakedPose(null, 0.0f);
            }
            if (!_remote)
            {
                _cooldown = StabDashMove.CooldownSeconds * PlayerVitals.CooldownFactorOf(_vitals);   // Cooldown Reduction shortens it
                if (wasActive)
                {
                    Fired?.Invoke(new AbilityEvent(EventEnded, transform.position, Direction));
                }
            }
        }

        /// <summary>Another player's stab dash, shown on this copy of their body: the animation on the same timeline, hurting nothing.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            _remote = true;
            if (evt.Kind == EventStarted)
            {
                Begin();
            }
            else if (evt.Kind == EventEnded && IsActive)
            {
                End();
            }
        }
    }
}
