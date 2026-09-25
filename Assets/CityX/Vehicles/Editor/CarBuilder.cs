using CityX.Vehicles;
using UnityEditor;
using UnityEngine;

namespace CityX.VehiclesEditor
{
    /// <summary>
    /// Builds the Mustang prefab from the same GLB model vehicles/car.gd
    /// uses, copied into this project's own Assets tree.
    ///
    /// THE MODEL IS NOT IN REAL-WORLD UNITS -- car.gd's own comment
    /// confirms this directly ("the GLB's own raw units aren't meters,
    /// confirmed during Stage 0's asset prep"), so this measures the
    /// visual's own bounds and normalizes against them, the same
    /// self-measuring approach RadioBuilder already uses for its own
    /// imported model, rather than trusting a magic scale number.
    /// </summary>
    public static class CarBuilder
    {
        private const string PrefabPath = "Assets/CityX/Vehicles/Mustang.prefab";
        private const string ModelPath = "Assets/CityX/Vehicles/Models/mustang.glb";

        /// <summary>Real-world Mustang length, nose to tail, scaled up 1.3x -- car.gd's own TARGET_LENGTH_METERS.</summary>
        public const float TargetLengthMeters = 4.8f * 1.3f;

        [MenuItem("CityX/Rebuild Mustang")]
        public static GameObject Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"CityX: {ModelPath} missing -- no Mustang built.");
                return null;
            }

            var root = new GameObject("Mustang");
            try
            {
                var visualRoot = new GameObject("VisualRoot");
                visualRoot.transform.SetParent(root.transform, false);

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.transform.SetParent(visualRoot.transform, false);

                // Longest HORIZONTAL dimension only (X or Z, not Y) -- a
                // car's height shouldn't factor into normalizing its own
                // length, matching car.gd's own maxf(aabb.size.x, aabb.size.z).
                Bounds bounds = Measure(visual);
                float longestHorizontal = Mathf.Max(bounds.size.x, bounds.size.z);
                float normalizeScale = longestHorizontal > 0.0001f
                    ? TargetLengthMeters / longestHorizontal
                    : 1.0f;
                visual.transform.localScale = Vector3.one * normalizeScale;
                // Re-centred on the mesh's OWN AABB centre so this node's
                // local origin -- where the collider/wheel rays/seats all
                // get built around -- already sits at the car's true
                // geometric middle, matching car.gd's own _build_visual.
                visual.transform.localPosition = -bounds.center * normalizeScale;
                Vector3 bodySize = bounds.size * normalizeScale;

                BuildCollision(root, bodySize);
                BuildInteractTrigger(root);
                Car car = BuildCarComponent(root, bodySize);
                BuildSeats(root, car, bodySize);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: Mustang written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Bounds Measure(GameObject go)
        {
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            // Renderer.bounds is WORLD space -- translate into root-local
            // space by subtracting root's own position (root sits at the
            // origin with identity rotation at build time, so this is a
            // plain offset, not a full inverse-transform).
            bounds.center -= go.transform.position;
            return bounds;
        }

        /// <summary>Bounding-box collider sized from the measured (already-normalized) extents -- not the decimated visual mesh itself.</summary>
        private static void BuildCollision(GameObject root, Vector3 bodySize)
        {
            var box = root.AddComponent<BoxCollider>();
            box.size = bodySize;

            var material = new PhysicsMaterial("MustangBody")
            {
                bounciness = 0.2f,
                dynamicFriction = 0.6f,
                staticFriction = 0.6f,
            };
            box.material = material;
        }

        /// <summary>
        /// A bigger sphere than a handheld pickup's own trigger (INTERACT_RANGE
        /// is 4m, not a gun's ~1m) since a car is a much bigger, unmissable
        /// target -- matches car.gd's own _build_interact_area. Sits
        /// alongside the BoxCollider above on the same GameObject; Unity
        /// (unlike Godot's own separate Area3D node requirement) allows a
        /// non-trigger and a trigger collider on one object without conflict.
        /// </summary>
        private static void BuildInteractTrigger(GameObject root)
        {
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = Car.InteractRange;
        }

        private static Car BuildCarComponent(GameObject root, Vector3 bodySize)
        {
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1400.0f;

            Car car = root.AddComponent<Car>();
            car.BodySize = bodySize;
            return car;
        }

        private static void BuildSeats(GameObject root, Car car, Vector3 bodySize)
        {
            Car.BuildSeatLocalPositions(bodySize, out Vector3 driverPosition, out Vector3 passengerPosition);

            var driverSeat = new GameObject("DriverSeat");
            driverSeat.transform.SetParent(root.transform, false);
            driverSeat.transform.localPosition = driverPosition;

            var passengerSeat = new GameObject("PassengerSeat");
            passengerSeat.transform.SetParent(root.transform, false);
            passengerSeat.transform.localPosition = passengerPosition;

            car.DriverSeat = driverSeat.transform;
            car.PassengerSeat = passengerSeat.transform;
        }
    }
}
