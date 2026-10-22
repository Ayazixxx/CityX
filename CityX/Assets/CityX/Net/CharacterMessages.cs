using FishNet.Broadcast;

namespace CityX.Net
{
    /// <summary>What a player's copy asks the server about their characters (<see cref="CharacterServer"/> answers).</summary>
    public struct CharacterRequest : IBroadcast
    {
        public const string List = "list";
        public const string Create = "create";
        public const string Delete = "delete";
        public const string Play = "play";
        public const string Save = "save";

        /// <summary>Matches the answer to the question.</summary>
        public int Ticket;

        public string Kind;

        /// <summary>Who is asking (<see cref="Core.PlayerKey"/>; Steam's id later).</summary>
        public string PlayerKey;

        /// <summary>A character id, or a character as JSON.</summary>
        public string Payload;
    }

    /// <summary>
    /// The server's answer. A list comes as one answer per character (<see cref="Item"/>) and then <see cref="Done"/>: a painted character is
    /// several kilobytes, so they are not sent all in one message.
    /// </summary>
    public struct CharacterReply : IBroadcast
    {
        public const string Item = "item";
        public const string Done = "done";

        public int Ticket;

        public string Kind;

        public bool Ok;

        /// <summary>For a character to play: whether the player is an admin.</summary>
        public bool IsAdmin;

        /// <summary>A character as JSON.</summary>
        public string Payload;
    }
}
