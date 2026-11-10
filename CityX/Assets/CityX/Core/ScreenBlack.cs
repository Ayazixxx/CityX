using UnityEngine;

namespace CityX.Core
{
    /// <summary>
    /// The whole screen faded to black and back, over everything, across a change of scene: the belt's drop at the end of making a
    /// character goes black, the world loads behind it, and the fall from the sky comes up out of it (docs/BELT_INTRO.md). Nothing exists
    /// until something asks for black, and it removes itself once the screen is clear again.
    /// </summary>
    public sealed class ScreenBlack : MonoBehaviour
    {
        private static ScreenBlack _instance;

        private float _level;
        private float _target;
        private float _perSecond;

        /// <summary>How black the screen is now: 0 clear, 1 black.</summary>
        public static float Level => _instance != null ? _instance._level : 0.0f;

        /// <summary>Fades to <paramref name="level"/> (0 clear, 1 black) over <paramref name="seconds"/>, from wherever it is.</summary>
        public static void FadeTo(float level, float seconds)
        {
            level = Mathf.Clamp01(level);
            if (_instance == null)
            {
                if (level <= 0.0f || !Application.isPlaying)
                {
                    return;   // already clear
                }
                var go = new GameObject("ScreenBlack") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ScreenBlack>();
            }
            _instance._target = level;
            _instance._held = 0.0f;
            _instance._perSecond = seconds > 0.0f ? 1.0f / seconds : float.PositiveInfinity;
            if (seconds <= 0.0f)
            {
                _instance._level = level;
            }
        }

        /// <summary>The longest the screen is held black with nothing asking again, seconds: a world that never clears it must not stay dark.</summary>
        public const float LongestHeldSeconds = 30.0f;

        private float _held;

        private void Update()
        {
            // By the clock, so it still fades with the game paused; by the game's own time where that runs ahead of the clock (a
            // render made frame by frame).
            float passed = Mathf.Max(Time.unscaledDeltaTime, Time.deltaTime);
            _level = Mathf.MoveTowards(_level, _target, _perSecond * passed);
            _held = _target > 0.0f ? _held + passed : 0.0f;
            if (_held > LongestHeldSeconds)
            {
                _target = 0.0f;
                _perSecond = 1.0f;
            }
            if (_level <= 0.0f && _target <= 0.0f)
            {
                _instance = null;
                Destroy(gameObject);
            }
        }

        private void OnGUI()
        {
            if (_level <= 0.0f || Event.current.type != EventType.Repaint)
            {
                return;
            }
            GUI.depth = int.MinValue;
            Color was = GUI.color;
            GUI.color = new Color(0.0f, 0.0f, 0.0f, _level);
            GUI.DrawTexture(new Rect(0.0f, 0.0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = was;
        }
    }
}
