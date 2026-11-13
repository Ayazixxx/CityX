# FEET PLANTED ON THE GROUND UNDER A BODY THE GAME CARRIES FORWARD (a sword cut's lunge, a punch's step). Loaded by a move's script with exec, after the rig and the pose solver
# (Octaslash/octaslash_pose.py), like the rest of this folder: SwordMoves/sword_moves_pose.py, FistCombo/fist_pose.py.
#
# A move writes where each foot steps ON THE STAGE: the ground the body starts on, in the character's own axes (x right, z forward). The game then moves the body along z, so a
# planted foot's place in the clip is its place on the stage less how far the body has been carried by then: it stays still on the ground as the capsule moves.
import math

ANKLE = 0.135
BALL_FORWARD = 0.25                 # the ball of the foot, ahead of the ankle


def smooth(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3.0 - 2.0 * u)


def foot_on_stage(steps, start, seconds):
    """Where a foot is on the stage, how far off the ground, and how much of a step it is through (0 planted).
    steps: [(from seconds, to seconds, where to (x, z), how high it lifts)], each from the stance before it; start: where it stands first."""
    at = start
    for t0, t1, to, lift in steps:
        if seconds <= t0:
            break
        u = (seconds - t0) / (t1 - t0)
        if u >= 1.0:
            at = to
            continue
        s = smooth(u)
        x, z = at[0] + (to[0] - at[0]) * s, at[1] + (to[1] - at[1]) * s
        return (x, z), lift * math.sin(math.pi * u), math.sin(math.pi * u)
    return at, 0.0, 0.0


def planted_leg(side, hip, steps, start, seconds, carried, hips_z, rear_toe_out, stats):
    """A leg for the solver: the ankle from the hip joint (`hip`, the character's axes), on the stage less how far the body has been `carried`. A foot far behind the hips
    rolls up onto its ball, as far as the leg needs, and turns out by `rear_toe_out` degrees; one in the air points its toes. `stats["reach"]` keeps the longest leg asked for."""
    (x, z), lift, stepping = foot_on_stage(steps, start, seconds)
    local_z = z - carried
    behind = hips_z - local_z
    pitch = 35.0 * smooth((behind - 0.12) / 0.33) + 25.0 * stepping
    # The heel rises about the ball: the ankle goes up and a little forward.
    r = math.radians(pitch)
    y = ANKLE + lift + BALL_FORWARD * math.sin(r)
    ankle_z = local_z + BALL_FORWARD * (1.0 - math.cos(r))
    out = -1.0 if side == "l" else 1.0
    yaw_out = math.radians(rear_toe_out * smooth((behind + 0.05) / 0.3) + 8.0)
    toes = (out * math.sin(yaw_out) * math.cos(r), -math.sin(r), math.cos(yaw_out) * math.cos(r))
    # The knee over the toes, a little more out as the leg goes behind.
    knee = (out * (0.2 + 0.4 * smooth(behind / 0.4)), 0.25, 1.0)
    foot = (x - hip.x, y - hip.y, ankle_z - hip.z)
    length = math.sqrt(foot[0] ** 2 + foot[1] ** 2 + foot[2] ** 2)
    stats["reach"] = max(stats["reach"], length)
    return {"foot": foot, "knee": knee, "toe": toes, "space": "root"}
