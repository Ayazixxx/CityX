using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// Turns the keyboard into a <see cref="LocomotionInput"/> and hands it to
    /// the body.
    ///
    /// Separated from PlayerLocomotion so that the only thing that reads a
    /// device is this one small component. A networked player replaces it with
    /// one that reads a replicated input instead, and nothing about how the
    /// body moves has to change.
    ///
    /// Uses the Input System package directly rather than an action asset --
    /// this project sets activeInputHandler to the new system only, so the old
    /// UnityEngine.Input calls would compile and then silently do nothing.
    /// </summary>
    [RequireComponent(typeof(PlayerLocomotion))]
    public sealed partial class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("Whose yaw movement is relative to. Falls back to the main camera.")]
        public Transform YawSource;

        /// <summary>
        /// Set by a gun's own OnEquip/OnUnequip (see PistolWeapon), the same
        /// pattern GrappleSwing.EquippedHook and MeleeAttacker.EquippedWeapon
        /// already use. Typed as the interface rather than a concrete class
        /// so this assembly (CityX.Character) never has to reference
        /// CityX.Weapons -- see IEquippableGun's own doc comment.
        /// </summary>
        public IEquippableGun EquippedGun;

        private GunFeel _gunFeel;

        /// <summary>
        /// Set by Flamethrower's own OnEquip/OnUnequip, same pattern as
        /// EquippedGun. While non-null, left-click takes over entirely --
        /// see TickCombat's own flamethrower branch and
        /// IEquippableFlamethrower's own doc comment for why this is not
        /// EquippedGun itself.
        /// </summary>
        public IEquippableFlamethrower EquippedFlamethrower;

        /// <summary>
        /// Set by a staff's OnEquip/OnUnequip (docs/STAFFS.md), as EquippedFlamethrower is. While non-null, left-click is the staff's: held it
        /// charges or keeps shooting, let go it shoots; see TickCombat's staff branch.
        /// </summary>
        public IEquippableStaff EquippedStaff;

        /// <summary>A ranged weapon is in hand (a gun, the flamethrower or a staff): aimed with the crosshair, never locked on.</summary>
        public bool HoldsRangedWeapon => EquippedGun != null || EquippedFlamethrower != null || EquippedStaff != null;

        /// <summary>
        /// Set by the spray can's own OnEquip/OnUnequip (2026-10-15), same pattern as EquippedGun. While non-null, a right-click asks for the
        /// graffiti canvas (<see cref="SprayRequested"/>) and the fists stand down.
        /// </summary>
        public ISprayCan EquippedSprayCan;

        /// <summary>Right-click with a spray can in hand: the UI (GraffitiMenuView) finds the wall in front and opens the canvas on it.</summary>
        public static event System.Action<PlayerInputReader> SprayRequested;

        /// <summary>
        /// Set by Jetpack's own OnEquip/OnUnequip, same pattern as
        /// EquippedGun. While non-null, Space thrusts instead of charging a
        /// jump -- see the Update() dispatch and IFlightGear's own doc comment.
        /// </summary>
        public IFlightGear EquippedFlightGear;

        /// <summary>
        /// Set by Parachute's own OnEquip/OnUnequip, same pattern as
        /// EquippedFlightGear. While non-null, a fresh press of Space while
        /// airborne toggles it open/closed instead of doing nothing -- see
        /// the Update() dispatch and IParachute's own doc comment. Shares
        /// Jetpack's own "back" equip slot (see ItemRegistry.Parachute's
        /// own doc comment), so Inventory's own slot eviction already
        /// guarantees this and EquippedFlightGear are never both non-null at
        /// once -- no extra mutual-exclusion needed here.
        /// </summary>
        public IParachute EquippedParachute;

        /// <summary>
        /// Edge-detects a jetpack wall hit (see the crash-damage block in
        /// Update()) so a player held against a wall under thrust takes
        /// ONE impact, not the same damage re-applied every single frame
        /// they stay pinned there. Reset to false the instant CollisionFlags.
        /// Sides stops appearing, so backing off and hitting something else
        /// later is a fresh crash.
        /// </summary>
        private bool _wasJetpackCrashing;

        /// <summary>
        /// The wall normal _wasJetpackCrashing's own gate was last armed
        /// against -- see NewWallContactAngleDegrees' own doc comment for
        /// why a plain bool isn't enough on its own.
        /// </summary>
        private Vector3 _lastJetpackCrashNormal;

        /// <summary>
        /// How much a contact's own normal has to rotate from
        /// _lastJetpackCrashNormal before this counts as hitting a
        /// DIFFERENT wall rather than continuing to press into the same
        /// one -- Bug report: "i bugged out when running into the
        /// corner of the building with jetpack and got stuck into it
        /// instead ragdolling." A plain "was I touching A wall last frame"
        /// bool cannot tell a genuinely new impact apart from continuing to
        /// touch the SAME wall: flying at an angle into a building corner
        /// typically grazes one face first (a shallow, low-damage or
        /// zero-damage touch that still sets HitWallThisFrame, arming
        /// _wasJetpackCrashing), then immediately slams into the SECOND,
        /// near-perpendicular face a frame or two later -- a real, hard,
        /// damage-worthy impact that the edge-detection gate silently
        /// swallowed because it still believed the player was mid-way
        /// through the SAME wall-touch as the earlier graze. With the real
        /// crash knockdown never firing, the player never gets
        /// Ragdoll.Activate's own DepenetrateFromWorld correction either
        /// (see its own doc comment -- that is what actually clears a body
        /// stuck overlapping geometry), leaving them visibly wedged in the
        /// corner with no automatic fix and no ragdoll. 90 degrees is a
        /// square building corner's own angle; comfortably lower catches
        /// off-square architecture too, while staying comfortably above
        /// the near-zero jitter a static flat wall's own reported normal
        /// has frame to frame.
        /// </summary>
        public const float NewWallContactAngleDegrees = 45.0f;

        /// <summary>
        /// Whether this frame's own wall touch is a fresh impact worth
        /// checking for crash damage -- see NewWallContactAngleDegrees' own
        /// doc comment for the building-corner bug this exists to fix.
        /// A PURE, STATIC decision extracted specifically so a test can
        /// drive the exact geometric cases (continuing to touch the same
        /// wall, a genuine corner's second face, the very first touch)
        /// directly, without needing a real keyboard, jetpack, or physics
        /// scene just to reach a few lines of vector math.
        /// </summary>
        public static bool IsNewWallContact(
            bool hitWall, bool wasJetpackCrashing, Vector3 currentWallNormal, Vector3 lastCrashWallNormal)
        {
            return hitWall && (!wasJetpackCrashing ||
                Vector3.Angle(currentWallNormal, lastCrashWallNormal) > NewWallContactAngleDegrees);
        }

        /// <summary>
        /// Bug report: "the crosshair doesnt seem to allign with
        /// where the bullet goes... maybe that was just for the fireball
        /// spell." It was: the fireball launches from a point on the
        /// player's own CHEST, but its own direction used to be
        /// aimSource.forward alone -- parallel to the camera's own sightline,
        /// not aimed AT it. Since the camera sits well behind and to the
        /// side of the chest (ThirdPersonCamera's own Distance/
        /// ShoulderOffset), two parallel rays with different origins never
        /// actually coincide at any finite distance -- the fireball's real
        /// path only ever approached the crosshair's own line as it
        /// receded toward infinity, never actually crossing it up close,
        /// which is exactly the kind of miss a player notices most.
        ///
        /// SAME FIX AS PlayerLocomotion.ApplyGunAimPose's own convergence
        /// point (GunAimConvergenceDistance ahead of the camera) --
        /// reused here rather than a second, independently-chosen distance,
        /// so every "aim at the crosshair from somewhere else on the body"
        /// case in this project agrees on what "the crosshair" even means.
        /// </summary>
        private static Vector3 ConvergedDirection(Vector3 origin, Transform aimSource) =>
            ConvergedDirection(origin, aimSource.position, aimSource.forward);

        /// <summary>From <paramref name="origin"/> toward where the crosshair is, the camera at <paramref name="eye"/> looking along <paramref name="look"/>.</summary>
        public static Vector3 ConvergedDirection(Vector3 origin, Vector3 eye, Vector3 look)
        {
            Vector3 aimPoint = eye + look * PlayerLocomotion.GunAimConvergenceDistance;
            Vector3 toAimPoint = aimPoint - origin;
            return toAimPoint.sqrMagnitude > 0.0001f ? toAimPoint.normalized : look;
        }

        /// <summary>
        /// Which way a thrown thing (a grenade, a bottle) leaves the hand: where the camera looks. It was the camera's back (ported from
        /// Godot, whose forward is -Z), so a grenade flew behind the thrower (owner, 2026-10-29).
        /// </summary>
        public static Vector3 ThrowDirection(Transform camera) => camera.forward;

        /// <summary>How far the crosshair is looked along for what it rests on (past it, the line runs to this point).</summary>
        public const float CrosshairReach = 300.0f;

        private static readonly RaycastHit[] CrosshairHits = new RaycastHit[32];

        /// <summary>
        /// From <paramref name="origin"/> (a staff's head) toward what the crosshair is ON: the first thing the camera's line meets past the
        /// body, not <paramref name="self"/>'s own. Aiming at a point a fixed distance ahead instead hit beside anything nearer or farther, the
        /// camera sitting off to the side of the hand (owner, 2026-10-29: "the beam staff was hitting slightly to the side of the crosshair").
        /// </summary>
        public static Vector3 CrosshairDirection(Vector3 origin, Vector3 eye, Vector3 look, Transform self)
        {
            look = look.sqrMagnitude > 1e-6f ? look.normalized : Vector3.forward;
            Vector3 toAimPoint = CrosshairPoint(origin, eye, look, self) - origin;
            return toAimPoint.sqrMagnitude > 0.0001f ? toAimPoint.normalized : look;
        }

        /// <summary>
        /// What the crosshair is ON: the first point the camera's line meets past the body (as far along it as <paramref name="origin"/>), not
        /// <paramref name="self"/>'s own; meeting nothing, the line's far end (<see cref="CrosshairReach"/>).
        /// </summary>
        public static Vector3 CrosshairPoint(Vector3 origin, Vector3 eye, Vector3 look, Transform self)
        {
            look = look.sqrMagnitude > 1e-6f ? look.normalized : Vector3.forward;
            float pastBody = Mathf.Max(0.0f, Vector3.Dot(origin - eye, look));
            Vector3 aimPoint = eye + look * CrosshairReach;
            float nearest = CrosshairReach;
            int count = Physics.RaycastNonAlloc(eye, look, CrosshairHits, CrosshairReach, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = CrosshairHits[i];
                if (hit.distance < pastBody || hit.distance >= nearest || (self != null && hit.transform.IsChildOf(self)))
                {
                    continue;
                }
                nearest = hit.distance;
                aimPoint = hit.point;
            }
            return aimPoint;
        }

        /// <summary>
        /// Set inside TickCombat's own gun branch, read back here in Update()
        /// once the LocomotionInput for this same frame is built -- TickCombat
        /// runs first and returns before movement is ever touched, so there is
        /// no other way for "is the gun's right-click held" to reach the
        /// animator without threading a whole extra parameter through.
        /// </summary>
        private bool _isAimingGun;

        /// <summary>Right-click's aim is held with a thrown weapon that has its own aim (IAimedThrow).</summary>
        private bool _throwAiming;
        private float _nextWeaponSwitch;

        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private MeleeAttacker _melee;
        private Ragdoll _ragdoll;
        private PlayerAudio _audio;
        private GrabCarry _grabCarry;
        private Inventory _inventory;
        private GrappleSwing _grappleSwing;
        private PlayerClimbing _climbing;
        private PlayerVaulting _vaulting;
        private PlayerLadder _ladder;
        private WallStick _wallStick;

        /// <summary>
        /// What this body has learnt and what is on its bar (owner, 2026-10-14). The number keys cast whatever is in their slot, and Dash, Climb,
        /// Wall Stick, Recovery and Super Jump work only once learnt; vaulting always does. A body with no character knows what the game had
        /// before the skill tree: everything but Super Jump and Recovery (PlayerSkills).
        /// </summary>
        private PlayerSkills _skills;

        /// <summary>Whether the player is asking to grab a wall (Space held a moment): see GrabIntent.</summary>
        private readonly GrabIntent _grabIntent = new GrabIntent();
        private ThirdPersonCamera _orbitCamera;
        private readonly WeaponAimInput _weaponAim = new WeaponAimInput();

        /// <summary>
        /// Exposed so Jetpack.OnEquip/OnUnequip can widen MinPitch/MaxPitch
        /// for full-vertical flight without each doing its own scene-wide
        /// Object.FindFirstObjectByType search for the one camera this
        /// class already found -- see Jetpack.cs's own doc comment on why
        /// jetpacking needs a wider pitch range than ordinary looking-around.
        /// </summary>
        public ThirdPersonCamera OrbitCamera => _orbitCamera;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            // All optional. A body with no vitals, no fists and no ragdoll
            // still walks -- useful for tests and for anything that is not the
            // player.
            _vitals = GetComponent<PlayerVitals>();
            _melee = GetComponent<MeleeAttacker>();
            if (_melee != null)
            {
                _melee.SwingStarted += OnMeleeSwingStarted;
            }
            _ragdoll = GetComponent<Ragdoll>();
            _audio = GetComponent<PlayerAudio>();
            _grabCarry = GetComponent<GrabCarry>();
            _inventory = GetComponent<Inventory>();
            _grappleSwing = GetComponent<GrappleSwing>();
            _climbing = GetComponent<PlayerClimbing>();
            _vaulting = GetComponent<PlayerVaulting>();
            _ladder = GetComponent<PlayerLadder>();
            _wallStick = GetComponent<WallStick>();
            _skills = PlayerSkills.On(gameObject);
            _fireballCaster = GetComponent<IFireballCaster>();
            _iceShards = GetComponent<IIceShardCaster>();
            _arcaneOrb = GetComponent<IArcaneOrbCaster>();
            _arcaneBarrier = GetComponent<IArcaneBarrierCaster>();
            _poisonBreath = GetComponent<IPoisonBreathCaster>();
            _swordWave = GetComponent<ISwordWaveCaster>();
            _acidRain = GetComponent<IAcidRainCaster>();
            _selfSmite = GetComponent<SelfSmiteAbility>();
            _bodySize = GetComponent<BodySizeAbility>();
            _stabDash = GetComponent<StabDashAbility>();
            _glacierSpikes = GetComponent<GlacierSpikesAbility>();

            // The player gets wet in the rain and dries under cover. Added here
            // rather than baked into the prefab: only the real, local player has
            // an input reader, and this needed no prefab rebuild.
            if (GetComponent<PlayerWetness>() == null)
            {
                gameObject.AddComponent<PlayerWetness>();
            }

            // The sparks and the ting of a blow stopped outright by a block (Block Chance), here and on other players' copies of this body.
            if (GetComponent<BlockDeflectEffect>() == null)
            {
                gameObject.AddComponent<BlockDeflectEffect>();
            }
            // What a blow that lands does to this body: the freeze, the shove, the flash, the blood, the thud, the camera's jolt.
            if (GetComponent<HitImpactEffects>() == null)
            {
                gameObject.AddComponent<HitImpactEffects>();
            }
            // Dying drops the weapon in hand for anyone to pick up, and E picks up what lies in reach (DroppedItems).
            if (GetComponent<DropsWeaponOnDeath>() == null)
            {
                gameObject.AddComponent<DropsWeaponOnDeath>();
            }
            if (GetComponent<DroppedItemPicker>() == null)
            {
                gameObject.AddComponent<DroppedItemPicker>();
            }
            // E opens the drawer, cupboard or chest looked at and shows what is inside (docs/LOOTING.md).
            if (GetComponent<LootSearcher>() == null)
            {
                gameObject.AddComponent<LootSearcher>();
            }
            // F opens the item looked at in its appraisal card beside it, the camera gliding in (docs/ITEM_INSPECT.md).
            if (GetComponent<ItemInspector>() == null)
            {
                gameObject.AddComponent<ItemInspector>();
            }

            // A stagger cuts short what was winding up (2026-10-14: the Stability stat makes it rarer).
            if (_vitals != null)
            {
                _vitals.Staggered += OnStaggered;
            }
        }

        /// <summary>
        /// STAGGERED (2026-10-14): a blow that put the body in hitstun cuts short whatever was winding up -- the poison scream before the gas
        /// leaves, the rest of an ice volley, Octaslash's wind-up, a charging sword spin. A blow the Stability stat shrugs off never gets here.
        /// </summary>
        private void OnStaggered()
        {
            _poisonBreath?.Interrupt();
            _iceShards?.Interrupt();
            if (_octaslash == null)
            {
                _octaslash = GetComponent<OctaslashAbility>();
            }
            _octaslash?.Interrupt();
            _stabDash?.Interrupt();
            _glacierSpikes?.Interrupt();
            _melee?.InterruptSpinCharge();
        }

        /// <summary>
        /// THE BODY IS OUT OF ITS OWNER'S HANDS (thrown limp, dead, staggered): what a held click was doing is let go of. Nothing that reads
        /// the click is reached while that lasts, so a bow half drawn stayed drawn and loosed its arrow by itself the moment control came
        /// back, and a staff's jet, a charge or a flame went on with nobody holding it.
        /// </summary>
        private void LetGoOfTheTrigger(float deltaTime)
        {
            if (EquippedGun is IDrawnWeapon drawn)
            {
                drawn.LetDown();
            }
            EquippedStaff?.CancelCharge(gameObject);
            EquippedFlamethrower?.Tick(false, transform.position, transform.forward, gameObject, deltaTime);
        }

        private void OnDestroy()
        {
            LetGoOfEveryHolder();
            if (_vitals != null)
            {
                _vitals.Staggered -= OnStaggered;
            }
        }

        /// <summary>
        /// A LOOKUP, NOT A GetComponent -- the camera lives on its own
        /// GameObject, not this one, matching UniqueNpc's own reasoning for
        /// finding it in Start() rather than Awake(): Awake ordering between
        /// separate objects is not guaranteed, and Start runs only after
        /// every object's own Awake has already run.
        /// </summary>
        private void Start()
        {
            _orbitCamera = Object.FindFirstObjectByType<ThirdPersonCamera>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // THE CAR OWNS THE WHOLE FRAME WHILE SEATED, matching player.gd's
            // own _seated_car early-return -- it's been reparented onto one
            // of the car's own seat nodes (see Vehicles/Car.cs's own
            // _complete_enter), so there is nothing for this class's own
            // movement/combat/dance handling to do until it's unparented
            // again. Placed before every other early-return below (ragdoll,
            // dead, climbing, grapple...) since none of those states can
            // coexist with being seated. Vehicles/Car.cs reads WASD/Space
            // itself, directly, entirely independently of this class -- the
            // same split car.gd's own Input.get_axis reads already have
            // from player.gd's own input handling.
            //
            // A BENCH OWNS THE FRAME THE SAME WAY, with no WASD reading of
            // its own -- BenchSeat.Update() polls E/WASD only to decide when
            // to stand back up, not to move anything, so there is genuinely
            // nothing for this class to do while seated, same as the car.
            if (_seatedCar != null || _seatedBench != null)
            {
                return;
            }

            if ((_isDancing || _isBreakdancing) && !IsHeld && RequestsDanceCancel(keyboard))
            {
                _isDancing = false;
                _isBreakdancing = false;
            }

            float deltaTime = Time.deltaTime;
            if (_vitals != null)
            {
                _vitals.TickHitstun(deltaTime);
                _vitals.TickMana(deltaTime);
                _vitals.TickHealthRegen(deltaTime);
            }
            if (_melee != null)
            {
                _melee.Tick(deltaTime);
            }
            SoulsSword?.Tick(deltaTime);
            _fireballCaster?.Tick(deltaTime);
            _iceShards?.Tick(deltaTime);
            _arcaneOrb?.Tick(deltaTime);
            _arcaneBarrier?.Tick(deltaTime);
            _poisonBreath?.Tick(deltaTime);
            _swordWave?.Tick(deltaTime);

            // DEATH IS POLLED HERE, NOT EVENT-DRIVEN -- see GrabCarry's own
            // doc comment for why an OnEnable subscription to
            // PlayerVitals.Died is unsafe in this codebase's own
            // scene-builder field-assignment order. Checked BEFORE every
            // early-return below (ragdoll/dead/stunned all lead into
            // ordinary falling, which knows nothing about a rope) so a
            // player who dies mid-swing is never left visibly tethered --
            // matches player.gd's own _die() calling release_grapple(false)
            // for exactly the same reason.
            if (_grappleSwing != null && _grappleSwing.IsActive &&
                _vitals != null && _vitals.IsDead)
            {
                _grappleSwing.Release(false);
            }

            // /PUSHUPS HOLDS THE BODY until it is standing again (PushUpEmote): a movement key, a spell key or a click calls it off and it gets
            // up. Mouse look still turns the camera (the camera reads the mouse, not this). A blow, death or a fall ends it at once, standing.
            if (IsDoingPushUps)
            {
                bool knockedOut = (_vitals != null && (_vitals.IsDead || _vitals.IsStunned)) || (_ragdoll != null && _ragdoll.IsLimp);
                if (knockedOut)
                {
                    _pushUps.Abort();
                }
                else
                {
                    if (!IsHeld)
                    {
                        TickPushUps(keyboard, deltaTime);
                    }
                    return;
                }
            }

            if (_ragdoll != null && _ragdoll.IsLimp)
            {
                // Physics owns the body now. Ticking locomotion here would call
                // Move on a CharacterController the ragdoll has just disabled,
                // which does nothing useful and complains about it every frame.
                // The footsteps have to be stopped by hand on every one of
                // these early returns: they are loops, so they do not run out
                // on their own, and a corpse walking on the spot is audible.
                _audio?.SilenceFootsteps();
                LetGoOfTheTrigger(deltaTime);

                // SAME REASONING, FOR THE JETPACK -- Bug report:
                // "when your using jetpack and crash into a surface and
                // ragdoll, the jetpack also turns off even if your holding
                // space." EquippedFlightGear.Tick (the only place that stops
                // its own flame/thrust audio) is never reached again while
                // this branch keeps returning early below, so holding
                // Space through the crash would otherwise leave the flame
                // burning and the thrust loop playing over a body that
                // is not flying anywhere any more.
                EquippedFlightGear?.StopThrustEffects();

                // Bug report: "im still getting up at an angle when
                // i ragdoll." Jetpack.Tick is the only thing that ever eases
                // the body's own flight lean back toward upright, and it
                // never runs while this branch keeps returning early -- so
                // whatever lean was baked in at the exact instant of the
                // crash used to sit there completely frozen through the
                // whole limp ragdoll AND the entire get-up clip,
                // several frames longer than it took the player to actually
                // regain control. See ResetTilt's own doc comment for the
                // full reasoning; called every frame while limp (same as
                // StopThrustEffects just above) rather than once, so it also
                // catches a jetpack that gets equipped mid-ragdoll.
                EquippedFlightGear?.ResetTilt();
                bool roped = _grappleSwing != null && _grappleSwing.IsRoped;   // Space lets go of the rope, so it flips nobody up the same frame
                if (roped && !IsHeld)
                {
                    TickRopedSwing(keyboard, deltaTime);   // the rope's controls while the body hangs limp from it (GrappleSwing.Rope.cs)
                }

                // A DEATH RAGDOLL NEVER TICKS ANY OF THIS -- IsDead is
                // checked here directly rather than relying on the ordinary
                // IsDead early-return further down, which this branch's own
                // `return` never reaches. Without this guard a dead body
                // would still accumulate rest time and let Space stand it
                // back up, which only makes sense for a knockdown, not a
                // death -- death stays permanent until an explicit Revive.
                bool dead = _vitals != null && _vitals.IsDead;
                if (!dead)
                {
                    // Drives the rest-timer/GettingUp recovery -- see
                    // Ragdoll.Tick's own doc comment for why this has to be
                    // called explicitly from here rather than Ragdoll
                    // running its own Update().
                    _ragdoll.Tick(deltaTime);

                    // Requested: "the ragdoll can continue to take fall
                    // damage." The torso's own Rigidbody is what is
                    // actually falling now, not the (disabled)
                    // CharacterController -- IsRestingOnGround is the
                    // closest thing it has to isGrounded while limp (see
                    // its own doc comment).
                    //
                    // RETURN VALUE NOW CHECKED -- Bug report: "no
                    // second impact noise once i crash and then hit the
                    // ground and take fall damage a second time." A wall
                    // crash's own impact plays through the jetpackThrusting
                    // branch further down, but that branch is never reached
                    // again once ragdolled -- this call was already dealing
                    // the real, second round of damage for a genuine second
                    // impact (an initial wall crash knocking the body into a
                    // ragdoll that then actually falls and lands for real),
                    // it just silently discarded the return value instead of
                    // playing anything, matching the exact same
                    // "only a landing that actually hurt" rule the ordinary
                    // grounded landing case already follows below.
                    if (_vitals != null && _ragdoll.Torso != null)
                    {
                        // Flipping back up (Recovery), the body is watched from its feet, as it will be once the walker has it.
                        float watchedY = _ragdoll.IsRecovering ? transform.position.y : _ragdoll.Torso.position.y;
                        float ragdollFallDamage = _vitals.TickFallDamage(_ragdoll.IsRestingOnGround, watchedY);
                        if (ragdollFallDamage > 0.0f)
                        {
                            _audio?.PlayFallImpact();
                        }
                    }

                    // SPACE GETS A BODY THAT IS DOWN BACK UP: with Recovery learnt, at once, from anywhere, with a flip (Saints Row IV's Ninja
                    // Reflexes, docs/RECOVERY.md); without it, once it has lain still a while, with the get-up.
                    if (!IsHeld && !roped && keyboard.spaceKey.wasPressedThisFrame)
                    {
                        if (!(_skills.Knows(SkillCatalog.Recovery) && _ragdoll.Recover(transform.forward)) && _ragdoll.CanStandUp)
                        {
                            _ragdoll.BeginStandingUp();
                        }
                    }
                }
                return;
            }

            if (_vitals != null && _vitals.IsDead)
            {
                // A corpse with no ragdoll still falls, so keep ticking the
                // body with no input rather than stopping it dead in the air.
                _locomotion.Tick(default, deltaTime);
                _audio?.SilenceFootsteps();
                LetGoOfTheTrigger(deltaTime);
                return;
            }

            // Being hit takes the controls away for a moment. Gravity still
            // applies, so a stunned body in mid-air keeps falling instead of
            // hanging there.
            if (_vitals != null && _vitals.IsStunned)
            {
                _locomotion.Tick(default, deltaTime);
                _audio?.SilenceFootsteps();
                LetGoOfTheTrigger(deltaTime);
                return;
            }

            // SELF SMITE HOLDS THE BODY, as Pikachu's Thunder does (SelfSmiteAbility): from the cast until it frees the caster nothing else is
            // read, and the body is ticked with no movement (it is rooted), so gravity and the Animator carry on. Mouse look still turns the camera.
            if (_selfSmite != null && _selfSmite.HoldsTheBody)
            {
                _locomotion.Tick(new LocomotionInput { YawDegrees = transform.eulerAngles.y }, deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // ARCANE BARRIER HOLDS THE BODY while its key is held (ArcaneBarrierCaster, docs/ARCANE_BARRIER.md): the caster stands still behind
            // the wall doing nothing else, as the owner asked to start with; let go, the wall fades and the body is free. Mouse look still turns
            // the camera.
            if (_arcaneBarrier != null && _arcaneBarrier.IsUp)
            {
                _arcaneBarrier.Hold(!IsHeld && _skills.Held(SkillCatalog.ArcaneBarrier, keyboard));
                _locomotion.Tick(new LocomotionInput { YawDegrees = transform.eulerAngles.y }, deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // THE STAB DASH HOLDS THE BODY too (StabDashAbility), from the key to the end of its recovery, as Smash's Power Thrust commits: it
            // moves the body itself, and until the lunge leaves it keeps turning to where the camera looks.
            if (_stabDash != null && _stabDash.HoldsTheBody)
            {
                Transform looking = YawSource != null ? YawSource : (Camera.main != null ? Camera.main.transform : null);
                if (_stabDash.IsWindingUp && looking != null)
                {
                    _stabDash.Aim(looking.forward);
                }
                _locomotion.Tick(new LocomotionInput { YawDegrees = transform.eulerAngles.y }, deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // FROZEN FOR THE ONE-SHOT MANTLE CLIP'S OWN DURATION, checked
            // first of all -- matches player.gd's own _is_climb_topping_out
            // early-return being the very first thing _physics_process
            // checks, ahead of even the grapple. The animation's own root
            // motion is what is moving the visible body; nothing here
            // should fight it, and TickTopOut is what notices the clip has
            // actually finished and relocates the real CharacterController.
            if (_climbing != null && _climbing.IsToppingOut)
            {
                _climbing.TickTopOut();
                _audio?.SilenceFootsteps();
                return;
            }

            // A VAULT OWNS THE FRAME TOO: the body follows a path over the obstacle (PlayerVaulting).
            if (_vaulting != null && _vaulting.IsVaulting)
            {
                _vaulting.Tick(deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // A MENU HAS THE KEYS (PlayerInputReader.Holds.cs): the body goes on with none read.
            if (IsHeld)
            {
                TickHeld(deltaTime);
                return;
            }

            // A LADDER OWNS THE FRAME TOO (PlayerLadder): W and S climb, Shift with S slides, Space hops off, Control drops.
            if (_ladder != null && _ladder.IsOn)
            {
                _ladder.Tick(keyboard.wKey.isPressed, keyboard.sKey.isPressed, keyboard.leftShiftKey.isPressed,
                    keyboard.spaceKey.wasPressedThisFrame, keyboard.leftCtrlKey.wasPressedThisFrame, deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // THE SWING OWNS THE WHOLE FRAME WHILE ACTIVE, matching
            // player.gd's own _physics_process early-return into
            // _process_grapple_swing -- placed above TickCombat and the
            // ordinary WASD-to-locomotion dispatch below so neither ever
            // runs while a rope is attached. Reel/payout reuse the sprint/
            // crouch keys and release reuses jump/punch, the same keys
            // Godot's own version does, since the grapple has no other use
            // for them while it is active (see TickCombat's own weapon-
            // equipped branch, which this pre-empts by never being reached).
            //
            // CHECKED BEFORE CLIMBING, matching player.gd's own comment on
            // this exact ordering: "a swing that carries you past a wall
            // doesn't get hijacked into a wall-grab by the same held W
            // that's pumping it."
            if (_grappleSwing != null && _grappleSwing.IsActive)
            {
                // "Ragdolling until he reaches land or dashes" -- the SAME
                // double-tap gesture ground movement already dashes on, not
                // a second key. Checked before anything else in this block:
                // a dash is a deliberate bail-out, worth honouring even on
                // the exact frame reel/payout also reads the held half of
                // this same shift key.
                if (ConsumeDoubleTapShift(keyboard, deltaTime))
                {
                    _grappleSwing.Release(false);
                    _audio?.SilenceFootsteps();
                    return;
                }

                Vector2 swingMove = Vector2.zero;
                if (keyboard.wKey.isPressed) swingMove.y += 1.0f;
                if (keyboard.sKey.isPressed) swingMove.y -= 1.0f;
                if (keyboard.dKey.isPressed) swingMove.x += 1.0f;
                if (keyboard.aKey.isPressed) swingMove.x -= 1.0f;

                Transform swingYawSource = YawSource != null
                    ? YawSource
                    : (Camera.main != null ? Camera.main.transform : null);
                float swingYaw = swingYawSource != null ? swingYawSource.eulerAngles.y : 0.0f;

                bool boostedRelease = keyboard.spaceKey.wasPressedThisFrame;
                bool plainRelease = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
                bool reelHeld = keyboard.leftShiftKey.isPressed;
                bool payoutHeld = keyboard.leftCtrlKey.isPressed;

                // THE GRAPPLE GUN OWNS THE RIGHT HAND, so a cast while
                // swinging goes through the left instead -- explicit ask:
                // "it still works just comes from your left hand". Same
                // digit1Key/origin-height/ignore-colliders shape as the
                // ordinary ground cast below.
                if (_fireballCaster != null && _skills.Pressed(SkillCatalog.Fireball, keyboard) && swingYawSource != null)
                {
                    SpellLaunch(swingYawSource, out Vector3 origin, out Vector3 direction);
                    _fireballCaster.TryCast(origin, direction, GetComponentsInChildren<Collider>());
                }
                if (_skills.Pressed(SkillCatalog.IceShards, keyboard))
                {
                    CastIceShards(swingYawSource);
                }
                if (_skills.Pressed(SkillCatalog.ArcaneOrb, keyboard))
                {
                    CastArcaneOrb(swingYawSource);
                }
                if (_skills.Pressed(SkillCatalog.PoisonBreath, keyboard))
                {
                    CastPoisonBreath(swingYawSource);
                }
                if (_skills.Pressed(SkillCatalog.AcidRain, keyboard))
                {
                    CastAcidRain(swingYawSource);
                }
                AimIceShards(swingYawSource);
                AimPoisonBreath(swingYawSource);

                // THE ORDINARY WASD-MOVEMENT BRANCH BELOW NEVER RUNS WHILE
                // SWINGING (this whole block returns before reaching it),
                // and that branch is the only other place SetCastPose gets
                // called -- so without this, casting while swinging would
                // fire the fireball but show no left-arm raise at all.
                // GrappleSwing itself never touches this bone any more (see
                // its own class doc comment), so PlayerLocomotion's normal
                // ApplyCastingPose is free to layer this on top of whatever
                // the swing's own DriveAnimatorFromExternalVelocity pose is,
                // exactly like it would during ordinary ground movement.
                _locomotion.SetCastPose(SpellCastFraction, useLeftArm: true);

                _grappleSwing.Tick(swingMove, swingYaw, boostedRelease, plainRelease,
                    reelHeld, payoutHeld, deltaTime);

                // Bug report: "make it so my limbs cant clip
                // through walls and get stuck... swinging around with
                // grapple." A swing never ragdolls (Ragdoll.Activate's own
                // "NEVER RAGDOLLS OUT OF AN ACTIVE SWING"), so the bones
                // stay kinematic/animator-driven the whole time -- see
                // Ragdoll.TickKinematicLimbClearance's own doc comment for
                // why that means nothing else ever pulls a swinging limb
                // back out of a wall it swung into.
                _ragdoll?.TickKinematicLimbClearance();

                _audio?.SilenceFootsteps();
                return;
            }

            // WALL GRABS ARE ASKED FOR, NOT STUMBLED INTO. Holding Space for a moment (a tap is still a jump)
            // is how the player says "grab that wall"; running, jumping through a window or walking through a
            // doorway never asks. Updated every frame, before the attached branch below, so letting go of the
            // key while hanging on is seen (a grab needs a fresh hold). Not with a jetpack or parachute on the
            // back: Space means thrust or canopy there.
            _grabIntent.Update(keyboard.spaceKey.isPressed, deltaTime);
            // Nor while Grow or Shrink has the body at another size: the grab and the ledge are measured for a 2.6 m body.
            bool wallGrabsAllowed = EquippedFlightGear == null && EquippedParachute == null && !_locomotion.IsResized;

            // ALREADY ATTACHED TO A WALL OWNS THE WHOLE FRAME TOO, matching
            // player.gd's own _is_climbing early-return into
            // _process_climbing -- a real reported bug there ("the sprint
            // noise is playing while climbing") is why footsteps are
            // silenced here rather than only at grab time: nothing
            // downstream ever runs again to turn them off otherwise.
            if (_climbing != null && _climbing.IsClimbing)
            {
                _climbing.Tick(keyboard.spaceKey.wasPressedThisFrame,
                    keyboard.wKey.isPressed, keyboard.sKey.isPressed, _vitals, deltaTime,
                    dropPressed: keyboard.leftCtrlKey.wasPressedThisFrame, leftHeld: keyboard.aKey.isPressed, rightHeld: keyboard.dKey.isPressed);
                _audio?.SilenceFootsteps();
                return;
            }

            // NOT YET CLIMBING -- try to grab on. Gated the same way
            // player.gd's own _try_start_climb call is: not carrying
            // something (both hands full) and not mid-dash (a committed
            // burst nothing should interrupt). Checked ahead of TickCombat/
            // ordinary movement, and if it succeeds THIS frame, returns
            // immediately -- see PlayerClimbing.Tick's own doc comment on
            // why the very first climbing frame must not also fall through
            // to ordinary WASD movement computed from the same held W that
            // just triggered the grab.
            // CLIMBING IS A SKILL (the Freerunner tree's Climb, which also pulls you up onto ledges): not learnt, no wall is ever grabbed.
            if (_climbing != null && wallGrabsAllowed && _skills.Knows(SkillCatalog.Climb) &&
                (_grabCarry == null || !_grabCarry.IsCarrying) &&
                !_locomotion.IsDashing)
            {
                float stamina = _vitals != null ? _vitals.Stamina : float.MaxValue;
                if (_climbing.TryStart(_grabIntent, keyboard.wKey.isPressed, stamina))
                {
                    _climbing.Tick(keyboard.spaceKey.wasPressedThisFrame,
                        keyboard.wKey.isPressed, keyboard.sKey.isPressed, _vitals, deltaTime,
                    dropPressed: keyboard.leftCtrlKey.wasPressedThisFrame, leftHeld: keyboard.aKey.isPressed, rightHeld: keyboard.dKey.isPressed);
                    _audio?.SilenceFootsteps();
                    return;
                }
            }

            // WHERE THE PLAYER IS TRYING TO GO (camera yaw + WASD), not where the body happens to face: the vault and the auto-climb look along this, so they work
            // however the camera is turned and however the body has caught up with it.
            Vector3 wishDirection = Vector3.zero;
            {
                Transform wishYaw = YawSource != null ? YawSource : (Camera.main != null ? Camera.main.transform : null);
                float yaw = wishYaw != null ? wishYaw.eulerAngles.y : transform.eulerAngles.y;
                float wx = (keyboard.dKey.isPressed ? 1.0f : 0.0f) - (keyboard.aKey.isPressed ? 1.0f : 0.0f);
                float wz = (keyboard.wKey.isPressed ? 1.0f : 0.0f) - (keyboard.sKey.isPressed ? 1.0f : 0.0f);
                if (wx != 0.0f || wz != 0.0f)
                {
                    wishDirection = Quaternion.Euler(0.0f, yaw, 0.0f) * new Vector3(wx, 0.0f, wz).normalized;
                }
            }

            // WALKING INTO A LADDER'S FOOT GETS ON IT (E gets on from anywhere beside it, and from its top: LadderPrompt).
            if (_ladder != null && (_grabCarry == null || !_grabCarry.IsCarrying) && !_locomotion.IsDashing
                && _ladder.TryWalkOn(keyboard.wKey.isPressed, wishDirection))
            {
                _audio?.SilenceFootsteps();
                return;
            }

            // RUNNING AT A LOW OBSTACLE VAULTS IT (Shift + W; holding Space is still the way to ask for a climb, checked above).
            if (_vaulting != null && !_locomotion.IsDashing && !_locomotion.IsResized && (_grabCarry == null || !_grabCarry.IsCarrying)
                && _vaulting.TryStart(keyboard.wKey.isPressed, keyboard.leftShiftKey.isPressed, _locomotion.IsGrounded, _locomotion.CurrentSpeed, deltaTime, wishDirection))
            {
                _vaulting.Tick(deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // RUNNING INTO A TALL WALL CLIMBS IT (Shift + W, held; guarded against stairs, doors and grazing -- see PlayerClimbing.TryStartAuto). After the
            // vault above so a low obstacle is vaulted, and after the Space-hold grab so a deliberate grab always wins.
            if (_climbing != null && wallGrabsAllowed && _skills.Knows(SkillCatalog.Climb) && !_locomotion.IsDashing
                && (_grabCarry == null || !_grabCarry.IsCarrying))
            {
                float stamina = _vitals != null ? _vitals.Stamina : float.MaxValue;
                if (_climbing.TryStartAuto(keyboard.wKey.isPressed, keyboard.leftShiftKey.isPressed, _locomotion.IsGrounded, stamina, deltaTime, wishDirection))
                {
                    _climbing.Tick(keyboard.spaceKey.wasPressedThisFrame,
                        keyboard.wKey.isPressed, keyboard.sKey.isPressed, _vitals, deltaTime,
                        dropPressed: keyboard.leftCtrlKey.wasPressedThisFrame, leftHeld: keyboard.aKey.isPressed, rightHeld: keyboard.dKey.isPressed);
                    _audio?.SilenceFootsteps();
                    return;
                }
            }

            // ALREADY STUCK TO A WALL OWNS THE WHOLE FRAME TOO -- same
            // pattern as the climbing block above.
            if (_wallStick != null && _wallStick.IsSticking)
            {
                Vector2 stickMove = Vector2.zero;
                if (keyboard.wKey.isPressed) stickMove.y += 1.0f;
                if (keyboard.sKey.isPressed) stickMove.y -= 1.0f;
                if (keyboard.dKey.isPressed) stickMove.x += 1.0f;
                if (keyboard.aKey.isPressed) stickMove.x -= 1.0f;

                _wallStick.Tick(keyboard.spaceKey.wasPressedThisFrame, stickMove, _vitals, deltaTime);
                _audio?.SilenceFootsteps();
                return;
            }

            // NOT YET STICKING -- try to grab on. Checked AFTER climbing's
            // own grab attempt above (see WallStick's own doc comment on why
            // the two never fight over the same frame: this one is gated on
            // being airborne, climbing's own grab is not gated on grounded
            // at all, so an airborne sprint+W into a shallow-enough wall
            // still climbs a ladder exactly as before this existed).
            if (_wallStick != null && wallGrabsAllowed && _skills.Knows(SkillCatalog.WallStick) &&
                (_grabCarry == null || !_grabCarry.IsCarrying) &&
                !_locomotion.IsDashing)
            {
                float stamina = _vitals != null ? _vitals.Stamina : float.MaxValue;
                if (_wallStick.TryStart(_grabIntent, keyboard.leftShiftKey.isPressed, _locomotion.IsGrounded, stamina))
                {
                    Vector2 stickMove = Vector2.zero;
                    if (keyboard.wKey.isPressed) stickMove.y += 1.0f;
                    if (keyboard.sKey.isPressed) stickMove.y -= 1.0f;
                    if (keyboard.dKey.isPressed) stickMove.x += 1.0f;
                    if (keyboard.aKey.isPressed) stickMove.x -= 1.0f;

                    _wallStick.Tick(keyboard.spaceKey.wasPressedThisFrame, stickMove, _vitals, deltaTime);
                    _audio?.SilenceFootsteps();
                    return;
                }
            }

            // TAB LOCKS ON, and pressing it again cycles through the
            // surrounding enemies (then releases). Read HERE rather than in
            // the camera, behind every menu/console/stun early-return above:
            // the last lock-on read its key inside the camera and raced the
            // player menu's own binding (see LockOn's doc comment).
            // NOT WITH A RANGED WEAPON (owner, 2026-10-24: "turn off tab lock on for ranged weapons"): a gun or the flamethrower is aimed with
            // the crosshair, so Tab does nothing with one in hand, and drawing one lets a lock go.
            if (HoldsRangedWeapon)
            {
                if (_orbitCamera != null && _orbitCamera.IsLockedOn)
                {
                    _orbitCamera.ReleaseLock();
                }
            }
            else if (keyboard.tabKey.wasPressedThisFrame && _orbitCamera != null)
            {
                _orbitCamera.PressLockOn();
            }

            // Q SWAPS THE CAMERA TO THE OTHER SHOULDER -- standard
            // third-person-shooter kit (RE4, Gears, Fortnite): peek round a
            // corner without exposing the body, or clear a wall your usual
            // shoulder is against. Works with anything in hand.
            if (keyboard.qKey.wasPressedThisFrame && _orbitCamera != null)
            {
                _orbitCamera.ToggleShoulder();
            }

            if (EquippedGun != null && _gunFeel == null)
            {
                _gunFeel = GetComponent<GunFeel>();   // added at runtime by the gun's OnEquip
            }

            TickCombat(deltaTime);

            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed) move.y += 1.0f;
            if (keyboard.sKey.isPressed) move.y -= 1.0f;
            if (keyboard.dKey.isPressed) move.x += 1.0f;
            if (keyboard.aKey.isPressed) move.x -= 1.0f;

            Transform yawSource = YawSource != null
                ? YawSource
                : (Camera.main != null ? Camera.main.transform : null);

            // THE FIREBALL SPELL, independent of whatever is equipped --
            // press 1 and it fires regardless of fists, bottle, sword or
            // gun, since it is an innate ability rather than something in
            // the weapon slot. Placed after every early-return above
            // (ragdoll/dead/stunned/topping-out/grapple-active all bail out
            // of Update long before reaching here), so those states block
            // a cast the same way they already block everything else.
            //
            // EVERY SPELL NOW CASTS FROM ITS SLOT ON THE BAR (PlayerSkills.Pressed): whichever of the keys 1 to 0 holds it, once learnt.
            if (_fireballCaster != null && _skills.Pressed(SkillCatalog.Fireball, keyboard) && yawSource != null)
            {
                SpellLaunch(yawSource, out Vector3 origin, out Vector3 direction);
                _fireballCaster.TryCast(origin, direction, GetComponentsInChildren<Collider>());
            }

            // 3: ICE SHARDS, innate like the fireball: four shards one after another down the crosshair, each sticking where it lands. The
            // rest of a volley follows the crosshair as it goes out.
            if (_skills.Pressed(SkillCatalog.IceShards, keyboard))
            {
                CastIceShards(yawSource);
            }
            // ARCANE ORB (Arcanist): a ball of magenta aura down the crosshair that leaves cursed orbs on whoever it hits (docs/ARCANE_ORB.md).
            if (_skills.Pressed(SkillCatalog.ArcaneOrb, keyboard))
            {
                CastArcaneOrb(yawSource);
            }
            // ARCANE BARRIER (Arcanist): a wall of magenta aura raised before the body, turned to where the camera looks, and held up for as long
            // as its key is (read above, where the wall takes the body).
            if (_skills.Pressed(SkillCatalog.ArcaneBarrier, keyboard))
            {
                RaiseArcaneBarrier(yawSource);
            }
            // 4: POISON GAS, innate like the fireball: a cloud of purple gas blown out down the crosshair; whatever breathes it is poisoned
            // (never the one who blew it).
            if (_skills.Pressed(SkillCatalog.PoisonBreath, keyboard))
            {
                CastPoisonBreath(yawSource);
            }
            // 5: ACID RAIN (a god-tier test spell): a storm of acid over a wide area where the crosshair points; roofs shelter.
            if (_skills.Pressed(SkillCatalog.AcidRain, keyboard))
            {
                CastAcidRain(yawSource);
            }
            // SELF SMITE (Electromancer): Pikachu's Thunder, a bolt from the clouds onto the caster, who bursts with electricity. Not while
            // swinging on the grapple (the swing branch above never reaches here): the move roots the body.
            if (_skills.Pressed(SkillCatalog.SelfSmite, keyboard) && (_selfSmite != null || (_selfSmite = GetComponent<SelfSmiteAbility>()) != null))
            {
                _selfSmite.TryCast();
            }
            // GROW AND SHRINK (Wizard): ten times as big, or a tenth, for 30 s (BodySizeAbility). The same key again ends it early.
            if (_skills.Pressed(SkillCatalog.Grow, keyboard) && (_bodySize != null || (_bodySize = GetComponent<BodySizeAbility>()) != null))
            {
                _bodySize.TryGrow();
            }
            if (_skills.Pressed(SkillCatalog.Shrink, keyboard) && (_bodySize != null || (_bodySize = GetComponent<BodySizeAbility>()) != null))
            {
                _bodySize.TryShrink();
            }
            // HEALING AURA (Cleric): a green circle round the body, going where it goes, healing everyone in it slowly (docs/HEALING.md).
            if (_skills.Pressed(SkillCatalog.HealingAura, keyboard) && (_healingAura != null || (_healingAura = GetComponent<HealingAuraAbility>()) != null))
            {
                _healingAura.TryCast();
            }
            // FIRE ENHANCEMENT (Pyromancer): the next swing, bullet or arrow burns (docs/FIRE_ENHANCEMENT.md).
            if (_skills.Pressed(SkillCatalog.FireEnhancement, keyboard)
                && (_fireEnhancement != null || (_fireEnhancement = GetComponent<FireEnhancementAbility>()) != null))
            {
                _fireEnhancement.TryCast();
            }
            // GLACIER SPIKES (Cryomancer): the key arms it and the body turns to where the camera looks; a left click casts it (read in TickCombat).
            TickGlacierSpikesKey(keyboard, yawSource);
            AimIceShards(yawSource);
            AimPoisonBreath(yawSource);
            AimWand(yawSource);

            // 8: BLOODRAGE, innate like the fireball (see BloodrageAbility): +25% speed and melee damage, the Bloodrage walk/run/jump, a red aura, 7.5 s.
            if (_skills.Pressed(SkillCatalog.Bloodrage, keyboard))
            {
                if (_bloodrageAbility == null)
                {
                    _bloodrageAbility = GetComponent<BloodrageAbility>();
                }
                _bloodrageAbility?.TryActivate();
            }

            // A JETPACK TAKES SHIFT OVER TOO WHILE ACTUALLY THRUSTING, not
            // just Space -- the explicit ask ("shift is a boost when midair
            // using jetpack") redefines what the key means the instant
            // thrust is live. Computed before wantsSprint below so that
            // check can exclude this case rather than double-booking one
            // key with two unrelated meanings and draining stamina for a
            // mechanic that is not supposed to cost any.
            //
            // AIRBORNE ONLY -- Bug report: "when you press space on
            // the floor with jetpack on it just does boost right away but
            // make it so you can still jump with jetpack on and but when you
            // hold space mid air then it activates." Space used to mean
            // thrust the instant the jetpack was equipped, grounded or not,
            // which left no way to jump at all while wearing it. Requiring
            // !_locomotion.IsGrounded here is also what makes the dispatch
            // just below correctly fall through to a jump instead while
            // still standing on the ground.
            // (docs/FLIGHT.md, 2026-10-28: Space takes off and the flight then goes on until the body lands, hovering with nothing held --
            // the gear's own FlightModel decides, below, once it has this frame's keys; this is where the flight stood at the end of the
            // last frame.)
            bool jetpackThrusting = EquippedFlightGear != null && EquippedFlightGear.IsFlying;

            // A PARACHUTE TAKES A FRESH PRESS OF SPACE WHILE AIRBORNE, not a
            // hold -- Requested: "press space and open and close it."
            // wasPressedThisFrame rather than isPressed, since this is a
            // toggle (open, then a second press later closes it again), not
            // a held state the way jetpack thrust is. Airborne-only for the
            // same reason jetpackThrusting is: while grounded this stays
            // false, so the dispatch below still falls through to an
            // ordinary jump instead, and there is nothing to "open" while
            // standing on the ground anyway. EquippedFlightGear and
            // EquippedParachute share Jetpack's own "back" equip slot (see
            // ItemRegistry.Parachute's own doc comment), so Inventory's own
            // slot eviction already guarantees at most one of them is ever
            // equipped at once -- no extra mutual-exclusion needed here.
            bool parachuteToggledThisFrame = EquippedParachute != null &&
                keyboard.spaceKey.wasPressedThisFrame && !_locomotion.IsGrounded;

            // Sprint only forwards. Sprinting sideways or backwards has no
            // animation and reads as a glitch rather than as speed.
            //
            // AIMING DOWN SIGHTS ENDS A SPRINT (and holding aim stops you
            // starting one) -- you cannot run flat out and hold a steady
            // sight picture (Fortnite's sprint puts the weapon away outright).
            bool wantsSprint = !jetpackThrusting && !_isAimingGun
                && keyboard.leftShiftKey.isPressed && move.y > 0.0f;

            // Stamina decides, not the key. Asking rather than checking a flag
            // keeps the exhaustion latch in one place.
            bool sprinting = _vitals != null
                ? _vitals.TickStamina(wantsSprint, deltaTime)
                : wantsSprint;

            // An unarmed swing roots the body, which is what makes a combo a
            // commitment rather than something you can walk out of. A sword's
            // swing takes nothing from the body's movement (below); a held
            // guard walks at half speed.
            Vector2 heldMove = move;
            SoulsSwordCombat soulsNow = SoulsSword;
            bool soulsArmed = soulsNow != null && soulsNow.IsEquipped;
            if (_melee != null && _melee.IsSwinging)
            {
                // The aerial sword spin can be steered a little (half pace), as Link's can. An UNARMED strike roots the body too, and carries it
                // into the blow by its own stride (MeleeAttacker.CarryIntoPunch): its clip's feet are planted on the ground it is carried over.
                move *= _melee.SwingMoveFraction;
                sprinting = false;
            }
            // (A SWORD'S SWING TAKES NOTHING FROM THE BODY'S MOVEMENT: owner, 2026-11-13, "You need to have the sword movements conserve
            // momentum and move freely while attacking ... You should be able to move freely with zero slowdown". The keys walk, sprint
            // and steer through every swing; the legs are the walk's or the run's and the attack is the arms and the torso over them
            // (SwordPoseDriver, docs/SWORD_MOMENTUM.md). It was slowed to 0.65 of a walk until 2026-11-13 and rooted for a few hours of it.)
            else if (soulsArmed && soulsNow.IsBlocking)
            {
                move *= 0.5f;
                sprinting = false;
            }
            else if (_melee != null && _melee.IsGuarding)
            {
                move *= FistGuard.MoveFraction;   // the bare-handed guard walks as the sword's does
                sprinting = false;
            }

            float yawDegrees = yawSource != null ? yawSource.eulerAngles.y : 0.0f;
            _lastYawDegrees = yawDegrees;
            // A roll goes where the keys were held even out of a swing that
            // was rooting the body -- the pre-rooting input, not the zeroed one.
            TickDash(keyboard, soulsArmed ? heldMove : move, yawDegrees, deltaTime);

            bool chargingJumpNow = false;
            float jumpChargeFraction = 0.0f;
            bool jumpReleased;
            // (A sword spin on the ground holds the body, charge included: no jumping out of it.)
            bool spinHoldsTheBody = _melee != null && _melee.IsSpinAttacking;
            // SUPER JUMP IS A SKILL NOW (owner, 2026-10-14: the Freerunner
            // tree's Super Jump). The charged jump was kept in the vault
            // ("get rid of charged jump for now that is going to be a skill
            // tree ability keep it in the vault"); learning Super Jump routes
            // Space back through TickJumpCharge, untouched: held, it charges;
            // let go, it jumps, up to ChargedJumpHeightMultiplier times as
            // high. Without the skill, a bare tap of Space while grounded
            // fires the jump at once at PlayerLocomotion's base JumpVelocity
            // (JumpReleased with JumpChargeFraction 0 resolves the height
            // multiplier to exactly 1). Computed unconditionally (not just
            // when the jetpack skips its own Tick below): it is grounded-gated
            // on its own and jetpackThrusting is forced false while grounded,
            // so Space on the ground always means jump, jetpack or not.
            if (_skills.Knows(SkillCatalog.SuperJump) && !spinHoldsTheBody)
            {
                TickJumpCharge(keyboard, _locomotion.IsGrounded, deltaTime,
                    out chargingJumpNow, out jumpReleased, out jumpChargeFraction);
                // The leap's own height never hurts its landing (PlayerVitals.ForgiveFallAbove).
                if (jumpReleased && jumpChargeFraction > 0.0f && _locomotion.IsGrounded)
                {
                    _vitals?.ForgiveFallAbove(transform.position.y);
                }
            }
            else
            {
                _jumpHeld = false;
                jumpReleased = _locomotion.IsGrounded && keyboard.spaceKey.wasPressedThisFrame && !spinHoldsTheBody;
            }

            // A JETPACK TAKES SPACE OVER ONLY ONCE ACTUALLY AIRBORNE --
            // Bug report: "when you press space on the floor with
            // jetpack on it just does boost right away but make it so you
            // can still jump with jetpack on and but when you hold space
            // mid air then it activates." While grounded, jetpackThrusting
            // is already false (see its own doc comment above), so Tick
            // below never reactivates thrust/flame/audio on the ground --
            // WASD keeps steering horizontally exactly the way ordinary
            // falling already lets it, matching the jetpack's own explicit
            // ask ("mixed with wasd to move around").
            //
            // STILL CALLED EVERY FRAME WHILE EQUIPPED, GROUNDED OR NOT --
            // Bug report: "after i crash and recover sometimes i
            // am stuck at an angle." This used to be gated on
            // `!_locomotion.IsGrounded`, so the instant the player landed
            // (including standing back up from a crash ragdoll) Tick simply
            // stopped being called at all -- and Tick's own TickTilt is the
            // ONLY thing that ever eases _currentTilt/the spine's own
            // localRotation back toward level (see TickTilt's own doc
            // comment). Whatever lean the body had at the exact instant of
            // the crash stayed baked into the spine transform forever,
            // since nothing else ever touches it. Now Tick runs
            // unconditionally while equipped; with thrustHeld already false
            // on the ground, TickTilt's own target collapses to
            // Vector2.zero and SmoothDamps the lean back to upright over
            // TiltSmoothTime, the same graceful ease-out used everywhere
            // else in this system -- not an instant snap.
            if (EquippedFlightGear != null)
            {
                EquippedFlightGear.Tick(new FlightInput
                {
                    ThrustHeld = keyboard.spaceKey.isPressed,
                    DescendHeld = keyboard.leftCtrlKey.isPressed,
                    BoostHeld = keyboard.leftShiftKey.isPressed,
                    Move = move,
                    CameraPitchDegrees = _orbitCamera != null ? _orbitCamera.Pitch : 0.0f,
                    Airborne = !_locomotion.IsGrounded,
                }, deltaTime);
                jetpackThrusting = EquippedFlightGear.IsFlying;
            }

            // Bug report: "make it so my limbs cant clip through
            // walls and get stuck when im flying around with jetpack." Only
            // while actually thrusting (matches jetpackThrusting's own
            // airborne-and-holding-space gate) -- a crash hard enough to
            // ragdoll is already handled by Ragdoll.Activate's own
            // DepenetrateFromWorld further below; this instead covers every
            // softer graze/near-miss along the way that never crosses that
            // threshold, where a Superman-dive arm can still reach a nearby
            // wall with nothing else correcting it. See Ragdoll.
            // TickKinematicLimbClearance's own doc comment for the mechanism.
            if (jetpackThrusting)
            {
                _ragdoll?.TickKinematicLimbClearance();
            }

            // CALLED EVERY FRAME WHILE EQUIPPED, GROUNDED OR NOT -- same
            // reasoning EquippedFlightGear.Tick just above is, per that block's
            // own doc comment: Tick is also where a parachute notices it
            // landed and auto-closes itself (see IParachute.Tick's own doc
            // comment), which has to keep happening even after the toggle
            // press itself is long past and even once grounded, not just on
            // the one frame Space was actually pressed.
            if (EquippedParachute != null)
            {
                EquippedParachute.Tick(parachuteToggledThisFrame, _locomotion.IsGrounded, deltaTime);
            }

            // A CAST NO LONGER BORROWS THE GUN'S OWN AIM POSE -- the explicit
            // ask was a dedicated animation (a raised right hand), so
            // isCastingFireball now only feeds the jetpack-suppression check
            // below, the same way gun-aiming does; the actual visual is a
            // separate procedural pose (see PlayerLocomotion.SetCastPose)
            // rather than a second state sharing LocomotionInput.Aiming.
            bool isCastingFireball = IsCastingSpell;
            bool isAiming = _isAimingGun || isCastingFireball;
            // THE GRAPPLE GUN OWNS THE RIGHT HAND -- see SetCastPose's own
            // doc comment. EquippedHook alone, not GrappleSwing.IsActive:
            // the gun is in that hand whether or not a rope currently
            // happens to be attached.
            bool castRaisesLeftArm = _grappleSwing != null && _grappleSwing.EquippedHook != null;
            _locomotion.SetCastPose(SpellCastFraction, castRaisesLeftArm);
            if (_orbitCamera != null)
            {
                _orbitCamera.IsSprinting = sprinting;   // the view widens a touch at a sprint (ThirdPersonCamera.SprintFieldOfViewScale)
            }

            var input = new LocomotionInput
            {
                Move = move,
                YawDegrees = yawDegrees,
                Sprint = sprinting,
                Crouch = keyboard.leftCtrlKey.isPressed && !jetpackThrusting,   // flying, Ctrl sinks the body (FlightInput.DescendHeld)
                ShieldRaised = _vitals != null && _vitals.ShieldRaised,
                // Charging kept in the vault for now -- see the dispatch
                // above (and TickJumpCharge's own doc comment) for why
                // Space fires a plain, un-charged jump directly instead.
                ChargingJump = chargingJumpNow,
                JumpReleased = jumpReleased,
                JumpHeld = keyboard.spaceKey.isPressed && !spinHoldsTheBody,
                JumpChargeFraction = jumpChargeFraction,
                // Gun-aiming only now -- see isCastingFireball's own comment
                // just above for why a cast no longer sets this too.
                Aiming = _isAimingGun,
                ThrowAiming = _throwAiming || GlacierSpikesArmed,
                AimCameraPosition = _orbitCamera != null ? _orbitCamera.transform.position : Vector3.zero,
                AimCameraForward = _orbitCamera != null ? _orbitCamera.transform.forward : Vector3.forward,
                // The two-handed carry (the 2hsw clips). A one-handed sword is carried by the ordinary walk's own arm, its attacks posed on top.
                SwordEquipped = _melee != null && _melee.EquippedWeapon != null &&
                                _melee.EquippedWeapon.MovesetId == ComboChain.Sword &&
                                !(_melee.EquippedWeapon is ISwordBlade oneHandedBlade && oneHandedBlade.IsOneHanded),
                // The one-handed sword's own carry clips (Sword1HLocomotion: SwordCarryBaker's walk, run and strafes with a sword arm).
                Sword1HEquipped = _melee != null && _melee.EquippedWeapon != null &&
                                  _melee.EquippedWeapon.MovesetId == ComboChain.Sword &&
                                  _melee.EquippedWeapon is ISwordBlade heldBlade && heldBlade.IsOneHanded,
                // A drawn weapon (the bow) poses both arms itself (PlayerLocomotion.Bow.cs): the gun's own arm pose stays off for it.
                GunEquipped = EquippedGun != null && !(EquippedGun is IDrawnWeapon),
                // Where the gun's shot would end -- the arm aims at it (see
                // LocomotionInput.GunAimPoint).
                GunAimPoint = _gunFeel != null ? _gunFeel.AimPoint : Vector3.zero,
                HasGunAimPoint = EquippedGun != null && _gunFeel != null && _gunFeel.HasAimPoint,
                LockedOn = _orbitCamera != null && _orbitCamera.IsLockedOn,
                // AIMING WINS, not jetpackThrusting raw -- "aim and shoot
                // while using the jetpack starts glitching", an explicit
                // report. Aiming and JetpackThrusting are both bool-gated
                // Any State entries (see PlayerRigBuilder's own
                // AddAimingState/AddJetpackIdleState); Unity re-evaluates
                // every Any State condition every frame regardless of the
                // current state, and with both true at once there is no
                // single well-defined winner, which is exactly what reads
                // as glitching -- the state machine has no reason to settle
                // on one over the other. This only gates the ANIMATION bool;
                // EquippedFlightGear.Tick above still gets the real,
                // un-suppressed jetpackThrusting, so holding right-click
                // never weakens the actual thrust/lean it's flying on.
                JetpackThrusting = jetpackThrusting && !isAiming,
                // UN-SUPPRESSED BY isAiming, matching JetpackThrusting's own
                // comment just above on why EquippedFlightGear.Tick's real
                // thrust/lean never weakens while aiming -- this drives real
                // movement speed, not an Animator Any-State bool, so there
                // is no state-machine conflict here to suppress in the
                // first place.
                FlightSpeed = jetpackThrusting ? EquippedFlightGear.HorizontalSpeed : 0.0f,
                FlightAcceleration = jetpackThrusting ? EquippedFlightGear.HorizontalAcceleration : 0.0f,
                Dancing = _isDancing,
                Breakdancing = _isBreakdancing,
            };

            float horizontalSpeedBeforeMove = _locomotion.CurrentSpeed;
            _locomotion.Tick(input, deltaTime);

            // Requested: "horizontal 'fall damage' when using the jetpack
            // and you crash into a wall... if the amount of damage you take
            // doesnt kill you, you still ragdoll." Gated on jetpackThrusting
            // specifically, not just EquippedFlightGear != null -- an ordinary
            // walk into a wall (no thrust) is not a crash, it is just
            // bumping into something, and ApplyCrashDamage's own
            // SafeFallDistance floor already makes a slow, harmless jetpack
            // nudge deal no damage either way.
            //
            // EDGE-DETECTED via _wasJetpackCrashing -- a real contact
            // reports true every frame the controller is still pressed into
            // the wall, and applying fresh damage each of those frames
            // would be continuous, not a single impact.
            //
            // HitWallThisFrame, NOT CollisionFlags.Sides -- Bug
            // REPORT: "the jetpack sideways fall damage thing still hasnt
            // worked, make it do it upon my playermodel coming into
            // contact with the wall or object." CollisionFlags is a coarse
            // summary bitmask of the whole Move call; PlayerLocomotion's
            // own OnControllerColliderHit now reads the actual per-contact
            // normal Unity reports for the real hit, which is the more
            // direct and reliable signal for "did the body really touch a
            // wall" -- see PlayerLocomotion.HitWallThisFrame's own doc
            // comment.
            if (jetpackThrusting)
            {
                bool hitWall = _locomotion.HitWallThisFrame;
                bool isNewWallContact = IsNewWallContact(
                    hitWall, _wasJetpackCrashing, _locomotion.LastWallHitNormal, _lastJetpackCrashNormal);

                if (isNewWallContact && _vitals != null)
                {
                    float damage = _vitals.ApplyCrashDamage(horizontalSpeedBeforeMove);
                    if (damage > 0.0f)
                    {
                        _audio?.PlayFallImpact();
                        if (!_vitals.IsDead)
                        {
                            // Bug report: "whenever i fly into a
                            // wall or window i just get stuck in it" --
                            // STILL true after a first attempt at a fixed
                            // pull-back nudge here (a guess at how far a
                            // limb tunnelled, which could not know how far
                            // a Superman-dive's outstretched arm actually
                            // reached past the wall on any given hit).
                            // Ragdoll.Activate now measures and clears the
                            // real overlap itself (see its own
                            // DepenetrateFromWorld), so this only has to
                            // hand it a knockback DIRECTION -- the real
                            // contact normal Unity just gave us, preferred
                            // over -velocity since velocity is often more
                            // steeply angled into the wall than the surface
                            // itself.
                            Vector3 awayFromWall = _locomotion.LastWallHitNormal.sqrMagnitude > 0.0001f
                                ? _locomotion.LastWallHitNormal
                                : -_locomotion.Velocity;
                            _vitals.TriggerCrashKnockdown(awayFromWall);
                        }
                    }
                }

                // Tracked whenever hitWall is true, not only when a fresh
                // impact actually fired -- the NEXT frame's own angle check
                // needs to compare against the MOST RECENT face touched
                // (so smoothly sliding along one continuous curved surface
                // never falsely accumulates into "a new wall" one small
                // step at a time), not a stale snapshot from several frames
                // back.
                if (hitWall)
                {
                    _lastJetpackCrashNormal = _locomotion.LastWallHitNormal;
                }
                _wasJetpackCrashing = hitWall;
            }
            else
            {
                _wasJetpackCrashing = false;
            }

            // AFTER the move, so grounded and height are this frame's, not the
            // previous one's. Checking before would measure the fall one frame
            // short and let a lethal drop land as a survivable one.
            if (_vitals != null)
            {
                // Deep water breaks a fall: the fall is measured from where the body is in it (PlayerLocomotion.WaterBreaksFall).
                if (_locomotion.WaterBreaksFall)
                {
                    _vitals.ResetFallTracking(transform.position.y);
                }
                float fallDamage =
                    _vitals.TickFallDamage(_locomotion.IsGrounded, transform.position.y);
                if (fallDamage > 0.0f)
                {
                    // Only a landing that actually HURT. A drop inside the safe
                    // distance deals nothing and makes no noise, which is what
                    // keeps the sound meaning something rather than firing on
                    // every hop off a kerb.
                    _audio?.PlayFallImpact();
                }
            }

            _audio?.UpdateFootsteps(
                _locomotion.CurrentSpeed > 0.01f, _locomotion.IsGrounded, _locomotion.IsSprinting);
        }

        /// <summary>
        /// The soulslike sword combat, or null when no sword has ever been
        /// equipped. Looked up lazily: SwordWeapon.OnEquip adds it at runtime.
        /// </summary>
        private SoulsSwordCombat SoulsSword
        {
            get
            {
                if (_soulsSword == null)
                {
                    _soulsSword = GetComponent<SoulsSwordCombat>();
                }
                return _soulsSword;
            }
        }
    }
}
