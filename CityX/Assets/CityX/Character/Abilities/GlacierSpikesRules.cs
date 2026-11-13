using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Glacier Spikes' numbers and timeline (docs/GLACIER_SPIKES.md), with no scene. A fracture front races from the caster along the aimed
    /// line; crystal spikes tear out of the ground behind it in a band that widens and grows taller toward the far end, a cluster of them is
    /// thrown up round the end, and after a while the whole field sinks back into the ground. Whoever the front passes in the band is struck
    /// once and chilled.
    ///
    /// The look's numbers are the "Frost Lance" of achrefelouafi's LinearAbiltyCastingThreeJS (MIT), its lengths made <see cref="Scale"/>
    /// times longer for a body 2.6 m tall; its times are kept. The mana, cooldown, damage and chill are mine.
    /// </summary>
    public static class GlacierSpikesRules
    {
        /// <summary>The reference is drawn round a 1.8 m figure; ours is 2.6 m (BodyRig.StandingHeight), so its lengths are about this much longer.</summary>
        public const float Scale = 1.4f;

        // --- what it costs and does (mine) ---

        /// <summary>The Arcane Orb's price: an area spell, above the Fireball's 10.</summary>
        public const float ManaCost = 20.0f;

        public const float CooldownSeconds = 6.0f;

        /// <summary>The Fireball's damage, to every one the front passes, before Spell Power.</summary>
        public const float Damage = 25.0f;

        /// <summary>Half a full chill (FrostRules): a body struck walks at three quarters of its pace for a moment; a second blow of frost freezes it.</summary>
        public const float ChillAmount = 0.5f;

        public const float HitstunSeconds = 0.5f;

        /// <summary>How far past the band's edge a body's middle may stand and still be struck, metres: about a body's half-width.</summary>
        public const float BodyMargin = 0.5f;

        /// <summary>How far below and above the ground under it a body's feet may be and still be struck, metres.</summary>
        public const float ReachBelow = 1.5f;

        public const float ReachAbove = 3.5f;

        /// <summary>The raised-hand cast pose: held this long at the cast, then eased out over <see cref="PoseEaseSeconds"/>.</summary>
        public const float PoseHoldSeconds = 0.3f;

        public const float PoseEaseSeconds = 0.4f;

        // --- the line (mine) ---

        /// <summary>The line starts this far ahead of the feet, metres, so no spike grows inside the caster.</summary>
        public const float StartAhead = 0.9f;

        /// <summary>The reference's 15 m: always cast to its full length, cut short by a wall in the way.</summary>
        public const float Range = 15.0f * Scale;

        /// <summary>Shorter than this (a wall at the caster's nose) and there is nothing to cast.</summary>
        public const float MinLength = 1.0f;

        /// <summary>How far apart the ground is looked at along the line, metres, and how much it may rise or fall from one look to the next.</summary>
        public const float GroundStep = 0.5f;

        public const float StepUp = 0.9f;

        public const float StepDown = 2.5f;

        /// <summary>How far below the feet the line's start may find its ground, metres; higher in the air than this there is nothing to cast on.</summary>
        public const float GroundBelowFeet = 4.0f;

        // --- the reference's look, scaled ---

        public const float FrontSpeed = 26.0f * Scale;

        /// <summary>The front eases up to speed over this long (outQuad).</summary>
        public const float FrontEaseSeconds = 0.08f;

        public const int SpikeCount = 190;

        public const float ImpactShare = 0.22f;

        public const float ImpactRadiusShare = 1.25f;

        public const float WidthNear = 0.55f * Scale;
        public const float Width = 2.5f * Scale;
        public const float WidthCurve = 0.75f;
        public const float Clumping = 1.35f;
        public const float Scatter = 0.55f;
        public const float FrontBias = 0.85f;

        public const float HeightNear = 0.5f * Scale;
        public const float Height = 3.1f * Scale;
        public const float HeightCurve = 1.7f;
        public const float HeightJitter = 0.55f;
        public const float Crown = 0.55f;
        public const float Peak = 1.45f;
        public const float PeakWidth = 0.28f;
        public const float Rubble = 0.42f;
        public const float RubbleScale = 0.3f;

        /// <summary>A crystal's width (its mesh is one unit across), metres.</summary>
        public const float Radius = 0.41f * Scale;
        public const float RadiusJitter = 0.93f;

        public const int Facets = 7;
        public const float Taper = 0.69f;
        public const float Roughness = 0.09f;
        public const float Bend = 0.66f;
        public const int Variants = 3;

        /// <summary>The lean away from the caster and outward, radians, and how much it varies.</summary>
        public const float Lean = 0.42f;
        public const float LeanJitter = 1.5f;

        public const float RiseSeconds = 0.17f;
        public const float RiseOvershoot = 0.26f;
        public const float RiseStagger = 0.09f;
        public const float SettleSeconds = 0.55f;

        /// <summary>How fast the overshoot wobbles, radians a second.</summary>
        public const float WobbleRate = 14.0f;

        /// <summary>How far down a rising crystal starts, as a share of its height.</summary>
        public const float RiseDepth = 0.85f;

        /// <summary>A rising crystal grows from this share of its size.</summary>
        public const float RiseScale = 0.86f;

        public const float LifetimeSeconds = 3.6f;
        public const float ShatterDelaySeconds = 0.6f;
        public const float SinkSeconds = 1.0f;

        /// <summary>How long a crystal glows from within after it breaks the ground, seconds.</summary>
        public const float BirthGlowSeconds = 0.45f;

        /// <summary>From the cast, when the field starts to sink, and when it is gone.</summary>
        public const float SinkStartSeconds = LifetimeSeconds + ShatterDelaySeconds;

        public const float EndSeconds = SinkStartSeconds + SinkSeconds;

        public static float ImpactRadius => Width * ImpactRadiusShare;

        /// <summary>The band's half-width at <paramref name="along"/> (0 at the caster, 1 at the far end), metres.</summary>
        public static float HalfWidth(float along) => Mathf.Lerp(WidthNear, Width, Mathf.Pow(Mathf.Clamp01(along), WidthCurve));

        /// <summary>How far the fracture front has run <paramref name="age"/> seconds after the cast, metres (it eases up to speed: outQuad).</summary>
        public static float FrontDistance(float age)
        {
            if (age <= 0.0f)
            {
                return 0.0f;
            }
            const float t = FrontEaseSeconds;
            if (age >= t)
            {
                return FrontSpeed * (age - t / 3.0f);
            }
            return FrontSpeed * (age * age / t - age * age * age / (3.0f * t * t));
        }

        /// <summary>When the front reaches <paramref name="distance"/> metres down the line, seconds after the cast.</summary>
        public static float ArrivalTime(float distance)
        {
            if (distance <= 0.0f)
            {
                return 0.0f;
            }
            const float t = FrontEaseSeconds;
            if (distance >= FrontDistance(t))
            {
                return distance / FrontSpeed + t / 3.0f;
            }
            float low = 0.0f;
            float high = t;
            for (int i = 0; i < 24; i++)
            {
                float middle = (low + high) * 0.5f;
                if (FrontDistance(middle) < distance)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }
            return (low + high) * 0.5f;
        }

        /// <summary>
        /// How far out of the ground a crystal is, <paramref name="sinceErupted"/> seconds after it began to break out: below 0 not yet,
        /// rising to 1 (outQuint) and then springing past it and back, settling.
        /// </summary>
        public static float Emergence(float sinceErupted)
        {
            if (sinceErupted < 0.0f)
            {
                return -1.0f;
            }
            if (sinceErupted < RiseSeconds)
            {
                float u = 1.0f - sinceErupted / RiseSeconds;
                return 1.0f - u * u * u * u * u;
            }
            float after = sinceErupted - RiseSeconds;
            return 1.0f + RiseOvershoot * Mathf.Sin(after * WobbleRate) * Mathf.Exp(-after / SettleSeconds);
        }

        /// <summary>How far the field has sunk, 0 to 1 (inCubic), <paramref name="age"/> seconds after the cast.</summary>
        public static float Sunk(float age)
        {
            float t = Mathf.Clamp01((age - SinkStartSeconds) / SinkSeconds);
            return t * t * t;
        }

        /// <summary>
        /// Whether a body whose feet are at <paramref name="feet"/> is struck by a front that has run <paramref name="front"/> metres down a
        /// line from <paramref name="start"/> along <paramref name="direction"/> (flat, unit) of <paramref name="length"/>: in the band behind the
        /// front, or round the far end once the front has reached it; and on the ground there, <paramref name="groundY"/> (within reach).
        /// </summary>
        public static bool Strikes(Vector3 start, Vector3 direction, float length, float front, Vector3 feet, float groundY)
        {
            float up = feet.y - groundY;
            if (up < -ReachBelow || up > ReachAbove || length <= 0.0f)
            {
                return false;
            }
            Vector3 flat = feet - start;
            flat.y = 0.0f;
            float along = Vector3.Dot(flat, direction);
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            float across = Vector3.Dot(flat, side);
            if (along >= -BodyMargin && along <= Mathf.Min(front, length) && Mathf.Abs(across) <= HalfWidth(along / length) + BodyMargin)
            {
                return true;
            }
            if (front < length)
            {
                return false;
            }
            Vector3 fromEnd = flat - direction * length;
            return fromEnd.magnitude <= ImpactRadius + BodyMargin;
        }
    }
}
