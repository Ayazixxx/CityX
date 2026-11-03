using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE DRAWN BODY GLIDES OVER STEPS (owner, 2026-11-02: "make going up and down stairs look less jank", docs/STAIRS.md). The capsule goes up a stair in lurches, 10 cm in a
    /// frame and then none; the body and the camera were fastened to it. Games keep the capsule for the physics and draw the body from a blurred ground: the
    /// height of the ground a little ahead and behind, averaged, is a slope however stepped the ground really is. PURE: the ground's heights go in, how far to
    /// draw the body from the capsule and the slope along the way it goes come out. The feet still land on the real steps (FootPlanting).
    /// </summary>
    public sealed class GroundGlide
    {
        /// <summary>Where the ground is read, metres along the travel. Evenly spaced, but not at any multiple of the stairs' 0.36 m tread: they cross their risers one at a time, not all at once.</summary>
        public static readonly float[] SampleAlong = { -0.41f, -0.14f, 0.13f, 0.4f };

        /// <summary>A sample further from the capsule's height than this per metre along (plus <see cref="SampleMargin"/>) is a wall or a low crate, not the way on.</summary>
        public const float SteepestRisePerMetre = 0.85f;

        public const float SampleMargin = 0.2f;

        /// <summary>The most the body is drawn away from the capsule, metres.</summary>
        public const float MaxOffset = 0.35f;

        /// <summary>Ground this level under all the samples is flat, and the body is the capsule's.</summary>
        public const float FlatWithin = 0.015f;

        /// <summary>How quickly the body follows the blurred ground (the caller reads the ground this much ahead at its speed, so it does not trail on a long flight).</summary>
        public const float EaseSeconds = 0.06f;

        /// <summary>Off the ground the body goes back onto the capsule this quickly.</summary>
        public const float InAirSeconds = 0.05f;

        private const float SlopeSeconds = 0.15f;

        private float _height;
        private bool _started;

        /// <summary>How far above the capsule the body is drawn, metres (below it when negative).</summary>
        public float Offset { get; private set; }

        /// <summary>The ground's rise per metre along the travel, smoothed: 0 on the flat, about 0.63 up a stair, negative going down.</summary>
        public float Slope { get; private set; }

        /// <summary>Forgets it all (a teleport, a respawn).</summary>
        public void Reset()
        {
            _started = false;
            Offset = 0.0f;
            Slope = 0.0f;
        }

        /// <summary>
        /// One frame. <paramref name="groundAt"/> is the ground's height at each of <see cref="SampleAlong"/>, NaN where there is none; <paramref name="skin"/> how
        /// far the capsule rests above what it stands on, <paramref name="lead"/> how far ahead of the body the samples were moved. <paramref name="onGround"/> is false in the
        /// air and in any state the body is not to glide.
        /// </summary>
        public void Step(float capsuleY, float[] groundAt, float skin, float lead, bool onGround, float deltaTime)
        {
            if (deltaTime <= 0.0f)
            {
                return;
            }
            if (!_started)
            {
                _started = true;
                _height = capsuleY;
            }

            float sum = 0.0f, lowest = float.MaxValue, highest = float.MinValue, behind = 0.0f, ahead = 0.0f;
            int count = 0, behindCount = 0, aheadCount = 0;
            float standsOn = capsuleY - skin;
            for (int i = 0; i < SampleAlong.Length; i++)
            {
                float ground = groundAt[i];
                if (float.IsNaN(ground) || Mathf.Abs(ground - standsOn) > SteepestRisePerMetre * Mathf.Abs(SampleAlong[i] + lead) + SampleMargin)
                {
                    continue;
                }
                sum += ground;
                lowest = Mathf.Min(lowest, ground);
                highest = Mathf.Max(highest, ground);
                count++;
                if (SampleAlong[i] < 0.0f)
                {
                    behind += ground;
                    behindCount++;
                }
                else
                {
                    ahead += ground;
                    aheadCount++;
                }
            }

            bool glides = onGround && count >= 2 && highest - lowest > FlatWithin;
            if (onGround)
            {
                float wanted = glides ? capsuleY + Mathf.Clamp(sum / count + skin - capsuleY, -MaxOffset, MaxOffset) : capsuleY;
                _height = Mathf.Lerp(_height, wanted, 1.0f - Mathf.Exp(-deltaTime / EaseSeconds));
                Offset = Mathf.Clamp(_height - capsuleY, -MaxOffset, MaxOffset);
            }
            else
            {
                // Eased on the offset, not the height: a jump's capsule goes up fast, and the body must go with it.
                Offset *= Mathf.Exp(-deltaTime / InAirSeconds);
            }
            if (!glides && Mathf.Abs(Offset) < 0.0005f)
            {
                Offset = 0.0f;
            }
            _height = capsuleY + Offset;

            float slope = 0.0f;
            if (glides && behindCount > 0 && aheadCount > 0)
            {
                float across = (SampleAlong[SampleAlong.Length - 1] + SampleAlong[SampleAlong.Length - 2] - SampleAlong[0] - SampleAlong[1]) * 0.5f;
                slope = (ahead / aheadCount - behind / behindCount) / across;
            }
            Slope = Mathf.Lerp(Slope, slope, 1.0f - Mathf.Exp(-deltaTime / SlopeSeconds));
            if (slope == 0.0f && Mathf.Abs(Slope) < 0.001f)
            {
                Slope = 0.0f;
            }
        }
    }
}
