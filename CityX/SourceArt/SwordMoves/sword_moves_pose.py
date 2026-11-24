# THE BODY UNDER THE SWORD'S ATTACKS. Owner, 2026-11-13: "the sword combat animations are stilted and awkward. they only use the arms so It looks odd. try to refine the
# combat animations so that they are telegraphed, have directionality, and the animation involves the whole body akin to dark souls combat." Each attack's LEGS, HIPS, TORSO
# AND HEAD are made here on the game's skeleton, with the game's own timing (SwordStrikes.TimingOf) and step (SwordStrikes.StepMetres): keep them in step.
#   * telegraphed: a real wind-up with the whole body coiled against the cut (the shoulders turned away, the weight back over the rear foot), held for a moment before it goes;
#   * directional: the body turns the way the cut travels and its follow-through carries on that way;
#   * the whole body: a stride with the foot planted before the blow, the hips turning first and the shoulders after them, the weight driven over the front foot, the rear heel
#     coming up, the head kept on what is ahead.
#
# THE ARMS ARE NOT MADE HERE. The game poses the sword arm, the blade and the hand on the handle itself (SwordStrikes' keys, PlayerLocomotion.ApplySwordPose), from the
# shoulders this clip turns, except for the one-handed cuts posed in sword_arms_1h.py, whose sword arm is baked into their own copy of the clip (SwordMotion1H.json). A
# draft that posed the arms here bent the wrists and put them through the chest (owner, 2026-11-13: "why are his wrists all twisted and poking through his torso"). Other
# arms on the preview's renders are stand-ins carried in front of the chest, and no arm bone is exported into SwordMotion.json (sword_moves_build.py).
#
# Needs the rig (../Octaslash/octaslash_rig.py), sword_hold.py (the game's hold), octaslash_pose.py's solver (with OCTASLASH_NO_BAKE set) and ../planted_feet.py loaded first;
# sword_moves_build.py does all of it.
#
# Poses are written in the character's own frame (Unity axes: x right, y up, z forward, metres, the feet's ground at y 0). The FEET are written on the stage (planted_feet.py),
# which the game's step carries the body over.

import bpy
import math
from mathutils import Vector

FPS = 60
HIP_HEIGHT = 0.9
STANCE_X = 0.20
READY_DROP = 0.08                   # SwordStrikes' ready stance


def V(x, y, z):
    return Vector((x, y, z))


def key(twist=0.0, hips=0.0, lean=2.0, tilt=0.0, drop=READY_DROP, shift=(0.0, 0.0), chin=0.0, staged=False):
    """One moment of an attack. twist, hips: the shoulders' and the hips' turn to the right, degrees; lean: forward; tilt: the right side down; drop: how far the hips sink;
    shift: the hips' place over the feet (x, z); chin: the head tucked down; staged: the hips lead into this key and the shoulders follow."""
    return {"twist": twist, "hips": hips, "lean": lean, "tilt": tilt, "drop": drop, "shift": V(shift[0], 0.0, shift[1]), "chin": chin, "staged": staged}


# Standing ready (SwordStrikes.Ready), with a one-handed sword and with a two-handed one.
READY_1H = key(twist=12.0, hips=6.0, lean=3.0)
READY_2H = key(twist=10.0, hips=6.0, lean=4.0)
READY_LEFT, READY_RIGHT = (-STANCE_X, 0.10), (STANCE_X, -0.10)


def phases(wind, hold, strike, follow, settle, recover):
    """An attack's moments in seconds, from its phases' lengths (SwordStrikes.StrikeTiming): the wind-up's end, the hold's, the contact, the follow-through's end, the settle's,
    and the whole."""
    return {"timing": (wind, hold, strike, follow, settle, recover), "wound": wind, "held": wind + hold, "contact": wind + hold + strike,
            "followed": wind + hold + strike + follow, "settled": wind + hold + strike + follow + settle, "seconds": wind + hold + strike + follow + settle + recover}


def attack(timing, step, wound, held, contact, followed, back, left, right, ready=READY_1H, rising=None, two_handed=False):
    """An attack from its phases, how far the game steps the body, and its keys: wound up, the hold's end (the wind-up crept a little further), the contact, the follow-through's
    end (held through the settle), a moment on the way back, and the feet on the stage. rising: (seconds, key), a moment on the way up to the wind-up. two_handed: only for
    the preview's stand-in arms."""
    t = phases(*timing)
    return dict(t, step=step, left=left, right=right, two_handed=two_handed, keys=[(0.0, ready, None)] + ([(rising[0], rising[1], "linear")] if rising is not None else []) + [
        (t["wound"], wound, "smooth"),
        (t["held"], held, "linear"),
        (t["contact"], contact, "linear"),
        (t["followed"], followed, "slow_in"),
        (t["settled"], followed, "linear"),
        (t["settled"] + (t["seconds"] - t["settled"]) * 0.55, back, "smooth"),
        (t["seconds"], ready, "smooth"),
    ])


