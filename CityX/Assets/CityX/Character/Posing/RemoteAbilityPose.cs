using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Holds the raised-hand cast pose on a copy of a body that someone ELSE owns. On the owner PlayerInputReader sets the pose every frame
    /// from the caster; a remote copy has no input reader running, so whatever replays a spell there asks this to do it for a while: full
    /// strength for a hold, then easing out. Added to the remote body the first time it is needed and idle (disabled) between spells.
    /// </summary>
    public sealed class RemoteAbilityPose : MonoBehaviour
    {
        private PlayerLocomotion _locomotion;
        private float _remaining;
        private float _ease = 0.4f;

        /// <summary>How strongly the pose is held right now, 0 to 1.</summary>
        public float Fraction { get; private set; }

        /// <summary>Raises the hand on <paramref name="body"/> for <paramref name="holdSeconds"/> and then eases it down over <paramref name="easeSeconds"/>.</summary>
        public static void Show(GameObject body, float holdSeconds, float easeSeconds)
        {
            if (body == null)
            {
                return;
            }
            var pose = body.GetComponent<RemoteAbilityPose>();
            if (pose == null)
            {
                pose = body.AddComponent<RemoteAbilityPose>();
            }
            pose._locomotion = body.GetComponent<PlayerLocomotion>();
            pose._ease = Mathf.Max(0.01f, easeSeconds);
            pose._remaining = Mathf.Max(pose._remaining, Mathf.Max(0.0f, holdSeconds) + pose._ease);
            pose.enabled = true;
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>Public so a test can step it without frames.</summary>
        public void Step(float deltaTime)
        {
            if (_remaining <= 0.0f)
            {
                Fraction = 0.0f;
                enabled = false;
                return;
            }
            _remaining -= deltaTime;
            Fraction = _remaining > _ease ? 1.0f : Mathf.Clamp01(_remaining / _ease);
            if (_locomotion != null)
            {
                _locomotion.SetCastPose(Fraction);
            }
            if (_remaining <= 0.0f)
            {
                Fraction = 0.0f;
                if (_locomotion != null)
                {
                    _locomotion.SetCastPose(0.0f);
                }
                enabled = false;
            }
        }
    }
}
