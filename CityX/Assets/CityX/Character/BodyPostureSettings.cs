using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The posture every body stands in, laid over whatever animation is playing (owner, 2026-10-13: "the head is always angled downward
    /// for every character and the legs are too forward on the torso they should be in the middle ... id like to adjust that").
    ///
    /// HOW TO ADJUST IT. Select Assets/CityX/Character/Resources/BodyPosture.asset and drag the sliders -- while the game is running too: every
    /// body reads it every frame, and a change made in Play Mode stays when Play stops (it is an asset). Or in the game's console:
    /// /posture head 12, /posture legs 0.06, /posture show. Every character shares it -- the player, other players' copies, civilians.
    /// </summary>
    [CreateAssetMenu(menuName = "CityX/Body Posture")]
    public sealed class BodyPostureSettings : ScriptableObject
    {
        public const string ResourceName = "BodyPosture";
        public const string AssetPath = "Assets/CityX/Character/Resources/BodyPosture.asset";

        [Range(-30.0f, 30.0f)]
        [Tooltip("Degrees the head is tipped UP from where the animation puts it (negative tips it further down).")]
        public float HeadPitchUp = 30.0f;

        [Range(-0.3f, 0.3f)]
        [Tooltip("Metres the head (and its hair) is moved BACK from where the animation puts it (negative: forward).")]
        public float HeadBack = 0.0f;

        [Range(-0.3f, 0.3f)]
        [Tooltip("Metres the head (and its hair) is moved UP (negative: down, into the torso).")]
        public float HeadUp = 0.0f;

        [Range(-45.0f, 45.0f)]
        [Tooltip("Degrees the head is turned to its RIGHT (negative: to its left).")]
        public float HeadTurnRight = 0.0f;

        [Range(-30.0f, 30.0f)]
        [Tooltip("Degrees the head is tilted toward its right shoulder (negative: toward its left).")]
        public float HeadTiltRight = 0.0f;

        [Range(-0.15f, 0.15f)]
        [Tooltip("Metres the legs sit BACK under the torso (negative moves them forward). The torso and head stay where they are.")]
        public float LegsBack = 0.05f;

        private static BodyPostureSettings _current;

        /// <summary>The asset, or a default one when there is none (a fresh project, a test).</summary>
        public static BodyPostureSettings Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Resources.Load<BodyPostureSettings>(ResourceName);
                    if (_current == null)
                    {
                        _current = CreateInstance<BodyPostureSettings>();
                    }
                }
                return _current;
            }
            set => _current = value;
        }
    }
}
