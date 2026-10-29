using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What flew off a body (a cut-off part, a chunk of gore): lies <see cref="LifeSeconds"/>, then shrinks away over <see cref="ShrinkSeconds"/>,
    /// taking with it the meshes made for it (<see cref="Own"/>).
    /// </summary>
    public sealed class Debris : MonoBehaviour
    {
        public const float LifeSeconds = 60.0f;
        public const float ShrinkSeconds = 3.0f;

        private readonly List<Mesh> _owned = new List<Mesh>();
        private float _age;
        private Vector3 _scale;

        /// <summary>Meshes made at runtime for this piece alone: destroyed with it.</summary>
        public void Own(IEnumerable<Mesh> meshes)
        {
            _owned.AddRange(meshes);
        }

        private void Start()
        {
            _scale = transform.localScale;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float past = _age - LifeSeconds;
            if (past <= 0.0f)
            {
                return;
            }
            if (past >= ShrinkSeconds)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = _scale * (1.0f - past / ShrinkSeconds);
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in _owned)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }
            _owned.Clear();
        }
    }
}
