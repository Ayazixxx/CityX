namespace CityX.Character
{
    /// <summary>
    /// THE BARE-HANDED GUARD (owner, 2026-11-10: "improve upon the blocking no weapon in hand animation right now the legs dont even move
    /// and it isnt timed right at all. redo the hole thing like a souls like game"). Held, as the sword's guard is and as a Souls game's
    /// is: up for as long as right-click is, its first instant a parry, a blow soaked for stamina, the body walking on at half pace under it.
    /// It was a block thrown for a fixed 0.7 s with a borrowed full-body clip, which stood the legs still whatever the body did.
    /// The names are what the animation driver is told (and other players' copies with it); the clips are SourceArt/FistGuard's, on the
    /// upper body only, so the legs are the walk's.
    /// </summary>
    public static class FistGuard
    {
        /// <summary>The guard comes up; it is let down; a blow is soaked on it; a blow is parried by it.</summary>
        public const string RaiseName = "fist_guard";
        public const string LowerName = "fist_guard_lower";
        public const string HitName = "fist_guard_hit";
        public const string ParryName = "fist_guard_parry";

        public const string MotionPath = "Fists/FistGuardMotion";

        /// <summary>The clips: coming up from standing ready, held (a loop), a blow soaked, a blow swatted aside.</summary>
        public const string RaiseClip = "guard_raise";
        public const string HoldClip = "guard_hold";
        public const string HitClip = "guard_hit";
        public const string ParryClip = "guard_parry";

        /// <summary>The guard's first instant parries, seconds: the sword's own window (SoulsSwordCombat.PerfectGuardSeconds).</summary>
        public const float ParrySeconds = 0.2f;

        /// <summary>
        /// Stamina a soaked blow costs, per point of its damage: half as much again as a blade's guard. Forearms are a poorer guard than
        /// steel, as bare fists are in a Souls game.
        /// </summary>
        public const float StaminaPerDamage = 1.5f;

        /// <summary>How long a guard broken open (a blow it could not afford) leaves the body staggered, seconds.</summary>
        public const float BreakStunSeconds = 1.0f;

        /// <summary>The body's pace under a held guard, as a share of its own: the sword's guarded walk.</summary>
        public const float MoveFraction = 0.5f;

        /// <summary>How quickly the guard's pose takes the arms, and gives them back, seconds.</summary>
        public const float EngageSeconds = 0.08f;
        public const float LowerSeconds = 0.2f;

        public static BakedMotion Motion => BakedMotion.Load(MotionPath);

        public static bool Has(string name) => name == RaiseName || name == LowerName || name == HitName || name == ParryName;
    }
}
