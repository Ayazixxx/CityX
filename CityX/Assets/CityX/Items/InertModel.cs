using UnityEngine;

namespace CityX.Items
{
    /// <summary>
    /// An item's model with nothing of the item left acting: no pickup, no collision, no physics, no scripts, no sound. What another player's
    /// copy holds and what a body lets go of when it dies are both made from the item's real prefab this way.
    /// </summary>
    public static class InertModel
    {
        public static void Strip(GameObject model)
        {
            // Switched off, not destroyed: an item's script may require its collider (SwordWeapon its pickup sphere), and Unity refuses, with
            // an error, to destroy a collider a script requires.
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }
            foreach (Rigidbody rb in model.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
            }
            foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                b.enabled = false;
            }
            foreach (AudioSource a in model.GetComponentsInChildren<AudioSource>(true))
            {
                a.Stop();
                a.enabled = false;
            }
        }
    }
}
