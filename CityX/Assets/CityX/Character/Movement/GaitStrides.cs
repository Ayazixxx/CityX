using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How much longer than the clip's own stride the feet are placed at each speed the animator plays (stride warping): written when the animator is
    /// built (ClipCadence, Editor), read every frame by the foot placement (PlayerLocomotion.FootIK). A clip is only sped up by so much (within 20%,
    /// the industry's limit before the motion degrades); what that does not cover, the stride takes (also within 20%), and the foot locks absorb
    /// the rest. Keyed by the animator state (its blend tree) and the blend position the sample sits at, in the body's own metres per second.
    /// </summary>
    [CreateAssetMenu(menuName = "CityX/Gait Strides")]
    public sealed class GaitStrides : ScriptableObject
    {
        public const string ResourceName = "GaitStrides";
        public const string AssetPath = "Assets/CityX/Character/Resources/GaitStrides.asset";

        [System.Serializable]
        public struct Entry
        {
            public string Tree;
            public Vector2 Move;
            public float Stride;
        }

        public List<Entry> Entries = new List<Entry>();

        private static GaitStrides _current;

        /// <summary>The asset, or an empty one (every stride 1) when there is none.</summary>
        public static GaitStrides Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Resources.Load<GaitStrides>(ResourceName);
                    if (_current == null)
                    {
                        _current = CreateInstance<GaitStrides>();
                    }
                }
                return _current;
            }
            set => _current = value;
        }

        public void Add(string tree, Vector2 move, float stride)
        {
            Entries.Add(new Entry { Tree = tree, Move = move, Stride = stride });
        }

        /// <summary>The stride at the sample nearest <paramref name="move"/> (the body's velocity in its own frame) in <paramref name="tree"/>; 1 when nothing is near.</summary>
        public float StrideFor(string tree, Vector2 move)
        {
            float best = float.MaxValue;
            float stride = 1.0f;
            foreach (Entry entry in Entries)
            {
                if (entry.Tree != tree)
                {
                    continue;
                }
                float distance = Vector2.Distance(entry.Move, move);
                if (distance < best)
                {
                    best = distance;
                    stride = entry.Stride;
                }
            }
            return best <= 3.0f ? stride : 1.0f;
        }
    }
}
