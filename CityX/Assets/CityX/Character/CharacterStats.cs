using CityX.Core;

namespace CityX.Character
{
    /// <summary>
    /// A character's stat points between the save and the body. Since 2026-10-14 a save keeps every stat by name (CharacterSave.Stats); a save
    /// from before has only the old three, which become the stats they always were: Vitality is Max Health, Endurance is Max Stamina, Strength is
    /// Melee Damage. The three old fields are still written, for the character list and any older build reading the file.
    /// </summary>
    public static class CharacterStats
    {
        public static void Load(PlayerVitals vitals, CharacterSave save)
        {
            if (vitals == null || save == null)
            {
                return;
            }
            if (save.Stats != null && save.Stats.Count > 0)
            {
                vitals.Stats.Load(save.Stats);
                return;
            }
            vitals.Stats.Load(null);
            vitals.Stats[StatId.MaxHealth] = save.Vitality;
            vitals.Stats[StatId.MaxStamina] = save.Endurance;
            vitals.Stats[StatId.MeleeDamage] = save.Strength;
        }

        /// <summary>The character list's line: the three stats with the most points ("Max Health 5   Armor 3   Spell Power 2").</summary>
        public static string Summary(CharacterSave save)
        {
            if (save == null)
            {
                return string.Empty;
            }
            var sheet = new StatSheet();
            if (save.Stats != null && save.Stats.Count > 0)
            {
                sheet.Load(save.Stats);
            }
            else
            {
                sheet[StatId.MaxHealth] = save.Vitality;
                sheet[StatId.MaxStamina] = save.Endurance;
                sheet[StatId.MeleeDamage] = save.Strength;
            }
            var best = new System.Collections.Generic.List<StatDefinition>(StatCatalog.All);
            best.RemoveAll(stat => sheet[stat.Id] <= 0);
            if (best.Count == 0)
            {
                return "No stat points spent";
            }
            // Most points first; the page's order among equals.
            best.Sort((a, b) => sheet[b.Id] != sheet[a.Id] ? sheet[b.Id].CompareTo(sheet[a.Id]) : ((int)a.Id).CompareTo((int)b.Id));
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < best.Count && i < 3; i++)
            {
                text.Append(i > 0 ? "   " : string.Empty).Append(best[i].Name).Append(' ').Append(sheet[best[i].Id]);
            }
            return text.ToString();
        }

        public static void Save(PlayerVitals vitals, CharacterSave save)
        {
            if (vitals == null || save == null)
            {
                return;
            }
            save.Stats = vitals.Stats.ToList();
            save.Vitality = vitals.Stats[StatId.MaxHealth];
            save.Endurance = vitals.Stats[StatId.MaxStamina];
            save.Strength = vitals.Stats[StatId.MeleeDamage];
        }
    }
}
