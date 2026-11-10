using System.Collections.Generic;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Which room lamps hold one of the few shadow maps, faded rather than switched. A lamp with a shadow map also lays its light out through
    /// the windows onto the street (CityXShineOutThroughWindows), so a lamp that simply lost its map as the player walked on would take its
    /// window-shaped patch off the pavement in one frame. A lamp that is no longer wanted keeps its map while its strength goes to nothing, and
    /// only then is the map given to the next lamp, so never more than <see cref="_slots"/> are drawn at once.
    /// </summary>
    public sealed class RoomLampShadows
    {
        /// <summary>A casting lamp's shadow strength. MUST match CITYX_ROOM_LAMP_SHADOW_STRENGTH in CityXToonLighting.hlsl.</summary>
        public const float FullStrength = 0.85f;

        /// <summary>How long a lamp takes to come up to its full strength, or to go down from it, in seconds.</summary>
        public const float FadeSeconds = 0.3f;

        private readonly int _slots;
        private readonly List<Light> _casting = new List<Light>();
        private readonly List<Light> _wanted = new List<Light>();

        public RoomLampShadows(int slots)
        {
            _slots = slots;
        }

        /// <summary>How many lamps hold a shadow map now.</summary>
        public int Casting => _casting.Count;

        /// <summary>Starts a new choice of lamps: follow with <see cref="Want"/>, nearest first.</summary>
        public void ClearWanted()
        {
            _wanted.Clear();
        }

        public void Want(Light lamp)
        {
            _wanted.Add(lamp);
        }

        /// <summary>A lamp gone back to the pool holds nothing any more.</summary>
        public void Forget(Light lamp)
        {
            _casting.Remove(lamp);
            _wanted.Remove(lamp);
        }

        /// <summary>The fades carried on by a frame's <paramref name="deltaTime"/>; a map freed by one goes to the nearest lamp waiting.</summary>
        public void Step(float deltaTime)
        {
            float change = FullStrength * deltaTime / FadeSeconds;
            for (int i = _casting.Count - 1; i >= 0; i--)
            {
                Light lamp = _casting[i];
                if (lamp == null)
                {
                    _casting.RemoveAt(i);
                    continue;
                }
                bool wanted = _wanted.Contains(lamp);
                lamp.shadowStrength = Mathf.MoveTowards(lamp.shadowStrength, wanted ? FullStrength : 0.0f, change);
                if (!wanted && lamp.shadowStrength <= 0.0f)
                {
                    lamp.shadows = LightShadows.None;
                    _casting.RemoveAt(i);
                }
            }
            for (int i = 0; i < _wanted.Count && _casting.Count < _slots; i++)
            {
                Light lamp = _wanted[i];
                if (lamp != null && !_casting.Contains(lamp))
                {
                    lamp.shadows = LightShadows.Soft;
                    lamp.shadowStrength = change;
                    _casting.Add(lamp);
                }
            }
        }

        /// <summary>The choice in force at once, with no fading (tests and captures, which look at a single frame).</summary>
        public void Snap()
        {
            foreach (Light lamp in _casting)
            {
                if (lamp != null && !_wanted.Contains(lamp))
                {
                    lamp.shadows = LightShadows.None;
                }
            }
            _casting.Clear();
            for (int i = 0; i < _wanted.Count && _casting.Count < _slots; i++)
            {
                Light lamp = _wanted[i];
                if (lamp != null)
                {
                    lamp.shadows = LightShadows.Soft;
                    lamp.shadowStrength = FullStrength;
                    _casting.Add(lamp);
                }
            }
        }
    }
}
