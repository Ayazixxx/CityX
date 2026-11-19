using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHAT KIND OF SHIELD A BODY CARRIES (docs/SHIELD.md): how it is held up, what it leaves uncovered, and what it can be used beside. The round
    /// shield's are the numbers every shield had before the riot shield; the riot shield's are the owner's rules of 2026-11-18 ("make a riot shield
    /// make it so you can equip 1 handed weapons with it", "when you hold right click, he brings the shield forward and blocks with it and if you
    /// want to shoot you have to left click with no aim in and you have to move the riot shield from its guarded state", feet showing unless crouched
    /// as in Call of Duty, and "no guard meter ... it should last forever").
    /// </summary>
    public sealed class ShieldTraits
    {
        /// <summary>Raised, where the left hand goes, from the left shoulder, metres on the body at its built height: across, up, forward.</summary>
        public Vector3 RaisedHand = new Vector3(0.38f, -0.32f, 0.62f);

        /// <summary>How far the shield's face stands off the middle of the fist, metres.</summary>
        public float StandOff = 0.11f;

        /// <summary>How far above the shield's middle its grip is, metres: a tall shield hangs mostly below the fist.</summary>
        public float GripAboveMiddle;

        /// <summary>Too tall to hang from a straight arm: lowered, it is carried upright with the arm bent, the hand here from the left shoulder (as <see cref="RaisedHand"/>).</summary>
        public bool CarriedBent;

        public Vector3 CarriedHand;

        /// <summary>The speed the body walks at with it up, as a share of its own.</summary>
        public float MoveSpeedShare = PlayerLocomotion.ShieldMoveSpeedShare;

        /// <summary>It can only be raised beside a one-handed weapon (or bare hands), and with a pistol right-click raises it and nothing aims.</summary>
        public bool BesideOneHandedOnly;

        /// <summary>A click to shoot, swing or cast takes it out of its guard for this long, seconds: it cannot cover and attack at once. 0: it can.</summary>
        public float OpenToAttackSeconds;

        /// <summary>Raised on a standing body, it covers nothing this near the ground, metres on the body at its built height. Crouched behind it, everything.</summary>
        public float UncoveredBelow;

        public static readonly ShieldTraits Round = new ShieldTraits();

        /// <summary>Tall, held a little out and lower; no slowing (owner, 2026-11-06: "no annoying penalties"); my numbers.</summary>
        public static readonly ShieldTraits Riot = new ShieldTraits
        {
            RaisedHand = new Vector3(0.34f, -0.16f, 0.66f),
            StandOff = 0.09f,
            GripAboveMiddle = 0.18f,
            CarriedBent = true,
            CarriedHand = new Vector3(-0.06f, -0.62f, 0.24f),
            MoveSpeedShare = 1.0f,
            BesideOneHandedOnly = true,
            OpenToAttackSeconds = 0.5f,
            UncoveredBelow = 0.55f,
        };

        /// <summary>
        /// Whether it covers a hit landing <paramref name="heightAboveFeet"/> up the body (metres, the body <paramref name="bodyScale"/> times its
        /// built height), raised and from the front.
        /// </summary>
        public bool CoversHeight(float heightAboveFeet, float bodyScale, bool crouching) => crouching || heightAboveFeet >= UncoveredBelow * bodyScale;

        /// <summary>
        /// Whether it can be raised with <paramref name="held"/> in the other hand. The round shield: with anything but a gun or a spray can, whose
        /// right-click is their own. The riot shield: bare hands, a one-handed melee weapon, a pistol or a wand (owner, 2026-11-18: wands only, not staffs).
        /// </summary>
        public bool GoesBeside(HeldKind held)
        {
            if (BesideOneHandedOnly)
            {
                return held == HeldKind.BareHands || held == HeldKind.OneHandedMelee || held == HeldKind.Pistol || held == HeldKind.Wand;
            }
            return held != HeldKind.Pistol && held != HeldKind.LongGun && held != HeldKind.SprayCan;
        }
    }

    /// <summary>What a body has in its weapon hand, as a shield sees it (<see cref="ShieldTraits.GoesBeside"/>).</summary>
    public enum HeldKind
    {
        BareHands,
        OneHandedMelee,
        TwoHandedMelee,
        Pistol,
        LongGun,
        Wand,
        Staff,
        SprayCan,

        /// <summary>Anything else: a flamethrower, a torch, the grappling hook, the Giant Boomerang.</summary>
        Other,
    }
}
