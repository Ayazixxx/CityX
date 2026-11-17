using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>The camera indoors (CameraRoom): what counts as a room, and the arm that snaps in and eases out.</summary>
    public sealed class CameraRoomTests
    {
        [Test]
        public void OnlyARoofWithWallsOnMostSidesIsARoom()
        {
            Assert.AreEqual(0.0f, CameraRoom.Enclosure(false, CameraRoom.WallProbes), "a street between buildings has no roof: not a room");
            Assert.AreEqual(0.0f, CameraRoom.Enclosure(true, CameraRoom.OpenWalls), "under a footbridge, open on most sides: not a room");
            Assert.AreEqual(1.0f, CameraRoom.Enclosure(true, CameraRoom.RoomWalls), "a roof and walls round: a room");
            Assert.AreEqual(1.0f, CameraRoom.Enclosure(true, CameraRoom.WallProbes));
            float corridorEnd = CameraRoom.Enclosure(true, (CameraRoom.OpenWalls + CameraRoom.RoomWalls) / 2);
            Assert.Greater(corridorEnd, 0.0f);
            Assert.Less(corridorEnd, 1.0f, "between open and closed, the framing is part way");
        }

        [Test]
        public void TheArmSnapsInAtOnceAndDriftsBackOut()
        {
            Assert.AreEqual(1.5f, CameraRoom.Arm(6.0f, 1.5f, 6.0f, 6.0f, 5.0f, 1.0f / 60.0f), "a wall in the way must pull the camera in the same frame");
            float next = CameraRoom.Arm(1.5f, 6.0f, 6.0f, 6.0f, 5.0f, 1.0f / 60.0f);
            Assert.AreEqual(1.5f + 5.0f / 60.0f, next, 1e-5f, "with the wall gone it drifts back, it does not jump");
            Assert.AreEqual(6.0f, CameraRoom.Arm(5.99f, 9.0f, 6.0f, 6.0f, 5.0f, 1.0f), "it never goes past where it wants to be");
            Assert.AreEqual(3.6f, CameraRoom.Arm(6.0f, 9.0f, 3.6f, 6.0f, 5.0f, 1.0f / 60.0f), "a shorter wanted arm (a room, an aim) is taken at once");
            Assert.AreEqual(20.0f, CameraRoom.Arm(6.0f, 99.0f, 20.0f, 6.0f, 5.0f, 1.0f / 60.0f), "nothing held it in: a longer wanted arm (a body growing) is followed as it is");
        }
    }
}
