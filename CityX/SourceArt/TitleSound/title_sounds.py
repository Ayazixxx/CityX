# THE TITLE'S SOUNDS, made from nothing (owner, 2026-11-11, of two bought sounds: "the sound effects dont sound good. can you try to generate
# your own with the same idea. 1 basic neon sign/city rooftop ambience (no signs of life in the city) and 2 florescent sign flickering
# noises"). Headless, in Blender's Python (numpy, wave):
#   blender --background --factory-startup --python title_sounds.py [-- --pictures <dir>]
#
# THE ROOFTOP AT NIGHT, a loop of LOOP_SECONDS: the sign's transformer humming at the mains' 60 Hz and its harmonics (120 Hz the strongest, as
# a magnetic ballast's is), a faint sizzle of the gas in the tube pulsing at 120 Hz, wind over the roof in slow gusts with a soft whistle,
# and the city's air far below, a deep rush: no traffic, no voices, nothing alive. It loops with no seam because everything in it is periodic
# over the loop: every tone and every swell has a whole number of cycles in it, and the noise is shaped in the frequency domain (a circular
# filter), so its end runs straight on into its start.
#
# TWO FLUORESCENT FLICKERS: a tube failing to hold its strike. Each burst of light is the ballast's 120 Hz buzz (a square-ish tone) with the
# arc's crackle on it (noise chopped at 120 Hz), and a "tink" where the tube catches or drops (a click and a short metallic ring); between
# bursts, silence but for the hum. Each has its own rhythm of bursts. The game plays one at even odds as each fault of the sign begins.
#
# Written as 16-bit stereo WAV at RATE into Assets/CityX/UI/Audio; the game sets how loud each plays.
import math
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "CityX", "UI", "Audio"))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

RATE = 48000
LOOP_SECONDS = 16.0   # long enough not to be heard coming round, short enough to keep small (3 MB)
MAINS = 60.0          # the US mains: a ballast hums at twice it
PEAK = 0.85
SEED = 20261111


# --- tools -------------------------------------------------------------------------------------------------------------------------------------

def shaped_noise(n, rng, shape):
    """White noise of n samples filtered by `shape(frequency in Hz) -> gain`, circularly: the result loops seamlessly over n."""
    spectrum = np.fft.rfft(rng.standard_normal(n))
    freqs = np.fft.rfftfreq(n, 1.0 / RATE)
    spectrum *= shape(freqs)
    out = np.fft.irfft(spectrum, n)
    return out / (np.abs(out).max() + 1e-12)


def band(lo, hi, slope=2.0):
    """A soft band-pass: flat between lo and hi, falling off as the `slope` power of the distance outside (in octaves)."""
    def shape(f):
        f = np.maximum(f, 1.0)
        below = np.clip(np.log2(lo / f), 0.0, None)
        above = np.clip(np.log2(f / hi), 0.0, None)
        return np.exp(-slope * (below ** 2 + above ** 2))
    return shape


def pink(f):
    return 1.0 / np.sqrt(np.maximum(f, 10.0))


def periodic_lfo(t, length, cycles, phases):
    """A smooth swell, 0 to 1, made of sines with whole numbers of cycles over `length`: it loops."""
    total = np.zeros_like(t)
    for c, p in zip(cycles, phases):
        total += np.sin(2.0 * math.pi * c * t / length + p)
    total /= len(cycles)
    return 0.5 + 0.5 * total


def one_pole_lowpass(x, cutoff):
    a = math.exp(-2.0 * math.pi * cutoff / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc = (1.0 - a) * x[i] + a * acc
        y[i] = acc
    return y


def write(name, left, right):
    stereo = np.stack([left, right], axis=1)
    stereo *= PEAK / (np.abs(stereo).max() + 1e-12)
    pcm = (np.clip(stereo, -1.0, 1.0) * 32767.0).astype("<i2")
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name)
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())
    print("wrote %s: %.2f s" % (path, len(left) / RATE))
    return stereo


# --- the rooftop at night --------------------------------------------------------------------------------------------------------------------

