using UnityEngine;

namespace CityX.Character
{
    /// <summary>A part of the body a killing blow can take off (or burst): what was hit, as near as the blow's point says.</summary>
    public enum BodyPart
    {
        Torso = 0,
        Head = 1,
        LeftUpperArm = 2,
        LeftLowerArm = 3,
        RightUpperArm = 4,
        RightLowerArm = 5,
        LeftUpperLeg = 6,
        LeftLowerLeg = 7,
        RightUpperLeg = 8,
        RightLowerLeg = 9,
    }

    /// <summary>What a blow does to the body: nothing more, a clean cut, or the part burst into gore.</summary>
    public enum Dismemberment
    {
        None = 0,
        Sever = 1,
        Burst = 2,
    }

    /// <summary>A blow as it landed on a body: the one that killed it (PlayerVitals.LastKillingBlow), or one on its corpse.</summary>
    public struct Blow
    {
        /// <summary>How much it did, and how much the body had left before it (none, for a corpse).</summary>
        public float Damage;

        public float HealthLeft;

        /// <summary>Where it met the body, when the striker knew (a blade's sweep, a bullet's hit).</summary>
        public Vector3? Contact;

        /// <summary>Where it came from.</summary>
        public Vector3 From;

        /// <summary>Struck with an edge (a sword), so it cuts rather than bursts.</summary>
        public bool Edged;

        /// <summary>A blow that draws no blood (a water jet): it takes nothing off.</summary>
        public bool Bloodless;

        /// <summary>How much more it did than the body had left.</summary>
        public float Overkill => Damage - HealthLeft;

        /// <summary>The blow landing now, as its striker described it (<see cref="DamageContext"/>).</summary>
        public static Blow Struck(float damage, float healthLeft, Vector3 from)
        {
            return new Blow
            {
                Damage = damage,
                HealthLeft = healthLeft,
                Contact = DamageContext.ContactPoint,
                From = from,
                Edged = DamageContext.Edged,
                Bloodless = DamageContext.Bloodless,
            };
        }
    }

    /// <summary>
    /// When a blow takes a body apart, and which part (docs/DISMEMBERMENT.md), with no scene. A living body comes apart only on a killing blow
    /// that does way more than it had left (<see cref="OverkillNeeded"/>: at least double); a corpse, under blows that add up to what the part
    /// they land on can take (<see cref="Toughness"/>). An edge cuts the part off, anything else bursts it, a bloodless blow takes nothing. The
    /// part is the bone segment nearest the blow, of those still on; an edge on the torso cuts at the nearest joint instead.
    /// </summary>
    public static class DismemberRules
    {
        /// <summary>
        /// A killing blow must do at least as much again as the body had left (owner, 2026-10-29: "if your killing blow does way more damage
        /// then the amount of health they had left"), and at least this much, or this share of its whole health if that is more.
        /// </summary>
        public const float MinOverkill = 5.0f;

        public const float OverkillShareOfMaxHealth = 0.05f;

        /// <summary>How much of the body's whole health a part of its corpse takes before it comes off: the head, an arm or leg part, the torso.</summary>
        public const float HeadToughness = 0.2f;

        public const float LimbToughness = 0.15f;
        public const float TorsoToughness = 0.4f;

        /// <summary>A forearm's segment runs this far past the wrist (over the hand), as a share of the forearm.</summary>
        public const float HandReach = 1.3f;

        public static float OverkillNeeded(float healthLeft, float maxHealth)
        {
            return Mathf.Max(MinOverkill, Mathf.Max(0.0f, healthLeft), OverkillShareOfMaxHealth * Mathf.Max(0.0f, maxHealth));
        }

        /// <summary>What a killing blow does to a living body.</summary>
        public static Dismemberment Decide(Blow blow, float maxHealth)
        {
            if (blow.Bloodless || float.IsNaN(blow.Overkill) || blow.Overkill < OverkillNeeded(blow.HealthLeft, maxHealth))
            {
                return Dismemberment.None;
            }
            return blow.Edged ? Dismemberment.Sever : Dismemberment.Burst;
        }

