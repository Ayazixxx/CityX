using System;
using System.Collections.Generic;

namespace CityX.World
{
    /// <summary>Tells the other machines in a session that a piece of Old Guy's house broke here (Character.TutorialHouseNetwork).</summary>
    public interface ITutorialHouseRelay
    {
        void Send(int piece);

        /// <summary>The house's power turned on here (TutorialHousePower), for every machine.</summary>
        void SendPowerOn();
    }

    /// <summary>
    /// What of Old Guy's house is broken (docs/OLD_GUYS_HOUSE.md): the front door's planks and the windows' panes, by the numbers the plan gives them
    /// (OldGuysHouse.FrontDoorPlanks, then OldGuysHouse.PaneCount), the same on every machine. A piece broken on this machine goes to the others
    /// through <see cref="Relay"/>; one broken on another comes in through <see cref="Told"/>. Either way the house's own piece hears
    /// <see cref="PieceBroken"/> and breaks, and a house built later (a player joining) finds it broken already. In singleplayer the relay is
    /// null and nothing leaves this machine.
    /// </summary>
    public sealed class TutorialHouseDamage
    {
        public static TutorialHouseDamage Shared { get; } = new TutorialHouseDamage();

        public ITutorialHouseRelay Relay;

        /// <summary>A piece broke, here or on another machine.</summary>
        public event Action<int> PieceBroken;

        private readonly HashSet<int> _broken = new HashSet<int>();

        public bool IsBroken(int piece) => _broken.Contains(piece);

        /// <summary>A piece the house has: what a message from another machine is held to.</summary>
        public static bool Acceptable(int piece) => piece >= 0 && piece < OldGuysHouse.BreakableCount;

        /// <summary>This machine broke <paramref name="piece"/>: kept, told to the others, and heard by the house.</summary>
        public void Break(int piece)
        {
            if (!Acceptable(piece) || !_broken.Add(piece))
            {
                return;
            }
            Relay?.Send(piece);
            PieceBroken?.Invoke(piece);
        }

        /// <summary>Another machine broke <paramref name="piece"/>.</summary>
        public void Told(int piece)
        {
            if (!Acceptable(piece) || !_broken.Add(piece))
            {
                return;
            }
            PieceBroken?.Invoke(piece);
        }

        /// <summary>Every broken piece, for a player joining.</summary>
        public List<int> All() => new List<int>(_broken);

        public void Clear() => _broken.Clear();
    }
}
