using UnityEngine;

namespace CityX.Character
{
    /// <summary>The sword attacks: five with a one-handed sword, four with a two-handed one.</summary>
    public enum SwordStrikeKind
    {
        /// <summary>One-handed: a horizontal slash, right to left, at chest height (the owner's choice for the first attack).</summary>
        Slash1H,

        /// <summary>One-handed: the backhand that follows, a diagonal cut from the upper left down to the right.</summary>
        Cut1H,

        /// <summary>One-handed: a thrust, the hilt drawn to the hip and driven straight out with a lunge onto the right foot.</summary>
        Thrust1H,

        /// <summary>One-handed heavy: an overhead cut from behind the head, down the middle.</summary>
        Overhead1H,

        /// <summary>Two-handed: the finisher, a wide diagonal from over the right shoulder down to the lower left, with a long step.</summary>
        Cut2H,

        /// <summary>Two-handed: the flat BACKHAND, left to right at chest height, the right foot passing forward (the second light).</summary>
        Reverse2H,

        /// <summary>Two-handed: the flat FOREHAND, right to left at chest height, the whole body turning through it (the first light).</summary>
        Sweep2H,

        /// <summary>Two-handed heavy: the overhead slam, a long hold at the top then everything into the floor.</summary>
        Slam2H,

        /// <summary>One-handed: the last of the three lights, a big diagonal from over the right shoulder down to the lower left, the whole body stepping through it.</summary>
        Finisher1H,
    }

    /// <summary>
    /// What the sword arm, the torso, the hips, the feet and the free hand are doing at one moment, in terms that do not depend on the rig:
    /// where the sword hand is relative to its shoulder (body axes, arm lengths), which way the blade points and which way its edge faces (body
    /// axes), how the shoulders and the hips are turned, the lean and tilt, how far the hips sink, the stance the feet stand in, and where the
    /// other hand is when it is not on the grip.
    /// </summary>
    public struct SwordFrame
    {
        /// <summary>Anything to apply at all.</summary>
        public bool Active;

        /// <summary>0 = the animation's own arm and torso, 1 = this pose.</summary>
        public float Weight;

        /// <summary>The sword (right) hand's offset from the right shoulder: x to the body's right, y up, z forward, in arm lengths (shoulder to wrist).</summary>
        public Vector3 Hand;

        /// <summary>The blade's direction, hilt to tip, in the body's axes (unit).</summary>
        public Vector3 Blade;

        /// <summary>The way the cutting edge faces, perpendicular to the blade, in the body's axes (unit). It leads every cut.</summary>
        public Vector3 Edge;

        /// <summary>How far the sword arm's elbow is pushed out to the side rather than hanging down (0 to 1).</summary>
        public float ElbowOut;

        /// <summary>The SHOULDERS' turn about the vertical, degrees, positive to the body's right (the hips' share included).</summary>
        public float TwistDegrees;

        /// <summary>The HIPS' turn about the vertical, degrees, positive to the right: they lead a cut and the shoulders follow.</summary>
        public float HipTwistDegrees;

        /// <summary>The torso's lean forward, degrees (negative leans back).</summary>
        public float LeanDegrees;

        /// <summary>The torso's sideways tilt, degrees, positive dropping the right shoulder.</summary>
        public float TiltDegrees;

        /// <summary>How far the hips sink toward the ground, metres: the legs bend to keep the feet planted.</summary>
        public float HipDrop;

        /// <summary>Which foot leads the stance: -1 the left foot forward, 1 the right, 0 square (see <see cref="SwordFootwork"/>).</summary>
        public float StanceLead;

        /// <summary>How far apart the feet are front to back, in leg lengths (hip to ankle): a lunge opens it.</summary>
        public float StanceDepth;

        /// <summary>0 to 1: how much the feet stand in the stance and step (SwordFootwork) rather than following the walk clip.</summary>
        public float Footwork;

        /// <summary>Which feet may set off on a new step at this moment.</summary>
        public FootSteps Steps;

        /// <summary>True when the free hand is on the grip too (the support hand follows the sword); false and it is placed by <see cref="FreeHand"/>.</summary>
        public bool TwoHanded;

        /// <summary>The free (left) hand's offset from the left shoulder, same units as <see cref="Hand"/> (x toward the body's middle); only with a one-handed sword.</summary>
        public Vector3 FreeHand;

        public float FreeElbowOut;

        /// <summary>0 to 1: how much the free hand is placed at all (0 leaves it to the walk clip: the carry).</summary>
        public float FreeHandWeight;

        /// <summary>0 to 1: how far through the cut it is (1 at the contact), for effects.</summary>
        public float Reach;

        /// <summary>0 to 1: how much the elbow is placed so the forearm runs straight into the hand (no bent wrist) rather than where the key bows it. The held stances (carry, ready, guard) use it; the cuts keep their authored elbows.</summary>
        public float ForearmAlign;
    }

    /// <summary>
    /// PROCEDURAL SWORD ATTACKS. Each attack is a handful of keyframes of the SWORD (where the hand is, which way the blade points, which way the
    /// edge faces) and the BODY (shoulders, hips, lean, the stance of the feet), joined the way a good sword animation is built:
    ///
    ///  * a WIND-UP that eases into a short HOLD at the top (the anticipation);
    ///  * a STRIKE that accelerates all the way into the contact, so the blade is at its fastest the moment it lands, along an ARC;
    ///  * a FOLLOW-THROUGH that carries the blade on at the same speed and decelerates (no dead stop at the contact: the strike and the
    ///    follow-through sweep the blade at the same average rate, so its speed is continuous where they meet), then a slower RECOVERY;
    ///  * the HIPS LEAD and the shoulders follow (the kinetic chain: hips wind up and unwind first), the body leans into the cut and sinks;
    ///  * the FEET take a stance and STEP into the cut (the stance opens, SwordFootwork steps the feet to it), the swordsman's advancing step;
    ///  * the EDGE LEADS: a cut's blade turns within one PLANE (<see cref="CutPlane"/>), and the edge is the plane's tangent, so the cutting side
    ///    (the spike sword's spikes) is always the side that arrives.
    ///
    /// (Owner, 2026-10-16: "my character holds and swings the sword awkwardly ... make the character is not turned in any awkward way and make
    /// sure his feet dont lock up during combat animations while moving", "i like the two handed horizontal slashes of this linked game", and
    /// "use the spikesword as guide ... the side with the spikes" should be hitting the enemy.) The two-handed light chain is two flat slashes,
    /// forehand then backhand, then the diagonal finisher.
    ///
    /// The keys are written as POINTS in the body's own standing frame (metres: x right, y up from the ground, z forward), turned into offsets
    /// from the turned shoulder here, so a key reads as "the hands at the right of the chest" rather than as arm-length arithmetic.
    ///
    /// Pure data and maths (no scene): the paths can be tested, and PlayerLocomotion only has to put the hand where a frame says, point the
    /// blade the way it says (two-bone IK on the arm, then the wrist), and turn the hips and spine.
    /// </summary>
    public static class SwordStrikes
    {
        public const string Slash1HName = "sword1h_slash";
        public const string Cut1HName = "sword1h_cut";
        public const string Thrust1HName = "sword1h_thrust";
        public const string Overhead1HName = "sword1h_overhead";
        public const string Finisher1HName = "sword1h_finisher";
        public const string Cut2HName = "sword2h_cut";
        public const string Reverse2HName = "sword2h_reverse";
        public const string Sweep2HName = "sword2h_sweep";
        public const string Slam2HName = "sword2h_slam";

        /// <summary>The held guard poses, played as states so another player's copy raises the same guard.</summary>
        public const string Guard1HName = "sword1h_guard";

        public const string Guard2HName = "sword2h_guard";

        /// <summary>The parry flourish: a held guard snapped forward for a moment when it catches a blow in its first instant.</summary>
        public const string ParryName = "sword_parry";

