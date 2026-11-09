using System.Collections.Generic;
using UnityEngine;

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
            /// <summary>A rank asked of a skill not learnt yet.</summary>
            NotLearnt,
            /// <summary>At its highest rank already, or a skill with no ranks.</summary>
            HighestRank,
        }

        /// <summary>Skill points a rank past the first costs. One each for now, like learning; GAME_DESIGN.md's Infinite upgrades is the open curve.</summary>
        public const int RankCost = SkillCatalog.LearnCost;

        private readonly HashSet<string> _learnt = new HashSet<string>();

        /// <summary>The ranks bought past the first, by skill (a learnt skill missing here is at rank 1).</summary>
        private readonly Dictionary<string, int> _ranks = new Dictionary<string, int>();

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
        /// the game until a skill could bring it back ("keep it in the vault"), and Recovery, which came after the tree (2026-11-08) and changes
        /// what Space does to a body that is down. A body with no character (the editor's play button, the tests) and every character made
        /// before the tree know this much, so Space still jumps the instant it is pressed for them and stands a knocked-down body up as before.
        /// </summary>
        public static SkillBook AsBeforeTheTree()
        {
            SkillBook book = Everything();
            book._learnt.Remove(SkillCatalog.SuperJump);
            book._learnt.Remove(SkillCatalog.Recovery);
            return book;
        }

        public bool Knows(string id) => id != null && _learnt.Contains(id);

        public IEnumerable<string> Learnt => _learnt;

        public int Count => _learnt.Count;

        /// <summary>A skill's rank: 0 not learnt, 1 once learnt, one more for each rank bought.</summary>
        public int RankOf(string id) => !Knows(id) ? 0 : _ranks.TryGetValue(id, out int rank) ? rank : 1;

        /// <summary>The skills ranked past 1, with their ranks.</summary>
        public IEnumerable<KeyValuePair<string, int>> Ranks => _ranks;

        /// <summary>The skill points spent on ranks past the first.</summary>
        public int PointsInRanks
        {
            get
            {
                int points = 0;
                foreach (KeyValuePair<string, int> rank in _ranks)
                {
                    points += (rank.Value - 1) * RankCost;
                }
                return points;
            }
        }

        /// <summary>Why a learnt skill cannot go up a rank now (None: it can).</summary>
        public Refusal WhyNotRankUp(string id, int points)
        {
            SkillNode node = SkillCatalog.Find(id);
            if (node == null)
            {
                return Refusal.Unknown;
            }
            if (!Knows(id))
            {
                return Refusal.NotLearnt;
            }
            if (RankOf(id) >= node.MaxRank)
            {
                return Refusal.HighestRank;
            }
            return points >= RankCost ? Refusal.None : Refusal.NoPoints;
        }

        /// <summary>Raises a learnt skill a rank if <see cref="WhyNotRankUp"/> allows, taking the cost from <paramref name="points"/>.</summary>
        public Refusal TryRankUp(string id, ref int points)
        {
            Refusal refusal = WhyNotRankUp(id, points);
            if (refusal != Refusal.None)
            {
                return refusal;
            }
            _ranks[id] = RankOf(id) + 1;
            points -= RankCost;
            return Refusal.None;
        }

        /// <summary>
        /// Replaces the ranks with <paramref name="ranks"/> (from a save): each kept for a skill that is learnt and has ranks, no higher than its
        /// highest. Call after <see cref="SetLearnt"/>.
        /// </summary>
        public void SetRanks(IEnumerable<KeyValuePair<string, int>> ranks)
        {
            _ranks.Clear();
            if (ranks == null)
            {
                return;
            }
            foreach (KeyValuePair<string, int> rank in ranks)
            {
                SkillNode node = SkillCatalog.Find(rank.Key);
                if (node != null && Knows(rank.Key) && rank.Value > 1)
                {
                    _ranks[rank.Key] = Mathf.Min(rank.Value, node.MaxRank);
                }
            }
        }

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

        /// <summary>Forgets everything but what everyone knows from the start, ranks too, and says how many points that gives back.</summary>
        public int ResetAll()
        {
            int refunded = PointsInRanks;
            foreach (string id in _learnt)
            {
                SkillNode node = SkillCatalog.Find(id);
                if (node != null && !node.KnownFromStart)
                {
                    refunded += SkillCatalog.LearnCost;
                }
            }
            _learnt.Clear();
            _ranks.Clear();
            AddKnownFromStart();
            return refunded;
        }

        /// <summary>
        /// Replaces what is learnt with <paramref name="ids"/> (from a save). Anything the game no longer has, or that is an empty star, is dropped;
        /// what everyone knows from the start is always there. A skill whose prerequisites are missing is kept: it was paid for. A skill no longer
        /// learnt loses its rank.
        /// </summary>
        public void SetLearnt(IEnumerable<string> ids)
        {
            _learnt.Clear();
            AddKnownFromStart();
            if (ids != null)
            {
                foreach (string id in ids)
                {
                    SkillNode node = SkillCatalog.Find(id);
                    if (node != null && !node.IsPlaceholder && SkillCatalog.FindTree(node.TreeId).InGame)
                    {
                        _learnt.Add(id);
                    }
                }
            }
            var forgotten = new List<string>();
            foreach (string id in _ranks.Keys)
            {
                if (!_learnt.Contains(id))
                {
                    forgotten.Add(id);
                }
            }
            foreach (string id in forgotten)
            {
                _ranks.Remove(id);
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
