using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// One of Octaslash's eight cuts, drawn: a wide crescent of white light swept round the body, each in a different plane turned about the
    /// dash (so the eight read as a flurry, not one arc eight times), the last one bigger and level. It flashes in and fades in a fifth of a
    /// second. Drawn with the sword spin's own streak material.
    /// </summary>
    public sealed class OctaslashSlash : MonoBehaviour
    {
        public const float LifeSeconds = 0.2f;
        public const float Radius = 2.6f;
        public const float FinalRadius = 3.4f;
        public const float ArcDegrees = 210.0f;
        public const int Points = 24;

        private LineRenderer _line;
        private float _age;
        private Color _colour;

        public static OctaslashSlash Spawn(Material material, Vector3 centre, Vector3 dashDirection, int index, bool final)
        {
            var go = new GameObject("OctaslashSlash");
            var slash = go.AddComponent<OctaslashSlash>();
            slash.Draw(material, centre, dashDirection, index, final);
            return slash;
        }

        private void Draw(Material material, Vector3 centre, Vector3 dashDirection, int index, bool final)
        {
            Vector3 axis = dashDirection.sqrMagnitude > 1e-6f ? dashDirection.normalized : Vector3.forward;
            // Each cut's plane contains the dash and is turned a golden angle on from the last: no two neighbours alike. The final one is level.
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            Vector3 across = final ? side : Quaternion.AngleAxis(index * 137.5f, axis) * side;
            Vector3 normal = Vector3.Cross(axis, across).normalized;
            float radius = final ? FinalRadius : Radius;

            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = Points;
            _line.sharedMaterial = material;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.widthCurve = new AnimationCurve(new Keyframe(0.0f, 0.02f), new Keyframe(0.55f, final ? 0.45f : 0.3f), new Keyframe(1.0f, 0.02f));
            _line.numCapVertices = 2;
            float start = -ArcDegrees * 0.5f + (index % 2 == 0 ? 0.0f : 180.0f);
            for (int i = 0; i < Points; i++)
            {
                float angle = start + ArcDegrees * i / (Points - 1);
                Vector3 offset = Quaternion.AngleAxis(angle, normal) * across * radius;
                _line.SetPosition(i, centre + offset);
            }
            _colour = final ? new Color(1.0f, 0.95f, 0.85f, 1.0f) : new Color(0.9f, 0.95f, 1.0f, 0.9f);
            Shade();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= LifeSeconds)
            {
                Destroy(gameObject);
                return;
            }
            Shade();
        }

        private void Shade()
        {
            float alpha = _colour.a * (1.0f - _age / LifeSeconds);
            var c = new Color(_colour.r, _colour.g, _colour.b, alpha);
            _line.startColor = c;
            _line.endColor = c;
        }
    }
}