# The game turns the sword arm with the shoulders (PlayerLocomotion.ArmAxesUnderTheClip): by as far as a clip has them turned past the twist of SwordStrikes' own key, the
# whole arm and the blade are turned too. So each CONTACT key's twist here is SwordStrikes' own, and the blade lands where its keys aim it; the wind-ups and the
# follow-throughs are turned further than its keys, and draw the blade further back and through.
ATTACKS = {
    # THE ONE-HANDED CHAIN AND ITS HEAVY ARE THE OWNER'S REFERENCE COMBO (2026-11-13: "make the animation exactly like this gif ... two sort of downward/diagonal slashes
    # and then 1 horizontal slash, then ... a fourth downward slash that hits the ground"; docs/SWORD_GIF_COMBO.md). The second cut is the first's mirror; it was thrown out of a full turn of the whole
    # body, laid over this clip by the game, until the owner (2026-11-14) had it taken out. The third is the game's own level slash (owner).
    #
    # THE FIRST: down from over the right shoulder to the lower left. The body arched back and coiled, a long stride onto the left foot, and the whole body drops into a deep
    # lunge behind the blade.
    "sword1h_slash": attack(
        (0.22, 0.06, 0.12, 0.05, 0.12, 0.24), 0.45,
        key(twist=56.0, hips=26.0, lean=-11.0, tilt=-6.0, drop=0.10, shift=(0.07, -0.11)),
        key(twist=60.0, hips=28.0, lean=-13.0, tilt=-6.0, drop=0.12, shift=(0.07, -0.12)),
        key(twist=-25.0, hips=-36.0, lean=34.0, tilt=10.0, drop=0.21, shift=(-0.07, 0.07), chin=8.0, staged=True),
        key(twist=-66.0, hips=-48.0, lean=36.0, tilt=8.0, drop=0.25, shift=(-0.09, 0.06), chin=8.0),
        key(twist=-26.0, hips=-18.0, lean=14.0, drop=0.15, shift=(-0.03, 0.04)),
        left=[(0.10, 0.33, (-0.22, 0.60), 0.14)],
        right=[(0.42, 0.64, (STANCE_X, 0.35), 0.05)]),
    # THE SECOND: from over the left shoulder steeply down to the right. The shoulders turned away to the left; the right foot steps through as the
    # body comes round, and it unwinds to the right behind the blade.
    "sword1h_cut": attack(
        (0.20, 0.05, 0.12, 0.06, 0.10, 0.24), 0.50,
        key(twist=-66.0, hips=-30.0, lean=-6.0, tilt=-5.0, drop=0.13, shift=(-0.07, -0.09)),
        key(twist=-70.0, hips=-32.0, lean=-7.0, tilt=-5.0, drop=0.14, shift=(-0.07, -0.10)),
        key(twist=25.0, hips=32.0, lean=22.0, tilt=-6.0, drop=0.19, shift=(0.05, 0.10), chin=5.0, staged=True),
        key(twist=56.0, hips=44.0, lean=18.0, tilt=-3.0, drop=0.18, shift=(0.07, 0.09), chin=5.0),
        key(twist=28.0, hips=20.0, lean=8.0, drop=0.11, shift=(0.03, 0.04)),
        right=[(0.09, 0.31, (0.18, 0.40), 0.12)],
        left=[(0.37, 0.60, (-STANCE_X, 0.60), 0.05)]),
    # THE THIRD: the level slash, right to left (owner, 2026-11-15: "switch the direction of the third swing make it a horizontal swing right to left", "you can reuse the
    # old horizontal swing animation"): the chain's old first cut as it was before the reference combo, on the finisher's timing. The shoulders turned right round to the
    # right after the blade, the weight back; a stride onto the left foot, and the hips bring the body round to the left, driving in over the front foot.
    "sword1h_finisher": attack(
        (0.30, 0.12, 0.13, 0.10, 0.12, 0.55), 0.55,
        key(twist=72.0, hips=34.0, lean=-7.0, tilt=5.0, drop=0.14, shift=(0.07, -0.10)),
        key(twist=76.0, hips=36.0, lean=-8.0, tilt=5.0, drop=0.15, shift=(0.07, -0.11)),
        key(twist=-15.0, hips=-30.0, lean=18.0, tilt=3.0, drop=0.17, shift=(-0.05, 0.10), chin=4.0, staged=True),
        key(twist=-64.0, hips=-44.0, lean=14.0, tilt=1.0, drop=0.16, shift=(-0.07, 0.09), chin=4.0),
        key(twist=-20.0, hips=-14.0, lean=8.0, drop=0.11, shift=(-0.02, 0.04)),
        left=[(0.14, 0.44, (-0.22, 0.65), 0.11)],
        right=[(0.58, 0.86, (STANCE_X, 0.45), 0.05)]),
    # THE HEAVY: into the ground. The body arched back under the sword raised behind the head and held there (the tell); a long stride under the held blade, and the body
    # folds into a deep lunge over the front knee as the blade goes into the ground, and stays down over it before it rises.
    "sword1h_overhead": attack(
        (0.34, 0.30, 0.13, 0.12, 0.26, 0.34), 0.50,
        key(twist=18.0, hips=8.0, lean=-16.0, drop=0.04, shift=(0.03, -0.12)),
        key(twist=20.0, hips=9.0, lean=-19.0, drop=0.06, shift=(0.03, -0.14)),
        key(twist=-10.0, hips=-10.0, lean=46.0, drop=0.36, shift=(0.0, 0.08), chin=10.0, staged=True),
        key(twist=-14.0, hips=-12.0, lean=50.0, drop=0.40, shift=(0.0, 0.07), chin=10.0),
        key(twist=0.0, hips=0.0, lean=20.0, drop=0.20, shift=(0.0, 0.05)),
        left=[(0.40, 0.72, (-0.22, 0.62), 0.14)],
        right=[(1.17, 1.42, (STANCE_X, 0.40), 0.06)],
        # Up by way of the first cut's own wind-up: a held click turns the light already winding up into this (SoulsSwordCombat.HoldIntoHeavy), from the pose it has reached.
        rising=(0.16, key(twist=52.0, hips=24.0, lean=-8.0, tilt=-4.0, drop=0.10, shift=(0.06, -0.09)))),
}

