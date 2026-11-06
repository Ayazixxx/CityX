# THE SWORD SPIN (key 2), posed: Link's Spin Attack (his up special in Smash Ultimate) on CityX's own skeleton, on the ground and in the air.
#
# Needs the rig (../Octaslash/octaslash_rig.py) and octaslash_pose.py's solver loaded first, with OCTASLASH_NO_BAKE set; spin_build_all.py
# does both, bakes, exports and renders check frames.
#
# THE MOVE, read frame by frame from SmashWiki's hitbox GIFs of Ultimate's grounded and aerial Spin Attack (one GIF frame per game frame; the
# clip's frame f is the game's frame f, so the hits land on SwordSpinMove's frames):
#   ground       0-4    a quick dip into a deep lunge, turned to the right, the sword cocked high over the right shoulder (a held charge
#                       stays here: ground_hold)
#                5-7    the release, whipping round to the left; on frame 7 (the first, strongest hit) the blade points straight ahead
#                7-39   the spin: low and wide, the sword arm straight out to the side with the blade on its line, the free arm out the
#                       other way; three turns to the left, fastest at the start (Smash's hitboxes: frames 7-39)
#                39-49  the last turn, slowing, the arm coming down into a deep lunge facing ahead with the blade low in front
#                49-69  held, settling;  69-80 rising a little, the blade lifting ahead (the game hands the body back on frame 74)
#   ground_hold  48 frames of the cocked pose, breathing, the blade trembling (looped while the key is held)
#   air          0-5    turned to the right, the sword arm out behind, knees tucked;  5-8 whipping round to the left
#                8-48   four and a half fast turns on an axis tipped forward (the blade low in front, high behind), Smash's blows at
#                       frames 8, 12, 16, 19, 22, 26, 31 and 38, the blade straight ahead or straight behind on each; the last turn swings
#                       the blade up into the launching cut (47-49)
#                49-58  held overhead;  58-91 lowered out behind as the turning dies away, back to the guard
#
# Poses are written in the character's own frame (Unity axes: x right, y up, z forward). The TURN is put on afterwards, frame by frame, about
# the upright through the feet (and, in the air, the tip of the axis too), so the whole body spins inside the animation: the game's own
# facing stays where it was, and another player's copy shows the spin from the animation alone.

import bpy
import math
from mathutils import Vector, Quaternion, Matrix

GROUND = (0, 80)
GROUND_HOLD = (80, 128)
AIR = (128, 220)

GROUND_CHARGE_FRAME = 4          # the cocked pose: where a held charge waits (SwordSpinMove.ChargeFrame)


# ---------------------------------------------------------------------------------------------------------------------------------------------
# THE TURN: degrees to the left (counter-clockwise seen from above) by frame, through the GIFs' landmarks, smooth and never turning back.

GROUND_YAW = [(0, 0.0), (2, -8.0), (4, -35.0), (5, -25.0), (6, 35.0), (7, 90.0), (11, 270.0), (15.5, 450.0), (21.5, 630.0), (26.5, 810.0),
              (33, 990.0), (39, 1170.0), (44, 1340.0), (49, 1440.0), (80, 1440.0)]

AIR_YAW = [(0, -90.0), (3, -100.0), (5, -75.0), (6, -10.0), (7, 45.0), (8.5, 90.0), (12.5, 270.0), (16.5, 450.0), (19.5, 630.0),
           (22.5, 810.0), (26.5, 990.0), (31.5, 1170.0), (38.5, 1350.0), (48, 1530.0), (60, 1650.0), (75, 1745.0), (92, 1800.0)]

# The air spin's axis tips forward this far (the blade passing low in front and high behind, as in Smash), easing in and out with the spin.
# Level since the owner, 2026-11-05: "hold the sword more horizontally when doing the spin ... mostly horizontally and very slightly tilted
# upward" (tipped, the blade swung from 16 degrees down to 31 up).
AIR_TILT = 0.0


