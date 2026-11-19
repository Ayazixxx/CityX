using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>What a skill is, which decides whether it can go on the bar.</summary>
    public enum SkillKind
    {
        /// <summary>Its own controls (Shift, Space), never on the bar (owner, 2026-10-14).</summary>
        Movement,
        /// <summary>A spell, cast from a slot on the bar.</summary>
        Spell,
        /// <summary>A weapon technique, from a slot on the bar, with that weapon in hand.</summary>
        Ability,
        /// <summary>A star nobody has designed yet: shown, never learnt.</summary>
        Placeholder,
        /// <summary>At work for as long as it is learnt, with nothing to press (Dual Pistols): never on the bar.</summary>
        Passive,
    }

    /// <summary>One constellation on the skill map.</summary>
    public sealed class SkillTree
    {
        public readonly string Id;
        public readonly string Name;

        /// <summary>False for a tree that is not in the game yet: drawn faded, nothing in it can be learnt.</summary>
        public readonly bool InGame;

        /// <summary>A hybrid tree (Steam) sits in the gap between its two parents and joins their tops.</summary>
        public readonly string[] Parents;

        /// <summary>How much of the row the tree takes, in map units (design pixels at zoom 1). A hybrid takes less: it sits in a gap.</summary>
        public readonly float Width;

        public SkillTree(string id, string name, bool inGame, float width, params string[] parents)
        {
            Id = id;
            Name = name;
            InGame = inGame;
            Width = width;
            Parents = parents ?? new string[0];
        }

        public bool IsHybrid => Parents.Length > 0;
    }

    /// <summary>One star: a skill, where it sits in its tree, and what has to be learnt first.</summary>
    public sealed class SkillNode
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string TreeId;
        public readonly SkillKind Kind;

        /// <summary>Learnt before this one can be. Same tree, except a hybrid's first star, which needs the tops of both parents.</summary>
        public readonly string[] Requires;

        /// <summary>Map units from the bottom of the tree's column: x across from its middle, y up from its base.</summary>
        public readonly Vector2 Position;

        public readonly string Description;

        /// <summary>Everyone has it from the start (vaulting: the tutorial teaches it, owner 2026-10-14).</summary>
        public readonly bool KnownFromStart;

        /// <summary>Only with a sword in hand (the sword's spin, wave, Octaslash and stab dash).</summary>
        public readonly bool NeedsSword;

        /// <summary>
        /// The highest rank skill points can raise it to once learnt (learning it is rank 1): 1 for a skill with no ranks,
        /// <see cref="EndlessRanks"/> for one that grows for ever (GAME_DESIGN.md: Infinite upgrades).
        /// </summary>
        public readonly int MaxRank;

        public const int EndlessRanks = int.MaxValue;

        public SkillNode(string id, string name, string treeId, SkillKind kind, Vector2 position, string description,
            string[] requires = null, bool knownFromStart = false, bool needsSword = false, int maxRank = 1)
        {
            Id = id;
            Name = name;
            TreeId = treeId;
            Kind = kind;
            Position = position;
            Description = description;
            Requires = requires ?? new string[0];
            KnownFromStart = knownFromStart;
            NeedsSword = needsSword;
            MaxRank = Mathf.Max(1, maxRank);
        }

        public bool IsPlaceholder => Kind == SkillKind.Placeholder;

        /// <summary>Spells and weapon techniques go on the bar; movement keeps its own controls (owner, 2026-10-14).</summary>
        public bool GoesOnBar => Kind == SkillKind.Spell || Kind == SkillKind.Ability;

        /// <summary>Skill points can raise it past rank 1.</summary>
        public bool HasRanks => MaxRank > 1;
    }

    /// <summary>
    /// THE SKILL TREES (owner, 2026-10-14: "implement all the skills we have so far including the movements like dash, climb, super jump ... with
    /// the ability to add them as hotbar spells"). One big map, the trees side by side like Skyrim's constellations (docs/SKILLS.md). Every skill
    /// the game has is a star here; the empty stars ("Not designed yet") hold the places later skills will take, and two trees that are not in the
    /// game yet (Hydromancer, and Steam, the hybrid of fire and water) show where trees found later and hybrids go.
    ///
    /// Freerunner is the owner's: vaulting at the root, given to everyone in the tutorial, then three paths, Dash, Climb (with pulling yourself up
    /// onto ledges, owner) and Recovery, with Super Jump after Recovery (owner, 2026-11-08: "recovery should be one of the first 3 options in
    /// replacement of superjump and superjump can go after it"). Wall Stick on the Climb path is my placement. Every other tree's shape is my
    /// layout.
    /// </summary>
    public static class SkillCatalog
    {
        public const string Vault = "vault";
        public const string Dash = "dash";
        public const string Climb = "climb";
        public const string SuperJump = "super-jump";
        public const string Recovery = "recovery";
        public const string WallStick = "wall-stick";
        public const string Fireball = "fireball";
        public const string IceShards = "ice-shards";
        public const string GlacierSpikes = "glacier-spikes";
        public const string PoisonBreath = "poison-breath";
        public const string AcidRain = "acid-rain";
        public const string SwordSpin = "sword-spin";
        public const string SwordWave = "sword-wave";
        public const string Octaslash = "octaslash";
        public const string StabDash = "stab-dash";
        public const string Bloodrage = "bloodrage";
        public const string SelfSmite = "self-smite";
        public const string Grow = "grow";
        public const string Shrink = "shrink";
        public const string ArcaneOrb = "arcane-orb";
        public const string ArcaneBarrier = "arcane-barrier";
        public const string HealingAura = "healing-aura";
        public const string FireEnhancement = "fire-enhancement";
        public const string DualPistols = "dual-pistols";

        /// <summary>Skill points a star costs. One each for now; what deeper stars cost is open.</summary>
        public const int LearnCost = 1;

        /// <summary>Map units between rows of stars, and across between a path and its neighbour.</summary>
        public const float RowSpacing = 105.0f;
        public const float PathSpacing = 115.0f;

        /// <summary>The width of an ordinary tree's column, and of a hybrid's narrower one.</summary>
        public const float TreeWidth = 400.0f;
        public const float HybridWidth = 270.0f;

        public const string NotDesignedYet = "Not designed yet.";

        /// <summary>The bar a character had before the skill tree existed: what the number keys always did, then Self Smite on 9 and Grow on 0 (2026-10-15).</summary>
        public static readonly string[] DefaultBar =
        {
            Fireball, SwordSpin, IceShards, PoisonBreath, AcidRain, Octaslash, SwordWave, Bloodrage, SelfSmite, Grow,
        };

        private static readonly SkillTree[] TreeList =
        {
            new SkillTree("freerunner", "Freerunner", true, TreeWidth),
            new SkillTree("sword", "Sword", true, TreeWidth),
            // The guns' tree (owner, 2026-11-19: "Call the guns skill tree "Gunslinger" for now but i think we can come up with a better name").
            new SkillTree("gunslinger", "Gunslinger", true, TreeWidth),
            new SkillTree("berserker", "Berserker", true, TreeWidth),
            new SkillTree("pyromancer", "Pyromancer", true, TreeWidth),
            new SkillTree("steam", "Steam", false, HybridWidth, "pyromancer", "hydromancer"),
            new SkillTree("hydromancer", "Hydromancer", false, TreeWidth),
            new SkillTree("cryomancer", "Cryomancer", true, TreeWidth),
            new SkillTree("plaguebringer", "Plaguebringer", true, TreeWidth),
            new SkillTree("electromancer", "Electromancer", true, TreeWidth),
            new SkillTree("arcanist", "Arcanist", true, TreeWidth),
            new SkillTree("wizard", "Wizard", true, TreeWidth),
            new SkillTree("cleric", "Cleric", true, TreeWidth),
        };

        private static readonly List<SkillNode> NodeList = BuildNodes();
        private static readonly Dictionary<string, SkillNode> ById = Index(NodeList);

        /// <summary>The trees in the order they stand along the row, left to right; the row wraps round.</summary>
        public static IReadOnlyList<SkillTree> Trees => TreeList;

        public static IReadOnlyList<SkillNode> Nodes => NodeList;

        public static SkillNode Find(string id) => id != null && ById.TryGetValue(id, out SkillNode node) ? node : null;

        public static SkillTree FindTree(string id)
        {
            foreach (SkillTree tree in TreeList)
            {
                if (tree.Id == id)
                {
                    return tree;
                }
            }
            return null;
        }

        public static IEnumerable<SkillNode> InTree(string treeId)
        {
            foreach (SkillNode node in NodeList)
            {
                if (node.TreeId == treeId)
                {
                    yield return node;
                }
            }
        }

        /// <summary>Every star that is a real skill (not a placeholder, in a tree that is in the game).</summary>
        public static IEnumerable<SkillNode> RealSkills()
        {
            foreach (SkillNode node in NodeList)
            {
                if (!node.IsPlaceholder && FindTree(node.TreeId).InGame)
                {
                    yield return node;
                }
            }
        }

        private static Vector2 At(float across, float row) => new Vector2(across * PathSpacing, row * RowSpacing);

        private static SkillNode Empty(string treeId, int index, Vector2 position, params string[] requires) =>
            new SkillNode($"{treeId}-{index}", "?", treeId, SkillKind.Placeholder, position, NotDesignedYet, requires);

        private static List<SkillNode> BuildNodes()
        {
            var nodes = new List<SkillNode>
            {
                // --- Freerunner (owner): vaulting at the root, then Dash, Climb and Recovery; Super Jump after Recovery.
                new SkillNode(Vault, "Vault", "freerunner", SkillKind.Movement, At(0, 0),
                    "Run at a low wall, a railing or a window with Shift + W and you go over it or through it. Everyone learns it in the tutorial.",
                    knownFromStart: true),
                new SkillNode(Dash, "Dash", "freerunner", SkillKind.Movement, At(-1, 1),
                    "Double-tap Shift: a quick burst the way you are moving, or backwards with nothing held.", new[] { Vault }),
                new SkillNode(Climb, "Climb", "freerunner", SkillKind.Movement, At(0, 1),
                    "Hold Space at a wall to grab it, or run into it with Shift + W. Climb any wall, pull yourself up onto ledges and climb in " +
                    "through windows too high to vault.", new[] { Vault }),
                new SkillNode(Recovery, "Recovery", "freerunner", SkillKind.Movement, At(1, 1),
                    "Knocked down or thrown, press Space to flip straight back onto your feet, even in mid-air, keeping your momentum.",
                    new[] { Vault }),
                new SkillNode(SuperJump, "Super Jump", "freerunner", SkillKind.Movement, At(1, 2),
                    "Hold Space to crouch and charge, let go to leap. A full charge takes a second and a half and goes many times higher.",
                    new[] { Recovery }),
                new SkillNode(WallStick, "Wall Stick", "freerunner", SkillKind.Movement, At(0, 2),
                    "Sprint and leap at a wall to stick to it by your feet, then run along it.", new[] { Climb }),
                Empty("freerunner", 1, At(-1, 2), Dash),
                Empty("freerunner", 2, At(-1, 3), "freerunner-1"),
                Empty("freerunner", 3, At(0, 3), WallStick),
                Empty("freerunner", 4, At(1, 3), SuperJump),

                // --- Sword.
                new SkillNode(SwordSpin, "Sword Spin", "sword", SkillKind.Ability, At(0, 0),
                    "With a sword: hold the key to charge, let go to spin. In the air it spins at once, climbing.", needsSword: true),
                new SkillNode(SwordWave, "Sword Wave", "sword", SkillKind.Ability, At(-0.8f, 1),
                    "With a sword: a swing that sends a crescent of light down the crosshair.", new[] { SwordSpin }, needsSword: true),
                new SkillNode(Octaslash, "Octaslash", "sword", SkillKind.Ability, At(0.8f, 1),
                    "With a sword: a long wind-up, then a dash of eight cuts where the camera looks. Click to send it early.",
                    new[] { SwordSpin }, needsSword: true),
                new SkillNode(StabDash, "Stab Dash", "sword", SkillKind.Ability, At(0, 1),
                    "With a sword: draw back, then lunge a dash's length where the camera looks with the blade thrust out. It hits hardest at once.",
                    new[] { SwordSpin }, needsSword: true),
                Empty("sword", 1, At(-0.8f, 2), SwordWave),
                Empty("sword", 2, At(0.8f, 2), Octaslash),
                Empty("sword", 3, At(0, 3), "sword-1", "sword-2"),

                // --- Gunslinger (owner, 2026-11-19): Dual Pistols at the root ("it will be a skill you have to unlock on the tree"). The empty
                // stars round it are my layout.
                new SkillNode(DualPistols, "Dual Pistols", "gunslinger", SkillKind.Passive, At(0, 0),
                    "Carry two pistols in your primary slot or your secondary slot, one in each hand. Left-click fires the right-hand gun, " +
                    "right-click the left-hand one; hold both to fire both. No aiming down the sights. R reloads both."),
                Empty("gunslinger", 1, At(-0.8f, 1), DualPistols),
                Empty("gunslinger", 2, At(0.8f, 1), DualPistols),
                Empty("gunslinger", 3, At(0, 2), "gunslinger-1", "gunslinger-2"),

                // --- Berserker.
                new SkillNode(Bloodrage, "Bloodrage", "berserker", SkillKind.Spell, At(0, 0),
                    "Seven and a half seconds of rage: faster, harder melee blows and a red aura."),
                Empty("berserker", 1, At(0, 1), Bloodrage),
                Empty("berserker", 2, At(-0.7f, 2), "berserker-1"),
                Empty("berserker", 3, At(0.7f, 2), "berserker-1"),

                // --- Pyromancer. Its top star joins Steam. The Fire Enhancement (owner, 2026-10-29: the first "enhance weapon" skill) beside
                // Fireball's branch.
                new SkillNode(Fireball, "Fireball", "pyromancer", SkillKind.Spell, At(0, 0),
                    "A ball of fire down the crosshair that bursts where it lands."),
                new SkillNode(FireEnhancement, "Fire Enhancement", "pyromancer", SkillKind.Spell, At(-0.8f, 1),
                    "Your weapon catches fire: your next swing, bullet or arrow burns what it hits and sets it alight. A sword burns along its " +
                    "blade while the fire waits. No effect on staffs.", new[] { Fireball }),
                Empty("pyromancer", 2, At(0.8f, 1), Fireball),
                Empty("pyromancer", 3, At(0, 2), FireEnhancement, "pyromancer-2"),
                Empty("pyromancer", 4, At(0, 3), "pyromancer-3"),

                // --- Steam: a hybrid, opened by mastering Pyromancer and Hydromancer. Not in the game yet.
                Empty("steam", 1, At(0, 3.6f), "pyromancer-4", "hydromancer-4"),

                // --- Hydromancer: not in the game yet.
                Empty("hydromancer", 0, At(0, 0)),
                Empty("hydromancer", 1, At(-0.8f, 1), "hydromancer-0"),
                Empty("hydromancer", 2, At(0.8f, 1), "hydromancer-0"),
                Empty("hydromancer", 3, At(0, 2), "hydromancer-1", "hydromancer-2"),
                Empty("hydromancer", 4, At(0, 3), "hydromancer-3"),

                // --- Cryomancer. Glacier Spikes (owner, 2026-11-12) on the first row's left.
                new SkillNode(IceShards, "Ice Shards", "cryomancer", SkillKind.Spell, At(0, 0),
                    "Four shards of ice, one after another, down the crosshair. They stick where they land."),
                new SkillNode(GlacierSpikes, "Glacier Spikes", "cryomancer", SkillKind.Spell, At(-0.8f, 1),
                    "Press the key to aim along the ground where you look, then click: a line of ice crystals tears out of the ground, hurting " +
                    "and chilling everyone along it. Press the key again or right-click to put it away.", new[] { IceShards }),
                Empty("cryomancer", 2, At(0.8f, 1), IceShards),
                Empty("cryomancer", 3, At(0, 2), GlacierSpikes, "cryomancer-2"),

                // --- Plaguebringer.
                new SkillNode(PoisonBreath, "Poison Breath", "plaguebringer", SkillKind.Spell, At(0, 0),
                    "Scream a cloud of poison gas at the crosshair. Whoever breathes it is poisoned."),
                new SkillNode(AcidRain, "Acid Rain", "plaguebringer", SkillKind.Spell, At(0, 1),
                    "Call a storm of acid down where the crosshair points. Anyone out in the open burns; roofs shelter.", new[] { PoisonBreath }),
                Empty("plaguebringer", 1, At(-0.8f, 2), AcidRain),
                Empty("plaguebringer", 2, At(0.8f, 2), AcidRain),
                Empty("plaguebringer", 3, At(0, 3), "plaguebringer-1", "plaguebringer-2"),

                // --- Electromancer (owner, 2026-10-15): Self Smite, Pikachu's Thunder, at the root.
                new SkillNode(SelfSmite, "Self Smite", "electromancer", SkillKind.Spell, At(0, 0),
                    "Call a giant bolt of lightning down onto yourself. It hurts whatever it passes on the way down, then bursts round you, " +
                    "throwing everyone close by. You stand still while it comes, and nothing can hurt you as it lands."),
                Empty("electromancer", 1, At(-0.8f, 1), SelfSmite),
                Empty("electromancer", 2, At(0.8f, 1), SelfSmite),
                Empty("electromancer", 3, At(0, 2), "electromancer-1", "electromancer-2"),

                // --- Arcanist (owner, 2026-10-27: "lets make some arcane spells"): Arcane Orb, Sephiroth's Shadow Flare, and Arcane Barrier,
                // Bartolomeo's barrier (2026-10-28), side by side at the root; both rank up for ever (an orb a rank up to three, then damage; a
                // tenth more health a rank).
                new SkillNode(ArcaneOrb, "Arcane Orb", "arcanist", SkillKind.Spell, At(-0.5f, 0),
                    "A ball of magenta aura down the crosshair. Whoever it hits is circled by a cursed orb that strikes four seconds later. " +
                    "Each rank adds an orb, up to three, and more damage.",
                    maxRank: SkillNode.EndlessRanks),
                new SkillNode(ArcaneBarrier, "Arcane Barrier", "arcanist", SkillKind.Spell, At(0.5f, 0),
                    "Raise a wall of magenta aura before you and hold the key to keep it up. You stand still behind it while it stops whatever " +
                    "comes at its face, until its health runs out and it fades away. Each rank gives it a tenth more health.",
                    maxRank: SkillNode.EndlessRanks),
                Empty("arcanist", 1, At(-0.8f, 1), ArcaneOrb),
                Empty("arcanist", 2, At(0.8f, 1), ArcaneBarrier),
                Empty("arcanist", 3, At(0, 2), "arcanist-1", "arcanist-2"),

                // --- Wizard (owner, 2026-10-15: "idek what skilltree that would fall under i was going to make one just called Wizard"): Grow
                // and Shrink, side by side at the root. 30 s each.
                new SkillNode(Grow, "Grow", "wizard", SkillKind.Spell, At(-0.5f, 0),
                    "Grow to ten times your size for 30 seconds: a giant, striding over the street, slow and heavy. Cast it again to come back " +
                    "down early. Under a roof you grow as far as there is room."),
                new SkillNode(Shrink, "Shrink", "wizard", SkillKind.Spell, At(0.5f, 0),
                    "Shrink to a tenth of your size for 30 seconds: small enough to run between people's feet. Cast it again to grow back early."),
                Empty("wizard", 1, At(-0.8f, 1), Grow),
                Empty("wizard", 2, At(0.8f, 1), Shrink),
                Empty("wizard", 3, At(0, 2), "wizard-1", "wizard-2"),

                // --- Cleric (owner, 2026-10-29: "add a healing spell that makes a circle on the floor around you that moves with you and heals
                // yourself and others that go in the circle slowly"): the Healing Aura at the root. The tree's name is mine.
                new SkillNode(HealingAura, "Healing Aura", "cleric", SkillKind.Spell, At(0, 0),
                    "A green circle on the ground round you, going where you go for 12 seconds: you and everyone standing in it are healed, " +
                    "slowly. 30 seconds from one cast to the next."),
                Empty("cleric", 1, At(-0.8f, 1), HealingAura),
                Empty("cleric", 2, At(0.8f, 1), HealingAura),
                Empty("cleric", 3, At(0, 2), "cleric-1", "cleric-2"),
            };
            return nodes;
        }

        private static Dictionary<string, SkillNode> Index(List<SkillNode> nodes)
        {
            var index = new Dictionary<string, SkillNode>();
            foreach (SkillNode node in nodes)
            {
                index[node.Id] = node;
            }
            return index;
        }
    }
}