        /// <summary>
        /// THE REBOUND (owner, 2026-10-17: "implement a stagger system for if you swing at a block"): a blade that struck a guard is thrown
        /// back off it -- the sword arm knocked up and back past the shoulder, the shoulders turned open and the body leaning away -- held a
        /// moment wide open, then gathered back into the ready stance. Played on the attacker for as long as SoulsSwordCombat stuns it, and
        /// announced by name like a swing, so other players see whose blow bounced.
        /// </summary>
        public const string Rebound1HName = "sword1h_rebound";

        public const string Rebound2HName = "sword2h_rebound";

        /// <summary>How long the rebound takes, seconds: thrown back, held open, gathered back.</summary>
        public const float ReboundSeconds = 0.6f;

        public static string ReboundName(bool oneHanded) => oneHanded ? Rebound1HName : Rebound2HName;

        /// <summary>True for a rebound state name, with which hold it is.</summary>
        public static bool TryGetRebound(string animation, out bool oneHanded)
        {
            oneHanded = animation == Rebound1HName;
            return animation == Rebound1HName || animation == Rebound2HName;
        }

        public static readonly SwordStrikeKind[] All =
        {
            SwordStrikeKind.Slash1H, SwordStrikeKind.Cut1H, SwordStrikeKind.Thrust1H, SwordStrikeKind.Overhead1H,
            SwordStrikeKind.Cut2H, SwordStrikeKind.Reverse2H, SwordStrikeKind.Sweep2H, SwordStrikeKind.Slam2H, SwordStrikeKind.Finisher1H,
        };

        public static bool TryGet(string animation, out SwordStrikeKind kind)
        {
            switch (animation)
            {
                case Slash1HName: kind = SwordStrikeKind.Slash1H; return true;
                case Cut1HName: kind = SwordStrikeKind.Cut1H; return true;
                case Thrust1HName: kind = SwordStrikeKind.Thrust1H; return true;
                case Overhead1HName: kind = SwordStrikeKind.Overhead1H; return true;
                case Finisher1HName: kind = SwordStrikeKind.Finisher1H; return true;
                case Cut2HName: kind = SwordStrikeKind.Cut2H; return true;
                case Reverse2HName: kind = SwordStrikeKind.Reverse2H; return true;
                case Sweep2HName: kind = SwordStrikeKind.Sweep2H; return true;
                case Slam2HName: kind = SwordStrikeKind.Slam2H; return true;
                default: kind = SwordStrikeKind.Slash1H; return false;
            }
        }

        /// <summary>True for a guard state name, with which hold it is.</summary>
        public static bool TryGetGuard(string animation, out bool oneHanded)
        {
            oneHanded = animation == Guard1HName;
            return animation == Guard1HName || animation == Guard2HName;
        }

        /// <summary>A strike, a guard or the parry: anything this plays instead of the Animator.</summary>
        public static bool Has(string animation) => TryGet(animation, out _) || TryGetGuard(animation, out _) || animation == ParryName
            || TryGetRebound(animation, out _);

        public static string NameOf(SwordStrikeKind kind)
        {
            switch (kind)
            {
                case SwordStrikeKind.Slash1H: return Slash1HName;
                case SwordStrikeKind.Cut1H: return Cut1HName;
                case SwordStrikeKind.Thrust1H: return Thrust1HName;
                case SwordStrikeKind.Overhead1H: return Overhead1HName;
                case SwordStrikeKind.Finisher1H: return Finisher1HName;
                case SwordStrikeKind.Cut2H: return Cut2HName;
                case SwordStrikeKind.Reverse2H: return Reverse2HName;
                case SwordStrikeKind.Sweep2H: return Sweep2HName;
                default: return Slam2HName;
            }
        }

        public static string GuardName(bool oneHanded) => oneHanded ? Guard1HName : Guard2HName;

        public static bool IsOneHanded(SwordStrikeKind kind)
        {
            return kind == SwordStrikeKind.Slash1H || kind == SwordStrikeKind.Cut1H
                || kind == SwordStrikeKind.Thrust1H || kind == SwordStrikeKind.Overhead1H || kind == SwordStrikeKind.Finisher1H;
        }

        public static bool IsHeavy(SwordStrikeKind kind) => kind == SwordStrikeKind.Overhead1H || kind == SwordStrikeKind.Slam2H;

        /// <summary>The swing is heard as a heavy one: the heavies, and a chain's finisher.</summary>
        public static bool SoundsHeavy(SwordStrikeKind kind) => IsHeavy(kind) || kind == SwordStrikeKind.Finisher1H;

        /// <summary>
        /// THE PHASES OF AN ATTACK, in seconds (owner, 2026-10-23: "attacks should have clear anticipation -> acceleration -> impact -> recovery.
        /// Light attacks feel snappy; heavy ones feel powerful. Avoid floaty or weightless swings"): the WIND-UP (the anticipation), a HOLD at
        /// the top (a light's is a few frames, so the click is answered at once; a heavy's a real pause, the telegraph), the STRIKE (accelerating
        /// all the way into the contact), the FOLLOW-THROUGH (the blade carrying on past the target, losing its speed), the SETTLE (the
        /// follow-through's pose held a moment: the weight of the blade arriving, as an animator holds the frame after a blow) and the RECOVERY
        /// to the ready stance (a heavy's overshoots it and settles back, like a weight coming to rest). The keys' times come from these, so an
        /// attack is tuned here in seconds, and the game's own windows (SoulsAttack.FromStrike) follow.
        /// </summary>
        public readonly struct StrikeTiming
        {
            public readonly float WindUp, Hold, Strike, Follow, Settle, Recover;

            public StrikeTiming(float windUp, float hold, float strike, float follow, float settle, float recover)
            {
                WindUp = windUp;
                Hold = hold;
                Strike = strike;
                Follow = follow;
                Settle = settle;
                Recover = recover;
            }

            /// <summary>Seconds from the start to the contact: how long a click takes to land.</summary>
            public float Contact => WindUp + Hold + Strike;

            public float Total => Contact + Follow + Settle + Recover;
        }

        // The follow-through's seconds are its degrees over the strike's rate (see BuildKeys), so the blade carries its speed through the
        // contact. A light lands about 0.3 s after the click (the research's "respond within a few frames, spend the frames on the follow-through
        // instead of the wind-up"); a heavy pauses at the top for a fifth of a second or more and lands in 0.65-0.85 s.
        public static StrikeTiming TimingOf(SwordStrikeKind kind)
        {
            switch (kind)
            {
                case SwordStrikeKind.Slash1H: return new StrikeTiming(0.13f, 0.04f, 0.11f, 0.10f, 0.05f, 0.19f);
                case SwordStrikeKind.Cut1H: return new StrikeTiming(0.14f, 0.04f, 0.11f, 0.085f, 0.05f, 0.195f);
                case SwordStrikeKind.Thrust1H: return new StrikeTiming(0.12f, 0.04f, 0.10f, 0.06f, 0.04f, 0.20f);
                case SwordStrikeKind.Overhead1H: return new StrikeTiming(0.30f, 0.22f, 0.13f, 0.12f, 0.14f, 0.30f);
                // The chain's end: a bigger wind-up the target can see coming, the blow's pose held, and a long recovery in which the fighter is open.
                case SwordStrikeKind.Finisher1H: return new StrikeTiming(0.24f, 0.08f, 0.13f, 0.095f, 0.10f, 0.55f);
                case SwordStrikeKind.Cut2H: return new StrikeTiming(0.20f, 0.07f, 0.15f, 0.115f, 0.07f, 0.25f);
                case SwordStrikeKind.Reverse2H: return new StrikeTiming(0.15f, 0.05f, 0.13f, 0.12f, 0.05f, 0.20f);
                case SwordStrikeKind.Sweep2H: return new StrikeTiming(0.16f, 0.05f, 0.13f, 0.12f, 0.05f, 0.21f);
                default: return new StrikeTiming(0.38f, 0.30f, 0.16f, 0.13f, 0.18f, 0.35f);
            }
        }

