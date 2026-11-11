# THE TITLE'S SOUNDS, from the owner's files here, headless:
#   blender --background --factory-startup --python title_sounds.py
# Owner, 2026-11-11: neonlightambienceloop.mp3 "make this play at low volume on the title screen and make it a clean loop"; flicker1.mp3 and
# flicker2.mp3 "make one of these two sound effects play when the magenta neon sign part flickers, make it 50/50 which one plays".
#
# An mp3 carries silence at both ends (the encoder's padding, ~45 ms here), which is a gap in a loop: each sound is cut to where it is
# heard. The hum's end is then crossfaded into its start (equal power, CROSSFADE_SECONDS) so it runs round with no seam and no click. All
# are brought up to one peak (the files are very quiet) and written as 16-bit WAV: the game sets how loud each plays.
import os

import aud
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "CityX", "UI", "Audio"))
HEARD = 1e-4               # quieter than this, a sample is the padding
PEAK = 0.8
CROSSFADE_SECONDS = 0.3


def load(name):
    sound = aud.Sound(os.path.join(HERE, name))
    rate = int(sound.specs[0])
    data = sound.data().astype(np.float64)
    loud = np.nonzero(np.abs(data).max(axis=1) > HEARD)[0]
    return data[loud[0]:loud[-1] + 1], rate


def write(data, rate, name):
    data = data * (PEAK / max(1e-9, np.abs(data).max()))
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name)
    aud.Sound.buffer(data.astype(np.float32), rate).write(path, rate, aud.CHANNELS_STEREO, aud.FORMAT_S16,
                                                          container=aud.CONTAINER_WAV, codec=aud.CODEC_PCM)
    print("wrote %s: %.2f s" % (path, len(data) / rate))


def looped(data, rate):
    """The end laid over the start, fading out as the start fades in: the last sample runs straight on into the first."""
    n = int(CROSSFADE_SECONDS * rate)
    body, tail = data[:-n].copy(), data[-n:]
    t = (np.arange(n) / n)[:, None]
    body[:n] = body[:n] * np.sqrt(t) + tail * np.sqrt(1.0 - t)
    return body


hum, rate = load("neonlightambienceloop.mp3")
write(looped(hum, rate), rate, "TitleNeonHum.wav")
for i in (1, 2):
    flicker, rate = load("flicker%d.mp3" % i)
    write(flicker, rate, "TitleNeonFlicker%d.wav" % i)
