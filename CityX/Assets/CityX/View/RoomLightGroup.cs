using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// One powered room's lights in a streamed chunk: the glowing faces of its fittings, its windows lit from inside (what the room looks like from
    /// the street), its lamps (spots shining down) and their bounces (spots shining up off the floor), shown while its switch is on (World.RoomLights)
    /// and hidden while it is off. Only buildings with power (World.PowerGrid) get one; a dead building's fittings are just part of the chunk's mesh,
    /// and no window anywhere is lit any other way. Built by CityStreamer from what CityMeshBuilder collected (RoomGlow).
    ///
    /// A lit room's glow is always shown; its real lights are switched on straight away, and RoomLightDirector then keeps them only for the lit rooms
    /// near enough to matter, and gives the nearest lamps shadows.
    /// </summary>
    public sealed class RoomLightGroup : MonoBehaviour
    {
        public RoomKey Key;

        private Renderer[] _glows;
        private Light[] _lamps;
        private Light[] _bounces;

        /// <summary>Every room whose switch is on, for RoomLightDirector.</summary>
        internal static readonly List<RoomLightGroup> Lit = new List<RoomLightGroup>();

        public bool IsLit { get; private set; }

        /// <summary>Whether its real lights are on: it is lit, and RoomLightDirector has not turned them off for being far away.</summary>
        public bool LightsOn { get; private set; }

        /// <summary>The middle of its lamps, in the world.</summary>
        public Vector3 Centre { get; private set; }

        public Light[] Lamps => _lamps ?? System.Array.Empty<Light>();
        public Light[] Bounces => _bounces ?? System.Array.Empty<Light>();
        public int LightCount => Lamps.Length + Bounces.Length;

        public void Bind(RoomKey key, Renderer[] glows, Light[] lamps, Light[] bounces = null)
        {
            Key = key;
            _glows = glows;
            _lamps = lamps;
            _bounces = bounces;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Light lamp in Lamps)
            {
                if (lamp != null)
                {
                    sum += lamp.transform.position;
                    n++;
                }
            }
            Centre = n > 0 ? sum / n : transform.position;
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
            Lit.Remove(this);
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
            SetLights(on);
            if (on)
            {
                if (!Lit.Contains(this))
                {
                    Lit.Add(this);
                }
                RoomLightDirector.Ensure();
            }
            else
            {
                Lit.Remove(this);
            }
        }

        /// <summary>Turns the real lights on or off (the glow stays as the switch says). RoomLightDirector's.</summary>
        internal void SetLights(bool on)
        {
            on &= IsLit;
            LightsOn = on;
            foreach (Light light in Lamps)
            {
                if (light != null)
                {
                    light.enabled = on;
                    if (!on)
                    {
                        light.shadows = LightShadows.None;
                    }
                }
            }
            foreach (Light light in Bounces)
            {
                if (light != null)
                {
                    light.enabled = on;
                }
            }
        }
    }
}