        /// <summary>How much damage a part of a corpse takes, all told, before it comes off.</summary>
        public static float Toughness(BodyPart part, float maxHealth)
        {
            float share = part == BodyPart.Head ? HeadToughness : part == BodyPart.Torso ? TorsoToughness : LimbToughness;
            return Mathf.Max(MinOverkill, share * Mathf.Max(0.0f, maxHealth));
        }

        /// <summary>Taking <paramref name="taken"/> off takes <paramref name="part"/> with it: itself, an upper limb its lower half, the torso everything.</summary>
        public static bool Takes(BodyPart taken, BodyPart part)
        {
            switch (taken)
            {
                case BodyPart.Torso: return true;
                case BodyPart.LeftUpperArm: return part == BodyPart.LeftUpperArm || part == BodyPart.LeftLowerArm;
                case BodyPart.RightUpperArm: return part == BodyPart.RightUpperArm || part == BodyPart.RightLowerArm;
                case BodyPart.LeftUpperLeg: return part == BodyPart.LeftUpperLeg || part == BodyPart.LeftLowerLeg;
                case BodyPart.RightUpperLeg: return part == BodyPart.RightUpperLeg || part == BodyPart.RightLowerLeg;
                default: return part == taken;
            }
        }

        /// <summary>
        /// The segment a part covers, from where it starts to where it ends, looked up through <paramref name="bone"/>: the head from the head
        /// bone to as far again past it, a forearm on over the hand, a shin to the foot. False when the body lacks a bone it needs.
        /// </summary>
        public static bool Segment(BodyPart part, System.Func<HumanBodyBones, Vector3?> bone, out Vector3 start, out Vector3 end)
        {
            start = end = Vector3.zero;
            Vector3? a, b;
            switch (part)
            {
                case BodyPart.Head:
                    Vector3? neck = bone(HumanBodyBones.Neck);
                    a = bone(HumanBodyBones.Head);
                    b = neck != null && a != null ? a.Value + (a.Value - neck.Value) : (Vector3?)null;
                    break;
                case BodyPart.Torso:
                    a = bone(HumanBodyBones.Hips);
                    b = bone(HumanBodyBones.Neck);
                    break;
                case BodyPart.LeftUpperArm:
                    a = bone(HumanBodyBones.LeftUpperArm);
                    b = bone(HumanBodyBones.LeftLowerArm);
                    break;
                case BodyPart.LeftLowerArm:
                    a = bone(HumanBodyBones.LeftLowerArm);
                    b = Past(a, bone(HumanBodyBones.LeftHand), HandReach);
                    break;
                case BodyPart.RightUpperArm:
                    a = bone(HumanBodyBones.RightUpperArm);
                    b = bone(HumanBodyBones.RightLowerArm);
                    break;
                case BodyPart.RightLowerArm:
                    a = bone(HumanBodyBones.RightLowerArm);
                    b = Past(a, bone(HumanBodyBones.RightHand), HandReach);
                    break;
                case BodyPart.LeftUpperLeg:
                    a = bone(HumanBodyBones.LeftUpperLeg);
                    b = bone(HumanBodyBones.LeftLowerLeg);
                    break;
                case BodyPart.LeftLowerLeg:
                    a = bone(HumanBodyBones.LeftLowerLeg);
                    b = bone(HumanBodyBones.LeftFoot);
                    break;
                case BodyPart.RightUpperLeg:
                    a = bone(HumanBodyBones.RightUpperLeg);
                    b = bone(HumanBodyBones.RightLowerLeg);
                    break;
                default:
                    a = bone(HumanBodyBones.RightLowerLeg);
                    b = bone(HumanBodyBones.RightFoot);
                    break;
            }
            if (a == null || b == null)
            {
                return false;
            }
            start = a.Value;
            end = b.Value;
            return true;
        }