        /// <summary>The blade is live from this far into the strike (0-1) -- once it is moving in earnest -- to this far into the follow-through.</summary>
        public const float LiveFromStrikeShare = 0.25f;

        public const float LiveToFollowShare = 0.7f;

        /// <summary>How long the whole attack takes, seconds: wind-up, hold, strike, follow-through, settle and recovery.</summary>
        public static float Seconds(SwordStrikeKind kind) => TimingOf(kind).Total;

        /// <summary>Where in the attack (0-1) the strike leaves the hold: the blade starts to move in earnest, and the swing is heard.</summary>
        public static float StrikeStart(SwordStrikeKind kind)
        {
            StrikeTiming t = TimingOf(kind);
            return (t.WindUp + t.Hold) / t.Total;
        }

        /// <summary>Where in the attack (0-1) the blade becomes dangerous: a quarter of the way into the strike.</summary>
        public static float ActiveStart(SwordStrikeKind kind)
        {
            StrikeTiming t = TimingOf(kind);
            return (t.WindUp + t.Hold + t.Strike * LiveFromStrikeShare) / t.Total;
        }

        /// <summary>Where in the attack (0-1) the blade stops being dangerous: into the follow-through, before it has slowed.</summary>
        public static float ActiveEnd(SwordStrikeKind kind)
        {
            StrikeTiming t = TimingOf(kind);
            return (t.Contact + t.Follow * LiveToFollowShare) / t.Total;
        }

        /// <summary>The moment of contact (0-1): the end of the strike, where the blade is fastest and the reach is 1.</summary>
        public static float ContactFraction(SwordStrikeKind kind)
        {
            StrikeTiming t = TimingOf(kind);
            return t.Contact / t.Total;
        }

        // ---- the body the keys are written against -------------------------------------------------------------------------------------

        /// <summary>Shoulder to wrist, metres: the unit of the hand offsets.</summary>
        public static readonly float ArmMetres = BodyRig.WristT * BodyRig.ArmLength;

        /// <summary>The right shoulder in the body's standing frame with the shoulders turned <paramref name="twistDegrees"/> (to the right): the turn swings it back.</summary>
        public static Vector3 RightShoulder(float twistDegrees)
        {
            float r = twistDegrees * Mathf.Deg2Rad;
            return new Vector3(BodyRig.ShoulderX * Mathf.Cos(r), BodyRig.ShoulderY, -BodyRig.ShoulderX * Mathf.Sin(r));
        }

        /// <summary>The left shoulder likewise: a turn to the right brings it forward.</summary>
        public static Vector3 LeftShoulder(float twistDegrees)
        {
            float r = twistDegrees * Mathf.Deg2Rad;
            return new Vector3(-BodyRig.ShoulderX * Mathf.Cos(r), BodyRig.ShoulderY, BodyRig.ShoulderX * Mathf.Sin(r));
        }

        /// <summary>A frame's wrist as a point in the body's standing frame (metres), the inverse of how the keys are written.</summary>
        public static Vector3 HandPoint(in SwordFrame frame) => RightShoulder(frame.TwistDegrees) + frame.Hand * ArmMetres;

        /// <summary>
        /// The plane a cut's blade turns in: at angle 0 it points along <see cref="Forward"/>, and as the angle grows it turns toward
        /// <see cref="Across"/>. The edge is the plane's tangent, the way the blade is moving, so it leads by construction.
        /// </summary>
        public readonly struct CutPlane
        {
            public readonly Vector3 Forward;
            public readonly Vector3 Across;

            public CutPlane(Vector3 forward, Vector3 across)
            {
                Forward = forward.normalized;
                Across = (across - Forward * Vector3.Dot(across, Forward)).normalized;
            }

            public Vector3 Blade(float degrees)
            {
                float r = degrees * Mathf.Deg2Rad;
                return (Mathf.Cos(r) * Forward + Mathf.Sin(r) * Across).normalized;
            }

            /// <summary>The way the blade moves as the angle grows: where its edge must face.</summary>
            public Vector3 Edge(float degrees)
            {
                float r = degrees * Mathf.Deg2Rad;
                return (-Mathf.Sin(r) * Forward + Mathf.Cos(r) * Across).normalized;
            }
        }

        /// <summary>The flat forehand: right to left, a hair downhill toward the left (the blade rises a little behind the right shoulder).</summary>
        public static readonly CutPlane ForehandFlat = new CutPlane(new Vector3(0.0f, -0.05f, 1.0f), new Vector3(-1.0f, -0.2f, 0.0f));

        /// <summary>The flat backhand: left to right, the mirror of the forehand.</summary>
        public static readonly CutPlane BackhandFlat = new CutPlane(new Vector3(0.0f, -0.05f, 1.0f), new Vector3(1.0f, -0.2f, 0.0f));

        /// <summary>The two-handed finisher: from over the right shoulder, around and down across the front to the lower left (a 28 degree diagonal).</summary>
        public static readonly CutPlane DiagonalDown = new CutPlane(new Vector3(0.247f, -0.165f, 0.955f), new Vector3(-0.883f, -0.442f, 0.152f));

        /// <summary>The one-handed backhand diagonal: from over the left shoulder down across the front to the lower right (the finisher's mirror).</summary>
        public static readonly CutPlane DiagonalDownBackhand = new CutPlane(new Vector3(-0.247f, -0.165f, 0.955f), new Vector3(0.883f, -0.442f, 0.152f));

        /// <summary>Straight down the middle: from behind the head over the top and down in front (the heavies).</summary>
        public static readonly CutPlane Overhead = new CutPlane(Vector3.forward, Vector3.down);

        // ---- the ready stances, guards and carries ----------------------------------------------------------------------------------------

        /// <summary>
        /// The stance the attacks start from and return to, held for a while after the last one (in place of the carry). Two-handed: a MIDDLE
        /// GUARD, the hands in front of the belly, the blade forward and up at the opponent's face, the edge down, the left foot forward and
        /// the knees a little bent. One-handed: the hand at the waist with the point forward and up, the free arm left to the clips (relaxed
        /// at the side, swinging with the walk: see <see cref="FreeHandRelaxed"/>).
        /// </summary>
        public static SwordFrame Ready(bool oneHanded)
        {
            SwordFrame frame = oneHanded ? Frame(ReadyKey1H, 1.0f, false) : Frame(ReadyKey2H, 1.0f, true);
            frame.ForearmAlign = 1.0f;
            if (oneHanded)
            {
                frame.FreeHandWeight = 0.0f;
            }
            return frame;
        }

        /// <summary>The held guard: one-handed, the blade across the body at chest height and the free arm relaxed (the clips'); two-handed, the blade up across the front, edge out.</summary>
        public static SwordFrame Guard(bool oneHanded)
        {
            SwordFrame frame = oneHanded ? Frame(GuardKey1H, 1.0f, false) : Frame(GuardKey2H, 1.0f, true);
            frame.ForearmAlign = 1.0f;
            if (oneHanded)
            {
                frame.FreeHandWeight = 0.0f;
            }
            return frame;
        }

        /// <summary>
        /// THE TWO-HANDED CARRY: the sword held while nothing is being done with it (walking about, standing). Low and relaxed at the right side,
        /// the point forward and down (the reference's carry); with <paramref name="run01"/> rising to 1 at a sprint, the blade trails behind
        /// instead. The legs are the walk's own (no footwork). A ONE-HANDED sword is carried by its own clips instead (the sword1h_* locomotion,
        /// SwordCarryArm), not posed.
        /// </summary>
        public static SwordFrame Carry(float run01)
        {
            float t = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(run01));
            SwordFrame frame = Lerp(Frame(CarryKey2H, 1.0f, true), Frame(TrailKey2H, 1.0f, true), t);
            frame.Footwork = 0.0f;
            frame.FreeHandWeight = 0.0f;
            frame.ForearmAlign = 1.0f;
            return frame;
        }

