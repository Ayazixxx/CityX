using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>The headings the Stats page groups its rows under, in the owner's order.</summary>
    public enum StatGroup
    {
        Vitals,
        Offense,
        Precision,
        Mobility,
        Defense,
    }

    /// <summary>Every stat a point can go into (owner, 2026-10-14). The order is the Stats page's.</summary>
    public enum StatId
    {
        MaxHealth,
        HealthRegen,
        MaxStamina,
        StaminaRegen,
        MaxMana,
        ManaRegen,
        MeleeDamage,
        MeleeSpeed,
        FirearmDamage,
        ReloadSpeed,
        FireRate,
        SpellPower,
        CastSpeed,
        CooldownReduction,
        ArmorPenetration,
        Accuracy,
        RecoilControl,
        MovementSpeed,
        FlightSpeed,
        VehicleHandling,
        Armor,
        MagicResistance,
        Stability,
        BlockChance,
    }

    /// <summary>How a stat's effect reads on the page.</summary>
    public enum StatUnit
    {
        /// <summary>A plain amount: "+20 health".</summary>
        Amount,
        /// <summary>An amount a second: "+0.5 health a second".</summary>
        PerSecond,
        /// <summary>A share: "+6%".</summary>
        Percent,
        /// <summary>Armour and magic resistance: a rating whose share of damage stopped is shown beside it.</summary>
        Rating,
    }

    /// <summary>One stat: what it is called, what it does (the owner's own words), and what a point is worth.</summary>
    public sealed class StatDefinition
    {
        public readonly StatId Id;

        /// <summary>The name a save stores it under: never changes, so stats can be added or reordered.</summary>
        public readonly string Key;

        public readonly string Name;
        public readonly StatGroup Group;
        public readonly string Description;

        /// <summary>What one point adds: an amount, a fraction (0.02 = 2%), or rating points.</summary>
        public readonly float PerPoint;

        /// <summary>The most the stat can add however many points go in (a fraction for a share).</summary>
        public readonly float Cap;

        public readonly StatUnit Unit;

        /// <summary>What the effect is of: "health", "melee speed"... for the page's "+6% melee speed".</summary>
        public readonly string Noun;

        /// <summary>False for a stat whose part of the game does not exist yet (wings, the new cars): points can go in, nothing changes yet.</summary>
        public readonly bool InGame;

        public StatDefinition(StatId id, string key, string name, StatGroup group, string description, float perPoint, float cap,
            StatUnit unit, string noun, bool inGame = true)
        {
            Id = id;
            Key = key;
            Name = name;
            Group = group;
            Description = description;
            PerPoint = perPoint;
            Cap = cap;
            Unit = unit;
            Noun = noun;
            InGame = inGame;
        }

        /// <summary>What <paramref name="points"/> add, capped.</summary>
        public float Effect(int points) => Mathf.Min(Mathf.Max(0, points) * PerPoint, Cap);

        /// <summary>The effect as the page shows it: "+20 health", "+0.5 health a second", "+6% melee speed", "25 armour (20% less damage)".</summary>
        public string Describe(int points)
        {
            float effect = Effect(points);
            switch (Unit)
            {
                case StatUnit.PerSecond:
                    return $"+{effect:0.##} {Noun} a second";
                case StatUnit.Percent:
                    return $"+{effect * 100.0f:0.#}% {Noun}";
                case StatUnit.Rating:
                    return $"{effect:0.#} {Noun} ({StatCatalog.RatingReduction(effect) * 100.0f:0.#}% less damage)";
                default:
                    return $"+{effect:0.##} {Noun}";
            }
        }
    }

    /// <summary>
    /// THE STATS (owner, 2026-10-14: "I want to add all of these stats to the stats page ... and make them actually effect the character as the
    /// stat describes ... if the stat describes something that isnt in the game just add it"; Dodge Efficiency taken back out; Flight Speed and
    /// Vehicle Handling added to the list, their mechanics later). What a point is worth and every cap are mine, first values to tune. See
    /// docs/STATS.md.
    /// </summary>
    public static class StatCatalog
    {
        /// <summary>Armour (and magic resistance) stops rating / (rating + this) of the damage: 100 rating stops half, never all.</summary>
        public const float RatingHalfPoint = 100.0f;

        /// <summary>Everyone's chance to stop a blow outright while blocking, before any points (the owner: "it starts at a lower chance").</summary>
        public const float BaseBlockChance = 0.1f;

        private static readonly StatDefinition[] List =
        {
            // --- Vitals ---
            new StatDefinition(StatId.MaxHealth, "max-health", "Max Health", StatGroup.Vitals,
                "Total amount of health you can have.", 10.0f, float.MaxValue, StatUnit.Amount, "health"),
            new StatDefinition(StatId.HealthRegen, "health-regen", "Health Regen", StatGroup.Vitals,
                "How quickly your health recovers over time.", 0.25f, float.MaxValue, StatUnit.PerSecond, "health"),
            new StatDefinition(StatId.MaxStamina, "max-stamina", "Max Stamina", StatGroup.Vitals,
                "Total stamina for dodging, sprinting, attacking, and parkour.", 10.0f, float.MaxValue, StatUnit.Amount, "stamina"),
            new StatDefinition(StatId.StaminaRegen, "stamina-regen", "Stamina Regen", StatGroup.Vitals,
                "How quickly your stamina recovers.", 0.04f, float.MaxValue, StatUnit.Percent, "stamina regen"),
            new StatDefinition(StatId.MaxMana, "max-mana", "Max Mana", StatGroup.Vitals,
                "Total mana for casting spells.", 10.0f, float.MaxValue, StatUnit.Amount, "mana"),
            new StatDefinition(StatId.ManaRegen, "mana-regen", "Mana Regen", StatGroup.Vitals,
                "How quickly your mana recovers.", 0.05f, float.MaxValue, StatUnit.Percent, "mana regen"),

            // --- Offense ---
            new StatDefinition(StatId.MeleeDamage, "melee-damage", "Melee Damage", StatGroup.Offense,
                "Increases damage dealt with swords and melee weapons.", 0.02f, float.MaxValue, StatUnit.Percent, "melee damage"),
            new StatDefinition(StatId.MeleeSpeed, "melee-speed", "Melee Speed", StatGroup.Offense,
                "Makes your melee attacks faster.", 0.015f, 0.6f, StatUnit.Percent, "melee speed"),
            new StatDefinition(StatId.FirearmDamage, "firearm-damage", "Firearm Damage", StatGroup.Offense,
                "Increases damage dealt with guns.", 0.02f, float.MaxValue, StatUnit.Percent, "firearm damage"),
            new StatDefinition(StatId.ReloadSpeed, "reload-speed", "Reload Speed", StatGroup.Offense,
                "How quickly you reload guns.", 0.02f, 0.75f, StatUnit.Percent, "reload speed"),
            new StatDefinition(StatId.FireRate, "fire-rate", "Fire Rate", StatGroup.Offense,
                "Increases how fast guns fire.", 0.015f, 0.6f, StatUnit.Percent, "fire rate"),
            new StatDefinition(StatId.SpellPower, "spell-power", "Spell Power", StatGroup.Offense,
                "Increases damage (and healing) of spells.", 0.02f, float.MaxValue, StatUnit.Percent, "spell power"),
            new StatDefinition(StatId.CastSpeed, "cast-speed", "Cast Speed", StatGroup.Offense,
                "Makes spells cast faster.", 0.015f, 0.6f, StatUnit.Percent, "cast speed"),
            new StatDefinition(StatId.CooldownReduction, "cooldown-reduction", "Cooldown Reduction", StatGroup.Offense,
                "Reduces the cooldown of your abilities and spells.", 0.01f, 0.5f, StatUnit.Percent, "shorter cooldowns"),
            new StatDefinition(StatId.ArmorPenetration, "armor-penetration", "Armor Penetration", StatGroup.Offense,
                "Reduces the effectiveness of enemy armor and block chance.", 0.02f, 1.0f, StatUnit.Percent, "of their armour and block ignored"),

            // --- Precision ---
            new StatDefinition(StatId.Accuracy, "accuracy", "Accuracy", StatGroup.Precision,
                "Improves gun precision and reduces bullet spread.", 0.03f, 0.9f, StatUnit.Percent, "less spread"),
            new StatDefinition(StatId.RecoilControl, "recoil-control", "Recoil Control", StatGroup.Precision,
                "Reduces gun recoil and helps the weapon settle faster after shooting.", 0.02f, 0.8f, StatUnit.Percent, "less recoil"),

            // --- Mobility ---
            new StatDefinition(StatId.MovementSpeed, "movement-speed", "Movement Speed", StatGroup.Mobility,
                "Increases your base running and walking speed.", 0.01f, 0.4f, StatUnit.Percent, "movement speed"),
            new StatDefinition(StatId.FlightSpeed, "flight-speed", "Flight Speed", StatGroup.Mobility,
                "How fast you fly with wings, a jetpack and the like.", 0.01f, 0.5f,
                StatUnit.Percent, "flight speed", inGame: false),
            new StatDefinition(StatId.VehicleHandling, "vehicle-handling", "Vehicle Handling", StatGroup.Mobility,
                "Improves steering, acceleration, and overall control of ground and flying vehicles.",
                0.02f, 1.0f, StatUnit.Percent, "handling", inGame: false),

            // --- Defense ---
            new StatDefinition(StatId.Armor, "armor", "Armor", StatGroup.Defense,
                "Physical resistance: reduces damage taken from melee attacks and bullets.", 5.0f, float.MaxValue, StatUnit.Rating, "armour"),
            new StatDefinition(StatId.MagicResistance, "magic-resistance", "Magic Resistance", StatGroup.Defense,
                "Reduces damage taken from spells.", 5.0f, float.MaxValue, StatUnit.Rating, "resistance"),
            new StatDefinition(StatId.Stability, "stability", "Stability", StatGroup.Defense,
                "Makes you harder to stagger or interrupt while attacking, blocking, or casting.", 0.03f, 0.75f, StatUnit.Percent, "stability"),
            new StatDefinition(StatId.BlockChance, "block-chance", "Block Chance", StatGroup.Defense,
                "Chance to fully block or deflect attacks (including bullets) while holding block.", 0.02f, 0.7f, StatUnit.Percent, "block chance"),
        };

        public static IReadOnlyList<StatDefinition> All => List;

        public static int Count => List.Length;

        public static StatDefinition Get(StatId id) => List[(int)id];

        public static StatDefinition FindByKey(string key)
        {
            foreach (StatDefinition stat in List)
            {
                if (stat.Key == key)
                {
                    return stat;
                }
            }
            return null;
        }

        /// <summary>The share of damage a rating stops: rating / (rating + <see cref="RatingHalfPoint"/>).</summary>
        public static float RatingReduction(float rating) => rating <= 0.0f ? 0.0f : rating / (rating + RatingHalfPoint);
    }
}
