using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// Where everything sits on the skill map, in map units (design pixels at zoom 1): the trees stand side by side along one row in
    /// SkillCatalog's order, each as wide as its Width, and the row wraps round like Skyrim's sky, so scrolling past the last tree brings back the
    /// first. A position along the row is "scroll": the row's x at the middle of the view. The menu draws the row twice, side by side, and slides
    /// the two copies so that between them they always cover the view (<see cref="CopyOffsets"/>); the view is never wider than the row (the
    /// furthest zoom out, <see cref="ZoomToShowAll"/>, fits the row exactly). Plain class, tested without a scene.
    /// </summary>
    public sealed class SkillMapLayout
    {
        public readonly float RowWidth;

        private readonly Dictionary<string, float> _centres = new Dictionary<string, float>();
        private readonly List<string> _order = new List<string>();

        public SkillMapLayout() : this(SkillCatalog.Trees)
        {
        }

        public SkillMapLayout(IReadOnlyList<SkillTree> trees)
        {
            float x = 0.0f;
            foreach (SkillTree tree in trees)
            {
                _centres[tree.Id] = x + tree.Width * 0.5f;
                _order.Add(tree.Id);
                x += tree.Width;
            }
            RowWidth = Mathf.Max(x, 1.0f);
        }

        public IReadOnlyList<string> TreeOrder => _order;

        /// <summary>The middle of a tree's column along the row.</summary>
        public float TreeCentre(string treeId) => _centres.TryGetValue(treeId, out float centre) ? centre : 0.0f;

        /// <summary>A star on the row: across from its tree's middle, up from the base of the trees.</summary>
        public Vector2 StarPosition(SkillNode node) => new Vector2(TreeCentre(node.TreeId) + node.Position.x, node.Position.y);

        /// <summary>A position along the row brought into [0, RowWidth).</summary>
        public float Wrap(float x) => x - RowWidth * Mathf.Floor(x / RowWidth);

        /// <summary>
        /// Where the two copies of the row start, relative to the middle of the view, for a view centred on <paramref name="scroll"/>: the first
        /// puts the row's point <paramref name="scroll"/> in the middle; the second sits on whichever side the first leaves open.
        /// </summary>
        public void CopyOffsets(float scroll, out float first, out float second)
        {
            float s = Wrap(scroll);
            first = -s;
            second = s < RowWidth * 0.5f ? -s - RowWidth : -s + RowWidth;
        }

        /// <summary>How far a point of the row is from the middle of the view, the short way round (negative: to the left).</summary>
        public float Across(float scroll, float x)
        {
            float d = Wrap(x - scroll);
            return d > RowWidth * 0.5f ? d - RowWidth : d;
        }

        /// <summary>The tree whose middle is nearest the middle of the view.</summary>
        public string TreeAt(float scroll)
        {
            string best = _order.Count > 0 ? _order[0] : null;
            float bestDistance = float.MaxValue;
            foreach (string id in _order)
            {
                float distance = Mathf.Abs(Across(scroll, TreeCentre(id)));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = id;
                }
            }
            return best;
        }

        /// <summary>The scroll that centres the next tree along (+1) or the one before (-1), reached the short way round.</summary>
        public float ScrollToNeighbour(float scroll, int direction)
        {
            int count = _order.Count;
            if (count == 0)
            {
                return scroll;
            }
            int index = _order.IndexOf(TreeAt(scroll));
            float offCentre = Across(scroll, TreeCentre(_order[index]));
            // Already past the middle of this tree in the direction asked: its neighbour is the next one along, not this one.
            int step = direction > 0 ? 1 : -1;
            if (Mathf.Abs(offCentre) > 1.0f && Mathf.Sign(offCentre) == step)
            {
                step = 0;
            }
            string target = _order[((index + step) % count + count) % count];
            return scroll + Across(scroll, TreeCentre(target));
        }

        /// <summary>The furthest zoom out: the whole row fits across the view, exactly once.</summary>
        public float ZoomToShowAll(float viewWidth) => viewWidth / RowWidth;
    }
}