        /// <summary>
        /// THE FREE (LEFT) HAND OF A ONE-HANDED SWORD, RELAXED (owner, 2026-10-17, of it held low in front of the stomach after a swing: "i dont
        /// like the way left arm is in holding shield position after swinging. make it relaxed or look at what most one handed sword animations
        /// do with the other hand when not holding anything"). With nothing in it, games and fencers keep the empty hand out of the way: hanging
        /// loose at the side between swings (the stance and the guard leave it to the clips, so it hangs standing and swings walking), and in a
        /// cut swinging against the sword for balance, like a tennis player's other arm: forward while the sword is wound back on the right, drawn
        /// back as it crosses. This is where it hangs, from the left shoulder in arm lengths (out to the left, down, a touch forward): 96% of the
        /// arm's reach, so the elbow keeps only a soft bend (at 85% it bent 65 degrees and stuck out like a hand on the hip).
        /// </summary>
        public static readonly Vector3 FreeHandRelaxed = new Vector3(-0.20f, -0.94f, 0.06f);

        /// <summary>How far the free hand swings forward and back against the sword in a cut, arm lengths.</summary>
        public const float FreeHandBalanceSwing = 0.12f;

        /// <summary>The relaxed free arm's elbow: a little out, mostly back (a soft hanging bend).</summary>
        public const float FreeHandElbowOut = 0.1f;

        private static readonly Key ReadyKey2H = Pose(0.0f, Ease.Smooth, hand: new Vector3(0.10f, 1.42f, 0.40f), blade: new Vector3(-0.06f, 0.37f, 0.927f),
            edge: Vector3.down, elbow: 0.25f, twist: 10.0f, hips: 6.0f, lean: 4.0f, tilt: 0.0f, drop: 0.08f, lead: -1.0f, depth: 0.35f);

        private static readonly Key ReadyKey1H = Pose(0.0f, Ease.Smooth, hand: new Vector3(0.30f, 1.34f, 0.36f), blade: new Vector3(-0.10f, 0.50f, 0.86f),
            edge: Vector3.down, elbow: 0.30f, twist: 12.0f, hips: 6.0f, lean: 3.0f, tilt: 0.0f, drop: 0.08f, lead: -0.5f, depth: 0.30f);

        // THE TWO-HANDED HOLD IS A HAMMER GRIP (2026-10-28, SwordWeapon.DefaultGripRotation): the handle lies across the palm and the forearm
        // runs into the hand along the knuckles, which is where the EDGE faces. So every two-handed key's edge is also where its forearm comes
        // from, and a key whose edge ran along the blade's own line (the old hold's, with the handle along the forearm) would bend the wrist
        // double: the guard holds the blade near upright with the forearms coming in from the sides, the carries hold it out to the right.
        private static readonly Key GuardKey2H = Pose(0.0f, Ease.Smooth, hand: new Vector3(0.12f, 1.52f, 0.42f), blade: new Vector3(-0.15f, 0.95f, 0.27f),
            edge: new Vector3(-0.7f, 0.1f, 0.7f), elbow: 0.35f, twist: 5.0f, hips: 4.0f, lean: 5.0f, tilt: 0.0f, drop: 0.10f, lead: -1.0f, depth: 0.40f);

        private static readonly Key GuardKey1H = Pose(0.0f, Ease.Smooth, hand: new Vector3(0.34f, 1.77f, 0.40f), blade: new Vector3(-0.62f, 0.75f, 0.24f),
            edge: Vector3.forward, elbow: 0.50f, twist: 15.0f, hips: 8.0f, lean: 3.0f, tilt: 0.0f, drop: 0.09f, lead: -0.5f, depth: 0.35f);

        // The two-handed carries hold the grip clear of the belly (the torso is 0.26 m round the spine): at 0.22 m forward the left hand, lower on
        // the handle, sat inside it and both elbows went through the body (owner, 2026-10-19, of the bat).
        // The carry: the sword held low before the belly pointing ahead and down, spikes down, the elbows bent above the fists (the forearms come
        // down the knuckles). Both fists a little left of the middle and half a metre out, where each arm reaches and the left forearm crosses
        // clear of the body (SwordArmsClearPlayTests): at the right hip the left forearm went through the belly.
        private static readonly Key CarryKey2H = Pose(0.0f, Ease.Smooth, hand: new Vector3(0.05f, 1.35f, 0.52f), blade: new Vector3(0.10f, -0.50f, 0.86f),
            edge: Vector3.down, elbow: 0.35f, twist: 0.0f, hips: 0.0f, lean: 0.0f, tilt: 0.0f, drop: 0.0f, lead: -1.0f, depth: 0.30f);

        private static readonly Key TrailKey2H = Pose(0.0f, Ease.Smooth, hand: new Vector3(0.38f, 1.32f, 0.04f), blade: new Vector3(0.25f, -0.45f, -0.86f),
            edge: Vector3.up, elbow: 0.15f, twist: -8.0f, hips: 0.0f, lean: 0.0f, tilt: 0.0f, drop: 0.0f, lead: -1.0f, depth: 0.30f);


        // ---- the keyframes -------------------------------------------------------------------------------------------------------------

        private enum Ease
        {
            /// <summary>Smoothstep: a wind-up (anticipation is never faster than the action), a light's recovery.</summary>
            Smooth,

            /// <summary>Fast off the mark, decelerating into the key (a cubic): a follow-through losing its speed.</summary>
            SlowIn,

            /// <summary>Slow off the mark, accelerating hard into the key (a cubic): the strike, fastest at the contact, a whip not a wave.</summary>
            Snap,

            /// <summary>Straight: a hold (the pose does not change).</summary>
            Linear,

            /// <summary>Decelerating into the key, a little past it, and back: a heavy's recovery settling into the stance like a weight coming to rest.</summary>
            Overshoot,
        }

        /// <summary>How far past the key an <see cref="Ease.Overshoot"/> goes: easeOutBack's usual constant, a tenth of the travel (2-4 cm of hand).</summary>
        private const float OvershootPull = 1.7f;

        private struct Key
        {
            public float T;
            public Vector3 Hand;
            public Vector3 Blade;
            public Vector3 Edge;
            public float ElbowOut;
            public float Twist;
            public float Hips;
            public float Lean;
            public float Tilt;
            public float Drop;
            public float Lead;
            public float Depth;
            public Ease Ease;
            public float Reach;
            public bool HasControl;
            public Vector3 Control;
            public bool HasVia;
            public Vector3 BladeVia;
            public bool OnPlane;
            public CutPlane Plane;
            public float Angle;
            public bool HasFreeHand;
            public Vector3 FreeHand;
            public bool IsStaged;
        }

        /// <summary>
        /// A key, from the wrist's POINT in the body's standing frame (turned into its offset from the shoulder as turned by
        /// <paramref name="twist"/>), the blade, and the way the edge should face (made perpendicular to the blade).
        /// </summary>
        private static Key Pose(float t, Ease ease, Vector3 hand, Vector3 blade, Vector3 edge, float elbow, float twist, float hips, float lean,
            float tilt, float drop, float lead, float depth, float reach = 0.0f)
        {
            Vector3 b = blade.normalized;
            Vector3 e = edge - b * Vector3.Dot(edge, b);
            if (e.sqrMagnitude < 1e-6f)
            {
                e = Vector3.Cross(b, Vector3.up);
                if (e.sqrMagnitude < 1e-6f) e = Vector3.Cross(b, Vector3.forward);
            }
            return new Key
            {
                T = t, Hand = (hand - RightShoulder(twist)) / ArmMetres, Blade = b, Edge = e.normalized, ElbowOut = elbow, Twist = twist, Hips = hips,
                Lean = lean, Tilt = tilt, Drop = drop, Lead = lead, Depth = depth, Ease = ease, Reach = reach,
            };
        }

