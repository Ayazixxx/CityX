using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A can that pours (the jerry can, in Weapons): what FireSync shows pouring on a copy of another player, without the Character assembly
    /// depending on Weapons -- the same layering reason IEquippableFlamethrower exists.
    /// </summary>
    public interface IPouredCan
    {
        /// <summary>Shows the stream to where the owner's gasoline is landing, for a moment.</summary>
        void ShowPouring(Vector3 landing);
    }
}
