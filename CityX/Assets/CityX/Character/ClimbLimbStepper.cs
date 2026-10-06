using System;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// PROCEDURAL WALL CLIMBING, the way Breath of the Wild shows it: the hands and feet are PLANTED on the wall, the body moves up past them, and when a limb
    /// has been left too far behind it takes a quick STEP to a new hold ahead, the diagonal pairs alternating (left hand with right foot, then right hand with
    /// left foot), the other pair carrying the weight. The old climb was a clip that waved both arms over the head and bent the legs wherever it liked, and then
    /// had its limbs shoved off the wall by a clamp: bent the wrong way, never touching anything.
    ///
    /// This class is the pure bookkeeping (no scene): given where the shoulders and hips are and where the wall is, it keeps four anchors on the wall and says where
    /// each limb's hand or foot should be this frame. PlayerLocomotion turns them into two-bone IK after the Animator.
    ///
    /// The rules, all testable: every anchor is ON the wall surface (never inside it); an anchor does not move while its limb holds; a limb steps only when it is
    /// left behind (or about to be out of reach); only one diagonal pair steps at a time; both hands never step together; a step lasts a fixed short time with a small lift
    /// off the wall; and no limb is ever asked to reach farther than it can.
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

        /// <summary>A hand left this far behind its ideal hold (in arm lengths) steps.</summary>
        public const float HandStrain = 0.30f;

        /// <summary>A foot left this far behind its ideal hold (in leg lengths) steps.</summary>
        public const float FootStrain = 0.30f;

        /// <summary>A step overshoots the ideal hold by this much in the direction of travel, in limb lengths, so the next step is not due at once.</summary>
        public const float Lead = 0.14f;

        /// <summary>How far outside the shoulder line the hands hold, and outside the hip line the feet, in limb lengths.</summary>
        public const float SpreadHand = 0.28f;

        public const float SpreadFoot = 0.34f;

        /// <summary>Holds sit this far off the wall surface, metres (a palm and a toe are not flush with it).</summary>
        public const float HandOffWall = 0.03f;

        public const float FootOffWall = 0.07f;

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

        /// <summary>The hold a limb would ideally have right now, on the wall plane, for a body climbing in <paramref name="direction"/> (+1 up, -1 down).</summary>
        public Vector3 Ideal(Limb limb, in Body body, Vector3 wallPoint, Vector3 normal, int direction)
        {
            direction = direction < 0 ? -1 : 1;
            bool hand = limb == Limb.LeftHand || limb == Limb.RightHand;
            Vector3 root = limb == Limb.LeftHand ? body.LeftShoulder
                : limb == Limb.RightHand ? body.RightShoulder
                : limb == Limb.LeftFoot ? body.LeftHip
                : body.RightHip;
            // Reaching up for a hold, hands about an arm's length above the shoulder when going up and nearer when going down; the feet below the hips.
            float up = hand
                ? body.ArmLength * (direction > 0 ? 0.62f : 0.30f)
                : -body.LegLength * (direction > 0 ? 0.50f : 0.46f);
            // SPREAD OUT, as a climber's limbs are: the hands wider than the shoulders and the feet wider than the hips, not both hands at the head.
            Vector3 right = body.RightShoulder - body.LeftShoulder;
            right.y = 0.0f;
            right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            bool leftSide = limb == Limb.LeftHand || limb == Limb.LeftFoot;
            float spread = hand ? SpreadHand * body.ArmLength : SpreadFoot * body.LegLength;
            Vector3 outward = (leftSide ? -right : right) * spread;
            return OnWall(root + Vector3.up * up + outward, wallPoint, normal, hand ? HandOffWall : FootOffWall);
        }

        private static Vector3 OnWall(Vector3 p, Vector3 wallPoint, Vector3 normal, float offset)
        {
            return p - normal * (Vector3.Dot(p - wallPoint, normal) - offset);
        }

        /// <summary>
        /// Puts the four limbs on the wall at a grab: the diagonal pair going up high, the other pair a little lower, as a climber hangs.
        /// <paramref name="snap"/> (optional) moves a point onto the real surface (a raycast), since a wall is rarely a perfect plane.
        /// </summary>
        public void Plant(in Body body, Vector3 wallPoint, Vector3 normal, int direction, Func<Vector3, Vector3, Vector3> snap = null)
        {
            for (int i = 0; i < 4; i++)
            {
                Limb limb = (Limb)i;
                Vector3 ideal = Ideal(limb, body, wallPoint, normal, direction);
                bool otherPair = limb == Limb.RightHand || limb == Limb.LeftFoot;
                float limbLength = (limb == Limb.LeftHand || limb == Limb.RightHand) ? body.ArmLength : body.LegLength;
                if (otherPair)
                {
                    ideal -= Vector3.up * (limbLength * 0.22f);
                }
                _anchor[i] = snap != null ? snap(ideal, normal) : ideal;
                _t[i] = 1.0f;
            }
            _planted = true;
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

        /// <summary>Advances the steps and starts one when a pair has been left behind.</summary>
        public void Tick(float deltaTime, in Body body, Vector3 wallPoint, Vector3 normal, int direction, Func<Vector3, Vector3, Vector3> snap = null)
        {
            if (!_planted)
            {
                Plant(body, wallPoint, normal, direction, snap);
                return;
            }
            direction = direction < 0 ? -1 : 1;

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

            // Only one pair steps at a time: the other holds the weight.
            if (PairStepping(0) || PairStepping(1))
            {
                return;
            }

            // How far behind each pair is. The pair that has been left further behind goes next.
            float strain0 = PairStrain(0, body, wallPoint, normal, direction);
            float strain1 = PairStrain(1, body, wallPoint, normal, direction);
            float strain = Mathf.Max(strain0, strain1);
            if (strain < 1.0f)
            {
                return;
            }
            int pair = strain0 >= strain1 ? 0 : 1;
            StartPair(pair, body, wallPoint, normal, direction, snap);
        }

        // 1.0 means a limb of the pair is exactly at its strain limit.
        private float PairStrain(int pair, in Body body, Vector3 wallPoint, Vector3 normal, int direction)
        {
            Limb hand = pair == 0 ? Limb.LeftHand : Limb.RightHand;
            Limb foot = pair == 0 ? Limb.RightFoot : Limb.LeftFoot;
            float h = Strain(hand, body, wallPoint, normal, direction) / (HandStrain * body.ArmLength);
            float f = Strain(foot, body, wallPoint, normal, direction) / (FootStrain * body.LegLength);
            return Mathf.Max(h, f);
        }

        private float Strain(Limb limb, in Body body, Vector3 wallPoint, Vector3 normal, int direction)
        {
            // Measured along the direction of travel: how far the hold is behind where it ought to be.
            Vector3 ideal = Ideal(limb, body, wallPoint, normal, direction);
            float behind = Vector3.Dot(ideal - _anchor[(int)limb], Vector3.up * direction);
            return Mathf.Max(0.0f, behind);
        }

        private void StartPair(int pair, in Body body, Vector3 wallPoint, Vector3 normal, int direction, Func<Vector3, Vector3, Vector3> snap)
        {
            Limb hand = pair == 0 ? Limb.LeftHand : Limb.RightHand;
            Limb foot = pair == 0 ? Limb.RightFoot : Limb.LeftFoot;
            StartStep(hand, body.ArmLength, body, wallPoint, normal, direction, snap);
            StartStep(foot, body.LegLength, body, wallPoint, normal, direction, snap);
        }

        private void StartStep(Limb limb, float limbLength, in Body body, Vector3 wallPoint, Vector3 normal, int direction, Func<Vector3, Vector3, Vector3> snap)
        {
            int i = (int)limb;
            Vector3 destination = Ideal(limb, body, wallPoint, normal, direction) + Vector3.up * (direction * Lead * limbLength);
            _from[i] = _anchor[i];
            _to[i] = snap != null ? snap(destination, normal) : destination;
            _t[i] = 0.0f;
        }
    }
}
