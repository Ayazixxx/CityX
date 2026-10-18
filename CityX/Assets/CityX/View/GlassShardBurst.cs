using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A one-shot burst of real angular glass fragments -- explicit ask:
    /// "improve upon the window breaking mechanics. ideally it would be like
    /// garrys mod/half life where it turns to shards." Replaces the old
    /// billboard-particle sparkle: every child under this root is its own
    /// small Rigidbody+MeshCollider prism (GlassShardsBuilder/GlassShardMesh),
    /// so shards fly outward under real physics, tumble, bounce off whatever
    /// they land on, and settle -- rather than fading out as a flat sprite.
    ///
    /// A SHARED PREFAB CANNOT PRE-BAKE PER-INSTANCE RANDOMNESS, so this
    /// script's only job is giving each child a fresh, random launch the
    /// moment a burst spawns, then freeing the whole burst once it has had
    /// time to fly, bounce and settle -- matching BloodBurst's own
    /// "a temporary effect cleans up after itself" convention, just on a
    /// timer instead of watching a ParticleSystem, since these are ordinary
    /// Rigidbodies with no natural "finished" signal of their own.
    ///
    /// DIRECTIONAL, unlike the blood splatter's forward cone: a pane gives
    /// way outward along its OWN wall's normal (WindowGrid.OutwardNormal),
    /// not toward whatever hit it, so every shard's launch is biased around
    /// Spawn's own "away" argument.
    /// </summary>
    public sealed class GlassShardBurst : MonoBehaviour
    {
        [Tooltip("How long a burst survives before cleanup -- flies, bounces, settles, then gone.")]
        public float Lifetime = 6.0f;

        [Tooltip("Outward launch speed range, metres/second.")]
        public float SpeedMin = 1.5f;
        public float SpeedMax = 4.0f;

        [Tooltip("Half-angle of the cone each shard launches within, around the wall's own outward normal.")]
        public float SpreadDegrees = 55.0f;

        [Tooltip("Tumble speed range, degrees/second per axis.")]
        public float SpinMin = 90.0f;
        public float SpinMax = 540.0f;

        /// <summary>
        /// Puts one burst at a shattered pane, flying away from its own wall.
        /// Mirrors BloodBurst.Spawn's own shape and its own zero-normal
        /// guard, for the same reason: LookRotation on a zero vector is an
        /// undefined answer and a console error.
        /// </summary>
        public static GlassShardBurst Spawn(GameObject prefab, Vector3 position, Vector3 normal)
        {
            if (prefab == null)
            {
                return null;
            }

            Vector3 away = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            GameObject instance = Instantiate(prefab, position, Quaternion.LookRotation(away));
            return instance.GetComponent<GlassShardBurst>();
        }

        private void Awake()
        {
            // Solid to the world, but not to bullets or the gun's aim: see IgnoredByShots.
            if (GetComponent<IgnoredByShots>() == null)
            {
                gameObject.AddComponent<IgnoredByShots>();
            }

            Vector3 away = transform.forward;
            foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>())
            {
                body.linearVelocity = RandomDirectionInCone(away, SpreadDegrees) * Random.Range(SpeedMin, SpeedMax);
                body.angularVelocity = new Vector3(
                    RandomSignedRange(SpinMin, SpinMax),
                    RandomSignedRange(SpinMin, SpinMax),
                    RandomSignedRange(SpinMin, SpinMax)) * Mathf.Deg2Rad;
            }

            Destroy(gameObject, Lifetime);
        }

        private static float RandomSignedRange(float min, float max)
        {
            float magnitude = Random.Range(min, max);
            return Random.value < 0.5f ? -magnitude : magnitude;
        }

        /// <summary>
        /// A uniformly random direction within `maxAngleDegrees` of `axis` --
        /// the standard spherical-cap construction, not Slerp-toward-a-random-
        /// point (which biases samples toward the pole rather than spreading
        /// them evenly across the cone).
        /// </summary>
        private static Vector3 RandomDirectionInCone(Vector3 axis, float maxAngleDegrees)
        {
            float angle = Random.Range(0.0f, maxAngleDegrees) * Mathf.Deg2Rad;
            float spin = Random.Range(0.0f, 360.0f);

            Vector3 arbitrary = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.99f ? Vector3.up : Vector3.right;
            Vector3 perpendicular = Vector3.Cross(axis, arbitrary).normalized;
            Vector3 rotatedPerpendicular = Quaternion.AngleAxis(spin, axis) * perpendicular;

            return (Mathf.Cos(angle) * axis + Mathf.Sin(angle) * rotatedPerpendicular).normalized;
        }
    }
}
