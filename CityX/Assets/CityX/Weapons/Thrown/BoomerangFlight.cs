using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The path of a thrown boomerang, with no scene (docs/BOOMERANG.md). As Terraria's: no gravity; straight out for its out time, or until it meets
    /// something, which turns it straight back; then it steers for wherever the thrower now is, slowing, stopping and coming back up to its top speed,
    /// until it reaches them. Its numbers are a <see cref="BoomerangProfile"/>: the wooden one's unless given.
    /// </summary>
    public sealed class BoomerangFlight
    {
        private readonly BoomerangProfile _profile;
        private float _age;

        public BoomerangFlight(Vector3 from, Vector3 way, float speed) : this(from, way, speed, BoomerangProfile.Wooden)
        {
        }

        public BoomerangFlight(Vector3 from, Vector3 way, float speed, BoomerangProfile profile)
        {
            _profile = profile;
            Position = from;
            Velocity = way.normalized * speed;
        }

        public Vector3 Position { get; private set; }

        public Vector3 Velocity { get; private set; }

        /// <summary>It has turned for home: from here on it goes through walls and hits everyone it passes.</summary>
        public bool Returning { get; private set; }

        public float Age => _age;

        public void Step(float deltaTime, Vector3 home)
        {
            _age += deltaTime;
            if (!Returning && _age >= _profile.OutSeconds)
            {
                Returning = true;
            }
            if (Returning)
            {
                Vector3 toHome = home - Position;
                Vector3 wanted = toHome.sqrMagnitude > 1e-6f ? toHome.normalized * _profile.BackTopSpeed : Velocity;
                Velocity = Vector3.MoveTowards(Velocity, wanted, _profile.TurnAcceleration * deltaTime);
            }
            Position += Velocity * deltaTime;
        }

        /// <summary>It met something on its way out: it comes straight back from there.</summary>
        public void TurnBack()
        {
            if (!Returning)
            {
                Returning = true;
                Velocity = -Velocity;
            }
        }

        public bool Arrived(Vector3 home) => Returning && (home - Position).sqrMagnitude <= _profile.CatchMetres * _profile.CatchMetres;

        public bool Lost(Vector3 home) => _age >= BoomerangRules.LifeSeconds || (home - Position).sqrMagnitude >= BoomerangRules.LostMetres * BoomerangRules.LostMetres;
    }
}
