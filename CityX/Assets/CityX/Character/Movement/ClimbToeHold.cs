using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A climber's foot ON ITS TOES (owner, 2026-10-30: "the bottom of the feet is pressed flat on the wall instead of it being like the tip of the
    /// toes", with a video of a Unity climbing system as the model): the foot points into the wall, turned out a little with the knee, its heel dropped a
    /// touch below level, and only the front of the toe touches the face. Pure: where the foot bone turns, and where the ankle must be for the toe to
    /// land on a hold. The foot's shape is the body's own (FusedBodyShape: a flat-soled stadium under the ankle, its round toe ahead of it).
    /// </summary>
    public static class ClimbToeHold
    {
        /// <summary>How far each foot turns out from straight into the wall, toward its own side, degrees (the knees go out, a frog's).</summary>
        public const float TurnOutDegrees = 22.0f;

        /// <summary>How far the heel hangs below level, degrees: the weight on the ball of the foot pushes it down.</summary>
        public const float HeelDropDegrees = 12.0f;

        /// <summary>
        /// The foot bone's rotation on its toes against a wall facing <paramref name="normal"/>. The foot bone's axes are the body's: +z the toes,
        /// +y the top of the foot.
        /// </summary>
        public static Quaternion Rotation(Vector3 normal, bool left)
        {
            Vector3 into = -normal;
            into.y = 0.0f;
            into = into.sqrMagnitude > 1e-6f ? into.normalized : Vector3.forward;
            Vector3 toes = Quaternion.AngleAxis(left ? -TurnOutDegrees : TurnOutDegrees, Vector3.up) * into;
            Vector3 right = Vector3.Cross(Vector3.up, toes).normalized;
            toes = Quaternion.AngleAxis(-HeelDropDegrees, right) * toes;
            return Quaternion.LookRotation(toes, Vector3.Cross(toes, right));
        }

        /// <summary>
        /// The point of the toe nearest the wall, from the ankle, for a foot turned <paramref name="rotation"/> (the world offset): the front of the
        /// round toe at the sole or at the top of the foot, whichever reaches further in.
        /// </summary>
        public static Vector3 ToeFromAnkle(Quaternion rotation, Vector3 normal, float scale)
        {
            // Into the wall, in the foot's own frame, flattened onto its sole: the toe's round front reaches furthest that way.
            Vector3 into = Quaternion.Inverse(rotation) * -normal;
            into.y = 0.0f;
            into = into.sqrMagnitude > 1e-6f ? into.normalized : Vector3.forward;
            float toeCentre = FusedBodyShape.FootToeCentreForward;
            float sole = -BodyRig.FootY;
            float top = sole + FusedBodyShape.FootHeight - FusedBodyShape.FootTopRounding;
            Vector3 atSole = rotation * ((new Vector3(0.0f, sole, toeCentre) + into * FusedBodyShape.FootToeRadius) * scale);
            Vector3 atTop = rotation * ((new Vector3(0.0f, top, toeCentre) + into * FusedBodyShape.FootToeRadius) * scale);
            return Vector3.Dot(atSole, -normal) >= Vector3.Dot(atTop, -normal) ? atSole : atTop;
        }

        /// <summary>Where the ankle goes so the toe of a foot turned <paramref name="rotation"/> lands on <paramref name="toe"/>.</summary>
        public static Vector3 AnkleFor(Vector3 toe, Quaternion rotation, Vector3 normal, float scale)
        {
            return toe - ToeFromAnkle(rotation, normal, scale);
        }
    }
}
