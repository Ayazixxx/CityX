using System.Collections.Generic;
using CityX.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// A body's skills: which it has learnt (<see cref="SkillBook"/>) and what is on its bar (<see cref="Hotbar"/>), with the points spent from
    /// PlayerVitals. The input reader asks it whether a skill's key was pressed (<see cref="Pressed"/>) and whether a movement skill is known
    /// (<see cref="Knows"/>); the bar on the HUD and the skill menu read and change it.
    ///
    /// A BODY WITH NO CHARACTER KNOWS WHAT THE GAME HAD BEFORE THE TREE (every skill but Super Jump, SkillBook.AsBeforeTheTree), with the old
    /// number keys on the bar: pressing play on the city scene in the editor, and every test, plays as before the skill tree. A character from
    /// the menu gets what it saved (<see cref="Load"/>, called by CharacterIdentity).
    ///
    /// Added at runtime (PlayerInputReader.Awake, CharacterIdentity), like PlayerWetness: nothing in it is tuned in the editor, and no prefab has
    /// to be rebuilt for it. Learning skills is the owner's own business: nothing here is sent to other players (what the skills DO is, by the
    /// abilities themselves).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSkills : MonoBehaviour
    {
        private static readonly Key[] SlotKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0,
        };

        private static PlayerSkills _local;

        private PlayerVitals _vitals;
        private MeleeAttacker _melee;

        public SkillBook Book { get; private set; } = SkillBook.AsBeforeTheTree();

        public Hotbar Bar { get; } = Hotbar.WithDefaults();

        /// <summary>Raised when anything changes: a skill learnt or forgotten, the bar, the points.</summary>
        public event System.Action Changed;

        /// <summary>
        /// The skills of the body this machine's player controls: the menu and the HUD's bar follow it. The first body to wake claims it; on a
        /// server the body this player owns takes it over (NetworkedPlayerIdentity), and a body owned by somebody else gives it up.
        /// </summary>
        public static PlayerSkills Local
        {
            get => _local != null ? _local : null;
            set => _local = value;
        }

        /// <summary>The body's skills, added the first time anything asks.</summary>
        public static PlayerSkills On(GameObject body)
        {
            if (body == null)
            {
                return null;
            }
            PlayerSkills skills = body.GetComponent<PlayerSkills>();
            return skills != null ? skills : body.AddComponent<PlayerSkills>();
        }

        private void Awake()
        {
            _melee = GetComponent<MeleeAttacker>();
            Bar.Changed += RaiseChanged;
            if (Local == null)
            {
                Local = this;
            }
        }

        private void OnDestroy()
        {
            if (_local == this)
            {
                _local = null;
            }
        }

        /// <summary>Unspent skill points (PlayerVitals keeps them with the level and the stat points).</summary>
        public int Points => Vitals != null ? Vitals.SkillPoints : 0;

        private PlayerVitals Vitals => _vitals != null ? _vitals : (_vitals = GetComponent<PlayerVitals>());

        /// <summary>Sets the unspent skill points (the console; never below zero).</summary>
        public void SetPoints(int points)
        {
            if (Vitals == null)
            {
                return;
            }
            Vitals.SkillPoints = Mathf.Max(0, points);
            RaiseChanged();
        }

        public bool Knows(string id) => Book.Knows(id);

        /// <summary>Why a star cannot be learnt now (None: it can).</summary>
        public SkillBook.Refusal WhyNot(string id) => Book.WhyNot(id, Points);

        /// <summary>Learns a skill, paying its point, and puts it in the first empty slot if it goes on the bar.</summary>
        public SkillBook.Refusal TryLearn(string id)
        {
            int points = Points;
            SkillBook.Refusal refusal = Book.TryLearn(id, ref points);
            if (refusal != SkillBook.Refusal.None)
            {
                return refusal;
            }
            if (Vitals != null)
            {
                Vitals.SkillPoints = points;
            }
            Bar.PlaceInFirstEmpty(id);
            RaiseChanged();
            return refusal;
        }

        /// <summary>Forgets every skill but vaulting and gives the points back (a respec: free for now, GAME_DESIGN.md).</summary>
        public int ResetAll()
        {
            int refunded = Book.ResetAll();
            if (Vitals != null)
            {
                Vitals.SkillPoints += refunded;
            }
            for (int slot = 0; slot < Hotbar.SlotCount; slot++)
            {
                if (Bar[slot] != null && !Book.Knows(Bar[slot]))
                {
                    Bar.Clear(slot);
                }
            }
            RaiseChanged();
            return refunded;
        }

        /// <summary>Learns everything there is, free (the console's /skills all).</summary>
        public void LearnEverything()
        {
            var ids = new List<string>(Book.Learnt);
            foreach (SkillNode node in SkillCatalog.RealSkills())
            {
                ids.Add(node.Id);
            }
            Book.SetLearnt(ids);
            RaiseChanged();
        }

        /// <summary>Puts a learnt skill that goes on the bar into a slot. False for anything else.</summary>
        public bool PutOnBar(int slot, string id)
        {
            if (!Book.Knows(id))
            {
                return false;
            }
            return Bar.Put(slot, id);
        }

        public bool ClearSlot(int slot) => Bar.Clear(slot);

        // --- the keys ---

        /// <summary>The key a slot is on: 1 to 9, then 0.</summary>
        public static Key KeyFor(int slot) => SlotKeys[Mathf.Clamp(slot, 0, Hotbar.SlotCount - 1)];

        /// <summary>The key of the slot holding this skill was pressed this frame (and the skill is learnt).</summary>
        public bool Pressed(string id, Keyboard keyboard)
        {
            int slot = SlotFor(id, keyboard);
            return slot >= 0 && keyboard[KeyFor(slot)].wasPressedThisFrame;
        }

        /// <summary>The key of the slot holding this skill is down (the sword spin charges while it is held).</summary>
        public bool Held(string id, Keyboard keyboard)
        {
            int slot = SlotFor(id, keyboard);
            return slot >= 0 && keyboard[KeyFor(slot)].isPressed;
        }

        private int SlotFor(string id, Keyboard keyboard)
        {
            if (keyboard == null || !Book.Knows(id))
            {
                return -1;
            }
            return Bar.SlotOf(id);
        }

        /// <summary>Learnt, and nothing stops it right now that the bar can show: the sword's techniques need a sword in hand.</summary>
        public bool CanUseNow(string id)
        {
            SkillNode node = SkillCatalog.Find(id);
            if (node == null || !Book.Knows(id))
            {
                return false;
            }
            if (node.NeedsSword)
            {
                if (_melee == null)
                {
                    _melee = GetComponent<MeleeAttacker>();
                }
                return _melee != null && _melee.EquippedWeapon != null && _melee.EquippedWeapon.MovesetId == ComboChain.Sword;
            }
            return true;
        }

        // --- the save ---

        /// <summary>
        /// Takes a character's skills from its save. A save from before the skill tree (SkillsVersion 0) keeps everything it could already use
        /// and the old number keys; a newer one has what it learnt and its own bar.
        /// </summary>
        public void Load(CharacterSave save)
        {
            if (save == null)
            {
                return;
            }
            if (save.SkillsVersion < CharacterSave.CurrentSkillsVersion)
            {
                Book = SkillBook.AsBeforeTheTree();
                Bar.Load(SkillCatalog.DefaultBar);
            }
            else
            {
                var book = new SkillBook();
                book.SetLearnt(save.LearnedSkills);
                Book = book;
                Bar.Load(save.Hotbar);
                for (int slot = 0; slot < Hotbar.SlotCount; slot++)
                {
                    if (Bar[slot] != null && !Book.Knows(Bar[slot]))
                    {
                        Bar.Clear(slot);
                    }
                }
            }
            RaiseChanged();
        }

        /// <summary>Writes what is learnt and the bar onto the save.</summary>
        public void Save(CharacterSave save)
        {
            if (save == null)
            {
                return;
            }
            save.SkillsVersion = CharacterSave.CurrentSkillsVersion;
            save.LearnedSkills = new List<string>(Book.Learnt);
            save.LearnedSkills.Sort(System.StringComparer.Ordinal);
            save.Hotbar = Bar.ToList();
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
