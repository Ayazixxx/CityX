using CityX.Wearables;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// What a long open-fronted coat hides of the body under it, and how it lies on the ground: shared by the long coats' builders
    /// (MidnightCoatBuilder, FurHoodedParkaBuilder). docs/MIDNIGHT_COAT.md, "Hiding the body under the coat".
    /// </summary>
    internal static class LongCoatBody
    {
        // Tipped so it reads as a dropped coat.
        public const float GroundScale = 0.45f;
        public const float GroundPitchDegrees = -78.0f;
        public const float GroundLift = 0.15f;

        /// <summary>
        /// The body never drawn under the coat (owner, 2026-10-19: "make the torso and top half of arms up until the elbow invisible while
        /// wearing the midnight coat"): the torso and the upper arms, to the elbow; and the neck, but for what shows in the open front (owner,
        /// 2026-11-21: "when you crouch the neck pokes through ... its a clean chunk of neck that pokes through and can be easily made invisible").
        /// </summary>
        private static readonly HumanBodyBones[] AlwaysHidden =
        {
            HumanBodyBones.Spine, HumanBodyBones.Neck, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
        };

        /// <summary>
        /// Behind the body, also the hips and the top of the thighs, down to the buttocks' crease (bind pose, -z behind; owner: "make the amount
        /// invisible a little more on the back, go lower because some the back is clipping through when doing swinging sword animations").
        /// </summary>
        private static readonly HumanBodyBones[] AlwaysHiddenBehind =
        {
            HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
        };
        private static readonly Bounds Behind = new Bounds(new Vector3(0.0f, 1.05f, -0.3f), new Vector3(0.8f, 0.6f, 0.6f));

        /// <summary>
        /// The coat's open front (UnderCoat.FrontOpening, shared with a shirt under it): the torso there is drawn wherever it shows through it
        /// (owner: "make a little bit of the top of the torso visible, any amount that is in view regularly", "and ... toward the bottom").
        /// </summary>
        private static readonly Bounds FrontOpening = UnderCoat.FrontOpening;

        /// <summary>
        /// The body hidden only where the coat covers it (BodyUnderGarment): the forearms. Never the hips and legs: the skirt swings
        /// off them and the legs come out of its slit.
        /// </summary>
        private static readonly HumanBodyBones[] HiddenWhereCovered =
        {
            HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
        };

        /// <summary>The body's triangles the coat hides, one bit each, and the body's triangle count they were made for.</summary>
        public static byte[] Hidden(Mesh coat, out int bodyTriangles) =>
            BodyUnderGarment.HiddenTriangles(coat, AlwaysHidden, AlwaysHiddenBehind, Behind, HiddenWhereCovered, FrontOpening, out bodyTriangles);
    }
}
