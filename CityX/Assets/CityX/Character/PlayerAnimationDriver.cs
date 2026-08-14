using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Plays the swing animation the attack chain asked for.
    ///
    /// A LISTENER RATHER THAN A SECOND STATE MACHINE. The chain already knows
    /// which swing is playing and when; expressing that again as animator
    /// triggers and conditions would create two state machines that have to
    /// agree about where in a combo you are. They would not, and the symptom
    /// would be a punch that lands while the character plays a different swing
    /// -- exactly the class of drift the Godot build had when the same state
    /// lived in both character.gd and player.gd.
    ///
    /// So the animator gets told, by name, and has no opinion.
    /// </summary>
    [RequireComponent(typeof(MeleeAttacker))]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        [Tooltip("Cross-fade time into a swing. Short -- a punch should start now, not ease in.")]
        public float SwingBlendSeconds = 0.06f;

        private Animator _animator;
        private MeleeAttacker _melee;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _melee = GetComponent<MeleeAttacker>();
        }

        private void OnEnable()
        {
            if (_melee != null)
            {
                _melee.SwingStarted += OnSwingStarted;
                _melee.BlockThrown += OnBlockThrown;
            }
        }

        private void OnDisable()
        {
            if (_melee != null)
            {
                _melee.SwingStarted -= OnSwingStarted;
                _melee.BlockThrown -= OnBlockThrown;
            }
        }

        /// <summary>
        /// The block pose. Told by name for the same reason the swings are: the
        /// window it corresponds to is the vitals' state, and expressing it a
        /// second time as an animator condition is how a body ends up guarding
        /// while the damage says it was not.
        /// </summary>
        private void OnBlockThrown()
        {
            if (_animator != null)
            {
                _animator.CrossFadeInFixedTime("Block", SwingBlendSeconds, 0);
            }
        }

        private void OnSwingStarted(ComboStage stage)
        {
            if (_animator == null || string.IsNullOrEmpty(stage.Animation))
            {
                return;
            }

            // CrossFadeInFixedTime, not CrossFade: the normalised version scales
            // the blend by the CURRENT state's length, so the same call blends
            // over a different real duration depending on whether you were
            // idling or sprinting when you threw the punch.
            _animator.CrossFadeInFixedTime(stage.Animation, SwingBlendSeconds, 0);
        }
    }
}
