using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A right arm's pose as a body moves it, in degrees: the shoulder's swing and lift, the upper arm's roll, the elbow, the forearm's roll and
    /// the wrist. Every value is one joint a real arm has, so a pose built from them is one a real arm can make.
    /// </summary>
    [System.Serializable]
    public struct ArmAngles
    {
        /// <summary>The shoulder's swing: + takes the arm forward, - back.</summary>
        public float Flex;

        /// <summary>The shoulder's lift away from the side, from hanging straight down.</summary>
        public float Abduct;

        /// <summary>The upper arm's roll about its length: + turns its front outward.</summary>
        public float Twist;

        /// <summary>The elbow's bend: 0 straight.</summary>
        public float Elbow;

        /// <summary>The forearm's roll: + turns the thumb outward (supination), - inward.</summary>
        public float Roll;

        /// <summary>The wrist bent toward the little finger (+, ulnar deviation: tips a held sword's point down) or the thumb (-).</summary>
        public float Deviation;

        /// <summary>The wrist bent toward the palm (+) or the back of the hand (-).</summary>
        public float WristFlex;

        public ArmAngles(float flex, float abduct, float twist, float elbow, float roll, float deviation, float wristFlex)
        {
            Flex = flex;
            Abduct = abduct;
            Twist = twist;
            Elbow = elbow;
            Roll = roll;
            Deviation = deviation;
            WristFlex = wristFlex;
        }
    }

    /// <summary>
    /// THE ONE-HANDED SWORD CARRIED ON THE MOVE (owner, 2026-10-17: "the sword is like spinning around make you own sword running and walking
    /// animations and make them look good", then "research how games do one handed sword running and walking animations and make ours work").
    /// The sword arm of the sword1h_* clips: SwordCarryBaker bakes them from the game's own idle, walk, run and strafes (the Godot clips) with
    /// this arm in place of theirs, so the legs, hips, chest and free arm are the walk everyone already has and the sword arm is this.
    ///
    /// WHAT GAMES DO (researched 2026-10-17): the carry is a locomotion set of its own, apart from the combat stance; the sword arm hangs
    /// relaxed a little away from the body and swings only a little, OPPOSITE the leg on its own side; the sword is held off to the side so it
    /// clears the legs; the wrist stays nearly in line with the forearm (a hammer grip pointed down reads as a broken wrist); the arm trails a
    /// little to show the sword's weight; and the parts overlap -- the upper arm leads, the forearm follows a beat later, the hand last -- rather
    /// than swinging as one stiff piece.
    ///
    /// WHY THE BLADE SPUN BEFORE: the sword sits across the fist (out past the thumb), so whatever pitches the forearm pitches the blade by the
    /// same angle, and a run's forearm swinging through 80 degrees see-sawed it from the ground to straight ahead. Here the shoulder swings a
    /// fraction of the clip's own swing, and a beat later the elbow takes up a little of it and the wrist leans the point back against it,
    /// so the hand swings but the blade only bobs a couple of degrees.
    ///
    /// Pure: the angles, the swing and the arm's rotations from them; SwordCarryBaker poses the bake skeleton with them.
    /// </summary>
    public static class SwordCarryArm
    {
        /// <summary>
        /// Standing and walking: the arm hanging a little behind the side and out from it, the elbow soft, the thumb turned a little out, the
        /// wrist tipping the point forward and down. With a sword held out past the thumb the blade points forward, about 20 to 35 degrees
        /// down and 15 to 25 degrees out (clear of the legs); the hand hangs beside the thigh.
        /// </summary>
        public static readonly ArmAngles Walk = new ArmAngles(flex: -12.0f, abduct: 14.0f, twist: 0.0f, elbow: 8.0f, roll: 18.0f, deviation: 27.0f, wristFlex: 0.0f);

        /// <summary>The same arm at rest.</summary>
        public static readonly ArmAngles Idle = Walk;

        /// <summary>
        /// Running: the arm trails further back (the sword's weight) and further out, the elbow bends a little more and the thumb turns further
        /// out, so the blade angles wider of the knees, which come up high in a run.
        /// </summary>
        public static readonly ArmAngles Run = new ArmAngles(flex: -22.0f, abduct: 16.0f, twist: 0.0f, elbow: 16.0f, roll: 26.0f, deviation: 26.0f, wristFlex: 0.0f);

        /// <summary>How a gait's own arm swing is handed to the sword arm: shares of it at the shoulder, the elbow and the wrist, and how late each follows.</summary>
        public readonly struct SwingShares
        {
            /// <summary>Of the clip's swing, how much the shoulder makes (the rest of the arm hangs from it).</summary>
            public readonly float Shoulder;

            /// <summary>Of the clip's swing, how much more the elbow bends as the arm comes forward.</summary>
            public readonly float Elbow;

            /// <summary>Of the clip's swing, how far the wrist leans the point back down against it.</summary>
            public readonly float Wrist;

            /// <summary>How late the elbow follows the shoulder, as a share of the gait's cycle.</summary>
            public readonly float ElbowLag;

            /// <summary>How late the wrist follows the shoulder, as a share of the gait's cycle (the hand last: overlap).</summary>
            public readonly float WristLag;

            public SwingShares(float shoulder, float elbow, float wrist, float elbowLag, float wristLag)
            {
                Shoulder = shoulder;
                Elbow = elbow;
                Wrist = wrist;
                ElbowLag = elbowLag;
                WristLag = wristLag;
            }
        }

        /// <summary>At rest the arm keeps half the idle's own sway (breathing), so it is never a statue.</summary>
        public static readonly SwingShares IdleSwing = new SwingShares(shoulder: 0.5f, elbow: 0.0f, wrist: 0.3f, elbowLag: 0.0f, wristLag: 0.05f);

        /// <summary>Walking: under a third of the free arm's swing, the forearm and hand trailing it.</summary>
        public static readonly SwingShares WalkSwing = new SwingShares(shoulder: 0.3f, elbow: 0.15f, wrist: 0.35f, elbowLag: 0.06f, wristLag: 0.06f);

        /// <summary>Running: a fifth of the pumping arm's swing (the sword is carried, not pumped).</summary>
        public static readonly SwingShares RunSwing = new SwingShares(shoulder: 0.2f, elbow: 0.15f, wrist: 0.3f, elbowLag: 0.06f, wristLag: 0.06f);

        /// <summary>The wrist never bends further toward the little finger than this, degrees (a real wrist goes to about 35-40).</summary>
        public const float MaxDeviation = 36.0f;

        /// <summary>
        /// The arm in <paramref name="pose"/> swung by a gait: <paramref name="swing"/> is the gait's own arm swing now (degrees, + forward, about
        /// its middle), <paramref name="elbowSwing"/> and <paramref name="wristSwing"/> the same swing as it was <see cref="SwingShares.ElbowLag"/>
        /// and <see cref="SwingShares.WristLag"/> of a cycle ago.
        /// </summary>
        public static ArmAngles Swinging(in ArmAngles pose, in SwingShares shares, float swing, float elbowSwing, float wristSwing)
        {
            ArmAngles a = pose;
            a.Flex += shares.Shoulder * swing;
            a.Elbow = Mathf.Max(0.0f, a.Elbow + shares.Elbow * elbowSwing);
            a.Deviation = Mathf.Clamp(a.Deviation + shares.Wrist * wristSwing, 0.0f, MaxDeviation);
            return a;
        }

        /// <summary>The swinging arm's frame, before the shoulder turns it: hanging straight down (+X), its front forward (+Z), its back outward (+Y).</summary>
        private static readonly Quaternion Hanging = Quaternion.LookRotation(Vector3.forward, Vector3.right);

        /// <summary>
        /// The RIGHT arm's world rotations for <paramref name="a"/>, hung from a chest turned <paramref name="chest"/>. Each bone is the rig's own
        /// (BodyRig: built in a T-pose with every rotation the identity): its +X runs along it toward the hand, its +Z is its front (the thumb's
        /// side), its +Y the back of the hand. So the hand's +X runs to the fingertips, +Z is the thumb and -Y the palm.
        /// </summary>
        public static void Solve(in ArmAngles a, Quaternion chest, out Quaternion upperArm, out Quaternion forearm, out Quaternion hand)
        {
            // The shoulder: out from the side (about the chest's forward axis), then forward or back (about its sideways axis).
            Quaternion shoulder = Quaternion.AngleAxis(-a.Flex, Vector3.right) * Quaternion.AngleAxis(a.Abduct, Vector3.forward);
            // The upper arm's roll about its own length: + turns its front (+Z) outward (+Y).
            upperArm = chest * shoulder * Hanging * Quaternion.AngleAxis(-a.Twist, Vector3.right);
            // The elbow bends the forearm toward the arm's front; the forearm's roll turns the thumb (+Z) outward (+Y) for +.
            forearm = upperArm * Quaternion.AngleAxis(-a.Elbow, Vector3.up) * Quaternion.AngleAxis(-a.Roll, Vector3.right);
            // The wrist: toward the little finger tips the thumb's side (+Z) toward the fingertips (+X); toward the palm turns the fingers to -Y.
            hand = forearm * Quaternion.AngleAxis(a.Deviation, Vector3.up) * Quaternion.AngleAxis(-a.WristFlex, Vector3.forward);
        }

        /// <summary>
        /// A swing sampled from a looping series: <paramref name="values"/> at <paramref name="times"/> (ascending, one cycle of
        /// <paramref name="cycle"/> seconds), read at <paramref name="time"/> wrapped into the cycle, straight between samples.
        /// </summary>
        public static float SampleLooping(float[] times, float[] values, float cycle, float time)
        {
            int n = times.Length;
            if (n == 0)
            {
                return 0.0f;
            }
            if (n == 1 || cycle <= 0.0f)
            {
                return values[0];
            }
            float t = Mathf.Repeat(time - times[0], cycle) + times[0];
            for (int i = 0; i < n - 1; i++)
            {
                if (t <= times[i + 1])
                {
                    float u = Mathf.InverseLerp(times[i], times[i + 1], t);
                    return Mathf.Lerp(values[i], values[i + 1], u);
                }
            }
            // Past the last sample: across the wrap to the first.
            float span = times[0] + cycle - times[n - 1];
            float w = span > 1e-5f ? (t - times[n - 1]) / span : 0.0f;
            return Mathf.Lerp(values[n - 1], values[0], w);
        }
    }
}
