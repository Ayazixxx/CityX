using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>The bolt lying on the crossbow's rail (docs/ARCHERY.md): shown while one is loaded, gone while the crossbow is empty or being spanned.</summary>
    public sealed class CrossbowBolt : MonoBehaviour
    {
        public PistolWeapon Weapon;

        private void LateUpdate()
        {
            if (Weapon == null)
            {
                return;
            }
            bool loaded = Weapon.MagazineAmmo > 0 && !Weapon.IsReloading;
            foreach (Transform child in transform)
            {
                if (child.gameObject.activeSelf != loaded)
                {
                    child.gameObject.SetActive(loaded);
                }
            }
        }
    }
}