# ---- TWO HANDS (the spike sword, the bat, the crowbar, the sledgehammer) -----------------------------------------------------------------------------------------------------
# With both hands on the handle the shoulders turn the blade: a two-handed cut is swung by the body more than by the arms.
ATTACKS.update({
    # THE SWEEP: level, right to left. The shoulders turned right round after the blade; a stride, and the hips bring it across.
    "sword2h_sweep": attack(
        (0.26, 0.07, 0.14, 0.13, 0.07, 0.27), 0.45,
        key(twist=70.0, hips=34.0, lean=-6.0, tilt=4.0, drop=0.14, shift=(0.07, -0.10)),
        key(twist=74.0, hips=36.0, lean=-7.0, tilt=4.0, drop=0.15, shift=(0.07, -0.11)),
        key(twist=-10.0, hips=-30.0, lean=18.0, tilt=2.0, drop=0.17, shift=(-0.05, 0.10), chin=4.0, staged=True),
        key(twist=-52.0, hips=-42.0, lean=14.0, drop=0.16, shift=(-0.07, 0.09), chin=4.0),
        key(twist=-16.0, hips=-12.0, lean=8.0, drop=0.11, shift=(-0.02, 0.04)),
        left=[(0.12, 0.38, (-0.22, 0.55), 0.11)],
        right=[(0.47, 0.70, (STANCE_X, 0.35), 0.05)],
        ready=READY_2H, two_handed=True),
    # THE REVERSE: level, left to right. The shoulders turned away to the left; the right foot strides through and the body unwinding brings the blade back across.
    "sword2h_reverse": attack(
        (0.24, 0.06, 0.14, 0.12, 0.06, 0.26), 0.50,
        key(twist=-62.0, hips=-30.0, lean=-5.0, tilt=-4.0, drop=0.13, shift=(-0.07, -0.09)),
        key(twist=-66.0, hips=-32.0, lean=-6.0, tilt=-4.0, drop=0.14, shift=(-0.07, -0.10)),
        key(twist=12.0, hips=30.0, lean=18.0, tilt=-2.0, drop=0.17, shift=(0.05, 0.10), chin=4.0, staged=True),
        key(twist=50.0, hips=40.0, lean=14.0, drop=0.16, shift=(0.07, 0.09), chin=4.0),
        key(twist=24.0, hips=18.0, lean=8.0, drop=0.11, shift=(0.03, 0.04)),
        right=[(0.10, 0.36, (0.18, 0.40), 0.12)],
        left=[(0.44, 0.68, (-STANCE_X, 0.60), 0.05)],
        ready=READY_2H, two_handed=True),
    # THE CUT (the chain's last): down from over the right shoulder to the lower left, the body coiled and sunk before a long stride.
    "sword2h_cut": attack(
        (0.30, 0.10, 0.15, 0.115, 0.08, 0.32), 0.55,
        key(twist=50.0, hips=24.0, lean=-11.0, tilt=-5.0, drop=0.10, shift=(0.07, -0.12)),
        key(twist=54.0, hips=26.0, lean=-13.0, tilt=-5.0, drop=0.12, shift=(0.07, -0.13)),
        key(twist=-20.0, hips=-34.0, lean=30.0, tilt=8.0, drop=0.22, shift=(-0.07, 0.06), chin=8.0, staged=True),
        key(twist=-50.0, hips=-44.0, lean=28.0, tilt=6.0, drop=0.22, shift=(-0.09, 0.05), chin=8.0),
        key(twist=-22.0, hips=-16.0, lean=13.0, drop=0.15, shift=(-0.03, 0.04)),
        left=[(0.14, 0.48, (-0.24, 0.65), 0.17)],
        right=[(0.52, 0.80, (STANCE_X, 0.45), 0.06)],
        ready=READY_2H, two_handed=True),
    # THE SMASH (a hammer's third light): straight down from over the head, quicker than the slam and without its hold.
    "sword2h_smash": attack(
        (0.28, 0.08, 0.13, 0.11, 0.09, 0.34), 0.40,
        key(twist=8.0, hips=4.0, lean=-12.0, drop=0.05, shift=(0.02, -0.10)),
        key(twist=8.0, hips=4.0, lean=-14.0, drop=0.06, shift=(0.02, -0.11)),
        key(twist=-5.0, hips=-6.0, lean=30.0, drop=0.22, shift=(0.0, 0.07), chin=8.0, staged=True),
        key(twist=-8.0, hips=-8.0, lean=36.0, drop=0.26, shift=(0.0, 0.06), chin=8.0),
        key(twist=2.0, hips=0.0, lean=14.0, drop=0.14, shift=(0.0, 0.04)),
        left=[(0.14, 0.44, (-0.22, 0.50), 0.12)],
        right=[(0.49, 0.74, (STANCE_X, 0.30), 0.06)],
        ready=READY_2H, two_handed=True),
    # THE SLAM (the heavy): the body arched back under the sword raised over the head and held there (the tell); a stride, and everything goes into the ground.
    "sword2h_slam": attack(
        (0.42, 0.38, 0.16, 0.13, 0.18, 0.38), 0.50,
        key(twist=8.0, hips=4.0, lean=-15.0, drop=0.05, shift=(0.02, -0.11)),
        key(twist=8.0, hips=4.0, lean=-18.0, drop=0.07, shift=(0.02, -0.13)),
        key(twist=-5.0, hips=-6.0, lean=36.0, drop=0.27, shift=(0.0, 0.07), chin=8.0, staged=True),
        key(twist=-8.0, hips=-8.0, lean=42.0, drop=0.32, shift=(0.0, 0.06), chin=8.0),
        key(twist=2.0, hips=0.0, lean=16.0, drop=0.16, shift=(0.0, 0.05)),
        left=[(0.50, 0.90, (-0.22, 0.60), 0.13)],
        right=[(0.96, 1.22, (STANCE_X, 0.40), 0.06)],
        ready=READY_2H, two_handed=True,
        # Up by way of the sweep's own wind-up, as the one-handed heavy goes by way of the slash's.
        rising=(0.18, key(twist=48.0, hips=22.0, lean=-6.0, tilt=3.0, drop=0.11, shift=(0.06, -0.08)))),
})
# ---- THE RUNNING ATTACKS ---------------------------------------------------------------------------------------------------------------------------------------------------
# Owner, 2026-11-13: "if you are sprinting it does a different attack", and then "You need to have the sword movements conserve momentum and move freely while attacking ...
# You should be able to move freely with zero slowdown". The first light of a body that is sprinting: the chain's last cut (the diagonal from over the right shoulder)
# brought down on the run. The keys carry the body through it at its full pace and its legs are its run's own: the game takes only the TORSO AND THE HEAD from these
# clips (SwordStrikes.ClipLegs), so the feet here simply stand and nothing is stepped. The body leans into its run all the way: the pace is the wind-up.
ATTACKS.update({
    "sword1h_run": attack(
        (0.24, 0.06, 0.13, 0.05, 0.15, 0.40), 0.0,
        key(twist=50.0, hips=20.0, lean=10.0, tilt=-5.0, drop=0.03),
        key(twist=54.0, hips=22.0, lean=9.0, tilt=-5.0, drop=0.02),
        key(twist=-25.0, hips=-36.0, lean=34.0, tilt=10.0, drop=0.16, chin=8.0, staged=True),
        key(twist=-62.0, hips=-48.0, lean=32.0, tilt=8.0, drop=0.16, chin=8.0),
        key(twist=-24.0, hips=-16.0, lean=16.0, drop=0.10),
        left=[], right=[]),
    "sword2h_run": attack(
        (0.26, 0.07, 0.15, 0.115, 0.10, 0.42), 0.0,
        key(twist=46.0, hips=20.0, lean=10.0, tilt=-4.0, drop=0.03),
        key(twist=50.0, hips=22.0, lean=9.0, tilt=-4.0, drop=0.02),
        key(twist=-20.0, hips=-34.0, lean=32.0, tilt=8.0, drop=0.16, chin=8.0, staged=True),
        key(twist=-50.0, hips=-44.0, lean=30.0, tilt=6.0, drop=0.16, chin=8.0),
        key(twist=-22.0, hips=-16.0, lean=15.0, drop=0.10),
        left=[], right=[],
        ready=READY_2H, two_handed=True),
})
# ---- THE SPEAR -------------------------------------------------------------------------------------------------------------------------------------------------------------
# Owner, 2026-11-18: "research video games with spears and try to make good spear animations/mechanics. i feel like it should be mostly thrusting stabs but some swinging as
# well maybe the heavy attacks are swings" (docs/SPEAR.md). A spear is fought SIDE ON, the left side to the enemy: the shoulders stay turned to the right even as a thrust
# lands (SwordStrikes' spear keys: the rear hand runs forward along the right side, the butt passing under that arm). So a thrust is not a turn of the body as a cut is: it is
# the weight thrown from the rear leg onto the front one along the shaft's own line, the body lengthening over a stride, and the shoulders opening only part of the way.
# EVERY KEY'S TWIST HERE IS SwordStrikes' OWN, the wind-ups' too (the cuts' wind-ups are turned further than their keys, which draws a blade further back): the game turns the
# arm with the shoulders by whatever a clip has them turned past its key, and that swings the point of a shaft this long a foot to the side on its way out.
READY_SPEAR = key(twist=36.0, hips=18.0, lean=4.0, drop=0.10)
ATTACKS.update({
    # THE JAB: a short draw back over the rear leg and straight out, a short step.
    "spear_jab": attack(
        (0.20, 0.04, 0.09, 0.04, 0.06, 0.20), 0.30,
        key(twist=52.0, hips=26.0, lean=-2.0, drop=0.11, shift=(0.04, -0.08)),
        key(twist=52.0, hips=27.0, lean=-3.0, drop=0.12, shift=(0.04, -0.09)),
        key(twist=24.0, hips=8.0, lean=12.0, drop=0.13, shift=(-0.02, 0.08), chin=3.0, staged=True),
        key(twist=20.0, hips=6.0, lean=14.0, drop=0.13, shift=(-0.02, 0.09), chin=3.0),
        key(twist=30.0, hips=14.0, lean=7.0, drop=0.10, shift=(0.0, 0.03)),
        left=[(0.08, 0.27, (-0.22, 0.45), 0.07)],
        right=[(0.39, 0.57, (STANCE_X, 0.20), 0.04)],
        ready=READY_SPEAR, two_handed=True),
    # THE THRUST: the same on a higher line, drawn back a little further, a longer step.
    "spear_thrust": attack(
        (0.20, 0.05, 0.10, 0.04, 0.07, 0.22), 0.40,
        key(twist=56.0, hips=28.0, lean=-4.0, drop=0.11, shift=(0.05, -0.09)),
        key(twist=56.0, hips=29.0, lean=-5.0, drop=0.12, shift=(0.05, -0.10)),
        key(twist=20.0, hips=6.0, lean=14.0, drop=0.14, shift=(-0.02, 0.09), chin=3.0, staged=True),
        key(twist=16.0, hips=4.0, lean=16.0, drop=0.14, shift=(-0.02, 0.10), chin=3.0),
        key(twist=30.0, hips=14.0, lean=8.0, drop=0.10, shift=(0.0, 0.03)),
        left=[(0.08, 0.29, (-0.22, 0.50), 0.08)],
        right=[(0.42, 0.62, (STANCE_X, 0.30), 0.04)],
        ready=READY_SPEAR, two_handed=True),
    # THE LUNGE (the chain's last): coiled right back and sunk over the rear leg, held; then a long stride and the whole body stretched low along the shaft.
    "spear_lunge": attack(
        (0.28, 0.12, 0.11, 0.05, 0.14, 0.50), 0.80,
        key(twist=64.0, hips=32.0, lean=-8.0, tilt=-3.0, drop=0.16, shift=(0.06, -0.14)),
        key(twist=64.0, hips=34.0, lean=-10.0, tilt=-3.0, drop=0.18, shift=(0.06, -0.16)),
        key(twist=16.0, hips=0.0, lean=30.0, tilt=2.0, drop=0.28, shift=(-0.03, 0.12), chin=6.0, staged=True),
        key(twist=12.0, hips=-2.0, lean=32.0, tilt=2.0, drop=0.29, shift=(-0.03, 0.13), chin=6.0),
        key(twist=28.0, hips=12.0, lean=14.0, drop=0.16, shift=(0.0, 0.05)),
        left=[(0.20, 0.50, (-0.24, 0.95), 0.16)],
        right=[(0.72, 1.00, (STANCE_X, 0.70), 0.06)],
        ready=READY_SPEAR, two_handed=True),
    # THE SWEEP (the heavy): the one time the body turns right round as for a cut, and it is a backhand (SwordStrikes: the hands are where a spear's are). The shoulders
    # wound away to the LEFT after the point and held there (the tell); the right foot strides through and the body unwinding to the right brings the whole shaft level
    # across the front.
    "spear_sweep": attack(
        (0.38, 0.32, 0.15, 0.13, 0.14, 0.40), 0.45,
        key(twist=-16.0, hips=-8.0, lean=-5.0, tilt=-4.0, drop=0.13, shift=(-0.06, -0.09)),
        key(twist=-16.0, hips=-9.0, lean=-6.0, tilt=-4.0, drop=0.14, shift=(-0.06, -0.10)),
        key(twist=40.0, hips=30.0, lean=18.0, tilt=-2.0, drop=0.18, shift=(0.05, 0.10), chin=4.0, staged=True),
        key(twist=66.0, hips=46.0, lean=10.0, drop=0.16, shift=(0.07, 0.09), chin=4.0),
        key(twist=44.0, hips=24.0, lean=7.0, drop=0.11, shift=(0.02, 0.04)),
        right=[(0.40, 0.78, (STANCE_X, 0.35), 0.11)],
        left=[(0.92, 1.16, (-STANCE_X, 0.55), 0.05)],
        ready=READY_SPEAR, two_handed=True),
    # THE RUNNING ATTACK: the lunge on the run. The legs are the run's own (only the torso and the head are taken), so the feet here stand.
    "spear_run": attack(
        (0.20, 0.06, 0.11, 0.05, 0.14, 0.42), 0.0,
        key(twist=64.0, hips=30.0, lean=8.0, tilt=-3.0, drop=0.03),
        key(twist=64.0, hips=31.0, lean=7.0, tilt=-3.0, drop=0.02),
        key(twist=16.0, hips=0.0, lean=30.0, tilt=2.0, drop=0.16, chin=6.0, staged=True),
        key(twist=12.0, hips=-2.0, lean=32.0, tilt=2.0, drop=0.16, chin=6.0),
        key(twist=26.0, hips=10.0, lean=16.0, drop=0.10),
        left=[], right=[],
        ready=READY_SPEAR, two_handed=True),
})
# ---- THE BO STAFF -----------------------------------------------------------------------------------------------------------------------------------------------------------
# Owner, 2026-11-19, with a clip of a staff fighter: "make copy these movements as best you can", its first spinning swing "maybe that will be the heavy hold click attack but
# the rest looks pretty good" (docs/BO_STAFF.md). A staff is swung from the hips with both hands, low and wide, the body turning right round through each blow: a chop
# from over the right shoulder, a level backhand sweep at the middle, and a rising blow from low on the right up past the left shoulder that ends the chain (owner, 2026-11-22: the lunging thrust cut); the
# heavy is wound right round to the right, held, and let go flat round the front. As for every cut, the contacts' twists are SwordStrikes' own.
READY_STAFF = key(twist=14.0, hips=8.0, lean=5.0, drop=0.10)
ATTACKS.update({
    # THE CHOP: up over the right shoulder, the body coiled and leaning back, then down through the front with a stride, square to the enemy at the
    # contact and carried on only a little to the left (owner, 2026-11-22: it was "aimed at someone to the side").
    "staff_chop": attack(
        (0.24, 0.06, 0.13, 0.10, 0.07, 0.26), 0.40,
        key(twist=48.0, hips=22.0, lean=-8.0, tilt=-5.0, drop=0.12, shift=(0.06, -0.10)),
        key(twist=52.0, hips=24.0, lean=-10.0, tilt=-5.0, drop=0.13, shift=(0.06, -0.11)),
        key(twist=0.0, hips=-10.0, lean=24.0, tilt=4.0, drop=0.20, shift=(-0.02, 0.07), chin=8.0, staged=True),
        key(twist=-30.0, hips=-26.0, lean=22.0, tilt=4.0, drop=0.20, shift=(-0.05, 0.06), chin=8.0),
        key(twist=-10.0, hips=-8.0, lean=12.0, drop=0.15, shift=(-0.02, 0.03)),
        left=[(0.12, 0.40, (-0.24, 0.50), 0.15)],
        right=[(0.55, 0.78, (STANCE_X, 0.35), 0.06)],
        ready=READY_STAFF, two_handed=True),
    # THE SWEEP: a level backhand from where the chop ended: sunk and wound to the left, then the body rising and turning round to the right, the staff flat across
    # the front at the middle, the right foot passing through (owner, 2026-11-22: it was sunk low at the shins, "just make it horizontal").
    "staff_sweep": attack(
        (0.24, 0.06, 0.14, 0.13, 0.07, 0.28), 0.40,
        key(twist=-58.0, hips=-14.0, lean=12.0, tilt=4.0, drop=0.30, shift=(-0.07, -0.06)),
        key(twist=-62.0, hips=-15.0, lean=13.0, tilt=4.0, drop=0.32, shift=(-0.07, -0.07)),
        key(twist=0.0, hips=14.0, lean=14.0, tilt=-3.0, drop=0.16, shift=(0.04, 0.06), staged=True),
        key(twist=72.0, hips=40.0, lean=12.0, tilt=-2.0, drop=0.16, shift=(0.08, 0.04)),
        key(twist=20.0, hips=14.0, lean=8.0, drop=0.12, shift=(0.02, 0.02)),
        right=[(0.04, 0.40, (0.26, 0.35), 0.10)],
        left=[(0.58, 0.85, (-STANCE_X - 0.05, 0.30), 0.05)],
        ready=READY_STAFF, two_handed=True),
    # THE RISING BLOW (the chain's last), from where the sweep ended: coiled low on the right, held, then up from the lower right past the left shoulder,
    # the left foot striding through and the body rising and leaning back behind the blow.
    "staff_rising": attack(
        (0.28, 0.08, 0.14, 0.12, 0.10, 0.50), 0.50,
        key(twist=40.0, hips=24.0, lean=10.0, tilt=6.0, drop=0.26, shift=(0.06, -0.08)),
        key(twist=42.0, hips=25.0, lean=11.0, tilt=6.0, drop=0.28, shift=(0.06, -0.09)),
        key(twist=-20.0, hips=-28.0, lean=-6.0, tilt=8.0, drop=0.10, shift=(-0.05, 0.08), chin=-6.0, staged=True),
        key(twist=-50.0, hips=-40.0, lean=-14.0, tilt=6.0, drop=0.06, shift=(-0.07, 0.07), chin=-10.0),
        key(twist=-16.0, hips=-10.0, lean=4.0, drop=0.10, shift=(-0.02, 0.03)),
        left=[(0.14, 0.50, (-STANCE_X, 0.45), 0.10)],
        right=[(0.80, 1.05, (STANCE_X, 0.40), 0.05)],
        ready=READY_STAFF, two_handed=True),
    # THE SPIN (the heavy, the clip's opening blow): wound right round to the right and sunk, held there (the tell), then flat round the front at the chest, the whole body
    # turning through it and on round to the left.
    "staff_spin": attack(
        (0.40, 0.30, 0.16, 0.15, 0.14, 0.40), 0.55,
        key(twist=78.0, hips=40.0, lean=4.0, tilt=4.0, drop=0.20, shift=(0.07, -0.10)),
        key(twist=84.0, hips=44.0, lean=3.0, tilt=4.0, drop=0.22, shift=(0.08, -0.11)),
        key(twist=-8.0, hips=-24.0, lean=18.0, tilt=2.0, drop=0.18, shift=(-0.04, 0.08), chin=2.0, staged=True),
        key(twist=-88.0, hips=-62.0, lean=14.0, drop=0.16, shift=(-0.08, 0.06)),
        key(twist=-30.0, hips=-20.0, lean=8.0, drop=0.12, shift=(-0.03, 0.03)),
        left=[(0.30, 0.70, (-0.30, 0.55), 0.14)],
        right=[(0.95, 1.20, (STANCE_X, 0.40), 0.06)],
        ready=READY_STAFF, two_handed=True,
        rising=(0.18, key(twist=40.0, hips=18.0, lean=-4.0, drop=0.12, shift=(0.04, -0.05)))),
})
ORDER_OF_ATTACKS = ("sword1h_slash", "sword1h_cut", "sword1h_finisher", "sword1h_overhead", "sword2h_sweep", "sword2h_reverse", "sword2h_cut", "sword2h_smash", "sword2h_slam",
                    "sword1h_run", "sword2h_run", "spear_jab", "spear_thrust", "spear_lunge", "spear_sweep", "spear_run",
                    "staff_chop", "staff_sweep", "staff_rising", "staff_spin")

