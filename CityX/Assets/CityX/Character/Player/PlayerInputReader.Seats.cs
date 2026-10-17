using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// SITTING DOWN: getting into and out of a car (the camera pulled back while driving) and onto and off a bench.
    /// </summary>
    public sealed partial class PlayerInputReader
    {
        /// <summary>
        /// The car this body is seated in (driver or passenger), or null.
        /// Ported from player.gd's own _seated_car -- set/cleared only by
        /// Vehicles/Car.cs's own _begin_enter/_complete_exit equivalents,
        /// via BeginCarTransition/EndCarTransition below. Not used for
        /// anything beyond the null-check in Update(): which car it is
        /// doesn't matter to this class itself, only whether one owns the
        /// frame right now.
        /// </summary>
        private GameObject _seatedCar;

        /// <summary>
        /// The BenchSeat this body is sitting on, or null. Same "something
        /// else owns the frame right now" shape as _seatedCar, but its own
        /// separate field rather than reusing that one: a bench needs none
        /// of the car's own extra machinery (no camera-distance widening, no
        /// transition-in-progress polling), and naming the field after a
        /// car while a bench uses it would read as a bug to the next person
        /// touching this file. See BenchSeat.cs's own doc comment.
        /// </summary>
        private GameObject _seatedBench;

        private float _savedCameraDistance;

        /// <summary>
        /// Metres, ported from player.gd's own DRIVING_SPRING_LENGTH -- a
        /// car reads much bigger on screen than the player's own body,
        /// which the default on-foot ThirdPersonCamera.Distance was tuned
        /// against. Applies to the passenger seat too, not just the
        /// driver's -- both are inside the same car needing the same wider
        /// view.
        /// </summary>
        public const float DrivingCameraDistance = 7.0f;

        /// <summary>
        /// Called by Vehicles/Car.cs the instant a seat transition starts
        /// (before the entering_car animation even finishes) -- deliberately
        /// not deferred until the player is actually seated, so they can't
        /// walk away mid-animation. Ported from player.gd's own
        /// begin_car_transition; car itself isn't used by this class beyond
        /// the null-check (see _seatedCar's own doc comment).
        /// </summary>
        public void BeginCarTransition(GameObject car)
        {
            _seatedCar = car;
            if (_orbitCamera != null)
            {
                _savedCameraDistance = _orbitCamera.Distance;
                _orbitCamera.Distance = DrivingCameraDistance;
                // A locked-on/aiming camera has no business fighting the
                // car's own view swap -- same release entering any other
                // input-freezing state already uses (see the ragdoll/dead
                // branches above resetting _orbitCamera.IsAiming).
                _orbitCamera.IsAiming = false;
            }
            if (_locomotion != null)
            {
                _locomotion.InCarSeat = true;
            }
        }

        /// <summary>
        /// Called by Vehicles/Car.cs's own _complete_exit, once the player
        /// is already back in the world at a safe position. Ported from
        /// player.gd's own end_car_transition -- its own camera_pivot yaw/
        /// pitch fold-back has no Unity equivalent here: ThirdPersonCamera
        /// orbits Target's own live world transform rather than
        /// accumulating rotation onto a node the body itself carries, so
        /// there is nothing left over on the camera side for a reparent to
        /// leave stuck at an angle.
        /// </summary>
        public void EndCarTransition()
        {
            _seatedCar = null;
            if (_orbitCamera != null)
            {
                _orbitCamera.Distance = _savedCameraDistance;
            }
            if (_locomotion != null)
            {
                _locomotion.InCarSeat = false;
            }
        }

        /// <summary>
        /// Called by BenchSeat the instant a player sits down -- no
        /// transition-in-progress step to wait out, unlike
        /// BeginCarTransition, since PlayerLocomotion.ApplySittingPose
        /// bends the leg bones directly every frame rather than waiting on
        /// an Animator clip to finish.
        /// </summary>
        public void BeginBenchSeat(GameObject bench)
        {
            _seatedBench = bench;
            if (_locomotion != null)
            {
                _locomotion.SetSitting(true);
            }
        }

        /// <summary>Called by BenchSeat the instant a player stands back up.</summary>
        public void EndBenchSeat()
        {
            _seatedBench = null;
            if (_locomotion != null)
            {
                _locomotion.SetSitting(false);
            }
        }
    }
}
