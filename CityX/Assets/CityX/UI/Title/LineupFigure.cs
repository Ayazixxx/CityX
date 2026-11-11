using CityX.Character;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// One character standing on the belt in the character list (<see cref="BeltLineup"/>): idling at the title's pace, from its own point in
    /// the clip so the row does not breathe as one, with the soles of its feet held on one spot of the belt (<see cref="PlantedFeet"/>, as the
    /// creator's body and the rooftop's).
    /// </summary>
    public sealed class LineupFigure : MonoBehaviour
    {
        /// <summary>Frames it is left to come into its idle before its feet are set down.</summary>
        private const int SettleFrames = 3;

        private Animator _animator;
        private PlantedFeet _feet;
        private float _phase;
        private int _settling;

        /// <summary>Starts it idling, <paramref name="phase"/> of the way into the clip (0 to 1).</summary>
        public void Begin(Animator animator, float phase)
        {
            _animator = animator;
            _phase = Mathf.Repeat(phase, 1.0f);
            _settling = SettleFrames;
            _feet = null;
            _animator.applyRootMotion = false;
            _animator.speed = TitleHero.IdleSpeed;
            _animator.enabled = true;
        }

        /// <summary>The feet, once set down; null before.</summary>
        public PlantedFeet Feet => _feet;

        private void LateUpdate()
        {
            if (_animator == null)
            {
                return;
            }
            if (_feet == null)
            {
                if (_settling == SettleFrames)
                {
                    // Into the idle at its own point of the clip (the first update has put it in the idle state).
                    _animator.Play(_animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, _phase);
                }
                if (--_settling > 0)
                {
                    return;
                }
                _feet = new PlantedFeet(_animator);
                _feet.SetDown();
            }
            _feet.Hold(1.0f);
        }
    }
}