def pchip(keys, x):
    """Monotone cubic through (x, y) keys (Fritsch-Carlson): smooth, and never overshooting a key, so the turn never runs backwards."""
    xs = [k[0] for k in keys]
    ys = [k[1] for k in keys]
    if x <= xs[0]:
        return ys[0]
    if x >= xs[-1]:
        return ys[-1]
    n = len(xs)
    h = [xs[i + 1] - xs[i] for i in range(n - 1)]
    d = [(ys[i + 1] - ys[i]) / h[i] for i in range(n - 1)]
    m = [0.0] * n
    m[0], m[-1] = d[0], d[-1]
    for i in range(1, n - 1):
        if d[i - 1] * d[i] <= 0.0:
            m[i] = 0.0
        else:
            w1, w2 = 2.0 * h[i] + h[i - 1], h[i] + 2.0 * h[i - 1]
            m[i] = (w1 + w2) / (w1 / d[i - 1] + w2 / d[i])
    for i in range(n - 1):
        if xs[i] <= x <= xs[i + 1]:
            t = (x - xs[i]) / h[i]
            t2, t3 = t * t, t * t * t
            return ((2 * t3 - 3 * t2 + 1) * ys[i] + (t3 - 2 * t2 + t) * h[i] * m[i] + (-2 * t3 + 3 * t2) * ys[i + 1]
                    + (t3 - t2) * h[i] * m[i + 1])
    return ys[-1]


def smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / float(b - a)))
    return t * t * (3.0 - 2.0 * t)


# ---------------------------------------------------------------------------------------------------------------------------------------------
# THE POSES (Unity axes, the character's own frame). The ankle sits 0.135 above the sole, so a foot is flat when hips.y + foot.y = 0.135.

ANKLE = 0.135


def legs_wide(hips_y, spread=0.30, stagger=0.15):
    """The spin's stance: wide and low, the left foot a little ahead, both knees out over the toes."""
    drop = ANKLE - hips_y
    return {
        "l_leg": {"foot": (-spread, drop, stagger), "knee": (-0.55, 0.35, 1.0), "toe": (-0.35, 0.0, 1.0), "space": "root"},
        "r_leg": {"foot": (spread, drop, -stagger), "knee": (0.55, 0.35, 1.0), "toe": (0.35, 0.0, 1.0), "space": "root"},
    }


def legs_lunge(hips_y, front=0.48, back=0.46):
    """A deep lunge: the left foot ahead and flat, the right leg behind with its knee low, on the ball of the foot."""
    drop = ANKLE - hips_y
    return {
        "l_leg": {"foot": (-0.12, drop, front), "knee": (-0.15, 0.45, 1.0), "toe": (-0.1, 0.0, 1.0), "space": "root"},
        "r_leg": {"foot": (0.16, drop + 0.04, -back), "knee": (0.05, -1.0, 0.25), "toe": (0.0, -0.75, 0.65), "space": "root"},
    }


def legs_tucked():
    """In the air: the left knee drawn up high in front, the right leg bent below."""
    return {
        "l_leg": {"foot": (-0.06, -0.36, 0.30), "knee": (0.0, 0.45, 1.0), "toe": (0.0, -0.55, 0.85), "space": "hips"},
        "r_leg": {"foot": (0.08, -0.62, -0.14), "knee": (0.05, -0.15, 1.0), "toe": (0.0, -0.85, 0.5), "space": "hips"},
    }


def stance():
    """Frame 0: the two-handed sword stance the game's idle holds (Octaslash's settle), so taking hold of the body is short."""
    return pose_settle()


