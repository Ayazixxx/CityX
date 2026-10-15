using System.Linq;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The skill map's row (owner, 2026-10-14: the trees side by side like Skyrim's, dragged and scrolled across): every tree in order along one
    /// row that wraps round, two copies of it always covering the view, and the furthest zoom out fitting the row exactly.
    /// </summary>
    public sealed class SkillMapLayoutTests
    {
        private readonly SkillMapLayout _layout = new SkillMapLayout();

        [Test]
        public void TheTreesStandSideBySideInTheirOrderWithoutOverlapping()
        {
            float x = 0.0f;
            foreach (SkillTree tree in SkillCatalog.Trees)
            {
                Assert.AreEqual(x + tree.Width * 0.5f, _layout.TreeCentre(tree.Id), 1e-3f, tree.Name);
                x += tree.Width;
                foreach (SkillNode node in SkillCatalog.InTree(tree.Id))
                {
                    float across = _layout.StarPosition(node).x - _layout.TreeCentre(tree.Id);
                    Assert.Less(Mathf.Abs(across), tree.Width * 0.5f, $"{node.Id} sticks out of {tree.Name}'s column");
                }
            }
            Assert.AreEqual(x, _layout.RowWidth, 1e-3f);
        }

        [Test]
        public void TwoCopiesOfTheRowAlwaysCoverTheView()
        {
            float half = _layout.RowWidth * 0.5f;
            for (float scroll = -2.0f * _layout.RowWidth; scroll < 3.0f * _layout.RowWidth; scroll += 37.0f)
            {
                _layout.CopyOffsets(scroll, out float first, out float second);
                float left = Mathf.Min(first, second);
                float right = Mathf.Max(first, second) + _layout.RowWidth;
                Assert.AreEqual(_layout.RowWidth, Mathf.Abs(first - second), 1e-2f, "the copies sit end to end");
                Assert.LessOrEqual(left, -half + 1e-2f, $"the left of the view is bare at scroll {scroll}");
                Assert.GreaterOrEqual(right, half - 1e-2f, $"the right of the view is bare at scroll {scroll}");
                Assert.AreEqual(0.0f, _layout.Wrap(scroll) + first, 1e-2f, "the first copy puts the scrolled-to point in the middle");
            }
        }

        [Test]
        public void TheRowWrapsRoundAndDistancesGoTheShortWay()
        {
            float w = _layout.RowWidth;
            Assert.AreEqual(10.0f, _layout.Wrap(w + 10.0f), 1e-3f);
            Assert.AreEqual(w - 10.0f, _layout.Wrap(-10.0f), 1e-3f);
            Assert.AreEqual(20.0f, _layout.Across(w - 10.0f, 10.0f), 1e-3f, "just past the end is just ahead");
            Assert.AreEqual(-20.0f, _layout.Across(10.0f, w - 10.0f), 1e-3f, "just before the start is just behind");
        }

        [Test]
        public void SteppingFromTreeToTreeGoesRoundTheWholeRowAndBack()
        {
            string[] order = SkillCatalog.Trees.Select(t => t.Id).ToArray();
            float scroll = _layout.TreeCentre(order[0]);
            for (int i = 1; i <= order.Length; i++)
            {
                scroll = _layout.ScrollToNeighbour(scroll, 1);
                Assert.AreEqual(order[i % order.Length], _layout.TreeAt(scroll), $"step {i}");
            }
            scroll = _layout.ScrollToNeighbour(scroll, -1);
            Assert.AreEqual(order[order.Length - 1], _layout.TreeAt(scroll), "back past the start comes to the last tree");

            // Halfway towards the next tree, the next step finishes the way to it rather than skipping it.
            float between = (_layout.TreeCentre(order[1]) + _layout.TreeCentre(order[2])) * 0.5f - 1.0f;
            Assert.AreEqual(order[2], _layout.TreeAt(_layout.ScrollToNeighbour(between, 1)));
        }

        [Test]
        public void ShowAllFitsTheWholeRowAcrossTheView()
        {
            Assert.AreEqual(1152.0f / _layout.RowWidth, _layout.ZoomToShowAll(1152.0f), 1e-6f);
            Assert.Less(_layout.ZoomToShowAll(1152.0f), 1.0f, "the row is wider than the screen at the start");
        }
    }
}
