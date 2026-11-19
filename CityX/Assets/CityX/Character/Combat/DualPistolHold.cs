using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SECOND PISTOL OF A PAIR, in the LEFT hand (docs/DUAL_PISTOLS.md): held as the right hand holds the first, seen in a mirror. The two
    /// hand bones are mirror images (HandGrip: +x runs to the fingertips of the right hand and back up the arm of the left, +y is the back of
    /// either hand, +z the thumb's side), so a point the right hand holds at (x, y, z) the left holds at (-x, y, z), and a gun's turn in the
    /// hand is mirrored the same way: its barrel and its top stay where the mirror puts them, and the gun itself is the same gun, not a
    /// mirror-image one (its ejection port stays on its right).
    /// </summary>
    public static class DualPistolHold
    {
        /// <summary>
        /// Where the right-hand gun of a raised pair goes (its origin, the web of its grip), from between the shoulders in the chest's frame;
        /// the left one mirrors it. Each before its own shoulder, the arms near straight: the akimbo stance.
        /// </summary>
        public static readonly Vector3 RightGunInChest = new Vector3(0.28f, -0.12f, 0.78f);

        /// <summary>A point held by the right hand, where the left hand holds it.</summary>
        public static Vector3 Mirror(Vector3 inRightHand) => new Vector3(-inRightHand.x, inRightHand.y, inRightHand.z);

        /// <summary>A turn in the right hand's frame, as the same hold in the left's (the reflection across the hand's x, both sides of it).</summary>
        public static Quaternion Mirror(Quaternion inRightHand) => new Quaternion(inRightHand.x, -inRightHand.y, -inRightHand.z, inRightHand.w);

        /// <summary>The name the left-hand gun's moving part goes by in a pair's reload motion (the right's keeps its own).</summary>
        public static string LeftHandPart(string partName) => partName + "L";
    }
}
