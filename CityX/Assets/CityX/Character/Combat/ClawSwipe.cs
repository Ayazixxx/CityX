using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A CLAW SWIPE (owner, 2026-11-25: "give squirrel boy a claw attack with a trail that looks like the image attached. you can use the same
    /// swing used in the wraith punch"; docs/SQUIRREL_BOY.md). The Wraith's punch, recorded in Blender (SourceArt/WraithPunch: drawn back and
    /// swung over and down), played on the upper body after the Animator, the legs left to their walk; three streaks of light behind the claws
    /// (ClawTrail); and as the swing comes down, a cut on whatever stands in front within reach, once each.
    ///
    /// MULTIPLAYER: announced by name (PlayerAnimationDriver.ClawSwipeName), as a giant's landing is; every other player's copy of the body
    /// swipes and streaks on the same timeline, and cuts nothing (the body's own machine did).
    /// </summary>
    public sealed class ClawSwipe : MonoBehaviour
    {
        /// <summary>The swing: the Wraith's punch, under Resources.</summary>
        public const string MotionResource = Wraith.PunchMotionResource;

        /// <summary>The recording's parts: drawn back and swung down (the blow lands as it ends), then through and back.</summary>
        public const string SwingSegment = "swing";

        public const string FollowSegment = "follow";

        /// <summary>What the cut does, and how long it stuns (a fist's is 10: the claws are worse, the Wraith's punch is 20).</summary>
        public const float Damage = 15.0f;

        public const float HitstunSeconds = 0.5f;

        /// <summary>How far from the middle claw, as the swing comes down, a body is cut, metres at the body's scale.</summary>
        public const float ReachMetres = 1.3f;

        /// <summary>How quickly the swing comes over the body and goes, seconds.</summary>
        public const float BlendInSeconds = 0.08f;

        public const float BlendOutSeconds = 0.2f;

        /// <summary>
        /// The streaks are drawn over the cutting stroke alone, from this long before the blow lands to this long after, seconds: the whole
        /// swing (drawn back, up over the head and down) drew one long loop, where the owner's picture has three short crescents.
        /// </summary>
        public const float DrawBeforeLandSeconds = 0.16f;

        public const float TrailAfterLandSeconds = 0.03f;

        [Tooltip("What the streaks are drawn in, and their sparks (ClawSlashMaterialBuilder).")]
        public Material SlashMaterial;

        public Material SparkMaterial;

        /// <summary>Whether a swipe is under way (on this body, or shown on another player's copy).</summary>
        public bool IsActive { get; private set; }

        /// <summary>Seconds since the swipe began.</summary>
        public float Elapsed { get; private set; }

        /// <summary>How many bodies the cuts have landed on, all told (for tests).</summary>
        public int Cuts { get; private set; }

        /// <summary>The whole swipe, seconds: the recording's swing and follow-through.</summary>
        public float TotalSeconds => _motion != null ? _swingSeconds + _followSeconds : 1.0f;

        /// <summary>When the blow lands, seconds into the swipe.</summary>
        public float LandsAtSeconds => _motion != null ? _swingSeconds : 0.5f;

        public ClawTrail Trail => _trail;

        private static readonly Collider[] Near = new Collider[32];
        private readonly HashSet<IDamageable> _cut = new HashSet<IDamageable>();
        private BakedMotion _motion;
        private BakedPose _pose;
        private float _swingSeconds;
        private float _followSeconds;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private Ragdoll _ragdoll;
        private PlayerAnimationDriver _driver;
        private ClawTrail _trail;
        private bool _remote;
        private bool _landed;
        private bool _drawing;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            _vitals = GetComponent<PlayerVitals>();
            _ragdoll = GetComponent<Ragdoll>();
            _driver = GetComponent<PlayerAnimationDriver>();
            _trail = GetComponent<ClawTrail>();
            if (_trail == null)
            {
                _trail = gameObject.AddComponent<ClawTrail>();
            }
            _motion = BakedMotion.Load(MotionResource);
            if (_motion == null || !_motion.HasSegment(SwingSegment) || !_motion.HasSegment(FollowSegment))
            {
                Debug.LogError($"CityX: no {MotionResource} with a swing and a follow-through -- no claw swipe.");
                _motion = null;
                return;
            }
            _pose = _motion.NewPose();
            _pose.UpperBodyOnly = true;   // the legs walk on: nobody is rooted for a blow
            _swingSeconds = _motion.SegmentSeconds(SwingSegment);
            _followSeconds = _motion.SegmentSeconds(FollowSegment);
        }

        private void OnEnable()
        {
            if (_driver != null)
            {
                _driver.RemoteClawSwiped += ShowRemote;
            }
        }

        private void OnDisable()
        {
            if (_driver != null)
            {
                _driver.RemoteClawSwiped -= ShowRemote;
            }
            if (IsActive)
            {
                End();
            }
        }

        /// <summary>Whether a swipe can start now on this body.</summary>
        public bool CanStart => !IsActive && _motion != null && (_vitals == null || (!_vitals.IsDead && !_vitals.IsStunned))
                                && (_ragdoll == null || !_ragdoll.IsLimp);

        /// <summary>Swipes, and tells the other players' copies of this body.</summary>
        public bool TryStart()
        {
            if (!CanStart)
            {
                return false;
            }
            _remote = false;
            Begin();
            _driver?.AnnounceState(PlayerAnimationDriver.ClawSwipeName);
            return true;
        }

        /// <summary>The body this is a copy of swiped: the same swing and streaks here, cutting nothing.</summary>
        private void ShowRemote()
        {
            if (_motion == null)
            {
                return;
            }
            _remote = true;
            Begin();
        }

        private void Begin()
        {
            IsActive = true;
            Elapsed = 0.0f;
            _landed = false;
            _drawing = false;
            _cut.Clear();
        }

        private void Update()
        {
            if (!IsActive)
            {
                return;
            }
            Elapsed += Time.deltaTime;
            bool knockedOut = (_vitals != null && (_vitals.IsDead || _vitals.IsStunned)) || (_ragdoll != null && _ragdoll.IsLimp);
            if (knockedOut && !_remote)
            {
                End();   // hit out of it: the swing is lost
                return;
            }
            if (!_drawing && Elapsed >= LandsAtSeconds - DrawBeforeLandSeconds)
            {
                _drawing = true;
                _trail.Begin(SlashMaterial, SparkMaterial);   // the cutting stroke: the claws come down
            }
            if (!_landed && Elapsed >= LandsAtSeconds)
            {
                _landed = true;
                if (!_remote)
                {
                    Cut();
                }
            }
            if (_trail.IsFollowing && Elapsed >= LandsAtSeconds + TrailAfterLandSeconds)
            {
                _trail.Stop();
            }
            if (Elapsed >= TotalSeconds)
            {
                End();
                return;
            }
            Show();
        }

        /// <summary>The swing's frame on the body, eased in and out.</summary>
        private void Show()
        {
            if (_locomotion == null || _pose == null)
            {
                return;
            }
            if (Elapsed < _swingSeconds)
            {
                _motion.Sample(SwingSegment, Elapsed, false, _pose);
            }
            else
            {
                _motion.Sample(FollowSegment, Elapsed - _swingSeconds, false, _pose);
            }
            _pose.Turn = Quaternion.identity;
            float taking = Mathf.SmoothStep(0.0f, 1.0f, Elapsed / BlendInSeconds);
            float giving = Mathf.SmoothStep(0.0f, 1.0f, (TotalSeconds - Elapsed) / BlendOutSeconds);
            _locomotion.SetBakedPose(_pose, Mathf.Min(taking, giving));
        }

        /// <summary>The blow: every body in front of the body, within reach of the middle claw, cut once (edged: it bleeds).</summary>
        private void Cut()
        {
            float scale = Mathf.Max(0.2f, transform.lossyScale.y);
            Vector3 claw = _trail.ClawTip(1);
            int count = Physics.OverlapSphereNonAlloc(claw, ReachMetres * scale, Near, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Near[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || _cut.Contains(target) || !(target is Component body))
                {
                    continue;
                }
                Vector3 toTarget = body.transform.position - transform.position;
                toTarget.y = 0.0f;
                if (toTarget.sqrMagnitude > 1e-6f && Vector3.Dot(toTarget.normalized, transform.forward) < 0.1f)
                {
                    continue;   // behind or beside: the claws come down in front
                }
                _cut.Add(target);
                using (DamageContext.Cutting(true))
                {
                    if (DamageContext.Strike(target, Damage, transform.position, HitstunSeconds, DamageKind.Physical, 0.0f, claw, gameObject)
                        != HitResponse.Ignored)
                    {
                        Cuts++;
                    }
                }
            }
        }

        private void End()
        {
            IsActive = false;
            _trail.Stop();
            if (_locomotion != null && _locomotion.HoldsBakedPose(_pose))
            {
                _locomotion.SetBakedPose(null, 0.0f);
            }
        }
    }
}
