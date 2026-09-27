using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// A TRIGGER volume that bullets and the gun's aim trace must still see. Shots ignore triggers (pickup
    /// spheres, rain volumes, room volumes), but a cracked window has no solid collider by design -- you
    /// walk through it -- and so the only thing standing where the glass is, for a bullet, is the trigger box
    /// its opening leaves. That box is one of these.
    /// </summary>
    public interface IShootableTrigger
    {
    }

    /// <summary>
    /// Marks loose debris that bullets, the gun's aim trace and wall-grab probes must look straight through:
    /// broken-glass shards today.
    ///
    /// WHY. A shattered window's shards are launched outward from the wall -- straight back toward whoever
    /// broke it -- as real rigid bodies with colliders, and they live six seconds. The pistol's aim trace runs
    /// every frame along the crosshair ray, so every shard that drifted across it was "what the crosshair is
    /// on": the aim point snapped to a fragment two metres away and back to the wall behind it, and the arm and
    /// the bullet tracer followed it every time. That was the reported "the pistol points in other directions
    /// after I shoot windows" -- and a bullet stopping on a falling shard is wrong on its own.
    /// </summary>
    public sealed class IgnoredByShots : MonoBehaviour
    {
        /// <summary>True when the collider belongs to something shots and aim traces should pass through.</summary>
        public static bool Covers(Collider collider)
        {
            return collider != null && collider.GetComponentInParent<IgnoredByShots>() != null;
        }
    }
}
