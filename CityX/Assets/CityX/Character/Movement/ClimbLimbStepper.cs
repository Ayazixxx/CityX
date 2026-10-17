using System;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// PROCEDURAL WALL CLIMBING, the way Breath of the Wild shows it: the hands and feet are PLANTED on the wall, the body moves past them, and when a limb
    /// has been left too far behind it takes a quick STEP to a new hold ahead, the diagonal pairs alternating (left hand with right foot, then right hand with
    /// left foot), the other pair carrying the weight. The old climb was a clip that waved both arms over the head and bent the legs wherever it liked, and then
    /// had its limbs shoved off the wall by a clamp: bent the wrong way, never touching anything.
    ///
    /// This class is the pure bookkeeping (no scene): given where the shoulders and hips are and where the wall is, it keeps four anchors on the wall and says where
    /// each limb's hand or foot should be this frame. PlayerLocomotion turns them into two-bone IK after the Animator.
    ///
    /// THE HOLDS (second pass, owner 2026-10-13: "update that animation because it looks awful ... allow sideways climbing with A &amp; D"): the first pass hung the
    /// hands 0.62 arm lengths over the shoulders, which on this rig is a straight arm over the head, and the feet half a leg down, straight: a hanging mannequin. Now
    /// each limb has a REST hold (a hand a little above its shoulder and out to the side, a foot below its hip and out) plus a REACH in the direction of travel, so
    /// climbing up the hands reach for holds the arm can only get to bent and the feet step up beside the hips (a frog's push), climbing down the feet lead and the
    /// hands follow at shoulder height, and shuffling sideways the leading hand and foot reach out along the wall and the trailing ones follow. A limb steps when it has
    /// been left behind ALONG THE DIRECTION OF TRAVEL, whatever that direction is, so a change of direction re-plants the holds one pair at a time. The sole of a foot
    /// is pressed flat on the wall (PlayerLocomotion turns it), so a foot's hold is a foot's height off the face.
    ///
    /// The rules, all testable: every anchor is ON the wall surface (never inside it); an anchor does not move while its limb holds; a limb steps only when it is
    /// left behind; only one diagonal pair steps at a time; both hands never step together; a step lasts a fixed short time with a small lift off the wall; the hands
    /// never cross; and no limb is ever asked to reach farther than it can.
    /// </summary>
    public sealed class ClimbLimbStepper
    {
        public enum Limb
        {
            LeftHand = 0,
            RightHand = 1,
            LeftFoot = 2,
            RightFoot = 3,
        }

        /// <summary>Where the body's reference points are this frame, and how long its limbs are.</summary>
        public struct Body
        {
            public Vector3 LeftShoulder, RightShoulder, LeftHip, RightHip;
            public float ArmLength, LegLength;
        }

        /// <summary>A step takes this long, seconds.</summary>
        public const float StepSeconds = 0.13f;

        /// <summary>How far a stepping limb lifts off the wall at the middle of its step, metres.</summary>
        public const float LiftMetres = 0.09f;

        /// <summary>A hand left this far behind its ideal hold (in arm lengths, along the travel) steps.</summary>
        public const float HandStrain = 0.36f;

        /// <summary>A foot left this far behind its ideal hold (in leg lengths, along the travel) steps.</summary>
        public const float FootStrain = 0.34f;

        /// <summary>A step overshoots the ideal hold by this much in the direction of travel, in limb lengths, so the next step is not due at once.</summary>
        public const float Lead = 0.10f;

        /// <summary>How far outside the shoulder line the hands hold, and outside the hip line the feet, in limb lengths.</summary>
        public const float SpreadHand = 0.26f;

        public const float SpreadFoot = 0.30f;

        /// <summary>A hand's rest hold is this far above its shoulder, in arm lengths (about chin height: bent, not stretched).</summary>
        public const float HandRestUp = 0.22f;

        /// <summary>A foot's rest hold is this far below its hip, in leg lengths.</summary>
        public const float FootRestDown = 0.42f;

        /// <summary>How far a hand reaches along the travel, in arm lengths: far when climbing up (the hands lead), less sideways (a starfish otherwise), a little when climbing down (the feet lead).</summary>
        public const float HandReachUp = 0.42f;

        public const float HandReachSide = 0.32f;

        public const float HandReachDown = 0.18f;

        /// <summary>How far a foot reaches along the travel, in leg lengths: a high step beside the hip going up; a shuffle sideways; a long reach going down.</summary>
        public const float FootReachUp = 0.26f;

        public const float FootReachSide = 0.26f;

        public const float FootReachDown = 0.34f;

        /// <summary>The reach for a limb going <paramref name="travel"/>: by whichever of along-the-wall and up-or-down the travel is mostly.</summary>
        private static float ReachFor(bool hand, Vector2 travel)
        {
            bool sideways = Mathf.Abs(travel.x) > Mathf.Abs(travel.y);
            if (hand)
            {
                return sideways ? HandReachSide : (travel.y < 0.0f ? HandReachDown : HandReachUp);
            }
            return sideways ? FootReachSide : (travel.y > 0.0f ? FootReachUp : FootReachDown);
        }

        /// <summary>A hand's hold sits this far off the wall surface, metres (a palm is not flush with it).</summary>
        public const float HandOffWall = 0.03f;

        /// <summary>A foot's hold is the ANKLE: with the sole pressed flat on the wall, the ankle is a foot's height off the face.</summary>
        public static readonly float FootOffWall = BodyRig.FootY + 0.01f;

        private readonly Vector3[] _anchor = new Vector3[4];
        private readonly Vector3[] _from = new Vector3[4];
        private readonly Vector3[] _to = new Vector3[4];
        private readonly float[] _t = { 1.0f, 1.0f, 1.0f, 1.0f };
        private bool _planted;

        /// <summary>True once the limbs have been put on the wall (<see cref="Plant"/>).</summary>
        public bool IsPlanted => _planted;

        public bool IsStepping(Limb limb) => _t[(int)limb] < 1.0f;

        /// <summary>True while either limb of the diagonal pair (left hand + right foot, or right hand + left foot) is mid-step.</summary>
        public bool PairStepping(int pair) => IsStepping(pair == 0 ? Limb.LeftHand : Limb.RightHand) || IsStepping(pair == 0 ? Limb.RightFoot : Limb.LeftFoot);

        public Vector3 Anchor(Limb limb) => _anchor[(int)limb];

        public void Reset()
        {
            _planted = false;
            for (int i = 0; i < 4; i++)
            {
                _t[i] = 1.0f;
            }
        }

        /// <summary>The body's right when it faces the wall: along the wall face.</summary>
        public static Vector3 Across(Vector3 normal)
        {
            Vector3 across = Vector3.Cross(Vector3.up, -normal);
            return across.sqrMagnitude > 1e-6f ? across.normalized : Vector3.right;
        }

        /// <summary>The way the body is going on the wall, in the world: <paramref name="travel"/> is (along the wall to the right, up), each -1 to 1; zero holding.</summary>
        public static Vector3 TravelDirection(Vector3 normal, Vector2 travel)
        {
            Vector3 move = Across(normal) * travel.x + Vector3.up * travel.y;
            return move.sqrMagnitude > 1.0f ? move.normalized : move;
        }

        private static bool IsHand(Limb limb) => limb == Limb.LeftHand || limb == Limb.RightHand;

        /// <summary>The hold a limb would ideally have right now, on the wall plane, for a body travelling <paramref name="travel"/> (see <see cref="TravelDirection"/>).</summary>
        public Vector3 Ideal(Limb limb, in Body body, Vector3 wallPoint, Vector3 normal, Vector2 travel)
        {
            bool hand = IsHand(limb);
            Vector3 root = limb == Limb.LeftHand ? body.LeftShoulder
                : limb == Limb.RightHand ? body.RightShoulder
                : limb == Limb.LeftFoot ? body.LeftHip
                : body.RightHip;
            float limbLength = hand ? body.ArmLength : body.LegLength;
            float rest = hand ? HandRestUp : -FootRestDown;
            float reach = ReachFor(hand, travel);
            Vector3 move = TravelDirection(normal, travel);

            // SPREAD OUT, as a climber's limbs are: the hands wider than the shoulders and the feet wider than the hips, not both hands at the head.
            Vector3 right = body.RightShoulder - body.LeftShoulder;
            right.y = 0.0f;
            right = right.sqrMagnitude > 1e-6f ? right.normalized : Across(normal);
            bool leftSide = limb == Limb.LeftHand || limb == Limb.LeftFoot;
            float spread = hand ? SpreadHand * body.ArmLength : SpreadFoot * body.LegLength;
            Vector3 outward = (leftSide ? -right : right) * spread;

            Vector3 p = root + Vector3.up * (rest * limbLength) + outward + move * (reach * limbLength);
            return OnWall(p, wallPoint, normal, hand ? HandOffWall : FootOffWall);
        }

        /// <summary>The same for a plain up (+1) or down (-1) climb.</summary>
        public Vector3 Ideal(Limb limb, in Body body, Vector3 wallPoint, Vector3 normal, int direction)
        {
            return Ideal(limb, body, wallPoint, normal, new Vector2(0.0f, direction < 0 ? -1.0f : 1.0f));
        }

        private static Vector3 OnWall(Vector3 p, Vector3 wallPoint, Vector3 normal, float offset)
        {
            return p - normal * (Vector3.Dot(p - wallPoint, normal) - offset);
        }

        /// <summary>
        /// Puts the four limbs on the wall at a grab: the diagonal pair going up high, the other pair a little lower, as a climber hangs.
        /// <paramref name="snap"/> (optional) moves a point onto the real surface (a raycast), since a wall is rarely a perfect plane.
        /// </summary>
        public void Plant(in Body body, Vector3 wallPoint, Vector3 normal, Vector2 travel, Func<Vector3, Vector3, Vector3> snap = null)
        {
            if (travel.sqrMagnitude < 1e-4f)
            {
                travel = Vector2.up;   // a fresh grab is laid out as a climb up
            }
            for (int i = 0; i < 4; i++)
            {
                Limb limb = (Limb)i;
                Vector3 ideal = Ideal(limb, body, wallPoint, normal, travel);
                bool otherPair = limb == Limb.RightHand || limb == Limb.LeftFoot;
                float limbLength = IsHand(limb) ? body.ArmLength : body.LegLength;
                if (otherPair)
                {
                    ideal -= TravelDirection(normal, travel) * (limbLength * 0.22f);
                }
                _anchor[i] = snap != null ? snap(ideal, normal) : ideal;
                _t[i] = 1.0f;
            }
            _planted = true;
        }

        public void Plant(in Body body, Vector3 wallPoint, Vector3 normal, int direction, Func<Vector3, Vector3, Vector3> snap = null)
        {
            Plant(body, wallPoint, normal, new Vector2(0.0f, direction < 0 ? -1.0f : 1.0f), snap);
        }

        /// <summary>Where the limb's hand or foot should be this frame: its anchor while it holds, along its step while it moves.</summary>
        public Vector3 Target(Limb limb, Vector3 normal)
        {
            int i = (int)limb;
            if (_t[i] >= 1.0f)
            {
                return _anchor[i];
            }
            float u = Mathf.SmoothStep(0.0f, 1.0f, _t[i]);
            return Vector3.Lerp(_from[i], _to[i], u) + normal * (Mathf.Sin(u * Mathf.PI) * LiftMetres);
        }

        /// <summary>Advances the steps and starts one when a pair has been left behind along the travel. Holding still (zero travel), nothing steps.</summary>
        public void Tick(float deltaTime, in Body body, Vector3 wallPoint, Vector3 normal, Vector2 travel, Func<Vector3, Vector3, Vector3> snap = null)
        {
            if (!_planted)
            {
                Plant(body, wallPoint, normal, travel, snap);
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                if (_t[i] < 1.0f)
                {
                    _t[i] = Mathf.Min(1.0f, _t[i] + deltaTime / StepSeconds);
                    if (_t[i] >= 1.0f)
                    {
                        _anchor[i] = _to[i];
                    }
                }
            }

            // Only one pair steps at a time: the other holds the weight. Hanging still, nothing is left behind.
            if (PairStepping(0) || PairStepping(1) || travel.sqrMagnitude < 1e-4f)
            {
                return;
            }

            // How far behind each pair is. The pair that has been left further behind goes next.
            float strain0 = PairStrain(0, body, wallPoint, normal, travel);
            float strain1 = PairStrain(1, body, wallPoint, normal, travel);
            float strain = Mathf.Max(strain0, strain1);
            if (strain < 1.0f)
            {
                return;
            }
            int pair = strain0 >= strain1 ? 0 : 1;
            StartPair(pair, body, wallPoint, normal, travel, snap);
        }

        /// <summary>The same for a plain up (+1) or down (-1) climb, or holding (0).</summary>
        public void Tick(float deltaTime, in Body body, Vector3 wallPoint, Vector3 normal, int direction, Func<Vector3, Vector3, Vector3> snap = null)
        {
            Tick(deltaTime, body, wallPoint, normal, new Vector2(0.0f, Mathf.Clamp(direction, -1, 1)), snap);
        }

        // 1.0 means a limb of the pair is exactly at its strain limit.
        private float PairStrain(int pair, in Body body, Vector3 wallPoint, Vector3 normal, Vector2 travel)
        {
            Limb hand = pair == 0 ? Limb.LeftHand : Limb.RightHand;
            Limb foot = pair == 0 ? Limb.RightFoot : Limb.LeftFoot;
            float h = Strain(hand, body, wallPoint, normal, travel) / (HandStrain * body.ArmLength);
            float f = Strain(foot, body, wallPoint, normal, travel) / (FootStrain * body.LegLength);
            return Mathf.Max(h, f);
        }

        private float Strain(Limb limb, in Body body, Vector3 wallPoint, Vector3 normal, Vector2 travel)
        {
            // Measured along the direction of travel: how far the hold is behind where it ought to be.
            Vector3 ideal = Ideal(limb, body, wallPoint, normal, travel);
            Vector3 move = TravelDirection(normal, travel);
            if (move.sqrMagnitude > 1e-6f) move.Normalize();
            float behind = Vector3.Dot(ideal - _anchor[(int)limb], move);
            return Mathf.Max(0.0f, behind);
        }

        private void StartPair(int pair, in Body body, Vector3 wallPoint, Vector3 normal, Vector2 travel, Func<Vector3, Vector3, Vector3> snap)
        {
            Limb hand = pair == 0 ? Limb.LeftHand : Limb.RightHand;
            Limb foot = pair == 0 ? Limb.RightFoot : Limb.LeftFoot;
            StartStep(hand, body.ArmLength, body, wallPoint, normal, travel, snap);
            StartStep(foot, body.LegLength, body, wallPoint, normal, travel, snap);
        }

        private void StartStep(Limb limb, float limbLength, in Body body, Vector3 wallPoint, Vector3 normal, Vector2 travel, Func<Vector3, Vector3, Vector3> snap)
        {
            int i = (int)limb;
            Vector3 move = TravelDirection(normal, travel);
            if (move.sqrMagnitude > 1e-6f) move.Normalize();
            Vector3 destination = Ideal(limb, body, wallPoint, normal, travel) + move * (Lead * limbLength);
            _from[i] = _anchor[i];
            _to[i] = snap != null ? snap(destination, normal) : destination;
            _t[i] = 0.0f;
        }
    }
}
