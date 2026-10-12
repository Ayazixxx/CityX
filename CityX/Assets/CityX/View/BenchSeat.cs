using CityX.Character;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.View
{
    /// <summary>
    /// A park bench you can walk up to and press E to sit on. EXPLICIT ASK,
    /// layered onto the pedestrian-zone/plaza work: "add benches you can go
    /// up to and press E to sit as well." A genuinely new feature -- nothing
    /// named bench or sit exists anywhere in the original Godot project
    /// (confirmed by search), so there is nothing to port here, unlike most
    /// of this codebase.
    ///
    /// OWNS ITS OWN PROXIMITY, same shape as Radio/BuildingDoor -- see
    /// Radio's own doc comment on why there is no shared "nearby
    /// interactable" registry in this project. Anchored on PlayerInputReader
    /// rather than a raw CharacterController/Transform because Begin/
    /// EndBenchSeat already live there, and Vehicles/Car.cs already
    /// establishes that PlayerInputReader and CharacterController always
    /// share the same root GameObject.
    ///
    /// SEATING REUSES THE CAR'S OWN "something else owns the frame" TRICK
    /// (PlayerInputReader._seatedBench, _seatedCar's sibling) rather than a
    /// new state machine: while seated, PlayerInputReader.Update() returns
    /// before touching movement/combat, so nothing here has to separately
    /// suppress WASD. A bench needs none of a car's extra machinery, though
    /// -- no transition-in-progress polling, no driving-camera widening, no
    /// reparenting onto a moving seat (a bench never moves) -- so this talks
    /// to PlayerLocomotion.SetSitting directly and disables the
    /// CharacterController itself instead, the same way Car.CompleteEnter/
    /// CompleteExit do it.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class BenchSeat : MonoBehaviour
    {
        public const float InteractRange = 2.0f;

        // EXPLICIT ASK: "make the bench a lil bigger so it fits better" --
        // bumped from the original human-proportioned dimensions once a
        // real sitting pose (ApplySittingPose) showed the seat was a
        // little cramped for this rig's own body.
        public const float SeatWidth = 1.8f;
        public const float SeatDepth = 0.55f;
        public const float SeatThickness = 0.06f;
        public const float SeatHeight = 0.45f;
        public const float BackrestHeight = 0.55f;
        public const float LegInset = 0.15f;
        public const float LegThickness = 0.07f;

        /// <summary>
        /// Where the sitter's root lands: position and facing (facing
        /// should point AWAY from the backrest). Set by Build(); exposed so
        /// a test can check it without re-deriving the geometry.
        /// </summary>
        public Transform SeatPoint { get; private set; }

        private PlayerInputReader _nearby;
        private PlayerInputReader _seated;

        public bool IsOccupied => _seated != null;

        /// <summary>For tests, same purpose as PistolWeapon's own identically-shaped property.</summary>
        public bool HasNearbyPlayer => _nearby != null;

        /// <summary>
        /// Builds the visible bench (seat, backrest, two legs -- plain
        /// primitive cubes, the same idiom BuildingDoor.Hang already uses
        /// for its own slab/handle) and the interact trigger. Local space:
        /// the seat's own centre sits at local origin, +Z is the direction
        /// a sitter faces (away from the backrest, which sits at -Z).
        /// </summary>
        public void Build(Material material)
        {
            GameObject seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seat.name = "Seat";
            seat.transform.SetParent(transform, false);
            seat.transform.localPosition = new Vector3(0.0f, SeatHeight, 0.0f);
            seat.transform.localScale = new Vector3(SeatWidth, SeatThickness, SeatDepth);
            if (material != null)
            {
                seat.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            GameObject backrest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backrest.name = "Backrest";
            backrest.transform.SetParent(transform, false);
            backrest.transform.localPosition = new Vector3(
                0.0f, SeatHeight + BackrestHeight * 0.5f, -SeatDepth * 0.5f);
            backrest.transform.localScale = new Vector3(SeatWidth, BackrestHeight, SeatThickness);
            if (material != null)
            {
                backrest.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            // Two legs, not four -- a bench this narrow reads the same
            // either way and it halves the primitive count for something
            // spawned all over a car-free plaza.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Leg";
                leg.transform.SetParent(transform, false);
                leg.transform.localPosition = new Vector3(
                    side * (SeatWidth * 0.5f - LegInset), SeatHeight * 0.5f, 0.0f);
                leg.transform.localScale = new Vector3(LegThickness, SeatHeight, SeatDepth * 0.8f);
                if (material != null)
                {
                    leg.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
            }

            // BELOW THE SEAT SURFACE, NOT AT IT -- EXPLICIT MEASUREMENT,
            // not a guess: the Hips bone sits at a FIXED local offset
            // above the rig's own root (BodyRig.HipY, ~0.9m -- "hip height
            // above the feet" for a standing person), and Hips is the
            // PARENT of the leg bones ApplySittingPose rotates, so bending
            // the thighs/shins has NO effect on the hips' own world
            // position -- only on the knee/foot descending from it. A
            // scratch diagnostic confirmed this the hard way: placing the
            // root AT SeatHeight put the hips at SeatHeight + HipY (about
            // 1.35m, standing-tall height) with the bent legs just folded
            // up in front like an airborne L-sit, nowhere near the seat.
            // Solving for the root that puts the HIPS at a realistic
            // seated pelvis height instead (SeatHeight plus a small real
            // hip-joint-above-cushion clearance): root = target hip height
            // minus BodyRig.HipY -- which comes out BELOW the bench's own
            // ground datum, exactly compensating for how much closer the
            // hips sit to the ground once the legs are folded.
            //
            // Z is biased slightly toward the backrest, matching how a
            // real seated pelvis sits closer to the back of a bench than
            // its front edge.
            const float PelvisAboveSeatClearance = 0.10f;
            float targetHipHeight = SeatHeight + PelvisAboveSeatClearance;
            float rootHeight = targetHipHeight - CityX.Character.BodyRig.HipY;

            var seatPoint = new GameObject("SeatPoint");
            seatPoint.transform.SetParent(transform, false);
            seatPoint.transform.localPosition = new Vector3(0.0f, rootHeight, -SeatDepth * 0.15f);
            seatPoint.transform.localRotation = Quaternion.identity;
            SeatPoint = seatPoint.transform;

            SphereCollider trigger = GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = InteractRange;
            trigger.center = new Vector3(0.0f, SeatHeight, 0.0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            var reader = other.GetComponentInParent<PlayerInputReader>();
            if (reader != null)
            {
                _nearby = reader;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!CityX.Character.InteractionReach.IsBodyLeaving(other))
            {
                return;   // a limb's collider swinging out of the trigger is not the player walking away
            }
            if (_nearby != null && other.transform.IsChildOf(_nearby.transform))
            {
                _nearby = null;
            }
        }

        private void Update()
        {
            // A collider disabled while still inside the trigger never fires
            // OnTriggerExit -- same staleness Radio's own Update guards
            // against, e.g. the player dying (Ragdoll.Activate turns their
            // CharacterController off mid-frame) while merely standing near
            // an unoccupied bench.
            if (_nearby != null && _seated == null &&
                InteractionReach.IsBeyond(Vector3.Distance(_nearby.transform.position, transform.position), InteractRange))
            {
                _nearby = null;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (_seated == null)
            {
                if (_nearby != null && keyboard.eKey.wasPressedThisFrame)
                {
                    Sit(_nearby);
                }
                return;
            }

            // STAND ON E, OR ON ANY MOVEMENT KEY -- pressing W/A/S/D to walk
            // away is the more natural "get up" gesture than making E do
            // double duty as the only way out; matches how getting off a
            // real bench works, not a deliberate menu action.
            if (keyboard.eKey.wasPressedThisFrame ||
                keyboard.wKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame ||
                keyboard.sKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame ||
                keyboard.spaceKey.wasPressedThisFrame)
            {
                StandUp();
            }
        }

        private void Sit(PlayerInputReader reader)
        {
            if (IsOccupied)
            {
                return;
            }

            CharacterController controller = reader.GetComponent<CharacterController>();
            if (controller == null)
            {
                return;
            }

            _seated = reader;
            controller.enabled = false;
            reader.transform.SetPositionAndRotation(SeatPoint.position, SeatPoint.rotation);
            reader.BeginBenchSeat(gameObject);
        }

        private void StandUp()
        {
            if (_seated == null)
            {
                return;
            }

            CharacterController controller = _seated.GetComponent<CharacterController>();
            _seated.EndBenchSeat();
            if (controller != null)
            {
                controller.enabled = true;
            }

            _nearby = _seated;
            _seated = null;
        }
    }
}
