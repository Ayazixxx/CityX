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
    }
}
