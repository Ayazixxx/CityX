using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Plays the hit reactions (<see cref="HitReactions"/>) on a body, owner or network copy alike: <see cref="PlayerAnimationDriver"/> calls
    /// <see cref="Begin"/> when the body is hit (here, or replayed from the network), this advances the recoil and hands each frame to
    /// <see cref="PlayerLocomotion"/>, whose LateUpdate throws the torso and head the way the frame says, on top of whatever else the body is doing.
    /// </summary>
    public sealed class FlinchPoseDriver : MonoBehaviour
    {
        private PlayerLocomotion _locomotion;
        private HitDirection _direction;
        private bool _stagger;
        private bool _playing;
        private float _elapsed;
        private float _duration = 0.3f;

        public bool IsPlaying => _playing;

        public HitDirection Direction => _direction;

        public bool IsStagger => _stagger;

        public float Progress => _duration > 0.0f ? Mathf.Clamp01(_elapsed / _duration) : 0.0f;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        private void OnDisable()
        {
            if (_locomotion != null)
            {
                _locomotion.ClearFlinchPose();
            }
        }

        /// <summary>A blow lands: the recoil starts from the top (a second blow restarts it).</summary>
        public void Begin(HitDirection direction, bool stagger)
        {
            _direction = direction;
            _stagger = stagger;
            _playing = true;
            _elapsed = 0.0f;
            _duration = HitReactions.Seconds(stagger);
            enabled = true;
        }

        public void Cancel()
        {
            _playing = false;
            _locomotion?.ClearFlinchPose();
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>Advances by <paramref name="deltaTime"/> and gives the locomotion this frame's recoil. Public so a test can step it without frames.</summary>
        public void Step(float deltaTime)
        {
            if (!_playing)
            {
                return;
            }
            // The body is held dead still through the instant of impact (the freeze); the recoil is thrown out of it.
            if (_locomotion != null && _locomotion.IsImpactFrozen)
            {
                return;
            }
            _elapsed += deltaTime;
            if (_elapsed >= _duration)
            {
                _playing = false;
                _locomotion?.ClearFlinchPose();
                enabled = false;
                return;
            }
            _locomotion?.SetFlinchPose(HitReactions.Evaluate(_direction, _stagger, Progress));
        }
    }
}
