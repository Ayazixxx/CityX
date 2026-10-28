using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// HOW A WING'S BONES TURN (docs/WINGS.md). The wing is three bones out from the shoulder blade (the humerus, the forearm, the hand) with a
    /// feather hanging off each by its own bone. Each bone's X runs along it and its Y is the wing's upper face. A pose is a SHAPE: the
    /// direction each bone points and the way the wing's face looks, in the wing root's frame (X out from the spine, Y up, Z forward of the
    /// body), blended between the open wing (as built), the glide, the tuck and the fold, with the stroke turned on top about the body's
    /// forward axis. The bones' local rotations follow from the shape; the left wing's are the right's mirror (<see cref="Mirror"/>).
    /// <list type="bullet">
    /// <item>FOLDED (on the ground), the humerus stands up and back behind the shoulder, the forearm comes back down from the elbow and the
    /// hand on down past the hip, the face turned to the back, the feathers' fan closed.</item>
    /// <item>The FLAP turns the wing about the body's forward axis at the shoulder, the elbow and the wrist each a little behind the one
    /// before (the wing whips), and each feather twists against the stroke, more toward the tip (the vanes bend).</item>
    /// <item>A GLIDE sweeps the wing back and raises it a little; a TUCK (descending) draws it back further and half closes it.</item>
    /// </list>
    /// Pure: the beat's numbers in, rotations out.
    /// </summary>
    public static class WingPose
    {
        /// <summary>Where the three bones point and the wing's face looks, in the root's frame, for one state of the wing.</summary>
        public struct Shape
        {
            public Vector3 Humerus;
            public Vector3 Forearm;
            public Vector3 Hand;
            public Vector3 Face;

            public Shape(Vector3 humerus, Vector3 forearm, Vector3 hand, Vector3 face)
            {
                Humerus = humerus.normalized;
                Forearm = forearm.normalized;
                Hand = hand.normalized;
                Face = face.normalized;
            }
        }

        /// <summary>Open, as the wing is built: out from the shoulder, rising to the elbow, the hand swept back, the face up.</summary>
        public static readonly Shape Open = new Shape(Along(28.0f, 12.0f), Along(4.0f, 24.0f), Along(-6.0f, 38.0f), Vector3.up);

        /// <summary>Gliding: swept back and raised a little.</summary>
        public static readonly Shape Glide = new Shape(new Vector3(0.80f, 0.40f, -0.45f), new Vector3(0.75f, 0.05f, -0.66f), new Vector3(0.50f, -0.15f, -0.85f),
            new Vector3(0.0f, 1.0f, 0.1f));

        /// <summary>Tucked for a descent: drawn back and in, dipped.</summary>
        public static readonly Shape Tuck = new Shape(new Vector3(0.55f, 0.15f, -0.82f), new Vector3(0.35f, -0.10f, -0.93f), new Vector3(0.10f, -0.30f, -0.95f),
            new Vector3(0.0f, 1.0f, 0.0f));

        /// <summary>Folded on the back: the elbow up behind the shoulder, the forearm back down, the hand down past the hip, the face to the back.</summary>
        public static readonly Shape Folded = new Shape(new Vector3(0.22f, 0.90f, -0.37f), new Vector3(-0.02f, -0.80f, -0.60f), new Vector3(0.05f, -0.82f, -0.57f),
            new Vector3(0.35f, 0.0f, -0.94f));

        /// <summary>Folded, the feathers' fan closes to this share of its open spread; gliding and tucked, to these.</summary>
        public const float FoldSpreadShare = 0.12f;

        public const float GlideSpreadShare = 0.7f;

        public const float TuckSpreadShare = 0.55f;

        /// <summary>The wing's middle sits this far above level when open and beating (a bird's dihedral), degrees.</summary>
        public const float RestDihedral = 8.0f;

        /// <summary>How far behind the shoulder the elbow and the wrist follow the stroke, as a share of a cycle, and how much of it they add.</summary>
        public const float ElbowLag = 0.10f;

        public const float WristLag = 0.2f;

        public const float ElbowShare = 0.45f;

        public const float WristShare = 0.35f;

        /// <summary>A feather twists against the stroke this many degrees for each degree a second the stroke moves, more toward the wingtip; capped.</summary>
        public const float FeatherTwistPerDegreePerSecond = 0.05f;

        public const float FeatherTwistMaxDegrees = 18.0f;

        /// <summary>The three bones' local rotations (each relative to the one before; the humerus to the wing root).</summary>
        public struct Arm
        {
            public Quaternion Humerus;
            public Quaternion Forearm;
            public Quaternion Hand;
        }

        /// <summary>The wing's shape for a blend of the states, 0..1 each: the open wing swept into a glide, then tucked, then folded.</summary>
        public static Shape Blend(float fold, float glide, float tuck)
        {
            Shape shape = Open;
            shape = Mix(shape, Glide, glide);
            shape = Mix(shape, Tuck, tuck);
            shape = Mix(shape, Folded, fold);
            return shape;
        }

        /// <summary>
        /// The arm's pose from the beat: <paramref name="shoulderDeg"/>, <paramref name="elbowDeg"/> and <paramref name="wristDeg"/> are the
        /// stroke's angle at the three joints (positive up), <paramref name="fold"/>, <paramref name="glide"/> and <paramref name="tuck"/> 0..1.
        /// </summary>
        public static Arm ArmPose(float fold, float glide, float tuck, float shoulderDeg, float elbowDeg, float wristDeg)
        {
            float open = 1.0f - fold;
            Shape shape = Blend(fold, glide, tuck);
            float shoulder = (RestDihedral + shoulderDeg) * open;
            float elbow = shoulder + elbowDeg * open;
            float wrist = elbow + wristDeg * open;
            Quaternion humerus = Stroke(shoulder) * Frame(shape.Humerus, shape.Face);
            Quaternion forearm = Stroke(elbow) * Frame(shape.Forearm, shape.Face);
            Quaternion hand = Stroke(wrist) * Frame(shape.Hand, shape.Face);
            return new Arm
            {
                Humerus = humerus,
                Forearm = Quaternion.Inverse(humerus) * forearm,
                Hand = Quaternion.Inverse(forearm) * hand,
            };
        }

        /// <summary>The bones' local rotations as the wing is built (open, still): what the mesh is bound in.</summary>
        public static Arm Rest => ArmPose(0.0f, 0.0f, 0.0f, -RestDihedral, 0.0f, 0.0f);

        /// <summary>How open the feathers' fan is, 0..1 of its built spread.</summary>
        public static float SpreadShare(float fold, float glide, float tuck)
        {
            return Mathf.Lerp(1.0f, FoldSpreadShare, fold) * Mathf.Lerp(1.0f, GlideSpreadShare, glide) * Mathf.Lerp(1.0f, TuckSpreadShare, tuck);
        }

        /// <summary>
        /// A feather's rotation on its arm bone: fanned back by <paramref name="spreadDeg"/> (its built spread) times <paramref name="spreadShare"/>,
        /// and twisted about its own length against the stroke, more the further out along the wing (<paramref name="along"/> 0..1).
        /// </summary>
        public static Quaternion FeatherPose(float spreadDeg, float spreadShare, float along, float strokeVelocityDegPerSecond)
        {
            float twist = Mathf.Clamp(-strokeVelocityDegPerSecond * FeatherTwistPerDegreePerSecond * (0.4f + 0.6f * Mathf.Clamp01(along)),
                -FeatherTwistMaxDegrees, FeatherTwistMaxDegrees);
            return Quaternion.AngleAxis(spreadDeg * spreadShare, Vector3.up) * Quaternion.AngleAxis(twist, Vector3.right);
        }

        /// <summary>The same rotation on the other side of the body: the left wing's bones are the right's mirrored across the spine.</summary>
        public static Quaternion Mirror(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

        /// <summary>A bone's frame: its X along <paramref name="direction"/>, its Y as near <paramref name="face"/> as that allows.</summary>
        public static Quaternion Frame(Vector3 direction, Vector3 face)
        {
            return Quaternion.LookRotation(direction, face) * Quaternion.Inverse(Quaternion.LookRotation(Vector3.right, Vector3.up));
        }

        /// <summary>A bone's direction when open: out along X, tipped up and swept back by the two angles, degrees.</summary>
        private static Vector3 Along(float upDegrees, float backDegrees)
        {
            float up = upDegrees * Mathf.Deg2Rad, back = backDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(up) * Mathf.Cos(back), Mathf.Sin(up), -Mathf.Cos(up) * Mathf.Sin(back));
        }

        private static Shape Mix(in Shape a, in Shape b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Shape(Vector3.Slerp(a.Humerus, b.Humerus, t), Vector3.Slerp(a.Forearm, b.Forearm, t), Vector3.Slerp(a.Hand, b.Hand, t),
                Vector3.Slerp(a.Face, b.Face, t));
        }

        /// <summary>The stroke: a turn about the body's forward axis, degrees, positive lifting the wing.</summary>
        private static Quaternion Stroke(float degrees) => Quaternion.AngleAxis(degrees, Vector3.forward);
    }
}
