using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// DRINKING A CAN, ON THE BODY (docs/PHIZZ_CAN.md, docs/MULTIPLAYER_STATE.md): plays the drink (one full-body clip made in Blender,
    /// CityX/SourceArt/Drink/can_drink.py, 3.5 s from the click: the owner's) and times what the can does to it: the left hand lifts the tab and the
    /// hole shows, the can goes to the mouth and the heal begins (the owner's body only: 80 health over 10 s), the hand lets the empty can drop.
    /// Other players are told when a drink begins; their copy of the body drinks its copy of the can the same way, healing no one. Standing, the clip
    /// is the whole body's; moving, the legs walk on.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class CanDrinker : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "drink";

        /// <summary>The event (<see cref="AbilityEvent.Kind"/>): a drink begins.</summary>
        public const int Began = 0;

        public const string MotionPath = "Drink/CanDrinkMotion";
        public const string Clip = "can_drink";

        /// <summary>The whole drink, click to done, seconds (the owner's).</summary>
        public const float Seconds = 3.5f;

        /// <summary>
        /// The moments of the drink, seconds in: keep them in step with can_drink.py's keys. The left fingertip under the tab and flicking it up;
        /// the lid cracked (the hole shows); the can at the lips for the first sip (the heal begins); the hand opening on the empty can.
        /// </summary>
        public const float TabFromSeconds = 0.32f;
        public const float TabToSeconds = 0.44f;
        public const float HealFromSeconds = 0.75f + 30.0f / 30.0f / 1.65f;
        public const float DropSeconds = 3.22f;

        /// <summary>
        /// The left hand at the tab (the owner's photo of a can being opened): the index finger hooked under the tab's ring from above, bent a little at the
        /// knuckle and curled hard at its tip; the other three loosely curled beside it; the thumb open and turned down to brace on the rim.
        /// </summary>
        public static FingerAmounts TabHand => new FingerAmounts(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, TabThumbDownDegrees)
        {
            Authored = 1.0f,
            IndexRootDegrees = 20.0f,
            IndexTipDegrees = 65.0f,
            FingerRootDegrees = 45.0f,
            FingerTipDegrees = 35.0f,
            ThumbRootDegrees = -10.0f,
            ThumbTipDegrees = 0.0f,
        };

        public const float TabThumbDownDegrees = 25.0f;

        /// <summary>When the left hand takes that shape and lets it go, seconds in: on its way to the tab, and on its way back down.</summary>
        public const float TabHandFromSeconds = 0.12f;
        public const float TabHandShapedSeconds = 0.26f;
        public const float TabHandLetGoSeconds = 0.56f;
        public const float TabHandDoneSeconds = 0.75f;

        /// <summary>How fast the empty can leaves the hand, m/s: it is let go, not thrown.</summary>
        public const float DropSpeed = 0.6f;

        public const float EaseInSeconds = 0.1f;
        public const float EaseOutSeconds = 0.2f;

        /// <summary>A body going faster than this, m/s, is moving: the drink is from the waist up and it walks on.</summary>
        public const float StandingSpeed = 0.6f;
        public const float LegsSeconds = 0.2f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        /// <summary>Raised on the owner's body when the heal begins (tests).</summary>
        public event System.Action Healed;

        private PlayerLocomotion _locomotion;
        private BakedMotion _motion;
        private BakedPose _pose;
        private bool _looked;
        private PhizzCan _can;
        private bool _harmless;
        private float _elapsed;
        private float _weight;
        private float _legs;
        private bool _healed;
        private bool _dropped;
        private bool _holdsBody;

        /// <summary>A drink is going.</summary>
        public bool Drinking => _can != null;

        /// <summary>How far into the drink, seconds (tests).</summary>
        public float Elapsed => _elapsed;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        private void OnDisable()
        {
            LetGoOfTheBody();
        }

        private bool Standing => _locomotion == null || _locomotion.CurrentSpeed < StandingSpeed;

        private bool HasClip()
        {
            if (!_looked)
            {
                _looked = true;
                _motion = BakedMotion.Load(MotionPath);
                if (_motion != null && _motion.HasSegment(Clip))
                {
                    _pose = _motion.NewPose();
                }
                else
                {
                    _motion = null;
                }
            }
            return _motion != null;
        }

        /// <summary>This body drinks <paramref name="can"/> (its owner's click). False if a drink is already going or it is dead.</summary>
        public bool Begin(PhizzCan can)
        {
            var vitals = GetComponent<PlayerVitals>();
            if (can == null || Drinking || (vitals != null && vitals.IsDead))
            {
                return false;
            }
            Play(can, false);
            Fired?.Invoke(new AbilityEvent(Began, transform.position, Vector3.zero));
            return true;
        }

        /// <summary>The can is put away or lost mid-drink: everything stops.</summary>
        public void Stop(PhizzCan can)
        {
            if (can != null && ReferenceEquals(can, _can))
            {
                _can = null;
            }
        }

        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Kind != Began || Drinking)
            {
                return;
            }
            PhizzCan can = GetComponentInChildren<PhizzCan>();
            if (can != null)
            {
                Play(can, true);
            }
        }

        private void Play(PhizzCan can, bool harmless)
        {
            _can = can;
            _harmless = harmless;
            _elapsed = 0.0f;
            _healed = false;
            _dropped = false;
            if (_weight <= 0.0f)
            {
                _legs = Standing ? 1.0f : 0.0f;
            }
            can.SetOpened(0.0f);
            enabled = true;
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>Advances by <paramref name="deltaTime"/>: the can's moments, and the body's pose. Public so a test can step it.</summary>
        public void Step(float deltaTime)
        {
            if (_can == null)
            {
                Ease(deltaTime);
                return;
            }
            _elapsed += deltaTime;
            _can.SetOpened(Mathf.InverseLerp(TabFromSeconds, TabToSeconds, _elapsed));
            float shaped = Mathf.Min(Mathf.InverseLerp(TabHandFromSeconds, TabHandShapedSeconds, _elapsed), 1.0f - Mathf.InverseLerp(TabHandLetGoSeconds, TabHandDoneSeconds, _elapsed));
            if (shaped > 0.0f && _locomotion != null)
            {
                _locomotion.SetLeftFingers(TabHand, Mathf.SmoothStep(0.0f, 1.0f, shaped));
            }
            if (!_healed && _elapsed >= HealFromSeconds)
            {
                _healed = true;
                if (!_harmless)
                {
                    GetComponent<PlayerVitals>()?.AddHealOverTime(PhizzCan.HealAmount, PhizzCan.HealSeconds);
                    Healed?.Invoke();
                }
            }
            if (!_dropped && _elapsed >= DropSeconds)
            {
                _dropped = true;
                _can.DropEmpty(transform.forward * DropSpeed + Vector3.down * DropSpeed);
            }
            if (_elapsed >= Seconds)
            {
                PhizzCan done = _can;
                _can = null;
                if (!_harmless)
                {
                    done.Finished();
                }
            }
            if (!HasClip())
            {
                return;
            }
            _motion.Sample(Clip, Mathf.Min(_elapsed, Seconds), false, _pose);
            _legs = Mathf.MoveTowards(_legs, Standing ? 1.0f : 0.0f, deltaTime / LegsSeconds);
            _pose.LegsWeight = Mathf.SmoothStep(0.0f, 1.0f, _legs);
            _weight = Mathf.MoveTowards(_weight, 1.0f, deltaTime / EaseInSeconds);
            if (_locomotion != null)
            {
                _holdsBody = true;
                _locomotion.SetBakedPose(_pose, Mathf.SmoothStep(0.0f, 1.0f, _weight));
            }
        }

        /// <summary>No drink: the clip's hold on the body eases off.</summary>
        private void Ease(float deltaTime)
        {
            _weight = Mathf.MoveTowards(_weight, 0.0f, deltaTime / EaseOutSeconds);
            if (_weight <= 0.0f || _pose == null)
            {
                LetGoOfTheBody();
                enabled = false;
            }
            else if (_locomotion != null)
            {
                _locomotion.SetBakedPose(_pose, Mathf.SmoothStep(0.0f, 1.0f, _weight));
            }
        }

        private void LetGoOfTheBody()
        {
            if (_holdsBody && _locomotion != null && _locomotion.HoldsBakedPose(_pose))
            {
                _locomotion.SetBakedPose(null, 0.0f);
            }
            _holdsBody = false;
        }
    }
}
