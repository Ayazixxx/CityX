using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The lamp over a powered building's door (World.EntranceLighting): a glowing box under the canopy and a warm spot shining down on the steps
    /// and the pavement, lit from dusk to dawn (DayNightCycle.Daylight). Marked as outside (CityLightLayers): it lights the street and the people
    /// on it, not the lobby behind the wall. Built by CityStreamer with the building's door.
    /// </summary>
    public sealed class EntranceLamp : MonoBehaviour
    {
        private Light _light;
        private Renderer _glow;

        public bool IsLit { get; private set; }
        public Light Light => _light;

        public void Bind(Light light, Renderer glow)
        {
            _light = light;
            _glow = glow;
            Apply(EntranceLighting.IsLitAt(DayNightCycle.Daylight));
        }

        private void Update()
        {
            bool lit = EntranceLighting.IsLitAt(DayNightCycle.Daylight);
            if (lit != IsLit)
            {
                Apply(lit);
            }
        }

        private void Apply(bool lit)
        {
            IsLit = lit;
            if (_light != null)
            {
                _light.enabled = lit;
            }
            if (_glow != null)
            {
                _glow.enabled = lit;
            }
        }

        /// <summary>The lamp of one building, in the world (under <paramref name="parent"/>). Null if it has none.</summary>
        public static EntranceLamp Build(BuildingSite site, Transform parent, Material glowMaterial, float glowBrightness)
        {
            if (!EntranceLighting.Has(site))
            {
                return null;
            }
            EntranceLighting.Place(site, out Vector3 at, out Vector3 shining, out float intensity, out float range);

            var go = new GameObject("EntranceLamp");
            go.transform.SetParent(parent, true);
            go.transform.position = at;

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(go.transform, false);
            lightObject.transform.rotation = Quaternion.LookRotation(shining, new Vector3((float)-site.AxisZ, 0.0f, (float)site.AxisX));
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = EntranceLighting.Colour;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = EntranceLighting.OuterAngle;
            light.innerSpotAngle = EntranceLighting.InnerAngle;
            light.shadows = LightShadows.None;
            CityLightLayers.MarkOutside(light);

            Renderer glow = null;
            if (glowMaterial != null)
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(box.GetComponent<Collider>());
                box.name = "Glow";
                box.transform.SetParent(go.transform, false);
                box.transform.localPosition = Vector3.up * 0.09f;   // its top against the canopy (0.12 m over the door head)
                box.transform.rotation = Quaternion.Euler(0.0f, CityMeshBuilder.YawDegreesOf(site), 0.0f);
                box.transform.localScale = EntranceLighting.LampSize;
                glow = box.GetComponent<Renderer>();
                glow.sharedMaterial = glowMaterial;
                glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColorId, EntranceLighting.Colour * glowBrightness);
                glow.SetPropertyBlock(block);
            }

            EntranceLamp lamp = go.AddComponent<EntranceLamp>();
            lamp.Bind(light, glow);
            return lamp;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    }
}
