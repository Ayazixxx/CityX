"""
Speeds a sound up to exact lengths WITHOUT changing its pitch (owner, 2026-10-14: "speed it up so its 2.8 seconds when magazine is empty and
2.17 seconds with rounds left, but try to maintain the pitch even when sped up"), with Blender's own audio library (aud) and numpy.

WSOLA (waveform-similarity overlap-add, Verhelst and Roelands 1993): the output is built from short overlapping windows of the input, taken
from further along it than they are laid down (so the sound is shorter), each nudged by up to a few milliseconds to where it best matches the
waveform the previous window would have gone on with, so the windows join in phase and the pitch is the original's. Unlike a phase vocoder,
it keeps clicks sharp, which is most of a reload. Both channels use the same nudges, so the stereo image stays put.

Usage: blender --background --factory-startup --python stretch_reload.py -- <source> <out dir> <name>=<seconds> [<name>=<seconds> ...]
The AK-47 (lengths = the reload motion segments, 130 and 168 frames at 60 fps), from the repository root:
  "C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/AK47/stretch_reload.py --
    CityX/SourceArt/AK47/ak47_reload_sound_source.mp3 CityX/Assets/CityX/Weapons/Audio ak47_reload_tactical=2.1667 ak47_reload_empty=2.8
It prints, for each output, its length, the clicks (onsets) against where the source ones should land, and the ringing frequencies against
the source (they must not move: a plain speed-up would multiply them by the speed).
"""
import os
import sys

import aud
import numpy as np

FRAME = 1024          # samples per window (23 ms at 44.1 kHz)
HOP_OUT = FRAME // 2  # windows are laid down this far apart
TOLERANCE = 384       # how far a window may be nudged to match, samples (8.7 ms)
PEAK = 0.89           # the source clips a little (peak 1.02): both outputs are scaled by the same gain to this peak


def wsola(x, speed):
    """x: (samples, channels) float array; returns it speed times shorter at the same pitch."""
    n = x.shape[0]
    out_len = int(round(n / speed))
    window = np.hanning(FRAME).astype(np.float64)
    mono = x.mean(axis=1)
    pad = FRAME + TOLERANCE * 2 + HOP_OUT
    xp = np.pad(x, ((TOLERANCE, pad), (0, 0)))
    mp = np.pad(mono, (TOLERANCE, pad))
    y = np.zeros((out_len + FRAME, x.shape[1]))
    norm = np.zeros(out_len + FRAME)
    hop_in = HOP_OUT * speed
    previous = 0            # where the last window was taken from (in the padded input, without the tolerance offset)
    k = 0
    while k * HOP_OUT < out_len:
        nominal = int(round(k * hop_in))
        if k == 0:
            chosen = 0
        else:
            # The waveform the previous window would have gone on with, and the stretch of input around the nominal place to find it in.
            natural = mp[TOLERANCE + previous + HOP_OUT:TOLERANCE + previous + HOP_OUT + FRAME]
            lo = TOLERANCE + nominal - TOLERANCE
            region = mp[lo:lo + FRAME + 2 * TOLERANCE]
            if len(region) < FRAME + 2 * TOLERANCE or not np.any(natural):
                chosen = nominal
            else:
                corr = np.correlate(region, natural, mode="valid")          # one value per nudge, -TOLERANCE..+TOLERANCE
                energy = np.sqrt(np.convolve(region ** 2, np.ones(FRAME), mode="valid")) + 1e-12
                chosen = nominal - TOLERANCE + int(np.argmax(corr / energy))
        chosen = max(0, chosen)
        segment = xp[TOLERANCE + chosen:TOLERANCE + chosen + FRAME]
        start = k * HOP_OUT
        y[start:start + FRAME] += segment * window[:, None]
        norm[start:start + FRAME] += window
        previous = chosen
        k += 1
    norm[norm < 1e-6] = 1.0
    return (y / norm[:, None])[:out_len]


def centroid(mono, rate):
    spec = np.abs(np.fft.rfft(mono * np.hanning(len(mono))))
    freqs = np.fft.rfftfreq(len(mono), 1.0 / rate)
    return float(np.sum(freqs * spec) / np.sum(spec))


