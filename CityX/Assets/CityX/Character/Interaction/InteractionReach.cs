namespace CityX.Character
{
    /// <summary>
    /// When something you can use with E (a book, a bench, a radio, a story NPC) FORGETS the player who walked up to it.
    ///
    /// Each one notices the player through a trigger sphere of radius InteractRange, and forgets them by a distance check in Update (a
    /// collider switched off inside a trigger never fires OnTriggerExit). The trigger fires when the EDGE of the player's body first touches the
    /// sphere, and the check measured from the body's CENTRE, which is still outside the radius at that moment -- so the player was noticed and
    /// forgotten in the same frame, and, already inside the trigger, never noticed again: E did nothing until they walked away and back
    /// (found 2026-10-12: BookPlayTests failed in full runs for days). The building door already allowed a metre; now they all do.
    /// </summary>
    public static class InteractionReach
    {
        /// <summary>How much further than its InteractRange the player must be before a usable thing forgets them.</summary>
        public const float ForgetSlackMetres = 1.0f;

        public static bool IsBeyond(float distance, float interactRange) => distance > interactRange + ForgetSlackMetres;

        /// <summary>
        /// Whether a collider leaving a usable thing's trigger is the PLAYER'S BODY leaving it -- their CharacterController -- rather than one of
        /// the small colliders on their limbs (the ragdoll's, kinematic while alive). Every usable thing used to forget the player when ANY of
        /// their colliders left: an arm swinging out of the sphere in the idle animation was "the player walked away", and E did nothing while
        /// they stood right there (found 2026-10-12: the bench forgot a player 1.48 m away, well inside its 2 m reach).
        /// </summary>
        public static bool IsBodyLeaving(UnityEngine.Collider other) => other is UnityEngine.CharacterController;

        /// <summary>
        /// The body of THIS machine's player when <paramref name="other"/> is one of its colliders (a limb's or its own), else null. Only that body
        /// can pick something up here: an NPC has no input reader, and another player's copy has its reader switched off. Every pickup used to take
        /// whichever body walked in last (found 2026-10-17), so a swordsman passing through a sword's sphere left E doing nothing until the player
        /// stepped out and back in, and another player's copy could be handed the item.
        /// </summary>
        public static UnityEngine.Transform LocalPlayerBody(UnityEngine.Collider other)
        {
            PlayerVitals vitals = other != null ? other.GetComponentInParent<PlayerVitals>() : null;
            if (vitals == null)
            {
                return null;
            }
            var reader = vitals.GetComponent<PlayerInputReader>();
            return reader != null && reader.enabled ? vitals.transform : null;
        }

        /// <summary>
        /// Whether <paramref name="other"/> leaving a pickup's sphere is <paramref name="nearby"/>, the player it noticed, walking away: their body
        /// itself, not a limb swinging out (IsBodyLeaving), and not some other body passing through.
        /// </summary>
        public static bool IsLeaving(UnityEngine.Collider other, UnityEngine.Transform nearby) =>
            nearby != null && IsBodyLeaving(other) && other.transform == nearby;

        /// <summary>Where a player looks from: the yaw source the input reader turns with (the camera's), else the main camera, else the body.</summary>
        public static UnityEngine.Transform LookSource(UnityEngine.Transform player)
        {
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null && input.YawSource != null)
            {
                return input.YawSource;
            }
            return UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform : player;
        }

        /// <summary>
        /// What every pickup asks: <paramref name="point"/> is within <paramref name="range"/> of the player's feet and inside the cone they look
        /// along (<paramref name="lookDot"/>, the cosine of its half-angle). The look's dot is given back, so the most directly looked at can win.
        /// </summary>
        public static bool CanReach(UnityEngine.Vector3 point, UnityEngine.Transform player, float range, float lookDot, out float dot)
        {
            dot = 1.0f;
            if ((point - player.position).sqrMagnitude > range * range)
            {
                return false;
            }
            UnityEngine.Transform look = LookSource(player);
            UnityEngine.Vector3 toPoint = point - look.position;
            if (toPoint.sqrMagnitude > 1e-4f)
            {
                dot = UnityEngine.Vector3.Dot(look.forward, toPoint.normalized);
            }
            return dot >= lookDot;
        }
    }
}
