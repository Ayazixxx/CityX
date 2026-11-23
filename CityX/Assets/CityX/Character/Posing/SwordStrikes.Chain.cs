using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE CHAIN'S ATTACKS (docs/CHAIN.md; owner, 2026-11-22, with a video of four games: "i want our main left click combo to be two alternating
    /// diagonal swing animations that play consecutively just like delsin's", one-handed as Dark Souls' whips are swung, "make the combo start with
    /// right to left"; a swing in mid air like Kurapika's in the Hunter x Hunter fighting game; and a downward one "when someone is ragdolled or
    /// downed or dead").
    ///
    /// The BODY, both arms included, is a clip made in Blender (<see cref="ChainMotion"/>, SourceArt/Chain): nothing here poses an arm. What the
    /// keys say is what the game and the clips must agree on, as with every attack (the timing, the step, the shoulders' turn at each moment), and
    /// one thing only they say: WHICH WAY THE CHAIN IS THROWN, the frame's <see cref="SwordFrame.Blade"/>. A chain has no blade to point; it is
    /// flung, and the links trail the hand. The weapon (Weapons/Melee/ChainWeapon) hangs it from the fist and pulls it toward that line, late and
    /// later toward its far end, so it bows back through a swing as a whip does; the same line, the chain's length long, is what a swing hits with.
    /// Each lash turns the line within one plane that holds the straight-ahead (as the one-handed sword's cuts do), so from the game's camera a
    /// swing is one clean diagonal.
    /// </summary>
    public static partial class SwordStrikes
    {
        public const string ChainLashName = "chain_lash";
        public const string ChainBackhandName = "chain_backhand";
        public const string ChainAirName = "chain_air";
        public const string ChainGroundName = "chain_ground";

        /// <summary>A chain's attack.</summary>
        public static bool IsChain(SwordStrikeKind kind) => kind >= SwordStrikeKind.ChainLash && kind <= SwordStrikeKind.ChainGround;

        /// <summary>The chain's attacks as full-body clips with both arms, in Resources (SourceArt/Chain/chain_build.py): one segment an attack.</summary>
        public const string ChainMotionPath = "Chain/ChainMotion";

        public static BakedMotion ChainMotion => BakedMotion.Load(ChainMotionPath);

        // (Made when asked for, not as fields: a partial class's fields in another file may be set before ArmMetres, which Pose divides by.)

        /// <summary>Where a chain's attacks start from and go back to: standing as the walk stands, the arm hanging, the chain down from the fist.</summary>
        private static Key ChainReadyKey => Pose(0.0f, Ease.Smooth, hand: new Vector3(0.50f, 1.04f, 0.06f), blade: new Vector3(0.0f, -1.0f, 0.08f),
            edge: Vector3.forward, elbow: 0.0f, twist: 0.0f, hips: 0.0f, lean: 2.0f, tilt: 0.0f, drop: 0.07f, lead: 0.0f, depth: 0.0f);

        /// <summary>A chain key's frame: one hand, the other arm the clip's.</summary>
        private static SwordFrame ChainFrame(Key key)
        {
            SwordFrame frame = Frame(key, 1.0f, false);
            frame.FreeHandWeight = 0.0f;
            return frame;
        }

        /// <summary>The furthest a chain key has its wrist from the shoulder, arm lengths.</summary>
        private const float ChainKeyReach = 0.99f;

        /// <summary>
        /// The keys with each wrist drawn in to the arm's length. They are written as the clip's wrists in the body's standing frame, where the
        /// clip leans and sinks the body after them; from a shoulder that stands upright, as a key's does, some are out of reach. Nothing poses an
        /// arm from them (the clip has both), so only the offset is shortened.
        /// </summary>
        private static Key[] WithinTheArm(Key[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].Hand = Vector3.ClampMagnitude(keys[i].Hand, ChainKeyReach);
                keys[i].Control = Vector3.ClampMagnitude(keys[i].Control, ChainKeyReach);
            }
            return keys;
        }

        private static Key[] BuildChainKeys(SwordStrikeKind kind)
        {
            switch (kind)
            {
                case SwordStrikeKind.ChainLash:
                {
                    // THE LASH: the hand up and back over the right shoulder, the chain flung out behind it; then over the top and down across the
                    // front to the lower left, straight out at whoever is ahead as it lands, and on down to the ground on the left.
                    CutPlane cut = SteepDown;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(0.55f, 2.45f, -0.75f), cut, -135.0f, elbow: 0.8f, twist: 48.0f, hips: 22.0f,
                        lean: -9.0f, tilt: -5.0f, drop: 0.10f, lead: -1.0f, depth: 0.30f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(-0.06f, 1.23f, 1.28f), cut, 0.0f, elbow: 0.3f, twist: -28.0f, hips: -34.0f,
                        lean: 30.0f, tilt: 9.0f, drop: 0.21f, lead: -1.0f, depth: 0.70f, reach: 1.0f), new Vector3(0.62f, 2.30f, 0.35f), 10.0f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(-0.42f, 0.92f, 1.00f), cut, 75.0f, elbow: 0.3f, twist: -58.0f, hips: -44.0f,
                        lean: 34.0f, tilt: 8.0f, drop: 0.25f, lead: -1.0f, depth: 0.75f, reach: 0.6f);
                    return WithinTheArm(Assemble(kind, ChainReadyKey, windup, contact, follow));
                }
                case SwordStrikeKind.ChainBackhand:
                {
                    // THE BACKHAND: the hand brought up before the left shoulder, the chain flung back over it; then over and down across the front
                    // to the lower right, the mirror of the lash.
                    CutPlane cut = SteepDownBackhand;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(-0.42f, 1.86f, 0.34f), cut, -135.0f, elbow: -0.6f, twist: -62.0f, hips: -28.0f,
                        lean: -6.0f, tilt: -4.0f, drop: 0.12f, lead: 1.0f, depth: 0.30f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(0.58f, 1.30f, 0.96f), cut, 0.0f, elbow: 0.3f, twist: 26.0f, hips: 22.0f,
                        lean: 20.0f, tilt: -6.0f, drop: 0.17f, lead: 1.0f, depth: 0.70f, reach: 1.0f), new Vector3(0.05f, 2.05f, 0.95f), -15.0f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(0.84f, 1.05f, 0.28f), cut, 75.0f, elbow: 0.3f, twist: 54.0f, hips: 30.0f,
                        lean: 18.0f, tilt: -5.0f, drop: 0.18f, lead: 1.0f, depth: 0.75f, reach: 0.6f);
                    return WithinTheArm(Assemble(kind, ChainReadyKey, windup, contact, follow));
                }
                case SwordStrikeKind.ChainAir:
                {
                    // IN THE AIR: the hand behind the head and the chain up and back; then straight down ahead, steeply, onto whoever is below.
                    CutPlane cut = Overhead;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(0.50f, 2.62f, -0.70f), cut, -140.0f, elbow: 0.8f, twist: 34.0f, hips: 16.0f,
                        lean: -8.0f, tilt: 0.0f, drop: 0.0f, lead: 0.0f, depth: 0.0f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(0.10f, 0.95f, 1.15f), cut, 40.0f, elbow: 0.3f, twist: -20.0f, hips: -14.0f,
                        lean: 26.0f, tilt: 5.0f, drop: 0.0f, lead: 0.0f, depth: 0.0f, reach: 1.0f), new Vector3(0.42f, 2.55f, 0.75f), 5.0f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(-0.02f, 0.72f, 1.00f), cut, 80.0f, elbow: 0.3f, twist: -28.0f, hips: -18.0f,
                        lean: 29.0f, tilt: 5.0f, drop: 0.0f, lead: 0.0f, depth: 0.0f, reach: 0.6f);
                    return WithinTheArm(Assemble(kind, ChainReadyKey, windup, contact, follow));
                }
                default:
                {
                    // ONTO A BODY ON THE GROUND: the hand straight up behind the head, the chain back over it, held a beat; then down at the ground
                    // ahead, the chain's length laid along it.
                    CutPlane cut = Overhead;
                    Key windup = Pose(0.0f, Ease.Smooth, new Vector3(0.45f, 2.70f, -0.65f), cut, -125.0f, elbow: 0.8f, twist: 30.0f, hips: 14.0f,
                        lean: -14.0f, tilt: 0.0f, drop: 0.05f, lead: -1.0f, depth: 0.30f);
                    Key contact = Arc(Pose(0.0f, Ease.Snap, new Vector3(0.20f, 0.80f, 1.25f), cut, 50.0f, elbow: 0.3f, twist: -12.0f, hips: -10.0f,
                        lean: 44.0f, tilt: 4.0f, drop: 0.34f, lead: -1.0f, depth: 0.55f, reach: 1.0f), new Vector3(0.40f, 2.50f, 0.80f), 5.0f);
                    Key follow = Pose(0.0f, Ease.SlowIn, new Vector3(0.12f, 0.62f, 1.15f), cut, 64.0f, elbow: 0.3f, twist: -16.0f, hips: -12.0f,
                        lean: 48.0f, tilt: 4.0f, drop: 0.38f, lead: -1.0f, depth: 0.55f, reach: 0.6f);
                    return WithinTheArm(Assemble(kind, ChainReadyKey, windup, contact, follow));
                }
            }
        }
    }
}
