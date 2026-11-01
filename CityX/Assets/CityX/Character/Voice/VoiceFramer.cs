using System;

namespace CityX.Character
{
    /// <summary>
    /// Cuts the microphone's samples into the frames the codec takes (<see cref="VoiceCodec.FrameSamples"/> at
    /// <see cref="VoiceCodec.SampleRate"/>), resampling on the way when the microphone cannot record at that rate (docs/VOICE.md).
    /// </summary>
    public sealed class VoiceFramer
    {
        /// <summary>A whole frame, in a buffer reused for the next one.</summary>
        public event Action<float[]> FrameReady;

        /// <summary>The microphone's rate, samples a second.</summary>
        public int InputRate { get; set; } = VoiceCodec.SampleRate;

        private readonly float[] _frame = new float[VoiceCodec.FrameSamples];
        private int _fill;
        private double _position;
        private float _previous;

        public void Reset()
        {
            _fill = 0;
            _position = 0.0;
            _previous = 0.0f;
        }

        /// <summary>Takes the first <paramref name="count"/> samples of <paramref name="input"/>, the ones recorded after the last call's.</summary>
        public void Push(float[] input, int count)
        {
            if (count <= 0)
            {
                return;
            }
            double step = (double)InputRate / VoiceCodec.SampleRate;
            // Each output sample lies between two input samples; the one before this block's first is the last block's last.
            while (_position < count - 1)
            {
                int i = (int)Math.Floor(_position);
                float t = (float)(_position - i);
                float a = i < 0 ? _previous : input[i];
                float b = input[i + 1];
                _frame[_fill++] = a + (b - a) * t;
                if (_fill == _frame.Length)
                {
                    _fill = 0;
                    FrameReady?.Invoke(_frame);
                }
                _position += step;
            }
            _position -= count;
            _previous = input[count - 1];
        }
    }
}
