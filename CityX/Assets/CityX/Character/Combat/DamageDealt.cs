using System;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>Something with a health bar: what a blow's share of the bar is measured against (DamageDealt.ShareOfBar).</summary>
    public interface IHealthBar
    {
        float Health { get; }

        float MaxHealth { get; }
    }

    /// <summary>
    /// A blow that landed, told to whoever shows it (the damage numbers, UI/Hud/DamageNumbers) the moment it lands on this machine: who struck
    /// it, what it hit and where, how much, and how big a share of the target's whole health bar that one blow was. Raised by
    /// <see cref="DamageContext.Strike"/> for blows struck with a striker named; a hit forwarded from another player's machine to its owner
    /// names none, so it is never shown twice.
    /// </summary>
    public readonly struct DamageDealt
    {
        public readonly GameObject Striker;
        public readonly IDamageable Target;
        public readonly Vector3 Point;
        public readonly float Amount;

        /// <summary>0 to 1: the blow's amount over the target's maximum health (1 for a target with no health bar).</summary>
        public readonly float ShareOfBar;

        public readonly DamageKind Kind;

        /// <summary>A heavy blow (its stun counts as heavy, HitImpactEffects.HeavyHitstunSeconds): shown bigger.</summary>
        public readonly bool Heavy;

        public DamageDealt(GameObject striker, IDamageable target, Vector3 point, float amount, float shareOfBar, DamageKind kind, bool heavy)
        {
            Striker = striker;
            Target = target;
            Point = point;
            Amount = amount;
            ShareOfBar = shareOfBar;
            Kind = kind;
            Heavy = heavy;
        }

        /// <summary>Every blow that lands with a striker named, on this machine.</summary>
        public static event Action<DamageDealt> Landed;

        /// <summary>The share of <paramref name="target"/>'s whole health bar a blow of <paramref name="amount"/> is, 0 to 1.</summary>
        public static float ShareOf(IDamageable target, float amount)
        {
            float max = target is IHealthBar bar ? bar.MaxHealth : 0.0f;
            return max > 0.0f ? Mathf.Clamp01(amount / max) : 1.0f;
        }

        /// <summary>Where a blow with no contact point is shown: a little above the middle of the target's body.</summary>
        public static Vector3 PointOn(IDamageable target)
        {
            if (!(target is Component component) || component == null)
            {
                return Vector3.zero;
            }
            Collider body = component.GetComponentInChildren<CharacterController>();
            if (body == null)
            {
                body = component.GetComponentInChildren<Collider>();
            }
            if (body == null)
            {
                return component.transform.position + Vector3.up;
            }
            Bounds bounds = body.bounds;
            return bounds.center + Vector3.up * (bounds.extents.y * 0.5f);
        }

        internal static void Report(GameObject striker, IDamageable target, float amount, float hitstunSeconds, DamageKind kind,
            Vector3? contactPoint, HitResponse response)
        {
            if (striker == null || response != HitResponse.Landed || Landed == null || !(amount > 0.0f))
            {
                return;
            }
            Landed(new DamageDealt(striker, target, contactPoint ?? PointOn(target), amount, ShareOf(target, amount), kind,
                hitstunSeconds >= HitImpactEffects.HeavyHitstunSeconds));
        }
    }
}
