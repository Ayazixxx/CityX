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
    /// Cuts the scene's single shared camera to a front-on shot of the
    /// player for as long as the C-menu is open, then hands it back.
    /// Ported conceptually from ClaimCutscene's own "drive the existing
    /// camera rather than spawn a second one" pattern, but static rather
    /// than animated -- the menu wants an immediate cut, not an orbit, so
    /// there is no Tick/ease loop here, just one positioning call.
    /// </summary>
    public sealed class PlayerMenuCamera : MonoBehaviour
    {
        private Transform _camera;
        private Transform _player;

        /// <summary>
        /// Cuts the camera to the front-on shot immediately. The caller
        /// (PlayerMenuView) is responsible for disabling PlayerInputReader
        /// and ThirdPersonCamera first and re-enabling them on close --
        /// this component only ever touches the camera transform.
        /// </summary>
        public void Begin(Transform cameraTransform, Transform player)
        {
            _camera = cameraTransform;
            _player = player;
            Place();
        }

        private void Place()
        {
            if (_camera == null || _player == null)
            {
                return;
            }
            Vector3 forward = _player.forward;
            _camera.position = PlayerMenuCameraMath.CameraPosition(_player.position, forward);
            _camera.rotation = PlayerMenuCameraMath.CameraRotation(forward);
        }
    }
}
