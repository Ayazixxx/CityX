using System.Collections.Generic;
using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A FALLEN BODY CAN BE SEARCHED (docs/LOOTING.md; owner, 2026-11-06: "i want enemies to be lootable like skyrim how you can grab the armor
    /// and weapons off of them"). When a body that is not a player's dies wearing something (a helmet, a hat, shoes, a shield), a loot window
    /// opens on it as on a drawer (LootContainer, kind Body, on the hips so it follows the ragdoll), listing what it wears; taking a piece takes
    /// it off the body. Its weapon already falls from its hand when it dies (docs/DROPPED_ITEMS.md).
    ///
    /// In a session the gear is everyone's to take but only once: the take is asked of the server (<see cref="Relay"/>, NetworkedCorpseLoot),
    /// which gives it to the first to ask and has every machine take it off its copy of the body.
    /// </summary>
    public sealed class CorpseLoot : MonoBehaviour
    {
        /// <summary>The searchable body, once fallen with something on it; null before.</summary>
        public LootContainer Container { get; private set; }

        /// <summary>Set by a networked body: takes go through the server. Null outside a session.</summary>
        public ICorpseLootRelay Relay { get; set; }

        private PlayerVitals _vitals;

        /// <summary>Pieces the server has said are taken (heard before this copy fell, they are never listed).</summary>
        private readonly HashSet<string> _gone = new HashSet<string>();

        /// <summary>Who on this machine asked the server for a piece: it goes into that inventory when the server says it is theirs.</summary>
        private readonly Dictionary<string, Inventory> _asked = new Dictionary<string, Inventory>();

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            if (_vitals != null)
            {
                _vitals.Died += OnDied;
            }
        }

        private void OnDestroy()
        {
            if (_vitals != null)
            {
                _vitals.Died -= OnDied;
            }
        }

        private void OnDied()
        {
            if (Container != null || PlayerBodies.IsPlayer(gameObject))
            {
                return;   // a player's own body is not loot (their gear is theirs: docs/GAME_DESIGN.md, death)
            }
            var items = new List<string>();
            foreach (IWornItem worn in GetComponentsInChildren<IWornItem>())
            {
                if (worn.IsWorn && !string.IsNullOrEmpty(worn.WornItemId) && !_gone.Contains(worn.WornItemId))
                {
                    items.Add(worn.WornItemId);
                }
            }
            if (items.Count == 0)
            {
                return;   // nothing on it: no prompt over every fallen body
            }
            Animator animator = GetComponentInChildren<Animator>();
            Transform hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            Container = (hips != null ? hips : transform).gameObject.AddComponent<LootContainer>();
            Container.Kind = ContainerKind.Body;
            Container.Label = NameOf(gameObject);
            Container.Items = items;
            Container.Taken += OnTaken;
        }

        /// <summary>What the loot window calls the body: its prefab's name, without Unity's "(Clone)" or the session's "Networked".</summary>
        public static string NameOf(GameObject body)
        {
            string name = body.name.Replace("(Clone)", "").Trim();
            return name.StartsWith("Networked") ? name.Substring("Networked".Length) : name;
        }

        /// <summary>Whether the body still wears <paramref name="itemId"/> here, fallen or not (the server's check: a dedicated server's copy is never told it fell).</summary>
        public bool Wears(string itemId)
        {
            if (_gone.Contains(itemId))
            {
                return false;
            }
            foreach (IWornItem worn in GetComponentsInChildren<IWornItem>())
            {
                if (worn.IsWorn && worn.WornItemId == itemId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether <paramref name="itemId"/> is still on the fallen body, to be taken.</summary>
        public bool Offers(string itemId) => Container != null && Container.Items.Contains(itemId);

        /// <summary>
        /// A player's ask for <paramref name="itemId"/> (LootSearcher): given at once outside a session (true: it leaves the list), or asked of the
        /// server in one (false: it stays listed until the server's answer takes it off, <see cref="Remove"/>).
        /// </summary>
        public bool Give(string itemId, Inventory looter)
        {
            if (!Offers(itemId) || looter == null || !looter.CanPickUp(itemId))
            {
                return false;
            }
            if (Relay != null && Relay.IsShared)
            {
                _asked[itemId] = looter;
                Relay.AskToTake(itemId);
                return false;
            }
            return looter.PickUp(itemId);
        }

        /// <summary>
        /// The server's word that <paramref name="itemId"/> is taken: off the list and off the body, here; into this machine's inventory that
        /// asked for it when it went to this machine's player (<paramref name="byThisMachine"/>).
        /// </summary>
        public void Taken(string itemId, bool byThisMachine)
        {
            if (!_gone.Add(itemId))
            {
                return;   // heard already (the whole list comes each time)
            }
            if (byThisMachine && _asked.TryGetValue(itemId, out Inventory into) && into != null)
            {
                into.PickUp(itemId);
            }
            _asked.Remove(itemId);
            if (Container != null)
            {
                Container.Items.Remove(itemId);
            }
            TakeOff(itemId);
        }

        /// <summary>A piece no longer on the body by any take (it fell off with the part it was on, Dismemberer): off the list, here.</summary>
        public void Lose(string itemId)
        {
            _gone.Add(itemId);
            if (Container != null)
            {
                Container.Items.Remove(itemId);
            }
        }

        private void OnTaken(string itemId)
        {
            TakeOff(itemId);
        }

        /// <summary>Takes the worn piece off the body (its model goes; a helmet uncovers the hair).</summary>
        private void TakeOff(string itemId)
        {
            foreach (IWornItem worn in GetComponentsInChildren<IWornItem>())
            {
                if (worn.IsWorn && worn.WornItemId == itemId && worn is IEquippableItem equippable)
                {
                    equippable.OnUnequip(gameObject);
                    return;
                }
            }
        }
    }

    /// <summary>Carries a take from a fallen body to the server, in a session (NetworkedCorpseLoot).</summary>
    public interface ICorpseLootRelay
    {
        bool IsShared { get; }

        void AskToTake(string itemId);
    }
}
