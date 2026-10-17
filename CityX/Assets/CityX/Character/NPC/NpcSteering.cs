using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Local obstacle avoidance for anything that walks toward a point.
    /// Ported from npc/npc_steering.gd.
    ///
    /// Every NPC chases the same way: work out the direction to the goal, set
    /// velocity along it, move. That is a STRAIGHT LINE, and a controller's own
    /// sliding is all that happens when a building is in the way -- so an NPC
    /// whose target is on the far side of a block walks into the wall and
    /// grinds along it forever. That is the bug this exists to fix.
    ///
    /// IT IS NOT PATHFINDING. There is no navmesh anywhere in this project, and
    /// baking one per chunk would land on the streaming budget, which is the
    /// standing first priority. Pure local steering is enough here for a
    /// specific geometric reason: every obstacle the city generates is a CONVEX
    /// box. Wall-following provably escapes a convex obstacle -- you round it
    /// and resume -- so the cheap answer is also the correct one for this
    /// world.
    ///
    /// WHAT IT WILL NOT DO, said plainly rather than left to be discovered: it
    /// will not follow somebody through a doorway and up a staircase. Building
    /// interiors are genuinely concave, and local steering gets trapped in
    /// concave geometry. Chasing you indoors needs real pathfinding, which is a
    /// separate piece of work rather than a tuning tweak to this one.
    /// </summary>
    public sealed class NpcSteering
    {
        /// <summary>
        /// Reaching the goal is worth more than hugging the wall exactly. The
        /// returned direction is the wall tangent blended this far back toward
        /// the goal, which is what makes an NPC cut the corner and peel off the
        /// moment the building stops being in the way, rather than tracing its
        /// whole perimeter.
        /// </summary>
        public const float GoalBias = 0.35f;

        /// <summary>
        /// How long a chosen side is held before it can be reconsidered. THE
        /// most important number here. Without it, an NPC standing square on to
        /// a flat wall re-picks left or right every frame as its own tiny
        /// movements flip which tangent scores better, and it jitters in place
        /// instead of going anywhere -- the classic local-steering failure.
        /// Long enough to clear a corner, short enough that a wrong guess is
        /// cheap.
        ///
        /// The commitment is also dropped EARLY, the moment the straight line
        /// is clear again; this is only the ceiling for when it never clears.
        /// </summary>
        public const float CommitSeconds = 1.1f;

        /// <summary>
        /// Probe height above the body's origin, as a fraction of its own
        /// height. Deliberately up at chest level: a ray along the floor hits
        /// the kerb every street has and reads a 6cm step as a wall.
        /// </summary>
        public const float ProbeHeightFraction = 0.45f;

        public const float LookAheadRadiusMultiple = 2.5f;
        public const float LookAheadMargin = 1.6f;

        /// <summary>What counts as something to steer around. World geometry by default.</summary>
        public LayerMask ObstacleMask = ~0;

        public float BodyRadius { get; private set; } = 0.4f;
        public float ProbeHeight { get; private set; } = 1.0f;
        public float LookAhead { get; private set; } = 2.6f;

        /// <summary>
        /// The side committed to, as a signed multiplier on the left vector:
        /// -1 right, +1 left, 0 for not avoiding anything.
        /// </summary>
        private float _commitSide;

        private float _commitRemaining;

        public bool IsAvoiding => _commitSide != 0.0f;

        /// <summary>
        /// Sizes the probe by hand. <paramref name="width"/> is the body's own
        /// half-width across, <paramref name="topY"/> how far above its origin
        /// its head is, and <paramref name="reach"/> its longest horizontal
        /// half-extent.
        /// </summary>
        public void Configure(float width, float topY, float reach)
        {
            BodyRadius = Mathf.Max(width, 0.05f);
            ProbeHeight = Mathf.Max(topY * ProbeHeightFraction, 0.2f);
            LookAhead = Mathf.Max(reach, width) * LookAheadRadiusMultiple + LookAheadMargin;
        }

        /// <summary>
        /// Sizes the probe off the body's OWN controller, so an NPC cannot
        /// drift out of sync with its collider the way a hand-copied number
        /// would.
        /// </summary>
        public void ConfigureFrom(CharacterController controller)
        {
            if (controller == null)
            {
                return;
            }
            float top = controller.center.y + controller.height * 0.5f;
            Configure(controller.radius, top, controller.radius);
        }

        /// <summary>
        /// The direction to actually walk this frame.
        ///
        /// <paramref name="desired"/> is the unit direction toward the goal --
        /// exactly what the NPC already computed. The result is that same
        /// direction back again whenever nothing is in the way, so an NPC
        /// crossing open ground moves in precisely the straight line it always
        /// did and costs three raycasts to prove it.
        /// </summary>
        public Vector3 Steer(Transform body, Vector3 desired, float deltaTime)
        {
            var goal = new Vector3(desired.x, 0.0f, desired.z);
            if (goal.sqrMagnitude < 0.000001f)
            {
                return desired;
            }
            goal.Normalize();

            _commitRemaining = Mathf.Max(0.0f, _commitRemaining - deltaTime);

            Vector3 origin = body.position + Vector3.up * ProbeHeight;
            if (!Blocked(origin, goal))
            {
                // Dropped HERE rather than left to time out, which is what
                // makes an NPC peel off a wall the instant it has cleared the
                // corner instead of tracing the rest of the building.
                Reset();
                return goal;
            }

            Vector3 left = Vector3.Cross(Vector3.up, goal).normalized;
            if (_commitRemaining <= 0.0f || _commitSide == 0.0f)
            {
                _commitSide = ChooseSide(body, origin, goal, left);
                _commitRemaining = CommitSeconds;
            }

            Vector3 along = (left * _commitSide).normalized;
            Vector3 steered = along + goal * GoalBias;
            return steered.sqrMagnitude < 0.000001f ? along : steered.normalized;
        }

        /// <summary>
        /// Which way to go round.
        ///
        /// PREFERS THE SIDE THAT IS ACTUALLY OPEN -- a real probe down each
        /// tangent -- over anything derived from the surface normal. At an
        /// outside corner both tangents look equally good to the normal, and
        /// only a real cast can tell that one of them walks straight into the
        /// adjoining wall.
        /// </summary>
        private float ChooseSide(Transform body, Vector3 origin, Vector3 goal, Vector3 left)
        {
            bool leftOpen = !Cast(origin, left);
            bool rightOpen = !Cast(origin, -left);
            if (leftOpen && !rightOpen)
            {
                return 1.0f;
            }
            if (rightOpen && !leftOpen)
            {
                return -1.0f;
            }

            // A tie -- a flat wall face-on, or an alcove. Both tangents are
            // perpendicular to the goal so neither is "toward" it; broken on
            // the body's own heading instead, so something already moving keeps
            // its momentum rather than reversing on the spot.
            Vector3 heading = body.forward;
            heading.y = 0.0f;
            if (heading.sqrMagnitude > 0.01f)
            {
                return Vector3.Dot(heading.normalized, left) >= 0.0f ? 1.0f : -1.0f;
            }
            return 1.0f;
        }

        /// <summary>
        /// Whether anything solid sits ahead.
        ///
        /// THREE CASTS, NOT ONE. A single centre ray lets an NPC clip a corner
        /// with its shoulder and snag -- the ray passes the edge cleanly while
        /// the body does not. The two offset casts are what give it its real
        /// width.
        /// </summary>
        private bool Blocked(Vector3 origin, Vector3 goal)
        {
            if (Cast(origin, goal))
            {
                return true;
            }
            Vector3 shoulder = Vector3.Cross(Vector3.up, goal).normalized * BodyRadius;
            return Cast(origin + shoulder, goal) || Cast(origin - shoulder, goal);
        }

        private bool Cast(Vector3 from, Vector3 direction)
        {
            // Triggers ignored: the indoor volumes and the door reach are both
            // triggers covering whole buildings, and an NPC that treated them
            // as walls would refuse to walk down a street at all.
            return Physics.Raycast(from, direction, LookAhead, ObstacleMask,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Clears the committed side. Called when an NPC changes goal outright,
        /// so a commitment made for the old one does not steer the first
        /// moments of the new one.
        /// </summary>
        public void Reset()
        {
            _commitSide = 0.0f;
            _commitRemaining = 0.0f;
        }
    }
}
