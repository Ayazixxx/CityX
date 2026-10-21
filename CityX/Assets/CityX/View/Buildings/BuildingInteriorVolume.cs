using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// "Am I standing inside this building." A trigger box over the whole
    /// building, ported from the Area3D city_chunk.gd hangs off every one.
    ///
    /// THE WHOLE FOOTPRINT AND THE WHOLE HEIGHT, not just the ground floor:
    /// somebody who took the stairs to the sixth floor is still indoors. And
    /// down through the basement when there is one, or a player underground
    /// counts as outdoors and hears the street through the concrete.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BuildingInteriorVolume : MonoBehaviour
    {
        private BoxCollider _box;

        /// <summary>The box, in world space, for a building on its own pad.</summary>
        public static Bounds BoundsFor(BuildingSite site)
        {
            float basement = site.Params.HasBasement
                ? BuildingInterior.StoreyHeight(site.Params.Height)
                : 0.0f;
            float low = site.BaseHeight - basement;
            float high = site.BaseHeight + site.Params.Height;

            var bounds = new Bounds();
            bounds.SetMinMax(
                new Vector3((float)site.MinX, low, (float)site.MinZ),
                new Vector3((float)site.MaxX, high, (float)site.MaxZ));
            return bounds;
        }

        public void Fit(in Bounds bounds)
        {
            Fit(bounds, Quaternion.identity);
        }

        /// <summary>
        /// The box, turned about its own centre: a building is turned to face its street, and <see cref="BoundsFor"/> gives its
        /// box as if it were not.
        /// </summary>
        public void Fit(in Bounds bounds, Quaternion rotation)
        {
            transform.position = bounds.center;
            transform.rotation = rotation;
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
            _box.size = bounds.size;
        }

        private void OnTriggerEnter(Collider other)
        {
            var aware = other.GetComponentInParent<IIndoorAware>();
            aware?.EnterBuilding(transform);
        }

        private void OnTriggerExit(Collider other)
        {
            var aware = other.GetComponentInParent<IIndoorAware>();
            aware?.ExitBuilding(transform);
        }
    }
}
