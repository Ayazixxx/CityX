using CityX.View;
using CityX.World;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The waypoint in the world: a tall beam of soft purple light standing on the ground where the pin is, visible over the rooftops from far
    /// away (open-world games put the map's marker in the world too, so the player can navigate by looking up instead of at the map). It fades
    /// as the player closes in, so it does not blind them on arrival, and goes when the waypoint does.
    /// </summary>
    public sealed class WaypointBeacon : MonoBehaviour
    {
        public const float HeightMetres = 160.0f;
        public const float RadiusMetres = 0.9f;
        public const float FullAlpha = 0.45f;

        /// <summary>From this far away the beam is at full strength; it fades out to nothing at <see cref="FadeNearMetres"/>.</summary>
        public const float FadeFarMetres = 60.0f;

        public const float FadeNearMetres = 12.0f;

        public MapWaypoint Waypoint;

        private GameObject _beam;
        private Material _material;

        public bool Showing => _beam != null && _beam.activeSelf;

        public Vector3 BeamBase => _beam != null ? _beam.transform.position - Vector3.up * (HeightMetres * 0.5f) : Vector3.zero;

        private void LateUpdate()
        {
            if (Waypoint == null || !Waypoint.Has)
            {
                if (_beam != null)
                {
                    _beam.SetActive(false);
                }
                return;
            }
            GameObject beam = Beam();
            if (beam == null)
            {
                return;
            }
            float ground = 0.0f;
            CityWorld world = CurrentWorld.Instance;
            if (world != null)
            {
                ground = (float)world.Plan.GroundHeight(Waypoint.Point.x, Waypoint.Point.y);
            }
            beam.transform.position = new Vector3(Waypoint.Point.x, ground + HeightMetres * 0.5f, Waypoint.Point.y);
            float near = Mathf.InverseLerp(FadeNearMetres, FadeFarMetres, Waypoint.StraightMetres);
            _material.color = new Color(MapPainter.WaypointColour.r, MapPainter.WaypointColour.g, MapPainter.WaypointColour.b, FullAlpha * near);
            beam.SetActive(true);
        }

        private GameObject Beam()
        {
            if (_beam != null)
            {
                return _beam;
            }
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }
            _beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _beam.name = "WaypointBeacon";
            Collider solid = _beam.GetComponent<Collider>();
            if (solid != null)
            {
                Destroy(solid);   // light, not a pillar to walk into
            }
            _beam.transform.localScale = new Vector3(RadiusMetres * 2.0f, HeightMetres * 0.5f, RadiusMetres * 2.0f);
            _material = new Material(shader) { name = "WaypointBeacon", hideFlags = HideFlags.DontSave };
            var renderer = _beam.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return _beam;
        }

        private void OnDestroy()
        {
            if (_beam != null)
            {
                Destroy(_beam);
            }
        }
    }
}
