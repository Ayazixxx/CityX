namespace CityX.Character
{
    /// <summary>
    /// THE BO STAFF'S NUMBERS (docs/BO_STAFF.md; owner, 2026-11-19, with a clip of a staff fighter: "make copy these movements as best you can.
    /// make the model just a basic metal pole nothing special. make it about the length of the spikesword or however long you think it should be
    /// based on the video"). The pole in the clip is about as long as its fighter is tall, and it is gripped near one end so the long end reaches:
    /// a 2.2 m pole on this 2.6 m body, both fists on its last 0.45 m like a bat's. All mine.
    /// </summary>
    public static class StaffRules
    {
        /// <summary>The pole's ends in the sword's frame (the root a hand's breadth above the right fist, the length along +Y), metres: 0.15 m past the left fist below, 1.85 m past the right fist above.</summary>
        public const float Butt = -0.45f, Tip = 1.75f;

        public const float Radius = 0.022f;

        /// <summary>How far ahead a staff catches a target, as a share of a sword's reach (SoulsSwordCombat.ReachMetres): long, short of a spear's.</summary>
        public const float ReachShare = 1.25f;

        /// <summary>The chain's rising blow and the spin throw whoever they land on down limp: this fast away from the blow, and this fast up, m/s.</summary>
        public const float KnockdownSpeed = 6.0f;

        public const float KnockdownLift = 3.0f;
    }
}
