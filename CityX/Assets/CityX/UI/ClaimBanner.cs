using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Big centred announcement text -- "BUILDING ACQUIRED" when a claim
    /// lands, and the refusal message when one doesn't. Fades in, holds,
    /// fades out. Ported from ui/claim_banner.gd.
    ///
    /// REUSED FOR EVERY MESSAGE, never rebuilt -- calling ShowMessage again
    /// while one is still on screen replaces it outright rather than
    /// queueing, same as Godot's own version: the newest message is always
    /// the more current one, and a queue would show stale text seconds late.
    /// </summary>
    public sealed class ClaimBanner : MonoBehaviour
    {
        public const float FadeInSeconds = 0.45f;
        public const float HoldSeconds = 2.2f;
        public const float FadeOutSeconds = 0.8f;

        public Text Label;

        private float _elapsed;
        private bool _showing;

        /// <summary>Fired once a message has fully faded back out.</summary>
        public event System.Action Finished;

        public bool IsShowing => _showing;

        public void ShowMessage(string text)
        {
            if (Label == null)
            {
                return;
            }
            Label.text = text;
            _elapsed = 0.0f;
            _showing = true;
            SetAlpha(0.0f);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// The fade timeline advanced by one step. Public, and taking its
        /// own deltaTime rather than reading Time.deltaTime, so a test can
        /// fast-forward through the whole fade without waiting on real
        /// wall-clock seconds -- same reasoning as PlayerVitals.TickStamina.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_showing || Label == null)
            {
                return;
            }
            _elapsed += deltaTime;
            SetAlpha(AlphaFor(_elapsed, FadeInSeconds, HoldSeconds, FadeOutSeconds));

            if (_elapsed >= TotalDuration())
            {
                _showing = false;
                Finished?.Invoke();
            }
        }

        private void SetAlpha(float alpha)
        {
            Color colour = Label.color;
            colour.a = alpha;
            Label.color = colour;
        }

        /// <summary>
        /// The fade-in/hold/fade-out curve as a pure function of elapsed
        /// time, so the timing can be checked without a frame ever running.
        /// </summary>
        public static float AlphaFor(float elapsed, float fadeIn, float hold, float fadeOut)
        {
            if (elapsed < fadeIn)
            {
                return fadeIn <= 0.0f ? 1.0f : Mathf.Clamp01(elapsed / fadeIn);
            }
            if (elapsed < fadeIn + hold)
            {
                return 1.0f;
            }
            float intoFadeOut = elapsed - fadeIn - hold;
            if (intoFadeOut >= fadeOut)
            {
                return 0.0f;
            }
            return fadeOut <= 0.0f ? 0.0f : 1.0f - Mathf.Clamp01(intoFadeOut / fadeOut);
        }

        public static float TotalDuration() => FadeInSeconds + HoldSeconds + FadeOutSeconds;
    }
}
