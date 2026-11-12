using UnityEngine;

namespace CityX.Items
{
    /// <summary>
    /// A BIG WEAPON CARRIED ON THE BACK (owner, 2026-11-12, of the Giant Boomerang: "I want to add cool ways to hold larger weapons there are going to be large
    /// greatswords, guns, etc. find out a good way to carry the large boomerang, maybe on the back?"). Slung across the back at a slant, riding the spine so
    /// it leans and turns with the trunk, and taken into the hand by the weapon's own draw. A place on the back is given from the spine bone, in the body's own
    /// axes at rest (x right, y up, z forward; the live skeleton rests with every bone unturned), so the numbers read as the body does.
    /// </summary>
    public readonly struct BackCarry
    {
        /// <summary>Where the weapon's grip rides, from the spine bone, metres.</summary>
        public readonly Vector3 Grip;

        /// <summary>Which way the weapon's length runs from its grip (its local +z), and which way its flat face looks (its local +y), in the body's axes.</summary>
        public readonly Vector3 Along, Face;

        public BackCarry(Vector3 grip, Vector3 along, Vector3 face)
        {
            Grip = grip;
            Along = along;
            Face = face;
        }

        public Quaternion Turn => Quaternion.LookRotation(Along, Face);

        /// <summary>Hangs <paramref name="item"/> on <paramref name="body"/>'s back here.</summary>
        public void Hang(Transform item, GameObject body)
        {
            CosmeticMount.ToBone(item, body, HumanBodyBones.Spine, Grip, Turn.eulerAngles, 1.0f);
        }
    }
}
