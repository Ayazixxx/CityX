namespace CityX.Character
{
    /// <summary>
    /// A speaker's decoded voice waiting to be played (docs/VOICE.md). Frames arrive unevenly over the network; playback holds back until
    /// <see cref="StartSamples"/> are waiting, so a late frame does not cut the voice, and drops the oldest past <see cref="MaxSamples"/>, so
    /// the voice never falls further behind the lips. Run dry, it plays silence and holds back again. Written on the main thread and read on
    /// the audio thread, hence the lock.
    /// </summary>
    public sealed class VoiceJitterBuffer
    {
        /// <summary>Three frames (60 ms) waiting before playback starts.</summary>
        public const int StartSamples = VoiceCodec.FrameSamples * 3;

        /// <summary>Never more than 200 ms behind.</summary>
        public const int MaxSamples = VoiceCodec.SampleRate / 5;

        private readonly float[] _ring = new float[MaxSamples + VoiceCodec.FrameSamples];
        private readonly object _lock = new object();
        private int _start;
        private int _count;
        private bool _playing;

        public int Waiting
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        public void Write(float[] samples, int length)
        {
            lock (_lock)
            {
                for (int i = 0; i < length; i++)
                {
                    _ring[(_start + _count) % _ring.Length] = samples[i];
                    if (_count < _ring.Length)
                    {
                        _count++;
                    }
                    else
                    {
                        _start = (_start + 1) % _ring.Length;
                    }
                }
                if (_count > MaxSamples)
                {
                    _start = (_start + _count - MaxSamples) % _ring.Length;
                    _count = MaxSamples;
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _start = 0;
                _count = 0;
                _playing = false;
            }
        }

        /// <summary>Fills <paramref name="output"/> with what is waiting, silence where nothing is.</summary>
        public void Read(float[] output)
        {
            lock (_lock)
            {
                if (!_playing && _count >= StartSamples)
                {
                    _playing = true;
                }
                for (int i = 0; i < output.Length; i++)
                {
                    if (_playing && _count > 0)
                    {
                        output[i] = _ring[_start];
                        _start = (_start + 1) % _ring.Length;
                        _count--;
                    }
                    else
                    {
                        output[i] = 0.0f;
                        _playing = false;
                    }
                }
            }
        }
    }
}
