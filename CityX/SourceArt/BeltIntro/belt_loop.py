# Blender, headless: cuts the conveyor belt's running sound out of the owner's recording as a loop with no seam (docs/BELT_INTRO.md).
#   blender --background --factory-startup --python belt_loop.py -- ConveyerBeltNoise.mp3 ../../Assets/CityX/UI/Audio/conveyor_belt_loop.ogg
#
# The recording fades in over its first four seconds, longer than the belt takes to roll a body in, and has no end to come back from;
# so the loop is its steady part, the last second of it laid over the first so that the end runs into the beginning.
import sys
import aud
import numpy as np

STEADY_FROM = 4.5   # seconds: past the recording's own fade-in
TAIL = 0.1          # seconds left off its end
OVERLAP = 1.0       # seconds of the end laid over the beginning
PEAK = 0.9          # the loudest sample of the loop
BITRATE = 96000

source, out = sys.argv[sys.argv.index("--") + 1:][:2]
sound = aud.Sound(source)
rate, channels = sound.specs
rate = int(rate)
data = sound.data()
steady = data[int(STEADY_FROM * rate):data.shape[0] - int(TAIL * rate)]
overlap = int(OVERLAP * rate)
length = steady.shape[0] - overlap

# Equal power, so the hum is no quieter where the two are mixed.
across = np.linspace(0.0, np.pi / 2.0, overlap, dtype=np.float32)[:, None]
loop = steady[:length].copy()
loop[:overlap] = steady[:overlap] * np.sin(across) + steady[length:] * np.cos(across)
loop *= PEAK / np.abs(loop).max()

aud.Sound.buffer(np.ascontiguousarray(loop, dtype=np.float32), rate).write(
    out, rate=rate, channels=channels, format=aud.FORMAT_S16, container=aud.CONTAINER_OGG, codec=aud.CODEC_VORBIS, bitrate=BITRATE)
print(f"LOOP {length / rate:.2f} s written to {out}")
