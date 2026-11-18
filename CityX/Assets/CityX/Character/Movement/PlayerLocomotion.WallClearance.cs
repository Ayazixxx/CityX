using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The body stops where its arms meet a wall, not where its capsule does (Ragdoll.WallContacts; owner, 2026-11-10: "make it so body parts
    /// cant clip through walls and other objects ... stop at contact with the wall"). Once posed each frame the body is moved, through its
    /// capsule so it goes into nothing else, as far as takes its arms and head out of whatever they reached into, and kept that far off the
    /// wall while it walks along it (WallStandoff). Not seated: a seat's sides are meant to be against the body.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>The most the body is moved out of walls in one step, metres on a body of the player's size: a nudge, never a jump.</summary>
        public const float MostOutOfWallsPerStep = 0.25f;

        /// <summary>Two walls pushing this nearly opposite ways are a squeeze narrower than the arms: the body is not pushed at all there.</summary>
        private const float SqueezeDot = -0.5f;

        private Ragdoll _wallRagdoll;
        private bool _wallRagdollLooked;
        private readonly WallStandoff _standoff = new WallStandoff();
        private readonly Vector3[] _wallContacts = new Vector3[16];
        private readonly Vector3[] _wallNeeds = new Vector3[16];

        /// <summary>The frame this body last took an ordinary step of its own (Tick, to its end): a copy moved by the network never does.</summary>
        private int _steppedFrame = -1;

        /// <summary>
        /// The pose after the Animator (PoseAfterTheAnimator), and then the body out of walls by that pose, the one that is drawn. Taken out by
        /// the pose before the Animator had moved the arms on, the walk swung them back in before the frame was drawn (measured: 5.6 cm).
        /// </summary>
        private void LateUpdate()
        {
            PoseAfterTheAnimator();
            if (_steppedFrame == Time.frameCount)
            {
                KeepTheBodyOutOfWalls();
                KeepTheHandsOutOfWalls();
            }
            else
            {
                _standoff.Clear();   // climbing, on a ladder, limp, moved by the network: whatever walls it was kept off are someone else's business
                _aheadStandoff.Clear();
            }
            KeepTheBladeOutOfWalls();
        }

        /// <summary>A wall whose way out is this nearly straight ahead or behind is one the body is going into or out of, not one beside it.</summary>
        private const float HeadOnDot = 0.8f;

        /// <summary>
        /// Only the walls beside the body (the arms hang at its sides): one ahead or behind is the capsule's, as it was. Stopped at an arm's
        /// touch head-on too, the body could no longer run into a window opening narrower than its arms (1.16 m against 1.25 m): the arm met
        /// the wall beside the opening and held it outside. Keeps the first <paramref name="count"/> contacts that are beside it, in place, each
        /// turned to push only sideways.
        /// </summary>
        private int BesideTheBody(int count)
        {
            Vector3 ahead = transform.forward;
            ahead.y = 0.0f;
            ahead.Normalize();
            int kept = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 way = _wallContacts[i];
                float along = Vector3.Dot(way.normalized, ahead);
                if (Mathf.Abs(along) > HeadOnDot)
                {
                    continue;
                }
                // Only its sideways part, as far as that has to go: an arm on the corner of an opening was pushed back out of it.
                Vector3 side = way - ahead * Vector3.Dot(way, ahead);
                _wallContacts[kept++] = side.normalized * (way.magnitude / Mathf.Sqrt(1.0f - along * along));
            }
            return kept;
        }

        private void KeepTheBodyOutOfWalls()
        {
            if (IsSitting || InCarSeat || _controller == null || !_controller.enabled)
            {
                _standoff.Clear();
                _aheadStandoff.Clear();
                return;
            }
            if (!_wallRagdollLooked)
            {
                _wallRagdoll = GetComponent<Ragdoll>();
                _wallRagdollLooked = true;
            }
            if (_wallRagdoll == null)
            {
                return;
            }
            int contacts = BesideTheBody(_wallRagdoll.WallContacts(_wallContacts));
            int ahead = 0;
            Vector3 forward = transform.forward;
            forward.y = 0.0f;
            forward.Normalize();
            int hands = HandsIntoWalls(_handContacts);
            for (int i = 0; i < hands; i++)
            {
                if (Mathf.Abs(Vector3.Dot(_handContacts[i].normalized, forward)) > HeadOnDot)
                {
                    _aheadContacts[ahead++] = _handContacts[i];
                }
                else if (contacts < _wallContacts.Length)
                {
                    _wallContacts[contacts++] = _handContacts[i];
                }
            }

            _standoff.ForgetAheadAndBehind(transform.forward, HeadOnDot);
            _wallStillBeside ??= WallStillBeside;
            if (_standoff.Count > 0)
            {
                _standoff.ForgetUnless(_wallStillBeside);
            }
            int needs = _standoff.Needs(transform.position, _wallContacts, contacts, _lastTickDeltaTime, BodyScale, _wallNeeds);

            _aheadStandoff.ForgetBeside(transform.forward, HeadOnDot);
            if (_aheadStandoff.Count > 0)
            {
                _aheadStandoff.ForgetUnless(_wallStillBeside);
            }
            int aheadNeeds = _aheadStandoff.Needs(transform.position, _aheadContacts, ahead, _lastTickDeltaTime, BodyScale, _aheadNeeds);
            for (int i = 0; i < aheadNeeds && needs < _wallNeeds.Length; i++)
            {
                _wallNeeds[needs++] = _aheadNeeds[i];
            }
            Vector3 push = Vector3.ClampMagnitude(WallClearance.Combined(_wallNeeds, needs, SqueezeDot), MostOutOfWallsPerStep * BodyScale);
            if (push.sqrMagnitude > 1e-8f)
            {
                Vector3 before = transform.position;
                _controller.Move(push);
                // Not the body's own going: the lean (PlayerLocomotion.Lean) tilts into the body's measured acceleration, and a push it counted
                // threw the shoulders the other way and the arms further into the wall, a step at a time (measured: 19 cm off between walls
                // that should not have moved it at all).
                _leanLastPosition += transform.position - before;
            }
        }

        /// <summary>
        /// Where the arms and the head can reach out to one side, as a share of the body's height: from the hands hanging down to over the head,
        /// and out from the body's middle (its middle's distance, and the reach either side of it).
        /// </summary>
        private const float ReachLow = 0.4f, ReachHigh = 0.85f, ReachOut = 0.22f, ReachAround = 0.13f;

        private System.Predicate<Vector3> _wallStillBeside;
        private static readonly Collider[] BesideHits = new Collider[16];

        /// <summary>Whether anything of the world is still within the arms' reach on the side a wall's way out (<paramref name="wayOut"/>) points from.</summary>
        private bool WallStillBeside(Vector3 wayOut)
        {
            float height = WorldHeight;
            Vector3 side = transform.position - wayOut * (ReachOut * height);
            int hits = Physics.OverlapCapsuleNonAlloc(side + Vector3.up * (ReachLow * height), side + Vector3.up * (ReachHigh * height),
                ReachAround * height, BesideHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                Collider c = BesideHits[i];
                if (c.attachedRigidbody == null && !(c is CharacterController) && !c.transform.IsChildOf(transform))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