# What the game is given: no arm bone (the game's own poses have the arms).
EXPORTED_BONES = ("hips", "spine", "neck", "head", "thigh_l", "shin_l", "foot_l", "thigh_r", "shin_r", "foot_r")

# The preview's stand-in arms, from each shoulder in the chest's own frame: the sword held before the chest (and the left hand hanging, or on the handle).
STAND_IN_1H = {"hand": (-0.05, -0.56, 0.36), "blade": (-0.10, 0.50, 0.86)}
STAND_IN_2H = {"hand": (-0.25, -0.48, 0.40), "blade": (-0.06, 0.37, 0.927)}
STAND_IN_FREE = {"hand": (-0.18, -0.84, 0.05), "pole": (-0.4, -0.2, -1.0)}


def left_on_the_handle(D, pos):
    """The stand-in left arm of a two-handed hold: its wrist at the handle's lower grip (SwordWeapon.DefaultSupportGripDown under the guard)."""
    grip_at_rest = _sw.translation - BLADE_REST * SUPPORT_GRIP_DOWN
    grip = pos["hand_r"] + D["hand_r"] @ (grip_at_rest - HEAD["hand_r"])
    return {"hand": tuple(to_unity(grip)), "pole": (-0.5, -1.0, -0.2), "space": "world"}