def rooftop(rng):
    n = int(LOOP_SECONDS * RATE)
    t = np.arange(n) / RATE

    # The transformer's hum: whole cycles of 60 Hz and its harmonics over the loop (60 x 16 s is whole), 120 Hz the strongest; its
    # loudness breathing a little, and a 0.5 Hz beat between two near tones (also whole over the loop).
    harmonics = {60.0: 0.35, 120.0: 1.0, 180.0: 0.45, 240.0: 0.35, 300.0: 0.12, 360.0: 0.16, 480.0: 0.07, 600.0: 0.04}
    hum = np.zeros(n)
    for f, a in harmonics.items():
        hum += a * np.sin(2.0 * math.pi * f * t + rng.uniform(0, 2 * math.pi))
    hum += 0.25 * np.sin(2.0 * math.pi * 120.5 * t)
    hum *= 0.85 + 0.15 * periodic_lfo(t, LOOP_SECONDS, [3, 7], [0.4, 2.1])
    hum /= np.abs(hum).max()

    # The gas in the tube: a thin high sizzle, pulsing at 120 Hz (the arc strikes twice a mains cycle).
    pulse = 0.55 + 0.45 * np.abs(np.sin(2.0 * math.pi * MAINS * t)) ** 3
    sizzle_l = shaped_noise(n, rng, band(3000.0, 9000.0, 1.5)) * pulse
    sizzle_r = shaped_noise(n, rng, band(3000.0, 9000.0, 1.5)) * pulse

    # Wind over the roof: a broad rush in slow gusts, each side its own air, and a soft whistle that comes with the strongest gusts.
    gust = periodic_lfo(t, LOOP_SECONDS, [1, 2, 5], [0.0, 1.3, 2.9]) ** 1.6
    wind_l = shaped_noise(n, rng, lambda f: pink(f) * band(60.0, 900.0, 1.2)(f))
    wind_r = shaped_noise(n, rng, lambda f: pink(f) * band(60.0, 900.0, 1.2)(f))
    whistle_low = shaped_noise(n, rng, band(650.0, 720.0, 6.0))
    whistle_high = shaped_noise(n, rng, band(980.0, 1060.0, 6.0))
    sway = periodic_lfo(t, LOOP_SECONDS, [3, 4], [0.7, 0.2])
    whistle = (whistle_low * sway + whistle_high * (1.0 - sway)) * gust ** 2

    # The city far below: a deep, steady rush of air.
    city_l = shaped_noise(n, rng, lambda f: pink(f) * band(25.0, 160.0, 1.0)(f))
    city_r = shaped_noise(n, rng, lambda f: pink(f) * band(25.0, 160.0, 1.0)(f))

    left = 0.30 * hum + 0.035 * sizzle_l + (0.25 + 0.55 * gust) * wind_l * 0.55 + 0.06 * whistle + 0.30 * city_l
    right = 0.30 * hum + 0.035 * sizzle_r + (0.25 + 0.55 * gust) * wind_r * 0.55 + 0.06 * whistle + 0.30 * city_r
    return left, right


# --- the fluorescent flickers ----------------------------------------------------------------------------------------------------------------

def ballast_buzz(t, rng):
    """The tube lit: a square-ish 120 Hz buzz (odd harmonics, softened), and the arc's crackle on it."""
    tone = np.zeros_like(t)
    for k in range(1, 16, 2):
        tone += np.sin(2.0 * math.pi * 120.0 * k * t) / k ** 1.3
    tone += 0.3 * np.sin(2.0 * math.pi * 60.0 * t)
    chop = (np.sin(2.0 * math.pi * 120.0 * t) > 0.55).astype(float)
    crackle = rng.standard_normal(len(t))
    crackle = crackle - one_pole_lowpass(crackle, 900.0)   # high-passed: the bright fizz of the arc
    return tone / np.abs(tone).max() * 0.6 + crackle * chop * 0.35


def tink(n, at, rng, strength):
    """Where the tube catches or drops: a click, and a short metallic ring off the fitting."""
    out = np.zeros(n)
    i = int(at * RATE)
    length = int(0.06 * RATE)
    if i >= n:
        return out
    m = min(length, n - i)
    k = np.arange(m) / RATE
    click = rng.standard_normal(m) * np.exp(-k / 0.0018)
    ring_f = rng.uniform(1800.0, 3400.0)
    ring = np.sin(2.0 * math.pi * ring_f * k) * np.exp(-k / 0.012) * 0.6
    out[i:i + m] = (click + ring) * strength
    return out


