using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// THE FIGHTING KEYS: punches (turned to the target, a held punch made heavy), the sword spin's key and the soulslike sword.
    /// </summary>
    public sealed partial class PlayerInputReader
    {
        private float _lastYawDegrees;

        /// <summary>
        /// A punch goes where the body faces, and the body only turned to the camera while MOVING: standing still after turning the camera the punch went
        /// the way you used to face. So a punch turns the body, quickly, to the nearest enemy near where the camera looks (punch magnetism), else to the camera.
        /// </summary>
        private void OnMeleeSwingStarted(ComboStage stage)
        {
            if (_melee == null || _locomotion == null || _melee.EquippedWeapon != null || !FistStrikes.Has(stage.Animation))
            {
                return;
            }
            Transform yawSource = YawSource != null ? YawSource : (Camera.main != null ? Camera.main.transform : null);
            float cameraYaw = yawSource != null ? yawSource.eulerAngles.y : _lastYawDegrees;
            float yaw = _melee.TryFindMagnetYaw(cameraYaw, out float toTarget) ? toTarget : cameraYaw;
            _locomotion.SetFacingOverride(yaw, 1200.0f, 0.22f);
        }

        /// <summary>
        /// How long left-click has to be held before it becomes a heavy punch.
        ///
        /// Long enough that an ordinary click -- typically well under 150ms
        /// between press and release -- never rolls one by accident, short
        /// enough that deliberately holding does not feel like waiting for a
        /// laggy input to register.
        /// </summary>
        public const float HeavyPunchHoldSeconds = 0.4f;

        private bool _punchHeld;

        private float _punchHeldSeconds;

        /// <summary>The sword's left click is still down since the press that asked for a light, and for how long (a hold makes it the heavy).</summary>
        private bool _swordClickHeld;

        private float _swordClickHeldSeconds;

        /// <summary>A shield is worn on the off hand (docs/SHIELD.md).</summary>
        private bool ShieldInHand => _vitals != null && _vitals.Shield != null;

        /// <summary>The shield can be raised beside what is in the weapon hand (ShieldTraits.GoesBeside): right-click is the shield's then.</summary>
        private bool ShieldUsable => ShieldInHand && _vitals.ShieldTraits.GoesBeside(HeldNow());

        /// <summary>A shield that cannot cover and attack at once (ShieldTraits.OpenToAttackSeconds) stays out of its guard until this time.</summary>
        private float _shieldOpenUntil = float.NegativeInfinity;

        /// <summary>What is in the weapon hand, as a shield sees it.</summary>
        private HeldKind HeldNow()
        {
            if (EquippedSprayCan != null)
            {
                return HeldKind.SprayCan;
            }
            if (EquippedDrink != null)
            {
                return HeldKind.Other;
            }
            if (EquippedGun != null)
            {
                return _locomotion != null && _locomotion.HoldsPistol ? HeldKind.Pistol : HeldKind.LongGun;
            }
            if (EquippedStaff != null)
            {
                return _locomotion != null && _locomotion.Wand != null ? HeldKind.Wand : HeldKind.Staff;
            }
            if (EquippedFlamethrower != null || (_grappleSwing != null && _grappleSwing.EquippedHook != null))
            {
                return HeldKind.Other;
            }
            IEquippableWeapon weapon = _melee != null ? _melee.EquippedWeapon : null;
            if (weapon == null)
            {
                return HeldKind.BareHands;
            }
            if (weapon is IAimedThrow)
            {
                return HeldKind.Other;
            }
            // (A spear goes beside a shield: it is thrust with the right hand alone then, docs/SPEAR.md.)
            return weapon is ISwordBlade blade && !blade.IsOneHanded && !blade.IsKnife && !blade.IsSpear ? HeldKind.TwoHandedMelee : HeldKind.OneHandedMelee;
        }

        /// <summary>
        /// Punching, holding for a heavy one, and blocking.
        ///
        /// THE PUNCH FIRES ON RELEASE, NOT ON PRESS, which it has to: whether
        /// this was a jab or a haymaker is not known until the button comes
        /// back up. That is the cost of a hold-to-charge attack and it is the
        /// arrangement Godot uses.
        /// </summary>
        private void TickCombat(float deltaTime)
        {
            if (Mouse.current == null)
            {
                return;
            }

            // DEFAULTS OFF, SET BACK ON BELOW WHEREVER RIGHT-CLICK ACTUALLY
            // MEANS AIMING. Every early return below this line (carrying
            // something, the grapple hook equipped, unarmed) is a state
            // that is not aiming, so this only has to be re-armed by the
            // two branches that are.
            if (_orbitCamera != null)
            {
                _orbitCamera.IsAiming = false;
                _orbitCamera.PistolAim = _locomotion != null && _locomotion.HoldsPistol;
            }
            _isAimingGun = false;
            _throwAiming = false;
            if (_vitals != null)
            {
                _vitals.ShieldRaised = false;   // the same: down unless right-click holds it up below
            }

            // GLACIER SPIKES ARMED: the clicks are the spell's (left casts, right puts it away), not a weapon's.
            if (TakeGlacierSpikesClicks(Mouse.current))
            {
                return;
            }

            // Both hands are full while carrying something -- matches
            // player.gd's own "entirely while carrying something" guard on
            // punch/block input. The click that throws is not an attack either.
            if (_grabCarry != null && _grabCarry.HandsFull)
            {
                return;
            }

            // A SHIELD ON THE OFF HAND (docs/SHIELD.md): right-click held raises it -- bare-handed or with a one-handed weapon, instead of the
            // fists' timed block or the sword's guard. Not with a gun (right-click aims it) or a spray can in hand; the riot shield only beside a
            // one-handed weapon, a pistol included (nothing aims then), and out of its guard for a moment after every click that attacks.
            if (ShieldUsable)
            {
                float open = _vitals.ShieldTraits.OpenToAttackSeconds;
                if (open > 0.0f && Mouse.current.leftButton.isPressed)
                {
                    _shieldOpenUntil = Time.time + open;
                }
                _vitals.ShieldRaised = Mouse.current.rightButton.isPressed && Time.time >= _shieldOpenUntil;
            }

            // SCROLL WHEEL: put the weapon in hand away and take out the other (primary <-> secondary). Not while carrying (returned above), and not
            // mid-aim: a gun being aimed down would otherwise vanish out of the player's hands.
            if (_inventory != null && !(Mouse.current.rightButton.isPressed))
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f && Time.unscaledTime >= _nextWeaponSwitch)
                {
                    if (_inventory.CycleWeapon(gameObject))
                    {
                        _nextWeaponSwitch = Time.unscaledTime + 0.25f;
                    }
                }
            }

            // A SPRAY CAN (2026-10-15): right-click at a wall opens the graffiti canvas; nothing is punched or blocked with a can in hand.
            if (EquippedSprayCan != null)
            {
                if (Mouse.current.rightButton.wasPressedThisFrame)
                {
                    SprayRequested?.Invoke(this);
                }
                return;
            }

            // A DRINK (2026-11-23, docs/PHIZZ_CAN.md): a click drinks it; nothing is punched or blocked with a can in hand.
            if (EquippedDrink != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    EquippedDrink.Drink();
                }
                return;
            }

            // A GUN TAKES OVER LEFT/RIGHT-CLICK AND RELOAD ENTIRELY WHILE
            // EQUIPPED, matching player.gd's own separate `if _equipped_gun
            // != null` branch: semi-automatic (a fresh press fires once,
            // none of the fist system's hold-for-heavy tracking or cooldown
            // gating here -- the gun tracks its own internally), aiming is
            // continuous for as long as right is HELD (unlike block()'s own
            // one-shot press), and reload is its own key rather than sharing
            // one with anything else. Checked ahead of the grapple hook and
            // the generic weapon branch below since a gun is neither.
            // A PAIR OF PISTOLS (owner, 2026-11-19: "Each click fires a gun"): left-click the right hand's, right-click the left hand's, both
            // held both; nothing aims down the sights; R reloads the pair (the right-hand gun leads it).
            if (EquippedGun != null && EquippedLeftGun != null)
            {
                Transform pairYawSource = YawSource != null ? YawSource : (Camera.main != null ? Camera.main.transform : null);
                if (pairYawSource != null)
                {
                    if (EquippedGun.IsAutomatic ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        EquippedGun.Fire(pairYawSource.position, pairYawSource.forward, false, gameObject);
                    }
                    if (EquippedLeftGun.IsAutomatic ? Mouse.current.rightButton.isPressed : Mouse.current.rightButton.wasPressedThisFrame)
                    {
                        EquippedLeftGun.Fire(pairYawSource.position, pairYawSource.forward, false, gameObject);
                    }
                }
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    EquippedGun.TryReload();
                }
                return;
            }

            if (EquippedGun != null)
            {
                bool aiming = Mouse.current.rightButton.isPressed && !ShieldUsable;   // beside a riot shield right-click is the shield's: shot from the hip
                if (_orbitCamera != null)
                {
                    _orbitCamera.IsAiming = aiming;
                }
                _isAimingGun = aiming;

                Transform gunYawSource = YawSource != null
                    ? YawSource
                    : (Camera.main != null ? Camera.main.transform : null);

                if (EquippedGun is IDrawnWeapon drawn)
                {
                    // A drawn weapon (the bow, docs/ARCHERY.md) is told the trigger every frame: it draws while it is held and looses when it is let go.
                    if (gunYawSource != null)
                    {
                        drawn.Draw(Mouse.current.leftButton.isPressed, gunYawSource.position, gunYawSource.forward, aiming, gameObject, deltaTime);
                    }
                }
                else
                {
                    // An automatic (the AK-47) fires for as long as the button is held, at its own rate (the gun gates it); anything else once per press.
                    bool trigger = EquippedGun.IsAutomatic ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame;
                    if (trigger && gunYawSource != null)
                    {
                        EquippedGun.Fire(gunYawSource.position, gunYawSource.forward, aiming, gameObject);
                    }
                }
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    EquippedGun.TryReload();
                }
                return;
            }

            // A FLAMETHROWER TAKES OVER LEFT-CLICK ENTIRELY WHILE EQUIPPED,
            // same "exclusive dispatch" shape as EquippedGun just above --
            // but HELD, not a single press: see
            // IEquippableFlamethrower.Tick's own doc comment for why this
            // isn't folded into EquippedGun itself. isPressed rather than
            // wasPressedThisFrame is what makes the stream continuous for
            // as long as the button stays down.
            if (EquippedFlamethrower != null)
            {
                // A flashlight also switches on and off with F (the Godot game's key; docs/FLASHLIGHT.md), unless F is about to open the
                // card of an item in reach (ItemInspector).
                if (EquippedFlamethrower is ITorch torch && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame
                    && !(GetComponent<ItemInspector>() is ItemInspector inspector && inspector.ClaimsF))
                {
                    torch.Toggle();
                }
                Transform flameYawSource = YawSource != null
                    ? YawSource
                    : (Camera.main != null ? Camera.main.transform : null);
                if (flameYawSource != null)
                {
                    EquippedFlamethrower.Tick(Mouse.current.leftButton.isPressed,
                        flameYawSource.position, flameYawSource.forward, gameObject, deltaTime);
                }
                return;
            }

            // A STAFF TAKES OVER LEFT-CLICK (docs/STAFFS.md): held it charges (or, the ice staff, keeps shooting), let go it shoots, toward the
            // crosshair. Right-click stays the shield's, when there is one.
            if (EquippedStaff != null)
            {
                Transform staffYawSource = YawSource != null
                    ? YawSource
                    : (Camera.main != null ? Camera.main.transform : null);
                if (staffYawSource != null)
                {
                    EquippedStaff.Tick(Mouse.current.leftButton.isPressed, staffYawSource.position, staffYawSource.forward, gameObject, deltaTime);
                }
                return;
            }

            // THE GRAPPLING HOOK TAKES OVER LEFT-CLICK ENTIRELY WHILE
            // EQUIPPED, matching player.gd's own separate
            // `elif _equipped_grapple != null` branch. FIRING ONLY: once a
            // shot attaches, GrappleSwing.IsActive goes true and
            // PlayerInputReader.Update's own swing-active early return
            // means this whole method never runs again until the rope
            // drops -- releasing is handled entirely inside
            // GrappleSwing.Tick instead. Checked ahead of the `_melee ==
            // null` guard below since firing needs no fists at all.
            if (_grappleSwing != null && _grappleSwing.EquippedHook != null)
            {
                // THE SAME AIM POSE A PISTOL USES -- Requested: "use the
                // pistol animation for the grapple gun." Right-click raises
                // the arm via ApplyGunAimPose, the exact mechanism
                // EquippedGun's own branch above uses -- purely the visual;
                // firing itself is unaffected, a plain left-click launches
                // the rope whether or not right is held, the same way the
                // pistol fires regardless of whether `aiming` happens to be
                // true.
                bool aimingGrapple = Mouse.current.rightButton.isPressed;
                if (_orbitCamera != null)
                {
                    _orbitCamera.IsAiming = aimingGrapple;
                }
                _isAimingGun = aimingGrapple;

                if (Mouse.current.leftButton.wasPressedThisFrame && Camera.main != null)
                {
                    _grappleSwing.TryFire(Camera.main.transform.position, Camera.main.transform.forward);
                }
                return;
            }

            if (_melee == null)
            {
                return;
            }

            // A THROWABLE WEAPON TAKES OVER LEFT/RIGHT-CLICK ENTIRELY,
            // matching the bottle's own aim/melee/throw sequencing in
            // player.gd: hold right = aim, release right without ever
            // pressing left = cancel free, left while aiming = throw, left
            // while not aiming = melee swing. Nothing below this branch --
            // the ordinary block/punch-hold logic -- ever runs while a
            // throwable weapon is equipped.
            //
            // GATED ON weapon.CanThrow(), NOT JUST weapon != null -- a
            // sword can't be thrown (CanThrow() is false), so it has no
            // aim-to-throw sequence to run at all. Routing it through here
            // anyway was a real bug: right-click held it in a permanent
            // "aiming to throw" pose that could never resolve (nowhere for
            // Action.Throw to go), and left-click fired Action.Melee on the
            // very first frame of a press with no hold-for-heavy timing --
            // silently eating both the block button and the charged-attack
            // hold. A sword falls through to the ordinary dispatch below
            // instead, unchanged from bare fists: _melee.TryAttack()/
            // TryAttack(heavy: true) already branch on EquippedWeapon
            // internally (see MeleeAttacker's own doc comment), so nothing
            // here needs to know a sword is equipped at all -- it already
            // gets its own moveset/chain/heavy stage for free.
            IEquippableWeapon weapon = _inventory != null
                ? _inventory.EquippedInstance(_inventory.ActiveWeaponSlot)?.GetComponent<IEquippableWeapon>()
                : null;

            // THE SWORD IS FOUGHT THE SOULSLIKE WAY -- see SoulsSwordCombat.
            // It owns left-click, F, right-click and the roll outright, so
            // nothing below (the fist system's hold-for-heavy, the timed
            // block) runs while a sword is in hand.
            SoulsSwordCombat souls = SoulsSword;
            if (weapon != null && souls != null && souls.IsEquipped && weapon.MovesetId == ComboChain.Sword)
            {
                // A SWUNG THING THAT IS ALSO THROWN (a whole bottle, docs/GLASS_BOTTLE.md): right-click held is its aim, not a guard, and a
                // click from the aim throws it; with right-click up it is swung as any one-handed weapon is. Once it cannot be thrown (the
                // bottle broken), right-click is the guard again.
                if (weapon is IAimedThrow thrown && (weapon.CanThrow() || thrown.IsThrowing))
                {
                    if (TickSwungThrow(weapon, thrown, souls))
                    {
                        return;
                    }
                }
                TickSoulsSword(souls);
                return;
            }

            if (weapon != null && weapon.CanThrow())
            {
                WeaponAimInput.Action action = _weaponAim.Tick(
                    Mouse.current.rightButton.wasPressedThisFrame,
                    Mouse.current.rightButton.wasReleasedThisFrame,
                    Mouse.current.leftButton.wasPressedThisFrame);
                if (_orbitCamera != null)
                {
                    _orbitCamera.IsAiming = _weaponAim.IsAiming;
                }

                // A throwable with no melee use (a grenade, docs/GRENADE.md) is thrown on a plain click too: it is no club.
                bool throws = action == WeaponAimInput.Action.Throw
                    || (action == WeaponAimInput.Action.Melee && weapon.GetMeleeDamage() <= 0.0f);
                if (!weapon.InHand)
                {
                    // Away (a boomerang in the air): the hands are bare and a click punches. Right-click still counts toward the catch's aim.
                    if (action == WeaponAimInput.Action.Melee || action == WeaponAimInput.Action.Throw)
                    {
                        _melee.TryAttack();
                    }
                }
                else if (action == WeaponAimInput.Action.Melee && !throws)
                {
                    _melee.TryAttack();
                }
                else if (throws && weapon.CanThrow())
                {
                    Transform yawSource = YawSource != null
                        ? YawSource
                        : (Camera.main != null ? Camera.main.transform : null);
                    if (yawSource != null)
                    {
                        Vector3 direction = ThrowDirection(yawSource);
                        Vector3 origin = transform.position + Vector3.up * (_melee.OriginHeightFraction *
                            (_locomotion != null ? _locomotion.WorldHeight : 1.8f)) + direction * 0.5f;
                        weapon.Throw(gameObject, origin, direction);
                    }
                }
                // A weapon with its own aim draws itself into it and puts itself away (after the throw: a click from the aim throws from it).
                if (weapon is IAimedThrow aimedThrow)
                {
                    aimedThrow.SetAiming(gameObject, _weaponAim.IsAiming);
                    _throwAiming = _weaponAim.IsAiming;
                }
                return;
            }

            // BARE HANDS: right-click HELD is the guard (FistGuard, the sword's way: its first instant parries, the body walks on at half pace).
            if (weapon == null && !ShieldUsable)
            {
                _melee.SetGuardHeld(Mouse.current.rightButton.isPressed);
            }
            // A club in hand: right-click, timed, NOT holdable. A press throws the block, and the window is exactly as long as the clip.
            else if (Mouse.current.rightButton.wasPressedThisFrame && _vitals != null && !ShieldUsable)
            {
                _vitals.StartBlock();
                _melee.PlayBlock();
            }

            // THE SWORD'S OWN SPIN ATTACK -- explicit ask: "press 2 to use
            // it. its like links up special in smash bros he just spins
            // around with the sword for a bit". TrySpinAttack itself
            // refuses without a sword equipped (MeleeAttacker.EquippedWeapon
            // .MovesetId == ComboChain.Sword), so pressing 2 bare-handed or
            // with any other weapon silently does nothing -- same "ask,
            // decide nothing here" split digit1Key's own fireball check
            // above already uses, just scoped to melee instead of an
            // innate ability available regardless of loadout.
            TickSpinKey(Keyboard.current, canStart: true);

            // A PUNCH GOES OUT ON THE PRESS, not on the release. It used to wait for the button to come back up (to tell a tap from a hold), which put
            // 100 ms or more between the click and the fist: the "horrible" lag. Now the click throws the punch at once; holding the button on
            // past HeavyPunchHoldSeconds and letting go follows it with the overhand (buffered if the first punch is still going), and held
            // again in the overhand's window, the uppercut.
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                _punchHeld = true;
                _punchHeldSeconds = 0.0f;
                _melee.TryAttack();
            }
            else if (_punchHeld && Mouse.current.leftButton.isPressed)
            {
                _punchHeldSeconds += deltaTime;
            }

            if (_punchHeld && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _punchHeld = false;
                if (_punchHeldSeconds >= HeavyPunchHoldSeconds)
                {
                    _melee.TryAttack(heavy: true);
                }
            }
        }

        /// <summary>
        /// 2, the sword's spin (Link's Up Special): pressed on the ground it starts CHARGING for as long as the key is held (up to a
        /// second, for up to 1.6x the damage) and spins when let go; pressed in the air it goes at once, climbing. MeleeAttacker
        /// decides everything else, including that there is a sword in hand.
        /// </summary>
        private void TickSpinKey(Keyboard keyboard, bool canStart)
        {
            if (keyboard == null || _melee == null)
            {
                return;
            }
            // From whichever slot of the bar holds it (2 by default).
            if (canStart && _skills.Pressed(SkillCatalog.SwordSpin, keyboard))
            {
                _melee.TrySpinAttack(charge: true);
            }
            if (_melee.IsChargingSpin && !_skills.Held(SkillCatalog.SwordSpin, keyboard))
            {
                _melee.ReleaseSpinAttack();
            }
        }

        private SoulsSwordCombat _soulsSword;

        /// <summary>
        /// Left-click = light attack (pressed, not released: a soulslike
        /// swing starts on the click), right-click HELD = guard. The heavy
        /// attack has no key for now.
        /// Every swing goes where the camera looks.
        /// </summary>
        /// <summary>
        /// The aim and the throw of a swung weapon that can be thrown. True while it has the mouse (aiming, or the throw going): nothing is
        /// swung or guarded then.
        /// </summary>
        private bool TickSwungThrow(IEquippableWeapon weapon, IAimedThrow thrown, SoulsSwordCombat souls)
        {
            Mouse mouse = Mouse.current;
            bool canThrow = weapon.CanThrow() && !souls.IsCommitted;
            WeaponAimInput.Action action = _weaponAim.Tick(
                canThrow && mouse.rightButton.wasPressedThisFrame,
                mouse.rightButton.wasReleasedThisFrame || (_weaponAim.IsAiming && (!mouse.rightButton.isPressed || !weapon.CanThrow())),
                mouse.leftButton.wasPressedThisFrame);
            if (action == WeaponAimInput.Action.Throw && weapon.CanThrow())
            {
                Transform yawSource = YawSource != null ? YawSource : (Camera.main != null ? Camera.main.transform : null);
                if (yawSource != null)
                {
                    Vector3 direction = ThrowDirection(yawSource);
                    Vector3 origin = transform.position + Vector3.up * (_melee.OriginHeightFraction *
                        (_locomotion != null ? _locomotion.WorldHeight : 1.8f)) + direction * 0.5f;
                    weapon.Throw(gameObject, origin, direction);
                }
            }
            bool busy = _weaponAim.IsAiming || thrown.IsThrowing;
            if (_orbitCamera != null)
            {
                _orbitCamera.IsAiming = busy;
            }
            if (!thrown.IsThrowing)
            {
                thrown.SetAiming(gameObject, _weaponAim.IsAiming);
            }
            _throwAiming = busy;
            if (busy)
            {
                souls.SetBlockHeld(false);
            }
            return busy;
        }

        private void TickSoulsSword(SoulsSwordCombat souls)
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            Transform source = YawSource != null
                ? YawSource
                : (Camera.main != null ? Camera.main.transform : null);
            float yaw = source != null ? source.eulerAngles.y : transform.eulerAngles.y;

            souls.Locked = _locomotion != null && _locomotion.IsDashing;

            // The bare-handed spin attack (2) is still MeleeAttacker's own;
            // while it plays the souls swing stands aside rather than stacking.
            bool spinning = _melee != null && _melee.IsSwinging;

            // 6: OCTASLASH (Sephiroth's held up special), only here, with the sword in hand: a 2.5 s wind-up, then an eight-cut dash where the
            // camera looks. A left click during the wind-up sends it at once -- and is not also a swing.
            if (_octaslash == null)
            {
                _octaslash = GetComponent<OctaslashAbility>();
            }
            bool octaslashCharging = _octaslash != null && _octaslash.IsCharging;
            if (_octaslash != null && source != null)
            {
                float chest = (_locomotion != null ? _locomotion.WorldHeight : 1.8f) * 0.55f;
                Vector3 look = ConvergedDirection(transform.position + Vector3.up * chest, source);
                _octaslash.Aim(look);
                if (_skills.Pressed(SkillCatalog.Octaslash, keyboard) && !spinning && !souls.IsCommitted)
                {
                    _octaslash.TryStart(look);
                }
            }
            bool octaslashBusy = _octaslash != null && _octaslash.IsActive;
            spinning |= octaslashBusy;

            // THE STAB DASH (Power Thrust), from its slot on the bar with the sword in hand: a short wind-up, then a lunge a dash's length where
            // the camera looks, the blade thrust out. It holds the body from here (PlayerInputReader.Update), so a held guard is let go now.
            if (_stabDash != null && source != null && !spinning && !souls.IsCommitted && _skills.Pressed(SkillCatalog.StabDash, keyboard)
                && _stabDash.TryStart(source.forward))
            {
                souls.SetBlockHeld(false);
                return;
            }

            // LEFT CLICK: A TAP IS THE LIGHT, A HOLD THE HEAVY (owner, 2026-10-24: "make it so holding click does a heavy guardbreaking
            // attack"; it was the wheel's click): the overhead with a one-handed sword, the slam with a two-handed one. The light goes out on
            // the press; held on for SoulsSwordCombat.HeavyHoldSeconds it becomes the heavy. F stays unbound -- explicit ask.
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _swordClickHeld = false;
                if (octaslashCharging)
                {
                    _octaslash.Release();
                }
                else if (!spinning)
                {
                    souls.RequestLight(yaw);
                    _swordClickHeld = true;
                    _swordClickHeldSeconds = 0.0f;
                }
            }
            else if (_swordClickHeld && mouse.leftButton.isPressed)
            {
                _swordClickHeldSeconds += Time.deltaTime;
                if (_swordClickHeldSeconds >= SoulsSwordCombat.HeavyHoldSeconds)
                {
                    _swordClickHeld = false;
                    if (!spinning)
                    {
                        souls.HoldIntoHeavy(yaw);
                    }
                }
            }
            else
            {
                _swordClickHeld = false;
            }

            if (!octaslashBusy)
            {
                TickSpinKey(keyboard, canStart: !souls.IsCommitted);
            }

            // 7: THE SWORD'S WAVE (a Getsuga Tensho style crescent). The sword swings (a light attack) and a crescent of light leaves the
            // blade down the crosshair, exactly where the fireball would go. Only with the sword in hand, and not mid-swing.
            if (_skills.Pressed(SkillCatalog.SwordWave, keyboard) && _swordWave != null && source != null
                && !spinning && !souls.IsCommitted && _swordWave.CanCast)
            {
                float height = (_locomotion != null ? _locomotion.WorldHeight : 1.8f) * 0.68f;
                Vector3 origin = transform.position + Vector3.up * height;
                if (_swordWave.TryCast(height, ConvergedDirection(origin, source), GetComponentsInChildren<Collider>()))
                {
                    souls.RequestLight(yaw);
                }
            }

            souls.SetBlockHeld(mouse.rightButton.isPressed && !spinning && !ShieldUsable);   // with a shield, right-click is the shield's
        }
    }
}
