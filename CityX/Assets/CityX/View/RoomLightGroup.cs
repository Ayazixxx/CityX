using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// One powered room's lights in a streamed chunk: the glowing faces of its fittings, its windows lit from inside (what the room looks like from
    /// the street) and its few real (point) lights, shown while its switch is on (World.RoomLights) and hidden while it is off. Only buildings with
    /// power (World.PowerGrid) get one; a dead building's fittings are just part of the chunk's mesh, and no window anywhere is lit any other way.
    /// Built by CityStreamer from what CityMeshBuilder collected (RoomGlow).
    /// </summary>
    public sealed class RoomLightGroup : MonoBehaviour
    {
        public RoomKey Key;

        private Renderer[] _glows;
        private Light[] _lights;

        public bool IsLit { get; private set; }

        public void Bind(RoomKey key, Renderer[] glows, Light[] lights)
        {
            Key = key;
            _glows = glows;
            _lights = lights;
            Apply(RoomLights.IsOn(key));
        }

        private void OnEnable()
        {
            RoomLights.Changed += OnChanged;
            Apply(RoomLights.IsOn(Key));
        }

        private void OnDisable()
        {
            RoomLights.Changed -= OnChanged;
        }

        private void OnChanged(RoomKey key, bool on)
        {
            if (key.Equals(Key))
            {
                Apply(on);
            }
        }

        private void Apply(bool on)
        {
            IsLit = on;
            if (_glows != null)
            {
                foreach (Renderer glow in _glows)
                {
                    if (glow != null)
                    {
                        glow.enabled = on;
                    }
                }
            }
            if (_lights != null)
            {
                foreach (Light light in _lights)
                {
                    if (light != null)
                    {
                        light.enabled = on;
                    }
                }
            }
        }
    }
}
