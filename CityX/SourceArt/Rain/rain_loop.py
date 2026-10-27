# THE OUTDOOR RAIN'S SOUND, headless (owner, 2026-10-26: "take the audio from this and replace our outdoor rain noise with this. make it a
# clean loop"), with Blender's own audio library (aud, which reads the video's sound and writes Ogg Vorbis; no ffmpeg on this machine):
#   blender --background --factory-startup --python rain_loop.py -- <video or sound> <out .ogg> [<match loudness of this sound>]
#   1. The sound read whole; TRIM seconds cut from each end (an encoder's priming and its last frame).
#   2. A CLEAN LOOP by crossfading: the loop is the first L seconds, and its first CROSSFADE seconds are blended (equal power, for noise)
#      with the CROSSFADE seconds that followed L in the recording. Played round, the end runs straight on into what came after it, which
#      the start has been faded into: no click, no dip, no change of texture at the seam.
#   3. Levelled to the loudness (RMS) of the sound it replaces, so the game's rain volumes (Weather) still fit; never past PEAK.
import os
import sys

import aud
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
SOURCE, OUT = os.path.abspath(args[0]), os.path.abspath(args[1])
MATCH = os.path.abspath(args[2]) if len(args) > 2 else None

TRIM = 0.05          # seconds off each end
CROSSFADE = 0.75     # seconds the seam is blended over
PEAK = 0.9           # the loudest a sample may be after levelling
BITRATE = 112000     # Ogg Vorbis bits per second: rain is noise, which needs more than speech


def rms(samples):
    return float(np.sqrt(np.mean(np.square(samples))))


sound = aud.Sound(SOURCE)
rate = int(sound.specs[0])
data = np.asarray(sound.data(), dtype=np.float64)
if data.ndim == 1:
    data = data[:, None]
trim = int(TRIM * rate)
data = data[trim:len(data) - trim]
fade = int(CROSSFADE * rate)
length = len(data) - fade
if length <= fade:
    raise SystemExit("the sound is too short to loop with a %.2f s crossfade" % CROSSFADE)

t = (np.arange(fade) + 0.5) / fade
fade_in = np.sqrt(t)[:, None]
fade_out = np.sqrt(1.0 - t)[:, None]
loop = data[:length].copy()
loop[:fade] = data[:fade] * fade_in + data[length:length + fade] * fade_out

gain = 1.0
if MATCH:
    reference = np.asarray(aud.Sound(MATCH).data(), dtype=np.float64)
    gain = rms(reference) / max(rms(loop), 1e-9)
    gain = min(gain, PEAK / max(np.abs(loop).max(), 1e-9))
loop *= gain

seam = np.abs(loop[0] - loop[-1]).max()
step = np.percentile(np.abs(np.diff(loop, axis=0)).max(axis=1), 99.0)
print("loop: %.3f s, %d channels at %d Hz, crossfade %.2f s, gain %.2f (%.1f dB), rms %.4f, peak %.4f" % (
    length / rate, loop.shape[1], rate, CROSSFADE, gain, 20.0 * np.log10(gain), rms(loop), np.abs(loop).max()))
print("seam: the jump from the last sample to the first %.5f, against the 99th centile of sample-to-sample steps %.5f" % (seam, step))

os.makedirs(os.path.dirname(OUT), exist_ok=True)
out = aud.Sound.buffer(loop.astype(np.float32), rate)
out.write(OUT, rate=rate, channels=aud.CHANNELS_STEREO if loop.shape[1] == 2 else aud.CHANNELS_MONO, format=aud.FORMAT_FLOAT32,
          container=aud.CONTAINER_OGG, codec=aud.CODEC_VORBIS, bitrate=BITRATE)
print("WROTE %s: %d bytes" % (OUT, os.path.getsize(OUT)))