        /// <summary>A key whose blade lies in a cut's plane at <paramref name="degrees"/>, its edge the plane's tangent (leading the cut).</summary>
        private static Key Pose(float t, Ease ease, Vector3 hand, CutPlane plane, float degrees, float elbow, float twist, float hips, float lean,
            float tilt, float drop, float lead, float depth, float reach = 0.0f)
        {
            Key key = Pose(t, ease, hand, plane.Blade(degrees), plane.Edge(degrees), elbow, twist, hips, lean, tilt, drop, lead, depth, reach);
            key.OnPlane = true;
            key.Plane = plane;
            key.Angle = degrees;
            return key;
        }

        /// <summary>The hand travels on an arc into this key, bowed toward <paramref name="point"/> (the body's standing frame), not a straight chord.</summary>
        private static Key Arc(Key key, Vector3 point, float twistOnTheWay)
        {
            key.HasControl = true;
            key.Control = (point - RightShoulder(twistOnTheWay)) / ArmMetres;
            return key;
        }

        /// <summary>The blade passes near this direction halfway through the segment into this key (for a key not on a cut's plane).</summary>
        private static Key Via(Key key, Vector3 bladeVia)
        {
            key.HasVia = true;
            key.BladeVia = bladeVia.normalized;
            return key;
        }

        /// <summary>The free hand goes here at this key (from the left shoulder, arm lengths), not where it hangs relaxed: a big blow's arm working against the sword.</summary>
        private static Key FreeArm(Key key, Vector3 freeHand)
        {
            key.HasFreeHand = true;
            key.FreeHand = freeHand;
            return key;
        }

        /// <summary>
        /// The strike into this key is STAGED: everything arrives together at the contact, but the hips and the weight set off first, then the shoulders, then
        /// the arm, and the blade (the Snap's cubic) last. All moving as one, the swing reads as an arm waving a stiff body about.
        /// </summary>
        private static Key Staged(Key key)
        {
            key.IsStaged = true;
            return key;
        }

        /// <summary>The powers of a staged strike's ease: the lower, the sooner that part of the body is under way. The blade keeps the Snap's 3.</summary>
        private const float StagedHipsPower = 1.5f;

        private const float StagedShouldersPower = 2.0f;

        private const float StagedHandPower = 2.5f;

        private static Key Hold(Key key, float t)
        {
            key.T = t;
            key.Ease = Ease.Linear;
            return key;
        }

        private static Key At(Key key, float t)
        {
            key.T = t;
            return key;
        }

        /// <summary>How far a heavy's hold creeps on back, metres and degrees: a moving hold, never a dead one.</summary>
        private const float CreepMetres = 0.03f;

        private const float CreepLeanDegrees = 2.0f;

        /// <summary>A heavy's hold at the top: not frozen but still gathering, the hand drawn a little further back and the lean deepening, slowly.</summary>
        private static Key Creep(Key key, float t)
        {
            key.T = t;
            key.Ease = Ease.Smooth;
            key.Hand.z -= CreepMetres / ArmMetres;
            key.Lean -= CreepLeanDegrees;
            return key;
        }

        /// <summary>
        /// An attack's keys in order, timed by <see cref="TimingOf"/>: the ready stance, the wind-up, its hold (a heavy's creeping), the contact,
        /// the follow-through, its settle, and the ready stance again (a heavy's reached with an overshoot).
        /// </summary>
        private static Key[] Assemble(SwordStrikeKind kind, Key ready, Key windup, Key contact, Key follow)
        {
            StrikeTiming tm = TimingOf(kind);
            float total = tm.Total;
            bool heavy = IsHeavy(kind);
            float holdEnd = (tm.WindUp + tm.Hold) / total;
            float followEnd = (tm.Contact + tm.Follow) / total;
            Key back = At(ready, 1.0f);
            back.Ease = heavy ? Ease.Overshoot : Ease.Smooth;
            return new[]
            {
                ready,
                At(windup, tm.WindUp / total),
                heavy ? Creep(windup, holdEnd) : Hold(windup, holdEnd),
                At(contact, tm.Contact / total),
                At(follow, followEnd),
                Hold(follow, followEnd + tm.Settle / total),
                back,
            };
        }

        private static readonly Key[][] Cache = new Key[9][];

        private static Key[] KeysFor(SwordStrikeKind kind)
        {
            int index = (int)kind;
            if (Cache[index] == null)
            {
                Cache[index] = BuildKeys(kind);
            }
            return Cache[index];
        }

