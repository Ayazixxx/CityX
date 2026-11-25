using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>A heat press put down in the world (where it stands; its id is made from that).</summary>
    public struct HeatPressPlacedMessage : IBroadcast
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>A joining player asking for every press already down.</summary>
    public struct HeatPressStateRequest : IBroadcast
    {
    }

    /// <summary>A shirt going through a press: which press, which shirt, the design it had and the one it comes out with.</summary>
    public struct HeatPressPressingMessage : IBroadcast
    {
        public int PressId;
        public string ItemId;
        public string OldDesignId;
        public string NewDesignId;
    }

    /// <summary>
    /// HEAT PRESSES ON EVERY MACHINE (docs/HEAT_PRESS.md; owner, 2026-11-24: "visible across computers"): a press put down by one player stands on
    /// every machine, and one joining later finds it; a shirt pressed in one is shown going through it on every machine (the shirt on the
    /// drawer, the clamp, the steam, the print). The shirt itself comes back to its owner on their own machine (HeatPress.PressShirt); what
    /// others then see them wear is the equipment's own (NetworkedEquipmentSync, with the design's id). FishNet broadcasts on the city
    /// streamer, as the graffiti's are. In singleplayer nothing is started and this does nothing.
    ///
    /// THE SERVER CHECKS a press is put down near the one putting it and a pressing names a press that stands, a shirt the game has and
    /// design ids that are ids.
    /// </summary>
    public sealed class HeatPressNetwork : SessionRelay
    {
        /// <summary>The furthest from their own body a player puts a press down, metres.</summary>
        public const float MaxReachMetres = 15.0f;

        [Tooltip("What a press put down on another machine is made from (HeatPressBuilder's prefab).")]
        public GameObject PressPrefab;

        /// <summary>The relay this machine's presses go through (null outside a session).</summary>
        public static HeatPressNetwork Active { get; private set; }

        /// <summary>What the server keeps for a joiner: every press put down, in order.</summary>
        private readonly List<HeatPressPlacedMessage> _placed = new List<HeatPressPlacedMessage>();

        protected override void HearAsServer(ServerManager server, bool on)
        {
            Hear<HeatPressPlacedMessage>(server, on, OnPlacedAtServer);
            Hear<HeatPressStateRequest>(server, on, OnStateRequestAtServer);
            Hear<HeatPressPressingMessage>(server, on, OnPressingAtServer);
            if (!on)
            {
                _placed.Clear();
            }
        }

        protected override void HearAsClient(ClientManager client, bool on)
        {
            Hear<HeatPressPlacedMessage>(client, on, OnPlacedAtClient);
            Hear<HeatPressPressingMessage>(client, on, OnPressingAtClient);
            if (on)
            {
                Active = this;
            }
            else if (ReferenceEquals(Active, this))
            {
                Active = null;
            }
        }

        protected override void OnLetIn(ClientManager client)
        {
            client.Broadcast(new HeatPressStateRequest());
        }

        private void OnDisable()
        {
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }
        }

        /// <summary>A press put down on this machine: put down on every other one too (nothing outside a session).</summary>
        public static void TellPlaced(HeatPress press)
        {
            if (Active != null && Active.CanSend && press != null)
            {
                Transform root = PressRoot(press);
                Active.Session.ClientManager.Broadcast(new HeatPressPlacedMessage { Position = root.position, Rotation = root.rotation });
            }
        }

        /// <summary>A shirt going through a press on this machine: shown going through it on every other one.</summary>
        public static void TellPressing(HeatPress press, string itemId, string oldDesign, string newDesign)
        {
            if (Active != null && Active.CanSend && press != null)
            {
                Active.Session.ClientManager.Broadcast(new HeatPressPressingMessage
                {
                    PressId = press.Id, ItemId = itemId, OldDesignId = oldDesign ?? string.Empty, NewDesignId = newDesign ?? string.Empty,
                });
            }
        }

        /// <summary>The prefab's root (the table), which a press is put down by: the press itself stands on it.</summary>
        private static Transform PressRoot(HeatPress press) => press.transform.root;

        /// <summary>Whether a pressing message is one to show: the press stands, the shirt is a thing the game has, the designs are ids.</summary>
        public static bool Acceptable(HeatPressPressingMessage message) =>
            !string.IsNullOrEmpty(message.ItemId) && ItemRegistry.Has(message.ItemId)
            && (string.IsNullOrEmpty(message.OldDesignId) || DesignStore.IsValidId(message.OldDesignId))
            && DesignStore.IsValidId(message.NewDesignId);

        private void OnPlacedAtServer(NetworkConnection sender, HeatPressPlacedMessage message, Channel channel)
        {
            if (sender == null || !WireChecks.Finite(message.Position) || !Near(sender, message.Position))
            {
                return;
            }
            _placed.Add(message);
            Session.ServerManager.Broadcast(message);
        }

        private void OnStateRequestAtServer(NetworkConnection sender, HeatPressStateRequest request, Channel channel)
        {
            if (sender == null)
            {
                return;
            }
            foreach (HeatPressPlacedMessage placed in _placed)
            {
                Session.ServerManager.Broadcast(sender, placed);
            }
        }

        private void OnPressingAtServer(NetworkConnection sender, HeatPressPressingMessage message, Channel channel)
        {
            if (sender != null && Acceptable(message))
            {
                Session.ServerManager.Broadcast(message);
            }
        }

        private void OnPlacedAtClient(HeatPressPlacedMessage message, Channel channel)
        {
            Place(message.Position, message.Rotation);
        }

        /// <summary>A press standing where one was put down (none made when one stands there already: its putter's own, or one heard twice).</summary>
        public HeatPress Place(Vector3 position, Quaternion rotation)
        {
            if (PressPrefab == null)
            {
                return null;
            }
            GameObject made = Instantiate(PressPrefab, position, rotation);
            var press = made.GetComponentInChildren<HeatPress>();
            HeatPress standing = null;
            if (press == null || (HeatPress.All.TryGetValue(press.Id, out standing) && standing != press))
            {
                DestroyImmediate(made);   // one stands there already
                return standing;
            }
            return press;
        }

        private void OnPressingAtClient(HeatPressPressingMessage message, Channel channel)
        {
            if (!Acceptable(message) || !HeatPress.All.TryGetValue(message.PressId, out HeatPress press) || press.Pressing)
            {
                return;   // the presser's own machine is pressing it already
            }
            var inventory = LocalPlayerBody.Find<Inventory>();
            press.ShowPressing(message.ItemId, message.OldDesignId, message.NewDesignId, inventory != null ? inventory.PrefabForItem(message.ItemId) : null);
        }

        private static bool Near(NetworkConnection sender, Vector3 point)
        {
            foreach (NetworkObject owned in sender.Objects)
            {
                if (owned != null && Vector3.Distance(owned.transform.position, point) <= MaxReachMetres)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
