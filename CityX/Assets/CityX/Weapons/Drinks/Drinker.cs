using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// DRINKING, ON THE BODY (docs/PHIZZ_CAN.md, docs/MULTIPLAYER_STATE.md): plays the drink in the hand (<see cref="HeldDrink"/>: a can of PHIZZ, a full
    /// PHIZZ bottle; one full-body clip each, made in Blender, CityX/SourceArt/Drink/can_drink.py, 3.5 s from the click: the owner's) and times what the
    /// drink does to it: the left hand opens it, it goes to the mouth and the heal begins (the owner's body only: what the drink heals), the bottle's
    /// liquid goes down as it is drunk, the hand lets an empty can fall. Other players are told when a drink begins; their copy of the body drinks its
    /// copy of the drink the same way, healing no one. Standing, the clip
    /// is the whole body's; moving, the legs walk on.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class Drinker : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "drink";

        /// <summary>The event (<see cref="AbilityEvent.Kind"/>): a drink begins.</summary>
        public const int Began = 0;

        public const string MotionPath = "Drink/DrinkMotion";

        /// <summary>The whole drink, click to done, seconds (the owner's).</summary>
        public const float Seconds = 3.5f;

        /// <summary>
        /// The moments of the drink, seconds in: keep them in step with can_drink.py's keys. The left fingertip under the tab and flicking it up;
        /// the lid cracked (the hole shows); the drink at the lips for the first sip (the heal begins); the hand opening on an empty can.
        /// </summary>
        public const float TabFromSeconds = 0.32f;
        public const float TabToSeconds = 0.44f;
        public const float HealFromSeconds = 0.75f + 30.0f / 30.0f / 1.65f;

        /// <summary>When the drink is all drunk, seconds in: the end of the gulp (the recording's frame 140). The bottle's liquid goes down from the first sip to here.</summary>
        public const float DrunkBySeconds = 0.75f + 85.0f / 30.0f / 1.65f;
        public const float DropSeconds = 3.22f;

        /// <summary>
        /// The left hand at the tab (owner: "the hand his turned knuckles upward and the finger is pinching the tab"): the index finger and the thumb pinch it,
        /// the other fingers tucked, as the slingshot's pouch is pinched. The clip turns the hand knuckles up and puts the pinch on the tab (can_drink.py).
        /// </summary>
        public static FingerAmounts TabHand => PlayerLocomotion.SlingshotPinch;

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
        private HeldDrink _drink;
        private bool _harmless;
        private float _elapsed;
        private float _weight;
        private float _legs;
        private bool _healed;
        private bool _dropped;
        private bool _holdsBody;

        /// <summary>A drink is going.</summary>
        public bool Drinking => _drink != null;

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

        private bool HasClip(string clip)
        {
            if (!_looked)
            {
                _looked = true;
                _motion = BakedMotion.Load(MotionPath);
                if (_motion != null)
                {
                    _pose = _motion.NewPose();
                }
                else
                {
                    _motion = null;
                }
            }
            return _motion != null && _motion.HasSegment(clip);
        }

        /// <summary>This body drinks <paramref name="drink"/> (its owner's click). False if a drink is already going or it is dead.</summary>
        public bool Begin(HeldDrink drink)
        {
            var vitals = GetComponent<PlayerVitals>();
            if (drink == null || Drinking || (vitals != null && vitals.IsDead))
            {
                return false;
            }
            Play(drink, false);
            Fired?.Invoke(new AbilityEvent(Began, transform.position, Vector3.zero));
            return true;
        }

        /// <summary>The drink is put away or lost mid-drink: everything stops.</summary>
        public void Stop(HeldDrink drink)
        {
            if (drink != null && ReferenceEquals(drink, _drink))
            {
                _drink = null;
            }
        }

        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Kind != Began || Drinking)
            {
                return;
            }
            HeldDrink drink = GetComponentInChildren<HeldDrink>();
            if (drink != null)
            {
                Play(drink, true);
            }
        }

        private void Play(HeldDrink drink, bool harmless)
        {
            _drink = drink;
            _harmless = harmless;
            _elapsed = 0.0f;
            _healed = false;
            _dropped = false;
            if (_weight <= 0.0f)
            {
                _legs = Standing ? 1.0f : 0.0f;
            }
            drink.SetOpened(0.0f);
            drink.SetDrunk(0.0f);
            enabled = true;
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>Advances by <paramref name="deltaTime"/>: the can's moments, and the body's pose. Public so a test can step it.</summary>
        public void Step(float deltaTime)
        {
            if (_drink == null)
            {
                Ease(deltaTime);
                return;
            }
            _elapsed += deltaTime;
            HeldDrink drink = _drink;
            drink.SetOpened(Mathf.InverseLerp(TabFromSeconds, TabToSeconds, _elapsed));
            drink.SetDrunk(Mathf.InverseLerp(HealFromSeconds, DrunkBySeconds, _elapsed));
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
                    GetComponent<PlayerVitals>()?.AddHealOverTime(drink.HealAmount, drink.HealSeconds);
                    Healed?.Invoke();
                }
            }
            if (!_dropped && _elapsed >= DropSeconds)
            {
                _dropped = true;
                drink.LetGo(transform.forward * DropSpeed + Vector3.down * DropSpeed);
            }
            if (_elapsed >= Seconds)
            {
                _drink = null;
                if (_harmless)
                {
                    drink.FinishedCopy();
                }
                else
                {
                    drink.Finished();
                }
            }
            if (!HasClip(drink.Clip))
            {
                return;
            }
            _motion.Sample(drink.Clip, Mathf.Min(_elapsed, Seconds), false, _pose);
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
