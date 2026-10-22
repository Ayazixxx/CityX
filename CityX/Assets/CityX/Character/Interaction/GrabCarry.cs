using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// Skyrim's carry (owner, 2026-10-21: "make it so holding E on objects carrys them while tapping E key equips it. make the holding system
    /// like skyrims"). HOLD E on something in reach that the player looks at (<see cref="Carryable"/>: an item lying in the world, a dropped
    /// item, a limp body's bone) and it is lifted to float in front of them; it follows where they look and keeps the turn it was picked up at.
    /// LET GO of E and it drops; CLICK while holding it and it is thrown. A TAP of E is a pickup, each item's own (<see cref="InteractKey"/>).
    ///
    /// The carried thing is pulled by a bounded spring force, not placed: it bumps into walls, and something heavy (a body's hips) lags where a
    /// light item snaps to the hands. A lifted item floats free of gravity, as in Skyrim, and turns back to its held angle; a bone keeps both, so
    /// a body hangs from the part that was grabbed.
    ///
    /// Movement and looking stay free while carrying; only combat is held off (PlayerInputReader.TickCombat).
    ///
    /// Only on this machine: what is carried, and where it ends up, is not sent to other players yet.
    /// </summary>
    public sealed class GrabCarry : MonoBehaviour
    {
        /// <summary>How far from the feet something can be lifted.</summary>
        public const float GrabRange = 3.0f;

        /// <summary>How directly the player must look at something to lift it (cosine of the cone's half-angle).</summary>
        public const float LookDot = 0.55f;

        /// <summary>Where things are held from: chest height on the 2.6 m body.</summary>
        public const float HoldHeight = 1.6f;

        public const float CarryMinDistance = 1.3f;
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

        /// <summary>How fast a held item turns back to the angle it was picked up at (per second).</summary>
        public const float TurnBackRate = 10.0f;

        /// <summary>How fast a thrown thing leaves the hands.</summary>
        public const float ThrowSpeed = 10.0f;

        public PlayerVitals Vitals;

        [Tooltip("The scene's single camera transform: where the player looks.")]
        public Transform Eye;

        public Rigidbody CarriedBody { get; private set; }
        public bool IsCarrying => CarriedBody != null;

        /// <summary>Carrying, or throwing this frame: the hands are not free to fight.</summary>
        public bool HandsFull => IsCarrying || _threwOnFrame == Time.frameCount;

        private int _threwOnFrame = -1;
        private float _carryDistance;
        private bool _floats;
        private bool _hadGravity;
        private Quaternion _heldTurn;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Tick(InteractKey.HoldBegan, InteractKey.IsDown, mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        /// <summary>
        /// One step of the hold/carry: lift on <paramref name="holdBegan"/>, drop once E is up, throw on <paramref name="throwPressed"/>. Public
        /// and fed the key state, so a test can drive it without a keyboard.
        ///
        /// DEATH IS POLLED HERE, NOT EVENT-DRIVEN: an OnEnable subscription to Vitals.Died bound before the scene builder assigned Vitals.
        /// </summary>
        public void Tick(bool holdBegan, bool interactDown, bool throwPressed)
        {
            if (CarriedBody != null)
            {
                if ((Vitals != null && Vitals.IsDead) || !interactDown)
                {
                    Drop();
                }
                else if (throwPressed)
                {
                    Throw();
                }
                return;
            }
            if (!holdBegan || (Vitals != null && (Vitals.IsDead || Vitals.IsStunned)))
            {
                return;
            }
            GameObject target = FindGrabTarget();
            if (target != null)
            {
                StartCarry(Carryable.Physical(target));
            }
        }

        private void FixedUpdate()
        {
            if (CarriedBody != null)
            {
                ApplyCarryForce();
            }
        }

        /// <summary>The liftable thing in reach the player looks at most directly, or null.</summary>
        public GameObject FindGrabTarget()
        {
            GameObject best = null;
            float bestDot = float.MinValue;
            Collider[] near = Physics.OverlapSphere(transform.position + Vector3.up * HoldHeight * 0.5f, GrabRange, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider hit in near)
            {
                if (Carryable.TryFind(hit, transform, out GameObject thing, out Vector3 centre) && thing != best &&
                    (centre - transform.position).sqrMagnitude <= GrabRange * GrabRange)
                {
                    float dot = LookDotTo(centre);
                    if (dot >= LookDot && dot > bestDot)
                    {
                        best = thing;
                        bestDot = dot;
                    }
                }
            }
            return best;
        }

        /// <summary>How directly the player looks at <paramref name="point"/>: the cosine between the look and the way to it.</summary>
        private float LookDotTo(Vector3 point)
        {
            Vector3 from = Eye != null ? Eye.position : transform.position;
            Vector3 toPoint = point - from;
            return toPoint.sqrMagnitude > 1e-4f ? Vector3.Dot(LookForward(), toPoint.normalized) : 1.0f;
        }

        public void StartCarry(Rigidbody body)
        {
            CarriedBody = body;
            _carryDistance = GrabCarryMath.ClampCarryDistance(Vector3.Distance(HoldPoint(), body.worldCenterOfMass), CarryMinDistance, CarryMaxDistance);
            // A loose thing floats and keeps its turn; a bone hangs off its body.
            _floats = body.GetComponent<Joint>() == null;
            _hadGravity = body.useGravity;
            if (_floats)
            {
                body.useGravity = false;
            }
            _heldTurn = Quaternion.Inverse(LookYaw()) * body.rotation;
            // It must not shove the body carrying it.
            foreach (Collider mine in GetComponentsInChildren<Collider>())
            {
                foreach (Collider its in body.GetComponentsInChildren<Collider>())
                {
                    Physics.IgnoreCollision(mine, its, true);
                }
            }
        }

        public void Drop()
        {
            if (CarriedBody != null)
            {
                CarriedBody.useGravity = _hadGravity;
            }
            CarriedBody = null;
        }

        /// <summary>Lets go with a push along the look: Skyrim's throw.</summary>
        public void Throw()
        {
            Rigidbody body = CarriedBody;
            Drop();
            _threwOnFrame = Time.frameCount;
            body.linearVelocity += LookForward() * ThrowSpeed;
        }

        private void ApplyCarryForce()
        {
            Vector3 target = GrabCarryMath.CarryTarget(HoldPoint(), LookForward(), _carryDistance);
            if (GrabCarryMath.ShouldAutoDrop(target, CarriedBody.worldCenterOfMass, MaxCarryDistanceBeforeAutoDrop))
            {
                Drop();
                return;
            }
            Vector3 force = GrabCarryMath.SpringForce(
                target, CarriedBody.worldCenterOfMass, CarriedBody.linearVelocity, CarrySpringK, CarryDampingK, MaxCarryForce);
            CarriedBody.AddForce(force, ForceMode.Force);
            if (_floats)
            {
                CarriedBody.angularVelocity = GrabCarryMath.TurnTowards(CarriedBody.rotation, LookYaw() * _heldTurn, TurnBackRate);
            }
        }

        private Vector3 HoldPoint() => transform.position + Vector3.up * HoldHeight;

        private Vector3 LookForward() => Eye != null ? Eye.forward : transform.forward;

        /// <summary>The look's turn about the vertical alone: a held thing turns with the player, not with the camera's pitch.</summary>
        private Quaternion LookYaw()
        {
            Vector3 flat = Vector3.ProjectOnPlane(LookForward(), Vector3.up);
            return flat.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(flat, Vector3.up) : Quaternion.identity;
        }
    }
}
