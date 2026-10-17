using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Local 3D obstacle avoidance for something that flies -- the airborne
    /// counterpart to NpcSteering, same "not pathfinding, just reactive local
    /// steering" scope and the same reasoning it is provably enough: every
    /// obstacle this city generates is a convex box, and a flier has an
    /// escape a walker does not -- it can always just go OVER one. Climbing
    /// is therefore the whole avoidance strategy here, not a fallback:
    /// simpler than picking a side to go round, and just as reliable against
    /// a convex obstacle, with only one axis to correct instead of two.
    ///
    /// ALSO OWNS ALTITUDE ABOVE THE REAL GROUND, for the same reason
    /// NpcSteering raycasts rather than trusting arithmetic: there is no
    /// "building height at this XZ" query anywhere in CityX.World
    /// (CityLayout only knows street-vs-lot, not how tall whatever got built
    /// on a lot actually is), so a straight-down raycast against the real
    /// streamed geometry is the only accurate source for "how far above
    /// whatever is actually beneath me right now."
    /// </summary>
    public sealed class FlyingNpcSteering
    {
        /// <summary>
        /// Where it settles once nothing is wrong -- comfortably over this
        /// city's own low/mid-rise rooftops (7-85m), well under its towers
        /// (110-210m) and supertalls (up to 320m), which is exactly why this
        /// cannot just be "always fly above the tallest possible building"
        /// -- that would put it absurdly high nearly all the time.
        /// </summary>
        public const float CruiseAltitudeAboveSurface = 35.0f;

        /// <summary>A hard floor -- climbing kicks in below this regardless of what the forward probe sees.</summary>
        public const float MinAltitudeAboveSurface = 18.0f;

        public const float ObstacleLookaheadDistance = 30.0f;

        /// <summary>
        /// Deliberately much faster than CruiseAdjustRate -- an obstacle
        /// ahead or an unsafe height is urgent; drifting back to the cruise
        /// band once clear is not, and easing both at the same rate would
        /// make clearing a real building look exactly as lazy as ordinary
        /// station-keeping.
        /// </summary>
        public const float ClimbRate = 14.0f;

        public const float CruiseAdjustRate = 5.0f;

        /// <summary>Dead zone around CruiseAltitudeAboveSurface so it settles rather than hunting up and down forever.</summary>
        public const float CruiseTolerance = 2.0f;

        public const float CruiseSpeed = 9.0f;

        private const float GroundProbeDistance = 1000.0f;

        /// <summary>What counts as something to avoid or measure height against. World geometry by default, same as NpcSteering's own.</summary>
        public LayerMask ObstacleMask = ~0;

        public bool IsAvoiding { get; private set; }

        public float HeightAboveSurface { get; private set; } = CruiseAltitudeAboveSurface;

        /// <summary>
        /// The actual velocity to fly this frame -- horizontal at a fixed
        /// cruise speed along <paramref name="desiredHeading"/>, vertical
        /// from whatever the two probes below find.
        ///
        /// UNLIKE NpcSteering.Steer, RETURNS A REAL VELOCITY, not a unit
        /// direction the caller re-scales by its own speed -- a flier's
        /// vertical and horizontal rates are genuinely independent numbers
        /// here (ClimbRate/CruiseAdjustRate vs CruiseSpeed), not one shared
        /// walking pace, so folding them into a single normalized direction
        /// would lose the distinction between them.
        /// </summary>
        public Vector3 Steer(Transform body, Vector3 desiredHeading, float deltaTime)
        {
            Vector3 heading = new Vector3(desiredHeading.x, 0.0f, desiredHeading.z);
            heading = heading.sqrMagnitude > 0.000001f ? heading.normalized : Vector3.forward;

            bool blockedAhead = Physics.Raycast(body.position, heading, ObstacleLookaheadDistance,
                ObstacleMask, QueryTriggerInteraction.Ignore);

            // Triggers ignored: same reasoning NpcSteering's own Cast has --
            // the indoor volumes covering whole buildings are triggers, and
            // trusting them here would make every rooftop read as bottomless.
            float surfaceY = Physics.Raycast(body.position, Vector3.down, out RaycastHit hit,
                    GroundProbeDistance, ObstacleMask, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                // Nothing found (off the edge of the streamed world, or a
                // gap) -- assumed already safe rather than climbing forever
                // chasing a surface that was never there to find.
                : body.position.y - CruiseAltitudeAboveSurface;

            HeightAboveSurface = body.position.y - surfaceY;
            IsAvoiding = blockedAhead || HeightAboveSurface < MinAltitudeAboveSurface;

            float verticalVelocity;
            if (IsAvoiding)
            {
                verticalVelocity = ClimbRate;
            }
            else if (HeightAboveSurface > CruiseAltitudeAboveSurface + CruiseTolerance)
            {
                verticalVelocity = -CruiseAdjustRate;
            }
            else if (HeightAboveSurface < CruiseAltitudeAboveSurface - CruiseTolerance)
            {
                verticalVelocity = CruiseAdjustRate;
            }
            else
            {
                verticalVelocity = 0.0f;
            }

            Vector3 velocity = heading * CruiseSpeed;
            velocity.y = verticalVelocity;
            return velocity;
        }
    }
}
