using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// AN ENEMY WITH NOTHING TO FIGHT WANDERS (owner, 2026-11-14: "make sure that the mobs have patrol behaviors or in other words they
    /// wander around in a natural way rather than awkwardly stand still"). It strolls a few seconds one way, stops and stands a few seconds
    /// (often turning to look somewhere else), sets off another way; it turns before it walks, or comes round in a curve as it walks;
    /// something in its way or a drop in front of it and it turns aside and goes on; and it stays about where it began (its home), turning back when it has strayed.
    ///
    /// Pure: the body's own brain tells it where the body is and what is in front of it (<see cref="Senses"/>; NpcWanderProbe asks the
    /// physics) and does what it says (<see cref="Step"/>): face this way, walk this much. The swordsman, the Shoota and the Crimson Claw
    /// all use it; the Wraith drifts by its own rules (WraithDrift). In a session the enemy's owner runs it and every copy is moved as the
    /// owner's body moves, so every player sees the same wandering.
    /// </summary>
    public sealed class NpcWander
    {
        /// <summary>How fast it strolls, as a share of the body's walk: an amble, not a march.</summary>
        public const float StrollShare = 0.45f;

        /// <summary>
        /// A walking body coming round from something in its way (it cannot turn where it stands) shuffles round at this share of its
        /// walk until the way it is steered is clear, then strolls on.
        /// </summary>
        public const float ShuffleShare = 0.08f;

        public const float TurnDegreesPerSecond = 150.0f;

        public const float WalkSecondsMin = 2.5f, WalkSecondsMax = 6.0f;

        public const float PauseSecondsMin = 1.5f, PauseSecondsMax = 4.5f;

        /// <summary>Standing, how often it turns to look another way, and how far round.</summary>
        public const float GlanceChance = 0.6f;

        public const float GlanceDegreesMin = 35.0f, GlanceDegreesMax = 110.0f;

        /// <summary>How far ahead it looks for something in its way, metres, and how near that may be before it turns aside.</summary>
        public const float ProbeMetres = 2.5f;

        public const float StopShortMetres = 1.2f;

        /// <summary>How far aside it turns from something in its way, degrees.</summary>
        public const float TurnAsideDegreesMin = 100.0f, TurnAsideDegreesMax = 180.0f;

        /// <summary>It stays within about this of where it began, metres: further, it heads back.</summary>
        public const float HomeRadiusMetres = 9.0f;

        /// <summary>It walks once it faces within this of the way it means to go, degrees: it turns, then walks.</summary>
        public const float FacingToWalkDegrees = 35.0f;

        /// <summary>What the body's brain knows this frame.</summary>
        public struct Senses
        {
            public Vector3 Position;

            /// <summary>The way the body faces, degrees about the vertical.</summary>
            public float YawDegrees;

            /// <summary>How far ahead of the body is clear, metres (<see cref="ProbeMetres"/> or more: nothing in the way).</summary>
            public float ClearAheadMetres;

            /// <summary>There is ground to walk on a stride ahead (false at a drop: a stair's well, a roof's edge). A floater always says true.</summary>
            public bool GroundAhead;
        }

        /// <summary>What the body is to do this frame.</summary>
        public struct Step
        {
            /// <summary>Face this way, degrees about the vertical (already turned no faster than it turns).</summary>
            public float YawDegrees;

            /// <summary>Walk forward this much, as a share of the body's walk (0: stand).</summary>
            public float Move;
        }

        private readonly System.Random _random;
        private readonly bool _turnsOnTheSpot;
        private bool _begun;
        private Vector3 _home;
        private float _left;

        /// <param name="seed">Its own dice.</param>
        /// <param name="turnsOnTheSpot">
        /// True for a body that can turn where it stands (the Crimson Claw): it turns to face the way it means to go, then goes. False
        /// for the game's walking bodies, which turn only as they walk (and snap round if sent straight off another way): it sets off at
        /// once the way it faces and comes round to its heading in a curve.
        /// </param>
        public NpcWander(int seed, bool turnsOnTheSpot = true)
        {
            _random = new System.Random(seed);
            _turnsOnTheSpot = turnsOnTheSpot;
        }

        /// <summary>It is on a stroll (false: standing between two).</summary>
        public bool Walking { get; private set; }

        /// <summary>The way it means to go or look, degrees about the vertical.</summary>
        public float Heading { get; private set; }

        /// <summary>Forgets where it was: the next <see cref="Tick"/> starts from wherever the body then is (after a fight).</summary>
        public void Reset()
        {
            _begun = false;
        }

        private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        private float Side() => _random.Next(2) == 0 ? -1.0f : 1.0f;

        public Step Tick(in Senses senses, float deltaTime)
        {
            if (!_begun)
            {
                _begun = true;
                _home = senses.Position;
                Heading = senses.YawDegrees;
                Walking = false;
                _left = Range(0.3f, PauseSecondsMax);   // (not all of a crowd setting off on the same frame)
            }
            _left -= deltaTime;
            Vector3 toHome = _home - senses.Position;
            toHome.y = 0.0f;
            if (Walking)
            {
                bool facing = Mathf.Abs(Mathf.DeltaAngle(senses.YawDegrees, Heading)) <= FacingToWalkDegrees;
                if (facing && (!senses.GroundAhead || senses.ClearAheadMetres < StopShortMetres))
                {
                    // Something in its way, or a drop: it turns aside and goes on.
                    Heading = Mathf.Repeat(senses.YawDegrees + Side() * Range(TurnAsideDegreesMin, TurnAsideDegreesMax), 360.0f);
                }
                else if (toHome.magnitude > HomeRadiusMetres * 1.5f && Vector3.Dot(toHome, Forward(Heading)) < 0.0f)
                {
                    Heading = Toward(toHome);   // strayed, and still going away
                }
                else if (_left <= 0.0f)
                {
                    Walking = false;
                    _left = Range(PauseSecondsMin, PauseSecondsMax);
                    if (_random.NextDouble() < GlanceChance)
                    {
                        Heading = Mathf.Repeat(senses.YawDegrees + Side() * Range(GlanceDegreesMin, GlanceDegreesMax), 360.0f);
                    }
                }
            }
            else if (_left <= 0.0f)
            {
                Walking = true;
                _left = Range(WalkSecondsMin, WalkSecondsMax);
                Heading = toHome.magnitude > HomeRadiusMetres ? Toward(toHome) : Range(0.0f, 360.0f);
            }

            float yaw = Mathf.MoveTowardsAngle(senses.YawDegrees, Heading, TurnDegreesPerSecond * deltaTime);
            float move = 0.0f;
            if (Walking && _turnsOnTheSpot)
            {
                move = Mathf.Abs(Mathf.DeltaAngle(yaw, Heading)) <= FacingToWalkDegrees ? StrollShare : 0.0f;
            }
            else if (Walking)
            {
                // Comes round as it walks; with something in the way it is steered (or a drop), it shuffles round until that is clear.
                move = senses.GroundAhead && senses.ClearAheadMetres >= StopShortMetres ? StrollShare : ShuffleShare;
            }
            return new Step { YawDegrees = yaw, Move = move };
        }

        private static Vector3 Forward(float yawDegrees) => Quaternion.Euler(0.0f, yawDegrees, 0.0f) * Vector3.forward;

        /// <summary>The way home, give or take: it does not march back along a ruled line.</summary>
        private float Toward(Vector3 toHome) => Mathf.Repeat(Mathf.Atan2(toHome.x, toHome.z) * Mathf.Rad2Deg + Range(-30.0f, 30.0f), 360.0f);
    }

    /// <summary>What is in front of a wandering body, asked of the physics for <see cref="NpcWander"/>.</summary>
    public static class NpcWanderProbe
    {
        /// <summary>A drop deeper than this below the feet, a stride ahead, is not ground to walk on, metres.</summary>
        public const float DeepestStepDownMetres = 0.7f;

        /// <summary>How far ahead the ground is looked for, metres.</summary>
        public const float StrideMetres = 1.2f;

        /// <summary>A body the size of the player's, on its feet: its chest this high, and this much room wanted ahead of it, metres.</summary>
        public const float WalkerChestMetres = 1.3f;

        public const float WalkerRadius = 0.3f;

        /// <summary>
        /// What is in front of a humanoid on its feet, along the way it is being steered (<paramref name="steeredYawDegrees"/>). The
        /// game's bodies turn as they walk, not on the spot, so a walker's wander goes by the heading its brain has given it, which
        /// the body comes round to as it sets off.
        /// </summary>
        public static NpcWander.Senses SenseWalker(Transform body, float steeredYawDegrees) =>
            Sense(body, Quaternion.Euler(0.0f, steeredYawDegrees, 0.0f) * Vector3.forward, WalkerChestMetres, WalkerRadius, walks: true);

        /// <summary>
        /// A body whose feet are at its <paramref name="body"/>'s origin: what is ahead of its chest (<paramref name="chestMetres"/> up,
        /// a ball of <paramref name="radius"/>), and whether there is ground a stride ahead. <paramref name="walks"/> false for a floater,
        /// which needs none.
        /// </summary>
        public static NpcWander.Senses Sense(Transform body, float chestMetres, float radius, bool walks) => Sense(body, body.forward, chestMetres, radius, walks);

        /// <summary>The same, looking along <paramref name="along"/> (level) instead of the way the body faces, and saying that is the way it faces.</summary>
        public static NpcWander.Senses Sense(Transform body, Vector3 along, float chestMetres, float radius, bool walks)
        {
            Vector3 forward = along;
            forward.y = 0.0f;
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Vector3 chest = body.position + Vector3.up * chestMetres;
            bool blocked = Physics.SphereCast(chest, radius, forward, out RaycastHit ahead, NpcWander.ProbeMetres, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) && !ahead.transform.IsChildOf(body);
            bool ground = !walks || Physics.Raycast(chest + forward * StrideMetres, Vector3.down, chestMetres + DeepestStepDownMetres,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            return new NpcWander.Senses
            {
                Position = body.position,
                YawDegrees = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg,
                ClearAheadMetres = blocked ? ahead.distance : NpcWander.ProbeMetres,
                GroundAhead = ground,
            };
        }
    }
}
