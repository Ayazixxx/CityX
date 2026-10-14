namespace CityX.Character
{
    /// <summary>
    /// THE GUN RELOADS BY NAME. A reload is a baked motion (BakedMotion, made in Blender: CityX/SourceArt/Glock, CityX/SourceArt/AK47) with two
    /// segments: the TACTICAL reload (a magazine swap with a round still chambered) and the EMPTY one (the same, then the slide or bolt racked).
    /// Like a sword attack it is announced by name through PlayerAnimationDriver.PlayLocalState, so another player's copy of the body plays the
    /// same reload on its own copy of the gun (docs/MULTIPLAYER_STATE.md); GunReloadDriver poses the body and the gun's parts.
    /// </summary>
    public static class GunReload
    {
        public const string TacticalName = "gun_reload";

        public const string EmptyName = "gun_reload_empty";

        /// <summary>
        /// A shot, announced to other players' copies (PlayerAnimationDriver.AnnounceState) so they see the flash, the tracer, the brass and the
        /// slide or bolt cycling, and hear it. Never played on the shooter's own body: its gun has already done all of that.
        /// </summary>
        public const string FireName = "gun_fire";

        /// <summary>The motion's segment names (firearm_export.py).</summary>
        public const string TacticalSegment = "tactical";

        public const string EmptySegment = "empty";

        /// <summary>The events the motions mark: the old magazine clear of the gun (the game throws it), the fresh one in hand, seated (the ammo is in), the action racked.</summary>
        public const string DropEvent = "drop";

        public const string GrabEvent = "grab";

        public const string SeatEvent = "seat";

        public const string RackEvent = "rack";

        public static string NameFor(bool empty) => empty ? EmptyName : TacticalName;

        public static bool Has(string stateName) => stateName == TacticalName || stateName == EmptyName;

        public static bool TryGet(string stateName, out bool empty)
        {
            empty = stateName == EmptyName;
            return Has(stateName);
        }

        public static string SegmentFor(bool empty) => empty ? EmptySegment : TacticalSegment;

        /// <summary>
        /// What an event marker means, without the number Blender's unique marker names add ("seat2" in the empty segment is a "seat").
        /// </summary>
        public static string EventKind(string marker)
        {
            if (string.IsNullOrEmpty(marker))
            {
                return marker;
            }
            int end = marker.Length;
            while (end > 0 && char.IsDigit(marker[end - 1]))
            {
                end--;
            }
            return marker.Substring(0, end);
        }
    }
}
