namespace CityX.Character
{
    /// <summary>
    /// THE HOBOS' MAKES (docs/HOBO.md; owner, 2026-11-24: "make an enemy called hobo and it spawns as either the character i made named hobo2 ...
    /// or ... just hobo ... with either a metal pole, broken glass bottle. or a chain, or a machete as their weapon. its random which one and
    /// which skin they get"). Each look with each weapon is a prefab of its own (HoboBuilder), so every machine in a session spawns the same one
    /// without being told which: "hobo" is any of them, rolled where it is asked for (the console, the mob spawner).
    /// </summary>
    public static class HoboKinds
    {
        /// <summary>What /spawn and the mob spawner ask for: any hobo, rolled.</summary>
        public const string Any = "hobo";

        /// <summary>The owner's hobo characters (Character/NpcLooks): "Hobo", "hobo2", and "brack guy" (owner, 2026-11-24: "add my character
        /// "brack guy" as one of the hobos that can spawn").</summary>
        public static readonly string[] Looks = { "1", "2", "3" };

        /// <summary>The weapons a hobo is found with: the bo staff ("metal pole"), a broken glass bottle, the chain, the machete, the wood plank (owner, 2026-11-24: "also make wood plank one of the weapons hobos can spawn with").</summary>
        public static readonly string[] Weapons = { "pole", "bottle", "chain", "machete", "plank" };

        /// <summary>
        /// The share of hobos walking about with the weapon in hand; the rest take it out when hurt or in a fight (owner, 2026-11-24: "make it so
        /// homeless guys dont usually walk around with weapon in hand maybe 20 percent of them do. they usually only pull it out when attacked").
        /// </summary>
        public const float CalmArmedShare = 0.2f;

        /// <summary>Every hobo's kind name (EnemyNetwork's, the console's), each look with each weapon: "hobo_1_pole" ... "hobo_3_plank".</summary>
        public static readonly string[] All = BuildAll();

        private static string[] BuildAll()
        {
            var all = new string[Looks.Length * Weapons.Length];
            for (int look = 0; look < Looks.Length; look++)
            {
                for (int weapon = 0; weapon < Weapons.Length; weapon++)
                {
                    all[look * Weapons.Length + weapon] = KindOf(look, weapon);
                }
            }
            return all;
        }

        public static string KindOf(int look, int weapon) => $"{Any}_{Looks[look]}_{Weapons[weapon]}";

        /// <summary>The prefab's name for a kind ("hobo_1_pole" is Hobo_1_Pole): a fallen one's loot window reads "Hobo" (CorpseLoot.NameOf).</summary>
        public static string PrefabNameOf(int look, int weapon) => $"Hobo_{Looks[look]}_{char.ToUpperInvariant(Weapons[weapon][0])}{Weapons[weapon].Substring(1)}";

        public static bool IsHobo(string kind) => kind == Any || System.Array.IndexOf(All, kind) >= 0;

        /// <summary>A hobo by a roll of 0 to 1: every look and weapon as likely as any other.</summary>
        public static string Pick(float roll) => All[System.Math.Min(All.Length - 1, (int)(System.Math.Max(0.0f, roll) * All.Length))];

        /// <summary>A hobo of either look carrying <paramref name="weapon"/> (one of <see cref="Weapons"/>), the look by a roll of 0 to 1; null for no such weapon.</summary>
        public static string PickWith(string weapon, float roll)
        {
            int index = System.Array.IndexOf(Weapons, weapon);
            return index < 0 ? null : KindOf(System.Math.Min(Looks.Length - 1, (int)(System.Math.Max(0.0f, roll) * Looks.Length)), index);
        }
    }
}
