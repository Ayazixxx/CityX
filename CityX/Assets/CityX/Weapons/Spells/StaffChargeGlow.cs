using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The light at a staff's head while a shot charges (docs/STAFFS.md): a glow that swells with the charge and a light that brightens, and,
    /// for a staff whose charge counts (the Arcane Staff's orbs), little orbs circling the head, one for each the shot will carry. Shown by
    /// whichever spell is charging, every frame it charges, on its owner's body and on other players' copies; it fades the moment it is not.
    /// </summary>
    public sealed class StaffChargeGlow : MonoBehaviour
    {
        public const string ObjectName = "ChargeGlow";

        /// <summary>The glow across, metres, from a fresh charge to a full one (a staff may ask for a bigger full one).</summary>
        public const float LeastGlowSize = 0.25f;

        public const float MostGlowSize = 0.8f;

        /// <summary>The circling orbs: how far out from the head, how big, how fast round (turns a second).</summary>
        public const float SatelliteRadius = 0.18f;

        public const float SatelliteSize = 0.07f;

        public const float SatelliteTurnsPerSecond = 1.6f;

        /// <summary>How quickly an unfed glow goes out, seconds.</summary>
        public const float FadeSeconds = 0.12f;

        private MeshRenderer _glow;
        private MeshRenderer _hot;
        private Transform[] _satellites = new Transform[0];
        private Light _light;
        private Color _colour;
        private float _mostSize = MostGlowSize;
        private MaterialPropertyBlock _block;

        /// <summary>Made on first use: a property block may not be made while the component is (a field initializer).</summary>
        private MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();
        private float _fraction;
        private int _lit;
        private float _strength;
        private int _shownFrame = -1;
        private float _spin;

        /// <summary>
        /// The glow on <paramref name="tip"/>, made the first time: <paramref name="glow"/> and <paramref name="hot"/> its outer and inner light in
        /// <paramref name="colour"/>, with <paramref name="satellites"/> orbs to circle it.
        /// </summary>
        public static StaffChargeGlow On(Transform tip, Material glow, Material hot, Color colour, int satellites, float mostSize = MostGlowSize)
        {
            if (tip == null)
            {
                return null;
            }
            Transform existing = tip.Find(ObjectName);
            if (existing != null)
            {
                return existing.GetComponent<StaffChargeGlow>();
            }
            var go = new GameObject(ObjectName);
            go.transform.SetParent(tip, false);
            var charge = go.AddComponent<StaffChargeGlow>();
            charge._colour = colour;
            charge._mostSize = mostSize;
            charge._glow = ArcaneVisuals.Sprite(go.transform, glow, mostSize);
            charge._hot = ArcaneVisuals.Sprite(go.transform, hot, mostSize * 0.4f, "Hot");
            charge._light = ArcaneVisuals.Light(go.transform, 0.0f, 4.0f);
            charge._light.color = colour;
            charge._satellites = new Transform[Mathf.Max(0, satellites)];
            for (int i = 0; i < charge._satellites.Length; i++)
            {
                var orb = new GameObject("Satellite").transform;
                orb.SetParent(go.transform, false);
                ArcaneVisuals.Sphere(orb, ArcaneVisuals.CoreMaterial, SatelliteSize);
                ArcaneVisuals.Sprite(orb, ArcaneVisuals.Glow, SatelliteSize * 3.0f);
                orb.gameObject.SetActive(false);
                charge._satellites[i] = orb;
            }
            charge.Apply();
            return charge;
        }

        /// <summary>The charge this frame: <paramref name="fraction"/> of a full one, <paramref name="lit"/> of the circling orbs showing.</summary>
        public void Show(float fraction, int lit)
        {
            _fraction = Mathf.Clamp01(fraction);
            _lit = Mathf.Clamp(lit, 0, _satellites.Length);
            _shownFrame = Time.frameCount;
        }

        /// <summary>How brightly it shows now, 0 out .. 1 fed this frame.</summary>
        public float Strength => _strength;

        /// <summary>The circling orbs showing now.</summary>
        public int LitSatellites => _strength > 0.0f ? _lit : 0;

        private void LateUpdate()
        {
            bool fed = _shownFrame >= Time.frameCount - 1;
            _strength = fed ? 1.0f : Mathf.MoveTowards(_strength, 0.0f, Time.deltaTime / FadeSeconds);
            _spin += Time.deltaTime * SatelliteTurnsPerSecond * 360.0f;
            Apply();
        }

        private void Apply()
        {
            float size = Mathf.Lerp(LeastGlowSize, _mostSize, _fraction);
            if (_glow != null)
            {
                _glow.transform.localScale = Vector3.one * size;
                ArcaneVisuals.Fade(_glow, _colour, _strength, Block);
            }
            if (_hot != null)
            {
                _hot.transform.localScale = Vector3.one * (size * 0.4f);
                ArcaneVisuals.Fade(_hot, Color.white, _strength, Block);
            }
            if (_light != null)
            {
                _light.intensity = _strength * Mathf.Lerp(0.3f, 1.5f, _fraction);
                _light.enabled = _strength > 0.0f;
            }
            for (int i = 0; i < _satellites.Length; i++)
            {
                bool on = _strength > 0.0f && i < _lit;
                _satellites[i].gameObject.SetActive(on);
                if (!on)
                {
                    continue;
                }
                float angle = (_spin + i * 360.0f / _satellites.Length) * Mathf.Deg2Rad;
                Vector3 round = transform.parent != null ? transform.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
                Vector3 across = Vector3.Cross(round, Vector3.right).sqrMagnitude > 1e-4f ? Vector3.Cross(round, Vector3.right).normalized : Vector3.forward;
                Vector3 other = Vector3.Cross(round, across);
                _satellites[i].localPosition = (across * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * SatelliteRadius;
            }
        }
    }
}
