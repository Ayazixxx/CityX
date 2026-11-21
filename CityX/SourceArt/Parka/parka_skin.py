# THE PARKA'S SKINNING (exec after ../garment_rig.py), shared by parka_build.py (the game's mesh) and parka_pose_check.py (the pictures of
# it with its arms raised): the Midnight Coat's steps (../MidnightCoat/coat_build.py), with the sleeves told apart from the coat's body.
ARMS_FROM = (1.50, 1.62)   # the coat's own cloth follows an arm not at all below the first height, as the skin does from the second up
ARMPIT_REACH = 0.50        # wider and smoother than the Midnight Coat's 0.24 and 6: the parka's armpit hangs 38 cm under the shoulder
ARMPIT_ABOVE = 0.04        # joint, and a puffy sleeve's underarm is a wide web, which wants a long even blend
ARMPIT_SMOOTHING = 24
SKIRT_FROM, SKIRT_FULL = 1.15, 0.9    # the skirt bones take over from the hips from a little above the hip joints down to here: higher than
#                                       the Midnight Coat's 0.95 and 0.72, so a knee raised to hip height in a swing lifts the waist (SkirtCloth)
COLLAR_FROM, COLLAR_RISE, COLLAR_FRONT = 1.95, 0.11, 0.12   # the fur ring and the hood behind it: the Midnight Coat's collar,
COLLAR_REACH = 0.50        # but wider: the fur ring reaches 0.48 m out from the neck, and nothing else of the coat is out there that high
HEAD_CENTRE = (0.0, 0.0, 2.251)   # the game's head, a ball (BodyRig.HeadCentreY, FusedBodyShape.HeadRadius)
HEAD_RADIUS = 0.35
HEAD_NEAR = (0.04, 0.10)   # cloth this near the head's surface goes wholly onto the collar bone, fading out by the second


def skin_parka(coat, body, rig):
    """The parka's points and their weights, one column per TARGET_BONES: the nearest skin's; only the sleeves on the arms; the skirt on
    the skirt bones; the armpits shared with the half-turning shoulder bones; the fur ring, the hood and whatever lies near the head on the collar bone."""
    co = mesh_coords(coat.data)
    weights = body_weights(coat, body, rig)
    weights = sleeves_only(weights, co, coat.data, ARMS_FROM)
    weights = hang_skirt(weights, co, SKIRT_FROM, SKIRT_FULL)
    weights = share_armpits(weights, co, coat.data, ARMPIT_REACH, ARMPIT_ABOVE, ARMPIT_SMOOTHING)
    weights = lift_collar(weights, co, COLLAR_FROM, COLLAR_RISE, COLLAR_REACH, COLLAR_FRONT)
    return co, hug_head(weights, co)


def hug_head(weights, co):
    """The cloth round the head on the collar bone, from however low it hangs: the fur ring's inner edge sits right under the head's
    round underside, and on the shoulders' bones a sprint (the head dips 3.4 cm) sank the head 3.6 cm into it."""
    off = np.linalg.norm(co - np.array(HEAD_CENTRE), axis=1) - HEAD_RADIUS
    near = np.clip((HEAD_NEAR[1] - off) / (HEAD_NEAR[1] - HEAD_NEAR[0]), 0.0, 1.0)
    near = near * near * (3.0 - 2.0 * near)
    took = near * (1.0 - weights[:, BONE["collar"]])
    weights = weights * (1.0 - took)[:, None]
    weights[:, BONE["collar"]] += took
    print("head: %d more points on the collar bone" % ((took > 0.05).sum()))
    return weights
