namespace CityX.Weapons
{
    /// <summary>
    /// THE SLINGSHOT'S NUMBERS (owner, 2026-10-30: "make a slingshot weapon, the starter ranged non magic weapon ... make it work similarly to the
    /// bow and arrow where you hold to draw and shoot at full power, make the draw quicker than the bow and just make it shoot a little pebble";
    /// docs/SLINGSHOT.md). Zelda's slingshot is the bow's weaker forerunner (less reach, a stun more than a kill); Bully's charges for more
    /// damage, its Super Slingshot fully in 1.5 s. Ours is drawn and loosed as the bow is (BowWeapon, DrawnProfile.Slingshot): fully in half the
    /// bow's time, for about a third of its damage over half its reach, a pebble fetched into the pouch between shots. The numbers are mine.
    /// </summary>
    public static class SlingshotRules
    {
        /// <summary>How long the pouch takes to draw back fully, seconds (the bow's is 1).</summary>
        public const float FullDrawSeconds = 0.5f;

        /// <summary>How far a pebble flies flat (and at once) at a tap and at full draw, metres.</summary>
        public const float TapFlatRange = 8.0f;

        public const float FullFlatRange = 30.0f;

        /// <summary>Its damage at a tap and at full draw, before Firearm Damage (the bow's 18 and 55, a pistol round 20).</summary>
        public const float TapDamage = 5.0f;

        public const float FullDamage = 20.0f;

        /// <summary>How fast the pebble is still going where its flat flight ends and it starts to fall, m/s, at a tap and at full draw.</summary>
        public const float TapFallSpeed = 15.0f;

        public const float FullFallSpeed = 35.0f;

        /// <summary>Fetching the next pebble into the pouch, seconds: no draw starts sooner after a loose (tapping it would outdo a full draw).</summary>
        public const float ReloadSeconds = 0.25f;

        /// <summary>How fast a pebble is SEEN to cross its flat range, m/s: half an arrow's, so it reads as a flung stone.</summary>
        public const float VisibleSpeed = 90.0f;

        /// <summary>A pebble's size across, metres (a 3 cm stone at the game's 1.3x).</summary>
        public const float PebbleSize = 0.04f;

        /// <summary>How far the pouch is drawn back from the fork, metres: to the chin, short of a bow's draw to the cheek.</summary>
        public const float DrawLength = 0.5f;

        /// <summary>How far above the grip the pouch rests and the pebble flies: between the prongs' tips.</summary>
        public const float PouchAbove = 0.2f;

        /// <summary>A pebble that has hit something drops off it and lies this long, seconds.</summary>
        public const float LieSeconds = 3.0f;
    }
}
