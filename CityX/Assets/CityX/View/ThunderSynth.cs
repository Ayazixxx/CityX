namespace CityX.View
{
    /// <summary>
    /// Thunder, built from noise -- the project has no thunder recording and a
    /// storm with lightning and no thunder is half a storm.
    ///
    /// The shape is the physical one: a strike is a sharp CRACK (the channel
    /// snapping, all high frequencies, dying in a fraction of a second) that
    /// is followed by a ROLL (the same event reaching you from different
    /// stretches of the bolt, at different times, low and broad) -- and
    /// distance does two things to both: it strips the high end, so a far
    /// strike is only a low murmur, and it smears the rolls out, so the
    /// attack softens and the whole thing lasts longer. "Close thunder tends
    /// to have brighter high-frequency energy and a punchy impact, while
    /// distant thunder loses high end and becomes a broader low-end wave."
    ///
    /// PURE, NO AUDIO DEVICE: it returns samples, deterministically for a seed, so
    /// what it produces can be measured (peak, length, how bright it is at
    /// 200m against 1.5km) without an audio device. Rendering it is a few
    /// milliseconds; RainEffect caches a few variants.
    /// </summary>
    public static class ThunderSynth
    {
        public const int SampleRate = 32000;

        /// <summary>Peak level of a generated clip, leaving headroom for the mixer.</summary>
        public const float PeakLevel = 0.85f;

        /// <summary>How long a strike at this distance rumbles, in seconds: 4 up close, 8 far off.</summary>
        public static float DurationSeconds(float distanceMetres)
        {
            return 4.0f + 4.0f * (1.0f - Nearness(distanceMetres));
        }

        /// <summary>1 within 100m, 0 beyond 1.7km.</summary>
        public static float Nearness(float distanceMetres)
        {
            float t = (distanceMetres - 100.0f) / 1600.0f;
            return 1.0f - (t < 0.0f ? 0.0f : (t > 1.0f ? 1.0f : t));
        }

        // xorshift32: deterministic, allocation-free, and identical everywhere.
        private struct Rng
        {
            private uint _state;

            public Rng(int seed)
            {
                _state = (uint)seed * 2654435761u + 0x9E3779B9u;
                if (_state == 0)
                {
                    _state = 1;
                }
                Next();
                Next();
            }

            public uint Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return _state;
            }

            /// <summary>[0, 1)</summary>
            public float Unit()
            {
                return (Next() >> 8) / 16777216.0f;
            }

            /// <summary>[-1, 1)</summary>
            public float Signed()
            {
                return Unit() * 2.0f - 1.0f;
            }

            public float Range(float min, float max)
            {
                return min + (max - min) * Unit();
            }
        }

        /// <summary>One-pole low-pass coefficient for a cutoff in hertz.</summary>
        private static float LowPassCoefficient(float cutoffHz)
        {
            // 1 - exp(-2 pi fc / fs), by its Pade-friendly rational form so this
            // file needs no Math.Exp for something this coarse.
            float x = 6.2831853f * cutoffHz / SampleRate;
            float a = x / (1.0f + x);
            return a > 1.0f ? 1.0f : a;
        }

        private static float Exp(float x)
        {
            return (float)System.Math.Exp(x);
        }

        /// <summary>
        /// The samples of one strike heard from <paramref name="distanceMetres"/>
        /// away, mono at <see cref="SampleRate"/>, peak <see cref="PeakLevel"/>.
        /// The same (distance, seed) always gives the same clip.
        /// </summary>
        public static float[] Generate(float distanceMetres, int seed)
        {
            float near = Nearness(distanceMetres);
            float far = 1.0f - near;
            float seconds = DurationSeconds(distanceMetres);
            int length = (int)(seconds * SampleRate);
            var samples = new float[length];
            var rng = new Rng(seed);

            // --- the rolls: where in time the sound arrives from, and how ---
            int rolls = 4 + (int)(rng.Unit() * 4.0f);
            var rollStart = new float[rolls];
            var rollAmp = new float[rolls];
            var rollAttack = new float[rolls];
            var rollDecay = new float[rolls];
            for (int i = 0; i < rolls; i++)
            {
                float u = rolls == 1 ? 0.0f : (float)i / (rolls - 1);
                rollStart[i] = (i == 0 ? 0.0f : 0.15f + u * seconds * 0.6f) + rng.Range(0.0f, 0.25f);
                rollAmp[i] = (i == 0 ? 1.0f : rng.Range(0.35f, 0.9f)) * (1.0f - 0.45f * u);
                rollAttack[i] = 0.03f + (0.15f + 0.5f * far) * rng.Range(0.6f, 1.4f);
                rollDecay[i] = (0.7f + 1.2f * far) * rng.Range(0.7f, 1.6f);
            }

            // --- the rumble: brown noise, its cutoff sinking as it rolls away ---
            float brightHz = 260.0f + 700.0f * near;
            float brown = 0.0f;
            float lowPassed = 0.0f;
            float wobble = 0.0f;
            float wobbleCoefficient = LowPassCoefficient(5.0f);
            for (int n = 0; n < length; n++)
            {
                float t = (float)n / SampleRate;
                float white = rng.Signed();
                brown = brown * 0.996f + white * 0.09f;

                float cutoff = 70.0f + (brightHz - 70.0f) * Exp(-t / (1.2f + 1.5f * far));
                lowPassed += LowPassCoefficient(cutoff) * (brown - lowPassed);

                float envelope = 0.0f;
                for (int i = 0; i < rolls; i++)
                {
                    float since = t - rollStart[i];
                    if (since > 0.0f)
                    {
                        envelope += rollAmp[i] * (1.0f - Exp(-since / rollAttack[i])) * Exp(-since / rollDecay[i]);
                    }
                }

                // A slow tremble on the roll, the way real thunder beats.
                wobble += wobbleCoefficient * (rng.Signed() - wobble);
                float modulation = 0.8f + 0.9f * wobble;

                samples[n] = lowPassed * envelope * (modulation < 0.3f ? 0.3f : modulation);
            }

            // --- the crack: only if it is close enough to have one ---
            if (distanceMetres < 1200.0f)
            {
                float crackAmp = near * near * 1.6f + 0.1f;
                float crackLength = 0.35f;
                int crackSamples = (int)(crackLength * SampleRate);
                float highPassState = 0.0f;
                float crackLow = 0.0f;
                float crackCoefficient = LowPassCoefficient(CityX.World.Weather.ThunderBrightnessHz(distanceMetres));
                float highPassCoefficient = LowPassCoefficient(900.0f);
                for (int n = 0; n < crackSamples && n < length; n++)
                {
                    float t = (float)n / SampleRate;
                    float white = rng.Signed();
                    crackLow += crackCoefficient * (white - crackLow);
                    highPassState += highPassCoefficient * (crackLow - highPassState);
                    float band = crackLow - highPassState;   // 900Hz up to the brightness cutoff

                    // A sharp hit and a second, softer tearing sound just behind it.
                    float hit = Exp(-t / 0.045f);
                    float tear = t > 0.09f ? 0.45f * Exp(-(t - 0.09f) / 0.08f) : 0.0f;
                    samples[n] += band * crackAmp * (hit + tear) * 3.0f;
                }
            }

            // --- shape the ends and normalise ---
            int fade = (int)(0.6f * SampleRate);
            float peak = 0.0f;
            for (int n = 0; n < length; n++)
            {
                if (n > length - fade)
                {
                    float f = (float)(length - n) / fade;
                    samples[n] *= f * f;
                }
                float a = samples[n] < 0.0f ? -samples[n] : samples[n];
                if (a > peak)
                {
                    peak = a;
                }
            }
            float gain = peak > 1e-6f ? PeakLevel / peak : 0.0f;
            for (int n = 0; n < length; n++)
            {
                samples[n] *= gain;
            }
            return samples;
        }

    }
}
