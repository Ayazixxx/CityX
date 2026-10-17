using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE CARRIED ONE-HANDED SWORD'S HAND (owner, 2026-10-17: "he is moving is arm very akwardly make it fluid motion no jumps too no weird
    /// bends"). The arm is the one-handed sword clips' own, swinging as they move it; the hand starts as that arm's natural hand (wrist
    /// straight, palm to the leg, thumb forward) and the wrist turns it only as far as <see cref="MaxWristDegrees"/> toward where the carry
    /// wants the blade -- so a swinging arm does not see-saw a rigid blade from pointing down to pointing straight ahead, and the wrist is
    /// never folded. The turn is the shortest one, it grows and shrinks continuously with the arm, and nothing is ever chosen between two
    /// options (the old tilt picked "down" or "up" each frame and could flip).
    /// </summary>
    public static class CarryHand
    {
        /// <summary>The most the wrist bends to keep the blade where the carry wants it, degrees: a wrist cocked to hold the blade, not folded over.</summary>
        public const float MaxWristDegrees = 50.0f;

        /// <summary>
        /// The right hand's rotation: <paramref name="natural"/> (the straight-wrist hand) turned toward pointing <paramref name="bladeInHand"/>
        /// (the sword's axis in the hand's own frame) along <paramref name="wantedBlade"/> (world), by at most <see cref="MaxWristDegrees"/>.
        /// </summary>
        public static Quaternion Turn(Quaternion natural, Vector3 bladeInHand, Vector3 wantedBlade)
        {
            Vector3 blade = natural * bladeInHand;
            if (blade.sqrMagnitude < 1e-8f || wantedBlade.sqrMagnitude < 1e-8f)
            {
                return natural;
            }
            Quaternion full = Quaternion.FromToRotation(blade, wantedBlade);
            float angle = Quaternion.Angle(Quaternion.identity, full);
            if (angle <= MaxWristDegrees)
            {
                return full * natural;
            }
            return Quaternion.Slerp(Quaternion.identity, full, MaxWristDegrees / angle) * natural;
        }

        /// <summary>The straight-wrist right hand for a forearm running along <paramref name="fingers"/>, the thumb toward <paramref name="forward"/>.</summary>
        public static Quaternion Natural(Vector3 fingers, Vector3 forward)
        {
            fingers.Normalize();
            Vector3 thumb = forward - fingers * Vector3.Dot(forward, fingers);
            if (thumb.sqrMagnitude < 1e-6f)
            {
                thumb = Vector3.Cross(fingers, Vector3.up);
            }
            thumb.Normalize();
            // The right hand bone: +X to the fingertips, +Z the thumb, Y = Z x X (the palm is -Y).
            return Quaternion.LookRotation(thumb, Vector3.Cross(thumb, fingers));
        }
    }
}
