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
            if (_animator == null)
            {
                return;
            }
            // WHICH BLOCK POSE, DECIDED HERE, the same "told by name" split
            // this whole class already uses -- MeleeAttacker.PlayBlock
            // doesn't know or care what a block looks like, only that one
            // was thrown; this is the one place that already asks
            // EquippedWeapon anything for animation purposes.
            string moveset = _melee != null ? _melee.EquippedWeapon?.MovesetId : null;
            string blockState = moveset == ComboChain.Sword ? "SwordBlock" : "Block";
            _animator.CrossFadeInFixedTime(blockState, SwingBlendSeconds, 0);
        }

        private void OnSwingStarted(ComboStage stage)
        {
            if (_animator == null || string.IsNullOrEmpty(stage.Animation))
            {
                return;
            }

            // EXPLICIT BUG REPORT, root-caused directly with the sword spin
            // attack's own diagnostic render: a real swing thrown before the
            // Animator has ever actually evaluated a single frame crossfades
            // FROM the raw imported bind pose (a T-pose) rather than from a
            // real idle pose -- confirmed directly, rendered, on the very
            // first frame a fresh spin attack was triggered. Same fix, same
            // reasoning GrappleSwing.Attach/DevConsoleView.Open already
            // apply for the identical hazard: a forced zero-delta Update
            // guarantees a real evaluation has happened before anything
            // else touches this Animator, so the blend below always starts
            // from a genuine pose rather than whatever the raw import left
            // bones at.
            _animator.Update(0.0f);

            // CrossFadeInFixedTime, not CrossFade: the normalised version scales
            // the blend by the CURRENT state's length, so the same call blends
            // over a different real duration depending on whether you were
            // idling or sprinting when you threw the punch.
            _animator.CrossFadeInFixedTime(stage.Animation, SwingBlendSeconds, 0);
        }
    }
}