def ground_dip():
    """Frame 2: sinking, turning to the right, the sword hand rising toward the right shoulder."""
    hy = 0.72
    P = {"hips": (0.0, hy, 0.0), "body": rot(p=10, y=-10), "spine": rot(p=4, y=-10), "neck": rot(p=-8, y=12), "head": rot(p=-6, y=8),
         "sword": {"hand": (0.18, -0.15, 0.40), "blade": (0.15, 0.85, 0.30), "space": "chest"},
         "free": {"hand": (0.25, -0.60, 0.40), "pole": (-0.6, -0.6, -0.4), "space": "chest"}}
    P.update(legs_lunge(hy, front=0.36, back=0.30))
    return P


def ground_cocked():
    """Frame 4, and the held charge: a deep lunge, the body wound to the right and leaning in, the sword hand up by the right side of the
    head with the blade laid back over the shoulder, pointing behind and up; the free hand reaching forward and down (GIF frames 3-4)."""
    hy = 0.58
    P = {"hips": (0.0, hy, -0.04), "body": rot(p=16, y=-15), "spine": rot(p=8, y=-22), "neck": rot(p=-14, y=24), "head": rot(p=-12, y=14),
         "sword": {"hand": (0.12, 0.18, 0.18), "blade": (0.80, 0.45, -0.60), "space": "chest"},
         "free": {"hand": (0.30, -0.62, 0.48), "pole": (-0.7, -0.5, -0.3), "space": "chest"}}
    P.update(legs_lunge(hy))
    return P


def ground_release():
    """Frame 6: unwinding; the sword arm swinging out to the side, the blade coming round level (GIF frame 5)."""
    hy = 0.56
    P = {"hips": (0.0, hy, 0.0), "body": rot(p=12), "spine": rot(p=5, y=14), "neck": rot(p=-8, y=-10), "head": rot(p=-6, y=-6),
         "sword": {"hand": (0.76, 0.18, 0.36), "blade": (0.85, 0.12, 0.55), "space": "chest"},
         "free": {"hand": (-0.70, -0.20, -0.10), "pole": (0.0, -1.0, -0.3), "space": "chest"}}
    P.update(legs_wide(hy, stagger=0.28))
    return P


# The blade rides this much higher than the arm through the turns (rise per unit out, about 8 degrees): level or a little up, never down
# (owner, 2026-10-18: "i want it either horizontally or slightly upward").
BLADE_RISE = 0.14


def spin_pose(frame, hips_y=0.55, lift=0.0, base_tilt=0.0, tilt_swing=0.0, yaw_deg=0.0):
    """The spin: low and wide, the sword arm straight out to the right with the blade carrying on its line, the free arm out the other way.
    `tilt_swing` raises the blade when it points behind and lowers it in front (it follows the turn, `yaw_deg`), as the air spin's disc
    does; the hips bob a little twice a turn."""
    yaw_rad = math.radians(yaw_deg)
    bob = 0.025 * math.sin(2.0 * yaw_rad)
    tilt = base_tilt - tilt_swing * math.sin(yaw_rad) + lift
    hy = hips_y + bob
    P = {"hips": (0.0, hy, 0.0), "body": rot(p=12), "spine": rot(p=4, y=10), "neck": rot(p=-10, y=-8), "head": rot(p=-6, y=-6),
         "sword": {"hand": (0.85, 0.05 + tilt * 0.4, 0.10), "blade": (1.0, tilt + BLADE_RISE, 0.16), "space": "chest"},
         "free": {"hand": (-0.80, -0.32, -0.15), "pole": (0.0, -1.0, -0.3), "space": "chest"}}
    P.update(legs_wide(hy))
    return P


def ground_settle_turn():
    """Frame 44: the last turn slowing, the arm coming down and forward, the legs opening into the lunge (GIF frames 42-45)."""
    hy = 0.56
    P = {"hips": (0.0, hy, 0.0), "body": rot(p=14), "spine": rot(p=6, y=8), "neck": rot(p=-12, y=-6), "head": rot(p=-10, y=-4),
         "sword": {"hand": (0.62, -0.45, 0.42), "blade": (0.75, -0.45, 0.55), "space": "chest"},
         "free": {"hand": (-0.65, -0.05, -0.35), "pole": (0.0, -1.0, 0.2), "space": "chest"}}
    P.update(legs_lunge(hy, front=0.42, back=0.40))
    return P