        // THE STRIKE AND THE FOLLOW-THROUGH SWEEP THE BLADE AT THE SAME AVERAGE RATE (degrees of its plane over seconds): the strike (Snap)
        // arrives at three times its average, the follow-through (SlowIn) leaves at three times its own, so with the two averages equal the
        // blade does not stop dead at the contact. A light's follow-through seconds in TimingOf = its degrees / the strike's rate; the heavies
        // stop dead on purpose (they hit the ground). The poses here carry no times of their own: Assemble sets them from TimingOf.
        private static Key[] BuildKeys(SwordStrikeKind kind)
        {
            Key ready1 = ReadyKey1H;
            Key ready2 = ReadyKey2H;
            switch (kind)
            {
                case SwordStrikeKind.Sweep2H:
                {
                    // THE FOREHAND: wound up beside the right shoulder like a bat, the blade back over it, hips and shoulders turned right; the
                    // hips lead round, the arms reach out across the front at chest height with the blade flat, and it carries on round to the
                    // left with the shoulders past the hips; the stance opens into the step. 130 degrees in 0.136 s, 120 more in 0.126 s.
                    CutPlane cut = ForehandFlat;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(0.45f, 1.58f, 0.0f), cut, -135.0f, elbow: 0.55f, twist: 45.0f, hips: 20.0f,
                        lean: -3.0f, tilt: 4.0f, drop: 0.09f, lead: -1.0f, depth: 0.40f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(0.04f, 1.48f, 0.70f), cut, -5.0f, elbow: 0.25f, twist: -10.0f, hips: -25.0f,
                        lean: 6.0f, tilt: 2.0f, drop: 0.12f, lead: -1.0f, depth: 0.60f, reach: 1.0f), new Vector3(0.55f, 1.52f, 0.55f), 17.5f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(-0.42f, 1.44f, 0.36f), cut, 115.0f, elbow: 0.40f, twist: -55.0f, hips: -40.0f,
                        lean: 3.0f, tilt: 0.0f, drop: 0.10f, lead: -1.0f, depth: 0.55f, reach: 0.6f);
                    return Assemble(kind, ready2, windup, contact, follow);
                }
                case SwordStrikeKind.Reverse2H:
                {
                    // THE BACKHAND, the return: the hands crossed to the left shoulder, the blade back over it, everything turned left; then flat
                    // across the front to the right at chest height, the hips leading round, the right foot passing forward into the step.
                    CutPlane cut = BackhandFlat;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(-0.22f, 1.60f, 0.14f), cut, -135.0f, elbow: 0.30f, twist: -45.0f, hips: -22.0f,
                        lean: -2.0f, tilt: -4.0f, drop: 0.09f, lead: -0.2f, depth: 0.40f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(0.16f, 1.48f, 0.66f), cut, -5.0f, elbow: 0.30f, twist: 12.0f, hips: 25.0f,
                        lean: 6.0f, tilt: -2.0f, drop: 0.12f, lead: 1.0f, depth: 0.55f, reach: 1.0f), new Vector3(-0.50f, 1.52f, 0.60f), -16.5f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(0.62f, 1.42f, 0.20f), cut, 115.0f, elbow: 0.35f, twist: 50.0f, hips: 38.0f,
                        lean: 3.0f, tilt: 0.0f, drop: 0.10f, lead: 1.0f, depth: 0.50f, reach: 0.6f);
                    return Assemble(kind, ready2, windup, contact, follow);
                }
                case SwordStrikeKind.Cut2H:
                {
                    // THE FINISHER: both hands up at the right shoulder, the blade back over it; around the right side and down across the
                    // front to the lower left with a long step and the hips sinking, the body leaning in behind it.
                    CutPlane cut = DiagonalDown;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(0.40f, 1.80f, -0.02f), cut, -127.5f, elbow: 0.60f, twist: 40.0f, hips: 18.0f,
                        lean: -5.0f, tilt: -3.0f, drop: 0.08f, lead: -1.0f, depth: 0.35f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(0.0f, 1.38f, 0.62f), cut, 10.0f, elbow: 0.30f, twist: -20.0f, hips: -30.0f,
                        lean: 12.0f, tilt: 5.0f, drop: 0.14f, lead: -1.0f, depth: 0.70f, reach: 1.0f), new Vector3(0.55f, 1.60f, 0.50f), 10.0f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(-0.42f, 1.36f, 0.34f), cut, 115.0f, elbow: 0.40f, twist: -55.0f, hips: -42.0f,
                        lean: 10.0f, tilt: 5.0f, drop: 0.13f, lead: -1.0f, depth: 0.65f, reach: 0.6f);
                    return Assemble(kind, ready2, windup, contact, follow);
                }
                case SwordStrikeKind.Slam2H:
                {
                    // The slam: high overhead leaning back, the longest hold of any attack, then down the middle with a long step and the hips
                    // sinking hard, the tip scraping the floor. It stops dead on purpose: it hits the ground.
                    CutPlane cut = Overhead;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(0.25f, 2.42f, -0.12f), cut, -150.0f,
                        elbow: 0.50f, twist: 5.0f, hips: 0.0f, lean: -12.0f, tilt: 0.0f, drop: 0.06f, lead: -1.0f, depth: 0.40f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(0.12f, 1.55f, 0.76f), cut, 24.0f,
                        elbow: 0.25f, twist: -5.0f, hips: -5.0f, lean: 20.0f, tilt: 0.0f, drop: 0.18f, lead: -1.0f, depth: 0.75f, reach: 1.0f),
                        new Vector3(0.15f, 2.45f, 0.45f), 0.0f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(0.12f, 1.36f, 0.66f), cut, 27.0f,
                        elbow: 0.30f, twist: -5.0f, hips: -5.0f, lean: 24.0f, tilt: 0.0f, drop: 0.20f, lead: -1.0f, depth: 0.75f, reach: 0.6f);
                    return Assemble(kind, ready2, windup, contact, follow);
                }
                case SwordStrikeKind.Slash1H:
                {
                    // Coiled against the cut: the arm out to the right at chest height with the blade trailing behind, the hips and shoulders wound with
                    // it, the free arm ahead, and the left foot striding out through the wind-up so it is down before the blade arrives. A flat arc across
                    // the front, the power travelling up from the hips (Staged), the body driving in over the front foot and the free arm swinging back.
                    CutPlane cut = ForehandFlat;
                    Key windup = FreeArm(Pose(0.0f, Ease.Smooth, new Vector3(0.85f, 1.58f, -0.08f), cut, -140.0f, elbow: 0.60f, twist: 55.0f, hips: 28.0f,
                        lean: -6.0f, tilt: 5.0f, drop: 0.12f, lead: -1.0f, depth: 0.60f), new Vector3(-0.18f, -0.72f, 0.60f));
                    Key contact = FreeArm(Staged(Arc(Pose(0.0f, Ease.Snap, new Vector3(-0.02f, 1.50f, 0.78f), cut, -8.0f, elbow: 0.25f, twist: -15.0f, hips: -28.0f,
                        lean: 16.0f, tilt: 4.0f, drop: 0.15f, lead: -1.0f, depth: 0.72f, reach: 1.0f), new Vector3(0.80f, 1.55f, 0.55f), 20.0f)), new Vector3(-0.42f, -0.78f, -0.38f));
                    Key follow = FreeArm(Pose(0.0f, Ease.SlowIn, new Vector3(-0.52f, 1.42f, 0.42f), cut, 110.0f, elbow: 0.40f, twist: -55.0f, hips: -40.0f,
                        lean: 13.0f, tilt: 1.0f, drop: 0.14f, lead: -1.0f, depth: 0.68f, reach: 0.6f), new Vector3(-0.44f, -0.76f, -0.40f));
                    return Assemble(kind, ready1, windup, contact, follow);
                }
                case SwordStrikeKind.Cut1H:
                {
                    // The backhand: the hand crosses past the left shoulder, where the game's camera can see it (nearer the neck the arm was hidden behind the
                    // torso and the blade seemed to grow out of the head), the blade back over the shoulder, the right foot stepping through in the wind-up.
                    // Then diagonally down across the front to the lower right, the free arm swinging forward against it.
                    CutPlane cut = DiagonalDownBackhand;
                    Key windup = FreeArm(Pose(0.0f, Ease.Smooth, new Vector3(-0.40f, 1.92f, 0.30f), cut, -127.5f, elbow: 0.60f, twist: -48.0f, hips: -24.0f,
                        lean: -6.0f, tilt: -4.0f, drop: 0.12f, lead: 1.0f, depth: 0.45f), new Vector3(-0.40f, -0.78f, -0.36f));
                    Key contact = FreeArm(Staged(Arc(Pose(0.0f, Ease.Snap, new Vector3(0.34f, 1.44f, 0.58f), cut, 10.0f, elbow: 0.35f, twist: 25.0f, hips: 30.0f,
                        lean: 16.0f, tilt: -6.0f, drop: 0.15f, lead: 1.0f, depth: 0.62f, reach: 1.0f), new Vector3(-0.55f, 1.80f, 0.55f), -12.0f)), new Vector3(-0.38f, -0.76f, 0.48f));
                    Key follow = FreeArm(Pose(0.0f, Ease.SlowIn, new Vector3(0.58f, 1.32f, 0.28f), cut, 115.0f, elbow: 0.50f, twist: 48.0f, hips: 40.0f,
                        lean: 13.0f, tilt: -4.0f, drop: 0.14f, lead: 1.0f, depth: 0.58f, reach: 0.6f), new Vector3(-0.38f, -0.74f, 0.52f));
                    return Assemble(kind, ready1, windup, contact, follow);
                }
                case SwordStrikeKind.Finisher1H:
                {
                    // Out of the backhand's follow-through on the right: the hand rises over the right shoulder with the blade back and up, the body
                    // sunk and coiled against the cut, leaning away, the free arm reaching ahead at the target. The left foot strides out through the
                    // wind-up, so it is planted before the blade arrives (the stance opening with the strike landed the foot after the blow, the legs
                    // bunched under it). Then around and down across the front to the lower left, the body driving in over the front foot, the right
                    // shoulder dropping, the free arm whipped straight back against it. 140 degrees in 0.13 s, 105 more in 0.095 s.
                    CutPlane cut = DiagonalDown;
                    Key windup = FreeArm(Pose(0.0f, Ease.Smooth, new Vector3(0.70f, 2.12f, -0.10f), cut, -130.0f, elbow: 0.70f, twist: 50.0f, hips: 22.0f,
                        lean: -10.0f, tilt: -6.0f, drop: 0.12f, lead: -1.0f, depth: 0.70f), new Vector3(0.25f, -0.30f, 0.80f));
                    Key contact = FreeArm(Arc(Pose(0.0f, Ease.Snap, new Vector3(0.0f, 1.40f, 0.76f), cut, 10.0f, elbow: 0.25f, twist: -25.0f, hips: -32.0f,
                        lean: 30.0f, tilt: 10.0f, drop: 0.18f, lead: -1.0f, depth: 0.95f, reach: 1.0f), new Vector3(0.72f, 1.92f, 0.52f), 12.5f), new Vector3(-0.45f, -0.55f, -0.60f));
                    Key follow = FreeArm(Pose(0.0f, Ease.SlowIn, new Vector3(-0.38f, 1.26f, 0.44f), cut, 115.0f, elbow: 0.45f, twist: -60.0f, hips: -46.0f,
                        lean: 26.0f, tilt: 8.0f, drop: 0.18f, lead: -1.0f, depth: 0.90f, reach: 0.6f), new Vector3(-0.45f, -0.50f, -0.65f));
                    return Assemble(kind, ready1, windup, contact, follow);
                }
                case SwordStrikeKind.Thrust1H:
                {
                    // Drawn back to the hip, point forward, then driven straight out in a fencer's lunge onto the right foot.
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(0.52f, 1.40f, -0.10f), new Vector3(-0.08f, 0.12f, 0.99f), Vector3.left,
                        elbow: 0.55f, twist: 28.0f, hips: 15.0f, lean: -2.0f, tilt: 0.0f, drop: 0.08f, lead: 0.5f, depth: 0.40f);
                    Key contact = Pose(0.0f, Ease.Snap, new Vector3(0.30f, 1.62f, 0.86f), new Vector3(-0.03f, 0.04f, 1.0f), Vector3.left,
                        elbow: 0.20f, twist: -20.0f, hips: -10.0f, lean: 14.0f, tilt: 0.0f, drop: 0.13f, lead: 1.0f, depth: 0.85f, reach: 1.0f);
                    Key follow = Pose(0.0f, Ease.Linear, new Vector3(0.28f, 1.60f, 0.86f), new Vector3(-0.03f, 0.04f, 1.0f), Vector3.left,
                        elbow: 0.20f, twist: -20.0f, hips: -10.0f, lean: 14.0f, tilt: 0.0f, drop: 0.13f, lead: 1.0f, depth: 0.85f, reach: 0.9f);
                    return Assemble(kind, ready1, windup, contact, follow);
                }
                default:
                {
                    // The one-handed heavy: raised behind the head, a long hold leaning back, then over the top and down the middle with the
                    // hips sinking into a long step.
                    CutPlane cut = Overhead;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(0.45f, 2.42f, -0.20f), cut, -145.0f,
                        elbow: 0.55f, twist: 12.0f, hips: 5.0f, lean: -10.0f, tilt: 0.0f, drop: 0.06f, lead: -0.5f, depth: 0.40f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(0.40f, 1.65f, 0.80f), cut, 17.0f,
                        elbow: 0.20f, twist: -10.0f, hips: -8.0f, lean: 18.0f, tilt: 0.0f, drop: 0.16f, lead: -1.0f, depth: 0.70f, reach: 1.0f),
                        new Vector3(0.50f, 2.35f, 0.35f), 1.0f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(0.40f, 1.34f, 0.70f), cut, 41.0f,
                        elbow: 0.30f, twist: -12.0f, hips: -10.0f, lean: 22.0f, tilt: 0.0f, drop: 0.18f, lead: -1.0f, depth: 0.70f, reach: 0.6f);
                    return Assemble(kind, ready1, windup, contact, follow);
                }
            }
        }

        // ---- evaluation -----------------------------------------------------------------------------------------------------------------

        private static float Eased(Ease ease, float u)
        {
            u = Mathf.Clamp01(u);
            switch (ease)
            {
                case Ease.Smooth: return u * u * (3.0f - 2.0f * u);
                case Ease.SlowIn: return 1.0f - (1.0f - u) * (1.0f - u) * (1.0f - u);
                case Ease.Snap: return u * u * u;
                case Ease.Overshoot:
                {
                    float v = u - 1.0f;
                    return 1.0f + (OvershootPull + 1.0f) * v * v * v + OvershootPull * v * v;
                }
                default: return u;
            }
        }

        private static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float u)
        {
            float v = 1.0f - u;
            return v * v * a + 2.0f * v * u * c + u * u * b;
        }

        /// <summary>The pose at <paramref name="t01"/> (0 to 1) through an attack of the given kind.</summary>
        public static SwordFrame Evaluate(SwordStrikeKind kind, float t01)
        {
            t01 = Mathf.Clamp01(t01);
            SwordFrame frame = Sample(KeysFor(kind), t01, !IsOneHanded(kind));
            frame.Steps = StepsAt(kind, t01);
            return frame;
        }

        /// <summary>
        /// ONE STEP A CUT, AND BOTH FEET DOWN FOR THE BLOW (the one-handed chain): only the foot the cut strides on may step, and it may not set off once it could
        /// not land before the contact. Nothing else leaves the ground until the blow's pose has been held, unless the leg can no longer reach it. Left to the stance
        /// alone, the feet shuffled back and forth and were often in the air as the blade landed. They catch up in the recovery.
        /// </summary>
        private static FootSteps StepsAt(SwordStrikeKind kind, float t01)
        {
            FootSteps stride;
            switch (kind)
            {
                case SwordStrikeKind.Slash1H:
                case SwordStrikeKind.Finisher1H:
                    stride = FootSteps.OnlyLeft;
                    break;
                case SwordStrikeKind.Cut1H:
                    stride = FootSteps.OnlyRight;
                    break;
                default:
                    return FootSteps.Any;
            }
            StrikeTiming timing = TimingOf(kind);
            float seconds = t01 * timing.Total;
            if (seconds > timing.Contact + timing.Follow + timing.Settle)
            {
                return FootSteps.Any;
            }
            return seconds < timing.Contact - SwordFootwork.StepSeconds ? stride : FootSteps.None;
        }

        private static Key[] _rebound1H;
        private static Key[] _rebound2H;

        /// <summary>The pose at <paramref name="t01"/> (0 to 1) through a rebound off a guard (<see cref="Rebound1HName"/>).</summary>
        public static SwordFrame Rebound(bool oneHanded, float t01)
        {
            Key[] keys = oneHanded ? (_rebound1H ?? (_rebound1H = BuildRebound(true))) : (_rebound2H ?? (_rebound2H = BuildRebound(false)));
            return Sample(keys, Mathf.Clamp01(t01), !oneHanded);
        }

        /// <summary>
        /// Thrown back: the hand up beside and behind the shoulder, the blade pointing up and back over it, the edge out at whoever blocked it,
        /// the shoulders turned open to the right and the body leaning away (fast out of the contact, slowing as it goes); held there a moment,
        /// wide open; then gathered back into the ready stance. The start is wherever the cut was when it struck (the pose driver takes over
        /// from it).
        /// </summary>
        private static Key[] BuildRebound(bool oneHanded)
        {
            Key ready = oneHanded ? ReadyKey1H : ReadyKey2H;
            Key thrown = oneHanded
                ? Pose(0.24f, Ease.SlowIn, new Vector3(0.62f, 1.74f, 0.02f), new Vector3(0.28f, 0.80f, -0.53f), Vector3.forward,
                    elbow: 0.60f, twist: 28.0f, hips: 10.0f, lean: -9.0f, tilt: 3.0f, drop: 0.05f, lead: -0.5f, depth: 0.30f)
                : Pose(0.24f, Ease.SlowIn, new Vector3(0.40f, 1.80f, 0.06f), new Vector3(0.20f, 0.85f, -0.48f), Vector3.forward,
                    elbow: 0.50f, twist: 30.0f, hips: 12.0f, lean: -9.0f, tilt: 3.0f, drop: 0.05f, lead: -1.0f, depth: 0.35f);
            return new[] { ready, thrown, Hold(thrown, 0.50f), At(ready, 1.0f) };
        }

        /// <summary>The pose at <paramref name="t01"/> along a run of keys.</summary>
        private static SwordFrame Sample(Key[] keys, float t01, bool twoHanded)
        {

            int i = 0;
            while (i < keys.Length - 2 && t01 >= keys[i + 1].T)
            {
                i++;
            }
            Key a = keys[i];
            Key b = keys[i + 1];
            float span = Mathf.Max(b.T - a.T, 1e-4f);
            float through = (t01 - a.T) / span;
            float u = Eased(b.Ease, through);
            // The hand and the body may overshoot (Ease.Overshoot runs u past 1 and back); the blade's turn, the elbow and the feet never do.
            float uc = Mathf.Clamp01(u);
            float staged = Mathf.Clamp01(through);
            float hipsU = b.IsStaged ? Mathf.Pow(staged, StagedHipsPower) : u;
            float shouldersU = b.IsStaged ? Mathf.Pow(staged, StagedShouldersPower) : u;
            float handU = b.IsStaged ? Mathf.Pow(staged, StagedHandPower) : u;

            Vector3 blade;
            Vector3 edge;
            if (a.OnPlane && b.OnPlane && a.Plane.Forward == b.Plane.Forward && a.Plane.Across == b.Plane.Across)
            {
                // Both ends in the same cut's plane: the blade turns IN the plane (a flat slash stays flat), the edge its tangent (leading).
                float angle = Mathf.Lerp(a.Angle, b.Angle, uc);
                blade = a.Plane.Blade(angle);
                edge = a.Plane.Edge(angle) * Mathf.Sign(b.Angle - a.Angle + 1e-6f);
                if (Mathf.Abs(b.Angle - a.Angle) < 1e-3f)
                {
                    edge = a.Edge;   // a hold: the edge stays as the key has it
                }
            }
            else
            {
                blade = b.HasVia ? Vector3.Slerp(Vector3.Slerp(a.Blade, b.BladeVia, uc), Vector3.Slerp(b.BladeVia, b.Blade, uc), uc) : Vector3.Slerp(a.Blade, b.Blade, uc);
                edge = Vector3.Slerp(a.Edge, b.Edge, uc);
            }

            var key = new Key
            {
                Hand = b.HasControl ? Bezier(a.Hand, b.Control, b.Hand, handU) : Vector3.LerpUnclamped(a.Hand, b.Hand, handU),
                Blade = blade,
                Edge = edge,
                ElbowOut = Mathf.Lerp(a.ElbowOut, b.ElbowOut, uc),
                Twist = Mathf.LerpUnclamped(a.Twist, b.Twist, shouldersU),
                Hips = Mathf.LerpUnclamped(a.Hips, b.Hips, hipsU),
                Lean = Mathf.LerpUnclamped(a.Lean, b.Lean, shouldersU),
                Tilt = Mathf.LerpUnclamped(a.Tilt, b.Tilt, shouldersU),
                Drop = Mathf.LerpUnclamped(a.Drop, b.Drop, hipsU),
                Lead = Mathf.Lerp(a.Lead, b.Lead, uc),
                Depth = Mathf.Lerp(a.Depth, b.Depth, uc),
                Reach = Mathf.Lerp(a.Reach, b.Reach, uc),
            };
            if (a.HasFreeHand || b.HasFreeHand)
            {
                key.HasFreeHand = true;
                // Round from the shoulder, as an arm swings: straight across the chord it would fold in passing.
                key.FreeHand = Vector3.Slerp(FreeHandFor(a), FreeHandFor(b), uc);
            }
            return Frame(key, 1.0f, twoHanded);
        }

        private static SwordFrame Frame(Key key, float weight, bool twoHanded)
        {
            Vector3 blade = key.Blade.sqrMagnitude > 1e-6f ? key.Blade.normalized : Vector3.forward;
            Vector3 edge = key.Edge - blade * Vector3.Dot(key.Edge, blade);
            if (edge.sqrMagnitude < 1e-6f)
            {
                edge = Vector3.Cross(blade, Vector3.up);
            }
            return new SwordFrame
            {
                Active = true,
                Weight = weight,
                Hand = key.Hand,
                Blade = blade,
                Edge = edge.normalized,
                ElbowOut = key.ElbowOut,
                TwistDegrees = key.Twist,
                HipTwistDegrees = key.Hips,
                LeanDegrees = key.Lean,
                TiltDegrees = key.Tilt,
                HipDrop = key.Drop,
                StanceLead = key.Lead,
                StanceDepth = key.Depth,
                Footwork = 1.0f,
                TwoHanded = twoHanded,
                FreeHand = twoHanded ? Vector3.zero : FreeHandFor(key),
                FreeElbowOut = FreeHandElbowOut,
                FreeHandWeight = twoHanded ? 0.0f : 1.0f,
                Reach = key.Reach,
            };
        }

        /// <summary>The free hand through a one-handed attack: hanging relaxed at the side, swinging against the cut for balance (<see cref="FreeHandRelaxed"/>).</summary>
        private static Vector3 FreeHandFor(Key key)
        {
            if (key.HasFreeHand)
            {
                return key.FreeHand;
            }
            // Forward while the sword hand is out on the right (wound up), back as it crosses to the left (the cut through): against the sword.
            float balance = Mathf.Clamp((key.Hand.x - 0.15f) * 0.5f, -1.0f, 1.0f) * FreeHandBalanceSwing;
            return FreeHandRelaxed + new Vector3(0.0f, 0.0f, balance);
        }

        /// <summary>
        /// Halfway (by <paramref name="t"/>) between two poses: positions and angles straight, the blade and edge turned, the hands' hold from
        /// <paramref name="b"/>. How one attack takes over from the pose the body is actually in (the last one's recovery, a guard, the carry).
        /// </summary>
        public static SwordFrame Lerp(in SwordFrame a, in SwordFrame b, float t)
        {
            t = Mathf.Clamp01(t);
            Vector3 blade = Vector3.Slerp(a.Blade, b.Blade, t);
            if (blade.sqrMagnitude < 1e-6f) blade = b.Blade;
            blade.Normalize();
            Vector3 edge = Vector3.Slerp(a.Edge, b.Edge, t);
            edge -= blade * Vector3.Dot(edge, blade);
            if (edge.sqrMagnitude < 1e-6f) edge = Vector3.Cross(blade, Vector3.up);
            return new SwordFrame
            {
                Active = true,
                Weight = Mathf.Lerp(a.Weight, b.Weight, t),
                Hand = Vector3.Lerp(a.Hand, b.Hand, t),
                Blade = blade,
                Edge = edge.normalized,
                ElbowOut = Mathf.Lerp(a.ElbowOut, b.ElbowOut, t),
                TwistDegrees = Mathf.Lerp(a.TwistDegrees, b.TwistDegrees, t),
                HipTwistDegrees = Mathf.Lerp(a.HipTwistDegrees, b.HipTwistDegrees, t),
                LeanDegrees = Mathf.Lerp(a.LeanDegrees, b.LeanDegrees, t),
                TiltDegrees = Mathf.Lerp(a.TiltDegrees, b.TiltDegrees, t),
                HipDrop = Mathf.Lerp(a.HipDrop, b.HipDrop, t),
                StanceLead = Mathf.Lerp(a.StanceLead, b.StanceLead, t),
                StanceDepth = Mathf.Lerp(a.StanceDepth, b.StanceDepth, t),
                Footwork = Mathf.Lerp(a.Footwork, b.Footwork, t),
                Steps = b.Steps,
                TwoHanded = b.TwoHanded,
                FreeHand = Vector3.Lerp(a.FreeHand, b.FreeHand, t),
                FreeElbowOut = Mathf.Lerp(a.FreeElbowOut, b.FreeElbowOut, t),
                FreeHandWeight = Mathf.Lerp(a.FreeHandWeight, b.FreeHandWeight, t),
                Reach = Mathf.Lerp(a.Reach, b.Reach, t),
                ForearmAlign = Mathf.Lerp(a.ForearmAlign, b.ForearmAlign, t),
            };
        }

        /// <summary>
        /// The blade's tip relative to the right shoulder, in body metres, for a frame: the hand plus the blade, for checking reach and clearance
        /// (<paramref name="armLength"/> metres from the shoulder to the wrist, <paramref name="bladeLength"/> metres from the wrist to the tip).
        /// </summary>
        public static Vector3 TipOffset(in SwordFrame frame, float armLength, float bladeLength)
        {
            return frame.Hand * armLength + frame.Blade * bladeLength;
        }
    }
}
