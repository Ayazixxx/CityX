using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Turns the animated body into a falling one.
    ///
    /// The bodies, colliders and joints are built ahead of time by
    /// RagdollBuilder and sit here KINEMATIC, doing nothing, while the Animator
    /// poses them. Going limp is a switch, not a construction: building a rig
    /// at the moment of death means allocating a dozen colliders and joints on
    /// the exact frame something dies, which is the frame least able to afford
    /// it, and it is also the frame where a mistake is least recoverable.
    ///
    /// The Godot original built its rig on death, into a separate node under
    /// the scene root, because its bones were plain meshes with no bodies. Here
    /// the skeleton already exists and the physics can simply live on it.
    /// </summary>
    public sealed class Ragdoll : MonoBehaviour
    {
        /// <summary>
        /// character.gd RAGDOLL_LINEAR_DAMP / RAGDOLL_ANGULAR_DAMP.
        ///
        /// Angular damping is far higher than linear on purpose. What reads as
        /// janky is rotational jitter at the joints -- limbs quivering in place
        /// -- not the fall itself, which should stay unimpeded.
        /// </summary>
        public const float LinearDamping = 0.6f;

        public const float AngularDamping = 4.0f;

        /// <summary>
        /// character.gd RAGDOLL_GRAVITY_SCALE, as EXTRA gravity on top of the
        /// real thing, because a 3D Rigidbody has no gravity scale.
        ///
        /// From a standing start, a body at rest takes a beat of free-fall
        /// before gravity alone produces enough motion to read as falling --
        /// physically honest, but it looks like the character keeps standing
        /// for a moment after dying. A little extra shortens that beat without
        /// the fall reading as unnaturally fast.
        /// </summary>
        public const float ExtraGravityScale = 0.5f;

        /// <summary>
        /// character.gd RAGDOLL_TORSO_SPIN_DEGREES_PER_SEC.
        ///
        /// ONLY THE HIPS GET THIS. Every other body starts with no spin of its
        /// own and is dragged into motion by the joints, the way a real body's
        /// limbs only move because something else pulls on them. The Godot
        /// build first gave every piece its own random spin, and seven
        /// unrelated rotations starting on one frame read as the rig exploding
        /// rather than as something going limp.
        /// </summary>
        public const float TorsoSpinDegreesPerSecond = 90.0f;

        /// <summary>
        /// Below this speed the torso counts as "at rest" rather than
        /// "still falling/tumbling" -- what both StandUpRestSeconds' own
        /// timer and fall-damage tracking while limp use to decide the
        /// ragdoll has actually landed on something, since there is no
        /// CharacterController.isGrounded to ask while it is disabled.
        /// </summary>
        public const float RestVelocityThreshold = 0.3f;

        /// <summary>
        /// Requested, restated and retimed twice: "once you are ragdolled
        /// from one of these events your are able to press space to stand up
        /// once your on the floor for 2.5 seconds" -- corrected to "3 seconds
        /// on the ground," then shortened again to "2 seconds."
        /// </summary>
        public const float StandUpRestSeconds = 2.0f;

        /// <summary>
        /// How long the recovery actually locks movement for, once
        /// standing-up starts -- Bug report: "whenever i ragdoll
        /// and then get up my character is stuck standing still for a
        /// bit." BeginStandingUp plays the SAME "GettingUp" Animator state
        /// IntroCutscene uses for its own one-time fall-from-the-sky
        /// intro, and this used to wait out that clip's own real,
        /// measured length (IntroCutscene.GettingUpClipSeconds, 7.6
        /// seconds) before returning control on EVERY ordinary ragdoll
        /// recovery -- a jetpack crash, a finisher knockdown, /sleep, all
        /// of them. Checked against the Godot original: character.gd's own
        /// reset_ragdoll() (the real in-game recovery) is INSTANT, no
        /// animation at all -- "getting_up" there is exclusively
        /// intro_cutscene.gd's own slow, deliberate "waking up in a new
        /// world" beat, never reused for ordinary knockdowns. Locking
        /// routine movement for a one-time cinematic's own full clip
        /// length is what read as "stuck" -- the player is done watching
        /// long before 7.6 real seconds pass.
        ///
        /// Rather than skip the clip entirely (a visible pop straight from
        /// limp to standing), BeginStandingUp now plays the SAME clip at
        /// StandUpPlaybackSpeed so the full motion still completes before
        /// control returns -- just quickly, the way an ordinary in-game
        /// recovery should feel, without needing a second baked animation.
        /// </summary>
        public const float StandUpPaceSeconds = 1.8f;

        /// <summary>
        /// The get-up clip's last stretch (from about 5.0 of its 7.6 seconds, measured by sampling the pose) is the character standing perfectly
        /// still: the clip was cut for a cutscene that then looks around. Holding the player through it was the reported "freezes for a second
        /// and can't move after standing up". Control comes back when the body is up, which at this pace is after 5.0/7.6 of the clip, and the
        /// animator is cross-faded to locomotion so the tail never plays.
        /// </summary>
        public const float StandUpRecoverySeconds = StandUpPaceSeconds * UprightShare;

        /// <summary>How much of the get-up clip has passed when the body is up (5.0 of its 7.6 seconds).</summary>
        public const float UprightShare = 5.0f / 7.6f;

        /// <summary>How long the hand-back from the get-up clip to ordinary locomotion blends, in seconds.</summary>
        public const float StandUpHandBackBlendSeconds = 0.25f;

        /// <summary>How much faster than IntroCutscene's own cinematic pace the GettingUp clip plays for an ordinary recovery -- see StandUpRecoverySeconds' own doc comment.</summary>
        public static readonly float StandUpPlaybackSpeed =
            IntroCutscene.GettingUpClipSeconds / StandUpPaceSeconds;

        [Tooltip("Every rigid body in the rig. Filled in by RagdollBuilder.")]
        public Rigidbody[] Bodies;

        [Tooltip("The hips. Takes the initial spin and carries the rig.")]
        public Rigidbody Torso;

        [Tooltip("Switched off while limp so the animation stops fighting physics.")]
        public Animator Animator;

        private CharacterController _controller;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private GrappleSwing _swing;
        private bool _limp;
        private float _restSeconds;
        private float _standingUpElapsed = -1.0f;
        private bool _allowImmediateStandUp;
        private float _standUpPaceSeconds = StandUpPaceSeconds;

        public bool IsLimp => _limp;

        /// <summary>
        /// Another player's copy posed as its owner's limp body is (RagdollMirror, docs/MULTIPLAYER_STATE.md): only the owner's machine runs
        /// the ragdoll, so nothing here throws this one about (<see cref="Activate"/> does nothing) while the owner's pose is laid on it.
        /// </summary>
        public bool IsMirrored { get; set; }

        /// <summary>
        /// The torso has stopped moving significantly -- the closest thing
        /// a disabled CharacterController has to "grounded" while limp.
        /// Used both to gate CanStandUp and, from PlayerInputReader, to
        /// keep PlayerVitals.TickFallDamage running while ragdolled --
        /// Requested: "the ragdoll can continue to take fall damage."
        /// </summary>
        public bool IsRestingOnGround =>
            _limp && (Torso == null || Torso.linearVelocity.magnitude < RestVelocityThreshold);

        /// <summary>True from BeginStandingUp until the GettingUp clip finishes -- see FinishStandingUp.</summary>
        public bool IsStandingUp => _standingUpElapsed >= 0.0f;

        /// <summary>
        /// Whether pressing the stand-up key would actually do anything
        /// right now -- resting long enough, not already mid-recovery.
        ///
        /// _allowImmediateStandUp SKIPS THE REST TIMER ENTIRELY --
        /// Requested: "make it so i can press space to get up at any time after
        /// /sleep is used." /sleep is a diagnostic, not a real knockdown
        /// (PlayerChat.Dispatch calls Activate directly, bypassing
        /// PlayerVitals -- no death, no damage, no real "you got hit"
        /// moment to make you wait out), so it has no reason to share the
        /// same recovery delay a real crash/finisher knockdown uses to make
        /// getting knocked down actually cost something.
        /// </summary>
        public bool CanStandUp => _limp && !IsStandingUp &&
            (_allowImmediateStandUp || _restSeconds >= StandUpRestSeconds);

        private void Awake()
        {
            Bind();
        }

        /// <summary>
        /// Finds the controller/locomotion/vitals/swing/animator siblings.
        ///
        /// PUBLIC BECAUSE AWAKE IS NOT AVAILABLE OUTSIDE PLAY MODE, same
        /// reasoning as PlayerLocomotion.Bind()'s own doc comment -- an
        /// EditMode test can add this component but never gets Awake run
        /// on it, so without a way in by hand Activate() would silently
        /// skip disabling locomotion/the controller (both fields staying
        /// null) rather than actually failing loud. Calling it twice is
        /// harmless.
        /// </summary>
        public void Bind()
        {
            _controller = GetComponent<CharacterController>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _vitals = GetComponent<PlayerVitals>();
            _swing = GetComponent<GrappleSwing>();

            if (Animator == null)
            {
                Animator = GetComponentInChildren<Animator>();
            }

            IgnoreCollisionsWithin(Bodies, _controller);
            SetKinematic(true);
        }

        private void OnEnable()
        {
            if (_vitals != null)
            {
                _vitals.Died += OnDied;
                _vitals.Revived += OnRevived;
            }
        }

        private void OnDisable()
        {
            if (_vitals != null)
            {
                _vitals.Died -= OnDied;
                _vitals.Revived -= OnRevived;
            }
        }

        private void OnDied()
        {
            // The body's own motion, plus the fling of the blow that killed it (PlayerVitals.DeathImpulse: a melee kill throws the corpse).
            Vector3 velocity = _locomotion != null ? _locomotion.Velocity : Vector3.zero;
            if (_vitals != null)
            {
                velocity += _vitals.DeathImpulse;
            }
            Activate(velocity);
        }

        private void OnRevived()
        {
            Deactivate();
        }

        /// <summary>
        /// Stops the rig's own pieces from colliding with each other.
        ///
        /// An elbow capsule and its own upper arm capsule overlap at the joint
        /// by construction. Left to collide, the solver's first job on the
        /// first physics step is to push two deeply intersecting bodies apart,
        /// which flings the whole rig across the street. Godot solved this with
        /// a dedicated collision layer; done pairwise here so it needs no
        /// project-wide layer allocated to it, and so it cannot be broken by
        /// someone else's layer decision later.
        ///
        /// Runtime state rather than something serialisable, which is why it is
        /// here and not in the builder -- and why it is public and static: a
        /// rig assembled outside the normal Awake path (a test, an editor tool)
        /// has to be able to do this too, and without it the very first step
        /// throws the rig across the map for reasons that have nothing to do
        /// with whatever was being checked.
        /// </summary>
        public static void IgnoreCollisionsWithin(Rigidbody[] bodies, Collider exclude)
        {
            if (bodies == null)
            {
                return;
            }

            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == null) continue;
                Collider a = bodies[i].GetComponent<Collider>();
                if (a == null) continue;

                for (int j = i + 1; j < bodies.Length; j++)
                {
                    if (bodies[j] == null) continue;
                    Collider b = bodies[j].GetComponent<Collider>();
                    if (b == null) continue;

                    Physics.IgnoreCollision(a, b, true);
                }

                // The standing capsule is meaningless once the body is limp and
                // would otherwise shove the rig around from the inside.
                if (exclude != null)
                {
                    Physics.IgnoreCollision(a, exclude, true);
                }
            }
        }

        /// <summary>
        /// Goes limp, carrying <paramref name="velocity"/> into the fall.
        ///
        /// The velocity matters. A body that dies mid-sprint and drops straight
        /// down reads as the game freezing and then a corpse appearing; the
        /// same body carrying its momentum reads as being killed while running.
        ///
        /// <paramref name="allowImmediateStandUp"/> -- see CanStandUp's own
        /// doc comment for why /sleep passes true here and every real
        /// knockdown leaves it false.
        ///
        /// NEVER RAGDOLLS OUT OF AN ACTIVE SWING. Repeated request:
        /// "remove the ragdoll function completely and revert back to him
        /// just being still" -- the swing-to-ragdoll hand-off (see
        /// grapple_swing_deprioritized memory for the full saga: 4 rounds of
        /// pose-preservation engineering, then a 2026-09-14 partial removal
        /// that kept the hand-off itself) still read as a glitch no amount
        /// of pose-snap suppression fully hid. Rather than refine the
        /// hand-off further, this skips it: a knockdown/death landing while
        /// GrappleSwing.IsActive just releases the rope and returns,
        /// touching nothing else -- the Animator and PlayerLocomotion never
        /// get disabled, so the character keeps standing under ordinary
        /// control instead of going limp. Whatever triggered this call
        /// (death, a finisher, /sleep) still fires its own independent
        /// effects (PlayerVitals.Died, the HUD, etc.) exactly as before --
        /// only the physical ragdoll hand-off is skipped.
        /// </summary>
        public void Activate(Vector3 velocity, bool allowImmediateStandUp = false)
        {
            if (_limp || IsMirrored)
            {
                return;   // a second killing blow must not re-launch the corpse; a copy posed as its owner's ragdoll is not thrown here
            }

            if (_swing != null && _swing.IsActive)
            {
                _swing.Release(false);
                return;
            }

            _limp = true;
            _allowImmediateStandUp = allowImmediateStandUp;

            if (Animator != null)
            {
                // Ragdoll.Activate disables the Animator the same way
                // GrappleSwing.Attach and DevConsoleView.Open do, and can
                // hand an implausible one-frame Mecanim transition straight
                // to real ragdoll physics as its starting pose if nothing
                // catches it first -- see PlayerLocomotion.
                // SuppressImplausibleArmSnapsNow's own doc comment.
                _locomotion?.SuppressImplausibleArmSnapsNow();
                Animator.enabled = false;
            }
            // Both off before the bodies wake, so nothing spends a frame being
            // driven by two things at once.
            if (_locomotion != null)
            {
                _locomotion.enabled = false;
            }
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            // BEFORE going live -- see DepenetrateFromWorld's own doc
            // comment. Every body here is still kinematic and still
            // following the exact pose the Animator left it in, so pushing
            // the whole rig via transform.position (rather than moving any
            // one body on its own) drags every bone by the same amount,
            // the same mechanism PlayerInputReader's own crash-knockdown
            // pull-back already relied on.
            DepenetrateFromWorld();

            SetKinematic(false);

            foreach (Rigidbody body in Bodies)
            {
                if (body != null)
                {
                    body.linearVelocity = velocity;
                }
            }

            if (Torso != null)
            {
                Torso.angularVelocity =
                    Random.onUnitSphere * (TorsoSpinDegreesPerSecond * Mathf.Deg2Rad);
            }
        }

        /// <summary>
        /// How many extra passes to run pushing the whole rig out of
        /// anything it overlaps. One pass clears the single deepest
        /// overlap -- almost always the wall a jetpack crash flew into --
        /// but a corner (a wall AND a window ledge both embedded at once)
        /// needs a second look after the first push, since resolving one
        /// overlap can reveal or worsen another.
        /// </summary>
        private const int DepenetrationMaxPasses = 4;

        /// <summary>Extra clearance past the exact measured overlap, so the rig starts a hair clear rather than exactly touching.</summary>
        private const float DepenetrationSkin = 0.03f;

        /// <summary>
        /// Pushes the WHOLE, STILL-KINEMATIC rig (via transform.position --
        /// see Activate's own doc comment on why this has to run before
        /// SetKinematic(false)) out of anything any of its own body
        /// colliders is currently overlapping.
        ///
        /// Bug report: "whenever i fly into a wall i just get
        /// stuck in it" -- STILL happening after the first attempt at this
        /// fix (a fixed PlayerInputReader.CrashPullBackDistance nudge
        /// applied before calling Activate). A fixed guess cannot know how
        /// far a limb actually tunnelled -- a Superman-dive impact with an
        /// arm stretched straight out can bury it far past any single
        /// constant chosen to cover the common case. This measures the
        /// REAL overlap instead, with Physics.ComputePenetration against
        /// each body's own collider (already enabled and already sitting
        /// in the exact animated pose -- SetKinematic's own comment: "Colliders
        /// stay enabled either way"), and pushes back by exactly the
        /// depth found, however deep that turns out to be. Runs for EVERY
        /// Activate (crash, finisher knockdown, /sleep), not just the
        /// jetpack case -- harmless and a no-op whenever nothing overlaps.
        /// </summary>
        private void DepenetrateFromWorld()
        {
            if (Bodies == null)
            {
                return;
            }

            for (int pass = 0; pass < DepenetrationMaxPasses; pass++)
            {
                // The previous pass's push has to actually reach the
                // physics world before the next query can see it -- Unity
                // does not resync collider transforms until asked.
                Physics.SyncTransforms();

                // Starts at the threshold, not zero -- ordinary resting
                // contact (a standing character put straight into ragdoll,
                // feet already settled against the floor) is not perfectly
                // zero-penetration every frame, and treating that trivial
                // noise as something to push out of pops the whole rig up
                // on every single activation for no real reason. See
                // DepenetrationMinDistance's own doc comment -- the
                // same reasoning applies here, just one-shot rather than
                // ongoing.
                float deepest = DepenetrationMinDistance;
                Vector3 push = Vector3.zero;

                foreach (Rigidbody body in Bodies)
                {
                    if (body == null)
                    {
                        continue;
                    }
                    Collider self = body.GetComponent<Collider>();
                    if (self == null)
                    {
                        continue;
                    }

                    Bounds bounds = self.bounds;
                    Collider[] overlaps = Physics.OverlapBox(
                        bounds.center, bounds.extents, self.transform.rotation,
                        ~0, QueryTriggerInteraction.Ignore);

                    foreach (Collider other in overlaps)
                    {
                        if (other == null || other == self || IsOwnCollider(other))
                        {
                            continue;
                        }
                        if (Physics.ComputePenetration(
                                self, self.transform.position, self.transform.rotation,
                                other, other.transform.position, other.transform.rotation,
                                out Vector3 direction, out float distance) && distance > deepest)
                        {
                            deepest = distance;
                            push = direction * (distance + DepenetrationSkin);
                        }
                    }
                }

                if (deepest <= DepenetrationMinDistance)
                {
                    return;   // clear -- nothing left to push out of
                }
                transform.position += push;
            }
        }

        /// <summary>The rig's own colliders (bodies plus the standing capsule) never count as something to depenetrate FROM.</summary>
        private bool IsOwnCollider(Collider other)
        {
            if (_controller != null && (Collider)_controller == other)
            {
                return true;
            }
            if (Bodies == null)
            {
                return false;
            }
            foreach (Rigidbody body in Bodies)
            {
                if (body != null && body.GetComponent<Collider>() == other)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Per physics step, capped so a correction reads as a gentle push
        /// rather than a teleport -- see TickLiveDepenetration's own doc
        /// comment for why this has to stay small.
        /// </summary>
        private const float LiveDepenetrationMaxStepPerFixedUpdate = 0.1f;

        /// <summary>
        /// BUG, SHIPPED THEN CAUGHT THE SAME DAY: "my guy is like clipping
        /// through the floor making him shake and its making it so i cant
        /// get up." TickLiveDepenetration originally corrected ANY
        /// positive penetration distance at all, however microscopic --
        /// but ordinary resting contact under ExtraGravityScale's extra
        /// downward push is not perfectly zero-penetration every frame the
        /// way it would be at rest with no solver error at all; PhysX
        /// settles a resting body to within a small tolerance, not exactly
        /// zero. While resting on an ordinary floor, EVERY fixed step found
        /// a trivial overlap against the very floor the body was correctly
        /// resting on, and
        /// pushed it up by at least DepenetrationSkin (3cm) -- which
        /// promptly fell back down into the floor next step, got pushed
        /// back up again, forever: a real, continuous oscillation, not a
        /// one-off nudge, which is exactly what read as "shaking"/"clipping
        /// through the floor." It also meant the torso's own linearVelocity
        /// never stayed below RestVelocityThreshold for even one whole
        /// frame, so _restSeconds could never accumulate and CanStandUp
        /// never became true -- "making it so i cant get up" was not a
        /// separate symptom, it was the same bug.
        ///
        /// FIX: only correct penetration deeper than this -- comfortably
        /// above the few-millimetre tolerance ordinary resting contact
        /// settles to, comfortably below anything a real crash/wedge
        /// produces (JetpackWallCrashTests' own synthetic embedding is
        /// tens of centimetres). A body merely resting on the floor now
        /// measures as "not overlapping" and is left alone entirely.
        /// </summary>
        private const float DepenetrationMinDistance = 0.02f;

        /// <summary>
        /// Requested: "do what you can to avoid ragdolls from getting
        /// stuck in walls/floor." DepenetrateFromWorld only ever runs ONCE,
        /// at the instant Activate hands the rig to live physics -- it
        /// catches whatever pose the Animator left behind at the moment of
        /// a crash, but a ragdoll keeps moving for as long as it stays
        /// limp: tumbling into a corner, a joint swinging a hand through a
        /// railing, landing awkwardly across a step. PhysX's own solver
        /// does not reliably self-correct a body that ends up RESTING
        /// overlapped with something rather than merely touching it, so
        /// without an ongoing check that first, one-time correction is the
        /// only thing standing between a crash and a limb quietly sinking
        /// into geometry sometime after it settles.
        ///
        /// EACH BODY CORRECTED INDEPENDENTLY, via its own Rigidbody.position
        /// -- NOT the shared root transform the way DepenetrateFromWorld
        /// moves things. By the time this runs every body is a live,
        /// independently-simulated Rigidbody connected only by joints, no
        /// longer a single kinematic hierarchy that moves as one block;
        /// shoving the whole rig sideways to fix one overlapping hand would
        /// just as easily push an otherwise-fine torso into a NEW overlap.
        /// Rigidbody.position (rather than .transform.position) keeps
        /// PhysX's own internal state in sync immediately, without needing
        /// a manual Physics.SyncTransforms() call.
        ///
        /// CAPPED PER STEP, not corrected all at once -- a small, steady
        /// nudge every FixedUpdate clears real embedding within a handful
        /// of steps while staying gentle enough that the joint solver can
        /// absorb it, rather than a single large teleport that could yank
        /// a limb visibly or fight the very joints holding the rig together.
        /// </summary>
        private void TickLiveDepenetration()
        {
            if (Bodies == null)
            {
                return;
            }

            foreach (Rigidbody body in Bodies)
            {
                if (body == null)
                {
                    continue;
                }
                Collider self = body.GetComponent<Collider>();
                if (self == null)
                {
                    continue;
                }

                Bounds bounds = self.bounds;
                Collider[] overlaps = Physics.OverlapBox(
                    bounds.center, bounds.extents, self.transform.rotation,
                    ~0, QueryTriggerInteraction.Ignore);

                foreach (Collider other in overlaps)
                {
                    if (other == null || other == self || IsOwnCollider(other))
                    {
                        continue;
                    }
                    if (Physics.ComputePenetration(
                            self, self.transform.position, self.transform.rotation,
                            other, other.transform.position, other.transform.rotation,
                            out Vector3 direction, out float distance) && distance > DepenetrationMinDistance)
                    {
                        float step = Mathf.Min(distance + DepenetrationSkin, LiveDepenetrationMaxStepPerFixedUpdate);
                        body.position += direction * step;
                    }
                }
            }
        }

        /// <summary>
        /// Keeps a STILL-KINEMATIC limb from embedding in world geometry
        /// while jetpacking or grapple-swinging -- Bug report:
        /// "make it so my limbs cant clip through walls and get stuck when
        /// im flying around with jetpack or swinging around with grapple."
        /// Neither state ever ragdolls: Jetpack.cs never touches this class
        /// at all, and Activate's own "NEVER RAGDOLLS OUT OF AN ACTIVE
        /// SWING" refuses to hand off while GrappleSwing.IsActive. So
        /// DepenetrateFromWorld/TickLiveDepenetration -- both gated on
        /// _limp -- never run for either state, and a limb whose animated
        /// pose (a Superman-dive arm, a leg swinging through the arc)
        /// reaches past the CharacterController's own capsule into nearby
        /// geometry has nothing correcting it at all -- exactly the
        /// tunnelling JetpackWallCrashTests' own root-cause comment already
        /// describes for the harder-crash case, just without a crash
        /// happening at all this time.
        ///
        /// SAME MECHANISM AS DepenetrateFromWorld: every body here is still
        /// kinematic and still one rigid block with this transform, so
        /// pushing transform.position (never any individual
        /// Rigidbody.position -- that only means something once bodies are
        /// independently simulated, see TickLiveDepenetration's own doc
        /// comment) drags every bone along by the same amount. Unlike
        /// DepenetrateFromWorld's "loop until totally clear" (fine for a
        /// rare one-shot activation), this is called every frame from
        /// PlayerInputReader while flying/swinging, so it only takes ONE
        /// capped step per call, the same LiveDepenetrationMaxStepPerFixedUpdate-
        /// sized nudge TickLiveDepenetration uses -- a steady small
        /// correction each frame reads as the pose being held clear of the
        /// wall rather than a pop, and is cheap enough to run continuously.
        ///
        /// NEVER RUNS WHILE LIMP -- that overlap is TickLiveDepenetration's
        /// job instead, which corrects each body independently since they
        /// are no longer one kinematic block by then; running both at once
        /// would have them fight over the same overlap.
        /// </summary>
        public void TickKinematicLimbClearance()
        {
            if (_limp || Bodies == null)
            {
                return;
            }

            float deepest = DepenetrationMinDistance;
            Vector3 push = Vector3.zero;

            foreach (Rigidbody body in Bodies)
            {
                if (body == null)
                {
                    continue;
                }
                Collider self = body.GetComponent<Collider>();
                if (self == null)
                {
                    continue;
                }

                Bounds bounds = self.bounds;
                Collider[] overlaps = Physics.OverlapBox(
                    bounds.center, bounds.extents, self.transform.rotation,
                    ~0, QueryTriggerInteraction.Ignore);

                foreach (Collider other in overlaps)
                {
                    if (other == null || other == self || IsOwnCollider(other))
                    {
                        continue;
                    }
                    if (Physics.ComputePenetration(
                            self, self.transform.position, self.transform.rotation,
                            other, other.transform.position, other.transform.rotation,
                            out Vector3 direction, out float distance) && distance > deepest)
                    {
                        deepest = distance;
                        push = direction * Mathf.Min(
                            distance + DepenetrationSkin, LiveDepenetrationMaxStepPerFixedUpdate);
                    }
                }
            }

            if (push != Vector3.zero)
            {
                transform.position += push;
            }
        }

        /// <summary>Stands the body back up, for respawn.</summary>
        public void Deactivate()
        {
            if (!_limp)
            {
                return;
            }
            _limp = false;
            _allowImmediateStandUp = false;

            SetKinematic(true);

            if (_controller != null)
            {
                _controller.enabled = true;
            }
            if (_locomotion != null)
            {
                _locomotion.enabled = true;
            }
            if (Animator != null)
            {
                Animator.enabled = true;
            }
        }

        /// <summary>
        /// Call once per frame while limp, from PlayerInputReader's own
        /// ragdoll branch -- NOT automatic, since ordinary Update() would
        /// run this even for a death-ragdoll that should never stand back
        /// up (PlayerInputReader's own IsDead check sits above where the
        /// ragdoll branch would otherwise be reached, so a dead body never
        /// gets ticked here at all).
        ///
        /// Tracks how long the torso has been resting (for CanStandUp/
        /// StandUpRestSeconds), and drives the GettingUp recovery once
        /// BeginStandingUp has started one.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_limp)
            {
                _restSeconds = 0.0f;
                return;
            }

            if (IsStandingUp)
            {
                _standingUpElapsed += deltaTime;
                if (_standingUpElapsed >= _standUpPaceSeconds * UprightShare)
                {
                    FinishStandingUp();
                }
                return;
            }

            _restSeconds = IsRestingOnGround ? _restSeconds + deltaTime : 0.0f;
        }

        /// <summary>
        /// Starts the recovery: repositions the standing capsule to where
        /// the ragdoll actually settled (not wherever the root transform
        /// was frozen when Activate() was first called -- see
        /// RepositionToRagdollRest's own doc comment), goes kinematic
        /// again so the Animator stops fighting live physics the same way
        /// Activate's own doc comment already covers for the grapple-swing
        /// case, and plays the same "GettingUp" clip IntroCutscene uses for
        /// its own stand-from-lying-down beat.
        ///
        /// THE CONTROLLER AND LOCOMOTION STAY DISABLED FOR THE WHOLE CLIP,
        /// matching IntroCutscene's own reasoning for its own topping-out/
        /// getting-up phases: a player standing up mid-animation should not
        /// also be able to walk through it. Tick's own IsStandingUp branch
        /// re-enables them via FinishStandingUp once the clip's real length
        /// has elapsed. _limp itself stays true throughout, deliberately --
        /// that is what keeps PlayerInputReader routing frames through its
        /// own ragdoll branch (and this Tick) instead of ordinary movement
        /// while the clip plays.
        /// </summary>
        /// <param name="paceSeconds">How long the whole get-up clip takes: an ordinary recovery is quick (StandUpPaceSeconds); the intro takes
        /// its time.</param>
        public void BeginStandingUp(bool force = false, float paceSeconds = StandUpPaceSeconds)
        {
            if (!(force ? _limp && !IsStandingUp : CanStandUp))
            {
                return;
            }
            _standUpPaceSeconds = paceSeconds;
            _standingUpElapsed = 0.0f;
            _blendElapsed = -1.0f;

            // FROM ANY POSITION: the pose the ragdoll is in RIGHT NOW is captured, the body is turned and moved so the get-up clip starts
            // where the ragdoll is lying (head the same way, hips on the same spot), and the first moments of the clip are blended FROM the
            // captured pose (LateUpdate) so there is no pop. Researched: this is the standard "pose snapshot" ragdoll recovery -- snapshot,
            // align the animation's root to the body, cross-fade bone rotations, then let the animation run. A body face-down while the clip
            // starts face-up rolls over during the blend rather than snapping.
            CapturePose();
            SetKinematic(true);

            if (Animator != null)
            {
                Animator.enabled = true;
                Animator.speed = IntroCutscene.GettingUpClipSeconds / _standUpPaceSeconds;
                Animator.Play("GettingUp", 0, 0.0f);
                Animator.Update(0.0f);   // the clip's first frame is posed now, so it can be measured
            }
            RepositionToRagdollRest();
            _blendElapsed = _snapshotBones != null ? 0.0f : -1.0f;
        }

        /// <summary>Seconds over which the captured ragdoll pose is blended into the get-up animation.</summary>
        public const float StandUpBlendSeconds = 0.4f;

        private Transform[] _snapshotBones;
        private Quaternion[] _snapshotRotations;
        private Vector3 _snapshotHipsPosition;
        private float _blendElapsed = -1.0f;

        /// <summary>True while the ragdoll pose is still being blended into the get-up clip.</summary>
        public bool IsBlendingToStandUp => _blendElapsed >= 0.0f;

        private void CapturePose()
        {
            _snapshotBones = null;
            if (Animator == null || !Animator.isHuman)
            {
                return;
            }
            var bones = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform bone = Animator.GetBoneTransform((HumanBodyBones)i);
                if (bone != null)
                {
                    bones.Add(bone);
                }
            }
            _snapshotBones = bones.ToArray();
            _snapshotRotations = new Quaternion[_snapshotBones.Length];
            for (int i = 0; i < _snapshotBones.Length; i++)
            {
                _snapshotRotations[i] = _snapshotBones[i].rotation;
            }
            Transform hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            _snapshotHipsPosition = hips != null ? hips.position : Vector3.zero;
            Transform head = Animator.GetBoneTransform(HumanBodyBones.Head);
            _headWorldAtCapture = head != null ? head.position : _snapshotHipsPosition;
        }

        private void LateUpdate()
        {
            float clipShare = BlendFromSnapshot();
            if (IsStandingUp)
            {
                // The get-up was made on a body with shorter arms: its hands went under the floor (the capsule, moved to where the body lay,
                // stands on it). Faded in with the clip: the pose the ragdoll lay in is on the floor already, and clamping it in full swung
                // its elbows and knees on the first frame.
                LimbFloorClamp.Apply(Animator, transform.position.y, clipShare);
            }
        }

        /// <summary>Blends the captured ragdoll pose into the clip; returns how much of the pose is the clip's (1 once the blend is over).</summary>
        private float BlendFromSnapshot()
        {
            if (_blendElapsed < 0.0f || _snapshotBones == null)
            {
                return 1.0f;
            }
            _blendElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_blendElapsed / StandUpBlendSeconds);
            float eased = t * t * (3.0f - 2.0f * t);
            Transform hips = Animator != null ? Animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (hips != null)
            {
                hips.position = Vector3.Lerp(_snapshotHipsPosition, hips.position, eased);
            }
            for (int i = 0; i < _snapshotBones.Length; i++)
            {
                if (_snapshotBones[i] != null)
                {
                    _snapshotBones[i].rotation = Quaternion.Slerp(_snapshotRotations[i], _snapshotBones[i].rotation, eased);
                }
            }
            if (t >= 1.0f)
            {
                _blendElapsed = -1.0f;
                _snapshotBones = null;
            }
            return eased;
        }

        private void FinishStandingUp()
        {
            _standingUpElapsed = -1.0f;
            _limp = false;
            _restSeconds = 0.0f;
            _allowImmediateStandUp = false;

            // Back to normal pace for whatever plays next (Locomotion) --
            // see StandUpPlaybackSpeed's own doc comment. Set unconditionally,
            // not only when Animator != null was true in BeginStandingUp:
            // Animator can be reassigned/rebound between the two calls, and
            // a stale sped-up multiplier left on it would speed up every
            // other animation this rig ever plays afterward.
            if (Animator != null)
            {
                Animator.speed = 1.0f;
                // The GettingUp state has no exit of its own (the cutscene states are driven by name), so it would hold its last pose for
                // good: hand the body back to locomotion explicitly, blended.
                if (Animator.HasState(0, Animator.StringToHash("Locomotion")))
                {
                    Animator.CrossFadeInFixedTime("Locomotion", StandUpHandBackBlendSeconds, 0);
                }
            }

            if (_controller != null)
            {
                _controller.enabled = true;
            }
            if (_locomotion != null)
            {
                _locomotion.enabled = true;
            }
        }

        /// <summary>
        /// Moves the standing capsule to wherever the ragdoll actually came
        /// to rest. NOTHING moves the root transform during a ragdoll --
        /// only the individual Rigidbodies do, driven by physics -- so it
        /// is frozen at whatever position Activate() was called from, which
        /// could be metres from a jetpack crash's own tumble-and-slide.
        /// Standing up without correcting for that would snap the player
        /// back to the original impact point instead of where they are
        /// lying, a visible teleport.
        ///
        /// GROUND FOUND BY RAYCAST, not just "wherever the hips currently
        /// are" -- a lying-down body's hips sit close to the floor but not
        /// exactly on it, and the exact offset varies with the pose the
        /// ragdoll happened to settle into. The rig's OWN colliders are
        /// disabled for the raycast (SetBodyCollidersEnabled) -- staying
        /// enabled while kinematic, per SetKinematic's own comment, they
        /// would otherwise be the first (and only) thing directly below the
        /// hips that a downward ray finds.
        /// </summary>
        private void RepositionToRagdollRest()
        {
            if (Torso == null)
            {
                return;
            }
            // Where the ragdoll's hips WERE (captured before the clip was posed over it), not where the bones are now.
            Vector3 hips = _snapshotBones != null ? _snapshotHipsPosition : Torso.transform.position;

            SetBodyCollidersEnabled(false);
            float groundY = hips.y;
            if (Physics.Raycast(hips + Vector3.up * 3.0f, Vector3.down, out RaycastHit hit, 10.0f))
            {
                groundY = hit.point.y;
            }
            SetBodyCollidersEnabled(true);

            transform.position = new Vector3(hips.x, groundY, hips.z);

            // Facing follows wherever the torso ended up, flattened level --
            // standing up should not leave the character aimed at the sky
            // or the ground from whatever angle it happened to land at.
            Vector3 forward = Torso.transform.forward;
            forward.y = 0.0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }

            AlignClipToLyingBody(hips);
        }

        /// <summary>
        /// Turns and slides the root so the get-up clip's FIRST frame (already posed: see BeginStandingUp) lies where the ragdoll lies: its
        /// head pointing the same way along the ground, its hips on the ragdoll's hips. Without this the clip starts facing whichever way
        /// the root happened to point, so a body that landed sideways to it blends round through a quarter turn.
        /// </summary>
        private void AlignClipToLyingBody(Vector3 ragdollHips)
        {
            if (Animator == null || !Animator.isHuman || _snapshotBones == null)
            {
                return;
            }
            Transform clipHips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform clipHead = Animator.GetBoneTransform(HumanBodyBones.Head);
            if (clipHips == null || clipHead == null)
            {
                return;
            }
            // Where the ragdoll's head lies: its captured pose is the snapshot, so ask the snapshot (the bones themselves now show the clip).
            Vector3 ragdollHead = _headWorldAtCapture;
            Vector3 lying = ragdollHead - _snapshotHipsPosition;
            lying.y = 0.0f;
            Vector3 clipLying = clipHead.position - clipHips.position;
            clipLying.y = 0.0f;
            if (lying.sqrMagnitude > 0.01f && clipLying.sqrMagnitude > 0.01f)
            {
                float turn = Vector3.SignedAngle(clipLying, lying, Vector3.up);
                transform.RotateAround(clipHips.position, Vector3.up, turn);
            }
            Vector3 slide = _snapshotHipsPosition - clipHips.position;
            slide.y = 0.0f;
            transform.position += slide;
        }

        private Vector3 _headWorldAtCapture;

        private void SetBodyCollidersEnabled(bool enabled)
        {
            if (Bodies == null)
            {
                return;
            }
            foreach (Rigidbody body in Bodies)
            {
                if (body == null)
                {
                    continue;
                }
                Collider collider = body.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = enabled;
                }
            }
        }

        private void FixedUpdate()
        {
            if (!_limp || Bodies == null)
            {
                return;
            }

            // See ExtraGravityScale. Applied as acceleration rather than force
            // so it pulls every body equally regardless of mass -- gravity does
            // not care what a forearm weighs.
            Vector3 extra = Physics.gravity * ExtraGravityScale;
            foreach (Rigidbody body in Bodies)
            {
                if (body != null)
                {
                    body.AddForce(extra, ForceMode.Acceleration);
                }
            }

            // NOT WHILE STANDING UP -- bodies are kinematic again and being
            // driven by RepositionToRagdollRest/the GettingUp clip at that
            // point; a depenetration correction has no business fighting
            // either one.
            if (!IsStandingUp)
            {
                TickLiveDepenetration();
            }
        }

        private void SetKinematic(bool kinematic)
        {
            if (Bodies == null)
            {
                return;
            }
            foreach (Rigidbody body in Bodies)
            {
                if (body == null)
                {
                    continue;
                }
                // Interpolation only while limp, never on a bone the Animator is writing -- see RagdollBuilder.MakeBody.
                if (kinematic)
                {
                    body.interpolation = RigidbodyInterpolation.None;
                }
                body.isKinematic = kinematic;
                if (!kinematic)
                {
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                }
                // Colliders stay enabled either way; a kinematic body's
                // collider costs nothing and toggling them is one more thing to
                // get out of step with the kinematic flag.
                if (!kinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }
    }
}