def eased(kind, u):
    u = max(0.0, min(1.0, u))
    if kind == "smooth":
        return u * u * (3.0 - 2.0 * u)
    if kind == "slow_in":
        return 1.0 - (1.0 - u) ** 3
    return u


def sample(keys, seconds):
    """The attack's key at a moment. Into a staged key the hips lead and the shoulders follow (the game's blade comes after both: SwordStrikes' staged strike)."""
    i = 0
    while i < len(keys) - 2 and seconds >= keys[i + 1][0]:
        i += 1
    ta, a, _ = keys[i]
    tb, b, ease_kind = keys[i + 1]
    through = max(0.0, min(1.0, (seconds - ta) / max(tb - ta, 1e-4)))
    u = eased(ease_kind, through)
    hips_u = through ** 1.3 if b["staged"] else u
    shoulders_u = through ** 1.6 if b["staged"] else u

    def mix(field, share):
        return a[field] + (b[field] - a[field]) * share

    return {"twist": mix("twist", shoulders_u), "hips": mix("hips", hips_u), "lean": mix("lean", shoulders_u), "tilt": mix("tilt", shoulders_u), "drop": mix("drop", hips_u),
            "shift": a["shift"].lerp(b["shift"], hips_u), "chin": mix("chin", shoulders_u)}


