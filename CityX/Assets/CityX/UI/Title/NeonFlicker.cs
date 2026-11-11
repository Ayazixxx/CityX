using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// How bright a failing neon tube is at any moment (owner, 2026-11-11: the logo's magenta strokes "to have like a neon sign effect where
    /// it flickers and that part looks like its a led neon sign"). Lit, it hums a little; every few seconds a fault: a stutter (the tube
    /// catching and losing its strike, a few quick flashes), a drop-out (dark for a moment, then stuttering back) or a brown-out (sagging dim,
    /// jittering). The same seed gives the same sign.
    /// </summary>
    public sealed class NeonFlicker
    {
        /// <summary>Seconds lit between faults, at least and at most.</summary>
        public const float SteadyMin = 2.5f;

        public const float SteadyMax = 7.0f;

        /// <summary>How far the hum dips a lit tube, and how fast it ripples (cycles a second).</summary>
        public const float Hum = 0.05f;

        public const float HumRate = 1.7f;

        /// <summary>A stutter: this many flashes, each this long, each dark this far down.</summary>
        public const int StutterFlashesMin = 2;

        public const int StutterFlashesMax = 5;
        public const float FlashSeconds = 0.06f;
        public const float FlashDarkest = 0.0f;
        public const float FlashLightest = 0.35f;

        /// <summary>A drop-out: dark this long, then a stutter back on.</summary>
        public const float DropOutMin = 0.12f;

        public const float DropOutMax = 0.5f;

        /// <summary>A brown-out: sagging to this, for this long.</summary>
        public const float BrownOutLevel = 0.3f;

        public const float BrownOutMin = 0.5f;
        public const float BrownOutMax = 1.4f;

        private enum Fault
        {
            Stutter,
            DropOut,
            BrownOut,
        }

        private readonly int _seed;
        private System.Random _random;

        // The fault now running or next: lit until _faultStart, faulty until _faultEnd.
        private float _faultStart;
        private float _faultEnd;
        private Fault _fault;
        private float _dark;          // a drop-out's dark part, seconds
        private float[] _flashLows;   // each flash's low
        private float _lastAsked;
        private int _faultsMade;

        /// <summary>How many faults have begun by the time last asked: it counts up as each one starts (a sound for each, TitleLogo).</summary>
        public int FaultsBegun { get; private set; }

        public NeonFlicker(int seed)
        {
            _seed = seed;
            Restart();
        }

        /// <summary>Brightness at <paramref name="time"/> seconds, 0 (dark) to 1 (fully lit). Asked in any order: going back starts over.</summary>
        public float Brightness(float time)
        {
            if (time < 0.0f)
            {
                return Lit(0.0f);
            }
            if (time < _lastAsked)
            {
                Restart();
            }
            _lastAsked = time;
            while (time >= _faultEnd)
            {
                Next(_faultEnd);
            }
            FaultsBegun = time < _faultStart ? _faultsMade - 1 : _faultsMade;
            if (time < _faultStart)
            {
                return Lit(time);
            }
            float into = time - _faultStart;
            switch (_fault)
            {
                case Fault.DropOut:
                    return into < _dark ? 0.0f : Flashes(into - _dark, time);
                case Fault.BrownOut:
                {
                    float length = _faultEnd - _faultStart;
                    float sag = Mathf.Sin(Mathf.PI * into / length);   // down and back up
                    float jitter = _flashLows[Mathf.Min(_flashLows.Length - 1, (int)(into / FlashSeconds) % _flashLows.Length)] * 0.3f;
                    return Lit(time) * Mathf.Lerp(1.0f, BrownOutLevel + jitter, sag);
                }
                default:
                    return Flashes(into, time);
            }
        }

        private void Restart()
        {
            _random = new System.Random(_seed);
            _lastAsked = float.NegativeInfinity;
            _faultEnd = 0.0f;
            _faultsMade = 0;
            Next(0.0f);
        }

        /// <summary>A lit stretch from <paramref name="from"/>, then the next fault.</summary>
        private void Next(float from)
        {
            _faultStart = from + Range(SteadyMin, SteadyMax);
            _faultsMade++;
            double roll = _random.NextDouble();
            _fault = roll < 0.55 ? Fault.Stutter : roll < 0.85 ? Fault.DropOut : Fault.BrownOut;
            int flashes = _random.Next(StutterFlashesMin, StutterFlashesMax + 1);
            _flashLows = new float[flashes];
            for (int i = 0; i < flashes; i++)
            {
                _flashLows[i] = Range(FlashDarkest, FlashLightest);
            }
            float length = 2.0f * flashes * FlashSeconds;
            _dark = 0.0f;
            if (_fault == Fault.DropOut)
            {
                _dark = Range(DropOutMin, DropOutMax);
                length += _dark;
            }
            else if (_fault == Fault.BrownOut)
            {
                length = Range(BrownOutMin, BrownOutMax);
            }
            _faultEnd = _faultStart + length;
        }

        /// <summary>The flashes: low, lit, low, lit... ending lit.</summary>
        private float Flashes(float into, float time)
        {
            int step = (int)(into / FlashSeconds);
            if (step % 2 == 1)
            {
                return Lit(time);
            }
            return _flashLows[Mathf.Min(step / 2, _flashLows.Length - 1)];
        }

        private static float Lit(float time)
        {
            return 1.0f - Hum * (0.5f + 0.5f * Mathf.Sin(time * HumRate * 2.0f * Mathf.PI));
        }

        private float Range(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }
    }
}
