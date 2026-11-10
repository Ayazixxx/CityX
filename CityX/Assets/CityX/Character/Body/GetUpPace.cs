using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How fast a body gets up off the ground: where in the get-up clip it is, how long after it has rolled onto its front (owner, 2026-11-10:
    /// "make the standing up animation look good ... right now it looks glitchy"). The clip (getting_up, 7.6 s, measured on the body) lies
    /// face down and shifts for a second, pushes its chest up (to 2.0 s), gets onto its knees (3.0), brings its feet under it (4.0), stands
    /// in under a second (4.9) and settles. The new character's entrance plays it as it is. A knockdown in play has a second and a bit: played
    /// evenly at that pace the rise to standing took a fifth of a second and read as a pop, so the quick get-up skips the shifting and gives
    /// each part its share, the rise the largest.
    /// </summary>
    public readonly struct GetUpPace
    {
        /// <summary>The clip's length, and where in it the body is up (it settles and stands still from there), seconds.</summary>
        public const float ClipSeconds = IntroCutscene.GettingUpClipSeconds;

        public const float UprightClipSeconds = 5.0f;

        /// <summary>A get-up at least this long plays the clip evenly; a shorter one is the quick one, seconds for the whole clip.</summary>
        private const float EvenFromSeconds = 3.8f;

        /// <summary>The quick get-up: at each share of its time, the clip's second. The push-up, the knees, the feet under, the rise.</summary>
        private static readonly Vector2[] Quick =
        {
            new Vector2(0.00f, 0.9f),
            new Vector2(0.20f, 2.0f),
            new Vector2(0.40f, 3.0f),
            new Vector2(0.62f, 4.0f),
            new Vector2(1.00f, 4.9f),
        };

        /// <summary>How long a body flat on its back takes to roll onto its front, as a share of the pace: less from its side, by the angle.</summary>
        private const float RollShare = 0.22f;

        private readonly bool _quick;

        /// <summary>From lying on its front to standing, seconds; and a whole half turn of rolling over before that.</summary>
        public readonly float Seconds;

        public readonly float HalfTurnSeconds;

        /// <summary>The pace of a get-up whose whole clip would take <paramref name="paceSeconds"/> played evenly.</summary>
        public GetUpPace(float paceSeconds)
        {
            paceSeconds = Mathf.Max(paceSeconds, 0.1f);
            _quick = paceSeconds < EvenFromSeconds;
            Seconds = paceSeconds * UprightClipSeconds / ClipSeconds;
            HalfTurnSeconds = paceSeconds * RollShare;
        }

        /// <summary>How long a roll of <paramref name="degrees"/> about the body's length takes, seconds.</summary>
        public float RollSeconds(float degrees) => HalfTurnSeconds * Mathf.Clamp01(Mathf.Abs(degrees) / 180.0f);

        /// <summary>Where in the clip the body is, <paramref name="sinceOnItsFront"/> seconds after it was on its front (0 before), seconds.</summary>
        public float ClipSecondsAt(float sinceOnItsFront)
        {
            float share = Mathf.Clamp01(sinceOnItsFront / Seconds);
            if (!_quick)
            {
                return share * UprightClipSeconds;
            }
            for (int i = 1; i < Quick.Length; i++)
            {
                if (share <= Quick[i].x)
                {
                    return Mathf.Lerp(Quick[i - 1].y, Quick[i].y, Mathf.InverseLerp(Quick[i - 1].x, Quick[i].x, share));
                }
            }
            return Quick[Quick.Length - 1].y;
        }
    }
}
