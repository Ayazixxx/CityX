using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// One swing of the sword, defined the way a soulslike defines it: how long
    /// it winds up, how long the blade is actually dangerous, how long you are
    /// stuck recovering, and what it costs. Public fields so the /sword console
    /// command can retune any of it live.
    /// </summary>
    [System.Serializable]
    public sealed class SoulsAttack
    {
        [Tooltip("Animator state played for this swing.")]
        public string Animation;

        [Tooltip("Seconds of wind-up before the blade can hurt anything.")]
        public float Startup;

        [Tooltip("Seconds the blade is live (the ACTIVE frames). Each target is hit at most once per swing.")]
        public float Active;

        [Tooltip("Seconds of recovery after the active window, during which you are committed and open.")]
        public float Recovery;

        [Tooltip("Fraction of Recovery that must have passed before a buffered next attack may fire. Lower = snappier chains.")]
        public float ChainAt;

        [Tooltip("Lands as a heavy blow whatever its stun: a chain's finisher.")]
        public bool LandsHeavily;

        [Tooltip("Stamina spent to start this swing.")]
        public float StaminaCost;

        [Tooltip("Multiplier on the sword's base damage.")]
        public float DamageScale;

        [Tooltip("Seconds a target is staggered when this lands.")]
        public float HitstunSeconds;

        [Tooltip("Metres the body steps forward across startup+active -- the small lunge every soulslike swing has.")]
        public float LungeMetres;

        [Tooltip("Poise this blow takes off a target inside an attack with hyper-armour (PlayerVitals.MaxPoise is 60): they stagger once the pool is spent.")]
        public float PoiseDamage = 25.0f;

        [Tooltip("Hyper-armour: during this attack's wind-up and active frames, blows only stagger once the poise is spent (the heavies have it, the lights do not).")]
        public bool HyperArmour;

        [Tooltip("The blow breaks a held guard open (only a parry stops it): the heavies, since 2026-10-24.")]
        public bool BreaksGuard;

        [Tooltip("Seconds into the wind-up where the pose rests at its top: where an enemy's tell holds the swing (SoulsSwordCombat.TelegraphLightSeconds).")]
        public float HoldAt;

        [Tooltip("Seconds into the swing where a posed blade arrives at the front of the body: the forgiving reach volume waits for it. 0 for a clip.")]
        public float ContactAt;

        [Tooltip("Length in seconds of the Mixamo clip at its own pace (measured -- see SoulsSwordCapture.MeasureClips).")]
        public float ClipSeconds;

        [Tooltip("Where in the clip (0-1) the swing starts, skipping its idle lead-in.")]
        public float ClipStart;

        [Tooltip("Where in the clip (0-1) the swing is cut off. The stretch from ClipStart to ClipEnd is played over Startup+Active+Recovery.")]
        public float ClipEnd;

        public float Total => Startup + Active + Recovery;

        /// <summary>
        /// Playback speed that fits the clip's ClipStart..ClipEnd stretch into
        /// Total seconds, so the animation's strike lands inside the Active
        /// window. 1 when the clip is not described.
        /// </summary>
        public float AnimatorSpeed
        {
            get
            {
                float window = ClipSeconds * (ClipEnd - ClipStart);
                return window > 0.01f && Total > 0.01f ? window / Total : 1.0f;
            }
        }

        /// <summary>Seconds into the clip to start playing from.</summary>
        public float StartSeconds => ClipSeconds * ClipStart;

        /// <summary>
        /// An attack posed by <see cref="SwordStrikes"/>: its windows come from the strike's own timing. The blade is live from just before the
        /// contact (the fastest moment of the path) into the follow-through, and the recovery is the rest of the swing.
        /// </summary>
        public static SoulsAttack FromStrike(SwordStrikeKind kind, float chainAt, float staminaCost, float damageScale, float hitstunSeconds, float lungeMetres,
            float poiseDamage, bool hyperArmour)
        {
            float seconds = SwordStrikes.Seconds(kind);
            float start = SwordStrikes.ActiveStart(kind);
            float end = SwordStrikes.ActiveEnd(kind);
            var attack = new SoulsAttack(SwordStrikes.NameOf(kind), seconds * start, seconds * (end - start), seconds * (1.0f - end),
                chainAt, staminaCost, damageScale, hitstunSeconds, lungeMetres, 0.0f, 0.0f, 0.0f);
            attack.PoiseDamage = poiseDamage;
            attack.HyperArmour = hyperArmour;
            attack.BreaksGuard = hyperArmour;   // the heavies
            SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(kind);
            attack.HoldAt = timing.WindUp + timing.Hold * 0.5f;   // in the pause at the top, where the pose rests
            attack.ContactAt = timing.Contact;
            return attack;
        }

        /// <summary>The attack as a chain's finisher: it lands as a heavy blow whatever its stun (DamageContext.HeavyBlow), a longer hit-stop included.</summary>
        public static SoulsAttack AsFinisher(SoulsAttack attack)
        {
            attack.LandsHeavily = true;
            return attack;
        }

        /// <summary>
        /// The stun it leaves on a target, swung at <paramref name="swingSpeed"/> (Melee Speed). A finisher's shrinks with the swing: its recovery does, and a
        /// fast enough blade would otherwise be free before its target is.
        /// </summary>
        public float HitstunAt(float swingSpeed) => LandsHeavily ? HitstunSeconds / Mathf.Max(swingSpeed, 1.0f) : HitstunSeconds;

        /// <summary>True when the attack is posed by SwordStrikes rather than played from a clip.</summary>
        public bool IsProcedural => SwordStrikes.Has(Animation);

        public SoulsAttack(string animation, float startup, float active, float recovery, float chainAt,
            float staminaCost, float damageScale, float hitstunSeconds, float lungeMetres,
            float clipSeconds, float clipStart, float clipEnd)
        {
            Animation = animation;
            Startup = startup;
            Active = active;
            Recovery = recovery;
            ChainAt = chainAt;
            StaminaCost = staminaCost;
            DamageScale = damageScale;
            HitstunSeconds = hitstunSeconds;
            LungeMetres = lungeMetres;
            ClipSeconds = clipSeconds;
            ClipStart = clipStart;
            ClipEnd = clipEnd;
            HoldAt = startup * ClipHoldShare;
        }

        /// <summary>A clip's wind-up is taken to rest at its top this far in (a posed strike knows its own: <see cref="FromStrike"/>).</summary>
        public const float ClipHoldShare = 0.6f;
    }

    /// <summary>
    /// What the sword combat needs from the sword in hand: where the blade is
    /// right now and how hard it hits. An interface because the Weapons
    /// assembly depends on Character, not the other way round.
    /// </summary>
    public interface ISwordBlade
    {
        /// <summary>Where the blade meets the grip, in world space.</summary>
        Vector3 BladeBaseWorld { get; }

        /// <summary>The blade's tip, in world space.</summary>
        Vector3 BladeTipWorld { get; }

        /// <summary>
        /// The way the cutting edge faces (perpendicular to the blade), in world space, or zero when the blade does not say. The sword
        /// spin rolls the hand to lead with it.
        /// </summary>
        Vector3 EdgeDirectionWorld => Vector3.zero;

        /// <summary>True for a sword held in one hand (the other is free): the one-handed attacks and guard are used.</summary>
        bool IsOneHanded => false;

        /// <summary>True for a knife (one-handed too): the knife's quicker, lighter attacks, its reach and guard, its backstab (KnifeRules).</summary>
        bool IsKnife => false;

        /// <summary>True for a hammer (two-handed): the two-handed attacks, but the light chain ends in an overhead smash straight down.</summary>
        bool IsHammer => false;

        /// <summary>
        /// Holds the sword in the thrust grip at <paramref name="weight"/> (the handle in the fist, turned so the blade leans out toward the
        /// fingertips), and as it is otherwise held at 0. PlayerLocomotion does it while the stab dash's or the spin's pose holds the body (BakedPose.ThrustGrip).
        /// </summary>
        void HoldForThrust(float weight)
        {
        }

        /// <summary>Holds the sword in the mocap's two-handed grip at <paramref name="weight"/>: the Octaslash was posed with it (BakedPose.MocapGrip).</summary>
        void HoldForMocap(float weight)
        {
        }

        float GetMeleeDamage();
    }

    public enum SoulsPhase
    {
        Idle,
        Startup,
        Active,
        Recovery,
    }

    /// <summary>
    /// The sword, fought the way a soulslike fights it. Added to the body when
    /// a sword is equipped (SwordWeapon.OnEquip); nothing else about the body
    /// changes, and bare fists / the bottle keep MeleeAttacker's own combat.
    ///
    /// THE RULES, from how Dark Souls actually works (see
    /// docs/SOULSLIKE_COMBAT.md for the research and sources):
    ///
    ///  * COMMITMENT. Every swing is Startup -> Active -> Recovery and you
    ///    cannot walk out of it or cancel it; the only ways out are being hit
    ///    (which staggers you) or rolling during Recovery.
    ///  * ACTIVE FRAMES ONLY. The blade hurts only during the Active window,
    ///    and it is a real capsule swept along the actual sword, so a swing
    ///    that visibly misses, misses. Each target is hit once per swing.
    ///  * STAMINA. Every swing, roll and blocked hit spends the shared bar;
    ///    recovery pauses briefly after spending, then refills at 45/s. You may
    ///    swing with any stamina left, even if the cost is more than remains.
    ///  * INPUT BUFFER. An attack pressed early is remembered and fires the
    ///    instant the chain window opens, instead of being dropped.
    ///  * HEAVY vs LIGHT. The heavy has far more startup and recovery, far more
    ///    damage, and a long stagger -- a decision, not an upgrade.
    ///  * HELD BLOCK. Blocking costs stamina per hit absorbed; a hit you cannot
    ///    afford breaks your guard and staggers you wide open.
    ///
    /// EVERY RULE IS A FUNCTION OF A DELTA, like MeleeAttacker: Tick(dt) is the
    /// only thing that advances anything, so all of it is testable without a
    /// scene.
    /// </summary>
    public sealed class SoulsSwordCombat : MonoBehaviour
    {
        // --- the moveset ---------------------------------------------------

        // THE ATTACKS ARE POSED, NOT PLAYED FROM CLIPS (SwordStrikes, since 2026-10-13: the Mixamo chops were stiff, close to the chest and the
        // same for every swing, and there were none for one hand). Each attack's live window comes from its own strike: the blade goes live
        // just before the contact -- the fastest moment of the path -- and stops in the follow-through; the rest of the swing is the recovery.
        // Two sets, picked by the sword in hand (ISwordBlade.IsOneHanded): the greatsword's big cuts, and the combat sword's quicker ones,
        // whose first attack is a horizontal slash (the owner's choice). The two-handed chain is two FLAT slashes, forehand then backhand,
        // then the diagonal finisher (owner, 2026-10-16: "i like the two handed horizontal slashes of this linked game").
        // Poise: a light takes 25 (20 one-handed) off a target's 60, a heavy 60 (50) -- so a heavy breaks an opponent's heavy outright and it
        // takes three lights to stop one; only the heavies have hyper-armour themselves (the research: poise is for committed attacks).
        // HITSTUN (2026-10-17, owner: "if there wasnt dashing it would pretty much be two people spamming click on eachother"): a landed light
        // now stuns for longer than it takes the chain's next cut to arrive (0.35-0.4 s let the victim get up and trade blow for blow, so the
        // fight was whoever clicked first). The second cut of a chain lands while the first still holds, as a straight sword's R1-R1 does in
        // Dark Souls: a landed cut is a follow-up earned, and the way out is a guard held through the stun (the parry window catches the next
        // cut) or a dash the moment it ends. SoulsCombatRhythmTests pins the margin.
        // THE TEMPO (2026-11-13, owner: swings "telegraphed ... akin to dark souls combat", and slower, readable wind-ups chosen over the
        // 0.3 s lights of 2026-10-23): a light lands about 0.4 s after the click (a two-handed one nearer 0.47) and its chain window opens
        // 35% into its recovery, as its follow-through's pose lets go, so a chain runs at roughly a cut every 0.6 to 0.7 s; the stuns went
        // up with it to keep the margin above, and the damage with the cycle, so a sword does a second what it did. Each attack steps the
        // body by its clip's own stride (SwordStrikes.StepMetres). The heavies keep their long stuns.
        [Header("Two-handed light chain (left click)")]
        public SoulsAttack Light1 = SoulsAttack.FromStrike(SwordStrikeKind.Sweep2H, 0.35f, 22.0f, 1.23f, 0.62f, SwordStrikes.StepMetres(SwordStrikeKind.Sweep2H), 25.0f, false);
        public SoulsAttack Light2 = SoulsAttack.FromStrike(SwordStrikeKind.Reverse2H, 0.35f, 22.0f, 1.29f, 0.70f, SwordStrikes.StepMetres(SwordStrikeKind.Reverse2H), 25.0f, false);   // longer: the finisher after it winds up for 0.44 s
        public SoulsAttack Light3 = SoulsAttack.FromStrike(SwordStrikeKind.Cut2H, 0.50f, 26.0f, 1.60f, 0.75f, SwordStrikes.StepMetres(SwordStrikeKind.Cut2H), 30.0f, false);

        /// <summary>A hammer's third light: straight down overhead (owner, 2026-11-05), a little heavier than the diagonal it replaces. My numbers.</summary>
        public SoulsAttack HammerLight3 = SoulsAttack.FromStrike(SwordStrikeKind.Smash2H, 0.50f, 28.0f, 1.80f, 0.78f, SwordStrikes.StepMetres(SwordStrikeKind.Smash2H), 40.0f, false);

        [Header("Two-handed heavy (held left click)")]
        public SoulsAttack Heavy = SoulsAttack.FromStrike(SwordStrikeKind.Slam2H, 0.55f, 40.0f, 2.20f, 1.00f, SwordStrikes.StepMetres(SwordStrikeKind.Slam2H), 60.0f, true);

        [Header("One-handed light chain (left click)")]
        public SoulsAttack OneHandLight1 = SoulsAttack.FromStrike(SwordStrikeKind.Slash1H, 0.35f, 16.0f, 0.92f, 0.52f, SwordStrikes.StepMetres(SwordStrikeKind.Slash1H), 20.0f, false);
        public SoulsAttack OneHandLight2 = SoulsAttack.FromStrike(SwordStrikeKind.Cut1H, 0.35f, 16.0f, 0.98f, 0.52f, SwordStrikes.StepMetres(SwordStrikeKind.Cut1H), 20.0f, false);

        /// <summary>
        /// THE FINISHER (owner, 2026-11-04: a three-hit combo ending in a diagonal, no fling to start, and "if the final hit gives a stagger then why wouldnt
        /// you just keep attacking you can just spam click"): the biggest of the three and it lands as a heavy blow, but its stagger is shorter than the
        /// recovery it leaves the attacker in, which cannot be cancelled into the next swing (chain window 1, the very end). The target is free first.
        /// Its damage pays for that recovery: the whole string does what the two cuts alone did a second, and so stays above a knife's (KnifeRulesTests).
        /// </summary>
        public SoulsAttack OneHandLight3 = SoulsAttack.AsFinisher(SoulsAttack.FromStrike(SwordStrikeKind.Finisher1H, 1.0f, 24.0f, 2.45f, 0.50f, SwordStrikes.StepMetres(SwordStrikeKind.Finisher1H), 30.0f, false));

        [Header("One-handed heavy (held left click)")]
        public SoulsAttack OneHandHeavy = SoulsAttack.FromStrike(SwordStrikeKind.Overhead1H, 0.55f, 30.0f, 1.85f, 0.90f, SwordStrikes.StepMetres(SwordStrikeKind.Overhead1H), 50.0f, true);

        // THE KNIFE (KnifeRules, docs/KNIFE.md): the one-handed slash and backhand, back and forth, run KnifeRules.SwingSpeed faster, cheaper
        // in stamina and with NO STAGGER -- a knife's light does not stop what its target is doing, so it cannot trade blows with a sword
        // (a sword's light stuns longer than it takes the next cut to arrive: SoulsCombatRhythmTests). The heavy is a lunging stab that
        // closes the distance, guard-breaking as every heavy.
        [Header("Knife light chain (left click)")]
        public SoulsAttack KnifeLight1 = SoulsAttack.FromStrike(SwordStrikeKind.Slash1H, 0.35f, 11.0f, 0.98f, 0.0f, 0.15f, 8.0f, false);
        public SoulsAttack KnifeLight2 = SoulsAttack.FromStrike(SwordStrikeKind.Cut1H, 0.35f, 11.0f, 1.05f, 0.0f, 0.15f, 8.0f, false);

        [Header("Knife heavy (held left click): the lunging stab")]
        public SoulsAttack KnifeHeavy = SoulsAttack.FromStrike(SwordStrikeKind.Thrust1H, 0.50f, 22.0f, 2.05f, 0.60f, 1.0f, 35.0f, true);

        [Header("Feel")]
        [Tooltip("How long an early attack press is remembered. Long enough that a click during a light swing still chains at its window, " +
                 "short enough that a click you have forgotten does not throw a swing a moment later.")]
        public float BufferSeconds = 0.45f;

        [Tooltip("Seconds stamina recovery pauses after spending it on a swing.")]
        public float RegenDelaySeconds = 0.6f;

        [Tooltip("Stamina per second once recovery resumes. Dark Souls 3: 45.")]
        public float StaminaRegenPerSecond = 45.0f;

        [Tooltip("Seconds the swing freezes (animation and timing both) the instant its blade connects -- the WHOLE body with it since " +
                 "2026-10-17 (PlayerLocomotion.Impact.cs: the legs used to walk on under a frozen arm), and the victim holds for its own " +
                 "instant (HitImpactEffects). A few frames of stillness is what sells the impact. 0 turns it off.")]
        public float HitStopSeconds = 0.09f;

        [Tooltip("A heavy attack's hit-stop is this many times longer.")]
        public float HeavyHitStopMultiplier = 1.7f;

        [Header("Hit volumes (live only during the active frames)")]
        [Tooltip("Radius of the blade capsule swept along the actual sword.")]
        public float BladeRadius = 0.16f;

        [Tooltip("Sweep samples between last frame's blade and this frame's, so a fast swing cannot tunnel through a target.")]
        public int SweepSamples = 3;

        [Tooltip("Metres in front of the body the reach volume extends. The Mixamo swings keep the blade close to the body " +
                 "(the measured tip rarely gets past 1m ahead in a light attack), so the blade alone would miss a target at " +
                 "arm's length; this box in front of the wielder catches it. 0 turns it off (blade only).")]
        public float ReachMetres = 2.2f;

        [Tooltip("Half the width of the reach volume, in metres.")]
        public float ReachHalfWidth = 0.8f;

        [Header("Roll")]
        [Tooltip("Stamina a roll costs.")]
        public float RollStaminaCost = 20.0f;

        [Tooltip("Seconds of invincibility on a roll. Elden Ring's medium roll: 13 frames at 30 = 0.43s.")]
        public float RollInvulnerabilitySeconds = 0.42f;

        [Tooltip("Seconds stamina recovery pauses after a roll.")]
        public float RollRegenDelaySeconds = 0.6f;

        [Header("Block")]
        [Tooltip("Stamina lost per point of damage a held block absorbs.")]
        public float BlockStaminaPerDamage = 1.0f;

        [Tooltip("Seconds you are staggered when a hit breaks your guard.")]
        public float GuardBreakStunSeconds = 1.0f;

        [Tooltip("Seconds YOU are staggered when your swing is PARRIED (caught in the first instant of a guard): the blade thrown back, you wide open. Sekiro-long.")]
        public float BlockedRecoilSeconds = 0.9f;

        /// <summary>
        /// THE GUARD REBOUND (owner, 2026-10-17: "implement a stagger system for if you swing at a block"): a blow a HELD guard soaks throws
        /// the attacker's blade back off it (SwordStrikes.Rebound1HName) and leaves them stunned this long -- open to the defender's answer.
        /// Shorter than a parry's <see cref="BlockedRecoilSeconds"/>, which stays the bigger punishment for the better-timed guard. A blow the
        /// guard could not afford breaks the guard instead and does not rebound.
        /// </summary>
        public float GuardReboundSeconds = 0.6f;

        /// <summary>How far a blow bounced off a guard knocks its attacker back, metres: kept small, or the two end up out of reach and the opening cannot be punished.</summary>
        public const float ReboundShoveMetres = 0.25f;

        public const float ReboundShoveSeconds = 0.2f;

        /// <summary>The jolt of the player's own blade bouncing: harder than a blade that lands (HitImpactEffects.LandedShakeMetres).</summary>
        public const float ReboundShakeMetres = 0.1f;

        public const float ReboundShakeSeconds = 0.22f;

        /// <summary>Raised when this body's blade is thrown back off a guard (a soak or a parry, here or decided on another player's machine).</summary>
        public event System.Action Rebounded;

        /// <summary>A rebound decided this frame, shown as the swing it came off ends (the next Tick: the stun interrupts the swing first).</summary>
        private bool _reboundPending;

        [Tooltip("Seconds from the guard coming up during which a blow is parried outright: no damage, no stamina, the attacker staggered. Tap to parry, hold to block.")]
        public float PerfectGuardSeconds = 0.2f;

        [Tooltip("Seconds a hit on another player's copy waits for the owner's verdict (parried, rolled) before it is taken as landed for good.")]
        public float VerdictTimeoutSeconds = 2.0f;

        public LayerMask TargetLayers = ~0;

        /// <summary>
        /// Set by the input layer while the body cannot start an action (mid
        /// roll). A request made while locked is still remembered by the
        /// buffer and fires the moment this clears -- an attack pressed during
        /// a roll comes out as the roll ends.
        /// </summary>
        [System.NonSerialized]
        public bool Locked;

        /// <summary>The guard pose's state for the sword in hand (a SwordStrikes guard, posed rather than a clip), announced so other players' copies raise it too.</summary>
        public string BlockStateName => SwordStrikes.GuardName(_oneHanded);

        /// <summary>
        /// What a swing or a guard blends BACK to when it ends: the upper-body
        /// layer's empty state, which hands the arms back to the base layer's
        /// walk cycle. (It used to be a crossfade to "SwordLocomotion" on the
        /// base layer, which restarted the leg cycle mid-stride.)
        /// </summary>
        public const string IdleStateName = PlayerAnimationDriver.UpperEmptyStateName;

        [Tooltip(
            "Fraction of walking speed while a swing is under way, up to its recovery's tail (SlowsMovement). 0: a swing is a commitment, as in " +
            "Dark Souls (owner, 2026-11-13): the body does not walk under it, the attack's own step carries it (SwordStrikes.StepMetres). " +
            "It was 0.65 while the legs walked on under an arm.")]
        public float CommittedMoveSpeedFraction = 0.0f;

        [Tooltip("How fast the body turns to where the camera looks as a swing winds up, degrees per second. Whatever the turn, the body " +
                 "faces it by the time the blade is live; turning rather than snapping is what keeps a swing from jumping round on the spot.")]
        public float SwingTurnDegreesPerSecond = 1080.0f;

        [Tooltip(
            "AN ENEMY'S TELL (0 for a player): seconds the light that opens an attack holds at the top of its wind-up, the blade drawn back, " +
            "before it strikes, so it can be seen coming and dashed (owner, 2026-10-24: 'the telegraph of the enemies attack is either very " +
            "short or non existent'). Set by the enemy's brain.")]
        public float TelegraphLightSeconds;

        [Tooltip("The tell of a light chained on from another (a follow-up comes quicker than an opener).")]
        public float TelegraphChainedSeconds;

        [Tooltip("The tell of the heavy, on top of its own pause at the top.")]
        public float TelegraphHeavySeconds;

        /// <summary>A swing began its tell: the attack and how long it holds (an enemy's: the glint at the blade).</summary>
        public event System.Action<SoulsAttack, float> Telegraphed;

        /// <summary>The swing is held at the top of its wind-up, telling what is coming.</summary>
        public bool IsTelegraphing => _telegraphRemaining > 0.0f;

        private float _telegraphRemaining;
        private bool _telegraphDone;
        private bool _currentChained;

        /// <summary>The tell the swing under way gets, seconds (0 for a player's).</summary>
        private float TelegraphFor()
        {
            if (!_currentIsLight)
            {
                return TelegraphHeavySeconds;
            }
            return _currentChained ? TelegraphChainedSeconds : TelegraphLightSeconds;
        }

        private float _swingYaw;
        private bool _turningIntoSwing;
        private float _lungeDone;

        // --- state ---------------------------------------------------------

        private enum Buffered { None, Light, Heavy }

        private ISwordBlade _sword;
        private bool _equipped;
        private bool _oneHanded;
        private bool _knife;
        private bool _hammer;
        private PlayerVitals _vitals;
        private MeleeAttacker _melee;
        private PlayerAnimationDriver _driver;
        private Animator _animator;
        private CharacterController _controller;

        private SoulsPhase _phase = SoulsPhase.Idle;
        private SoulsAttack _current;
        private float _phaseTime;
        private int _chainIndex = -1;
        private bool _currentIsLight;

        private Buffered _buffered = Buffered.None;
        private float _bufferedYaw;
        private float _bufferRemaining;

        private readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();
        private readonly Collider[] _overlap = new Collider[32];
        private Vector3 _prevBase;
        private Vector3 _prevTip;
        private bool _havePrevBlade;

        private bool _blocking;
        private PlayerLocomotion _locomotion;

        /// <summary>Seconds of hit-stop still to run; the swing's clock and clip are held while it is above zero.</summary>
        private float _hitStopRemaining;

        // --- public surface ------------------------------------------------

        /// <summary>True while a landed hit is holding the swing still.</summary>
        public bool IsInHitStop => _hitStopRemaining > 0.0f;

        public bool IsEquipped => _equipped;

        /// <summary>True with a one-handed sword in hand: the one-handed attacks and guard are in use.</summary>
        public bool IsOneHanded => _oneHanded;

        /// <summary>True with a knife in hand (one-handed too): the knife's attacks, reach, guard and backstab.</summary>
        public bool IsKnife => _knife;

        /// <summary>The light chain for the sword in hand (0-2).</summary>
        public SoulsAttack LightAttack(int index)
        {
            if (_knife)
            {
                return index == 0 ? KnifeLight1 : KnifeLight2;
            }
            if (_oneHanded)
            {
                return index == 0 ? OneHandLight1 : (index == 1 ? OneHandLight2 : OneHandLight3);
            }
            return index == 0 ? Light1 : (index == 1 ? Light2 : (_hammer ? HammerLight3 : Light3));
        }

        /// <summary>How many lights the chain runs through before it starts again: three, ending in the finisher; a knife's two, back and forth.</summary>
        public int LightChainLength => _knife ? 2 : 3;

        /// <summary>The heavy for the sword in hand.</summary>
        public SoulsAttack HeavyAttack => _knife ? KnifeHeavy : (_oneHanded ? OneHandHeavy : Heavy);

        /// <summary>How much faster than its own timings the blade in hand swings: a knife's <see cref="KnifeRules.SwingSpeed"/>, else 1.</summary>
        public float BladeSwingSpeed => _knife ? KnifeRules.SwingSpeed : 1.0f;

        /// <summary>How far ahead of the body a swing catches a target, metres: <see cref="ReachMetres"/>, a knife's a little short of it.</summary>
        public float Reach => ReachMetres * (_knife ? KnifeRules.ReachShare : 1.0f);

        /// <summary>The stamina a blow on the held guard costs, per point of damage: a knife's guard pays more for each.</summary>
        public float GuardStaminaPerDamage => BlockStaminaPerDamage * (_knife ? KnifeRules.GuardStaminaShare : 1.0f);

        /// <summary>Raised for a blow that landed as a backstab (a critical hit), with its target.</summary>
        public event System.Action<IDamageable> Backstabbed;

        public SoulsPhase Phase => _phase;

        /// <summary>True from the first frame of a swing until its recovery ends: another attack or ability cannot start over it.</summary>
        public bool IsCommitted => _phase != SoulsPhase.Idle;

        /// <summary>
        /// THE RECOVERY'S TAIL: past the chain window, the swing has done its hitting and is only settling back into the stance. The body is free
        /// here -- to chain, dash, raise a guard or move at full speed -- while the arm finishes on its own. Reckless Kingdoms of Amalur locked you
        /// into every recovery and its combat read as sluggish for it (Aztez, "The Amalur Problem"); God of War and Bayonetta cancel the tail into
        /// anything, and the swings feel reactive. Before 2026-10-23 only a dash could leave a recovery.
        /// </summary>
        public bool InRecoveryTail => _phase == SoulsPhase.Recovery && _current != null && _phaseTime >= _current.Recovery * _current.ChainAt;

        /// <summary>True while a swing holds the legs to <see cref="CommittedMoveSpeedFraction"/>: from its first frame to its recovery's tail.</summary>
        public bool SlowsMovement => IsCommitted && !InRecoveryTail;

        public bool IsBlocking => _blocking;
        public SoulsAttack CurrentAttack => _current;

        /// <summary>Which light in the chain was last thrown (0-2), or -1 when not mid-chain.</summary>
        public int ChainIndex => _chainIndex;

        /// <summary>Raised with the swing being started.</summary>
        public event System.Action<SoulsAttack> AttackStarted;

        /// <summary>Raised for every target a swing actually damages.</summary>
        public event System.Action<IDamageable> AttackHit;

        /// <summary>Raised when an attack was asked for but there was no stamina to start it.</summary>
        public event System.Action OutOfStamina;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Finds the siblings. Public because EditMode never runs Awake.</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
            _melee = GetComponent<MeleeAttacker>();
            _driver = GetComponent<PlayerAnimationDriver>();
            _controller = GetComponent<CharacterController>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _animator = GetComponentInChildren<Animator>();
        }

        public void Equip(ISwordBlade sword)
        {
            Bind();
            _sword = sword;
            _oneHanded = sword != null && (sword.IsOneHanded || sword.IsKnife);
            _knife = sword != null && sword.IsKnife;
            _hammer = sword != null && sword.IsHammer;
            _equipped = true;
            if (_vitals != null)
            {
                _vitals.StaminaRegenPerSecondOverride = StaminaRegenPerSecond;
                _vitals.GuardBreakStunSeconds = GuardBreakStunSeconds;
                _vitals.BlockStaminaPerDamage = GuardStaminaPerDamage;
                _vitals.Parried -= OnParried;
                _vitals.Parried += OnParried;
            }
        }

        public void Unequip(ISwordBlade sword)
        {
            if (sword != null && !ReferenceEquals(sword, _sword))
            {
                return;
            }
            Interrupt();
            SetBlockHeld(false);
            _equipped = false;
            _sword = null;
            ForgetVerdicts();
            if (_vitals != null)
            {
                _vitals.StaminaRegenPerSecondOverride = 0.0f;
                _vitals.HyperArmour = false;
                _vitals.Parried -= OnParried;
            }
        }

        /// <summary>The guard caught a blow in its first instant: the flourish, announced so other players see the parry.</summary>
        private void OnParried(Vector3 fromPosition)
        {
            _driver?.PlayLocalState(SwordStrikes.ParryName);
        }

        // --- requests (input) ---------------------------------------------

        /// <summary>Asks for a light attack. Remembered for BufferSeconds if the body is busy.</summary>
        public void RequestLight(float yawDegrees)
        {
            Buffer(Buffered.Light, yawDegrees);
        }

        /// <summary>Asks for the heavy attack.</summary>
        public void RequestHeavy(float yawDegrees)
        {
            Buffer(Buffered.Heavy, yawDegrees);
        }

        /// <summary>
        /// HOLDING THE CLICK MAKES IT THE HEAVY (owner, 2026-10-24: "make it so holding click does a heavy guardbreaking attack"). The light
        /// still goes out on the press, so a click is answered at once (waiting for the release to tell a tap from a hold was the punch's
        /// "horrible" lag); a click still held this long becomes the heavy while that light is still winding up. Shorter than every light's
        /// wind-up (the quickest, the thrust's, is about 0.19 s), longer than a click (about 0.1 s).
        /// </summary>
        public const float HeavyHoldSeconds = 0.15f;

        /// <summary>
        /// The click is still held after <see cref="HeavyHoldSeconds"/>: the light it asked for becomes the heavy. A light still remembered
        /// (a press mid-swing) is remembered as the heavy instead; a light still winding up is turned into the heavy from the pose it has
        /// reached (the stamina it cost counts towards the heavy's); a light already live is followed by the heavy as the chain's next.
        /// </summary>
        public void HoldIntoHeavy(float yawDegrees)
        {
            if (!_equipped)
            {
                return;
            }
            if (_buffered == Buffered.Light)
            {
                _buffered = Buffered.Heavy;
                _bufferedYaw = yawDegrees;
                return;
            }
            if (_phase == SoulsPhase.Startup && _currentIsLight && _current != null && _hitStopRemaining <= 0.0f)
            {
                SoulsAttack heavy = HeavyAttack;
                _vitals?.TrySpendCombatStamina(Mathf.Max(0.0f, heavy.StaminaCost - _current.StaminaCost), RegenDelaySeconds);
                StartSwing(heavy, false, yawDegrees);
                return;
            }
            if (_phase != SoulsPhase.Idle && _currentIsLight)
            {
                Buffer(Buffered.Heavy, yawDegrees);
            }
        }

        private void Buffer(Buffered what, float yawDegrees)
        {
            if (!_equipped)
            {
                return;
            }
            _buffered = what;
            _bufferedYaw = yawDegrees;
            // Remembered for BufferSeconds -- or, pressed during a swing, until that swing's chain window has opened (plus half a buffer): a
            // press during the wind-up or the blade's own frames is how a chain is asked for, however long the swing is.
            _bufferRemaining = Mathf.Max(BufferSeconds, SecondsUntilChainWindow() + BufferSeconds * 0.5f);

            // Free to act right now: go this frame instead of waiting for the
            // next Tick, so a click starts the swing on the frame it happened.
            if (_phase == SoulsPhase.Idle && !Locked && _vitals != null && !_vitals.IsStunned && !_vitals.IsDead)
            {
                TryStartBuffered(chained: false);
            }
        }

        /// <summary>Seconds until the swing in progress lets a chained attack start (0 when nothing is in progress), hit-stop included.</summary>
        public float SecondsUntilChainWindow()
        {
            if (_phase == SoulsPhase.Idle || _current == null)
            {
                return 0.0f;
            }
            float window = _current.Recovery * _current.ChainAt;
            float until = _hitStopRemaining + _telegraphRemaining;
            switch (_phase)
            {
                case SoulsPhase.Startup:
                    until += _current.Startup - _phaseTime + _current.Active + window + (_telegraphDone ? 0.0f : TelegraphFor());
                    break;
                case SoulsPhase.Active: until += _current.Active - _phaseTime + window; break;
                default: until += window - _phaseTime; break;
            }
            return Mathf.Max(0.0f, until);
        }

        /// <summary>
        /// Whether a roll may begin now. Idle or Recovery only: startup and
        /// active frames are the commitment.
        /// </summary>
        public bool CanRoll => _equipped && (_phase == SoulsPhase.Idle || _phase == SoulsPhase.Recovery);

        /// <summary>
        /// Starts a roll: cancels whatever recovery is left (the one exit a
        /// committed swing has), drops a held block and forgets anything queued.
        /// The caller pays the stamina and grants the i-frames.
        /// </summary>
        public void BeginRoll()
        {
            if (_phase != SoulsPhase.Idle)
            {
                EndSwing();
            }
            SetBlockHeld(false);
            _buffered = Buffered.None;
            _bufferRemaining = 0.0f;
        }

        /// <summary>Called every frame with whether the block button is down. A guard may come up in a swing's recovery tail: it ends the swing.</summary>
        public void SetBlockHeld(bool held)
        {
            bool canBlock = held && _equipped && (_phase == SoulsPhase.Idle || InRecoveryTail)
                && _vitals != null && !_vitals.IsDead && !_vitals.IsStunned
                && !_vitals.IsExhausted && _vitals.Stamina > 0.0f;

            if (canBlock)
            {
                if (!_blocking)
                {
                    if (_phase != SoulsPhase.Idle)
                    {
                        EndSwing();
                        _buffered = Buffered.None;
                        _bufferRemaining = 0.0f;
                    }
                    _blocking = true;
                    _vitals.BlockDrainsStamina = true;
                    _vitals.BlockStaminaPerDamage = GuardStaminaPerDamage;
                    _vitals.GuardBreakStunSeconds = GuardBreakStunSeconds;
                    // The guard's first instant parries: a blow caught now costs nothing and leaves the attacker open.
                    _vitals.BeginPerfectGuard(PerfectGuardSeconds);
                    PlayBlockPose();
                }
                // Re-raised every frame, so the block lasts exactly as long as
                // the button does rather than timing out after 0.7s.
                _vitals.StartBlock();
                HoldBlockPose();
            }
            else if (_blocking)
            {
                _blocking = false;
                if (_vitals != null)
                {
                    _vitals.EndBlock();
                    _vitals.BlockDrainsStamina = false;
                }
                // Lower the guard now rather than waiting for the clip to run out.
                if (InState(BlockStateName) && _driver != null)
                {
                    _driver.PlayLocalState(IdleStateName);
                }
            }
        }

        private void PlayBlockPose()
        {
            if (_driver != null)
            {
                _driver.PlayLocalState(BlockStateName);
            }
        }

        /// <summary>Keeps the guard up: the posed guard holds itself for as long as it is told to, so this only raises it again if something knocked the body out of it.</summary>
        private void HoldBlockPose()
        {
            if (_driver != null && !InState(BlockStateName))
            {
                PlayBlockPose();
            }
        }

        // --- the state machine --------------------------------------------

        /// <summary>Advances everything by one frame.</summary>
        public void Tick(float deltaTime)
        {
            if (!_equipped)
            {
                return;
            }
            TickVerdicts(deltaTime);

            // HYPER-ARMOUR: through a heavy's wind-up and active frames, blows only stagger this body once its poise is spent (PlayerVitals).
            if (_vitals != null)
            {
                _vitals.HyperArmour = _current != null && _current.HyperArmour && (_phase == SoulsPhase.Startup || _phase == SoulsPhase.Active);
            }

            // A disabled CharacterController is how a ragdoll (or a car seat)
            // takes the body over; a swing must not carry on through that.
            bool bodyTakenOver = _controller != null && !_controller.enabled;
            if (bodyTakenOver || (_vitals != null && (_vitals.IsStunned || _vitals.IsDead)))
            {
                // Hit mid-swing: the swing is gone, and so is anything queued.
                // Being staggered is the price of trading blows with no poise.
                Interrupt();
                SetBlockHeld(false);
                if (_reboundPending)
                {
                    // Bounced off a guard: the blade thrown back, shown on this body and (by name) on its copies; the body knocked back a step (a copy is moved by the network).
                    _reboundPending = false;
                    _driver?.PlayLocalState(SwordStrikes.ReboundName(_oneHanded));
                    _locomotion?.Shove(-transform.forward, ReboundShoveMetres, ReboundShoveSeconds);
                    if (ThirdPersonCamera.Local != null && ThirdPersonCamera.Local.Target == transform)
                    {
                        ThirdPersonCamera.Local.AddShake(ReboundShakeMetres, ReboundShakeSeconds);
                    }
                    Rebounded?.Invoke();
                }
                return;
            }
            _reboundPending = false;

            if (_bufferRemaining > 0.0f)
            {
                _bufferRemaining -= deltaTime;
                if (_bufferRemaining <= 0.0f)
                {
                    _buffered = Buffered.None;
                }
            }

            if (_phase == SoulsPhase.Idle)
            {
                if (_buffered != Buffered.None && !Locked)
                {
                    TryStartBuffered(chained: false);
                }
                return;
            }

            // HIT-STOP: a landed hit holds the whole swing still for a few frames -- the clock here AND
            // the clip (stopped in LandOn) -- then lets both go on together, so they cannot drift apart.
            if (_hitStopRemaining > 0.0f)
            {
                _hitStopRemaining -= deltaTime;
                if (_hitStopRemaining > 0.0f)
                {
                    return;
                }
                ReleaseHitStop();
                return;
            }

            // AN ENEMY'S TELL: the swing held at the top of its wind-up (TelegraphLightSeconds and the rest), the clock and the pose together,
            // as a hit-stop holds them.
            if (_telegraphRemaining > 0.0f)
            {
                _telegraphRemaining -= deltaTime;
                if (_telegraphRemaining > 0.0f)
                {
                    return;
                }
                ReleaseHitStop();
                return;
            }

            _phaseTime += deltaTime * _swingSpeed;

            // A loop, not an if: a long frame can carry the swing across more
            // than one boundary, and none of them may be skipped (a swallowed
            // active window is a swallowed hit).
            for (int guard = 0; guard < 4; guard++)
            {
                if (_phase == SoulsPhase.Startup)
                {
                    TurnIntoSwing(deltaTime * _swingSpeed);
                    ApplyLunge();
                    if (!_telegraphDone && _phaseTime >= _current.HoldAt)
                    {
                        _telegraphDone = true;
                        float tell = TelegraphFor();
                        if (tell > 0.0f)
                        {
                            _telegraphRemaining = tell;
                            HoldClip();
                            Telegraphed?.Invoke(_current, tell);
                            return;
                        }
                    }
                    if (_phaseTime < _current.Startup)
                    {
                        return;
                    }
                    _phaseTime -= _current.Startup;
                    _phase = SoulsPhase.Active;
                    _hitThisSwing.Clear();
                    _havePrevBlade = false;
                    // The blade goes live facing where it was aimed, however far the wind-up had to turn.
                    TurnIntoSwing(float.PositiveInfinity);
                }

                if (_phase == SoulsPhase.Active)
                {
                    ApplyLunge();
                    SweepBlade();
                    if (_phaseTime < _current.Active)
                    {
                        return;
                    }
                    _phaseTime -= _current.Active;
                    _phase = SoulsPhase.Recovery;
                }

                if (_phase == SoulsPhase.Recovery)
                {
                    if (_buffered != Buffered.None && _current.Recovery > 0.0f
                        && _phaseTime >= _current.Recovery * _current.ChainAt)
                    {
                        if (TryStartBuffered(chained: true))
                        {
                            return;
                        }
                    }
                    if (_phaseTime >= _current.Recovery)
                    {
                        EndSwing();
                    }
                    return;
                }
            }
        }

        private bool TryStartBuffered(bool chained)
        {
            Buffered what = _buffered;
            float yaw = _bufferedYaw;
            _buffered = Buffered.None;

            SoulsAttack next;
            bool light;
            if (what == Buffered.Heavy)
            {
                next = HeavyAttack;
                light = false;
            }
            else
            {
                int index = chained && _currentIsLight ? (_chainIndex + 1) % LightChainLength : 0;
                next = LightAttack(index);
                _pendingChainIndex = index;
                light = true;
            }

            if (_vitals != null && !_vitals.TrySpendCombatStamina(next.StaminaCost, RegenDelaySeconds))
            {
                OutOfStamina?.Invoke();
                return false;
            }

            StartSwing(next, light, yaw, chained);
            return true;
        }

        private int _pendingChainIndex;

        /// <summary>How fast the swing under way runs: Melee Speed's, taken when it started.</summary>
        private float _swingSpeed = 1.0f;

        private void StartSwing(SoulsAttack attack, bool light, float yawDegrees, bool chained = false)
        {
            // A held block is dropped the moment you swing.
            SetBlockHeld(false);
            ReleaseHitStop();
            _telegraphRemaining = 0.0f;
            _telegraphDone = false;
            _currentChained = chained;

            // Melee Speed (2026-10-14): the whole swing -- wind-up, cut, recovery, the lunge and the pose -- runs this much faster.
            _swingSpeed = PlayerVitals.SpeedOf(_vitals, StatId.MeleeSpeed) * BladeSwingSpeed;   // a knife swings quicker (KnifeRules)
            _current = attack;
            _currentIsLight = light;
            _chainIndex = light ? _pendingChainIndex : -1;
            _phase = SoulsPhase.Startup;
            _phaseTime = 0.0f;
            _hitThisSwing.Clear();
            _havePrevBlade = false;

            // Swing where the camera is looking, not wherever the body last
            // happened to face -- without this a swing goes off at an angle
            // from the crosshair whenever you stopped turning a moment ago.
            // TURNED INTO through the wind-up (SwingTurnDegreesPerSecond), not
            // snapped: a body that jumped half round on the spot in one frame
            // was one of the "awkward" turns (owner, 2026-10-16). It faces the
            // way by the time the blade is live (made sure of in Tick).
            _swingYaw = yawDegrees;
            _turningIntoSwing = true;
            _lungeDone = 0.0f;
            TurnIntoSwing(0.0f);

            // The attack, by name: a SwordStrikes attack is posed by the driver's SwordPoseDriver (speed 1, from its start); a clip, if one
            // is ever used again, is played as the slice described -- announced to the network the same way either way, so other players
            // see the same swing.
            if (_driver != null)
            {
                _driver.PlayLocalState(attack.Animation, attack.AnimatorSpeed * _swingSpeed, attack.StartSeconds);
                if (attack.IsProcedural && !Mathf.Approximately(_swingSpeed, 1.0f))
                {
                    _driver.SetStateSpeed(attack.Animation, attack.AnimatorSpeed * _swingSpeed);   // the pose driver's own clock (sent on to other players)
                }
            }
            AttackStarted?.Invoke(attack);
        }

        /// <summary>Lets the swing's clip run again after a hit-stop (a no-op when none is running).</summary>
        private void ReleaseHitStop()
        {
            if (_hitStopRemaining <= 0.0f && !_clipHeld)
            {
                return;
            }
            _hitStopRemaining = 0.0f;
            if (_clipHeld && _driver != null && _current != null)
            {
                _driver.SetStateSpeed(_current.Animation, _current.AnimatorSpeed * _swingSpeed);
            }
            _clipHeld = false;
        }

        /// <summary>True while the animation clip is stopped for a hit-stop, so it is put back exactly once.</summary>
        private bool _clipHeld;

        private void EndSwing()
        {
            ReleaseHitStop();
            _telegraphRemaining = 0.0f;
            _telegraphDone = false;
            _phase = SoulsPhase.Idle;
            _phaseTime = 0.0f;
            _turningIntoSwing = false;
            _chainIndex = -1;
            _currentIsLight = false;
            if (_vitals != null)
            {
                _vitals.HyperArmour = false;
            }

            // The swing's clip is cut off at ClipEnd, well before the clip's
            // own exit, so blend out by hand -- and put the animator speed
            // back to 1 (this same call carries speed 1 to other players).
            if (_driver != null)
            {
                _driver.PlayLocalState(IdleStateName);
            }
        }

        /// <summary>Drops the swing in progress and anything queued (a hit, death, unequip).</summary>
        public void Interrupt()
        {
            if (_phase != SoulsPhase.Idle)
            {
                EndSwing();
            }
            _buffered = Buffered.None;
            _bufferRemaining = 0.0f;
        }

        // --- hit detection --------------------------------------------------

        private void SweepBlade()
        {
            Vector3 bladeBase;
            Vector3 bladeTip;
            if (_sword != null)
            {
                bladeBase = _sword.BladeBaseWorld;
                bladeTip = _sword.BladeTipWorld;
            }
            else
            {
                // No sword object (a test, or one destroyed mid-swing): a blade
                // held out in front at chest height, so the rules still work.
                float height = 1.4f;
                bladeBase = transform.position + Vector3.up * height + transform.forward * 0.4f;
                bladeTip = bladeBase + transform.forward * 1.8f;
            }

            if (!_havePrevBlade)
            {
                _prevBase = bladeBase;
                _prevTip = bladeTip;
                _havePrevBlade = true;
            }

            int samples = Mathf.Max(1, SweepSamples);
            for (int i = 1; i <= samples; i++)
            {
                float f = i / (float)samples;
                Vector3 b = Vector3.Lerp(_prevBase, bladeBase, f);
                Vector3 t = Vector3.Lerp(_prevTip, bladeTip, f);
                int count = Physics.OverlapCapsuleNonAlloc(
                    b, t, BladeRadius, _overlap, TargetLayers, QueryTriggerInteraction.Ignore);
                LandOnOverlaps(count, t);
            }

            BreakGlassWithBlade(_prevBase, _prevTip, bladeBase, bladeTip);

            _prevBase = bladeBase;
            _prevTip = bladeTip;

            // The reach volume waits until the blade has come round. From the first live frame it landed the blow with the blade still pointing behind the body (the live
            // window opens a quarter into the strike, and the strike's whip has hardly moved it by then), and the hit-stop froze it there.
            float reach = Reach;
            if (reach > 0.0f && _current != null && _current.Startup + _phaseTime >= _current.ContactAt)
            {
                float height = _locomotion != null ? _locomotion.StandingHeight : 1.8f;
                Vector3 center = transform.position
                    + Vector3.up * (height * 0.5f)
                    + transform.forward * (reach * 0.5f + 0.15f);
                var halfExtents = new Vector3(ReachHalfWidth, height * 0.5f, reach * 0.5f);
                int count = Physics.OverlapBoxNonAlloc(
                    center, halfExtents, _overlap, transform.rotation, TargetLayers, QueryTriggerInteraction.Ignore);
                LandOnOverlaps(count, transform.position + Vector3.up * (height * 0.6f));
            }
        }

        /// <summary>
        /// A blade through a window takes the pane out at once -- a HEAVY blow, unlike a fist, which only
        /// cracks glass (the melee-versus-thrown-brick difference in The Last of Us Part II's windows).
        /// Three points along the blade are each traced from where they were last frame to where they are
        /// now, so a fast swing cannot pass through a pane between two frames; only the active frames call
        /// this, so a wind-up brushing a wall breaks nothing.
        /// </summary>
        private void BreakGlassWithBlade(Vector3 prevBase, Vector3 prevTip, Vector3 nowBase, Vector3 nowTip)
        {
            for (int i = 1; i <= 3; i++)
            {
                float along = i / 3.0f;
                Vector3 from = Vector3.Lerp(prevBase, prevTip, along);
                Vector3 travel = Vector3.Lerp(nowBase, nowTip, along) - from;
                float length = travel.magnitude;
                if (length < 0.02f)
                {
                    continue;
                }
                Vector3 direction = travel / length;
                if (Physics.Raycast(from, direction, out RaycastHit hit, length, TargetLayers, QueryTriggerInteraction.Ignore))
                {
                    var breakable = hit.collider.GetComponentInParent<World.IBreakableSurface>();
                    breakable?.TakeBlow(hit.point, direction, World.BlowStrength.Heavy);
                }
            }
        }

        /// <summary>Where this body's blows come from, for what stands in their way (BlowPath).</summary>
        private Vector3 Chest => transform.position + Vector3.up * ((_locomotion != null ? _locomotion.StandingHeight : 1.8f) * 0.6f);

        private void LandOnOverlaps(int count, Vector3 contactFrom)
        {
            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlap[i];
                if (collider == null)
                {
                    continue;
                }

                // Never our own body (or its ragdoll) or our own sword.
                if (collider.transform.IsChildOf(transform))
                {
                    continue;
                }
                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || (!target.IsAlive && !Dismemberer.TakesCorpseHits(target)) || _hitThisSwing.Contains(target))
                {
                    continue;
                }
                if (ReferenceEquals(target, _vitals))
                {
                    continue;
                }

                Vector3 point = collider.ClosestPoint(contactFrom);
                if (BlowPath.IsWalled(Chest, point, transform))
                {
                    continue;   // in the blade's sweep with a wall between: not struck, and still to be struck if the blade comes round the wall to it
                }
                _hitThisSwing.Add(target);
                LandOn(target, point);
            }
        }

        private void LandOn(IDamageable target, Vector3 point)
        {
            int strength = _vitals != null ? _vitals.Strength : 0;
            float baseDamage = _sword != null ? _sword.GetMeleeDamage() : CombatTuning.PunchDamage;
            float damage = CombatTuning.DamageFor(baseDamage * _current.DamageScale, strength) * (_melee != null ? _melee.BloodrageFactor : 1.0f);

            // THE BACKSTAB (Backstab): a one-handed blade's blow from behind a living body is a critical hit -- far more damage (a knife's the
            // most), and a stagger even from a knife's light.
            float hitstun = _current.HitstunAt(_swingSpeed);
            bool backstab = _oneHanded && target.IsAlive && target is Component body && body != null
                && Backstab.IsBehind(body.transform.position, body.transform.forward, transform.position);
            if (backstab)
            {
                damage *= Backstab.MultiplierFor(_knife);
                hitstun = Mathf.Max(hitstun, Backstab.StaggerSeconds);
            }

            // The blow's poise damage, told to the target just before it lands (a copy of another player forwards it with the hit).
            var victim = target as PlayerVitals;
            victim?.TakePoise(_current.PoiseDamage);

            HitResponse response;
            bool edged = _sword is IEquippableWeapon blade && blade.IsEdged;
            using (DamageContext.GuardBreaking(_current.BreaksGuard))
            using (DamageContext.Cutting(edged))
            using (DamageContext.Critically(backstab))
            using (DamageContext.LandingHeavily(_current.LandsHeavily))
            {
                response = _melee != null
                    ? _melee.RegisterExternalHit(target, point, damage, hitstun)
                    : DamageContext.Strike(target, damage, transform.position, hitstun, DamageKind.Physical,
                        ArmourRules.Combine(_vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f, ArmourRules.PierceOf(_sword)), point, gameObject);
            }

            if (response == HitResponse.Ignored)
            {
                return;
            }

            AttackHit?.Invoke(target);
            if (backstab && response == HitResponse.Landed)
            {
                Backstabbed?.Invoke(target);
            }
            BeginHitStop(backstab ? Backstab.HitStopMultiplier : 1.0f);

            if (response == HitResponse.Blocked && _vitals != null)
            {
                // PARRIED (or the classic timed block): the blade thrown back and you are the one left open, the longest.
                Rebound(BlockedRecoilSeconds);
            }
            else if (response == HitResponse.Guarded && _vitals != null)
            {
                // SOAKED BY A HELD GUARD: the defender pays stamina for it, and the blade still bounces -- you are open, a little less long.
                Rebound(GuardReboundSeconds);
            }
            else if (response == HitResponse.Landed && victim != null && victim.Relay is NetworkedDamage relay && relay.LastRelayWasSent)
            {
                // Another player's copy: "landed" is only what THIS machine saw. Their own body decides (a parry, a roll), and says so.
                WatchVerdict(relay, relay.LastRelayedHitId);
            }
        }

        // ---- verdicts from other players' bodies (PvP) ---------------------------------------------------------------------------------------

        private readonly List<(NetworkedDamage relay, int hitId, float expires)> _pendingVerdicts = new List<(NetworkedDamage, int, float)>();
        private readonly HashSet<NetworkedDamage> _watchedRelays = new HashSet<NetworkedDamage>();
        private float _verdictClock;

        /// <summary>Hits on other players' copies still waiting for the owner's word (for tests).</summary>
        public int PendingVerdicts => _pendingVerdicts.Count;

        private void WatchVerdict(NetworkedDamage relay, int hitId)
        {
            if (_watchedRelays.Add(relay))
            {
                relay.Verdict += OnRelayVerdict;
            }
            _pendingVerdicts.Add((relay, hitId, _verdictClock + VerdictTimeoutSeconds));
        }

        private void OnRelayVerdict(int hitId, HitResponse response)
        {
            for (int i = 0; i < _pendingVerdicts.Count; i++)
            {
                if (_pendingVerdicts[i].hitId == hitId)
                {
                    _pendingVerdicts.RemoveAt(i);
                    ApplyRemoteVerdict(response);
                    return;
                }
            }
        }

        /// <summary>
        /// What the other player's own body did with a swing this machine saw land: PARRIED (Blocked) or soaked by a held guard (Guarded)
        /// throws this attacker's blade back exactly as it would offline (a parry for longer); rolled through (Ignored) changes nothing here --
        /// the hit-stop already played, and the damage was never this machine's to decide. Public so a test can hand one in without a network.
        /// </summary>
        public void ApplyRemoteVerdict(HitResponse response)
        {
            if (_vitals == null)
            {
                return;
            }
            if (response == HitResponse.Blocked)
            {
                Rebound(BlockedRecoilSeconds);
            }
            else if (response == HitResponse.Guarded)
            {
                Rebound(GuardReboundSeconds);
            }
        }

        /// <summary>The blade came back off a guard: stunned for <paramref name="seconds"/>, the rebound shown as the swing ends (next Tick).</summary>
        private void Rebound(float seconds)
        {
            _vitals.Stun(seconds);
            _reboundPending = true;
        }

        private void TickVerdicts(float deltaTime)
        {
            _verdictClock += deltaTime;
            for (int i = _pendingVerdicts.Count - 1; i >= 0; i--)
            {
                if (_pendingVerdicts[i].expires <= _verdictClock || _pendingVerdicts[i].relay == null)
                {
                    _pendingVerdicts.RemoveAt(i);
                }
            }
        }

        private void ForgetVerdicts()
        {
            foreach (NetworkedDamage relay in _watchedRelays)
            {
                if (relay != null)
                {
                    relay.Verdict -= OnRelayVerdict;
                }
            }
            _watchedRelays.Clear();
            _pendingVerdicts.Clear();
        }

        private void BeginHitStop(float multiplier = 1.0f)
        {
            if (HitStopSeconds <= 0.0f || _current == null)
            {
                return;
            }
            float seconds = HitStopSeconds * (_currentIsLight && !_current.LandsHeavily ? 1.0f : HeavyHitStopMultiplier) * multiplier;
            if (seconds <= _hitStopRemaining)
            {
                return;   // several targets in one frame: the longest stop wins, they do not add up
            }
            _hitStopRemaining = seconds;
            HoldClip();
        }

        /// <summary>Stops the swing's clip (or posed strike) where it is, for a hit-stop or a tell; ReleaseHitStop lets it go on.</summary>
        private void HoldClip()
        {
            if (_driver != null && !_clipHeld && _current != null)
            {
                _driver.SetStateSpeed(_current.Animation, 0.0f);
                _clipHeld = true;
            }
        }

        /// <summary>
        /// THE STEP: from halfway through the startup to the end of the live window, eased in and out (SwordStrikes.StepShare, which the
        /// attack's clip plants its feet by), so the body surges into the cut rather than gliding at a steady pace from the click. Moved by
        /// what is still owed, so a long frame cannot carry it further than <see cref="SoulsAttack.LungeMetres"/>.
        /// </summary>
        private void ApplyLunge()
        {
            if (_controller == null || _current == null || _current.LungeMetres <= 0.0f)
            {
                return;
            }
            float window = _current.Startup + _current.Active;
            if (window <= 0.0f)
            {
                return;
            }
            float elapsed = _phase == SoulsPhase.Startup ? _phaseTime : _current.Startup + _phaseTime;
            float wanted = SwordStrikes.StepShare(_current.Startup, window, elapsed) * _current.LungeMetres;
            float step = wanted - _lungeDone;
            if (step > 0.0f)
            {
                _controller.Move(transform.forward * step);
                _lungeDone = wanted;
            }
        }

        /// <summary>
        /// Moves the aim of a swing still winding up to <paramref name="yawDegrees"/>: an enemy's brain, which turns its body itself, keeps the swing
        /// aimed where the body faces (its wind-up follows the target slowly; the blade goes where the body faces when it goes live).
        /// </summary>
        public void AimSwing(float yawDegrees)
        {
            if (_phase == SoulsPhase.Startup)
            {
                _swingYaw = yawDegrees;
            }
        }

        /// <summary>Turns the body toward the swing's aim by at most <paramref name="deltaTime"/> worth of <see cref="SwingTurnDegreesPerSecond"/> (infinity: all the way).</summary>
        private void TurnIntoSwing(float deltaTime)
        {
            if (!_turningIntoSwing)
            {
                return;
            }
            Quaternion aim = Quaternion.Euler(0.0f, _swingYaw, 0.0f);
            float left = Quaternion.Angle(transform.rotation, aim);
            if (left <= 2.0f || float.IsPositiveInfinity(deltaTime) || left <= SwingTurnDegreesPerSecond * deltaTime)
            {
                transform.rotation = aim;
                _turningIntoSwing = false;
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, aim, SwingTurnDegreesPerSecond * deltaTime);
        }

        // --- animation ---------------------------------------------------------

        private bool InState(string stateName)
        {
            return _driver != null && _driver.IsPlaying(stateName);
        }

        private void OnDisable()
        {
            Interrupt();
            SetBlockHeld(false);
            ForgetVerdicts();
            if (_vitals != null)
            {
                _vitals.HyperArmour = false;
            }
        }

        // --- live tuning (/sword) -------------------------------------------

        /// <summary>Every tunable, one per line, for the console.</summary>
        public string Describe()
        {
            var lines = new List<string>();
            foreach (FieldInfo field in typeof(SoulsSwordCombat).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(float) || field.FieldType == typeof(int))
                {
                    lines.Add($"{field.Name} = {field.GetValue(this)}");
                }
                else if (field.FieldType == typeof(SoulsAttack))
                {
                    var attack = (SoulsAttack)field.GetValue(this);
                    lines.Add($"{field.Name}: startup {attack.Startup} active {attack.Active} recovery {attack.Recovery} " +
                              $"chainAt {attack.ChainAt} cost {attack.StaminaCost} damage {attack.DamageScale} " +
                              $"hitstun {attack.HitstunSeconds} lunge {attack.LungeMetres} poise {attack.PoiseDamage}{(attack.HyperArmour ? " hyper-armour" : "")}");
                }
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Sets a tunable by path: "BufferSeconds", or "Heavy.Startup" for a
        /// field of an attack. Case-insensitive.
        /// </summary>
        public bool TrySet(string path, float value, out string message)
        {
            message = null;
            if (string.IsNullOrEmpty(path))
            {
                message = "no name given";
                return false;
            }

            string[] parts = path.Split('.');
            object owner = this;
            FieldInfo field = FindField(owner.GetType(), parts[0]);
            if (field == null)
            {
                message = $"no tunable '{parts[0]}' -- /sword lists them";
                return false;
            }

            if (parts.Length == 2 && field.FieldType == typeof(SoulsAttack))
            {
                owner = field.GetValue(this);
                field = FindField(typeof(SoulsAttack), parts[1]);
                if (field == null)
                {
                    message = $"an attack has no '{parts[1]}'";
                    return false;
                }
            }
            else if (parts.Length != 1)
            {
                message = "use Name or Attack.Field";
                return false;
            }

            if (field.FieldType == typeof(float))
            {
                field.SetValue(owner, value);
            }
            else if (field.FieldType == typeof(int))
            {
                field.SetValue(owner, Mathf.RoundToInt(value));
            }
            else
            {
                message = $"'{field.Name}' is not a number";
                return false;
            }

            // Re-apply anything the vitals were told at equip time.
            if (_equipped && _vitals != null)
            {
                _vitals.StaminaRegenPerSecondOverride = StaminaRegenPerSecond;
                _vitals.GuardBreakStunSeconds = GuardBreakStunSeconds;
                _vitals.BlockStaminaPerDamage = GuardStaminaPerDamage;
            }
            message = $"{path} = {value.ToString(CultureInfo.InvariantCulture)}";
            return true;
        }

        private static FieldInfo FindField(System.Type type, string name)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(field.Name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return field;
                }
            }
            return null;
        }
    }
}
