using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The bow's and the crossbow's sounds, made in code (no audio files to ship), as ArcaneOrbSounds are: the string's twang on a loose (the
    /// crossbow's harder, with the latch's clack), the creak of a draw, an arrow landing in something (a thud in a body, a knock in the world),
    /// and the crossbow being spanned. Built once, on first use.
    /// </summary>
    public static class ArcherySounds
    {
        private const int SampleRate = 44100;
        private static AudioClip _twang;
        private static AudioClip _clack;
        private static AudioClip _creak;
        private static AudioClip _thud;
        private static AudioClip _knock;
        private static AudioClip _span;

        public static AudioClip Twang => _twang != null ? _twang : (_twang = MakeTwang(170.0f, 0.35f, 0.0f));

        /// <summary>The crossbow's loose: a lower, harder string with the latch's clack in front of it.</summary>
        public static AudioClip Clack => _clack != null ? _clack : (_clack = MakeTwang(110.0f, 0.45f, 0.6f));

        /// <summary>A second of the string being drawn: a creak that rises, looped by the bow while it draws.</summary>
        public static AudioClip Creak => _creak != null ? _creak : (_creak = MakeCreak());

        public static AudioClip Thud => _thud != null ? _thud : (_thud = MakeHit(70.0f, 0.18f, 0.5f));

        public static AudioClip Knock => _knock != null ? _knock : (_knock = MakeHit(420.0f, 0.08f, 0.25f));

        /// <summary>The crossbow spanned and a bolt laid: a ratchet of clicks then a snap.</summary>
        public static AudioClip Span => _span != null ? _span : (_span = MakeSpan());

        public static void PlayImpact(Vector3 at, bool inSomeone)
        {
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(inSomeone ? Thud : Knock, at, 0.8f);
            }
        }

        /// <summary>A plucked string: a fundamental and its second and third partials decaying at different rates, a click of noise at the start.</summary>
        private static AudioClip MakeTwang(float fundamental, float seconds, float clack)
        {
            int samples = (int)(seconds * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(41);
            float smoothed = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                smoothed += (raw - smoothed) * 0.5f;
                float pluck = smoothed * Mathf.Exp(-t * 90.0f) * 0.5f;
                float latch = clack > 0.0f ? smoothed * Mathf.Exp(-t * 40.0f) * clack : 0.0f;
                float tone = Mathf.Sin(2.0f * Mathf.PI * fundamental * t) * Mathf.Exp(-t * 9.0f) * 0.5f
                    + Mathf.Sin(2.0f * Mathf.PI * fundamental * 2.0f * t) * Mathf.Exp(-t * 14.0f) * 0.25f
                    + Mathf.Sin(2.0f * Mathf.PI * fundamental * 3.0f * t) * Mathf.Exp(-t * 22.0f) * 0.12f;
                data[i] = Mathf.Clamp((tone + pluck + latch) * Mathf.Clamp01(t / 0.001f), -1.0f, 1.0f);
            }
            return MakeClip("ArcheryTwang", data);
        }

        /// <summary>Wood and string under strain: slow noise through a wandering resonance that climbs over the second.</summary>
        private static AudioClip MakeCreak()
        {
            int samples = SampleRate;
            var data = new float[samples];
            var noise = new System.Random(43);
            float band = 0.0f, bandVelocity = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                float hz = 260.0f + 180.0f * t;
                float w = 2.0f * Mathf.PI * hz / SampleRate;
                // A resonator: a damped spring driven by the noise.
                bandVelocity += (raw * 0.02f - band) * w * w - bandVelocity * w * 0.08f;
                band += bandVelocity;
                float grain = 0.6f + 0.4f * Mathf.Sin(2.0f * Mathf.PI * (18.0f + 10.0f * t) * t);
                data[i] = Mathf.Clamp(band * 4.0f * grain * (0.4f + 0.6f * t), -1.0f, 1.0f);
            }
            return MakeClip("ArcheryCreak", data);
        }

        /// <summary>A short blow: a tone falling in pitch under a puff of noise.</summary>
        private static AudioClip MakeHit(float hz, float seconds, float noiseShare)
        {
            int samples = (int)(seconds * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(47);
            float phase = 0.0f, smoothed = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                phase += 2.0f * Mathf.PI * hz * (1.0f - 0.5f * t / seconds) / SampleRate;
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                smoothed += (raw - smoothed) * 0.3f;
                float envelope = Mathf.Clamp01(t / 0.002f) * Mathf.Exp(-t * (6.0f / seconds));
                data[i] = Mathf.Clamp((Mathf.Sin(phase) * (1.0f - noiseShare) + smoothed * noiseShare) * envelope, -1.0f, 1.0f);
            }
            return MakeClip("ArcheryHit", data);
        }

        /// <summary>A ratchet of clicks slowing as the string comes back, then the latch's snap.</summary>
        private static AudioClip MakeSpan()
        {
            float seconds = ArcheryRules.CrossbowReloadSeconds;
            int samples = (int)(seconds * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(53);
            float smoothed = 0.0f;
            float nextClick = 0.1f;
            float clickAge = 1.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                if (t >= nextClick && t < seconds - 0.3f)
                {
                    clickAge = 0.0f;
                    nextClick += 0.06f + 0.05f * (t / seconds);
                }
                clickAge += 1.0f / SampleRate;
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                smoothed += (raw - smoothed) * 0.6f;
                float click = smoothed * Mathf.Exp(-clickAge * 400.0f) * 0.5f;
                float snapAt = seconds - 0.2f;
                float snap = t >= snapAt ? (Mathf.Sin(2.0f * Mathf.PI * 900.0f * (t - snapAt)) * 0.4f + smoothed * 0.5f) * Mathf.Exp(-(t - snapAt) * 60.0f) : 0.0f;
                data[i] = Mathf.Clamp(click + snap, -1.0f, 1.0f);
            }
            return MakeClip("ArcherySpan", data);
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