def ground_end(sink=0.0):
    """Frames 49-69: the deep lunge facing ahead, leaning in, the sword hand low in front with the blade pointing ahead and down, the free
    arm raised behind (GIF frames 48-69)."""
    hy = 0.50 - sink
    P = {"hips": (0.0, hy, -0.04), "body": rot(p=24, y=-12), "spine": rot(p=8, y=-8), "neck": rot(p=-22, y=10), "head": rot(p=-16, y=8),
         "sword": {"hand": (0.14, -0.66, 0.50), "blade": (0.18, -0.30, 1.0), "space": "root"},
         "free": {"hand": (-0.42, -0.12, -0.45), "pole": (-0.4, -0.6, 0.6), "space": "root"}}
    P.update(legs_lunge(hy))
    return P


def ground_rise():
    """Frame 80: risen a little, the blade lifted to point ahead and up, ready (GIF frames 70-75)."""
    hy = 0.66
    P = {"hips": (0.0, hy, -0.02), "body": rot(p=12, y=-8), "spine": rot(p=4, y=-6), "neck": rot(p=-10, y=8), "head": rot(p=-8, y=6),
         "sword": {"hand": (0.05, -0.62, 0.48), "blade": (0.12, 0.55, 0.85), "space": "root"},
         "free": {"hand": (0.05, -0.62, 0.25), "pole": (-0.6, -0.6, -0.5), "space": "root"}}
    P.update(legs_lunge(hy, front=0.40, back=0.38))
    return P


def air_wound():
    """Air frames 0-4: knees tucked, the sword arm straight out to the right and a little up (the turn puts it behind), the free arm in
    across the chest (GIF frames 0-4)."""
    P = {"hips": (0.0, 0.95, 0.0), "body": rot(p=-4), "spine": rot(p=4, y=-12), "neck": rot(p=-4, y=14), "head": rot(p=-4, y=10),
         "sword": {"hand": (0.84, 0.18, -0.05), "blade": (1.0, 0.32, -0.08), "space": "chest"},
         "free": {"hand": (0.25, -0.35, 0.50), "pole": (-0.6, -0.6, -0.4), "space": "chest"}}
    P.update(legs_tucked())
    return P


def air_spin(frame, yaw_deg):
    P = spin_pose(frame, yaw_deg=yaw_deg)   # no tilt swing: the blade stays level all the way round (owner, 2026-11-05)
    P["hips"] = (0.0, 0.95, 0.0)
    P["body"] = rot(p=-2)
    P.update(legs_tucked())
    return P


def air_launch(stage):
    """The launching cut, the arm swinging up from level at the side to straight overhead: stage 0 low, 1 level, 2 rising, 3 overhead."""
    hands = [(0.80, -0.30, 0.10), (0.85, 0.05, 0.10), (0.62, 0.60, 0.08), (0.14, 0.86, 0.04)]
    blades = [(1.0, -0.45, 0.12), (1.0, 0.05, 0.12), (0.70, 0.72, 0.08), (0.08, 1.0, -0.04)]
    P = {"hips": (0.0, 0.98, 0.0), "body": rot(p=-6), "spine": rot(p=-2, y=6), "neck": rot(p=-2, y=-4), "head": rot(p=-6, y=-4),
         "sword": {"hand": hands[stage], "blade": blades[stage], "space": "chest"},
         "free": {"hand": (-0.70, -0.30, -0.10), "pole": (0.0, -1.0, -0.3), "space": "chest"}}
    P.update(legs_tucked())
    if stage == 3:
        # Stretched up into the cut: the legs let down a little.
        P["l_leg"] = {"foot": (-0.06, -0.48, 0.22), "knee": (0.0, 0.35, 1.0), "toe": (0.0, -0.6, 0.8), "space": "hips"}
        P["r_leg"] = {"foot": (0.08, -0.70, -0.08), "knee": (0.05, -0.1, 1.0), "toe": (0.0, -0.9, 0.4), "space": "hips"}
    return P


