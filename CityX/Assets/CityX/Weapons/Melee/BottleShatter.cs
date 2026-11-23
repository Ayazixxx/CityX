using CityX.World;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A BOTTLE COMING APART (docs/GLASS_BOTTLE.md; owner, 2026-11-23: "improve ... the glass break"). The pieces are the bottle's own
    /// (BottleMesh.Pieces: its neck, its foot, its walls in shards, in its own glass), put where the bottle was as it broke, so it is seen to
    /// burst rather than vanish behind a puff. Each is a real body: thrown out from the bottle's middle and on the way the bottle was going,
    /// spinning, bouncing and coming to rest, then shrinking away. A spray of bright glints goes with them (the prefab's particles).
    ///
    /// A bottle broken in the hand keeps its half: only the pieces past the break fly (<see cref="Spawn"/>'s from and to), the rest are not made.
    /// </summary>
    public sealed class BottleShatter : MonoBehaviour
    {
        /// <summary>How long the pieces lie about before they go, and how long they take to shrink away at the end, seconds.</summary>
        public const float LifetimeSeconds = 5.0f;
        public const float ShrinkSeconds = 0.7f;

        /// <summary>How fast a piece is thrown out from the bottle's middle, m/s, and off the surface it broke on.</summary>
        public const float BurstSpeedMin = 1.2f;
        public const float BurstSpeedMax = 3.6f;
        public const float OffSurfaceSpeedMax = 1.8f;

        /// <summary>How much of the bottle's own speed along the surface its pieces keep, and how much of its speed into the surface comes back out.</summary>
        public const float SlideKept = 0.45f;
        public const float BounceKept = 0.18f;

        /// <summary>How fast a piece tumbles, degrees a second.</summary>
        public const float SpinMin = 200.0f;
        public const float SpinMax = 900.0f;

        [Tooltip("The pieces, each a body of its own under this root (BottleBuilder).")]
        public Rigidbody[] Pieces = new Rigidbody[0];

        [Tooltip("How far along the bottle from the fist each piece's middle is, metres: a bottle broken in the hand throws only those past the break.")]
        public float[] AlongBottle = new float[0];

        private float _age;

        /// <summary>How many pieces are flying.</summary>
        public int Flying { get; private set; }

        /// <summary>
        /// The bottle at <paramref name="position"/> and <paramref name="rotation"/> (its held frame: the fist on the neck at the origin, the
        /// base out along +Y) bursts: going at <paramref name="velocity"/> into a surface facing <paramref name="surfaceNormal"/>. Only the
        /// pieces between <paramref name="from"/> and <paramref name="to"/> metres along it from the fist fly: all of it by default.
        /// </summary>
        public static BottleShatter Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 surfaceNormal,
            float from = float.NegativeInfinity, float to = float.PositiveInfinity)
        {
            if (prefab == null)
            {
                return null;
            }
            GameObject instance = Instantiate(prefab, position, rotation);
            var shatter = instance.GetComponent<BottleShatter>();
            if (shatter != null)
            {
                shatter.Launch(velocity, surfaceNormal.sqrMagnitude > 1e-6f ? surfaceNormal.normalized : Vector3.up, from, to);
            }
            return shatter;
        }

        private void Launch(Vector3 velocity, Vector3 normal, float from, float to)
        {
            // Solid to the world, but not to bullets or a gun's aim (as a window's shards are).
            if (GetComponent<IgnoredByShots>() == null)
            {
                gameObject.AddComponent<IgnoredByShots>();
            }
            Vector3 middle = Vector3.zero;
            int counted = 0;
            for (int i = 0; i < Pieces.Length; i++)
            {
                if (Pieces[i] != null && Flies(i, from, to))
                {
                    middle += Pieces[i].position;
                    counted++;
                }
            }
            middle = counted > 0 ? middle / counted : transform.position;
            float into = Vector3.Dot(velocity, normal);
            Vector3 carried = (velocity - normal * into) * SlideKept + normal * (Mathf.Max(0.0f, -into) * BounceKept);
            for (int i = 0; i < Pieces.Length; i++)
            {
                Rigidbody piece = Pieces[i];
                if (piece == null)
                {
                    continue;
                }
                if (!Flies(i, from, to))
                {
                    Destroy(piece.gameObject);
                    Pieces[i] = null;
                    continue;
                }
                Vector3 outward = piece.position - middle;
                outward = outward.sqrMagnitude > 1e-8f ? outward.normalized : Random.onUnitSphere;
                piece.linearVelocity = carried + outward * Random.Range(BurstSpeedMin, BurstSpeedMax) + normal * Random.Range(0.0f, OffSurfaceSpeedMax);
                piece.maxAngularVelocity = SpinMax * Mathf.Deg2Rad;
                piece.angularVelocity = Random.onUnitSphere * (Random.Range(SpinMin, SpinMax) * Mathf.Deg2Rad);
                Flying++;
            }
        }

        private bool Flies(int piece, float from, float to) =>
            piece >= AlongBottle.Length || (AlongBottle[piece] >= from && AlongBottle[piece] <= to);

        private void Update()
        {
            _age += Time.deltaTime;
            float left = LifetimeSeconds - _age;
            if (left <= 0.0f)
            {
                Destroy(gameObject);
                return;
            }
            if (left < ShrinkSeconds)
            {
                Vector3 size = Vector3.one * Mathf.SmoothStep(0.0f, 1.0f, left / ShrinkSeconds);
                foreach (Rigidbody piece in Pieces)
                {
                    if (piece != null)
                    {
                        piece.transform.localScale = size;
                    }
                }
            }
        }
    }
}
