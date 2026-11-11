using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How fast a body gets up off the ground (<see cref="GetUpMotion"/>): a knockdown in play runs the clip at a little over twice its
    /// speed, the new character's entrance slower than its own, waking up. A body lying part way round from where its clip starts is
    /// first rolled there about its own length (Ragdoll.GetUp), and the clip waits for it.
    /// </summary>
    public readonly struct GetUpPace
    {
        /// <summary>A knockdown in play: the clip at a little over twice its own speed, control back as it stands.</summary>
        public const float QuickSpeed = 2.2f;

        /// <summary>How long a roll of half a turn about the body's length takes at the clip's own speed, seconds.</summary>
        public const float HalfTurnClipSeconds = 0.8f;

        /// <summary>How many times its own speed the clip runs.</summary>
        public readonly float Speed;

        /// <summary>The clip's length at its own speed, seconds.</summary>
        public readonly float ClipSeconds;

        public GetUpPace(float speed, float clipSeconds)
        {
            Speed = Mathf.Max(speed, 0.05f);
            ClipSeconds = Mathf.Max(clipSeconds, 0.0f);
        }

        /// <summary>From the end of the roll to standing, seconds.</summary>
        public float Seconds => ClipSeconds / Speed;

        public float HalfTurnSeconds => HalfTurnClipSeconds / Speed;

        /// <summary>How long a roll of <paramref name="degrees"/> about the body's length takes, seconds.</summary>
        public float RollSeconds(float degrees) => HalfTurnSeconds * Mathf.Clamp01(Mathf.Abs(degrees) / 180.0f);

        /// <summary>Where in the clip the body is, <paramref name="sinceRolled"/> seconds after its roll (0 before), seconds.</summary>
        public float ClipSecondsAt(float sinceRolled) => Mathf.Clamp(sinceRolled * Speed, 0.0f, ClipSeconds);
    }
}
