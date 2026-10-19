using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The local placement of every transform under a root, taken at one moment and put back later: a body shown again exactly as it stood
    /// then (the HUD portrait retakes its picture in the pose of its first one), and the pose it had in between put back after.
    /// </summary>
    public sealed class PoseSnapshot
    {
        private readonly Transform[] _parts;
        private readonly Vector3[] _positions;
        private readonly Quaternion[] _rotations;

        private PoseSnapshot(Transform root)
        {
            _parts = root.GetComponentsInChildren<Transform>(true);
            _positions = new Vector3[_parts.Length];
            _rotations = new Quaternion[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
            {
                _positions[i] = _parts[i].localPosition;
                _rotations[i] = _parts[i].localRotation;
            }
        }

        /// <summary>The pose under <paramref name="root"/> now.</summary>
        public static PoseSnapshot Of(Transform root) => new PoseSnapshot(root);

        /// <summary>Puts every part that still exists back where it was; parts added since are left alone.</summary>
        public void Apply()
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] != null)
                {
                    _parts[i].SetLocalPositionAndRotation(_positions[i], _rotations[i]);
                }
            }
        }
    }
}
