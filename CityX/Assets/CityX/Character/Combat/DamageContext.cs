using System;

namespace CityX.Character
{
    /// <summary>What a blow is made of, which decides what stops it: armour stops physical blows and bullets, magic resistance stops spells.</summary>
    public enum DamageKind
    {
        /// <summary>Fists, blades, a thrown bottle, a claw, the sword's wave.</summary>
        Physical,
        /// <summary>A gun's shot (armour stops it; a block can deflect it).</summary>
        Bullet,
        /// <summary>A spell: fire, ice, poison, acid.</summary>
        Magic,
    }

    /// <summary>
    /// WHAT THE NEXT BLOW IS (2026-10-14, for the stats): its kind and how much of the target's armour and block chance it ignores (the attacker's
    /// Armor Penetration). The attacker opens a scope around the hit (<see cref="Strike"/>) and the target reads it (PlayerVitals) -- the same
    /// "say it just before the hit" shape as PlayerVitals.TakePoise, so IDamageable's one method serves every target unchanged. A blow struck
    /// outside any scope is physical with no penetration, as every blow was before.
    /// </summary>
    public static class DamageContext
    {
        public static DamageKind Kind { get; private set; } = DamageKind.Physical;

        /// <summary>0 to 1: the share of the target's armour and block chance the blow ignores.</summary>
        public static float Penetration { get; private set; }

        /// <summary>
        /// Where the blow met the body, in world space, when the striker knows (a blade's sweep, a bullet's hit): what shows the wound
        /// (HitImpactEffects) bleeds from there. Null when nobody said -- a blow forwarded from another player's machine, a blow struck
        /// outside any scope -- and the wound is placed on the body's near side instead.
        /// </summary>
        public static UnityEngine.Vector3? ContactPoint { get; private set; }

        /// <summary>Opens a scope: until it is disposed, blows are of this kind and penetration.</summary>
        public static Scope As(DamageKind kind, float penetration) => new Scope(kind, penetration, null);

        /// <summary>Strikes a target inside a scope: the kind and penetration reach whatever takes the blow (or relays it to another player).</summary>
        public static HitResponse Strike(IDamageable target, float amount, UnityEngine.Vector3 fromPosition, float hitstunSeconds,
            DamageKind kind, float penetration)
        {
            return Strike(target, amount, fromPosition, hitstunSeconds, kind, penetration, null);
        }

        /// <summary>The same, with where the blow met the body (<see cref="ContactPoint"/>).</summary>
        public static HitResponse Strike(IDamageable target, float amount, UnityEngine.Vector3 fromPosition, float hitstunSeconds,
            DamageKind kind, float penetration, UnityEngine.Vector3? contactPoint)
        {
            if (target == null)
            {
                return HitResponse.Ignored;
            }
            using (new Scope(kind, penetration, contactPoint))
            {
                return target.ApplyHit(amount, fromPosition, hitstunSeconds);
            }
        }

        /// <summary>The penetration of a body's blows: its Armor Penetration stat, or none for a body without stats.</summary>
        public static float PenetrationOf(UnityEngine.Component attacker)
        {
            PlayerVitals vitals = attacker != null ? attacker.GetComponentInParent<PlayerVitals>() : null;
            return vitals != null ? vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
        }

        public readonly struct Scope : IDisposable
        {
            private readonly DamageKind _kindBefore;
            private readonly float _penetrationBefore;
            private readonly UnityEngine.Vector3? _contactBefore;

            public Scope(DamageKind kind, float penetration) : this(kind, penetration, null)
            {
            }

            public Scope(DamageKind kind, float penetration, UnityEngine.Vector3? contactPoint)
            {
                _kindBefore = Kind;
                _penetrationBefore = Penetration;
                _contactBefore = ContactPoint;
                Kind = kind;
                Penetration = UnityEngine.Mathf.Clamp01(penetration);
                ContactPoint = contactPoint;
            }

            public void Dispose()
            {
                Kind = _kindBefore;
                Penetration = _penetrationBefore;
                ContactPoint = _contactBefore;
            }
        }
    }
}
