# THE HEAD CARRIED AHEAD OF WHAT HANGS ON THE BACK (owner, 2026-11-20, of the head through the Giant Boomerang as the hand went up for it: "watch out for clipping through the
# head"). The game's idle and walk carry the head forward on the neck (16 degrees), and a big weapon on the back hangs just behind it; the solver (Octaslash/octaslash_pose.py)
# stands the neck straight up, which put the head 9 cm further back, into the weapon, from the moment a move took the body until the hand had it. A key's `nod` (nodding) tips
# the neck forward by so many degrees after the solve (carry_head), the head still looking where it looked: the idle's while the weapon is on the back, less as it leaves.
# Loaded with exec after the solver and FistCombo/fist_pose.py (its sample_field): GiantBoomerang/giant_boomerang_pose.py, IronSpikedBoomerang/iron_spiked_moves.py.

NOD = 16.0


def nodding(k, nod=NOD):
    """Key `k` with the neck tipped forward by `nod` degrees."""
    return dict(k, nod=nod)


def carry_head(D, pos, keys, t01):
    """The solved neck tipped forward by the keys' `nod` at a share of the move (eased as the shoulders are), the head's own turn kept."""
    D["neck"] = D["neck"] @ pitch(sample_field(keys, t01, "nod", 0.0, 1.6))
    pos["head"] = child_head(D, pos, "head", "neck")
