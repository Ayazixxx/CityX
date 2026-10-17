using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The pure part of the C-menu's camera cut: one static front-on framing
    /// of the player, computed from wherever they are currently facing.
    ///
    /// REUSES THE CREATOR'S OWN TUNED NUMBERS. CharacterCreatorBuilder
    /// positions its camera at stage-local (0, 1.6, 4) with rotation
    /// (8, 180, 0) -- 1.6m up, 4m in front, pitched 8 degrees down, looking
    /// back at the body. That framing is already proven to look right on
    /// this exact rig; the only thing that changes here is that "forward"
    /// is read from the live player's current facing instead of a fixed
    /// stage orientation, since the player can be facing any direction when
    /// C is pressed.
    /// </summary>
    public static class PlayerMenuCameraMath
    {
        public const float Height = 1.6f;
        public const float Distance = 4.0f;
        public const float PitchDegrees = 8.0f;

        public static Vector3 CameraPosition(Vector3 playerPosition, Vector3 playerForward)
        {
            return playerPosition + Vector3.up * Height + playerForward.normalized * Distance;
        }

        /// <summary>
        /// Looking back along -playerForward, with the creator's own fixed
        /// downward pitch layered on top.
        /// </summary>
        public static Quaternion CameraRotation(Vector3 playerForward)
        {
            float yaw = Quaternion.LookRotation(-playerForward, Vector3.up).eulerAngles.y;
            return Quaternion.Euler(PitchDegrees, yaw, 0.0f);
        }
    }

    /// <summary>
    /// Eases the scene's single shared camera into a front-on shot of the
    /// player for as long as the C-menu is open, then hands it back. Ported
    /// from ClaimCutscene's own "drive the existing camera rather than spawn
    /// a second one" shape, including its public Tick(deltaTime) taking its
    /// own delta rather than reading Time.deltaTime -- same reasoning: a
    /// test can step straight to the end of the ease without waiting on real
    /// wall-clock seconds.
    /// </summary>
    public sealed class PlayerMenuCamera : MonoBehaviour
    {
        /// <summary>
        /// How long the cut takes to arrive. Short enough to read as a snap
        /// of attention rather than a lingering pan -- this is a menu
        /// opening, not a cutscene -- long enough that the camera visibly
        /// swings into the front-on framing instead of popping there.
        /// </summary>
        public const float EaseSeconds = 0.35f;

        private Transform _camera;
        private Transform _player;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _elapsed;
        private bool _easing;

        /// <summary>
        /// Starts the ease from wherever the camera already is -- normally
        /// wherever ThirdPersonCamera's own last LateUpdate left it, since
        /// PlayerMenuView disables that component in the same call that
        /// reaches here. The caller is responsible for disabling
        /// PlayerInputReader and ThirdPersonCamera first and re-enabling
        /// them on close; this component only ever touches the camera
        /// transform.
        /// </summary>
        public void Begin(Transform cameraTransform, Transform player)
        {
            _camera = cameraTransform;
            _player = player;
            if (_camera == null || _player == null)
            {
                _easing = false;
                return;
            }

            Vector3 forward = _player.forward;
            _startPosition = _camera.position;
            _startRotation = _camera.rotation;
            _targetPosition = PlayerMenuCameraMath.CameraPosition(_player.position, forward);
            _targetRotation = PlayerMenuCameraMath.CameraRotation(forward);
            _elapsed = 0.0f;
            _easing = true;
        }

        /// <summary>
        /// Halts the ease immediately, mid-flight if need be. PlayerMenuView
        /// calls this on Close() -- without it, a menu closed before the
        /// ease finishes would leave this component still writing to the
        /// camera transform the same frame ThirdPersonCamera resumes doing
        /// the same thing, and the two would fight over the result.
        /// </summary>
        public void Stop()
        {
            _easing = false;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>The ease advanced by one step. See the class doc comment for why this takes its own deltaTime.</summary>
        public void Tick(float deltaTime)
        {
            if (!_easing || _camera == null)
            {
                return;
            }

            _elapsed += deltaTime;
            float t = EaseSeconds > 0.0f ? Mathf.Clamp01(_elapsed / EaseSeconds) : 1.0f;
            float eased = ClaimCutsceneMath.Ease(t);
            _camera.position = Vector3.Lerp(_startPosition, _targetPosition, eased);
            _camera.rotation = Quaternion.Slerp(_startRotation, _targetRotation, eased);

            if (t >= 1.0f)
            {
                _easing = false;
            }
        }
    }
}
