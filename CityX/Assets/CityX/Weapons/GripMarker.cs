using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A small marker on a held weapon showing where a hand reaches for it. It is an ordinary child
    /// object: while the game runs, select it in the Hierarchy (the weapon's Inspector has a button
    /// for it) and move it with the Scene view gizmos, and the hand follows. It draws itself in the
    /// Scene view (and in the Game view with Gizmos switched on) so you can see what you are moving.
    /// </summary>
    public sealed class GripMarker : MonoBehaviour
    {
        /// <summary>What to call it in the Scene view.</summary>
        public string Label = "hand";

        public Color Colour = new Color(1.0f, 0.55f, 0.1f);

        /// <summary>The size it is drawn at, in metres of world space (not the weapon's own, possibly scaled, space).</summary>
        public const float DrawRadiusMetres = 0.06f;

        /// <summary>Makes a marker as a child of <paramref name="weapon"/> (at its origin: the caller places it).</summary>
        public static Transform Create(Transform weapon, string objectName, string label, Color colour)
        {
            var marker = new GameObject(objectName);
            marker.transform.SetParent(weapon, false);
            var component = marker.AddComponent<GripMarker>();
            component.Label = label;
            component.Colour = colour;
            return marker.transform;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Colour;
            Gizmos.DrawWireSphere(transform.position, DrawRadiusMetres);
            Gizmos.DrawSphere(transform.position, DrawRadiusMetres * 0.25f);
#if UNITY_EDITOR
            UnityEditor.Handles.color = Colour;
            UnityEditor.Handles.Label(transform.position + Vector3.up * (DrawRadiusMetres * 1.6f), Label);
#endif
        }
    }
}
