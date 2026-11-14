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
        /// (HitImpactEffects) bleeds from there, and a killing blow takes off the part there (Dismemberer). A blow forwarded from another
        /// player's machine brings its point with it (NetworkedDamage). Null when nobody said -- a blow struck outside any scope -- and the
        /// wound is placed on the body's near side instead.
        /// </summary>
        public static UnityEngine.Vector3? ContactPoint { get; private set; }

        /// <summary>
        /// The blow breaks a held guard (the sword's held-click heavy; owner, 2026-10-24: "make it so holding click does a heavy guardbreaking
        /// attack"): a guard that catches it is broken open instead of soaking it. Only a parry, the guard's first instant, still stops it.
        /// Separate from <see cref="Scope"/> so a <see cref="Strike"/> inside it keeps it.
        /// </summary>
        public static bool BreaksGuard { get; private set; }

        /// <summary>Opens a scope in which blows break guards (or not, when <paramref name="breaks"/> is false).</summary>
        public static GuardBreakScope GuardBreaking(bool breaks) => new GuardBreakScope(breaks);

        /// <summary>
        /// The blow draws no blood (a jet of water, 2026-10-28): the wound shows its flash and its stagger but no burst and no splat. Separate
        /// from <see cref="Scope"/> as <see cref="BreaksGuard"/> is.
        /// </summary>
        public static bool Bloodless { get; private set; }

        /// <summary>Opens a scope in which blows draw no blood.</summary>
        public static BloodlessScope WithoutBlood() => new BloodlessScope(true);

        /// <summary>
        /// The blow is struck with an edge (a sword): a killing blow with plenty to spare cuts the part it hit off rather than bursting it
        /// (docs/DISMEMBERMENT.md). Separate from <see cref="Scope"/> as <see cref="BreaksGuard"/> is.
        /// </summary>
        public static bool Edged { get; private set; }

        /// <summary>Opens a scope in which blows are struck with an edge (or not, when <paramref name="edged"/> is false).</summary>
        public static EdgedScope Cutting(bool edged) => new EdgedScope(edged);

        /// <summary>
        /// How much colder the blow makes its target (the Frost Staff's spray, docs/FROST.md): the body's own Chill takes it where the blow lands.
        /// Separate from <see cref="Scope"/> as <see cref="BreaksGuard"/> is.
        /// </summary>
        public static float Chill { get; private set; }

        /// <summary>Opens a scope in which blows chill their target by <paramref name="amount"/>.</summary>
        public static ChillScope Chilling(float amount) => new ChillScope(amount);

        /// <summary>
        /// The blow is a critical hit (a backstab, Backstab): shown in gold where it lands (DamageDealt). Separate from <see cref="Scope"/>
        /// as <see cref="BreaksGuard"/> is.
        /// </summary>
        public static bool Critical { get; private set; }

        /// <summary>Opens a scope in which blows are critical hits (or not).</summary>
        public static CriticalScope Critically(bool critical) => new CriticalScope(critical);

        /// <summary>
        /// The blow lands as a heavy one whatever its stun (a chain's finisher: its stagger is short so it cannot be looped): the bigger freeze,
        /// burst, jolt and number. Separate from <see cref="Scope"/> as <see cref="BreaksGuard"/> is.
        /// </summary>
        public static bool HeavyBlow { get; private set; }

        /// <summary>Opens a scope in which blows land as heavy ones (or not).</summary>
        public static HeavyBlowScope LandingHeavily(bool heavy) => new HeavyBlowScope(heavy);

        /// <summary>
        /// A player struck the blow: armour stops less of it (ArmourRules.PlayerBlowCap). <see cref="Strike"/> says so from its striker; a
        /// blow forwarded from another player's machine names no striker and brings this with it (NetworkedDamage).
        /// </summary>
        public static bool ByPlayer { get; private set; }

        /// <summary>Opens a scope in which blows are a player's (or not).</summary>
        public static ByPlayerScope StruckByPlayer(bool byPlayer) => new ByPlayerScope(byPlayer);

        public readonly struct ByPlayerScope : IDisposable
        {
            private readonly bool _before;

            public ByPlayerScope(bool byPlayer)
            {
                _before = ByPlayer;
                ByPlayer = byPlayer;
            }

            public void Dispose()
            {
                ByPlayer = _before;
            }
        }

        public readonly struct HeavyBlowScope : IDisposable
        {
            private readonly bool _before;

            public HeavyBlowScope(bool heavy)
            {
                _before = HeavyBlow;
                HeavyBlow = heavy;
            }

            public void Dispose()
            {
                HeavyBlow = _before;
            }
        }

        public readonly struct CriticalScope : IDisposable
        {
            private readonly bool _before;

            public CriticalScope(bool critical)
            {
                _before = Critical;
                Critical = critical;
            }

            public void Dispose()
            {
                Critical = _before;
            }
        }

        public readonly struct ChillScope : IDisposable
        {
            private readonly float _before;

            public ChillScope(float amount)
            {
                _before = Chill;
                Chill = amount;
            }

            public void Dispose()
            {
                Chill = _before;
            }
        }

        public readonly struct EdgedScope : IDisposable
        {
            private readonly bool _before;

            public EdgedScope(bool edged)
            {
                _before = Edged;
                Edged = edged;
            }

            public void Dispose()
            {
                Edged = _before;
            }
        }

        public readonly struct BloodlessScope : IDisposable
        {
            private readonly bool _before;

            public BloodlessScope(bool bloodless)
            {
                _before = Bloodless;
                Bloodless = bloodless;
            }

            public void Dispose()
            {
                Bloodless = _before;
            }
        }

        public readonly struct GuardBreakScope : IDisposable
        {
            private readonly bool _before;

            public GuardBreakScope(bool breaks)
            {
                _before = BreaksGuard;
                BreaksGuard = breaks;
            }

            public void Dispose()
            {
                BreaksGuard = _before;
            }
        }

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
            return Strike(target, amount, fromPosition, hitstunSeconds, kind, penetration, contactPoint, null);
        }

        /// <summary>
        /// The same, saying who struck it: a blow that lands is then told to whoever shows it (<see cref="DamageDealt.Landed"/>, the damage
        /// numbers), and a blow that kills pays its striker what the kill is worth (<see cref="IXpReward"/>). Every weapon and spell names its
        /// wielder; a hit forwarded from another player's machine names none (EnemyNetwork credits that kill instead).
        /// </summary>
        public static HitResponse Strike(IDamageable target, float amount, UnityEngine.Vector3 fromPosition, float hitstunSeconds,
            DamageKind kind, float penetration, UnityEngine.Vector3? contactPoint, UnityEngine.GameObject striker)
        {
            if (target == null)
            {
                return HitResponse.Ignored;
            }
            if (EnemyPack.AreAllies(striker, target))
            {
                return HitResponse.Ignored;   // one enemy's blow on another: they are on the same side (EnemyPack)
            }
            if (Opponents.SameSide(striker, target))
            {
                return HitResponse.Ignored;   // a player's blow on a summon on their side (Opponents)
            }
            bool wasAlive = target.IsAlive;
            HitResponse response;
            using (new Scope(kind, penetration, contactPoint))
            using (StruckByPlayer(striker != null ? PlayerBodies.IsPlayer(BodyOf(striker)) : ByPlayer))
            {
                response = target.ApplyHit(amount, fromPosition, hitstunSeconds);
            }
            DamageDealt.Report(striker, target, amount, hitstunSeconds, kind, contactPoint, response);
            if (wasAlive && !target.IsAlive && striker != null && target is IXpReward reward)
            {
                PlayerVitals killer = striker.GetComponentInParent<PlayerVitals>();
                if (killer == null)
                {
                    killer = Opponents.SummonerOf(striker);   // a summon's kill is its summoner's
                }
                if (killer != null)
                {
                    killer.AddXp(reward.XpReward);
                }
            }
            return response;
        }

        /// <summary>The body a striker belongs to: the one with stats above it (a weapon's wielder), or the striker itself.</summary>
        private static UnityEngine.GameObject BodyOf(UnityEngine.GameObject striker)
        {
            PlayerVitals vitals = striker.GetComponentInParent<PlayerVitals>();
            return vitals != null ? vitals.gameObject : striker;
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