def onsets(mono, rate):
    hop, win = int(rate * 0.005), int(rate * 0.010)
    frames = (len(mono) - win) // hop
    db = 20 * np.log10(np.array([np.sqrt(np.mean(mono[i * hop:i * hop + win] ** 2)) for i in range(frames)]) + 1e-9)
    found = []
    for i in range(3, frames):
        if db[i] - db[i - 3] > 9 and db[i] > db.max() - 35:
            t = i * hop / rate
            if not found or t - found[-1] > 0.06:
                found.append(t)
    return found


args = sys.argv[sys.argv.index("--") + 1:]
source, out_dir, targets = args[0], args[1], args[2:]
sound = aud.Sound(source)
rate, channels = sound.specs
rate = int(rate)
x = sound.data().astype(np.float64)
if x.ndim == 1:
    x = x[:, None]
print(f"source {x.shape[0] / rate:.3f} s, {x.shape[1]} channels, {rate} Hz, peak {np.abs(x).max():.3f}, centroid {centroid(x.mean(axis=1), rate):.0f} Hz")
print("source onsets " + " ".join(f"{t:.3f}" for t in onsets(x.mean(axis=1), rate)))
results = []
for target in targets:
    name, seconds = target.split("=")
    seconds = float(seconds)
    speed = x.shape[0] / (seconds * rate)
    y = wsola(x, speed)
    results.append((name, seconds, speed, y))
gain = PEAK / max(np.abs(y).max() for _, _, _, y in results)
os.makedirs(out_dir, exist_ok=True)
for name, seconds, speed, y in results:
    y = (y * gain).astype(np.float32)
    path = os.path.join(out_dir, name + ".ogg")
    buffered = aud.Sound.buffer(np.ascontiguousarray(y), rate)
    buffered.write(path, rate=rate, channels=aud.CHANNELS_STEREO if y.shape[1] == 2 else aud.CHANNELS_MONO,
                   format=aud.FORMAT_S16, container=aud.CONTAINER_OGG, codec=aud.CODEC_VORBIS, bitrate=96000)
    back = aud.Sound(path).data().astype(np.float64)
    if back.ndim == 1:
        back = back[:, None]
    m = back.mean(axis=1)
    print(f"{name}: speed {speed:.3f}, written {back.shape[0] / rate:.3f} s (wanted {seconds:.3f}), peak {np.abs(back).max():.3f}, "
          f"centroid {centroid(m, rate):.0f} Hz, {os.path.getsize(path) // 1024} KB")
    print(f"{name} onsets " + " ".join(f"{t:.3f}" for t in onsets(m, rate)) + "   (source onsets / speed: " +
          " ".join(f"{t / speed:.3f}" for t in onsets(x.mean(axis=1), rate)) + ")")
    # For comparison only: the same length by simply playing it faster (what AudioSource.pitch does), which raises the pitch.
    idx = np.linspace(0, x.shape[0] - 1, back.shape[0])
    naive = np.interp(idx, np.arange(x.shape[0]), x.mean(axis=1))
    print(f"{name}: played faster instead, the centroid would be {centroid(naive, rate):.0f} Hz")


def resonances(mono, rate, count=6):
    """The strongest ringing frequencies (peaks of the long-term spectrum, 300 Hz to 12 kHz), a pitch that time-stretching must not move."""
    seg = 4096
    frames = [mono[i:i + seg] * np.hanning(seg) for i in range(0, len(mono) - seg, seg // 2)]
    spec = np.mean([np.abs(np.fft.rfft(f)) for f in frames], axis=0)
    freqs = np.fft.rfftfreq(seg, 1.0 / rate)
    band = (freqs > 300) & (freqs < 12000)
    s = np.where(band, spec, 0.0)
    peaks = [i for i in range(2, len(s) - 2) if s[i] > s[i - 1] and s[i] >= s[i + 1] and s[i] > s[i - 2] and s[i] >= s[i + 2]]
    peaks.sort(key=lambda i: -s[i])
    return sorted(freqs[i] for i in peaks[:count])


print("resonances, source:   " + " ".join(f"{f:.0f}" for f in resonances(x.mean(axis=1), rate)))
for name, seconds, speed, _ in results:
    back = aud.Sound(os.path.join(out_dir, name + ".ogg")).data().astype(np.float64)
    m = back.mean(axis=1) if back.ndim > 1 else back
    print(f"resonances, {name}: " + " ".join(f"{f:.0f}" for f in resonances(m, rate)) + f"   (played faster they would be x{speed:.2f})")
