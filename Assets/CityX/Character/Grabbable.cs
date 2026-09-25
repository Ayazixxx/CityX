using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Marks a Rigidbody as a valid target for <see cref="GrabCarry"/>.
    /// Ported from player.gd's "grabbable" group, which RagdollBuilder adds
    /// to every ragdoll bone the same way character.gd's own
    /// _make_ragdoll_body does.
    ///
    /// NOT SUFFICIENT ON ITS OWN. In Godot a ragdoll body only exists once a
    /// character has actually died -- character.gd builds the rig at death,
    /// not at spawn. This port's Ragdoll instead builds every bone's
    /// Rigidbody up front and leaves it kinematic until Ragdoll.Activate(),
    /// so a bone carries this marker throughout a character's whole life.
    /// GrabCarry additionally requires !Rigidbody.isKinematic before
    /// treating anything as liftable, which reaches the same practical
    /// outcome Godot's construction timing does for free: nothing is
    /// grabbable until it is actually limp.
    /// </summary>
    public sealed class Grabbable : MonoBehaviour
    {
    }
}
