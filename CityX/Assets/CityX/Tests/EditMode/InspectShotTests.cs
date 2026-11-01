using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The inspect shot (docs/ITEM_INSPECT.md): the card stands beside and over the item facing the camera; the camera comes round to the
    /// card's side, close enough to read it, looking at the point between them; and the glide there is still at both ends and never a jump.
    /// </summary>
    public sealed class InspectShotTests
    {
        private static readonly Vector3 Item = new Vector3(10.0f, 1.0f, 20.0f);
        private static readonly Vector3 ViewedFrom = new Vector3(10.0f, 3.0f, 14.0f);   // looking along +z at the item

        [Test]
        public void TheCardStandsBesideAndOverTheItemFacingTheCamera()
        {
            InspectShot shot = InspectShot.For(Item, ViewedFrom);
            Assert.AreEqual(Item.y + InspectShot.CardHeight * 0.5f + InspectShot.CardLift, shot.CardPosition.y, 1e-4f, "the card's bottom should sit just over the item");
            Vector3 sideways = shot.CardPosition - Item;
            sideways.y = 0.0f;
            Assert.AreEqual(InspectShot.CardWidth * 0.5f + InspectShot.CardGap, sideways.magnitude, 1e-4f, "the card is not the gap away from the item");
            Assert.Greater(shot.CardPosition.x, Item.x, "the card stands on the viewer's right");
            Vector3 toCard = (shot.CardPosition - shot.CameraPosition).normalized;
            Assert.Greater(Vector3.Dot(shot.CardRotation * Vector3.forward, toCard), 0.999f, "the card does not face the camera");
        }

        [Test]
        public void TheCameraComesRoundToTheCardsSideAndLooksAtThePointBetween()
        {
            InspectShot shot = InspectShot.For(Item, ViewedFrom);
            Assert.AreEqual(InspectShot.Distance, Vector3.Distance(shot.CameraPosition, shot.Focus), 1e-3f);
            Vector3 toFocus = shot.Focus - shot.CameraPosition;
            Assert.Less(Vector3.Angle(shot.CameraRotation * Vector3.forward, toFocus), 0.5f, "the camera does not look at the focus");
            Assert.Greater(shot.CameraPosition.y, shot.Focus.y, "the camera should look down on the item");
            Assert.Greater(shot.CameraPosition.x, Item.x + 1.0f, "the camera should have swung round to the card's side");
            Assert.Less(shot.CameraPosition.z, Item.z, "the camera stays on the viewer's side of the item");
            float swing = Vector3.Angle(Vector3.forward, Vector3.ProjectOnPlane(shot.CameraRotation * Vector3.forward, Vector3.up));
            Assert.AreEqual(InspectShot.SwingDegrees, swing, 0.1f);
        }

        [Test]
        public void TheCardCanStandOnTheLeftOrOverTheItemWhenTheRightIsTaken()
        {
            InspectShot right = InspectShot.For(Item, ViewedFrom);
            InspectShot left = InspectShot.For(Item, ViewedFrom, InspectShot.Distance, CardSide.Left);
            InspectShot over = InspectShot.For(Item, ViewedFrom, InspectShot.Distance, CardSide.Over);
            Assert.AreEqual(CardSide.Right, right.Side);
            Assert.Less(left.CardPosition.x, Item.x, "the left card should stand on the viewer's left");
            Assert.AreEqual(Item.x - (right.CardPosition.x - Item.x), left.CardPosition.x, 1e-4f, "the left card mirrors the right one");
            Assert.Less(left.CameraPosition.x, Item.x - 1.0f, "the camera should swing to the left card's side");
            Assert.AreEqual(Item.x, over.CardPosition.x, 1e-4f, "the card over the item stands centred on it");
            Assert.AreEqual(Item.z, over.CardPosition.z, 1e-4f);
            Assert.Greater(over.CardPosition.y - InspectShot.CardHeight * 0.5f, Item.y + InspectShot.CardLift, "the card over the item clears it");
            foreach (InspectShot shot in new[] { right, left, over })
            {
                Vector3 toCard = (shot.CardPosition - shot.CameraPosition).normalized;
                Assert.Greater(Vector3.Dot(shot.CardRotation * Vector3.forward, toCard), 0.999f, $"the {shot.Side} card does not face the camera");
                Assert.Less(Vector3.Angle(shot.CameraRotation * Vector3.forward, shot.Focus - shot.CameraPosition), 0.5f);
            }
        }

        [Test]
        public void AWallBehindTheCameraPullsItInButNoCloserThanTheLeast()
        {
            InspectShot near = InspectShot.For(Item, ViewedFrom, 1.8f);
            Assert.AreEqual(1.8f, Vector3.Distance(near.CameraPosition, near.Focus), 1e-3f);
            InspectShot least = InspectShot.For(Item, ViewedFrom, 0.3f);
            Assert.AreEqual(InspectShot.LeastDistance, Vector3.Distance(least.CameraPosition, least.Focus), 1e-3f);
        }

        [Test]
        public void TheGlideIsStillAtBothEndsAndNeverJumps()
        {
            Assert.AreEqual(0.0f, InspectShot.Ease(0.0f), 1e-6f);
            Assert.AreEqual(1.0f, InspectShot.Ease(1.0f), 1e-6f);
            Assert.AreEqual(0.5f, InspectShot.Ease(0.5f), 1e-6f);
            Assert.AreEqual(0.0f, InspectShot.Ease(-0.5f), 1e-6f, "before the start it stays at the start");

            InspectShot shot = InspectShot.For(Item, ViewedFrom);
            Quaternion fromRotation = Quaternion.LookRotation(Item - ViewedFrom);
            InspectShot.Blend(ViewedFrom, fromRotation, shot.CameraPosition, shot.CameraRotation, 0.0f, out Vector3 start, out Quaternion startRotation);
            Assert.AreEqual(0.0f, Vector3.Distance(start, ViewedFrom), 1e-5f, "the glide must begin exactly where the camera was");
            Assert.Less(Quaternion.Angle(startRotation, fromRotation), 1e-3f);
            InspectShot.Blend(ViewedFrom, fromRotation, shot.CameraPosition, shot.CameraRotation, 1.0f, out Vector3 end, out _);
            Assert.AreEqual(0.0f, Vector3.Distance(end, shot.CameraPosition), 1e-5f);

            const int steps = 30;
            float total = Vector3.Distance(ViewedFrom, shot.CameraPosition), largest = 0.0f, first = 0.0f;
            Vector3 previous = ViewedFrom;
            for (int i = 1; i <= steps; i++)
            {
                InspectShot.Blend(ViewedFrom, fromRotation, shot.CameraPosition, shot.CameraRotation, i / (float)steps, out Vector3 at, out _);
                float step = Vector3.Distance(at, previous);
                if (i == 1) first = step;
                largest = Mathf.Max(largest, step);
                previous = at;
            }
            Assert.Less(first, 0.02f * total, "the first frame of the glide is a jump");
            Assert.Less(largest, 1.6f * total / steps, "a frame of the glide moves too far at once");
        }
    }
}
