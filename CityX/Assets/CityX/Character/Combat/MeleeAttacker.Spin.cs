using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SWORD SPIN (key 2, sword in hand): Link's Spin Attack, his up special in Smash Ultimate. SwordSpinMove holds the timeline and
    /// docs/SWORD_SPIN.md the sources. On the ground, holding the key charges it in the cocked pose, and letting go spins four times in a
    /// low crouch, hitting everything round the body once. In the air it is a faster spin that climbs, hits eight times and launches with
    /// its last cut.
    ///
    /// THE BODY IS ANIMATED IN BLENDER from Smash's hitbox GIFs (CityX/SourceArt/SwordSpin) and played bone by bone after the Animator
    /// (BakedMotion, PlayerLocomotion.SetBakedPose). The turn is inside the animation, so the body's own facing never moves. Light trails
    /// from the real blade while it sweeps (BladeTrail).
    ///
    /// MULTIPLAYER (IReplicatedAbility, id "sword-spin"): the owner sends charging, turning (air or ground, and the charge) and ended. A copy
    /// plays the same animation and light on the same timeline from those, and hurts nothing.
    /// </summary>
    public sealed partial class MeleeAttacker
    {
        public const string SpinAbilityId = "sword-spin";

        string IReplicatedAbility.AbilityId => SpinAbilityId;

        /// <summary>The kinds of event the spin raises: charging began, the turn began (A is 1 for an air spin, B the charge in seconds), it ended.</summary>
        public const int SpinEventCharging = 1;

        public const int SpinEventTurning = 2;
        public const int SpinEventEnded = 3;

        /// <summary>Raised on the owner's copy as the sword spin charges, turns and ends. See IReplicatedAbility.</summary>
        public event System.Action<AbilityEvent> Fired;

        [Header("Sword Spin Attack (Link's Up Special -- see SwordSpinMove and docs/SWORD_SPIN.md)")]
        [Tooltip("How far the blade reaches from the body's middle: the sword (2.1 m) and the arm holding it out.")]
        public float SpinReachMetres = 2.8f;

        [Tooltip("Seconds after one spin before another can begin. Smash has none (the move's own lag is the cost); a short one stops a held key chaining them.")]
        public float SpinAttackCooldownSeconds = 0.4f;

        [Tooltip("Multiplier on every spin blow, on top of SwordSpinMove's own table.")]
        public float SpinAttackDamageScale = 1.0f;

        [Tooltip("How fast the aerial spin starts climbing, in metres per second. With its thinner gravity this carries the body about 4 m up.")]
        public float AirSpinRiseSpeed = 7.0f;

        [Tooltip("Gravity while the aerial spin turns, as a fraction of normal (Smash: 0.55).")]
        public float AirSpinGravityScale = 0.55f;

        [Tooltip("How much of its normal pace the body can steer at while the aerial spin turns (Smash: 0.5).")]
        public float AirSpinMoveFraction = 0.5f;

        [Tooltip("What the aerial spin's last blow throws its target at: upward speed, and the speed away from the spinner.")]
        public float AirSpinLaunchUp = 9.0f;

        public float AirSpinLaunchAway = 3.0f;

        [Tooltip("The light trailing from the blade as it spins (the sword spin's streak material, shared with Octaslash). Empty draws none.")]
        public Material SpinSlashMaterial;

        private enum SpinState
        {
            None,
            Charging,   // ground only: the key is held, the sword cocked
            Turning,    // the spin itself, through to when the body is handed back
            Landing,    // came down out of an aerial spin before it was over (on a copy: letting go of the pose)
        }

        private SpinState _spinState;
        private SpinKind _spinKind;
        private float _spinTime;
        private float _spinChargeSeconds;
        private float _spinChargeMultiplier = 1.0f;
        private int _nextPulse;
        private float _landingLagRemaining;
        private float _spinCooldownRemaining;
        private bool _airSpinUsed;
        private readonly HashSet<IDamageable> _spinHit = new HashSet<IDamageable>();

        private BakedMotion _spinMotion;
        private BakedPose _spinPose;
        private bool _spinMotionLoaded;
        private float _spinPoseWeight;
        private BladeTrail _spinSlash;

        /// <summary>
        /// The Animator is told to play "UpperEmpty" when a spin starts, which shows nothing new (the body is the animation's). What the call
        /// is for is PlayerAnimationDriver.PlayState's forced zero-delta evaluation (an Animator that has never run sits in its bind pose)
        /// and letting go of any swing still playing on the upper body.
        /// </summary>
        private static readonly ComboStage SpinAttackStage =
            new ComboStage(PlayerAnimationDriver.UpperEmptyStateName, 1.0f, 1.0f, 0.0f, false);

        /// <summary>True from the moment 2 is pressed until the body is handed back: charging, turning or landing.</summary>
        public bool IsSpinAttacking => _spinState != SpinState.None;

        /// <summary>True while a ground spin is being charged (the key held).</summary>
        public bool IsChargingSpin => _spinState == SpinState.Charging;

        /// <summary>True while the spin is actually turning (not charging, not landing).</summary>
        public bool IsSpinTurning => _spinState == SpinState.Turning;

        /// <summary>Which spin is under way (meaningful while <see cref="IsSpinAttacking"/>).</summary>
        public SpinKind CurrentSpinKind => _spinKind;

        /// <summary>How long the spin about to start (or in progress) keeps the body: the ground one's 74 frames, the air one's 91.</summary>
        public float SpinAttackSeconds => SwordSpinMove.CommitSeconds(_spinKind);

        /// <summary>Where the spin under way is on its timeline, seconds (a charged ground spin starts at its cocked frame).</summary>
        public float SpinElapsedSeconds => _spinTime;

        /// <summary>The spin's animation frame on the body, or null while it shows none (here or on another player's copy).</summary>
        public BakedPose SpinPoseShown => _spinPoseWeight > 0.0f ? _spinPose : null;

        /// <summary>How much of the body the spin's animation holds, 0 to 1.</summary>
        public float SpinPoseWeight => _spinPoseWeight;

        /// <summary>The light trailing from the blade (null until the first spin).</summary>
        public BladeTrail SpinSlash => _spinSlash;

        /// <summary>
        /// Asks to spin (sword only). Refuses while already swinging or spinning, or on cooldown. In the air it goes straight into the
        /// rising spin, once per time off the ground (a move that climbs, pressed again at the top, would be a way to fly). On the ground it
        /// waits in the cocked pose while <paramref name="charge"/> says the key is held (ReleaseSpinAttack lets it go), else goes at once.
        /// </summary>
        public bool TrySpinAttack(bool charge = false)
        {
            if (IsSwinging || (_vitals != null && _vitals.IsDead))
            {
                return false;
            }
            if (_spinCooldownRemaining > 0.0f)
            {
                return false;
            }
            if (EquippedWeapon == null || EquippedWeapon.MovesetId != ComboChain.Sword)
            {
                return false;
            }

            if (IsAirborne())
            {
                if (_airSpinUsed)
                {
                    return false;
                }
                BeginTurning(SpinKind.Air, 0.0f);
                return true;
            }

            _currentStage = SpinAttackStage;
            if (charge)
            {
                _spinState = SpinState.Charging;
                _spinKind = SpinKind.Ground;
                _spinChargeSeconds = 0.0f;
                _spinTime = 0.0f;
                _spinPoseWeight = 0.0f;
                SwingStarted?.Invoke(_currentStage);
                Fired?.Invoke(new AbilityEvent(SpinEventCharging, transform.position, transform.forward));
                return true;
            }
            BeginTurning(SpinKind.Ground, 0.0f);
            return true;
        }

        /// <summary>
        /// The key was let go: a ground spin that was being charged goes now, with whatever it charged. (It also goes by itself when the
        /// charge is full, as Link's does.) Does nothing when no charge is under way.
        /// </summary>
        public void ReleaseSpinAttack()
        {
            if (_spinState != SpinState.Charging)
            {
                return;
            }
            BeginTurning(SpinKind.Ground, _spinChargeSeconds);
        }

        private void BeginTurning(SpinKind kind, float chargeSeconds)
        {
            bool wasCharging = _spinState == SpinState.Charging;
            _spinState = SpinState.Turning;
            _spinKind = kind;
            _spinTime = SwordSpinMove.StartSeconds(kind, chargeSeconds);
            _nextPulse = 0;
            _spinHit.Clear();
            _spinChargeMultiplier = SwordSpinMove.ChargeMultiplier(chargeSeconds);
            _currentStage = SpinAttackStage;
            if (!wasCharging)
            {
                _spinPoseWeight = 0.0f;
                SwingStarted?.Invoke(_currentStage);
            }

            if (kind == SpinKind.Air)
            {
                _airSpinUsed = true;
                // Starts climbing at once, from whatever the body was doing (a fall is cut short, as in Smash), under thinner gravity.
                if (_locomotion != null)
                {
                    _locomotion.SetVerticalVelocity(AirSpinRiseSpeed);
                    _locomotion.GravityScale = AirSpinGravityScale;
                    // And keeps going the way it was going, through the spin and the rest of the fall (until it lands -- see Tick).
                    _locomotion.KeepsAirMomentum = true;
                }
            }
            BeginSpinSlash();
            Fired?.Invoke(new AbilityEvent(SpinEventTurning, transform.position, transform.forward,
                kind == SpinKind.Air ? 1.0f : 0.0f, chargeSeconds));
        }

        /// <summary>
        /// Feet off the ground: the controller says so AND there is no ground within a step below. The controller alone flickers to "not
        /// grounded" on stairs and kerbs, which would turn an ordinary spin into a leap.
        /// </summary>
        private bool IsAirborne()
        {
            if (_locomotion == null || _locomotion.IsGrounded)
            {
                return false;
            }
            Vector3 from = transform.position + Vector3.up * 0.4f;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, GroundProbe, 0.4f + AirborneClearanceMetres, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!GroundProbe[i].collider.transform.IsChildOf(transform))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>How far above the ground the body must be for a press of 2 to count as an aerial spin.</summary>
        private const float AirborneClearanceMetres = 0.35f;

        private static readonly RaycastHit[] GroundProbe = new RaycastHit[8];

        /// <summary>After the last blow of an aerial spin, how long gravity takes to come back to normal.</summary>
        private const float AirGravityReturnSeconds = 0.4f;

        private void TickSpin(float deltaTime)
        {
            if (EquippedWeapon == null || (_vitals != null && _vitals.IsDead))
            {
                EndSpin();
                return;
            }

            if (_spinState == SpinState.Charging)
            {
                _spinChargeSeconds += deltaTime;
                ShowSpinCharge(_spinChargeSeconds, deltaTime);
                if (_spinChargeSeconds >= SwordSpinMove.MaxChargeSeconds || IsAirborne())
                {
                    // Full charge goes by itself, and so does a charge whose ground was lost (an edge, a shove): no standing in the air.
                    BeginTurning(SpinKind.Ground, _spinChargeSeconds);
                }
                return;
            }

            if (_spinState == SpinState.Landing)
            {
                FadeSpinPose(deltaTime);
                _landingLagRemaining -= deltaTime;
                if (_landingLagRemaining <= 0.0f)
                {
                    EndSpin();
                }
                return;
            }

            float before = _spinTime;
            _spinTime += deltaTime;
            ShowSpin(_spinKind, _spinTime, deltaTime);

            if (_spinKind == SpinKind.Air)
            {
                if (_locomotion != null)
                {
                    // Thin gravity while it turns, then back to normal OVER A MOMENT: snapping it back at the last blow, with the body
                    // still rising, stalled the climb at its top, which read as the spin stopping the body in mid-air.
                    float sinceTurn = _spinTime - SwordSpinMove.AirActiveSeconds;
                    float back = sinceTurn <= 0.0f ? 0.0f : Mathf.SmoothStep(0.0f, 1.0f, sinceTurn / AirGravityReturnSeconds);
                    _locomotion.GravityScale = Mathf.Lerp(AirSpinGravityScale, 1.0f, back);
                    // A ceiling stops the climb outright; otherwise it would keep its speed and press on the roof.
                    if ((_locomotion.LastMoveCollisionFlags & CollisionFlags.Above) != 0 && _locomotion.VerticalVelocity > 0.0f)
                    {
                        _locomotion.SetVerticalVelocity(0.0f);
                    }
                }

                var pulses = SwordSpinMove.AirPulses;
                while (_nextPulse < pulses.Length && _spinTime >= pulses[_nextPulse].Time)
                {
                    LandSpinBlow(pulses[_nextPulse].DamageScale, pulses[_nextPulse].Final, oncePerTarget: false);
                    _nextPulse++;
                }

                // Coming down before the animation is over costs the landing lag.
                if (_locomotion != null && _spinTime > 0.15f && _locomotion.IsGrounded && _locomotion.VerticalVelocity <= 0.0f
                    && _spinTime < SwordSpinMove.AirCommitSeconds)
                {
                    _spinState = SpinState.Landing;
                    _landingLagRemaining = SwordSpinMove.AirLandingLagSeconds;
                    _locomotion.GravityScale = 1.0f;
                    _locomotion.KeepsAirMomentum = false;
                    _spinSlash?.Stop();
                    Fired?.Invoke(new AbilityEvent(SpinEventEnded, transform.position, transform.forward));
                    return;
                }
            }
            else if (_spinTime >= SwordSpinMove.GroundHitStart && before <= SwordSpinMove.GroundHitEnd)
            {
                // Every frame of the window: each target is hit once, by whatever the blow is worth at the moment it first connects.
                float sampled = Mathf.Clamp(_spinTime, SwordSpinMove.GroundHitStart, SwordSpinMove.GroundHitEnd);
                LandSpinBlow(SwordSpinMove.GroundDamageScale(sampled) * _spinChargeMultiplier, false, oncePerTarget: true);
            }

            if (_spinTime >= SwordSpinMove.CommitSeconds(_spinKind))
            {
                EndSpin();
            }
        }

        /// <summary>
        /// A stagger cuts a spin's charge short (the Stability stat's "harder to interrupt"): it ends without spinning, and other players see
        /// it end. A spin already turning carries on (it is the blow, not the wind-up).
        /// </summary>
        public void InterruptSpinCharge()
        {
            if (_spinState == SpinState.Charging)
            {
                EndSpin();
            }
        }

        private void EndSpin()
        {
            bool wasSpinning = _spinState != SpinState.None;
            _spinState = SpinState.None;
            _landingLagRemaining = 0.0f;
            _spinCooldownRemaining = SpinAttackCooldownSeconds * PlayerVitals.CooldownFactorOf(_vitals);   // Cooldown Reduction shortens it
            if (_locomotion != null)
            {
                _locomotion.GravityScale = 1.0f;
            }
            ReleaseSpinPose();
            _spinSlash?.Stop();
            if (wasSpinning)
            {
                Fired?.Invoke(new AbilityEvent(SpinEventEnded, transform.position, transform.forward));
            }
        }

        // ---- the look, the same on the owner's body and on every copy ----------------------------------------------------------

        /// <summary>The charge: the wind-up's first frames, then the cocked pose held (looped) for as long as the key is.</summary>
        private void ShowSpinCharge(float seconds, float deltaTime)
        {
            StepSpinPoseWeight(true, deltaTime);
            if (seconds < SwordSpinMove.ChargeFrameSeconds)
            {
                ShowSpinFrame(SwordSpinMove.GroundSegment, seconds, false);
            }
            else
            {
                ShowSpinFrame(SwordSpinMove.ChargeSegment, seconds - SwordSpinMove.ChargeFrameSeconds, true);
            }
        }

        /// <summary>The spin <paramref name="seconds"/> into its timeline, letting go of the body over the last frames before it hands it back.</summary>
        private void ShowSpin(SpinKind kind, float seconds, float deltaTime)
        {
            StepSpinPoseWeight(seconds < SwordSpinMove.CommitSeconds(kind) - SwordSpinMove.PoseBlendOutSeconds, deltaTime);
            ShowSpinFrame(SwordSpinMove.Segment(kind), seconds, false);
            if (_spinSlash != null && seconds >= SwordSpinMove.SlashEndSeconds(kind))
            {
                _spinSlash.Stop();
            }
        }

        private void StepSpinPoseWeight(bool holding, float deltaTime)
        {
            float rate = 1.0f / (holding ? SwordSpinMove.PoseBlendInSeconds : SwordSpinMove.PoseBlendOutSeconds);
            _spinPoseWeight = Mathf.MoveTowards(_spinPoseWeight, holding ? 1.0f : 0.0f, rate * deltaTime);
        }

        private void ShowSpinFrame(string segment, float seconds, bool loop)
        {
            if (_locomotion == null || !SpinMotionLoaded())
            {
                return;
            }
            _spinMotion.Sample(segment, seconds, loop, _spinPose);
            _locomotion.SetBakedPose(_spinPose, Mathf.SmoothStep(0.0f, 1.0f, _spinPoseWeight));
        }

        /// <summary>Lets go of the body over the blend out, holding the last frame shown. False once it has let go.</summary>
        private bool FadeSpinPose(float deltaTime)
        {
            StepSpinPoseWeight(false, deltaTime);
            if (_spinPoseWeight <= 0.0f)
            {
                ReleaseSpinPose();
                return false;
            }
            if (_locomotion != null && _spinPose != null)
            {
                _locomotion.SetBakedPose(_spinPose, Mathf.SmoothStep(0.0f, 1.0f, _spinPoseWeight));
            }
            return true;
        }

        private void ReleaseSpinPose()
        {
            _spinPoseWeight = 0.0f;
            if (_locomotion != null)
            {
                _locomotion.SetBakedPose(null, 0.0f);
            }
        }

        private bool SpinMotionLoaded()
        {
            if (!_spinMotionLoaded)
            {
                _spinMotionLoaded = true;
                _spinMotion = BakedMotion.Load(SwordSpinMove.MotionResource);
                _spinPose = _spinMotion?.NewPose();
                if (_spinPose != null)
                {
                    _spinPose.ThrustGrip = true;   // posed with it in Blender (spin_build_all.py), so every sword's blade sweeps level
                }
            }
            return _spinMotion != null;
        }

        /// <summary>Starts the light trailing from the blade (on a copy, from the sword mounted in its hand).</summary>
        private void BeginSpinSlash()
        {
            ISwordBlade blade = SpinSlashMaterial != null ? BladeInHand() : null;
            if (blade == null)
            {
                return;
            }
            if (_spinSlash == null)
            {
                _spinSlash = GetComponent<BladeTrail>();   // shared with Octaslash; not ??, a missing component is a Unity "fake null"
                if (_spinSlash == null)
                {
                    _spinSlash = gameObject.AddComponent<BladeTrail>();
                }
            }
            _spinSlash.Begin(blade, SpinSlashMaterial);
        }

        // ---- the spin on a copy of the body someone else owns ----------------------------------------------------------------
        // The rise is ordinary movement and arrives with the body's own transform; the look does not. A copy runs no input and no Tick, so it
        // plays the same timeline here from the events the owner raised, with no hitting, no cooldown and nothing sent back.

        private SpinState _remoteSpin;
        private SpinKind _remoteKind;
        private float _remoteTime;

        /// <summary>True while this copy of a remote player's body is showing their spin (charging or turning; not while letting go after it).</summary>
        public bool IsShowingRemoteSpin => _remoteSpin == SpinState.Charging || _remoteSpin == SpinState.Turning;

        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case SpinEventCharging:
                    _remoteSpin = SpinState.Charging;
                    _remoteKind = SpinKind.Ground;
                    _remoteTime = 0.0f;
                    _spinPoseWeight = 0.0f;
                    break;
                case SpinEventTurning:
                    if (_remoteSpin != SpinState.Charging)
                    {
                        _spinPoseWeight = 0.0f;
                    }
                    _remoteSpin = SpinState.Turning;
                    _remoteKind = evt.A > 0.5f ? SpinKind.Air : SpinKind.Ground;
                    _remoteTime = SwordSpinMove.StartSeconds(_remoteKind, evt.B);
                    BeginSpinSlash();
                    break;
                case SpinEventEnded:
                    EndRemoteSpin();
                    break;
            }
        }

        private void Update()
        {
            if (_remoteSpin != SpinState.None)
            {
                TickRemoteSpin(Time.deltaTime);
            }
        }

        /// <summary>Advances the remote spin's look by a step; public so a test can drive it without frames.</summary>
        public void TickRemoteSpin(float deltaTime)
        {
            switch (_remoteSpin)
            {
                case SpinState.None:
                    return;
                case SpinState.Charging:
                    // Held for as long as the owner holds the key (at most a full charge, then it goes by itself); a lost "ended" event
                    // cannot strand the pose.
                    _remoteTime += deltaTime;
                    ShowSpinCharge(_remoteTime, deltaTime);
                    if (_remoteTime > SwordSpinMove.MaxChargeSeconds + 1.0f)
                    {
                        EndRemoteSpin();
                    }
                    return;
                case SpinState.Landing:
                    if (!FadeSpinPose(deltaTime))
                    {
                        _remoteSpin = SpinState.None;
                    }
                    return;
            }
            _remoteTime += deltaTime;
            ShowSpin(_remoteKind, _remoteTime, deltaTime);
            if (_remoteTime >= SwordSpinMove.CommitSeconds(_remoteKind))
            {
                EndRemoteSpin();
            }
        }

        /// <summary>The owner's spin ended: the light stops, and the pose lets go over the blend out (an air spin that landed early ends mid-pose).</summary>
        private void EndRemoteSpin()
        {
            _remoteTime = 0.0f;
            _spinSlash?.Stop();
            _remoteSpin = _spinPoseWeight > 0.0f ? SpinState.Landing : SpinState.None;
            if (_remoteSpin == SpinState.None)
            {
                ReleaseSpinPose();
            }
        }

        private void OnDisable()
        {
            if (_spinState != SpinState.None)
            {
                EndSpin();
            }
            if (_locomotion != null)
            {
                _locomotion.KeepsAirMomentum = false;
            }
        }

        // ---- the blows ----------------------------------------------------------------------------------------------------------

        /// <summary>
        /// One blow of the spin, FULL CIRCLE round the body out to the blade's reach (SpinReachMetres), unlike an ordinary swing's forward
        /// arc. No combo, glass or block bookkeeping: TrySpinAttack already guaranteed a sword.
        /// </summary>
        /// <param name="scale">Multiplier on the sword's damage for this blow (SwordSpinMove's table, with the charge folded in).</param>
        /// <param name="final">The aerial spin's last blow: launches whatever can be launched.</param>
        /// <param name="oncePerTarget">The ground spin hits each target only once (the first frame it connects is what sets the damage).</param>
        private void LandSpinBlow(float scale, bool final, bool oncePerTarget)
        {
            // The sword can be unequipped while the spin it started is still running.
            if (EquippedWeapon == null || scale <= 0.0f)
            {
                return;
            }
            int strength = _vitals != null ? _vitals.Strength : 0;
            float damage = CombatTuning.DamageFor(EquippedWeapon.GetMeleeDamage() * SpinAttackDamageScale * scale, strength) * BloodrageFactor;

            bool connected = false;
            Vector3 origin = SwingOrigin();
            foreach (Contact contact in ContactsInReach(fullCircle: true, reach: SpinReachMetres))
            {
                if (oncePerTarget && _spinHit.Contains(contact.Target))
                {
                    continue;
                }
                HitResponse response = DamageContext.Strike(contact.Target, damage, transform.position, CombatTuning.HitstunSeconds, DamageKind.Physical, Penetration, contact.Point);
                if (response == HitResponse.Ignored)
                {
                    continue;
                }
                if (oncePerTarget)
                {
                    _spinHit.Add(contact.Target);
                }

                Landed?.Invoke(contact.Target, damage);
                connected = true;

                if (final && contact.Target.IsAlive && contact.Target is ILaunchable launchable)
                {
                    Vector3 away = contact.Point - transform.position;
                    away.y = 0.0f;
                    away = away.sqrMagnitude > 1e-4f ? away.normalized : transform.forward;
                    launchable.Launch(away * AirSpinLaunchAway + Vector3.up * AirSpinLaunchUp);
                }

                if (!contact.Target.IsAlive && contact.Target is IXpReward xpSource)
                {
                    _vitals?.AddXp(xpSource.XpReward);
                }

                if (response == HitResponse.Blocked)
                {
                    continue;
                }

                if (!(contact.Target is PlayerVitals))
                {
                    BloodBurst.Spawn(contact.Point, contact.Point - origin, 1.0f);
                }
            }

            if (connected)
            {
                PlayImpact();
                EquippedWeapon?.RegisterLandedMeleeHit();
            }
        }
    }
}
