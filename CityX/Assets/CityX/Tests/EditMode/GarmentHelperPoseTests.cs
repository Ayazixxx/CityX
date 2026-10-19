using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A garment's helper bones (games' skirt bones and half-turning shoulder bones): at rest they turn with their parent, they take the asked
    /// share of the child's swing, and a skirt panel follows a knee coming toward it and lets a leg going away from it go.
    /// </summary>
    public sealed class GarmentHelperPoseTests
    {
        private static readonly Quaternion ParentBind = Quaternion.Euler(0.0f, 30.0f, 0.0f);
        private static readonly Quaternion ChildBind = Quaternion.Euler(10.0f, 30.0f, 5.0f);

        [Test]
        public void AtTheBindPoseAHelperTurnsWithItsParent()
        {
            Quaternion parent = Quaternion.Euler(0.0f, 80.0f, 0.0f);
            Quaternion child = parent * Quaternion.Inverse(ParentBind) * ChildBind;
            Quaternion helper = GarmentHelperPose.Follow(parent, ParentBind, child, ChildBind, 0.7f);
            Assert.Less(Quaternion.Angle(helper, parent), 0.01f);
        }

        [Test]
        public void AHelperTakesItsShareOfTheChildsSwing()
        {
            Quaternion swing = Quaternion.AngleAxis(60.0f, Vector3.right);
            Quaternion child = ParentBind * swing * Quaternion.Inverse(ParentBind) * ChildBind;
            Assert.Less(Quaternion.Angle(GarmentHelperPose.Follow(ParentBind, ParentBind, child, ChildBind, 0.0f), ParentBind), 0.01f);
            Assert.AreEqual(30.0f, Quaternion.Angle(GarmentHelperPose.Follow(ParentBind, ParentBind, child, ChildBind, 0.5f), ParentBind), 0.05f);
            Assert.AreEqual(60.0f, Quaternion.Angle(GarmentHelperPose.Follow(ParentBind, ParentBind, child, ChildBind, 1.0f), ParentBind), 0.05f);
        }

        [Test]
        public void ASkirtPanelFollowsAKneeComingTowardItAndLetsALegGoingAwayGo()
        {
            Assert.AreEqual(GarmentHelperPose.LeadingShare, GarmentHelperPose.SkirtShare(0.6f, front: true), 0.001f);
            Assert.AreEqual(GarmentHelperPose.TrailingShare, GarmentHelperPose.SkirtShare(-0.6f, front: true), 0.001f);
            Assert.AreEqual(GarmentHelperPose.LeadingShare, GarmentHelperPose.SkirtShare(-0.6f, front: false), 0.001f);
            Assert.Less(GarmentHelperPose.SkirtShare(0.0f, front: true), GarmentHelperPose.SkirtShare(0.2f, front: true));
        }
    }
}
