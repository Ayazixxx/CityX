using System.Collections.Generic;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// Red embers rising off a drawing, as the Bloodrage aura's rise off a body (BloodrageAuraBuilder: soft dots coming up off the whole shape,
    /// orange to red to dark as they go, swelling a little then dying away). Pure: where they are, how big and what colour, step by step; a
    /// graphic draws them (<see cref="EmberGraphic"/>). Positions are in the drawing's own units, y up.
    /// </summary>
    public sealed class Embers
    {
        public struct Ember
        {
            public Vector2 Position;
            public float Rise;
            public float Sway;
            public float SwayPhase;
            public float Age;
            public float Life;
            public float Size;
        }

        /// <summary>Embers a second, at full strength.</summary>
        public const float Rate = 55.0f;

        /// <summary>How long one lives, seconds, at least and at most (the Bloodrage wisps' second, give or take).</summary>
        public const float LifeMin = 0.7f;

        public const float LifeMax = 1.25f;

        /// <summary>How fast one rises, and how far it sways side to side, as shares of the drawing's height (a second, and either way).</summary>
        public const float RiseMin = 0.45f;

        public const float RiseMax = 1.1f;
        public const float SwayShare = 0.08f;

        /// <summary>How big one is, as a share of the drawing's height.</summary>
        public const float SizeMin = 0.12f;

        public const float SizeMax = 0.3f;

        private readonly List<Ember> _alive = new List<Ember>();
        private readonly System.Random _random;
        private float _owed;

        public Embers(int seed)
        {
            _random = new System.Random(seed);
        }

        public IReadOnlyList<Ember> Alive => _alive;

        /// <summary>
        /// <paramref name="seconds"/> on: the living rise, sway and age, the old die, and new ones come off <paramref name="sources"/> (points of
        /// the drawing) at <paramref name="strength"/> (0 to 1) of the full rate. <paramref name="height"/> is the drawing's.
        /// </summary>
        public void Step(float seconds, float strength, IReadOnlyList<Vector2> sources, float height)
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                Ember ember = _alive[i];
                ember.Age += seconds;
                if (ember.Age >= ember.Life)
                {
                    _alive.RemoveAt(i);
                    continue;
                }
                ember.Position.y += ember.Rise * seconds;
                _alive[i] = ember;
            }
            if (strength <= 0.0f || sources == null || sources.Count == 0)
            {
                _owed = 0.0f;
                return;
            }
            _owed += Rate * strength * seconds;
            while (_owed >= 1.0f)
            {
                _owed -= 1.0f;
                _alive.Add(new Ember
                {
                    Position = sources[_random.Next(sources.Count)],
                    Rise = Range(RiseMin, RiseMax) * height,
                    Sway = Range(-SwayShare, SwayShare) * height,
                    SwayPhase = Range(0.0f, Mathf.PI * 2.0f),
                    Life = Range(LifeMin, LifeMax),
                    Size = Range(SizeMin, SizeMax) * height,
                });
            }
        }

        /// <summary>Where an ember is drawn: where it has risen to, swaying.</summary>
        public static Vector2 DrawnAt(Ember ember)
        {
            return ember.Position + new Vector2(ember.Sway * Mathf.Sin(ember.SwayPhase + ember.Age * 5.0f), 0.0f);
        }

        /// <summary>How big it is through its life (0 to 1): swelling a little, then dying away (the Bloodrage wisps' curve).</summary>
        public static float SizeAt(float life)
        {
            return life < 0.3f ? Mathf.Lerp(0.8f, 1.0f, life / 0.3f) : Mathf.Lerp(1.0f, 0.1f, (life - 0.3f) / 0.7f);
        }

        /// <summary>Its colour through its life: orange to red to dark, coming in quickly and fading out (the Bloodrage wisps' gradient).</summary>
        public static Color ColourAt(float life)
        {
            Color hot = new Color(1.0f, 0.5f, 0.3f), red = new Color(0.9f, 0.05f, 0.02f), dark = new Color(0.4f, 0.0f, 0.0f);
            Color colour = life < 0.5f ? Color.Lerp(hot, red, life / 0.5f) : Color.Lerp(red, dark, (life - 0.5f) / 0.5f);
            colour.a = life < 0.15f ? life / 0.15f : Mathf.Lerp(1.0f, 0.0f, (life - 0.15f) / 0.85f);
            return colour;
        }

        private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
    }
}