def stepped(move, seconds):
    """How far the game has stepped a STANDING body by then (SwordStrikes.Stepped: keep the two in step): from halfway through the startup to the end of the live window,
    eased. (A body the keys are moving is not stepped, and its legs are its walk's own, not these.)"""
    wind, hold, strike, follow, _, _ = move["timing"]
    startup = wind + hold + strike * 0.25                       # SwordStrikes.ActiveStart
    window = wind + hold + strike + follow * 0.7                # SwordStrikes.ActiveEnd
    return move["step"] * smooth((seconds - startup * 0.5) / (window - startup * 0.5))


def to_unity(b):
    return V(-b.x, b.z, -b.y)


# A LEG IS NEVER LOCKED STRAIGHT. Driving off the rear foot, the hips outrun it: asked for all of the leg's length the knee snapped straight in a frame and stood locked.
# Past LEG_EASY of its length the ankle gives toward the hip instead (the heel rising further, the toes dragging), and never goes past LEG_MOST.
LEG_EASY, LEG_MOST = 0.89, 0.975


def within_reach(spec, stats):
    leg = (HEAD["shin_l"] - HEAD["thigh_l"]).length + (HEAD["foot_l"] - HEAD["shin_l"]).length
    foot = V(*spec["foot"])
    asked, easy, most = foot.length, LEG_EASY * leg, LEG_MOST * leg
    reach = asked if asked <= easy else easy + (most - easy) * (1.0 - math.exp(-(asked - easy) / (most - easy)))
    stats["used"] = max(stats.get("used", 0.0), reach)
    return dict(spec, foot=tuple(foot * (reach / asked)))