def air_lowered(stage):
    """After the launch, the blade lowered out behind: stage 0 up and back, 1 level behind (GIF frames 58-80)."""
    hands = [(0.40, 0.66, -0.36), (0.66, 0.15, -0.50)]
    blades = [(0.35, 0.78, -0.52), (0.72, 0.10, -0.70)]
    P = {"hips": (0.0, 0.96, 0.0), "body": rot(p=-4), "spine": rot(p=0, y=4), "neck": rot(p=-4, y=-2), "head": rot(p=-4, y=-2),
         "sword": {"hand": hands[stage], "blade": blades[stage], "space": "chest"},
         "free": {"hand": (-0.40, -0.45, 0.30), "pole": (-0.5, -0.7, -0.4), "space": "chest"}}
    P.update(legs_tucked())
    return P


def air_guard():
    """Air frame 91: back in the guard, falling: the sword stance's arms over tucked legs."""
    P = pose_settle()
    P["hips"] = (0.0, 0.95, 0.0)
    P.update(legs_tucked())
    return P


# ---------------------------------------------------------------------------------------------------------------------------------------------
# THE TIMELINES

def turned(pose, degrees, tilt=0.0):
    """A solved pose with the whole body turned `degrees` to the left about the upright through the feet, its axis first tipped `tilt`
    degrees forward about the hips."""
    L, hips = pose
    Q = pitch(tilt) @ yaw(degrees)
    pivot = Vector((0.0, 0.0, hips.z))
    L = dict(L)
    L["hips"] = Q @ L["hips"]
    return L, pivot + Q @ (hips - pivot)


def ground_frames():
    frames = {}
    s, s_p = chained(stance(), None, "stance")
    dip, dip_p = chained(ground_dip(), s_p, "dip")
    cock, cock_p = chained(ground_cocked(), dip_p, "cocked")
    rel, rel_p = chained(ground_release(), cock_p, "release")
    keys = [(0, s, "inout"), (2, dip, "out"), (GROUND_CHARGE_FRAME, cock, "in"), (6, rel, "linear")]
    for f in range(0, 7):
        frames[f] = keyed(keys, f)
    previous = rel_p
    for f in range(7, 40):
        yaw_deg = pchip(GROUND_YAW, f)
        frames[f], previous = chained(spin_pose(f, yaw_deg=yaw_deg), previous, "spin %d" % f if f in (7, 20, 39) else None)
    last_spin = frames[39]
    turn, turn_p = chained(ground_settle_turn(), previous, "settle turn")
    end, end_p = chained(ground_end(), turn_p, "end")
    sunk, sunk_p = chained(ground_end(sink=0.025), end_p, "end sunk")
    rise, _ = chained(ground_rise(), sunk_p, "rise")
    keys = [(39, last_spin, "linear"), (44, turn, "out"), (49, end, "inout"), (60, sunk, "inout"), (66, end, "inout"), (80, rise, "linear")]
    for f in range(40, GROUND[1] + 1):
        frames[f] = keyed(keys, f)
    return {f: turned(pose, pchip(GROUND_YAW, f)) for f, pose in frames.items()}, cock_p


