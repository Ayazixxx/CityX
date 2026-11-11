using System.Collections.Generic;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The arrow buttons' shapes (ArrowShape; owner, 2026-11-10: the creator's PROCEED and BACK arrows).</summary>
    public sealed class ArrowShapeTests
    {
        [Test]
        public void TheStraightArrowPointsRightWithAHeadWiderThanItsShaft()
        {
            List<Vector2[]> parts = ArrowShape.Straight();
            Assert.IsTrue(ArrowShape.Contains(parts, new Vector2(ArrowShape.StraightLength - 0.2f, 0.0f)), "no point to the right");
            Assert.IsTrue(ArrowShape.Contains(parts, new Vector2(0.5f, 0.0f)), "no tail on the left");
            Assert.IsTrue(ArrowShape.Contains(parts, new Vector2(ArrowShape.StraightLength - ArrowShape.StraightHeadLength + 0.1f, ArrowShape.StraightHeadHalfHeight - 0.2f)),
                "the head has no barbs");
            Assert.IsFalse(ArrowShape.Contains(parts, new Vector2(0.5f, ArrowShape.StraightShaftHalfHeight + 0.2f)), "the shaft is as wide as the head");
        }

        [Test]
        public void TheCurledArrowEndsInAHeadPointingDownAndBackToTheLeft()
        {
            List<Vector2[]> parts = ArrowShape.Curved();
            Vector2[] head = parts[parts.Count - 1];
            Vector2 tip = head[2], baseMiddle = 0.5f * (head[0] + head[1]);
            Vector2 way = (tip - baseMiddle).normalized;
            Assert.Less(way.x, -0.5f, "the head does not point back to the left");
            Assert.Less(way.y, 0.0f, "the head does not point down");
            Assert.IsTrue(ArrowShape.Contains(parts, ArrowShape.Polar(ArrowShape.CurveMiddle, 80.0f * Mathf.Deg2Rad)), "the band has a gap");
        }

        [Test]
        public void NoTwoPiecesOverlapSoTheGlossIsNeverDoubled()
        {
            foreach (List<Vector2[]> parts in new[] { ArrowShape.Straight(), ArrowShape.Curved() })
            {
                Rect box = ArrowShape.Bounds(parts, 0.0f);
                for (float x = box.xMin; x <= box.xMax; x += box.width / 60.0f)
                {
                    for (float y = box.yMin; y <= box.yMax; y += box.height / 30.0f)
                    {
                        int inside = 0;
                        foreach (Vector2[] part in parts)
                        {
                            inside += ArrowShape.Contains(new List<Vector2[]> { part }, new Vector2(x, y) + new Vector2(0.0013f, 0.0017f)) ? 1 : 0;
                        }
                        Assert.LessOrEqual(inside, 1, $"two pieces overlap at {x:0.00}, {y:0.00}");
                    }
                }
            }
        }

        [Test]
        public void GrowingAPieceKeepsItsCornersInsideAndRoundsThem()
        {
            Vector2[] square = { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            Vector2[] grown = ArrowShape.Grown(square, 0.2f);
            foreach (Vector2 corner in square)
            {
                Assert.IsTrue(ArrowShape.Contains(new List<Vector2[]> { grown }, corner), $"{corner} is not inside the outline");
            }
            Assert.IsFalse(ArrowShape.Contains(new List<Vector2[]> { grown }, new Vector2(1.18f, 1.18f)), "the corner is square, not round");
            Assert.IsTrue(ArrowShape.Contains(new List<Vector2[]> { grown }, new Vector2(1.18f, 0.5f)), "the side did not grow");
        }

        [Test]
        public void TheGlossIsCutAtTheMiddle()
        {
            Vector2[] square = { new Vector2(0, 0), new Vector2(0, 2), new Vector2(2, 2), new Vector2(2, 0) };
            Vector2[] upper = ArrowShape.Above(square, 1.0f);
            Assert.AreEqual(2.0f, Mathf.Abs(ArrowShape.SignedArea(upper)), 1e-4f);
            Assert.AreEqual(0, ArrowShape.Above(square, 3.0f).Length);
        }
    }
}
