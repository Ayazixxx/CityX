using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What flew off a body (a cut-off part, a chunk of gore): lies <see cref="LifeSeconds"/>, then shrinks away over <see cref="ShrinkSeconds"/>,
    /// taking with it the meshes made for it (<see cref="Own"/>). At most <see cref="MaxLive"/> lie about at once, every body's together: a new
    /// piece sends the oldest away early (Source caps its gibs the same way).
    /// </summary>
    public sealed class Debris : MonoBehaviour
    {
        public const float LifeSeconds = 60.0f;
        public const float ShrinkSeconds = 3.0f;
        public const int MaxLive = 240;

        private static readonly List<Debris> Live = new List<Debris>();

        private readonly List<Mesh> _owned = new List<Mesh>();
        private float _age;
        private Vector3 _scale;

        /// <summary>How many pieces lie about and are not yet on their way out.</summary>
        public static int LiveCount => Live.Count;

        private void Awake()
        {
            Live.Add(this);
            if (Live.Count > MaxLive)
            {
                Debris oldest = Live[0];
                Live.RemoveAt(0);
                if (oldest != null)
                {
                    oldest.Expire();
                }
            }
        }

        /// <summary>Starts shrinking away now.</summary>
        public void Expire()
        {
            _age = Mathf.Max(_age, LifeSeconds);
            Live.Remove(this);
        }

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
            Live.Remove(this);
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
