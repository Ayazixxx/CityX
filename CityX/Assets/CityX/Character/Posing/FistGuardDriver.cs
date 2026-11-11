using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Plays the bare-handed guard (<see cref="FistGuard"/>) on a body, owner or network copy alike: <see cref="PlayerAnimationDriver"/>
    /// calls <see cref="Raise"/>, <see cref="Lower"/>, <see cref="Hit"/> and <see cref="Parry"/> by the names it is told. The clips are on
    /// the upper body only (BakedPose.UpperBodyOnly), handed to <see cref="PlayerLocomotion"/> to put on after the Animator, so the legs
    /// walk on under the guard; standing still, the fists' ready stance is held under it (FistPoseDriver.KeepReady).
    /// </summary>
    [DefaultExecutionOrder(10)]   // after FistPoseDriver: a punch's pose fading out lets go of the body slot, and the guard takes it the same frame
    public sealed class FistGuardDriver : MonoBehaviour
    {
        /// <summary>How long one clip takes to take over from another (a blow landing as the guard is still coming up), seconds.</summary>
        public const float CrossfadeSeconds = 0.06f;

        private PlayerLocomotion _locomotion;
        private FistPoseDriver _fist;
        private BakedMotion _motion;
        private BakedPose _pose;
        private BakedPose _crossfadeFrom;
        private float _crossfadeRemaining;
        private string _clip = FistGuard.HoldClip;
        private float _elapsed;
        private float _weight;
        private bool _up;
        private bool _holdsBody;

        /// <summary>The guard is held up.</summary>
        public bool IsUp => _up;

        /// <summary>0 to 1: how much of the upper body the guard's pose has.</summary>
        public float Weight => _weight;

        /// <summary>The clip playing (FistGuard's), for tests.</summary>
        public string Clip => _clip;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            _fist = GetComponent<FistPoseDriver>();
        }

        private void OnDisable()
        {
            Release();
        }

        public void Raise()
        {
            if (!_up)
            {
                _up = true;
                Play(FistGuard.RaiseClip);
            }
            enabled = true;
        }

        public void Lower()
        {
            _up = false;
        }

        /// <summary>A blow soaked on the guard: the forearms knocked back, and up again.</summary>
        public void Hit()
        {
            if (_up)
            {
                Play(FistGuard.HitClip);
            }
        }

        /// <summary>A blow caught in the guard's first instant: swatted aside.</summary>
        public void Parry()
        {
            if (_up)
            {
                Play(FistGuard.ParryClip);
            }
        }

        /// <summary>Drops the guard's pose at once (a punch, a weapon's pose, a clip that takes the arms).</summary>
        public void Cancel()
        {
            _up = false;
            Release();
        }

        private void Play(string clip)
        {
            if (_motion == null)
            {
                _motion = FistGuard.Motion;
                if (_motion == null)
                {
                    return;
                }
                _pose = _motion.NewPose();
                _crossfadeFrom = _motion.NewPose();
                _pose.UpperBodyOnly = true;
            }
            if (_weight > 0.0f)
            {
                _crossfadeFrom.CopyBodyFrom(_pose);
                _crossfadeRemaining = CrossfadeSeconds;
            }
            _clip = clip;
            _elapsed = 0.0f;
        }

        private void Release()
        {
            _weight = 0.0f;
            _crossfadeRemaining = 0.0f;
            if (_holdsBody && _locomotion != null)
            {
                _locomotion.SetBakedPose(null, 0.0f);
            }
            _holdsBody = false;
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>Advances by <paramref name="deltaTime"/> and gives the locomotion this frame's pose. Public so a test can step it without frames.</summary>
        public void Step(float deltaTime)
        {
            _weight = Mathf.MoveTowards(_weight, _up ? 1.0f : 0.0f, deltaTime / (_up ? FistGuard.EngageSeconds : FistGuard.LowerSeconds));
            if (_pose == null || _locomotion == null || (!_up && _weight <= 0.0f))
            {
                Release();
                enabled = false;
                return;
            }
            _elapsed += deltaTime;
            bool held = _clip == FistGuard.HoldClip;
            // A clip's last frame is the guard held: the hold takes over from it.
            float last = (_motion.SegmentFrames(_clip) - 1) / (float)_motion.FramesPerSecond;
            if (!held && _elapsed >= last)
            {
                _clip = FistGuard.HoldClip;
                _elapsed -= last;
                held = true;
            }
            _motion.Sample(_clip, held ? _elapsed : Mathf.Min(_elapsed, last), held, _pose);
            if (_crossfadeRemaining > 0.0f)
            {
                _crossfadeRemaining -= deltaTime;
                _pose.MixBodyFrom(_crossfadeFrom, Mathf.SmoothStep(0.0f, 1.0f, 1.0f - Mathf.Max(0.0f, _crossfadeRemaining) / CrossfadeSeconds));
            }
            _holdsBody = true;
            _locomotion.SetBakedPose(_pose, Mathf.SmoothStep(0.0f, 1.0f, _weight));
            if (_up && _fist != null)
            {
                _fist.KeepReady();
            }
        }
    }
}
