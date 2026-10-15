using System.Collections.Generic;

namespace CityX.Character
{
    /// <summary>
    /// Which skills a character has learnt, and the rules for learning one: it has to be a real skill (not an empty star, not in a tree that is not
    /// in the game yet), not already learnt, everything it requires learnt first, and a skill point to pay for it. The points themselves live with
    /// the level and the stat points (PlayerVitals); this only says whether they would be enough. Plain class, tested without a scene.
    /// </summary>
    public sealed class SkillBook
    {
        public enum Refusal
        {
            None,
            /// <summary>No such skill.</summary>
            Unknown,
            /// <summary>An empty star, or a tree that is not in the game yet.</summary>
            NotInGame,
            AlreadyLearnt,
            /// <summary>Something below it is not learnt yet.</summary>
            NeedsPrerequisite,
            NoPoints,
        }

        private readonly HashSet<string> _learnt = new HashSet<string>();

        /// <summary>A brand new character's: only what everyone knows from the start (vaulting).</summary>
        public SkillBook()
        {
            AddKnownFromStart();
        }

        /// <summary>Every skill the game has (the console's /skills all).</summary>
        public static SkillBook Everything()
        {
            var book = new SkillBook();
            foreach (SkillNode node in SkillCatalog.RealSkills())
            {
                book._learnt.Add(node.Id);
            }
            return book;
        }

        /// <summary>
        /// What the game gave everyone before the skill tree (2026-10-14): every skill but Super Jump, whose charged jump had been taken out of
        /// the game until a skill could bring it back ("keep it in the vault"). A body with no character (the editor's play button, the tests)
        /// and every character made before the tree know this much, so Space still jumps the instant it is pressed for them.
        /// </summary>
        public static SkillBook AsBeforeTheTree()
        {
            SkillBook book = Everything();
            book._learnt.Remove(SkillCatalog.SuperJump);
            return book;
        }

        public bool Knows(string id) => id != null && _learnt.Contains(id);

        public IEnumerable<string> Learnt => _learnt;

        public int Count => _learnt.Count;

        public Refusal WhyNot(string id, int points)
        {
            SkillNode node = SkillCatalog.Find(id);
            if (node == null)
            {
                return Refusal.Unknown;
            }
            if (node.IsPlaceholder || !SkillCatalog.FindTree(node.TreeId).InGame)
            {
                return Refusal.NotInGame;
            }
            if (Knows(id))
            {
                return Refusal.AlreadyLearnt;
            }
            if (!PrerequisitesMet(node))
            {
                return Refusal.NeedsPrerequisite;
            }
            return points >= SkillCatalog.LearnCost ? Refusal.None : Refusal.NoPoints;
        }

        /// <summary>Everything below the star is learnt: it is ready to learn, points allowing (an empty star is never learnable, whatever this says).</summary>
        public bool PrerequisitesMet(SkillNode node)
        {
            foreach (string required in node.Requires)
            {
                if (!Knows(required))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Learns it if <see cref="WhyNot"/> allows, taking the cost from <paramref name="points"/>.</summary>
        public Refusal TryLearn(string id, ref int points)
        {
            Refusal refusal = WhyNot(id, points);
            if (refusal != Refusal.None)
            {
                return refusal;
            }
            _learnt.Add(id);
            points -= SkillCatalog.LearnCost;
            return Refusal.None;
        }

        /// <summary>Forgets everything but what everyone knows from the start, and says how many points that gives back.</summary>
        public int ResetAll()
        {
            int refunded = 0;
            foreach (string id in _learnt)
            {
                SkillNode node = SkillCatalog.Find(id);
                if (node != null && !node.KnownFromStart)
                {
                    refunded += SkillCatalog.LearnCost;
                }
            }
            _learnt.Clear();
            AddKnownFromStart();
            return refunded;
        }

        /// <summary>
        /// Replaces what is learnt with <paramref name="ids"/> (from a save). Anything the game no longer has, or that is an empty star, is dropped;
        /// what everyone knows from the start is always there. A skill whose prerequisites are missing is kept: it was paid for.
        /// </summary>
        public void SetLearnt(IEnumerable<string> ids)
        {
            _learnt.Clear();
            AddKnownFromStart();
            if (ids == null)
            {
                return;
            }
            foreach (string id in ids)
            {
                SkillNode node = SkillCatalog.Find(id);
                if (node != null && !node.IsPlaceholder && SkillCatalog.FindTree(node.TreeId).InGame)
                {
                    _learnt.Add(id);
                }
            }
        }

        private void AddKnownFromStart()
        {
            foreach (SkillNode node in SkillCatalog.Nodes)
            {
                if (node.KnownFromStart)
                {
                    _learnt.Add(node.Id);
                }
            }
        }
    }
}
