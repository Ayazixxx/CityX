using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What a body holds out in front of it stays out of the wall it faces (owner, 2026-11-18, ten swordsmen pressing at a wall from the street:
    /// "they were poking through at first with their swords and body parts"; asked, "stop the poking too"). The capsule stops the body at a wall
    /// ahead, but the hands and the blade reach past the capsule: the hands keep the body back from the wall as far as they reach into it, and
    /// the blade turns off the wall onto its face.
    ///
    /// A part is in the wall ahead only when the line from the body's own middle out to it, at its height, meets the wall: running into a window
    /// opening, that line goes through the opening and the hands that touch its sides beside it hold nothing back (PlayerLocomotion.WallClearance).
    /// What breaks (glass, planks) is not held off by the hands, so a fist still cracks a pane, nor by the blade while a swing is live, so a cut
    /// still takes a pane out.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How far clear of a wall a hand's bone is kept, metres on a body of the player's size: the flesh past the bone.</summary>
        private const float HandSkin = 0.04f;

        /// <summary>How far clear of a wall the blade's edge is kept, metres.</summary>
        private const float BladeSkin = 0.02f;

        /// <summary>A surface whose way out is steeper than this (its share up or down) is a floor or a ceiling, not a wall.</summary>
        private const float ReachWallSteepest = 0.7f;

        /// <summary>How many times the blade is turned off a wall in one frame: once more for the wall it may meet turning off the first (a corner).</summary>
        private const int BladePasses = 3;

        private static readonly HumanBodyBones[] ReachBones =
        {
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal,
        };

        private Transform[] _reachBones;
        private readonly WallStandoff _aheadStandoff = new WallStandoff();
        private readonly Vector3[] _aheadNeeds = new Vector3[16];
        private readonly Vector3[] _handContacts = new Vector3[8];
        private readonly Vector3[] _aheadContacts = new Vector3[8];
        private static readonly RaycastHit[] ReachHits = new RaycastHit[16];

        /// <summary>
        /// Each wall a hand or a fingertip has gone into, found from the body's middle: the way out of it along the ground and how far, written
        /// into <paramref name="into"/>; returns how many. Most are a wall ahead; one beside is the side of an opening the body stands in front
        /// of, a hand reaching past its edge into the wall beside it.
        /// </summary>
        private int HandsIntoWalls(Vector3[] into)
        {
            if (_animator == null)
            {
                return 0;
            }
            if (_reachBones == null)
            {
                _reachBones = new Transform[ReachBones.Length];
                for (int i = 0; i < ReachBones.Length; i++)
                {
                    _reachBones[i] = _animator.GetBoneTransform(ReachBones[i]);
                }
            }
            Vector3 root = transform.position;
            int outs = 0;
            foreach (Transform bone in _reachBones)
            {
                if (bone == null || outs >= into.Length)
                {
                    continue;
                }
                Vector3 point = bone.position;
                Vector3 from = new Vector3(root.x, point.y, root.z);
                if (!WallOnTheWay(from, point, true, out RaycastHit hit))
                {
                    continue;
                }
                Vector3 way = new Vector3(hit.normal.x, 0.0f, hit.normal.z).normalized;
                float depth = Vector3.Dot(hit.point - point, way) + HandSkin * BodyScale;
                if (depth > 0.0f)
                {
                    into[outs++] = way * depth;
                }
            }
            return outs;
        }

        /// <summary>
        /// The hands out of what they are still in once the body has been kept back (KeepTheBodyOutOfWalls): a body that cannot step back,
        /// pressed from behind by a crowd or backed against another wall, bends its elbows instead. Each hand is moved back out, fingertips
        /// included, and the arm solved to it from the shoulder (TwoBoneIK) with the elbow bowing the way it already bows; the hand keeps its
        /// own turn, so what it holds keeps pointing where it pointed.
        /// </summary>
        private void KeepTheHandsOutOfWalls()
        {
            if (_animator == null || IsSitting || InCarSeat)
            {
                return;
            }
            KeepTheHandOutOfWalls(_rightUpperArm, _rightLowerArm, _handR, 1);
            KeepTheHandOutOfWalls(_leftUpperArm, _leftLowerArm, _handL, 0);
        }

        /// <param name="side">0 the left hand, 1 the right: its fingertip is <see cref="ReachBones"/>[2 + side].</param>
        private void KeepTheHandOutOfWalls(Transform upper, Transform lower, Transform hand, int side)
        {
            if (upper == null || lower == null || hand == null)
            {
                return;
            }
            Vector3 root = transform.position;
            Vector3 move = Vector3.zero;
            float most = 0.0f;
            Transform fingertip = _reachBones != null ? _reachBones[2 + side] : null;
            for (int i = 0; i < 2; i++)
            {
                Vector3 point = i == 0 ? hand.position : fingertip != null ? fingertip.position : hand.position;
                if (!WallOnTheWay(new Vector3(root.x, point.y, root.z), point, true, out RaycastHit hit))
                {
                    continue;
                }
                Vector3 way = new Vector3(hit.normal.x, 0.0f, hit.normal.z).normalized;
                float depth = Vector3.Dot(hit.point - point, way) + HandSkin * BodyScale;
                if (depth > most)
                {
                    most = depth;
                    move = way * depth;
                }
            }
            if (most <= 0.0f)
            {
                return;
            }
            Vector3 middle = (upper.position + hand.position) * 0.5f;
            Quaternion held = hand.rotation;
            TwoBoneIK.Solve(upper, lower, hand, hand.position + move, lower.position - middle);
            hand.rotation = held;
        }

        /// <summary>
        /// THE BLADE OFF THE WALL. If the held blade runs into a wall, the hand is turned about the upright until the tip lies on the wall's face:
        /// pressed at a wall in guard the sword lies along it, a cut that meets a wall scrapes along it. The ground under a blade's tip is
        /// KeepTipAboveGround's. Every body, this one or another player's copy, since it is only the drawn pose.
        /// </summary>
        private void KeepTheBladeOutOfWalls()
        {
            ISwordBlade blade = _handR != null ? HeldSword : null;
            if (blade == null || blade.IsChain)   // (a chain has no blade to turn off a wall)
            {
                return;
            }
            if (!_wallRagdollLooked)
            {
                _wallRagdoll = GetComponent<Ragdoll>();
                _wallRagdollLooked = true;
            }
            if (_wallRagdoll != null && _wallRagdoll.IsLimp)
            {
                return;   // a limp body's sword falls where physics drops it
            }
            bool swingLive = SwordCombat != null && SwordCombat.Phase == SoulsPhase.Active;
            // From the hand, which the sword turns about, to the tip: the whole of what it holds out, hilt and all.
            Vector3 root = transform.position;
            Vector3 grip = _handR.position;
            if (WallOnTheWay(new Vector3(root.x, grip.y, root.z), grip, swingLive, out _))
            {
                return;   // the hand itself is past a wall: a body that is not kept out of walls this frame (moved by the network, climbing)
            }
            for (int pass = 0; pass < BladePasses; pass++)
            {
                Vector3 tip = blade.BladeTipWorld;
                Vector3 line = tip - grip;
                float length = line.magnitude;
                if (length < 1e-3f || !WallOnTheWay(grip, tip, swingLive, out RaycastHit hit))
                {
                    return;
                }
                // Turned about the upright, so the blade keeps its slope: tipped toward the wall's face instead, a blade held low into the
                // foot of a wall went down into the ground there.
                Vector3 normal = new Vector3(hit.normal.x, 0.0f, hit.normal.z).normalized;
                Vector3 across = Vector3.Cross(Vector3.up, normal);
                Vector3 flat = new Vector3(line.x, 0.0f, line.z);
                float reach = flat.magnitude;
                if (reach < 1e-3f)
                {
                    return;
                }
                float wanted = (BladeSkin - Vector3.Dot(grip - hit.point, normal)) / reach;
                float now = Vector3.Dot(flat, normal) / reach;
                if (now >= wanted)
                {
                    return;
                }
                float from = Mathf.Atan2(Vector3.Dot(flat, across), Vector3.Dot(flat, normal));
                float to = Mathf.Acos(Mathf.Clamp(wanted, -1.0f, 1.0f)) * (from < 0.0f ? -1.0f : 1.0f);
                _handR.rotation = Quaternion.AngleAxis((to - from) * Mathf.Rad2Deg, Vector3.up) * _handR.rotation;
            }
        }

        /// <summary>How many tops and undersides the line is followed past, looking for a wall behind them.</summary>
        private const int PastLedges = 4;

        /// <summary>
        /// The nearest wall of the world's own on the line from <paramref name="from"/> to <paramref name="to"/>: not a body, not anything loose,
        /// and not what breaks when <paramref name="skipBreakables"/>. A top or an underside on the way (a window's ledge) is looked past: a
        /// collider answers a line once, with the first face it meets, so the wall the ledge stands out from, in the same building, would never
        /// be heard of.
        /// </summary>
        private bool WallOnTheWay(Vector3 from, Vector3 to, bool skipBreakables, out RaycastHit nearest)
        {
            nearest = default;
            Vector3 line = to - from;
            float left = line.magnitude;
            if (left < 1e-4f)
            {
                return false;
            }
            Vector3 direction = line / left;
            for (int ledge = 0; ledge <= PastLedges && left > 0.0f; ledge++)
            {
                int hits = Physics.RaycastNonAlloc(from, direction, ReachHits, left, ~0, QueryTriggerInteraction.Ignore);
                bool found = false;
                for (int i = 0; i < hits; i++)
                {
                    RaycastHit hit = ReachHits[i];
                    Collider collider = hit.collider;
                    if ((found && hit.distance >= nearest.distance) || !IsWall(collider)
                        || (skipBreakables && collider.GetComponentInParent<IBreakableSurface>() != null))
                    {
                        continue;
                    }
                    nearest = hit;
                    found = true;
                }
                if (!found)
                {
                    return false;
                }
                if (Mathf.Abs(nearest.normal.y) <= ReachWallSteepest)
                {
                    return true;
                }
                float past = nearest.distance + LookPastLedge;
                from += direction * past;
                left -= past;
            }
            nearest = default;
            return false;
        }

        /// <summary>
        /// Part of the world, not something in it: not a body (this one or another, or anything else blows land on: a training dummy), not
        /// anything loose, not something held (another sword: a blade on a guard is the clash's to stop, BlockDeflectEffect).
        /// </summary>
        private bool IsWall(Collider collider)
        {
            return collider.attachedRigidbody == null && !(collider is CharacterController) && !collider.transform.IsChildOf(transform)
                && collider.GetComponentInParent<IDamageable>() == null && collider.GetComponentInParent<ISwordBlade>() == null
                && collider.GetComponentInParent<CityX.Items.IEquippableItem>() == null;
        }

        /// <summary>How far past a ledge's face the line is taken up again, metres.</summary>
        private const float LookPastLedge = 0.002f;
    }
}
