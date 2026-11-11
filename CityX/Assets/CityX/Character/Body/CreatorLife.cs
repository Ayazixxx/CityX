using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The body being made, alive or holding still (docs/BELT_INTRO.md; owner, 2026-11-10: "i want him to be 'live' during the
    /// customization ... have a t pose mode where he actually lifts his arms into the t pose and moves into place instead of just
    /// snapping into position"). Alive, its Animator plays (it stands and idles) and it cannot be painted: the brush and the click target
    /// are the body as it stood when the creator woke (CharacterCreator.Initialise). Holding still, it is back in exactly that pose, arms
    /// out, and is painted. Going from one to the other every bone is eased across, never jumped.
    /// </summary>
    public sealed class CreatorLife : MonoBehaviour
    {
        /// <summary>How long the arms take to come up, or to go back down, seconds.</summary>
        public const float LiftSeconds = 0.55f;

        /// <summary>
        /// How fast the idle plays, against its own speed: slow, and stopped while a stroke is drawn on the body (owner, 2026-11-10: "make the
        /// idle animation on the character creator play at 10% of the speed it is now and make it freeze when you are holding click drawing
        /// on it").
        /// </summary>
        public const float IdleSpeed = 0.1f;

        public CharacterCreator Creator;

        public Animator Animator;

        private Transform[] _bones;
        private Vector3[] _stillPlaces;
        private Quaternion[] _stillTurns;
        private Vector3[] _fromPlaces;
        private Quaternion[] _fromTurns;
        private float _eased = 1.0f;

        /// <summary>Standing and idling (or on its way to it); otherwise holding still to be painted (or on its way).</summary>
        public bool Alive { get; private set; }

        /// <summary>In the pose it is painted in, every bone arrived.</summary>
        public bool Still => !Alive && _eased >= 1.0f;

        /// <summary>Its soles, stuck where they stand while it is alive; let go with the body at the belt's end (<see cref="LetGo"/>).</summary>
        public PlantedFeet Feet { get; private set; }

        private void Awake()
        {
            Remember();
        }

        /// <summary>Takes the pose the body is in now as the one it holds still in. Awake does, before anything has moved it.</summary>
        private void Remember()
        {
            if (_bones != null || Animator == null)
            {
                return;
            }
            Feet = new PlantedFeet(Animator);
            _bones = Animator.GetComponentsInChildren<Transform>(true);
            _stillPlaces = new Vector3[_bones.Length];
            _stillTurns = new Quaternion[_bones.Length];
            _fromPlaces = new Vector3[_bones.Length];
            _fromTurns = new Quaternion[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            {
                _stillPlaces[i] = _bones[i].localPosition;
                _stillTurns[i] = _bones[i].localRotation;
            }
        }

        /// <summary>Lets it stand and idle: the arms come down over <see cref="LiftSeconds"/>, or <paramref name="atOnce"/> where nobody sees.</summary>
        public void ComeAlive(bool atOnce = false)
        {
            Remember();
            if (Alive || _bones == null)
            {
                return;
            }
            Alive = true;
            StartFromHere();
            if (atOnce)
            {
                _eased = 1.0f;
            }
            Animator.applyRootMotion = false;
            Animator.enabled = true;
            if (Creator != null)
            {
                Creator.HoldingStill = false;
            }
        }

        /// <summary>Has it hold still to be painted: the arms come up over <see cref="LiftSeconds"/>, and then it can be.</summary>
        public void HoldStill()
        {
            Remember();
            if (!Alive || _bones == null)
            {
                return;
            }
            Alive = false;
            StartFromHere();
            Animator.enabled = false;
        }

        /// <summary>Holding still at once, with no easing: the creator as it opens when no belt brought the body in.</summary>
        public void SnapStill()
        {
            Remember();
            if (_bones == null)
            {
                return;
            }
            Alive = false;
            _eased = 1.0f;
            Animator.enabled = false;
            for (int i = 0; i < _bones.Length; i++)
            {
                _bones[i].SetLocalPositionAndRotation(_stillPlaces[i], _stillTurns[i]);
            }
            if (Creator != null)
            {
                Creator.HoldingStill = true;
            }
        }

        /// <summary>The body is somebody else's from here (the ragdoll's, at the belt's end): nothing more is eased.</summary>
        public void LetGo()
        {
            if (Animator != null)
            {
                Animator.speed = 1.0f;
            }
            _eased = 1.0f;
            enabled = false;
        }

        private void StartFromHere()
        {
            _eased = 0.0f;
            for (int i = 0; i < _bones.Length; i++)
            {
                _fromPlaces[i] = _bones[i].localPosition;
                _fromTurns[i] = _bones[i].localRotation;
            }
        }

        /// <summary>Before the Animator: the idle's pace.</summary>
        private void Update()
        {
            if (Alive && Animator != null)
            {
                Animator.speed = Creator != null && Creator.IsPainting ? 0.0f : IdleSpeed;
            }
        }

        /// <summary>After the Animator: each bone eased from where the change found it to where it is going.</summary>
        private void LateUpdate()
        {
            if (_bones == null)
            {
                return;
            }
            if (Alive && !Feet.IsDown)
            {
                Feet.SetDown();   // not before now: this is the first frame the Animator has it in the idle
            }
            float share = 1.0f;
            if (_eased < 1.0f)
            {
                _eased = Mathf.Min(1.0f, _eased + Time.deltaTime / LiftSeconds);
                share = Mathf.SmoothStep(0.0f, 1.0f, _eased);
                for (int i = 0; i < _bones.Length; i++)
                {
                    // Alive, the Animator has just put the bone where the idle has it; still, it goes to where it is painted.
                    Vector3 place = Alive ? _bones[i].localPosition : _stillPlaces[i];
                    Quaternion turn = Alive ? _bones[i].localRotation : _stillTurns[i];
                    _bones[i].SetLocalPositionAndRotation(Vector3.Lerp(_fromPlaces[i], place, share), Quaternion.Slerp(_fromTurns[i], turn, share));
                }
                if (_eased >= 1.0f && !Alive && Creator != null)
                {
                    Creator.HoldingStill = true;
                }
            }
            if (Alive)
            {
                Feet.Hold(share);   // holding still it is in the exact pose it is painted in, feet and all
            }
        }
    }
}
