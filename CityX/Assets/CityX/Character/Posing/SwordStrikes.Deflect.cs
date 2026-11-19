using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A HELD GUARD KNOCKED TO A NEW ANGLE BY EACH BULLET IT TURNS ASIDE (docs/BULLET_DEFLECTION.md): the blade snaps to one of a handful of
    /// guard angles in a few frames and stays there until the next bullet; the guard raised again starts from its own pose. Every angle keeps
    /// the blade across the front of the head or the body, between it and the shooter: a blade held up or out to the side that still turned
    /// a bullet aside would read as a cheat (owner, 2026-11-19). Pure data: the angles, which one a bullet sends the blade to, the snap, and
    /// the names they travel to other players under.
    /// </summary>
    public static partial class SwordStrikes
    {
        /// <summary>How many angles a guard can be at: 0 is the guard's own (<see cref="Guard(bool)"/>), 1 and up the deflections'.</summary>
        public const int DeflectPoseCount = 7;

        /// <summary>How long the blade takes to get to its new angle, seconds: there at a third of it, a little past, settled by the end.</summary>
        public const float DeflectSnapSeconds = 0.1f;

        /// <summary>A shot from more than this far above (or below) level sends the blade to cover the head (or the legs), degrees.</summary>
        public const float DeflectHeightDegrees = 20.0f;

        private const string DeflectNamePrefix = "sword_deflect_";

        /// <summary>Built once, so a bullet turned aside builds no string.</summary>
        private static readonly string[] DeflectNames = BuildDeflectNames();

        private static string[] BuildDeflectNames()
        {
            var names = new string[DeflectPoseCount];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = DeflectNamePrefix + i;
            }
            return names;
        }

        /// <summary>The name a guard knocked to <paramref name="pose"/> travels to other players under.</summary>
        public static string DeflectName(int pose) => DeflectNames[Mathf.Clamp(pose, 0, DeflectPoseCount - 1)];

        /// <summary>True for a deflection's name (<see cref="DeflectName"/>), with the angle it knocked the guard to.</summary>
        public static bool TryGetDeflect(string animation, out int pose)
        {
            for (pose = 0; pose < DeflectPoseCount; pose++)
            {
                if (animation == DeflectNames[pose])
                {
                    return true;
                }
            }
            pose = 0;
            return false;
        }

        /// <summary>One angle a guard can be at: the pose, and what it covers most (1 the head, -1 the hips and legs, 0 the chest).</summary>
        private readonly struct DeflectAngle
        {
            public readonly Key Key;
            public readonly int Height;

            public DeflectAngle(Key key, int height)
            {
                Key = key;
                Height = height;
            }
        }

        private static DeflectAngle[] _deflects1H;
        private static DeflectAngle[] _deflects2H;

        /// <summary>
        /// Every angle at its index: 0 the guard's own, then the deflections'. Built on first use: the keys need <see cref="ArmMetres"/> and the
        /// guard's own keys, set up in another file of this class.
        /// </summary>
        private static DeflectAngle[] Deflects(bool oneHanded) =>
            oneHanded ? _deflects1H ?? (_deflects1H = BuildDeflects(true)) : _deflects2H ?? (_deflects2H = BuildDeflects(false));

        /// <summary>
        /// One hand, after the guard's own (from the right of the chest up across the face): level across the face, from the right shoulder
        /// down across the chest, level and a little down across the hips, upright before the face, rising from the right hip across the
        /// chest, and hung point down before the body from a hand by the head. The edge (the knuckles) faces the shooter.
        ///
        /// Two hands, after the guard's own (upright before the body): up and left across the chest and the face, up and right across them,
        /// level across the chest pointing right, from the left of the belly down to the right, across the belly down to the left, and level
        /// across the chin pointing right. The hands stay near the middle, so the blade crosses the body, and the long blade does the reaching:
        /// held out on the right or high and right, the left arm reaching across for the handle went through the chest or the face.
        /// Each edge faces the way that forearm runs into the hand, as the hammer grip has it (see <see cref="Guard(bool)"/>).
        /// </summary>
        private static DeflectAngle[] BuildDeflects(bool oneHanded)
        {
            Vector3 outward = Vector3.forward;
            if (oneHanded)
            {
                return new[]
                {
                    new DeflectAngle(GuardKey1H, 0),
                    new DeflectAngle(Pose(0.0f, Ease.Smooth, hand: new Vector3(0.40f, 2.05f, 0.55f), blade: new Vector3(-0.95f, 0.20f, 0.10f), edge: outward,
                        elbow: 0.45f, twist: 6.0f, hips: 3.0f, lean: 0.0f, tilt: -2.0f, drop: 0.08f, lead: -0.5f, depth: 0.35f), 1),
                    new DeflectAngle(Pose(0.0f, Ease.Smooth, hand: new Vector3(0.42f, 2.00f, 0.50f), blade: new Vector3(-0.70f, -0.62f, 0.30f), edge: outward,
                        elbow: 0.50f, twist: 10.0f, hips: 5.0f, lean: 4.0f, tilt: 0.0f, drop: 0.10f, lead: -0.5f, depth: 0.35f), 0),
                    new DeflectAngle(Pose(0.0f, Ease.Smooth, hand: new Vector3(0.40f, 1.40f, 0.52f), blade: new Vector3(-0.93f, -0.25f, 0.25f), edge: outward,
                        elbow: 0.35f, twist: 12.0f, hips: 6.0f, lean: 8.0f, tilt: 2.0f, drop: 0.14f, lead: -0.5f, depth: 0.40f), -1),
                    new DeflectAngle(Pose(0.0f, Ease.Smooth, hand: new Vector3(0.06f, 1.62f, 0.56f), blade: new Vector3(-0.02f, 0.99f, 0.10f), edge: outward,
                        elbow: 0.40f, twist: -4.0f, hips: -2.0f, lean: 3.0f, tilt: 0.0f, drop: 0.10f, lead: -0.5f, depth: 0.35f), 1),
                    new DeflectAngle(Pose(0.0f, Ease.Smooth, hand: new Vector3(0.42f, 1.40f, 0.52f), blade: new Vector3(-0.88f, 0.42f, 0.22f), edge: outward,
                        elbow: 0.35f, twist: 12.0f, hips: 6.0f, lean: 6.0f, tilt: 0.0f, drop: 0.12f, lead: -0.5f, depth: 0.40f), -1),
                    new DeflectAngle(Pose(0.0f, Ease.Smooth, hand: new Vector3(0.30f, 2.15f, 0.50f), blade: new Vector3(-0.25f, -0.93f, 0.27f), edge: outward,
                        elbow: 0.55f, twist: 4.0f, hips: 2.0f, lean: 2.0f, tilt: 0.0f, drop: 0.08f, lead: -0.5f, depth: 0.35f), 0),
                };
            }
            return new[]
            {
                new DeflectAngle(GuardKey2H, 0),
                new DeflectAngle(Forearmed(new Vector3(0.08f, 1.64f, 0.54f), new Vector3(-0.68f, 0.68f, 0.27f),
                    twist: -6.0f, hips: -3.0f, lean: 3.0f, drop: 0.10f), 1),
                new DeflectAngle(Forearmed(new Vector3(-0.10f, 1.58f, 0.52f), new Vector3(0.62f, 0.72f, 0.30f),
                    twist: 8.0f, hips: 4.0f, lean: 2.0f, drop: 0.10f), 1),
                new DeflectAngle(Forearmed(new Vector3(-0.20f, 1.62f, 0.55f), new Vector3(0.95f, 0.05f, 0.30f),
                    twist: -16.0f, hips: -6.0f, lean: 3.0f, drop: 0.10f), 0),
                new DeflectAngle(Forearmed(new Vector3(-0.18f, 1.50f, 0.52f), new Vector3(0.70f, -0.45f, 0.55f),
                    twist: -12.0f, hips: -5.0f, lean: 7.0f, drop: 0.14f), -1),
                new DeflectAngle(Forearmed(new Vector3(0.10f, 1.45f, 0.55f), new Vector3(-0.70f, -0.40f, 0.59f),
                    twist: 6.0f, hips: 3.0f, lean: 8.0f, drop: 0.14f), -1),
                new DeflectAngle(Forearmed(new Vector3(-0.24f, 1.92f, 0.62f), new Vector3(0.95f, 0.10f, 0.30f),
                    twist: -14.0f, hips: -5.0f, lean: 1.0f, drop: 0.08f), 1),
            };
        }

        /// <summary>A two-handed guard angle whose edge faces the way the sword arm reaches from its shoulder to the hand (the hammer grip's forearm).</summary>
        private static Key Forearmed(Vector3 hand, Vector3 blade, float twist, float hips, float lean, float drop)
        {
            return Pose(0.0f, Ease.Smooth, hand, blade, edge: hand - RightShoulder(twist), elbow: 0.35f, twist: twist, hips: hips, lean: lean,
                tilt: 0.0f, drop: drop, lead: -1.0f, depth: 0.40f);
        }

        /// <summary>The held guard at angle <paramref name="pose"/> (0, or anything out of range: its own).</summary>
        public static SwordFrame Guard(bool oneHanded, int pose)
        {
            if (pose <= 0 || pose >= DeflectPoseCount)
            {
                return Guard(oneHanded);
            }
            SwordFrame frame = Frame(Deflects(oneHanded)[pose].Key, 1.0f, !oneHanded);
            frame.ForearmAlign = 1.0f;
            if (oneHanded)
            {
                frame.FreeHandWeight = 0.0f;
            }
            return frame;
        }

        /// <summary>
        /// The angle a bullet coming from <paramref name="from"/> (the way to whoever fired, in the body's axes) knocks a guard at
        /// <paramref name="current"/> to: never the angle it is at; over the head for a shot from above, low for one from below (or across the
        /// chest, which covers both); which of those by <paramref name="roll01"/> (0 to 1).
        /// </summary>
        public static int PickDeflect(bool oneHanded, int current, Vector3 from, float roll01)
        {
            DeflectAngle[] angles = Deflects(oneHanded);
            int height = HeightOf(from);
            int count = CountFitting(angles, current, height);
            if (count == 0)
            {
                height = 0;
                count = CountFitting(angles, current, height);
            }
            int chosen = Mathf.Min((int)(Mathf.Clamp01(roll01) * count), count - 1);
            for (int pose = 0; pose < angles.Length; pose++)
            {
                if (Fits(angles[pose], pose, current, height) && chosen-- == 0)
                {
                    return pose;
                }
            }
            return current;
        }

        private static int CountFitting(DeflectAngle[] angles, int current, int height)
        {
            int count = 0;
            for (int pose = 0; pose < angles.Length; pose++)
            {
                if (Fits(angles[pose], pose, current, height))
                {
                    count++;
                }
            }
            return count;
        }

        private static bool Fits(in DeflectAngle angle, int pose, int current, int height) =>
            pose != current && (height == 0 || angle.Height == 0 || angle.Height == height);

        /// <summary>How high <paramref name="from"/> is: 1 above, -1 below, 0 near enough level.</summary>
        public static int HeightOf(Vector3 from)
        {
            float flat = Mathf.Sqrt(from.x * from.x + from.z * from.z);
            float degrees = Mathf.Atan2(from.y, flat) * Mathf.Rad2Deg;
            return degrees > DeflectHeightDegrees ? 1 : degrees < -DeflectHeightDegrees ? -1 : 0;
        }

        /// <summary>What angle <paramref name="pose"/> covers most (1 the head, -1 the hips and legs, 0 the chest).</summary>
        public static int DeflectHeight(bool oneHanded, int pose) => Deflects(oneHanded)[Mathf.Clamp(pose, 0, DeflectPoseCount - 1)].Height;

        /// <summary>
        /// <paramref name="t01"/> of the way through the snap from <paramref name="from"/> to <paramref name="to"/>: fast off the mark, there
        /// at a third of it, carried a little past by its own speed (the blade's and the hand's, the shoulders' turn) and settled back.
        /// </summary>
        public static SwordFrame DeflectSnap(in SwordFrame from, in SwordFrame to, float t01)
        {
            if (t01 >= 1.0f)
            {
                return to;
            }
            float u = Eased(Ease.Overshoot, t01);
            SwordFrame frame = Lerp(from, to, u);
            if (u > 1.0f)
            {
                float past = u - 1.0f;
                frame.Hand = to.Hand + (to.Hand - from.Hand) * past;
                frame.Blade = Vector3.SlerpUnclamped(from.Blade, to.Blade, u).normalized;
                Vector3 edge = to.Edge - frame.Blade * Vector3.Dot(to.Edge, frame.Blade);
                frame.Edge = edge.sqrMagnitude > 1e-6f ? edge.normalized : to.Edge;
                frame.TwistDegrees = to.TwistDegrees + (to.TwistDegrees - from.TwistDegrees) * past;
                frame.LeanDegrees = to.LeanDegrees + (to.LeanDegrees - from.LeanDegrees) * past;
                frame.TiltDegrees = to.TiltDegrees + (to.TiltDegrees - from.TiltDegrees) * past;
            }
            return frame;
        }
    }
}
