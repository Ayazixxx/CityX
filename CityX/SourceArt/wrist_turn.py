# THE RIGHT HAND TURNED ON ITS FOREARM, AND A HAND PUT EXACTLY WHERE A CARRIED THING HANGS. The solver's empty hand (Octaslash/octaslash_pose.py) carries on its forearm's line,
# its palm turned with the elbow. A move that holds something under or over the palm needs more:
#   * a key's `wrist` (ikey) turns the hand on the forearm after the solve (turn_wrist), degrees: (back, the hand lifted toward its own back; side, toward the thumb; roll, the
#     back of the hand rolled toward the thumb's side), eased from key to key as the fist is (sample_wrist);
#   * fit_hand moves a key's wrist and turns its hand to where the hand's hold of a thing IS the thing as it hangs on the body, so it is taken off the body, and laid back on
#     it, without a jump.
# Loaded with exec after the rig, game_frames.py, the solver and FistCombo/fist_pose.py (its key, sample_field, body_pose): GiantBoomerang/giant_boomerang_pose.py,
# IronSpikedBoomerang/iron_spiked_moves.py.
import math


def ikey(fist, off, wrist=(0.0, 0.0, 0.0), **more):
    """fist_pose.key with the right hand's turn on its forearm."""
    k = key(fist, off, **more)
    k["wrist"] = V(*wrist)
    return k


_FINGERS = unit(TAIL["hand_r"] - HEAD["hand_r"])
_THUMB = unit(U(0.0, 0.0, 1.0))
_BACK = unit(_THUMB.cross(_FINGERS))
if (HEAD["hand_r"] + _BACK * 0.1 - HEAD["spine"]).length < (HEAD["hand_r"] - _BACK * 0.1 - HEAD["spine"]).length:
    _BACK = -_BACK                                         # at rest the palms face the body: the back of the hand faces away from it


def _sign(axis, start, toward):
    return 1.0 if (Quaternion(axis, 0.1) @ start).dot(toward) > 0.0 else -1.0


_BEND, _SIDE, _ROLL = _sign(_THUMB, _FINGERS, _BACK), _sign(_BACK, _FINGERS, _THUMB), _sign(_FINGERS, _BACK, _THUMB)


def _wrist_turn(wrist):
    return (Quaternion(_FINGERS, math.radians(wrist.z) * _ROLL) @ Quaternion(_BACK, math.radians(wrist.y) * _SIDE)
            @ Quaternion(_THUMB, math.radians(wrist.x) * _BEND))


def turn_wrist(D, pos, wrist):
    """The right hand turned on its forearm."""
    D["hand_r"] = D["forearm_r"] @ _wrist_turn(wrist)
    D["fingertip_r"] = D["hand_r"]
    pos["fingertip_r"] = pos["hand_r"] + D["hand_r"] @ (HEAD["fingertip_r"] - HEAD["hand_r"])


def sample_wrist(keys, t01):
    """A key's wrist at a share of the move, eased as the fist is."""
    return sample_field(keys, t01, "wrist", V(0, 0, 0), 2.0)


def unity_frame(D, pos, bone, rest):
    """A solved bone's place in the character's frame, as the game has it (`rest`: its place at the rig's rest, game_frames.py)."""
    turn = D[bone].to_matrix().to_4x4()
    at = Matrix.Translation(pos[bone]) @ turn @ Matrix.Translation(-HEAD[bone])        # the rest's world carried to the pose, Blender axes
    return TO_BLENDER.inverted() @ at @ TO_BLENDER @ rest


def _solved_key(k, turned):
    """A key held still, solved (the legs standing: they do not move the trunk or the arm)."""
    s = sample([(0.0, k, None), (1.0, k, "smooth")], 0.0)
    P, _, _ = body_pose(s, 1.0)
    legs = standing_legs()
    P["l_leg"], P["r_leg"] = legs["l_leg"], legs["r_leg"]
    D, pos = solve(P)
    if turned:
        turn_wrist(D, pos, k.get("wrist", V(0, 0, 0)))
    return D, pos


def _turn_miss(wrist, local):
    """Degrees between the hand turned by `wrist` and the turn wanted (a turn and its opposite quaternion are the same turn)."""
    return math.degrees(2.0 * math.acos(min(1.0, abs(_wrist_turn(wrist).dot(local)))))


def _refined(best, local):
    """A wrist turn searched out from `best` (miss, wrist) a degree step at a time, coarse to fine: the angles read off the turn miss a large one."""
    miss, wrist = best
    step = 32.0
    while step >= 0.25:
        moved = True
        while moved:
            moved = False
            for axis in range(3):
                for sign in (1.0, -1.0):
                    tried = wrist.copy()
                    tried[axis] += sign * step
                    tried_miss = _turn_miss(tried, local)
                    if tried_miss < miss - 1e-4:
                        miss, wrist, moved = tried_miss, tried, True
        step *= 0.5
    return miss, wrist


def fit_hand(k, hand_wanted, name="the grab", bone="spine", rest=SPINE_REST):
    """Moves key `k`'s right wrist, and turns its hand on the forearm, to where `hand_wanted(frame)` has the hand (both the game's frames; `frame` is `bone`'s as the key
    poses it, `rest` its place at the rig's rest: the spine, where a carried thing hangs, or the head, where a drink is put to the mouth). Returns the wrist's turn."""
    best = None
    for _ in range(3):
        D, pos = _solved_key(k, True)
        wanted = hand_wanted(unity_frame(D, pos, bone, rest))
        k["fist"] = V(*wanted.to_translation())
        D, pos = _solved_key(k, False)
        # The hand's turn on the forearm, in the rig's own terms: wanted = forearm @ local.
        wanted_turn = (TO_BLENDER @ wanted @ HAND_REST.inverted() @ TO_BLENDER.inverted()).to_quaternion()
        local = D["forearm_r"].inverted() @ wanted_turn
        basis = Matrix((_FINGERS, _BACK, _THUMB)).transposed()
        e = (basis.transposed() @ local.to_matrix() @ basis).to_euler('ZYX')
        best = None
        for sr in (1.0, -1.0):
            for ss in (1.0, -1.0):
                for sb in (1.0, -1.0):
                    wrist = V(math.degrees(e.z) * sb, math.degrees(e.y) * ss, math.degrees(e.x) * sr)
                    miss = _turn_miss(wrist, local)
                    if best is None or miss < best[0]:
                        best = (miss, wrist)
        if best[0] > 1.0:
            best = _refined(best, local)
        k["wrist"] = best[1]
    D, pos = _solved_key(k, True)
    reached = unity_frame(D, pos, "hand_r", HAND_REST)
    short = (reached.to_translation() - hand_wanted(unity_frame(D, pos, bone, rest)).to_translation()).length
    print("%s fitted: wrist at (%.2f, %.2f, %.2f), the hand turned on the forearm (%.0f, %.0f, %.0f), %.1f deg and %.2f m off"
          % (name, k["fist"].x, k["fist"].y, k["fist"].z, best[1].x, best[1].y, best[1].z, best[0], short))
    return best[1]