        private static Vector3? Past(Vector3? from, Vector3? to, float share)
        {
            return from == null || to == null ? (Vector3?)null : from.Value + (to.Value - from.Value) * share;
        }

        /// <summary>The part whose segment is nearest <paramref name="point"/>, of those <paramref name="present"/> says are still on (all, when null).</summary>
        public static BodyPart PartAt(Vector3 point, System.Func<HumanBodyBones, Vector3?> bone, System.Func<BodyPart, bool> present = null)
        {
            BodyPart best = BodyPart.Torso;
            float nearest = float.MaxValue;
            for (var part = BodyPart.Torso; part <= BodyPart.RightLowerLeg; part++)
            {
                if ((present != null && !present(part)) || !Segment(part, bone, out Vector3 start, out Vector3 end))
                {
                    continue;
                }
                float distance = DistanceToSegment(point, start, end);
                if (distance < nearest)
                {
                    nearest = distance;
                    best = part;
                }
            }
            return best;
        }

        private static readonly (HumanBodyBones joint, BodyPart part)[] TorsoJoints =
        {
            (HumanBodyBones.Neck, BodyPart.Head),
            (HumanBodyBones.LeftUpperArm, BodyPart.LeftUpperArm),
            (HumanBodyBones.RightUpperArm, BodyPart.RightUpperArm),
            (HumanBodyBones.LeftUpperLeg, BodyPart.LeftUpperLeg),
            (HumanBodyBones.RightUpperLeg, BodyPart.RightUpperLeg),
        };

        /// <summary>
        /// The part an edge takes off: the one it hit, or, when it hit the torso, the one at the joint nearest where it landed (the neck takes
        /// the head, a shoulder the arm, a hip the leg), of those <paramref name="present"/> says are still on. A body is not cut in half: with
        /// nothing left on the torso to cut, it is the torso.
        /// </summary>
        public static BodyPart CutAt(BodyPart hit, Vector3 point, System.Func<HumanBodyBones, Vector3?> bone, System.Func<BodyPart, bool> present = null)
        {
            if (hit != BodyPart.Torso)
            {
                return hit;
            }
            BodyPart best = BodyPart.Torso;
            float nearest = float.MaxValue;
            foreach ((HumanBodyBones joint, BodyPart part) in TorsoJoints)
            {
                Vector3? at = bone(joint);
                if (at == null || (present != null && !present(part)))
                {
                    continue;
                }
                float distance = (at.Value - point).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    best = part;
                }
            }
            return best;
        }

        /// <summary>The bone a part comes off at: everything under it goes with it (the torso: the hips, the whole body).</summary>
        public static HumanBodyBones RootOf(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head: return HumanBodyBones.Head;
                case BodyPart.LeftUpperArm: return HumanBodyBones.LeftUpperArm;
                case BodyPart.LeftLowerArm: return HumanBodyBones.LeftLowerArm;
                case BodyPart.RightUpperArm: return HumanBodyBones.RightUpperArm;
                case BodyPart.RightLowerArm: return HumanBodyBones.RightLowerArm;
                case BodyPart.LeftUpperLeg: return HumanBodyBones.LeftUpperLeg;
                case BodyPart.LeftLowerLeg: return HumanBodyBones.LeftLowerLeg;
                case BodyPart.RightUpperLeg: return HumanBodyBones.RightUpperLeg;
                case BodyPart.RightLowerLeg: return HumanBodyBones.RightLowerLeg;
                default: return HumanBodyBones.Hips;
            }
        }

        public static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 along = end - start;
            float length = along.sqrMagnitude;
            float t = length > 1e-8f ? Mathf.Clamp01(Vector3.Dot(point - start, along) / length) : 0.0f;
            return Vector3.Distance(point, start + along * t);
        }
    }
}