def pose_at(move, seconds, stats):
    s = sample(move["keys"], seconds)
    hips_at = V(s["shift"].x, HIP_HEIGHT - s["drop"], s["shift"].z)
    # The hips turn with their own share and carry a part of the lean; the chest has the rest of both. The body leans AT WHAT IT HITS, whichever way it is turned. The head
    # stays on what is ahead. (The keys' turns are positive to the right; yaw is positive to the left.)
    body = pitch(s["lean"] * 0.35) @ yaw(-s["hips"]) @ roll(s["tilt"] * 0.3)
    chest = pitch(s["lean"]) @ yaw(-s["twist"]) @ roll(s["tilt"])
    head = pitch(s["lean"] * 0.15 + s["chin"]) @ yaw(-s["twist"] * 0.1) @ roll(s["tilt"] * 0.2)
    neck = chest.slerp(head, 0.5)
    P = {
        "hips": tuple(hips_at), "body": body,
        "spine": body.inverted() @ chest, "neck": chest.inverted() @ neck, "head": neck.inverted() @ head,
        "sword": dict(STAND_IN_2H if move["two_handed"] else STAND_IN_1H),
        "free": dict(STAND_IN_FREE),
    }
    for side, steps, start in (("l", move["left"], READY_LEFT), ("r", move["right"], READY_RIGHT)):
        hip_b = U(*hips_at) + body @ (HEAD["thigh_" + side] - HEAD["hips"])
        P[side + "_leg"] = within_reach(planted_leg(side, to_unity(hip_b), steps, start, seconds, stepped(move, seconds), hips_at.z, 28.0, stats), stats)
    return P


