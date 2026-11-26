using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The pure part of the C-menu's camera: the front-on framing of the player, computed from wherever they are facing, and the way
    /// the camera gets there.
    ///
    /// REUSES THE CREATOR'S OWN TUNED NUMBERS. CharacterCreatorBuilder positions its camera at stage-local (0, 1.6, 4) with rotation
    /// (8, 180, 0): 1.6 m up, 4 m in front, pitched 8 degrees down, looking back at the body. Only "forward" changes here: it is read
    /// from the live player's facing, since the player can be facing any way when C is pressed.
    /// </summary>
    public static class PlayerMenuCameraMath
    {
        public const float Height = 1.6f;
        public const float Distance = 4.0f;
        public const float PitchDegrees = 8.0f;

        /// <summary>A spin round the player takes this long, plus <see cref="SecondsPerTurn"/> for each whole turn of it.</summary>
        public const float BaseSeconds = 0.25f;

        public const float SecondsPerTurn = 1.0f;

        public static Vector3 CameraPosition(Vector3 playerPosition, Vector3 playerForward)
        {
            return playerPosition + Vector3.up * Height + playerForward.normalized * Distance;
        }

        /// <summary>Looking back along -playerForward, with the creator's own fixed downward pitch layered on top.</summary>
        public static Quaternion CameraRotation(Vector3 playerForward)
        {
            float yaw = Quaternion.LookRotation(-playerForward, Vector3.up).eulerAngles.y;
            return Quaternion.Euler(PitchDegrees, yaw, 0.0f);
        }

        /// <summary>Where a point stands round the player, in degrees: 0 along +Z, growing clockwise seen from above (90 along +X).</summary>
        public static float Bearing(Vector3 player, Vector3 point)
        {
            return Mathf.Repeat(Mathf.Atan2(point.x - player.x, point.z - player.z) * Mathf.Rad2Deg, 360.0f);
        }

        /// <summary>How far the camera goes round the player, clockwise seen from above, from <paramref name="from"/> to <paramref name="to"/>, degrees.</summary>
        public static float ClockwiseDegrees(Vector3 player, Vector3 from, Vector3 to)
        {
            return Mathf.Repeat(Bearing(player, to) - Bearing(player, from), 360.0f);
        }

        /// <summary>
        /// Within this of the menu's shot the other way round (anticlockwise), the camera goes the short way back to it, degrees. Clockwise
        /// round from behind or the side (owner, 2026-11-05: "just make it spin around clockwise forward smoothly"), but never most of a turn
        /// round to a shot it is already in front of (owner, 2026-11-26: "make it so the c menu doesnt spin around if you are already looking
        /// at the front of your character ... no unnecessary spins").
        /// </summary>
        public const float ShortWayBackDegrees = 90.0f;

        /// <summary>
        /// How far the camera goes round the player to the shot, degrees: clockwise (positive) as a rule, anticlockwise (negative) when the
        /// shot is within <see cref="ShortWayBackDegrees"/> that way.
        /// </summary>
        public static float TurnDegrees(Vector3 player, Vector3 from, Vector3 to)
        {
            float clockwise = ClockwiseDegrees(player, from, to);
            float back = 360.0f - clockwise;
            return back <= ShortWayBackDegrees ? -back : clockwise;
        }

        /// <summary>How long the spin round to the front takes: the further round, the longer, at an even pace.</summary>
        public static float SpinSeconds(float degrees)
        {
            return BaseSeconds + SecondsPerTurn * Mathf.Clamp(Mathf.Abs(degrees), 0.0f, 360.0f) / 360.0f;
        }

        /// <summary>
        /// The camera <paramref name="share"/> of the way (0 to 1, already eased) round the player from where it was to the menu's shot:
        /// ALONG A CIRCLE ROUND THE PLAYER, the way <see cref="TurnDegrees"/> says, its distance and height going from the old ones to the new, and
        /// facing the player all the way (its own aim off the player at the start fading out). A straight line from behind to in front
        /// cut through the body, and the turn went whichever way was shorter.
        /// </summary>
        public static void Spin(Vector3 player, Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation,
            float share, out Vector3 position, out Quaternion rotation)
        {
            float startBearing = Bearing(player, fromPosition);
            float degrees = TurnDegrees(player, fromPosition, toPosition);
            float bearing = startBearing + degrees * share;

            Vector3 fromFlat = fromPosition - player, toFlat = toPosition - player;
            float fromHeight = fromFlat.y, toHeight = toFlat.y;
            fromFlat.y = 0.0f;
            toFlat.y = 0.0f;
            float radius = Mathf.Lerp(fromFlat.magnitude, toFlat.magnitude, share);
            float height = Mathf.Lerp(fromHeight, toHeight, share);
            float radians = bearing * Mathf.Deg2Rad;
            position = player + new Vector3(Mathf.Sin(radians) * radius, height, Mathf.Cos(radians) * radius);

            // Facing the player round the circle, plus whatever the camera's own aim was off it at the start (the third-person camera
            // looks past the shoulder), fading to nothing; its pitch going to the menu's.
            Vector3 fromAngles = fromRotation.eulerAngles, toAngles = toRotation.eulerAngles;
            float offAim = Mathf.DeltaAngle(startBearing + 180.0f, fromAngles.y) * (1.0f - share);
            float pitch = Mathf.LerpAngle(fromAngles.x, toAngles.x, share);
            rotation = Quaternion.Euler(pitch, bearing + 180.0f + offAim, 0.0f);
            if (share >= 1.0f)
            {
                position = toPosition;
                rotation = toRotation;
            }
        }

        /// <summary>Slow out, slow in, with no jolt at either end (smootherstep).</summary>
        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6.0f - 15.0f) + 10.0f);
        }
    }

    /// <summary>
    /// Spins the scene's single shared camera round to a front-on shot of the player for as long as the C-menu is open, then hands it
    /// back (docs/HUD.md). Drives the existing camera rather than spawning a second one, as ClaimCutscene does; Tick takes its own
    /// delta so a test can step straight to the end of the spin.
    /// </summary>
    public sealed class PlayerMenuCamera : MonoBehaviour
    {
        /// <summary>The longest the spin round to the front can take: a whole turn of it (PlayerMenuCameraMath.SpinSeconds).</summary>
        public const float EaseSeconds = PlayerMenuCameraMath.BaseSeconds + PlayerMenuCameraMath.SecondsPerTurn;

        private Transform _camera;
        private Transform _player;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _elapsed;
        private float _seconds;
        private bool _easing;

        /// <summary>
        /// Starts the spin from wherever the camera already is (normally where ThirdPersonCamera left it: PlayerMenuView switches that off
        /// in the same call). The caller switches the input and the third-person camera off and back on; this only moves the camera.
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
            _seconds = PlayerMenuCameraMath.SpinSeconds(PlayerMenuCameraMath.TurnDegrees(_player.position, _startPosition, _targetPosition));
            _elapsed = 0.0f;
            _easing = true;
        }

        /// <summary>
        /// Halts the spin at once, mid-flight if need be: PlayerMenuView calls this on Close(), or this and ThirdPersonCamera would both
        /// write the camera on the frame it resumes.
        /// </summary>
        public void Stop()
        {
            _easing = false;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>The spin advanced by one step.</summary>
        public void Tick(float deltaTime)
        {
            if (!_easing || _camera == null || _player == null)
            {
                return;
            }

            _elapsed += deltaTime;
            float t = _seconds > 0.0f ? Mathf.Clamp01(_elapsed / _seconds) : 1.0f;
            PlayerMenuCameraMath.Spin(_player.position, _startPosition, _startRotation, _targetPosition, _targetRotation,
                PlayerMenuCameraMath.Smooth(t), out Vector3 position, out Quaternion rotation);
            _camera.SetPositionAndRotation(position, rotation);

            if (t >= 1.0f)
            {
                _easing = false;
            }
        }
    }
}
