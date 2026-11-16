using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A two-handed sword. No Godot equivalent to faithfully port -- the
    /// explicit ask was to make sword combat BETTER than
    /// weapons/two_handed_sword.gd's own, not to reproduce it, so this is
    /// new-built on the same footing as Jetpack.cs rather than translated
    /// line for line. See ComboChain.Sword's own doc comment for the actual
    /// mechanical improvement (a real multi-stage chain through
    /// MeleeAttacker's delta-driven timing, not Godot's own hand-rolled
    /// _sword_swing_in_progress flag and its twice-rediscovered stuck-flag
    /// bug).
    ///
    /// NO DURABILITY, NO THROW -- unlike the bottle, a sword is not
    /// consumed by use and was never described as throwable. CanThrow is
    /// false and Throw is a no-op that PlayerInputReader's own throw
    /// dispatch (gated on CanThrow first) will never actually reach.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class SwordWeapon : MonoBehaviour, IEquippableItem, IEquippableWeapon, ISwordBlade, ICosmeticMountable
    {
        /// <summary>
        /// Godot has no separate sword damage constant at all -- player.gd's
        /// own _punch() runs sword swings through the exact same PUNCH_DAMAGE
        /// (10.0) every bare-fisted jab uses, then applies the chain's own
        /// damage_scale (1.0 for both sword_attack and sword_attack2 -- see
        /// ComboChain.Sword's own doc comment) on top, same as fists. An
        /// earlier version of this file invented its own 20.0 base here,
        /// never checked against player.gd -- confirmed wrong by reading it
        /// directly.
        /// </summary>
        public const float BaseMeleeDamage = CombatTuning.PunchDamage;

        /// <summary>
        /// Slower than the bottle's 0.5s -- a two-handed swing has real
        /// weight to it. Used for every sword swing (chain stages and the
        /// heavy variant alike); there is no separate weapon-heavy-duration
        /// concept in MeleeAttacker today, matching how HeavySwingSeconds
        /// itself only ever applied to the unarmed haymaker.
        /// </summary>
        public const float MeleeCooldownSecondsConst = 0.6f;

        public const float InteractRange = 2.5f;
        public const float LookDotThreshold = 0.82f; // cos(35 degrees)

        /// <summary>
        /// A two-handed blade's length in the sword root's own space, from the guard at the root up its local +Y to the tip: every greatsword
        /// model is placed in this frame (SpikeSwordBuilder scales its model to it) so they share one tuned grip.
        /// </summary>
        public const float BladeLocalLength = 2.1f;

        /// <summary>Which inventory item this blade is; each weapon's builder sets its own.</summary>
        [SerializeField] private string _itemId = ItemRegistry.SpikeSword;

        public string ItemId { get => _itemId; set => _itemId = value; }

        /// <summary>IEquippableWeapon: a sword cuts; the bat and the crowbar, swung the same way, are blunt.</summary>
        public bool IsEdged => ItemRegistry.HasEdge(_itemId);

        /// <summary>
        /// A ONE-HANDED sword (the owner's combat sword, 2026-10-13): held across the palm the way a hand grips a handle, the other hand free,
        /// and fought with the one-handed attacks and guard (SwordStrikes). False is the two-handed greatsword hold with the left hand on the grip.
        /// </summary>
        [SerializeField] private bool _oneHanded;

        /// <summary>The blade's length from the guard to the tip in the sword root's own space: what a swing sweeps. The greatsword's by default.</summary>
        [SerializeField] private float _bladeLength = BladeLocalLength;

        public bool OneHanded { get => _oneHanded; set => _oneHanded = value; }

        /// <summary>ISwordBlade: the combat picks its moveset by this.</summary>
        public bool IsOneHanded => _oneHanded;

        /// <summary>
        /// A KNIFE (the kitchen knife, docs/KNIFE.md): one-handed, held and swung as the combat sword is, but quicker and lighter, a little
        /// shorter in reach, its lights staggering no one, its guard thin and its backstab the biggest (KnifeRules, Backstab).
        /// </summary>
        [SerializeField] private bool _knife;

        public bool Knife { get => _knife; set => _knife = value; }

        public bool IsKnife => _knife;

        /// <summary>A HAMMER (the sledgehammer): the two-handed moves, but its third light smashes straight down overhead (SwordStrikeKind.Smash2H).</summary>
        [SerializeField] private bool _hammer;

        public bool Hammer { get => _hammer; set => _hammer = value; }

        public bool IsHammer => _hammer;

        /// <summary>A DULL knife (the butter knife): the knife's moves, but its blows land as <see cref="KnifeRules.DullDamage"/>.</summary>
        [SerializeField] private bool _dull;

        public bool Dull { get => _dull; set => _dull = value; }

        public float BladeLength { get => _bladeLength; set => _bladeLength = Mathf.Max(0.1f, value); }

        /// <summary>Where the blade meets the grip, in world space.</summary>
        public Vector3 BladeBaseWorld => transform.position;

        /// <summary>The blade's tip, in world space -- what a swing actually sweeps.</summary>
        public Vector3 BladeTipWorld => transform.TransformPoint(new Vector3(0.0f, _bladeLength, 0.0f));

        /// <summary>
        /// The cutting edge is on local +X instead of -X: true for the owner's combat sword, whose model is thin on +X (2 to 7 mm) and thick on
        /// -X (24 to 27 mm) the whole length of the blade (measured 2026-10-17). Every pose leads with the edge and the default hold turns it to
        /// the knuckles, so a sword marked the wrong way round swings with its spine and twists the hand half a turn.
        /// </summary>
        [SerializeField] private bool _edgeOnPlusX;

        public bool EdgeOnPlusX { get => _edgeOnPlusX; set => _edgeOnPlusX = value; }

        /// <summary>
        /// The side the cutting edge faces in the sword root's own space: -X by default (the spike sword's spikes); +X where
        /// <see cref="EdgeOnPlusX"/> says so.
        /// </summary>
        public Vector3 EdgeLocal => _edgeOnPlusX ? Vector3.right : Vector3.left;

        public Vector3 EdgeDirectionWorld => transform.TransformDirection(EdgeLocal);

        // ---- THE HOLD ----
        //
        // FROM THE HAND'S OWN SHAPE, as the one-handed hold below is (owner, 2026-10-28: "he doesnt even grip the sword properly, use spikesword
        // as guide"). The old default was derived from the 2hsw mocap's hands, which ran the handle ALONG the fingers (+X of the hand bone), so
        // the fist closed on nothing and both hands sat up on the guard. Now the handle lies ACROSS the palm with the blade out past the thumb
        // and the edge along the knuckles, the right fist just under the guard and the left fist a hand's breadth and more below it, reached onto
        // the handle by IK and turned onto it (PlayerLocomotion.SetSupportHand): the two-handed hammer grip.
        //
        // TUNED BY HAND in the Unity editor when it needs to be: see WeaponGripSettings and the Inspector on
        // this component. A saved hold replaces the derived one below.

        /// <summary>The blade's direction (hilt to tip) in the right-hand bone's own frame: out past the thumb.</summary>
        public static Vector3 DefaultBladeAxisInHand => OneHandBladeAxisInHand;

        /// <summary>How far down the handle from the guard the right (upper) fist's middle sits, in the sword's own space.</summary>
        public const float DefaultPrimaryGripDown = 0.10f;

        /// <summary>How far down the handle from the guard the left (lower) fist's middle sits: 20 cm below the right.</summary>
        public const float DefaultSupportGripDown = 0.30f;

        public static Quaternion DefaultGripRotation => DefaultOneHandGripRotation;

        /// <summary>The sword root's position in the right-hand bone's frame: the handle through the fist, the guard the primary grip above it.</summary>
        public static Vector3 DefaultGripPosition => FistPoint + DefaultBladeAxisInHand * DefaultPrimaryGripDown;

        /// <summary>Where the left palm goes, in the sword's own space (the blade runs along +Y from the guard).</summary>
        public static Vector3 DefaultSupportPoint => new Vector3(0.0f, -DefaultSupportGripDown, 0.0f);

        /// <summary>The sword's position in the right hand: the saved hold if there is one, else the derived one.</summary>
        public static Vector3 GripPosition =>
            WeaponGripSettings.Current.SwordTuned ? WeaponGripSettings.Current.SwordPosition : DefaultGripPosition;

        public static Quaternion GripRotation =>
            WeaponGripSettings.Current.SwordTuned ? Quaternion.Euler(WeaponGripSettings.Current.SwordEuler) : DefaultGripRotation;

        /// <summary>Where the left palm holds, in the sword's space.</summary>
        public static Vector3 SupportPoint =>
            WeaponGripSettings.Current.SwordTuned ? WeaponGripSettings.Current.SwordLeftHand : DefaultSupportPoint;

        // ---- THE ONE-HANDED HOLD ----
        //
        // DERIVED FROM THE HAND'S OWN SHAPE (FusedBodyShape.AddHand), not from a clip: in the right-hand bone's frame +X runs down the arm to the
        // fingertips, -Y is the palm's side (toward the body with the arm hanging) and +Z is the thumb's. A gripped handle lies across the palm,
        // so the blade runs straight out past the thumb (+Z), with the edge in line with the knuckles (+X), and the guard sits just beyond the
        // palm's surface. Tuned by hand the same way as the two-handed hold (WeaponGripSettings.OneHand*).
        //
        // STRAIGHT OUT, as the owner set it by hand (2026-10-17: their saved hold runs the blade 3 degrees back toward the wrist and 4 toward
        // the palm from straight out; the default takes the first, the hold has no tilt toward the palm); it was tilted 15 degrees toward the
        // fingers. The one-handed sword's carry clips (SwordCarryArm) point the blade forward and down from it, and with the old tilt the
        // point dragged on the ground.

        /// <summary>The blade's direction (hilt to tip) in the right-hand bone's frame for a one-handed hold: straight out past the thumb, as the owner holds it.</summary>
        public static readonly Vector3 OneHandBladeAxisInHand = new Vector3(-0.056f, 0.0f, 0.998f).normalized;

        /// <summary>How far past the palm's surface the handle's axis lies: half the palm's thickness and the handle's own radius.</summary>
        public const float OneHandHandleBelowPalm = FusedBodyShape.PalmHalfThickness + 0.03f;

        /// <summary>How far the guard sits from the palm's centre along the blade: the hand just below the guard.</summary>
        public const float OneHandGripDown = 0.09f;

        /// <summary>The sword's rotation in the right hand for a one-handed hold: its +Y along <see cref="OneHandBladeAxisInHand"/>, its edge (-X) toward the knuckles.</summary>
        public static Quaternion DefaultOneHandGripRotation => Quaternion.LookRotation(Vector3.up, OneHandBladeAxisInHand);

        /// <summary>The sword root's position in the right-hand bone's frame for a one-handed hold.</summary>
        public static Vector3 DefaultOneHandGripPosition =>
            new Vector3(PlayerLocomotion.PalmOffsetMetres, -OneHandHandleBelowPalm, 0.0f) + OneHandBladeAxisInHand * OneHandGripDown;

        public static Vector3 OneHandGripPosition =>
            WeaponGripSettings.Current.OneHandTuned ? WeaponGripSettings.Current.OneHandPosition : DefaultOneHandGripPosition;

        public static Quaternion OneHandGripRotation =>
            WeaponGripSettings.Current.OneHandTuned ? Quaternion.Euler(WeaponGripSettings.Current.OneHandEuler) : DefaultOneHandGripRotation;

        /// <summary>The default one-handed hold for a sword whose edge is on +X or -X: the edge toward the knuckles either way (turned half round the blade for +X).</summary>
        public static Quaternion DefaultOneHandGripRotationFor(bool edgeOnPlusX) =>
            edgeOnPlusX ? Quaternion.AngleAxis(180.0f, OneHandBladeAxisInHand) * DefaultOneHandGripRotation : DefaultOneHandGripRotation;

        /// <summary>This sword's position in the right hand: the hold for its kind (saved or derived).</summary>
        public Vector3 HoldPosition => _ownHold ? _ownHoldPosition : _oneHanded ? OneHandGripPosition : GripPosition;

        public Quaternion HoldRotation => _ownHold ? Quaternion.Euler(_ownHoldEuler)
            : _oneHanded
            ? (WeaponGripSettings.Current.OneHandTuned ? SavedOneHandHoldFor(_edgeOnPlusX) : DefaultOneHandGripRotationFor(_edgeOnPlusX))
            : GripRotation;

        /// <summary>
        /// A hold of this weapon's own, set by its builder, in place of the shared one-handed or two-handed hold: the kitchen knife's, which the
        /// owner placed by hand in the editor (2026-11-15) when it was made bigger.
        /// </summary>
        [SerializeField] private bool _ownHold;

        [SerializeField] private Vector3 _ownHoldPosition;

        [SerializeField] private Vector3 _ownHoldEuler;

        public bool HasOwnHold => _ownHold;

        /// <summary>Gives this weapon its own hold in the right hand: where it sits in the hand bone's space, and how it is turned (degrees).</summary>
        public void SetOwnHold(Vector3 position, Vector3 euler)
        {
            _ownHold = true;
            _ownHoldPosition = position;
            _ownHoldEuler = euler;
        }

        /// <summary>The edge of the sword the saved one-handed hold was set on: the combat sword's, +X (the owner's hold, 2026-10-17).</summary>
        public const bool SavedOneHandHoldEdgeOnPlusX = true;

        /// <summary>
        /// The saved one-handed hold for a sword whose edge is on +X or -X: as saved for the combat sword's side, turned half round the handle for
        /// the other, so the edge sits where the hold put the combat sword's. Not turned, the metal crowbar (edge on -X) sat in the hand edge to the
        /// thumb, and every one-handed pose turned the hand half round to lead with it: after a swing the arm went up over the shoulder (owner,
        /// 2026-10-19: "after you swing the crowbar it leave it in this weird arm position").
        /// </summary>
        public static Quaternion SavedOneHandHoldFor(bool edgeOnPlusX)
        {
            Quaternion saved = OneHandGripRotation;
            return edgeOnPlusX == SavedOneHandHoldEdgeOnPlusX ? saved : Quaternion.AngleAxis(180.0f, saved * Vector3.up) * saved;
        }

        /// <summary>
        /// The marker the left hand reaches for, a child of the sword. Select it while the game runs and
        /// drag it in the Scene view: the left hand follows. Null while the sword is not held.
        /// </summary>
        public Transform LeftHandGrip => _leftHandGrip;

        private Transform _leftHandGrip;

        // ---- THE THRUST GRIP (the stab dash, the sword spin) ----
        //
        // A sword thrust straight out ahead (the stab dash's lunge, animated in Blender) is held with the handle in the fist where the one-handed
        // hold has it, turned within the fist so the blade comes out between the thumb and the index finger leaning toward the fingertips: the
        // fingers round the handle, the blade carrying on ahead of the arm. Owner, 2026-10-18: "make him grip the sword properly too as well as
        // pointing it horizontally". Held straight out past the thumb (the one-handed hold), the blade stood off the arm and pointed at the street;
        // held as the two-handed sword is, the one-handed sword's long handle ran back along the forearm. SourceArt/StabDash/stab_build_all.py
        // poses the move with this same grip, and SourceArt/SwordSpin/spin_build_all.py the spin (owner, 2026-10-18: the spin's blade was
        // "angled downward" with the combat sword, held in its own grip on a move posed for the two-handed one).

        /// <summary>How far the thrust grip leans the blade from straight out past the thumb toward the fingertips, degrees, about the palm's normal.</summary>
        public const float ThrustTiltDegrees = 50.0f;

        /// <summary>Where the fist holds a handle, in the right-hand bone's frame: the palm's middle along the hand, the handle's radius under the palm.</summary>
        public static Vector3 FistPoint => new Vector3(PlayerLocomotion.PalmOffsetMetres, -OneHandHandleBelowPalm, 0.0f);

        /// <summary>The blade's direction (hilt to tip) in the right-hand bone's frame in the thrust grip.</summary>
        public static Vector3 ThrustBladeAxisInHand => Quaternion.AngleAxis(ThrustTiltDegrees, Vector3.up) * OneHandBladeAxisInHand;

        /// <summary>This sword's rotation in the right hand in the thrust grip: the one-handed hold's (edge toward the knuckles), leaned.</summary>
        public Quaternion ThrustGripRotation => Quaternion.AngleAxis(ThrustTiltDegrees, Vector3.up) * DefaultOneHandGripRotationFor(_edgeOnPlusX);

        /// <summary>How far up the handle from the fist this sword's guard sits: its one-handed hold's own (tuned) reach, or the two-handed upper grip's.</summary>
        private float ThrustGripReach => _oneHanded
            ? Mathf.Max(OneHandGripDown, Vector3.Dot(HoldPosition - FistPoint, HoldRotation * Vector3.up))
            : DefaultPrimaryGripDown;

        /// <summary>Holds the sword in the thrust grip, <paramref name="weight"/> of the way from its own hold (0: its own).</summary>
        public void HoldForThrust(float weight)
        {
            _flipDegrees = 0.0f;
            transform.localPosition = Vector3.Lerp(HoldPosition, FistPoint + ThrustBladeAxisInHand * ThrustGripReach, weight);
            transform.localRotation = Quaternion.Slerp(HoldRotation, ThrustGripRotation, weight);
        }

        // ---- THE MOCAP GRIP (the Octaslash) ----
        //
        // The two-handed hold the 2hsw mocap gave, which was the default until 2026-10-28: the blade axis the line from the lower hand to the
        // upper in the right hand's frame, the handle along the fingers. The Octaslash was posed in Blender against it (SourceArt/Octaslash), so
        // it keeps it while it holds the body; nothing else uses it.

        public static readonly Vector3 MocapBladeAxisInHand = new Vector3(0.84f, -0.25f, 0.48f).normalized;

        public const float MocapRollDegrees = 270.0f;

        public static Quaternion BakedTwoHandGripRotation =>
            Quaternion.FromToRotation(Vector3.up, MocapBladeAxisInHand) * Quaternion.AngleAxis(MocapRollDegrees, Vector3.up);

        public static Vector3 BakedTwoHandGripPosition =>
            new Vector3(0.949f, 0.141f, -0.283f).normalized * PlayerLocomotion.PalmOffsetMetres + MocapBladeAxisInHand * 0.12f;

        /// <summary>Holds the sword in the mocap grip a Blender move was posed with, <paramref name="weight"/> of the way from its own hold.</summary>
        public void HoldForMocap(float weight)
        {
            _flipDegrees = 0.0f;
            transform.localPosition = Vector3.Lerp(HoldPosition, BakedTwoHandGripPosition, weight);
            transform.localRotation = Quaternion.Slerp(HoldRotation, BakedTwoHandGripRotation, weight);
        }

        // ---- THE REVERSE GRIP (the knife's heavy, the icepick stab) ----
        //
        // Owner, 2026-11-15: a downward stab "holding the knife upside down", and "make the flip to upside down quick and natural". The knife
        // spins half a turn in the fist, OVER THE KNUCKLES (the blade passes out in front of the fingers, never through the hand or the
        // wrist), about the middle of the handle in the fist: the fist keeps hold of the same stretch of handle, and the blade, out past the
        // thumb in the hold, comes out past the little finger. Done this way rather than by an animated hand, as games swap a held thing's grip
        // (a knife spun into a reverse grip as it is drawn): the hand is posed as it would be anyway and the spin happens inside it, quick.

        private float _flipDegrees;

        /// <summary>The axis the reverse grip spins the knife about, in the hand's frame: across the blade and its edge (the palm's normal, near enough).</summary>
        private Vector3 FlipAxisInHand
        {
            get
            {
                Quaternion hold = HoldRotation;
                Vector3 axis = Vector3.Cross(hold * Vector3.up, hold * EdgeLocal);
                return axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.up;
            }
        }

        /// <summary>
        /// The hold's turn in the fist right now (the reverse grip's spin, in the hand's frame): the sword's rotation in the hand is this times
        /// its own hold. The pose takes it off to turn the hand for the hold the strike was written for (PlayerLocomotion.ApplySwordPose).
        /// </summary>
        public Quaternion GripFlipInHand => Quaternion.AngleAxis(_flipDegrees, FlipAxisInHand);

        /// <summary>
        /// Holds the sword <paramref name="weight"/> of the way through its spin into the reverse grip (1: upside down in the fist, 0: its own
        /// hold). Past 1 it spins on a little and settles back (an overshoot ease is given here as it is).
        /// </summary>
        public void HoldReversed(float weight)
        {
            _flipDegrees = 180.0f * weight;
            Vector3 hold = HoldPosition;
            Vector3 blade = HoldRotation * Vector3.up;
            Vector3 pivot = hold + blade * Vector3.Dot(FistPoint - hold, blade);   // the handle's middle in the fist
            Quaternion flip = GripFlipInHand;
            transform.localPosition = pivot + flip * (hold - pivot);
            transform.localRotation = flip * HoldRotation;
        }

        /// <summary>Puts the sword and the left-hand marker where the current hold (saved or derived) says.</summary>
        public void ApplyGrip()
        {
            _flipDegrees = 0.0f;
            transform.localPosition = HoldPosition;
            transform.localRotation = HoldRotation;
            if (_leftHandGrip != null)
            {
                _leftHandGrip.localPosition = SupportPoint;
            }
        }

        private bool _equipped;
        private SphereCollider _pickupTrigger;
        private readonly GroundPickup _pickup = new GroundPickup();
        private Animator _animator;

        private void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
        }

        private void Update()
        {
            if (!_equipped && _pickup.Player != null)
            {
                TryPickup(_pickup.Player, InteractKey.Tapped);
            }
        }

        private void OnTriggerEnter(Collider other) => _pickup.Entered(other);

        private void OnTriggerExit(Collider other) => _pickup.Left(other);

        /// <summary>
        /// See PistolWeapon.TryPickup's own comment -- same split, same
        /// reason: Keyboard.current is null outside a real Input System
        /// device, so a test drives this with an explicit flag instead.
        /// </summary>
        public bool TryPickup(Transform player, bool ePressed)
        {
            return ePressed && InteractionReach.CanReach(transform.position, player, InteractRange, LookDotThreshold, out _)
                && GroundPickup.Take(player, gameObject, _itemId);
        }

        public float GetMeleeDamage() => _knife ? (_dull ? KnifeRules.DullDamage : KnifeRules.BaseDamage)
            : _oneHanded && IsEdged ? CombatTuning.OneHandedSwordDamage : BaseMeleeDamage;

        public float MeleeCooldownSeconds => MeleeCooldownSecondsConst;

        public string MovesetId => ComboChain.Sword;

        public ComboStage? HeavyStage => CombatTuning.SwordHeavyAttack;

        /// <summary>Nothing to track between hits -- a sword doesn't degrade with use.</summary>
        public void RegisterLandedMeleeHit()
        {
        }

        public bool CanThrow() => false;

        public float ThrowSpeed => 0.0f;

        /// <summary>Never actually reached -- CanThrow is false, and PlayerInputReader checks that first.</summary>
        public void Throw(GameObject player, Vector3 origin, Vector3 direction)
        {
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            _animator = player.GetComponentInChildren<Animator>();
            Transform hand = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
            }

            // The left hand reaches for a marker on the sword, so the marker can be dragged in the editor
            // while the game runs and the hand follows (see WeaponGripSettings). A one-handed sword leaves it free.
            if (_leftHandGrip == null && !_oneHanded)
            {
                _leftHandGrip = GripMarker.Create(transform, "LeftHandGrip", "left hand", new Color(1.0f, 0.55f, 0.1f));
            }
            ApplyGrip();

            var locomotion = player.GetComponent<PlayerLocomotion>();
            if (locomotion != null && !_oneHanded)
            {
                // The left hand holds the handle too, wherever the swing takes the sword.
                locomotion.SetSupportHand(_leftHandGrip, Vector3.zero);
            }

            _equipped = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }

            var melee = player.GetComponent<MeleeAttacker>();
            if (melee != null)
            {
                melee.EquippedWeapon = this;
            }

            // THE SWORD FIGHTS THE SOULSLIKE WAY (SoulsSwordCombat): committed
            // attacks with startup/active/recovery, a stamina cost, buffered
            // chaining and a swept blade hitbox. Added on equip rather than
            // baked into Player.prefab so nothing else about the body changes.
            var souls = player.GetComponent<SoulsSwordCombat>();
            if (souls == null)
            {
                souls = player.AddComponent<SoulsSwordCombat>();
            }
            souls.Equip(this);
        }

        public void OnUnequip(GameObject player)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            if (locomotion != null)
            {
                locomotion.ClearSupportHand(_leftHandGrip);
            }
            var melee = player.GetComponent<MeleeAttacker>();
            if (melee != null && ReferenceEquals(melee.EquippedWeapon, this))
            {
                melee.EquippedWeapon = null;
            }
            var souls = player.GetComponent<SoulsSwordCombat>();
            if (souls != null)
            {
                souls.Unequip(this);
            }
            Destroy(gameObject);
        }

        // ---- on another player's copy: the model in the right hand and the free hand on the grip, nothing else ----
        public void MountCosmetic(GameObject body)
        {
            Animator animator = body.GetComponentInChildren<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
            }
            if (_leftHandGrip == null && !_oneHanded)
            {
                _leftHandGrip = GripMarker.Create(transform, "LeftHandGrip", "left hand", new Color(1.0f, 0.55f, 0.1f));
            }
            ApplyGrip();
            if (!_oneHanded)
            {
                body.GetComponent<PlayerLocomotion>()?.SetSupportHand(_leftHandGrip, Vector3.zero);
            }
        }

        public void UnmountCosmetic(GameObject body)
        {
            body.GetComponent<PlayerLocomotion>()?.ClearSupportHand(_leftHandGrip);
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
    }
}
