# Blender, headless: cuts the conveyor belt's running sound out of the owner's recording as a loop with no seam (docs/BELT_INTRO.md).
#   blender --background --factory-startup --python belt_loop.py -- ConveyerBelt2.mp3 ../../Assets/CityX/UI/Audio/conveyor_belt_loop.ogg
#
# The recording fades in and out at its ends, and runs for over a minute; the loop is twenty seconds of its steady middle, the last
# second of that laid over the first so that the end runs into the beginning.
import sys
import aud
import numpy as np

STEADY_FROM = 3.0   # seconds: past the recording's own fade-in
LENGTH = 20.0       # seconds of loop: enough not to be heard repeating, small enough to ship
OVERLAP = 1.0       # seconds of the end laid over the beginning
PEAK = 0.9          # the loudest sample of the loop (owner, 2026-11-10, of matching the first recording's loudness: "no use the volume of the new loop")
BITRATE = 96000

source, out = sys.argv[sys.argv.index("--") + 1:][:2]
sound = aud.Sound(source)
rate, channels = sound.specs
rate = int(rate)
data = sound.data()
start = int(STEADY_FROM * rate)
length = int(LENGTH * rate)
overlap = int(OVERLAP * rate)
steady = data[start:start + length + overlap]

# Equal power, so the hum is no quieter where the two are mixed.
across = np.linspace(0.0, np.pi / 2.0, overlap, dtype=np.float32)[:, None]
loop = steady[:length].copy()
loop[:overlap] = steady[:overlap] * np.sin(across) + steady[length:] * np.cos(across)
loop *= PEAK / np.abs(loop).max()

aud.Sound.buffer(np.ascontiguousarray(loop, dtype=np.float32), rate).write(
    out, rate=rate, channels=channels, format=aud.FORMAT_S16, container=aud.CONTAINER_OGG, codec=aud.CODEC_VORBIS, bitrate=BITRATE)
print(f"LOOP {length / rate:.2f} s written to {out}")
