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
        private readonly string[] _paths;
        private readonly Vector3[] _positions;
        private readonly Quaternion[] _rotations;

        private PoseSnapshot(Transform root)
        {
            _parts = root.GetComponentsInChildren<Transform>(true);
            _paths = new string[_parts.Length];
            _positions = new Vector3[_parts.Length];
            _rotations = new Quaternion[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
            {
                _paths[i] = PathUnder(root, _parts[i]);
                _positions[i] = _parts[i].localPosition;
                _rotations[i] = _parts[i].localRotation;
            }
        }

        private static string PathUnder(Transform root, Transform part)
        {
            string path = "";
            for (Transform t = part; t != null && t != root; t = t.parent)
            {
                path = path.Length == 0 ? t.name : t.name + "/" + path;
            }
            return path;
        }

        /// <summary>
        /// The same pose put on another body of the same rig (a respawned or networked body), part by part by name under
        /// <paramref name="root"/>; parts it lacks are skipped.
        /// </summary>
        public void ApplyTo(Transform root)
        {
            for (int i = 0; i < _paths.Length; i++)
            {
                Transform part = _paths[i].Length == 0 ? root : root.Find(_paths[i]);
                if (part != null)
                {
                    part.SetLocalPositionAndRotation(_positions[i], _rotations[i]);
                }
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
