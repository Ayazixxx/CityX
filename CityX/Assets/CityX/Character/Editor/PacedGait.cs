using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// The Godot walks re-paced for this body's speeds (owner, 2026-10-19: foot sliding, "make those improvements", "the style doesnt really need to
    /// be preserved just make it look good"). The walks move their planted feet back at 0.7-1.4 m/s under a body that walks at 4, too far apart for
    /// the foot locks to hold (FootPlanting.LocksViable: within 0.7 to 1.4 of the body's speed), so they glided. Each walk here is the Godot clip with
    /// LONGER STEPS and a QUICKER CYCLE so its feet keep up and the locks hold them: the leg muscles' swing is widened round its own middle (the arms
    /// and the mean pose stay the clip's) and the keys are brought closer together. ClipCadence then paces them within its usual limits.
    /// If these look off, the next try is Kimodo-generated walks at these speeds.
    /// </summary>
    public static class PacedGait
    {
        /// <summary>How one walk is re-paced.</summary>
        public readonly struct Gait
        {
            /// <summary>The thighs' swing forward (below the curve's middle: Unity's Upper Leg Front-Back is negative forward) and back.</summary>
            public readonly float Forward, Back;

            /// <summary>The thighs' swing out to the side (a strafe's step).</summary>
            public readonly float Side;

            /// <summary>The knees' bend: a longer step lifts the foot higher to clear the ground.</summary>
            public readonly float Knee;

            /// <summary>How much quicker the cycle is.</summary>
            public readonly float Quicker;

            public Gait(float forward, float back, float side, float knee, float quicker)
            {
                Forward = forward;
                Back = back;
                Side = side;
                Knee = knee;
                Quicker = quicker;
            }
        }

        /// <summary>Forward: a longer reach forward than back (a walking leg reaches further ahead than behind; even widening kicked it out behind).</summary>
        public static readonly Gait Walk = new Gait(1.5f, 1.15f, 1.0f, 1.4f, 1.35f);

        /// <summary>Backward: the step back is the reach here, both ways alike, and the quickest cycle (the clip's own pace is the slowest, 0.7 m/s).</summary>
        public static readonly Gait Backward = new Gait(1.6f, 1.6f, 1.0f, 1.3f, 1.6f);

        /// <summary>Sideways: a little more spread (1.5 was the splits) and a quicker cycle carries the rest.</summary>
        public static readonly Gait Strafe = new Gait(1.0f, 1.0f, 1.2f, 1.2f, 1.45f);

        /// <summary><paramref name="source"/> re-paced by <paramref name="gait"/>, saved as <c>godot_{name}_paced.anim</c> beside the Godot clips.</summary>
        public static AnimationClip Build(AnimationClip source, string name, Gait gait)
        {
            if (source == null)
            {
                return null;
            }
            AnimationClip paced = Object.Instantiate(source);
            paced.ClearCurves();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                Keyframe[] keys = curve.keys;
                float mean = 0.0f;
                foreach (Keyframe key in keys)
                {
                    mean += key.value;
                }
                mean /= Mathf.Max(1, keys.Length);
                string muscle = binding.propertyName;
                for (int i = 0; i < keys.Length; i++)
                {
                    float widen = Widening(muscle, keys[i].value - mean, gait);
                    keys[i].time /= gait.Quicker;
                    keys[i].value = Mathf.Clamp(mean + (keys[i].value - mean) * widen, -1.0f, 1.0f);
                    keys[i].inTangent *= widen * gait.Quicker;
                    keys[i].outTangent *= widen * gait.Quicker;
                }
                curve.keys = keys;
                AnimationUtility.SetEditorCurve(paced, binding, curve);
            }
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.stopTime /= gait.Quicker;
            AnimationUtility.SetAnimationClipSettings(paced, settings);
            return ClipAsset.Store(paced, $"{GodotMotionBaker.OutputDirectory}/godot_{name}_paced.anim");
        }

        private static float Widening(string muscle, float fromMiddle, in Gait gait)
        {
            if (muscle.EndsWith("Upper Leg Front-Back"))
            {
                return fromMiddle < 0.0f ? gait.Forward : gait.Back;
            }
            if (muscle.EndsWith("Upper Leg In-Out"))
            {
                return gait.Side;
            }
            if (muscle.EndsWith("Lower Leg Stretch"))
            {
                return gait.Knee;
            }
            return 1.0f;
        }
    }
}
