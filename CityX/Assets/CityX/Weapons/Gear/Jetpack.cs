using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A basic jetpack: hold Space to thrust straight up (with WASD still
    /// steering horizontally, exactly as ordinary falling already lets you),
    /// let go to fall normally. No Godot equivalent to port -- an explicit
    /// new-mechanic ask, same footing as PlayerLocomotion's own
    /// ChargedJumpHeightMultiplier.
    ///
    /// REPLACES THE CHARGED JUMP ENTIRELY WHILE EQUIPPED, not layered on top
    /// of it -- PlayerInputReader checks EquippedJetpack before calling
    /// TickJumpCharge at all, matching how a gun/grapple hook already take
    /// Space/click over from the ordinary combat dispatch the instant they
    /// are equipped. Space meaning two different things depending on what
    /// is in your hands is already this project's own convention (aim vs.
    /// melee vs. gun trigger); this is the same idea for the jump key.
    ///
    /// INFINITE FUEL, on purpose -- the explicit ask says so outright
    /// ("for now just make the jetpack have infinite fuel"), so there is no
    /// resource to track or drain here at all, matching how ChargedJump
    /// itself carries no stamina cost either.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Jetpack : MonoBehaviour, IEquippableItem, IJetpack, ICosmeticMountable
    {
        public string ItemId => ItemRegistry.Jetpack;

        public const float InteractRange = 2.5f;

        /// <summary>
        /// Faster than PlayerLocomotion.JumpVelocity (4.5) -- a jump is a
        /// brief arc, this has to read as sustained powered flight for as
        /// long as Space stays down, which needs more than jump-height
        /// speed to feel like thrust rather than a very long hang time.
        ///
        /// Requested: "make the default speed the speed that holding
        /// shift goes now" -- was 7.0, the plain (non-boosted) climb speed;
        /// now equal to the OLD BoostThrustSpeed (11.0), so ordinary
        /// thrust already climbs as fast as a boost used to.
        /// </summary>
        public const float ThrustSpeed = 11.0f;

        /// <summary>
        /// Vertical speed while boosting with no WASD direction held --
        /// "if not holding wasd keys then just make it go up faster",
        /// from the explicit ask. Only applies with nothing to lean
        /// toward; holding a direction spends the boost on lean instead
        /// (see TickTilt's own doc comment).
        ///
        /// Requested, same pass as ThrustSpeed's own: "make holding
        /// shift double as fast as that" -- double the NEW ThrustSpeed
        /// (11.0), not the old one, so boosting still means something
        /// once plain thrust is this much faster than it used to be.
        /// </summary>
        public const float BoostThrustSpeed = ThrustSpeed * 2.0f;

        /// <summary>
        /// Same multiplier as PlayerLocomotion.JetpackDiveSpeedMultiplier,
        /// kept here too rather than reached across classes for it --
        /// applied to the pitch-derived climb/dive rate in
        /// ClimbVelocityFor so a full W+shift dive climbs or descends
        /// exactly as much faster as it already travels horizontally,
        /// instead of flying at 2x forward speed but the ordinary 1x
        /// vertically.
        /// </summary>
        public const float DiveClimbMultiplier = 2.0f;

        /// <summary>
        /// Sits flush against the spine, between the shoulder blades --
        /// pushed out to -0.34, not just past the torso's own ~0.26 radius
        /// (FusedBodyShape.TorsoRadius), confirmed necessary by rendering
        /// it at -0.20 first and finding the tanks visibly sunk into the
        /// back rather than resting on it.
        /// </summary>
        public static readonly Vector3 BackPosition = new Vector3(0.0f, 0.02f, -0.34f);

        public static readonly Vector3 BackRotationEuler = Vector3.zero;

        /// <summary>
        /// How far the body leans at full WASD deflection, in degrees --
        /// "tilts SLIGHTLY", from the explicit ask, so this stays well
        /// short of anything that would read as falling over.
        /// </summary>
        public const float TiltMaxDegrees = 12.0f;

        /// <summary>
        /// How far the body leans instead, while boosting with a WASD
        /// direction held -- "tilt a little further in whatever direction
        /// you are holding", from the explicit ask. Still short of anything
        /// reading as falling over, just more pronounced than the plain lean.
        /// </summary>
        public const float BoostTiltMaxDegrees = 20.0f;

        /// <summary>
        /// While diving (W+shift together), A/D banks the whole body into
        /// the strafe -- Requested: "if your in superman position and
        /// you press A or D the model rolls slightly in that direction,"
        /// refined once actually seen in real play: the first version
        /// capped this at the plain TiltMaxDegrees (12 degrees) to read as
        /// a "slight" bank, but confirmed directly (a real equipped-body
        /// screenshot comparison, not just the isolated TiltTarget numbers)
        /// that 12 degrees is simply too small a rotation to read as
        /// rolling at all against this body's own curled dive silhouette --
        /// "still the same... not rolling like a plane." SupermanRollMaxDegrees
        /// is deliberately LARGER than the ordinary BoostTiltMaxDegrees
        /// strafe-lean (20) specifically because a real plane's own roll is
        /// a decisive, unmistakable motion, not a subtle lean -- confirmed
        /// visually distinct from a still pose at this size where 12
        /// degrees was not.
        /// </summary>
        public const float SupermanRollMaxDegrees = 35.0f;

        /// <summary>
        /// SECOND Requested, refining the first: "if you are holding
        /// shift and w... plus also looking forward, he should be
        /// horizontal like superman flies. if looking straight up he
        /// should be stood up straight normally. if looking down he
        /// should be head down feet in the air, straight vertically."
        ///
        /// Added to cameraPitchDegrees ONLY while actually flying forward
        /// (see TiltTarget's own doc comment for why a plain hover uses no
        /// offset at all any more) -- three data points, one straight line:
        /// camera -90 (straight up) -> body 0
        /// (upright); camera 0 (forward) -> body 90 (fully horizontal,
        /// confirmed the meaning of 90 here by the FIRST explicit ask this
        /// same feature already used that exact value for: "tilts all the
        /// way horizontally"); camera +90 (straight down) -> body 180 (a
        /// full extra 90 past horizontal, in the SAME rotating direction --
        /// nose continuing past level into head-down-feet-up). All three
        /// fall on cameraPitchDegrees + 90 exactly, with no clamp needed
        /// afterward since JetpackMinPitchDegrees/MaxPitchDegrees already
        /// bound the input to +/-90, which maps the output to exactly
        /// [0, 180].
        ///
        /// THIRD Correction -- this used to apply only while
        /// boosting (the "committed Superman dive"), leaving plain forward
        /// thrust (no shift) with a raw, unclamped camera-follow pitch
        /// instead: "when holding space and w without shift... if you look
        /// all the way up its like your character is lying on his back...
        /// looking all the way down it is rotated too far as well." The
        /// SAME three-point mapping is exactly what "just moving up/down"
        /// needed there too -- standing straight at the top (flames
        /// pointing straight down), a headstand at the bottom -- so this
        /// applied to ANY forward-held thrust, boosted or not.
        ///
        /// FOURTH Correction (SUPERSEDED, see the fifth below) --
        /// even THAT was still one gap short: a plain hover (Space alone)
        /// had no forward component to trigger the offset with, so it kept
        /// the old unclamped camera-follow. Briefly made unconditional
        /// inside TiltTarget to close that gap.
        ///
        /// FIFTH Correction (SUPERSEDED, see the sixth below) --
        /// making it unconditional over-fixed the fourth: "now hold space
        /// defaults to superman position make it default to standing
        /// upright." A LEVEL camera pitched a plain hover to a full 90
        /// (horizontal) just from holding Space with nothing else, because
        /// the unconditional version forced the SAME symmetric sweep flying
        /// forward needs onto a hover. Given a hover its own
        /// `2 * Clamp(camera, 0, 90)` curve instead: upright looking level
        /// or up, tipping toward a headstand only once actually looking
        /// down.
        ///
        /// SIXTH Correction -- even that partial tip while looking
        /// down was one tilt too many: "make it so if your just holding
        /// space or holding shift+space no w with jetpack then the
        /// character just stay upright and doesnt tilt forward when you
        /// look down." A hover has no direction of travel for the body to
        /// point along at all, so it no longer uses this offset in any
        /// form -- pitch is simply 0 (standing upright), unconditionally,
        /// for as long as W is not held. This offset now only ever applies
        /// while actually flying forward (<paramref name="flyingForward"/>
        /// in TiltTarget) -- see TiltTarget's own doc comment. Roll's own
        /// magnitude is untouched by any of this.
        /// </summary>
        public const float SupermanPitchOffsetDegrees = 90.0f;

        /// <summary>
        /// How far the camera is allowed to look, while the jetpack is the
        /// equipped item -- Requested: "make it so you can go all the
        /// vertical upward and downward." ThirdPersonCamera's own ordinary
        /// MinPitch/MaxPitch (-35/70) exist for looking around on foot,
        /// where straight up or straight down would either clip into the
        /// ground or stare at the sky uselessly; flying is exactly the
        /// situation where a player actually wants to point the camera
        /// near-vertical, since ClimbVelocityFor already derives full climb/
        /// dive speed from -Sin(pitch) and was always mathematically ready
        /// for the full range -- only the camera's own clamp was stopping
        /// it from ever being reached.
        ///
        /// THE TRUE +/-90, not just short of it -- SupermanPitchOffsetDegrees'
        /// own three anchor poses (upright/horizontal/inverted) only land
        /// exactly on 0/90/180 if the camera itself can actually reach
        /// exactly straight up and straight down; stopping short by even a
        /// few degrees would mean "looking straight up" never quite finishes
        /// standing the body all the way back upright. ThirdPersonCamera
        /// builds its rotation from a plain Quaternion.Euler(pitch, yaw, 0)
        /// rather than a LookRotation(forward, up), so there is no
        /// forward-parallel-to-up degeneracy at the pole the way the
        /// original -85/85 margin was guarding against -- yaw simply
        /// becomes visually meaningless for the one frame the camera sits
        /// exactly at the pole, same as any first/third-person camera.
        /// </summary>
        public const float JetpackMinPitchDegrees = -90.0f;

        public const float JetpackMaxPitchDegrees = 90.0f;

        /// <summary>
        /// Roughly how long the lean takes to settle into a new target, in
        /// seconds, via Mathf.SmoothDamp -- Correction: this used
        /// to be a plain constant-rate Mathf.MoveTowards ramp
        /// (TiltDegreesPerSecond, 90 degrees/second), which reaches its
        /// target at a constant angular velocity and then stops dead the
        /// instant it arrives -- a real discontinuity in speed, not just a
        /// fast one, which is exactly what reads as the body "jumping into
        /// place" rather than settling into the lean. SmoothDamp's own
        /// critically-damped curve eases out approaching the target the
        /// same way it eases in leaving the last one, so both ends of the
        /// motion are gradual. Also slower than the old rate on top of
        /// that, per the explicit ask -- the old constant rate reached the
        /// full 12-degree lean in 12/90 = 0.13s; this settles close to a
        /// target in roughly 2-3x TiltSmoothTime, noticeably more relaxed
        /// without reading as sluggish.
        /// </summary>
        public const float TiltSmoothTime = 0.25f;

        [Tooltip("Set by JetpackBuilder -- plays while thrusting, stopped otherwise.")]
        public ParticleSystem FlameEffect;

        [Tooltip("Set by JetpackBuilder (jetpack_thrust_loop.wav) -- loops while thrusting, stopped otherwise.")]
        public AudioSource ThrustAudio;

        /// <summary>
        /// Set by JetpackBuilder (jetpack_wind_loop.wav) -- Requested:
        /// "add this wind noise as a looping sound when flying through the
        /// air at shift speed using jetpack." WAV, not MP3 like ThrustAudio
        /// -- the source clip was built into a seamless loop with an exact,
        /// sample-aligned crossfade at the splice point, and MP3 encoding
        /// (LAME specifically) pads a real, audible gap of silence at the
        /// start of the file (encoder delay, confirmed directly: encoding
        /// the same finished loop to MP3 measured roughly 46ms of added
        /// leading silence) -- looping THAT would reintroduce exactly the
        /// click/gap the crossfade was built to eliminate. WAV carries raw
        /// PCM with no codec framing delay, so the loop point stays
        /// exactly what it was built to be.
        /// </summary>
        public AudioSource WindAudio;

        /// <summary>
        /// Follow-up request: "make the wind slowly fade in and out as
        /// it begins and stops when you enter and exit shift jetpack mode."
        /// A plain Play()/Stop() (the ordinary FlameEffect/ThrustAudio
        /// pattern) starts/stops instantly, which reads fine for an engine
        /// loop or a flame but was reported as too abrupt for something
        /// meant to feel like the air building up/dying down around the
        /// body. Seconds to cross the WHOLE 0..1 range -- entering boost
        /// with the fade already partway through (from a very recent
        /// release) ramps the REMAINING distance at the same rate, not a
        /// fixed duration regardless of where it started, so a rapid
        /// tap-tap-tap of shift never sounds slower than a single sustained
        /// boost would.
        /// </summary>
        public const float WindFadeSeconds = 0.5f;

        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private bool _equipped;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private Transform _animatorTransform;

        private ThirdPersonCamera _orbitCamera;
        private float _savedMinPitch;
        private float _savedMaxPitch;

        // --- flight feel (see FlightFeel): how the camera reacts to speed ---
        private float _feelFov;
        private float _feelFovVelocity;
        private float _feelDistance;
        private float _feelDistanceVelocity;
        private float _feelKickAge = 1.0e9f;
        private bool _feelWasBoosting;
        private JetpackSpeedLines _speedLines;

        /// <summary>The camera's current flight widening, degrees -- for tests and tuning.</summary>
        public float FeelFovBonus => _feelFov;

        /// <summary>The camera's current flight pull-back, metres -- for tests and tuning.</summary>
        public float FeelDistanceBonus => _feelDistance;

        /// <summary>The camera the flight feel drives. Set on equip; a test can point it at its own.</summary>
        public ThirdPersonCamera FeelCamera
        {
            get => _orbitCamera;
            set => _orbitCamera = value;
        }

        /// <summary>Current eased lean, X = pitch (forward/back), Z = roll (strafe).</summary>
        private Vector2 _currentTilt;

        /// <summary>SmoothDamp's own persistent per-axis rate of change -- see EaseTilt's own doc comment for why this has to be carried between calls rather than derived fresh each frame.</summary>
        private Vector2 _tiltVelocity;

        /// <summary>Exposed for testing without a real Animator -- see TickTilt's own doc comment.</summary>
        public Vector2 CurrentTilt => _currentTilt;

        private void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
        }

        private void Update()
        {
            if (_equipped)
            {
                return;
            }
            TickPickup();
        }

        private void OnTriggerEnter(Collider other)
        {
            Transform body = InteractionReach.LocalPlayerBody(other);
            if (body != null)
            {
                _nearbyPlayer = body;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (InteractionReach.IsLeaving(other, _nearbyPlayer))
            {
                _nearbyPlayer = null;
            }
        }

        /// <summary>Proximity + E, matching every other pickup item's own convention.</summary>
        private void TickPickup()
        {
            if (_nearbyPlayer == null)
            {
                return;
            }
            if (!InteractKey.Tapped)
            {
                return;
            }
            if (Vector3.Distance(transform.position, _nearbyPlayer.position) > InteractRange)
            {
                return;
            }

            var inventory = _nearbyPlayer.GetComponentInParent<Inventory>();
            if (inventory != null && inventory.PickUp(ItemRegistry.Jetpack))
            {
                Destroy(gameObject);
            }
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            var animator = player.GetComponentInChildren<Animator>();
            Transform spine = animator != null ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            if (spine != null)
            {
                transform.SetParent(spine, false);
                transform.localPosition = BackPosition;
                transform.localEulerAngles = BackRotationEuler;
            }
            // The whole visual body, leaned as one rigid piece -- nothing
            // else drives this transform's own localRotation (root motion
            // is off, so animation clips only move bones under it), the
            // same reason PlayerLocomotion's own ApplyHeight is free to
            // drive its localScale unopposed.
            _animatorTransform = animator != null ? animator.transform : null;
            _currentTilt = Vector2.zero;
            _tiltVelocity = Vector2.zero;

            _equipped = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }

            _locomotion = player.GetComponent<PlayerLocomotion>();
            _vitals = player.GetComponent<PlayerVitals>();

            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedJetpack = this;
                _orbitCamera = input.OrbitCamera;
            }

            // Widened for the duration of wearing the jetpack, not just
            // while actively thrusting -- a player wants to be able to look
            // where they are about to climb or dive BEFORE pressing thrust,
            // not have the camera itself refuse to point there yet. See
            // JetpackMinPitchDegrees's own doc comment.
            if (_orbitCamera != null)
            {
                _savedMinPitch = _orbitCamera.MinPitch;
                _savedMaxPitch = _orbitCamera.MaxPitch;
                _orbitCamera.MinPitch = JetpackMinPitchDegrees;
                _orbitCamera.MaxPitch = JetpackMaxPitchDegrees;

                // Streaks of rushing air, made on the camera and taken off with the jetpack.
                _speedLines = JetpackSpeedLines.Attach(_orbitCamera.transform);
            }

            if (FlameEffect != null)
            {
                FlameEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void OnUnequip(GameObject player)
        {
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null && ReferenceEquals(input.EquippedJetpack, this))
            {
                input.EquippedJetpack = null;
            }
            if (_orbitCamera != null)
            {
                _orbitCamera.MinPitch = _savedMinPitch;
                _orbitCamera.MaxPitch = _savedMaxPitch;
                // Also clamps the camera's OWN current angle back inside the
                // restored range -- otherwise a player who was looking
                // almost straight down when they unequipped keeps that
                // extreme pitch on foot until they move the mouse again.
                _orbitCamera.Pitch = Mathf.Clamp(_orbitCamera.Pitch, _savedMinPitch, _savedMaxPitch);

                // Back to the ordinary view: no widening, no pull-back, no streaks.
                _orbitCamera.FlightFovBonus = 0.0f;
                _orbitCamera.FlightDistanceBonus = 0.0f;
            }
            if (_speedLines != null)
            {
                Destroy(_speedLines.gameObject);
                _speedLines = null;
            }
            // Otherwise the body stays leaned from whatever WASD was held
            // the instant the jetpack came off.
            if (_animatorTransform != null)
            {
                _animatorTransform.localRotation = Quaternion.identity;
            }
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            CosmeticMount.ToBone(transform, body, HumanBodyBones.Spine, BackPosition, BackRotationEuler, 1.0f);
            if (FlameEffect != null)
            {
                FlameEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        /// <summary>The wearer is thrusting: the flame shows (the owner's own flame is driven by its real Tick).</summary>
        public void SetCosmeticActive(bool active)
        {
            if (FlameEffect == null)
            {
                return;
            }
            if (active && !FlameEffect.isEmitting)
            {
                FlameEffect.Play();
            }
            else if (!active && FlameEffect.isEmitting)
            {
                FlameEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        /// <summary>
        /// The actual vertical speed a frame of thrust climbs or dives at.
        ///
        /// FLYING FORWARD FOLLOWS THE CAMERA'S OWN PITCH -- Requested:
        /// "make it curve and turn with where your looking." Horizontal
        /// turning already follows the camera (WASD is camera-relative, the
        /// same as ordinary ground movement); this is what makes vertical
        /// flight do the same, so looking up climbs and looking down dives
        /// while flying forward, rather than thrust always being a flat
        /// straight-up push regardless of where the camera is aimed.
        ///
        /// NEGATED, not used directly -- ThirdPersonCamera's own Pitch
        /// convention runs the other way (MinPitch -35 is looking UP,
        /// MaxPitch +70 is looking DOWN, confirmed directly against its own
        /// mouse-delta wiring), so a positive angle has to produce a
        /// NEGATIVE (diving) vertical speed.
        ///
        /// ONLY WHILE ACTUALLY FLYING FORWARD -- with no forward component
        /// this returns the ordinary flat climb speed (BoostThrustSpeed
        /// with nothing held to lean toward, matching "if not holding wasd
        /// keys then just make it go up faster"; plain ThrustSpeed
        /// otherwise), unaffected by where the camera happens to be aimed.
        /// Hovering in place while looking around should not silently
        /// start climbing or sinking.
        /// </summary>
        public static float ClimbVelocityFor(Vector2 moveInput, bool boostHeld, float cameraPitchDegrees)
        {
            bool flyingForward = moveInput.y > 0.0f;
            if (!flyingForward)
            {
                bool boostingStraightUp = boostHeld && moveInput.sqrMagnitude < 0.0001f;
                return boostingStraightUp ? BoostThrustSpeed : ThrustSpeed;
            }

            float multiplier = boostHeld ? DiveClimbMultiplier : 1.0f;
            return -Mathf.Sin(cameraPitchDegrees * Mathf.Deg2Rad) * ThrustSpeed * multiplier;
        }

        /// <summary>
        /// Called every frame while equipped, in place of the ordinary
        /// charged-jump handling. Sets the body's own vertical velocity
        /// directly rather than adding an impulse -- the same
        /// SetVerticalVelocity hook GrappleSwing's release and
        /// PlayerClimbing's hop-off already use, called BEFORE
        /// PlayerLocomotion.Tick runs this same frame so its own gravity
        /// integration (which always fires first inside Tick) only ever
        /// shaves one frame's worth off the thrust speed rather than
        /// stacking a whole extra frame of unopposed fall first.
        ///
        /// BOOST SPENDS ON EITHER LEAN, CLIMB OR DIVE-SPEED, NEVER MORE
        /// THAN ONE -- "if not holding wasd keys then just make it go up
        /// faster", from the explicit ask, which only makes sense read as
        /// an else: WASD held means there is a direction to lean toward
        /// (or, flying forward, a real pitch to dive/climb along instead
        /// of a flat push), so the boost goes there rather than also
        /// stacking BoostThrustSpeed on top of it. See ClimbVelocityFor's
        /// own doc comment for the actual speed math.
        /// </summary>
        public void Tick(bool thrustHeld, Vector2 moveInput, bool boostHeld, float deltaTime,
            float cameraPitchDegrees = 0.0f)
        {
            if (thrustHeld)
            {
                _locomotion?.SetVerticalVelocity(
                    ClimbVelocityFor(moveInput, boostHeld, cameraPitchDegrees));
                // See PlayerVitals.ResetFallTracking's own doc comment --
                // this is the whole mechanism behind "negates fall damage
                // from the last height it was on."
                if (_vitals != null && _locomotion != null)
                {
                    _vitals.ResetFallTracking(_locomotion.transform.position.y);
                }
            }

            TickTilt(thrustHeld, moveInput, boostHeld, cameraPitchDegrees, deltaTime);

            // How the camera reacts to speed. Speed is the whole body's, so a fast
            // fall reads as fast too; on the ground it is off, so sprinting with a
            // jetpack on the back does not widen the view.
            if (_locomotion != null)
            {
                TickFlightFeel(_locomotion.Velocity, !_locomotion.IsGrounded, thrustHeld && boostHeld, deltaTime);
            }

            if (FlameEffect != null)
            {
                if (thrustHeld && !FlameEffect.isEmitting)
                {
                    FlameEffect.Play();
                }
                else if (!thrustHeld && FlameEffect.isEmitting)
                {
                    FlameEffect.Stop();
                }
            }

            if (ThrustAudio != null)
            {
                if (thrustHeld && !ThrustAudio.isPlaying)
                {
                    ThrustAudio.Play();
                }
                else if (!thrustHeld && ThrustAudio.isPlaying)
                {
                    ThrustAudio.Stop();
                }
            }

            // "AT SHIFT SPEED" -- boostHeld specifically, not thrustHeld
            // alone; ordinary unboosted thrust keeps only ThrustAudio's own
            // engine loop, silent on wind. TWO Follow-up requests
            // layered on top of the original Play()/Stop() pattern:
            // "end when stopped flying at that speed and starts from the
            // top when resumed" (Play()/Stop(), never Pause()/UnPause() --
            // Stop() always resets playback position, so the next Play()
            // begins fresh) AND "make the wind slowly fade in and out as it
            // begins and stops" (WindFadeSeconds ramps the VOLUME rather
            // than snapping straight to full/silent -- Play() itself still
            // fires instantly, right at the moment boosting starts, so
            // "starts from the top" still holds; only how LOUD it is over
            // the next half-second changes).
            bool boosting = thrustHeld && boostHeld;
            if (WindAudio != null)
            {
                float fadeStep = deltaTime / WindFadeSeconds;
                if (boosting)
                {
                    if (!WindAudio.isPlaying)
                    {
                        WindAudio.volume = 0.0f;
                        WindAudio.Play();
                    }
                    WindAudio.volume = Mathf.MoveTowards(WindAudio.volume, 1.0f, fadeStep);
                }
                else if (WindAudio.isPlaying)
                {
                    WindAudio.volume = Mathf.MoveTowards(WindAudio.volume, 0.0f, fadeStep);
                    if (WindAudio.volume <= 0.0f)
                    {
                        WindAudio.Stop();
                    }
                }
            }
        }

        /// <summary>Snaps the flight feel back to nothing: no widening, no pull-back, no streaks.</summary>
        public void ResetFlightFeel()
        {
            _feelFov = 0.0f;
            _feelFovVelocity = 0.0f;
            _feelDistance = 0.0f;
            _feelDistanceVelocity = 0.0f;
            _feelKickAge = 1.0e9f;
            _feelWasBoosting = false;
            if (_orbitCamera != null)
            {
                _orbitCamera.FlightFovBonus = 0.0f;
                _orbitCamera.FlightDistanceBonus = 0.0f;
            }
            if (_speedLines != null)
            {
                _speedLines.Set(0.0f, Vector3.zero);
            }
        }

        /// <summary>
        /// One frame of the flight feel: works out how fast the player is going
        /// (see <see cref="FlightFeel"/>), springs the camera's field-of-view widening and
        /// pull-back toward it, punches both on the moment a boost begins, quickens the wind
        /// and feeds the speed streaks. Public so a test can drive it with a made-up speed.
        /// </summary>
        public void TickFlightFeel(Vector3 velocity, bool airborne, bool boosting, float deltaTime)
        {
            bool boostBegan = boosting && !_feelWasBoosting;
            _feelWasBoosting = boosting;
            _feelKickAge = boostBegan ? 0.0f : _feelKickAge + deltaTime;

            float fraction = airborne ? FlightFeel.SpeedFraction(velocity.magnitude) : 0.0f;
            float kick = airborne ? FlightFeel.KickEnvelope(_feelKickAge) : 0.0f;

            _feelFov = Mathf.SmoothDamp(_feelFov, FlightFeel.FovBonus(fraction, kick),
                ref _feelFovVelocity, FlightFeel.SmoothSeconds, Mathf.Infinity, deltaTime);
            _feelDistance = Mathf.SmoothDamp(_feelDistance, FlightFeel.DistanceBonus(fraction, kick),
                ref _feelDistanceVelocity, FlightFeel.SmoothSeconds * 1.3f, Mathf.Infinity, deltaTime);

            if (_orbitCamera != null)
            {
                _orbitCamera.FlightFovBonus = _feelFov;
                _orbitCamera.FlightDistanceBonus = _feelDistance;
            }
            if (WindAudio != null)
            {
                WindAudio.pitch = FlightFeel.WindPitch(fraction);
            }
            if (_speedLines != null)
            {
                _speedLines.Set(fraction, velocity);
            }
        }

        /// <summary>
        /// Force-stops the flame and thrust audio without touching
        /// anything else -- for when something OTHER than releasing Space
        /// needs the effects to stop.
        ///
        /// Bug report: "when your using jetpack and crash into a
        /// surface and ragdoll, the jetpack also turns off even if your
        /// holding space." Thrust/movement already stops correctly the
        /// instant the ragdoll goes limp -- PlayerInputReader's own ragdoll
        /// branch returns before it ever reaches this component's Tick --
        /// but Tick is also the ONLY place that stops FlameEffect/
        /// ThrustAudio, and it never runs again while limp. Holding Space
        /// through a crash left the flame burning and the thrust loop
        /// playing over a body that was no longer flying anywhere.
        /// PlayerInputReader calls this every frame while ragdolled, the
        /// same way it already force-stops footstep audio there.
        /// </summary>
        public void StopThrustEffects()
        {
            // A crash or ragdoll ends the flight feel at once: the view goes back to normal.
            ResetFlightFeel();

            if (FlameEffect != null && FlameEffect.isEmitting)
            {
                FlameEffect.Stop();
            }
            if (ThrustAudio != null && ThrustAudio.isPlaying)
            {
                ThrustAudio.Stop();
            }
            if (WindAudio != null && WindAudio.isPlaying)
            {
                WindAudio.Stop();
            }
        }

        /// <summary>
        /// Snaps the body's own lean back to upright immediately, bypassing
        /// EaseTilt entirely -- for the moment a ragdoll takes over, not for
        /// ordinary flight.
        ///
        /// Bug report: "im still getting up at an angle when i
        /// ragdoll." _animatorTransform (see OnEquip's own doc comment --
        /// "the whole visual body, leaned as one rigid piece") sits on a
        /// CHILD of the character root, the same Animator.transform Ragdoll
        /// itself finds via GetComponentInChildren -- it is a SEPARATE node
        /// from the root transform Ragdoll.RepositionToRagdollRest re-levels
        /// on stand-up. Levelling the root was never enough to also level
        /// this child: PlayerInputReader's own ragdoll branch returns before
        /// ever reaching Jetpack.Tick (see StopThrustEffects' own doc
        /// comment for why that branch exists at all), so whatever lean was
        /// baked into this transform at the exact instant of the crash sat
        /// there completely untouched through the entire limp ragdoll AND
        /// the whole "GettingUp" clip, only for TickTilt to start easing it
        /// back out several frames after the player already regained full
        /// control and was already visibly standing there leaned over.
        ///
        /// PlayerInputReader calls this once the instant a ragdoll goes
        /// limp, alongside StopThrustEffects -- an artificial flight lean
        /// makes no sense at all once real ragdoll physics has taken over
        /// the actual bones, so there is no reason to keep it, let alone
        /// ease it out gradually, once real physics is what settles the
        /// body's pose instead.
        /// </summary>
        public void ResetTilt()
        {
            _currentTilt = Vector2.zero;
            _tiltVelocity = Vector2.zero;
            if (_animatorTransform != null)
            {
                _animatorTransform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Where the lean is headed, in degrees -- X = pitch (forward/
        /// back), Y = roll (strafe). Pure and static so the direction/scale
        /// of the lean is testable without a real Animator, the same
        /// reasoning PlayerInputReader.TickJumpCharge and
        /// PlayerVoiceCapture.ShouldCapture split their own decisions out
        /// for.
        ///
        /// PITCH FOLLOWS THE CAMERA, BUT ONLY WHILE FLYING FORWARD -- SIXTH
        /// Correction. The fifth correction gave a plain hover
        /// (Space alone, or Space+Shift, no W) its OWN curve --
        /// `2 * Clamp(camera, 0, 90)` -- so it stayed upright looking level
        /// or up and only tipped toward a headstand once the camera
        /// actually looked down. Follow-up: "make it so if your
        /// just holding space or holding shift+space no W with jetpack then
        /// the character just stay upright and doesnt tilt forward when you
        /// look down" -- even THAT partial tip was one tilt too many for
        /// this case. A hover has no direction of travel for the body to
        /// point along at all, so its pitch is now simply 0 (standing
        /// upright) no matter where the camera looks, full stop.
        ///
        /// FLYING FORWARD (W held) is UNCHANGED, still
        /// `cameraPitchDegrees + SupermanPitchOffsetDegrees` -- the body's
        /// own orientation IS the direction of travel there, so it still
        /// sweeps the full standing / horizontal / headstand range across
        /// the camera's own pitch. Only the hover curve was ever the
        /// target of any of these corrections.
        ///
        /// ROLL IS STILL WASD-DRIVEN, UNCHANGED BY ANY OF THIS -- strafing
        /// still banks the body into the turn the way it always did; only
        /// pitch was ever raw camera-follow. ROLL IS NEGATED ON moveInput.x
        /// per the same confirmed signs (Euler(0,0,10)*right = (0.98,0.17,0)):
        /// strafing RIGHT (positive x) needs a NEGATIVE angle to bank the
        /// right side down, leaning into the strafe rather than away from
        /// it. <paramref name="boosting"/> AND <paramref name="flyingForward"/>
        /// BOTH have to be true for SupermanRollMaxDegrees; anything else
        /// (a plain hover, plain forward thrust, a boosted strafe with no
        /// forward component) still gets one of the two smaller, ordinary
        /// roll caps, matching real callers' own existing combination
        /// (TickTilt's <c>amplify</c> only goes true while boosting) --
        /// nothing about any of the pitch bug reports was ever about roll.
        /// </summary>
        public static Vector2 TiltTarget(Vector2 moveInput, float cameraPitchDegrees,
            bool boosting = false, bool flyingForward = false)
        {
            float maxDegrees = (boosting && flyingForward) ? SupermanRollMaxDegrees
                : boosting ? BoostTiltMaxDegrees : TiltMaxDegrees;
            float roll = -moveInput.x * maxDegrees;
            float pitch = flyingForward
                ? cameraPitchDegrees + SupermanPitchOffsetDegrees
                : 0.0f;
            return new Vector2(pitch, roll);
        }

        /// <summary>
        /// Eases the current lean toward its target, one axis at a time --
        /// pure and static for the same testability reason TiltTarget is,
        /// just with the smoothing's own persistent rate-of-change threaded
        /// through explicitly (<paramref name="velocity"/>) rather than
        /// hidden in an instance field, so a test can still drive this with
        /// nothing but plain values. Independent per-axis SmoothDamp, not a
        /// single blended one, so releasing just one of two held keys (e.g.
        /// W+D, then letting go of D) drops only that axis back to zero
        /// rather than re-easing the whole lean vector toward a new
        /// diagonal target.
        ///
        /// SmoothDamp OVER MoveTowards -- see TiltSmoothTime's own doc
        /// comment for why a constant-rate ramp reads as snapping into
        /// place the instant it arrives. SmoothDamp needs a velocity to
        /// carry between calls (that is what makes it decelerate into the
        /// target instead of stopping dead), which is the whole reason this
        /// is no longer quite as stateless as a plain MoveTowards call was.
        /// </summary>
        public static Vector2 EaseTilt(Vector2 current, Vector2 target, ref Vector2 velocity, float deltaTime)
        {
            return new Vector2(
                Mathf.SmoothDamp(current.x, target.x, ref velocity.x, TiltSmoothTime, Mathf.Infinity, deltaTime),
                Mathf.SmoothDamp(current.y, target.y, ref velocity.y, TiltSmoothTime, Mathf.Infinity, deltaTime));
        }

        /// <summary>
        /// Turns a (pitch, roll) tilt pair into the actual local rotation --
        /// factored out, pure and static, for the same testability reason
        /// TiltTarget/EaseTilt already are.
        ///
        /// SECOND Correction -- the FIRST fix here (`pitch * roll`,
        /// rolling around the body's OWN post-pitch forward) was itself
        /// wrong, confirmed directly by the user: "roll not turn left to
        /// right." SupermanPitchOffsetDegrees pitches the body a full 90
        /// past its ordinary orientation specifically so it reads as lying
        /// horizontal -- which means the body's OWN forward axis, once
        /// pitched that far, no longer points anywhere near the actual
        /// horizontal direction of travel; it points straight down (a
        /// standing person tipped 90 degrees forward looks at the ground,
        /// not ahead). Rolling around THAT axis spins the body around a
        /// vertical line -- exactly a yaw, exactly "turning left to right,"
        /// never a bank, no matter how the numbers were verified.
        ///
        /// `roll * pitch` fixes this by applying roll AROUND THE FIXED,
        /// UN-PITCHED Vector3.forward -- the real, horizontal direction the
        /// parent (already yawed to face the camera/travel direction) is
        /// actually flying toward -- and only THEN pitching the whole
        /// already-rolled body up into the dive pose. The roll axis no
        /// longer depends on how far SupermanPitchOffsetDegrees has tipped
        /// the body, so it stays aligned with the real direction of travel
        /// at every pitch, which is what an actual plane roll needs.
        /// </summary>
        public static Quaternion TiltRotation(Vector2 tilt)
        {
            Quaternion pitch = Quaternion.AngleAxis(tilt.x, Vector3.right);
            Quaternion roll = Quaternion.AngleAxis(tilt.y, Vector3.forward);
            return roll * pitch;
        }

        /// <summary>
        /// ONLY LEANS WHILE ACTUALLY THRUSTING -- an explicit correction:
        /// this used to lean off WASD alone regardless of whether Space was
        /// held, so simply air-steering a fall with the jetpack equipped
        /// (but not thrusting) leaned the body as if flying. With thrust
        /// not held the target is forced to level, easing the body back to
        /// neutral the same way letting go of a single WASD key already
        /// eases that axis back (see EaseTiltStraightensOutOnceTheKeyIsReleased),
        /// so the model's own pose reads as idle/falling rather than banked
        /// the instant thrust actually stops.
        ///
        /// Boosting only amplifies the lean while there is a direction to
        /// lean toward -- with no WASD held, Tick spends the same boost on
        /// a faster climb instead (see its own doc comment), so this must
        /// not also treat "boosting straight up" as "lean further toward
        /// nothing," which would just widen dead-zone jitter around zero.
        /// </summary>
        private void TickTilt(bool thrustHeld, Vector2 moveInput, bool boostHeld,
            float cameraPitchDegrees, float deltaTime)
        {
            bool amplify = boostHeld && moveInput.sqrMagnitude > 0.0001f;
            // NO LONGER REQUIRES boostHeld -- see TiltTarget's own doc
            // comment on flyingForward for why: "just moving up/down"
            // (plain W+Space, no shift) needed the exact same
            // standing/headstand pitch mapping a boosted dive already had,
            // not a separate, unclamped 1:1 camera-follow. A forward
            // component specifically, still -- a pure sideways or backward
            // hold has no flight direction to pitch toward.
            bool flyingForward = moveInput.y > 0.0f;
            Vector2 target = thrustHeld
                ? TiltTarget(moveInput, cameraPitchDegrees, amplify, flyingForward)
                : Vector2.zero;
            _currentTilt = EaseTilt(_currentTilt, target, ref _tiltVelocity, deltaTime);
            if (_animatorTransform != null)
            {
                _animatorTransform.localRotation = TiltRotation(_currentTilt);
            }
        }
    }
}
