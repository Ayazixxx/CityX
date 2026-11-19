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

        public static bool Has(string stateName) => TryGetStage(stateName, out _);


        public static string SegmentFor(bool empty) => empty ? EmptySegment : TacticalSegment;

        // ---- a tube magazine, loaded a shell at a time (docs/SHOTGUN.md) ----
        // Not one motion but pieces strung together by the gun, each announced by name as it starts: the hand to the shells, one shell in (as
        // many times as there are shells to load), the hand back to the gun, with the action worked first when the gun had been run dry. Each
        // piece ends where the next begins, so only the first eases in from the aim and only the last eases back out to it.

        public const string ShellsStartName = "gun_shells_start";

        public const string ShellName = "gun_shell";

        public const string ShellsEndName = "gun_shells_end";

        public const string ShellsEndEmptyName = "gun_shells_end_empty";

        public const string ShellsStartSegment = "start";

        public const string ShellSegment = "shell";

        public const string ShellsEndSegment = "end";

        public const string ShellsEndEmptySegment = "endempty";

        /// <summary>One piece of a reload as the body plays it: the motion's segment, and whether it eases in from the aim and back out to it.</summary>
        public readonly struct Stage
        {
            public readonly string Segment;
            public readonly bool Empty, EaseIn, EaseOut;

            public Stage(string segment, bool empty, bool easeIn, bool easeOut)
            {
                Segment = segment;
                Empty = empty;
                EaseIn = easeIn;
                EaseOut = easeOut;
            }
        }

        /// <summary>The magazine swap of a gun with one (<see cref="TacticalName"/>, <see cref="EmptyName"/>).</summary>
        public static Stage StageFor(bool empty) => new Stage(SegmentFor(empty), empty, true, true);

        public static bool TryGetStage(string stateName, out Stage stage)
        {
            switch (stateName)
            {
                case TacticalName: stage = StageFor(false); return true;
                case EmptyName: stage = StageFor(true); return true;
                case ShellsStartName: stage = new Stage(ShellsStartSegment, false, true, false); return true;
                case ShellName: stage = new Stage(ShellSegment, false, false, false); return true;
                case ShellsEndName: stage = new Stage(ShellsEndSegment, false, false, true); return true;
                case ShellsEndEmptyName: stage = new Stage(ShellsEndEmptySegment, true, false, true); return true;
                default: stage = default; return false;
            }
        }

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
