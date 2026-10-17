using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// Skyrim-style physics grab: hold "interact" (E) while the crosshair
    /// is on a Grabbable rigidbody to lift it into a loose, force-driven
    /// carry; a plain tap while already carrying drops it. Ported from
    /// player.gd's _process_grab_carry/_find_grab_target/_apply_carry_force.
    ///
    /// MOVEMENT AND LOOKING STAY FREE WHILE CARRYING, deliberately -- unlike
    /// the Map/Chat/Pause overlays, this never disables PlayerInputReader or
    /// ThirdPersonCamera. Godot's own comment on _carried_body is explicit
    /// that carrying does not early-return _physics_process; only combat
    /// input is gated on it (see PlayerInputReader.TickCombat's own guard).
    ///
    /// NO NETWORK SYNC. Godot's version RPCs carry state to other peers for
    /// a networked ragdoll target; this port has no multiplayer carry sync
    /// yet (nothing in this single-player-focused build is networked today
    /// -- see ClaimSystem's own note on the same gap). Purely local.
    ///
    /// READS Keyboard.current DIRECTLY IN ITS OWN Update(), matching
    /// BuildingDoor's own established pattern of every interactable asking
    /// for the key itself rather than being told by a shared dispatcher --
    /// this project has no such dispatcher and Godot does not either.
    /// </summary>
    public sealed class GrabCarry : MonoBehaviour
    {
        public const float GrabHoldSeconds = 0.35f;
        public const float GrabRange = 3.0f;
        public const float CarryMinDistance = 1.0f;
        public const float CarryMaxDistance = 2.5f;

        /// <summary>
        /// Tuned against this project's own ragdoll mass range (see
        /// RagdollBuilder's own Density comment) -- a real force, not a
        /// direct position/velocity override, is what makes "heavy
        /// resists, light flings around" fall out for free (F=ma) without
        /// any per-object tuning.
        /// </summary>
        public const float CarrySpringK = 120.0f;
        public const float CarryDampingK = 18.0f;

        /// <summary>Hard force clamp so a continuous force can't blow up the physics solver.</summary>
        public const float MaxCarryForce = 2500.0f;

        /// <summary>Auto-drop rather than let the spring force keep growing against an unmovable obstruction.</summary>
        public const float MaxCarryDistanceBeforeAutoDrop = 4.0f;

        public PlayerVitals Vitals;

        [Tooltip("The scene's single camera transform -- the raycast origin and carry-target direction.")]
        public Transform Eye;

        public Rigidbody CarriedBody { get; private set; }
        public bool IsCarrying => CarriedBody != null;

        private float _carryDistance;
        private float _grabHoldTimer;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            Tick(Time.deltaTime, keyboard.eKey.wasPressedThisFrame, keyboard.eKey.isPressed);
        }

        /// <summary>
        /// The hold/tap state machine advanced by one step. Public, and
        /// taking its own deltaTime/key state rather than reading them
        /// directly, so a test can drive it without a real Keyboard --
        /// matches PlayerVitals.TickStamina's own reasoning.
        ///
        /// DEATH IS POLLED HERE, NOT EVENT-DRIVEN. An OnEnable subscription
        /// to Vitals.Died looked like the obvious port of player.gd's own
        /// die-handling drop, but OnEnable fires the moment AddComponent
        /// runs -- before the scene builder's very next line assigns the
        /// externally-wired Vitals field, so the subscription silently
        /// bound to null and never fired. Checking Vitals.IsDead here
        /// instead, on the same frame that already reads it for the "no new
        /// grab while dead" gate below, has no such ordering hazard: Tick
        /// only ever runs long after Vitals has been assigned.
        /// </summary>
        public void Tick(float deltaTime, bool interactPressedThisFrame, bool interactHeld)
        {
            if (CarriedBody != null)
            {
                if ((Vitals != null && Vitals.IsDead) || interactPressedThisFrame)
                {
                    Drop();
                }
                return;
            }

            // No new grab can start while dead/stunned -- same gating every
            // other player-initiated action in this project already applies.
            if (Vitals != null && (Vitals.IsDead || Vitals.IsStunned))
            {
                _grabHoldTimer = 0.0f;
                return;
            }

            if (interactHeld)
            {
                _grabHoldTimer += deltaTime;
                if (_grabHoldTimer >= GrabHoldSeconds)
                {
                    _grabHoldTimer = 0.0f;
                    Rigidbody target = FindGrabTarget();
                    if (target != null)
                    {
                        StartCarry(target);
                    }
                }
            }
            else
            {
                _grabHoldTimer = 0.0f;
            }
        }

        private void FixedUpdate()
        {
            if (CarriedBody == null || Eye == null)
            {
                return;
            }
            ApplyCarryForce();
        }

        /// <summary>
        /// Camera-forward raycast, only ever returning something explicitly
        /// marked Grabbable AND actually dynamic right now -- a raw world
        /// hit, or a still-kinematic (living) ragdoll bone, never counts.
        /// </summary>
        public Rigidbody FindGrabTarget()
        {
            if (Eye == null)
            {
                return null;
            }
            if (!Physics.Raycast(Eye.position, Eye.forward, out RaycastHit hit, GrabRange))
            {
                return null;
            }
            Rigidbody body = hit.rigidbody;
            if (body != null && !body.isKinematic && body.GetComponent<Grabbable>() != null)
            {
                return body;
            }
            return null;
        }

        public void StartCarry(Rigidbody body)
        {
            CarriedBody = body;
            float raw = Eye != null ? Vector3.Distance(Eye.position, body.position) : CarryMinDistance;
            _carryDistance = GrabCarryMath.ClampCarryDistance(raw, CarryMinDistance, CarryMaxDistance);
        }

        private void ApplyCarryForce()
        {
            Vector3 target = GrabCarryMath.CarryTarget(Eye.position, Eye.forward, _carryDistance);
            if (GrabCarryMath.ShouldAutoDrop(target, CarriedBody.position, MaxCarryDistanceBeforeAutoDrop))
            {
                Drop();
                return;
            }
            Vector3 force = GrabCarryMath.SpringForce(
                target, CarriedBody.position, CarriedBody.linearVelocity, CarrySpringK, CarryDampingK, MaxCarryForce);
            CarriedBody.AddForce(force, ForceMode.Force);
        }

        public void Drop()
        {
            CarriedBody = null;
        }
    }
}