def bake_attacks():
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("SwordMoves")
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    previous = {}
    start = 0
    spans = {}
    leg = (HEAD["shin_l"] - HEAD["thigh_l"]).length + (HEAD["foot_l"] - HEAD["shin_l"]).length
    for name in ORDER_OF_ATTACKS:
        move = ATTACKS[name]
        frames = int(round(move["seconds"] * FPS))
        scene.timeline_markers.new(name, frame=start)
        stats = {"reach": 0.0}
        arm_stats = {}
        tightest_knee = 180.0
        for i in range(frames):
            seconds = i / float(FPS)
            P = pose_at(move, seconds, stats)
            D, pos = solve(P)
            if move["two_handed"]:
                P["free"] = left_on_the_handle(D, pos)
                D, pos = solve(P)
            if name in ARMS_1H:
                pose_sword_arm(D, pos, arm_at(name, seconds, D, pos), arm_stats, seconds)
            for side in ("l", "r"):
                thigh = pos["thigh_" + side] - pos["shin_" + side]
                shin = pos["foot_" + side] - pos["shin_" + side]
                tightest_knee = min(tightest_knee, math.degrees(thigh.angle(shin)))
            apply_pose(locals_of(D), pos["hips"])
            f = start + i
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
            # The preview's step forward: the game moves the body; the export ignores this.
            rig.location = U(0.0, 0.0, stepped(move, seconds))
            rig.keyframe_insert("location", frame=f)
        print("%-17s %3d frames | tightest knee %3.0f deg | furthest ankle from its hip %.2f m of a %.2f m leg (%.2f m was asked for)"
              % (name, frames, tightest_knee, stats["used"], leg, stats["reach"]))
        if arm_stats:
            report_arm(name, arm_stats)
        spans[name] = (start, start + frames - 1, move)
        start += frames
    scene.timeline_markers.new("end", frame=start)    # one past the last frame: the exporter's segments end before their end marker
    for fc in action.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR'
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = start - 1
    scene.frame_set(0)
    return spans
