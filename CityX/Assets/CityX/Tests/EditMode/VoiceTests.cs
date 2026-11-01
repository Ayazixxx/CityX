using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Proximity voice's pure parts (docs/VOICE.md): the codec keeps speech recognisable at a quarter of the size and a lost frame costs only
    /// itself; the hearing fades with distance and through walls; the jitter buffer waits a little, never falls far behind and plays silence
    /// when dry; the framer cuts and resamples the microphone's samples into codec frames.
    /// </summary>
    public sealed class VoiceTests
    {
        private static float[] Tone(int frame, float hz, float amplitude, int rate = VoiceCodec.SampleRate, int length = VoiceCodec.FrameSamples)
        {
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                samples[i] = amplitude * Mathf.Sin(2.0f * Mathf.PI * hz * (frame * length + i) / rate);
            }
            return samples;
        }

        [Test]
        public void ATwentyMillisecondFrameIs165BytesAndDecodesCloseToWhatWasSaid()
        {
            var state = new VoiceCodec.State();
            var frame = new byte[VoiceCodec.FrameBytes];
            var decoded = new float[VoiceCodec.FrameSamples];
            Assert.AreEqual(165, VoiceCodec.FrameBytes);
            for (int f = 0; f < 6; f++)
            {
                float[] said = Tone(f, 440.0f, 0.5f);
                VoiceCodec.Encode(said, (ushort)f, ref state, frame);
                Assert.IsTrue(VoiceCodec.TryDecode(frame, decoded, out ushort sequence));
                Assert.AreEqual(f, sequence);
                if (f < 2)
                {
                    continue;   // the coder's step size settles over the first few milliseconds
                }
                float error = 0.0f;
                for (int i = 0; i < said.Length; i++)
                {
                    error += (said[i] - decoded[i]) * (said[i] - decoded[i]);
                }
                Assert.Less(Mathf.Sqrt(error / said.Length), 0.03f, $"frame {f} decoded too far from what was said");
            }
        }

        [Test]
        public void AFrameDecodesTheSameWithoutTheOnesBeforeIt()
        {
            var state = new VoiceCodec.State();
            var frames = new byte[3][];
            for (int f = 0; f < 3; f++)
            {
                frames[f] = new byte[VoiceCodec.FrameBytes];
                VoiceCodec.Encode(Tone(f, 300.0f, 0.4f), (ushort)f, ref state, frames[f]);
            }
            var inOrder = new float[VoiceCodec.FrameSamples];
            var alone = new float[VoiceCodec.FrameSamples];
            VoiceCodec.TryDecode(frames[0], inOrder, out _);
            VoiceCodec.TryDecode(frames[1], inOrder, out _);
            VoiceCodec.TryDecode(frames[2], inOrder, out _);
            VoiceCodec.TryDecode(frames[2], alone, out _);
            CollectionAssert.AreEqual(inOrder, alone, "a lost frame changed the ones after it");
        }

        [Test]
        public void ANonsenseFrameIsRefused()
        {
            var samples = new float[VoiceCodec.FrameSamples];
            Assert.IsFalse(VoiceCodec.TryDecode(null, samples, out _));
            Assert.IsFalse(VoiceCodec.TryDecode(new byte[VoiceCodec.FrameBytes - 1], samples, out _), "a short frame");
            var badStep = new byte[VoiceCodec.FrameBytes];
            badStep[4] = 200;
            Assert.IsFalse(VoiceCodec.TryDecode(badStep, samples, out _), "a step index past the table");
        }

        [Test]
        public void AVoiceIsFullCloseFadesWithDistanceAndIsGoneAtTheRange()
        {
            Assert.AreEqual(1.0f, VoiceHearing.VolumeAt(2.0f, 0), 1e-4f);
            float near = VoiceHearing.VolumeAt(10.0f, 0), far = VoiceHearing.VolumeAt(30.0f, 0);
            Assert.Greater(near, far);
            Assert.Greater(far, 0.0f);
            Assert.AreEqual(0.0f, VoiceHearing.VolumeAt(VoiceHearing.RangeMetres, 0));
            Assert.AreEqual(0.0f, VoiceHearing.VolumeAt(500.0f, 0));
        }

        [Test]
        public void EveryWallMakesAVoiceQuieterAndDuller()
        {
            Assert.Less(VoiceHearing.VolumeAt(6.0f, 1), VoiceHearing.VolumeAt(6.0f, 0));
            Assert.Less(VoiceHearing.VolumeAt(6.0f, 2), VoiceHearing.VolumeAt(6.0f, 1));
            Assert.Less(VoiceHearing.CutoffFor(1), VoiceHearing.CutoffFor(0));
            Assert.Less(VoiceHearing.CutoffFor(2), VoiceHearing.CutoffFor(1));
            Assert.AreEqual(VoiceHearing.VolumeAt(6.0f, VoiceHearing.MaxWalls), VoiceHearing.VolumeAt(6.0f, VoiceHearing.MaxWalls + 4), "walls past the last counted made it quieter");
        }

        [Test]
        public void TheBufferWaitsThreeFramesThenPlaysInOrderAndGoesSilentWhenDry()
        {
            var buffer = new VoiceJitterBuffer();
            var frame = new float[VoiceCodec.FrameSamples];
            var output = new float[VoiceCodec.FrameSamples];
            for (int i = 0; i < frame.Length; i++) frame[i] = i / 1000.0f;
            buffer.Write(frame, frame.Length);
            buffer.Read(output);
            Assert.AreEqual(0.0f, output[10], "played before three frames were waiting");
            buffer.Write(frame, frame.Length);
            buffer.Write(frame, frame.Length);
            buffer.Read(output);
            Assert.AreEqual(frame[10], output[10], 1e-6f, "did not play once three frames were waiting");
            buffer.Read(output);
            buffer.Read(output);
            Assert.AreEqual(0, buffer.Waiting);
            buffer.Read(output);
            Assert.AreEqual(0.0f, output[10], "run dry, it did not play silence");
        }

        [Test]
        public void TheBufferNeverFallsMoreThanAFifthOfASecondBehind()
        {
            var buffer = new VoiceJitterBuffer();
            var frame = new float[VoiceCodec.FrameSamples];
            for (int i = 0; i < 100; i++)
            {
                buffer.Write(frame, frame.Length);
            }
            Assert.LessOrEqual(buffer.Waiting, VoiceJitterBuffer.MaxSamples);
        }

        [Test]
        public void TheFramerCutsTheMicrophoneIntoFramesAndResamplesA48KilohertzOne()
        {
            var framer = new VoiceFramer();
            int frames = 0;
            framer.FrameReady += _ => frames++;
            var block = new float[160];
            for (int i = 0; i < 100; i++) framer.Push(block, block.Length);   // one second at 16 kHz
            Assert.AreEqual(VoiceCodec.SampleRate / VoiceCodec.FrameSamples, frames, 1, "16 kHz did not give 50 frames a second");

            // The same tone recorded at 48 kHz comes out as the 16 kHz tone.
            var resampled = new VoiceFramer { InputRate = 48000 };
            float[] first = null;
            resampled.FrameReady += f => { if (first == null) first = (float[])f.Clone(); };
            float[] recorded = Tone(0, 200.0f, 0.5f, 48000, 48000);
            resampled.Push(recorded, recorded.Length);
            Assert.IsNotNull(first, "a second at 48 kHz gave no frame");
            float[] expected = Tone(0, 200.0f, 0.5f);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], first[i], 0.01f, $"sample {i} of the resampled tone");
            }
        }
    }
}
