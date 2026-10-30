using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE BOW'S AND THE CROSSBOW'S NUMBERS (owner, 2026-10-28; docs/ARCHERY.md, with what other games do). The owner's shape: the bow is aimed
    /// with right-click, drawn with left-click held and loosed when it is let go, flying further the longer it was drawn; at full draw the arrow
    /// "travels a large distance instantly like the bullets ... but will eventually start to fall"; the crossbow is aimed the same, fires a loaded
    /// bolt at once at full power, and reloads slowly because it hits hard. So an arrow is a HITSCAN over its flat range, and past it a falling
    /// projectile. The numbers are mine: a full draw in a second (Minecraft's, between Apex's half second and Skyrim's 1.6), a full arrow about
    /// two AK rounds, a bolt nearly four.
    /// </summary>
    public static class ArcheryRules
    {
        // --- the bow ---

        /// <summary>How long the bow takes to draw fully, seconds.</summary>
        public const float FullDrawSeconds = 1.0f;

        /// <summary>The least draw a loose has (a tap): a weak lob, never nothing.</summary>
        public const float MinDraw = 0.15f;

        /// <summary>How far an arrow flies flat (and at once) at a tap and at full draw, metres.</summary>
        public const float TapFlatRange = 12.0f;

        public const float FullFlatRange = 60.0f;

        /// <summary>Its damage at a tap and at full draw, before Firearm Damage (an AK round is 34, a pistol's 20).</summary>
        public const float TapDamage = 18.0f;

        public const float FullDamage = 55.0f;

        /// <summary>How fast the arrow is still going where its flat flight ends and it starts to fall, m/s, at a tap and at full draw.</summary>
        public const float TapFallSpeed = 20.0f;

        public const float FullFallSpeed = 55.0f;


        // --- the crossbow ---

        public const float BoltDamage = 90.0f;

        public const float BoltFlatRange = 80.0f;

        public const float BoltFallSpeed = 65.0f;

        /// <summary>Spanning the crossbow and laying a bolt, seconds (Minecraft's 1.25, Hunt: Showdown's 5.3; "kind of long because crossbow does big damage").</summary>
        public const float CrossbowReloadSeconds = 2.5f;

        // --- arrows and bolts in flight ---

        /// <summary>How fast an arrow is SEEN to cross its flat range, m/s (the hit itself is instant; the arrow catches up in a third of a second at most).</summary>
        public const float VisibleSpeed = 180.0f;

        public const float Gravity = 9.81f;

        /// <summary>A falling arrow is gone after this long in the air, seconds.</summary>
        public const float FallSeconds = 6.0f;

        /// <summary>Where it lands it stays this long, seconds, in the world and in someone.</summary>
        public const float WorldStickSeconds = 20.0f;

        public const float CharacterStickSeconds = 8.0f;

        /// <summary>How deep the head goes into what it hit, metres.</summary>
        public const float EmbedMetres = 0.22f;

        /// <summary>An arrow from its nock to its point, metres (the model runs up +Z from its nock; ArrowBuilder draws it this long).</summary>
        public const float ArrowLength = 0.78f;

        /// <summary>An arrow's sweep radius in flight, metres.</summary>
        public const float Radius = 0.03f;

        /// <summary>The bow's flat range, damage and the speed it starts falling at for a draw of <paramref name="draw"/> (DrawnProfile.Bow).</summary>
        public static float FlatRangeFor(float draw) => DrawnProfile.Bow.FlatRangeFor(draw);

        public static float DamageFor(float draw) => DrawnProfile.Bow.DamageFor(draw);

        public static float FallSpeedFor(float draw) => DrawnProfile.Bow.FallSpeedFor(draw);

        public static float Clamp(float draw) => DrawnProfile.Bow.Clamp(draw);
    }
}
