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

        [Tooltip("Every rigid body in the rig. Filled in by RagdollBuilder.")]
        public Rigidbody[] Bodies;

        [Tooltip("The hips. Takes the initial spin and carries the rig.")]
        public Rigidbody Torso;

        [Tooltip("Switched off while limp so the animation stops fighting physics.")]
        public Animator Animator;

        // --- named individually for GrappleSwing's own partial ragdoll --
        // --- see ActivateSwing's own doc comment for why these specific
        // --- five bodies (not the rest) need to be addressable on their
        // --- own rather than just living in the flat Bodies array.
        [Tooltip("Filled in by RagdollBuilder. Kept kinematic and driven to the swing's own position during a partial ragdoll.")]
        public Rigidbody Chest;

        public Rigidbody Head;
        public Rigidbody LeftUpperArm;
        public Rigidbody LeftLowerArm;
        public Rigidbody RightUpperArm;
        public Rigidbody RightLowerArm;

        private CharacterController _controller;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private bool _limp;
        private bool _swinging;

        public bool IsLimp => _limp;

        /// <summary>True during GrappleSwing's own partial ragdoll -- see ActivateSwing's own doc comment.</summary>
        public bool IsSwinging => _swinging;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _vitals = GetComponent<PlayerVitals>();

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
            Activate(_locomotion != null ? _locomotion.Velocity : Vector3.zero);
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
        /// </summary>
        public void Activate(Vector3 velocity)
        {
            if (_limp)
            {
                return;   // a second killing blow must not re-launch the corpse
            }
            _limp = true;
            // A death mid-swing supersedes the partial ragdoll outright --
            // the loop below already forces every body non-kinematic
            // (chest/right arm included), so nothing here needs to treat
            // them specially the way ActivateSwing does. Cleared so
            // GrappleSwing's own still-in-flight TickSwingPose/
            // SetLeftArmPose calls this same frame become no-ops instead of
            // calling MovePosition on a body that is no longer kinematic.
            _swinging = false;

            if (Animator != null)
            {
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
        /// Goes PARTLY limp for a grapple swing -- explicit ask: "his right
        /// hand stay attached to it but the rest of his body is ragdolling".
        /// The Animator goes off exactly like a death ragdoll (nothing here
        /// should fight physics), but only the hips, head, legs and left arm
        /// actually become physics-driven. Chest and the whole right arm
        /// STAY kinematic -- GrappleSwing's own TickSwingPose drives Chest's
        /// position/rotation directly every frame to match the swing's own
        /// computed transform, and the CharacterJoint already connecting
        /// Chest to the (now non-kinematic) hips drags the hips -- and
        /// through their own joints, the legs and head -- along for the
        /// ride, exactly the way a real ragdoll gets dragged by whatever
        /// part of it is anchored. The right arm rides along with Chest
        /// through ordinary parent-child transform inheritance (it is
        /// kinematic too, so nothing fights that), holding whatever pose
        /// TickSwingPose gives it -- a reaching-for-the-rope shape rather
        /// than flailing loose, which is the whole visual point of keeping
        /// it out of the ragdoll at all.
        ///
        /// THE LEFT ARM IS THE ONE EXCEPTION THAT CAN FLIP BACK. It starts
        /// ragdolling like the hips/legs/head, but SetLeftArmPose can pull
        /// it back to kinematic for as long as a fireball cast's own raised-
        /// hand pose needs to hold -- see that method's own doc comment.
        /// </summary>
        public void ActivateSwing(Vector3 velocity)
        {
            if (_limp || _swinging)
            {
                return;
            }
            _swinging = true;

            if (Animator != null)
            {
                Animator.enabled = false;
            }

            SetBodyKinematic(Chest, true);
            SetBodyKinematic(RightUpperArm, true);
            SetBodyKinematic(RightLowerArm, true);

            foreach (Rigidbody body in Bodies)
            {
                if (body == null || body == Chest || body == RightUpperArm || body == RightLowerArm)
                {
                    continue;
                }
                SetBodyKinematic(body, false);
                body.linearVelocity = velocity;
            }

            if (Torso != null && !Torso.isKinematic)
            {
                Torso.angularVelocity =
                    Random.onUnitSphere * (TorsoSpinDegreesPerSecond * 0.5f * Mathf.Deg2Rad);
            }
        }

        /// <summary>
        /// Called every frame by GrappleSwing while a swing is active --
        /// drags Chest to the swing's own live position/rotation (see
        /// ActivateSwing's own doc comment for why that alone is enough to
        /// carry the whole ragdolling half of the rig with it), and holds
        /// the right arm in a fixed reaching pose relative to Chest. THE
        /// EXACT ANGLES ARE A FIRST GUESS, not yet checked against a real
        /// rendered pose the way every other procedural grip in this project
        /// eventually was (PistolWeapon.GripHandRotationEuler's own doc
        /// comment, etc.) -- reasonable starting values, meant to be
        /// render-tuned once actually seen swinging.
        /// </summary>
        public void TickSwingPose(Vector3 chestWorldPosition, Quaternion chestWorldRotation,
            Quaternion rightUpperArmLocalRotation, Quaternion rightLowerArmLocalRotation)
        {
            if (!_swinging)
            {
                return;
            }
            if (Chest != null)
            {
                Chest.MovePosition(chestWorldPosition);
                Chest.MoveRotation(chestWorldRotation);
            }
            if (RightUpperArm != null)
            {
                RightUpperArm.transform.localRotation = rightUpperArmLocalRotation;
            }
            if (RightLowerArm != null)
            {
                RightLowerArm.transform.localRotation = rightLowerArmLocalRotation;
            }
        }

        /// <summary>
        /// Pulls the left arm out of the swing's own ragdoll for as long as
        /// a fireball cast's own raised-hand pose needs it -- explicit ask:
        /// casting "still works, just comes from your left hand", which
        /// only reads as a deliberate cast rather than a loose flail if the
        /// arm actually holds the pose instead of following physics for
        /// that moment. Safe to call every frame with the same value; only
        /// touches kinematic state (and gives the flailing arm a
        /// non-explosive starting velocity on the way back OUT of kinematic,
        /// matching ActivateSwing's own "seed real velocity" reasoning)
        /// rather than resetting anything else about the swing.
        /// </summary>
        public void SetLeftArmPose(bool kinematic, Quaternion upperLocalRotation, Quaternion lowerLocalRotation)
        {
            if (!_swinging || LeftUpperArm == null || LeftLowerArm == null)
            {
                return;
            }
            if (kinematic)
            {
                SetBodyKinematic(LeftUpperArm, true);
                SetBodyKinematic(LeftLowerArm, true);
                LeftUpperArm.transform.localRotation = upperLocalRotation;
                LeftLowerArm.transform.localRotation = lowerLocalRotation;
            }
            else if (LeftUpperArm.isKinematic)
            {
                // Coming OUT of the held cast pose back into the ragdoll --
                // give it the chest's own current velocity rather than a
                // dead stop, the same "carry real motion into physics"
                // reasoning ActivateSwing/Activate already use.
                Vector3 velocity = Chest != null ? Chest.linearVelocity : Vector3.zero;
                SetBodyKinematic(LeftUpperArm, false);
                SetBodyKinematic(LeftLowerArm, false);
                LeftUpperArm.linearVelocity = velocity;
                LeftLowerArm.linearVelocity = velocity;
            }
        }

        /// <summary>Ends the swing's own partial ragdoll -- landed, released, or dashed out of it. Restores full animated control, same end state Deactivate leaves a death ragdoll in.</summary>
        public void DeactivateSwing()
        {
            if (!_swinging)
            {
                return;
            }
            _swinging = false;

            // Dying mid-swing already made Activate() take over (a full
            // ragdoll, not this partial one) -- Update ordering between
            // PlayerInputReader's own death check and PlayerVitals.Died
            // is not something to rely on, so this only clears the flag in
            // that case rather than undoing the real death ragdoll's own
            // kinematic state and Animator.
            if (_limp)
            {
                return;
            }

            SetKinematic(true);

            if (Animator != null)
            {
                Animator.enabled = true;
            }
        }

        private static void SetBodyKinematic(Rigidbody body, bool kinematic)
        {
            if (body == null)
            {
                return;
            }
            body.isKinematic = kinematic;
            if (!kinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
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
                body.isKinematic = kinematic;
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
