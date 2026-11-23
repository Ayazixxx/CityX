# THE CHAIN'S SWING SOUNDS, CLEANED. Owner, 2026-11-22, with four recordings: "in chainswing1 and 2 ... there is some background wind hearable can you put some effect on
# that or do something to make that less noticeable". Run headless:
#   blender --background --factory-startup --python chain_sounds.py -- <folder with Chainswing1.mp3 .. chainswing4.mp3> <out folder>
# In those two the wind is a low rumble under everything and a long stretch of nothing but wind before the chain is heard (0.55 s of 0.89 in the first). So: the rumble
# is filtered out (a high-pass, same loudness above it), and the lead-in is cut to a short breath of air faded up into the chain. The other two are written as they are.
# Prints where each sound is loudest: the game starts a sound that long before the blow lands (Character/Combat/ChainSounds).
import os
import sys

import aud
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
SOURCE, OUT = args[0], args[1]
RUMBLE_BELOW_HZ = 280.0
LEAD_IN_SECONDS = 0.16
FADE_OUT_SECONDS = 0.04
# The chain is heard from here in each recording, seconds (read off its loudness above 800 Hz); None: nothing cut. The fourth has no wind, but its chain comes in so
# late (0.4 s) that it would be heard after the blow it is played for: its lead-in is cut too, its rumble left.
CHAIN_FROM = {"Chainswing1": 0.57, "Chainswing2": 0.28, "chainswing3": None, "chainswing4": 0.38}
WINDY = ("Chainswing1", "Chainswing2")


def high_pass(data, rate):
    """Everything under RUMBLE_BELOW_HZ taken out (a fourth-order roll-off, no shift in time)."""
    spectrum = np.fft.rfft(data, axis=0)
    hz = np.fft.rfftfreq(len(data), 1.0 / rate)
    gain = 1.0 / np.sqrt(1.0 + (RUMBLE_BELOW_HZ / np.maximum(hz, 1e-3)) ** 8)
    return np.fft.irfft(spectrum * gain[:, None], n=len(data), axis=0)


def loudest_at(data, rate):
    mono = np.abs(data.mean(axis=1))
    window = int(rate * 0.03)
    smooth = np.convolve(mono, np.ones(window) / window, mode="same")
    return float(np.argmax(smooth)) / rate


os.makedirs(OUT, exist_ok=True)
for index, name in enumerate(("Chainswing1", "Chainswing2", "chainswing3", "chainswing4")):
    sound = aud.Sound(os.path.join(SOURCE, name + ".mp3"))
    rate, channels = int(sound.specs[0]), int(sound.specs[1])
    data = np.array(sound.data(), dtype=np.float64)
    before = float(np.sqrt((data ** 2).mean()))
    start = CHAIN_FROM[name]
    if name in WINDY:
        data = high_pass(data, rate)
    if start is not None:
        first = max(0, int((start - LEAD_IN_SECONDS) * rate))
        data = data[first:]
        fade = int(LEAD_IN_SECONDS * rate)
        data[:fade] *= (np.linspace(0.0, 1.0, fade) ** 2)[:, None]
    tail = int(FADE_OUT_SECONDS * rate)
    data[-tail:] *= np.linspace(1.0, 0.0, tail)[:, None]
    path = os.path.join(OUT, "ChainSwing%d.ogg" % (index + 1))
    aud.Sound.buffer(data.astype(np.float32), rate).write(path, rate, channels, aud.FORMAT_S16, aud.CONTAINER_OGG, aud.CODEC_VORBIS, 160000)
    print("SOUND %s -> %s: %.2f s, loudest at %.2f s, level %.3f -> %.3f, %d bytes"
          % (name, os.path.basename(path), len(data) / rate, loudest_at(data, rate), before, float(np.sqrt((data ** 2).mean())), os.path.getsize(path)))
