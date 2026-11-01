using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A voice frame squeezed to a quarter for the wire (docs/VOICE.md): 20 ms of 16 kHz mono speech as IMA ADPCM, four bits a sample, 165
    /// bytes. Each frame carries the coder's state at its start, so a frame lost on the way costs 20 ms of silence and nothing after it.
    /// </summary>
    public static class VoiceCodec
    {
        public const int SampleRate = 16000;

        public const int FrameSamples = 320;

        public const float FrameSeconds = (float)FrameSamples / SampleRate;

        /// <summary>The frame's number (2 bytes), then the coder's predictor (2) and step index (1) at its start.</summary>
        public const int HeaderBytes = 5;

        public const int FrameBytes = HeaderBytes + FrameSamples / 2;

        private const int MaxStepIndex = 88;

        private static readonly int[] StepSizes =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143,
            157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552,
            1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487,
            12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767,
        };

        private static readonly int[] IndexSteps = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };

        /// <summary>The coder's running state, carried from one frame to the next on the speaker's machine.</summary>
        public struct State
        {
            public int Predictor;
            public int Index;
        }

        /// <summary>Codes <see cref="FrameSamples"/> samples (-1..1) into <paramref name="frame"/> (<see cref="FrameBytes"/> long).</summary>
        public static void Encode(float[] samples, ushort sequence, ref State state, byte[] frame)
        {
            frame[0] = (byte)sequence;
            frame[1] = (byte)(sequence >> 8);
            frame[2] = (byte)state.Predictor;
            frame[3] = (byte)(state.Predictor >> 8);
            frame[4] = (byte)state.Index;
            for (int i = 0; i < FrameSamples; i += 2)
            {
                int low = EncodeSample(samples[i], ref state);
                int high = EncodeSample(samples[i + 1], ref state);
                frame[HeaderBytes + i / 2] = (byte)(low | (high << 4));
            }
        }

        /// <summary>
        /// Decodes a frame into <paramref name="samples"/>; false (and nothing written) for anything that is not a well-formed frame, which is
        /// all a relaying server checks.
        /// </summary>
        public static bool TryDecode(byte[] frame, float[] samples, out ushort sequence)
        {
            sequence = 0;
            if (!IsWellFormed(frame))
            {
                return false;
            }
            sequence = (ushort)(frame[0] | (frame[1] << 8));
            int predictor = (short)(frame[2] | (frame[3] << 8));
            int index = frame[4];
            for (int i = 0; i < FrameSamples; i += 2)
            {
                byte both = frame[HeaderBytes + i / 2];
                samples[i] = DecodeSample(both & 0x0F, ref predictor, ref index) / 32768.0f;
                samples[i + 1] = DecodeSample(both >> 4, ref predictor, ref index) / 32768.0f;
            }
            return true;
        }

        public static bool IsWellFormed(byte[] frame) => frame != null && frame.Length == FrameBytes && frame[4] <= MaxStepIndex;

        private static int EncodeSample(float sample, ref State state)
        {
            int value = Mathf.Clamp(Mathf.RoundToInt(sample * 32767.0f), short.MinValue, short.MaxValue);
            int difference = value - state.Predictor;
            int code = 0;
            if (difference < 0)
            {
                code = 8;
                difference = -difference;
            }
            int step = StepSizes[state.Index];
            if (difference >= step)
            {
                code |= 4;
                difference -= step;
            }
            if (difference >= step >> 1)
            {
                code |= 2;
                difference -= step >> 1;
            }
            if (difference >= step >> 2)
            {
                code |= 1;
            }
            // The coder follows the decoder exactly, so the two never drift apart.
            DecodeSample(code, ref state.Predictor, ref state.Index);
            return code;
        }

        private static int DecodeSample(int code, ref int predictor, ref int index)
        {
            int step = StepSizes[index];
            int delta = step >> 3;
            if ((code & 4) != 0) delta += step;
            if ((code & 2) != 0) delta += step >> 1;
            if ((code & 1) != 0) delta += step >> 2;
            predictor = Mathf.Clamp((code & 8) != 0 ? predictor - delta : predictor + delta, short.MinValue, short.MaxValue);
            index = Mathf.Clamp(index + IndexSteps[code], 0, MaxStepIndex);
            return predictor;
        }
    }
}
