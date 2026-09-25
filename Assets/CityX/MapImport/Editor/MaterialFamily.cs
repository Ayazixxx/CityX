namespace CityX.MapImport
{
    /// <summary>
    /// What kind of surface a Source material name describes.
    ///
    /// This is the one genuinely valuable thing the original materials carry
    /// that we can keep: the mapper's INTENT. He decided this wall is brick,
    /// that floor is concrete, that catwalk is grating. The images belong to
    /// Valve; the decisions are his, and they are what make a map read as a
    /// place rather than a set of boxes.
    ///
    /// So the names get classified into families, and each family gets a
    /// texture generated from scratch. Nothing is copied or derived -- the
    /// output is code drawing bricks, not Valve's picture of bricks.
    /// </summary>
    public enum MaterialFamily
    {
        Unknown,
        Brick,
        Concrete,
        MetalWall,
        MetalGrate,
        MetalFloor,
        Wood,
        WoodPlank,
        Glass,
        Stone,
        GrassDirt,
        DevGrid,
        Tool,
    }

    public static class MaterialFamilyClassifier
    {
        /// <summary>
        /// Maps a Source material path to a family.
        ///
        /// Ordering matters: the more specific checks run first, because
        /// "METAL/METALGRATE013A2" is both metal AND a grate, and a grate
        /// needs holes in it rather than a flat metal panel.
        /// </summary>
        public static MaterialFamily Classify(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
            {
                return MaterialFamily.Unknown;
            }

            string name = materialName.ToUpperInvariant();

            if (name.StartsWith("TOOLS/"))
            {
                return MaterialFamily.Tool;
            }
            if (name.StartsWith("DEV/"))
            {
                return MaterialFamily.DevGrid;
            }
            if (name.Contains("GRASS") || name.Contains("DIRT"))
            {
                return MaterialFamily.GrassDirt;
            }
            if (name.Contains("GLASS"))
            {
                return MaterialFamily.Glass;
            }
            if (name.Contains("GRATE"))
            {
                return MaterialFamily.MetalGrate;
            }
            if (name.Contains("METALFLOOR"))
            {
                return MaterialFamily.MetalFloor;
            }
            // DE_NUKE's "NUKMETWALL" set is metal panelling despite not
            // living under METAL/.
            if (name.Contains("METAL") || name.Contains("MET"))
            {
                return MaterialFamily.MetalWall;
            }
            if (name.Contains("BRICK"))
            {
                return MaterialFamily.Brick;
            }
            if (name.Contains("CRATE"))
            {
                return MaterialFamily.Wood;
            }
            if (name.StartsWith("WOOD/") || name.Contains("WOOD"))
            {
                return MaterialFamily.WoodPlank;
            }
            if (name.Contains("STONE"))
            {
                return MaterialFamily.Stone;
            }
            if (name.Contains("CONCRETE"))
            {
                return MaterialFamily.Concrete;
            }
            return MaterialFamily.Unknown;
        }

        /// <summary>
        /// A stable per-material tint, so the five different brick materials
        /// do not all come out identical. Derived from the name itself, which
        /// keeps it deterministic and means "TINTBEIGE" reliably differs from
        /// plain brick without anyone hand-authoring a table.
        /// </summary>
        public static float VariantHue(string materialName)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in materialName)
                {
                    hash = hash * 31 + c;
                }
                return (hash & 0x7fffffff) % 1000 / 1000.0f;
            }
        }

        /// <summary>Honours obvious colour words in the original name.</summary>
        public static bool MentionsBeige(string name) => name.ToUpperInvariant().Contains("BEIGE");
        public static bool MentionsBlue(string name) => name.ToUpperInvariant().Contains("BLUE");
        public static bool MentionsSnow(string name) => name.ToUpperInvariant().Contains("SNOW");
    }
}
