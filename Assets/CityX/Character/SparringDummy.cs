using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A thing that stands still and takes punches, ported in spirit from the
    /// Godot build's CPU sparring dummy.
    ///
    /// It exists because combat cannot be judged against nothing. Reach, arc,
    /// how many hits a chain takes to fell something, whether the finisher
    /// reads as heavier -- none of that can be felt without a target, and none
    /// of it needs an AI to be felt. Adding a brain first would mean tuning the
    /// swing and the chasing at the same time and knowing which was wrong.
    ///
    /// Keeps a hit count as well as health, because "did the third punch of the
    /// chain land" is the question being asked far more often than "how much
    /// health is left".
    /// </summary>
    public sealed class SparringDummy : MonoBehaviour, IDamageable
    {
        public float MaxHealth = 100.0f;

        [Tooltip("Seconds spent flinching before it stands back up. Cosmetic; it never fights back.")]
        public float FlinchSeconds = 0.3f;

        public float Health { get; private set; }

        public int TimesHit { get; private set; }

        public float TotalDamageTaken { get; private set; }

        /// <summary>Where the last blow came from, for knockback and effects.</summary>
        public Vector3 LastHitFrom { get; private set; }

        public float LastHitstun { get; private set; }

        public bool IsAlive => Health > 0.0f;

        private float _flinchRemaining;

        public bool IsFlinching => _flinchRemaining > 0.0f;

        public event System.Action Felled;

        private void Awake()
        {
            Health = MaxHealth;
        }

        /// <summary>Puts it back up. Handy for repeated tests and for a dev command.</summary>
        public void Reset()
        {
            Health = MaxHealth;
            TimesHit = 0;
            TotalDamageTaken = 0.0f;
            _flinchRemaining = 0.0f;
            LastHitstun = 0.0f;
        }

        HitResponse IDamageable.ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
        {
            if (!IsAlive)
            {
                return HitResponse.Ignored;
            }

            Health = Mathf.Max(0.0f, Health - amount);
            TimesHit++;
            TotalDamageTaken += amount;
            LastHitFrom = fromPosition;
            LastHitstun = hitstunSeconds;
            _flinchRemaining = FlinchSeconds;

            if (Health <= 0.0f)
            {
                Felled?.Invoke();
            }

            // A dummy stands there and takes it. That is what makes it useful
            // for judging reach and pacing without a second system in the way.
            return HitResponse.Landed;
        }

        private void Update()
        {
            if (_flinchRemaining > 0.0f)
            {
                _flinchRemaining = Mathf.Max(0.0f, _flinchRemaining - Time.deltaTime);
            }
        }
    }
}