def flicker(rng, bursts, length):
    """Bursts of light as (start, duration, strength): the buzz in each, faded in and out over a few ms; a tink at each edge."""
    n = int(length * RATE)
    t = np.arange(n) / RATE
    buzz = ballast_buzz(t, rng)
    envelope = np.zeros(n)
    edges = np.zeros(n)
    for start, duration, strength in bursts:
        i0, i1 = int(start * RATE), min(n, int((start + duration) * RATE))
        ramp = int(0.003 * RATE)
        env = np.ones(i1 - i0) * strength
        env[:ramp] *= np.linspace(0.0, 1.0, min(ramp, len(env)))[:len(env[:ramp])]
        env[-ramp:] *= np.linspace(1.0, 0.0, min(ramp, len(env)))[:len(env[-ramp:])]
        # A long burst steadies: its crackle settles as it goes.
        envelope[i0:i1] = np.maximum(envelope[i0:i1], env)
        edges += tink(n, start, rng, 0.9 * strength)
        edges += tink(n, start + duration, rng, 0.5 * strength)
    left = buzz * envelope + edges
    right = buzz * envelope * 0.92 + np.roll(edges, int(0.0004 * RATE))
    fade = int(0.01 * RATE)
    for side in (left, right):
        side[-fade:] *= np.linspace(1.0, 0.0, fade)
    return left, right


# --- pictures, to look at what cannot be heard here ------------------------------------------------------------------------------------------

def picture(name, stereo, out_dir):
    import bpy
    mono = stereo.mean(axis=1)
    w, h = 1000, 400
    img = np.zeros((h, w, 4), dtype=np.float32)
    img[..., 3] = 1.0
    # Top: the waveform's envelope. Bottom: a spectrogram, 0 to 8 kHz, log loudness.
    top = h // 3
    per = max(1, len(mono) // w)
    for x in range(w):
        seg = mono[x * per:(x + 1) * per]
        if len(seg) == 0:
            continue
        lo, hi = seg.min(), seg.max()
        y0, y1 = int((1 - (hi + 1) / 2) * top), int((1 - (lo + 1) / 2) * top)
        img[y0:y1 + 1, x, :3] = (0.4, 0.9, 0.5)
    win = 2048
    hop = max(1, (len(mono) - win) // w)
    rows = h - top
    for x in range(w):
        seg = mono[x * hop:x * hop + win]
        if len(seg) < win:
            break
        mag = np.abs(np.fft.rfft(seg * np.hanning(win)))
        freqs = np.fft.rfftfreq(win, 1.0 / RATE)
        keep = freqs <= 8000.0
        col = np.log10(mag[keep] + 1e-6)
        col = np.interp(np.linspace(0, len(col) - 1, rows), np.arange(len(col)), col)
        col = np.clip((col + 3.0) / 4.0, 0.0, 1.0)
        img[top:, x, 0] = col[::-1]
        img[top:, x, 1] = col[::-1] ** 2
        img[top:, x, 2] = col[::-1] ** 4
    image = bpy.data.images.new(name, w, h, alpha=False)
    image.pixels = np.flipud(img).ravel()
    image.filepath_raw = os.path.join(out_dir, name + ".png")
    image.file_format = "PNG"
    image.save()


rng = np.random.default_rng(SEED)
made = {
    "TitleRooftopNight": write("TitleRooftopNight.wav", *rooftop(rng)),
    # A tube catching after a few tries, then holding a moment before it settles back.
    "TitleNeonFlicker1": write("TitleNeonFlicker1.wav", *flicker(rng, [(0.00, 0.05, 1.0), (0.12, 0.03, 0.8), (0.22, 0.08, 1.0),
                                                                      (0.36, 0.025, 0.7), (0.44, 0.30, 0.9)], 0.95)),
    # A sputter: quick weak strikes, one strong buzz, and a last tick as it drops.
    "TitleNeonFlicker2": write("TitleNeonFlicker2.wav", *flicker(rng, [(0.00, 0.02, 0.6), (0.05, 0.025, 0.7), (0.11, 0.02, 0.5),
                                                                      (0.15, 0.035, 0.9), (0.24, 0.02, 0.6), (0.30, 0.22, 1.0),
                                                                      (0.60, 0.015, 0.5)], 0.75)),
}
if "--pictures" in args:
    out_dir = args[args.index("--pictures") + 1]
    os.makedirs(out_dir, exist_ok=True)
    for name, stereo in made.items():
        picture(name, stereo, out_dir)
    # The loop's seam: the last and the first second side by side, which should run on as one.
    loop = made["TitleRooftopNight"]
    picture("TitleRooftopNight-seam", np.concatenate([loop[-RATE:], loop[:RATE]]), out_dir)
    print("pictures in", out_dir)
