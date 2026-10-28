using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE RHYTHM OF A PAIR OF WINGS (docs/WINGS.md; owner, 2026-10-28, of the Lego games' fliers: "the rhythm at which the wings flap always
    /// seems to line up with how you are moving"). One looping stroke, scrubbed by a PHASE that advances at a rate set by what the body is
    /// doing, and a STRENGTH that sets how deep the beat is (DreamWorks' "cycle multiplexing": phase and strength, not a blend of clips):
    /// <list type="bullet">
    /// <item>hovering, slow deep beats; climbing, quick ones; descending, few and shallow (the wings tuck);</item>
    /// <item>flying forward, the faster the body goes the fewer the beats until it GLIDES, wings swept back, with a beat every few seconds;
    /// picking up speed adds beats (the wings work for it), so the beat follows the stick;</item>
    /// <item>a take-off starts a downstroke at once, strong; a climb begun mid-upstroke finishes the upstroke quickly so its first downstroke
    /// comes with the key. The downstroke is the quicker half (<see cref="DownstrokeShare"/>), as a real one is;</item>
    /// <item>on the ground the wings fold and only breathe.</item>
    /// </list>
    /// Pure: a frame's state in, the stroke out. The gear bobs the body with it (the lift of each downstroke), which is what makes the rhythm
    /// and the movement one thing.
    /// </summary>
    public sealed class WingBeat
    {
        /// <summary>Beats a second, by what the body is doing.</summary>
        public const float HoverHz = 1.1f;

        public const float ClimbHz = 1.8f;

        public const float GlideHz = 0.22f;

        public const float DescendHz = 0.6f;

        /// <summary>Folded on the back, the slow breath.</summary>
        public const float FoldedHz = 0.25f;

        /// <summary>Extra beats a second for each metre a second squared the body gains going forward.</summary>
        public const float AccelerationHzPerMetre = 0.12f;

        /// <summary>A climb begun mid-upstroke finishes the upstroke this many times quicker.</summary>
        public const float CatchUpRate = 3.0f;

        /// <summary>The stroke's amplitude at full strength, degrees either side of the wing's middle.</summary>
        public const float StrokeDegrees = 38.0f;

        /// <summary>The share of a cycle the downstroke takes: the quicker, working half.</summary>
        public const float DownstrokeShare = 0.45f;

        /// <summary>How deep the beat is, by what the body is doing (a take-off's first beat deeper than any).</summary>
        public const float HoverStrength = 0.8f;

        public const float ClimbStrength = 1.0f;

        public const float GlideStrength = 0.3f;

        public const float DescendStrength = 0.35f;

        public const float FoldedStrength = 0.06f;

        public const float TakeoffStrength = 1.25f;

        /// <summary>How quickly the strength follows its target, a second.</summary>
        public const float StrengthRate = 4.0f;

        /// <summary>Seconds for the wings to open, to fold, to sweep into a glide and to tuck.</summary>
        public const float OpenSeconds = 0.25f;

        public const float FoldSeconds = 0.6f;

        public const float GlideSeconds = 0.4f;

        public const float TuckSeconds = 0.3f;

        /// <summary>The glide begins at this share of the cruising speed and is complete at <see cref="FullGlideSpeedFraction"/>.</summary>
        public const float GlideStartSpeedFraction = 0.35f;

        public const float FullGlideSpeedFraction = 1.0f;

        private bool _catchingUp;
        private float _lastAngle;
        private bool _hasLastAngle;

        /// <summary>Where in the stroke: 0 the top (a downstroke begins), <see cref="DownstrokeShare"/> the bottom, 1 the top again.</summary>
        public float Phase { get; private set; }

        /// <summary>How deep the beat is now, 0..1 and a little over at a take-off.</summary>
        public float Strength { get; private set; }

        /// <summary>0 open .. 1 folded on the back.</summary>
        public float Fold { get; private set; } = 1.0f;

        /// <summary>0 .. 1 swept back into a glide.</summary>
        public float Glide { get; private set; }

        /// <summary>0 .. 1 tucked for a descent.</summary>
        public float Tuck { get; private set; }

        /// <summary>This tick began a downstroke (the phase passed the top).</summary>
        public bool DownstrokeBegan { get; private set; }

        /// <summary>Beats a second this tick.</summary>
        public float Rate { get; private set; }

        /// <summary>The stroke's angular velocity, degrees a second (negative through the downstroke): what the feathers trail against.</summary>
        public float StrokeVelocity { get; private set; }

        /// <summary>The stroke's angle now, degrees, positive up: see <see cref="StrokeAngle"/>.</summary>
        public float Angle => StrokeAngle(0.0f);

        /// <summary>How much lift this instant gives, 0..1: the downstroke's, none on the way up.</summary>
        public float LiftPulse => LiftAt(Phase);

        public void Tick(in WingBeatInput input, float deltaTime)
        {
            deltaTime = Mathf.Max(0.0f, deltaTime);
            DownstrokeBegan = false;
            float glideShare = Mathf.Clamp01((input.SpeedFraction - GlideStartSpeedFraction) / (FullGlideSpeedFraction - GlideStartSpeedFraction));

            float strengthTarget;
            if (!input.Flying)
            {
                Rate = FoldedHz;
                strengthTarget = FoldedStrength;
                _catchingUp = false;
            }
            else
            {
                if (input.TookOff)
                {
                    Phase = 0.0f;
                    _catchingUp = false;
                    DownstrokeBegan = true;
                }
                else if (input.ClimbBegan && Phase > DownstrokeShare)
                {
                    _catchingUp = true;
                }
                if (input.Climbing)
                {
                    Rate = ClimbHz;
                    strengthTarget = ClimbStrength;
                }
                else if (input.Descending)
                {
                    Rate = DescendHz;
                    strengthTarget = DescendStrength;
                }
                else
                {
                    Rate = Mathf.Lerp(HoverHz, GlideHz, glideShare);
                    strengthTarget = Mathf.Lerp(HoverStrength, GlideStrength, glideShare);
                }
                Rate += AccelerationHzPerMetre * Mathf.Max(0.0f, input.ForwardAcceleration);
                if (_catchingUp)
                {
                    Rate *= CatchUpRate;
                }
            }

            Phase += Rate * deltaTime;
            if (Phase >= 1.0f)
            {
                Phase -= Mathf.Floor(Phase);
                DownstrokeBegan = true;
                _catchingUp = false;
            }
            Strength = input.Flying && input.TookOff ? TakeoffStrength : Mathf.MoveTowards(Strength, strengthTarget, StrengthRate * deltaTime);

            Fold = Mathf.MoveTowards(Fold, input.Flying ? 0.0f : 1.0f, deltaTime / (input.Flying ? OpenSeconds : FoldSeconds));
            Glide = Mathf.MoveTowards(Glide, input.Flying && !input.Climbing && !input.Descending ? glideShare : 0.0f, deltaTime / GlideSeconds);
            Tuck = Mathf.MoveTowards(Tuck, input.Flying && input.Descending ? 1.0f : 0.0f, deltaTime / TuckSeconds);

            float angle = Angle;
            StrokeVelocity = _hasLastAngle && deltaTime > 1e-5f ? (angle - _lastAngle) / deltaTime : 0.0f;
            if (input.TookOff)
            {
                StrokeVelocity = 0.0f;
            }
            _lastAngle = angle;
            _hasLastAngle = true;
        }

        /// <summary>The stroke's angle <paramref name="phaseOffset"/> of a cycle behind now, degrees, positive up (the elbow and the wrist lag the shoulder).</summary>
        public float StrokeAngle(float phaseOffset)
        {
            return StrokeDegrees * Strength * Stroke(Phase - phaseOffset);
        }

        /// <summary>The looping stroke: +1 at the top (phase 0), -1 at the bottom (<see cref="DownstrokeShare"/>), +1 again at 1; the downstroke the quicker half.</summary>
        public static float Stroke(float phase)
        {
            float p = phase - Mathf.Floor(phase);
            float u = p < DownstrokeShare ? 0.5f * p / DownstrokeShare : 0.5f + 0.5f * (p - DownstrokeShare) / (1.0f - DownstrokeShare);
            return Mathf.Cos(2.0f * Mathf.PI * u);
        }

        /// <summary>The lift through the cycle, 0..1: a hump over the downstroke, nothing on the upstroke.</summary>
        public static float LiftAt(float phase)
        {
            float p = phase - Mathf.Floor(phase);
            return p < DownstrokeShare ? Mathf.Sin(Mathf.PI * p / DownstrokeShare) : 0.0f;
        }
    }

    /// <summary>One frame of what the wings are asked to do.</summary>
    public struct WingBeatInput
    {
        public bool Flying;

        public bool Climbing;

        public bool Descending;

        /// <summary>The horizontal speed as a share of the cruising speed (over 1 boosting).</summary>
        public float SpeedFraction;

        /// <summary>How fast the horizontal speed is growing, metres a second each second (negative shedding it).</summary>
        public float ForwardAcceleration;

        /// <summary>This frame began the flight, or a climb.</summary>
        public bool TookOff;

        public bool ClimbBegan;
    }
}