def ground_hold_frames(cock_p):
    """The cocked pose held: a slow breath through the body and a tremble in the blade, one whole cycle so the loop is seamless."""
    frames = {}
    length = GROUND_HOLD[1] - GROUND_HOLD[0]
    yaw_deg = pchip(GROUND_YAW, GROUND_CHARGE_FRAME)
    held = dict(cock_p)
    held["sword"] = dict(cock_p["sword"], edge=cock_p["_edge"])
    for k in range(length + 1):
        t = k / float(length)
        breath = math.sin(t * 2.0 * math.pi)
        shake = math.sin(t * 2.0 * math.pi * 8.0) * 0.012
        P = dict(held)
        hx, hy, hz = held["hips"]
        P["hips"] = (hx, hy - 0.012 * (1.0 - math.cos(t * 2.0 * math.pi)) * 0.5, hz)
        P["spine"] = held["spine"] @ rot(p=1.2 * breath)
        sword = dict(held["sword"])
        bx, by, bz = sword["blade"]
        sword["blade"] = (bx + shake, by + shake * 0.7, bz)
        P["sword"] = sword
        frames[GROUND_HOLD[0] + k] = turned(solved(P), yaw_deg)
    return frames


def air_frames():
    frames = {}
    wound, wound_p = chained(air_wound(), None, "air wound")
    for f in range(0, 5):
        frames[f] = wound
    previous = wound_p
    for f in range(5, 45):
        yaw_deg = pchip(AIR_YAW, f)
        frames[f], previous = chained(air_spin(f, yaw_deg), previous, "air spin %d" % f if f in (8, 30) else None)
    # The whip out of the wound-up pose: the free arm swings across over frames 4-8, not in one.
    for f in range(5, 8):
        frames[f] = blend(wound, frames[f], ease("inout", (f - 4) / 4.0))
    launch = []
    for stage in range(4):
        pose, previous = chained(air_launch(stage), previous, "launch %d" % stage)
        launch.append(pose)
    low0, low0_p = chained(air_lowered(0), previous, "lowered up")
    low1, low1_p = chained(air_lowered(1), low0_p, "lowered behind")
    guard, _ = chained(air_guard(), low1_p, "air guard")
    keys = [(44, frames[44], "linear"), (45, launch[0], "linear"), (47, launch[1], "linear"), (49, launch[2], "out"), (51, launch[3], "linear"),
            (58, launch[3], "inout"), (66, low0, "inout"), (74, low1, "inout"), (92, guard, "linear")]
    for f in range(45, AIR[1] - AIR[0] + 1):
        frames[f] = keyed(keys, f)
    out = {}
    for f, pose in frames.items():
        tilt = AIR_TILT * smooth(3, 8, f) * (1.0 - smooth(46, 56, f))
        out[AIR[0] + f] = turned(pose, pchip(AIR_YAW, f), tilt)
    return out


def timeline():
    ground, cock_p = ground_frames()
    frames = dict(ground)
    frames.update(ground_hold_frames(cock_p))
    frames.update(air_frames())
    return frames


# ---------------------------------------------------------------------------------------------------------------------------------------------
# BAKE onto the armature: one key a frame, linear between; markers name the segments for the exporter.

def bake_spin():
    frames = timeline()
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("SwordSpin")
    rig.animation_data.action = action
    rig.location = (0, 0, 0)
    previous = {}
    for f in sorted(frames):
        L, hips = frames[f]
        apply_pose(L, hips)
        for n in ORDER:
            pb = rig.pose.bones[n]
            q = pb.rotation_quaternion.copy()
            if n in previous and previous[n].dot(q) < 0.0:
                q = -q
            pb.rotation_quaternion = q
            previous[n] = q
            pb.keyframe_insert("rotation_quaternion", frame=f, group=n)
            if PARENT[n] is None:
                pb.keyframe_insert("location", frame=f, group=n)
    for fc in action.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR'
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    for name, (a, b) in (("ground", GROUND), ("ground_hold", GROUND_HOLD), ("air", AIR)):
        scene.timeline_markers.new(name, frame=a)
    scene.timeline_markers.new("end", frame=AIR[1])
    scene.render.fps = FPS
    scene.frame_start = GROUND[0]
    scene.frame_end = AIR[1]
    scene.frame_set(GROUND[0])
    return len(frames)
