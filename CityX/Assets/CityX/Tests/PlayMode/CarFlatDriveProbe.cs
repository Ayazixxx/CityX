using System.Collections;
using CityX.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: how much the game's car climbs (2026-10-16, for the garage drives). On a flat plane, driven by TickDriving after each physics
    /// step, it makes 15 m in 3 s; it climbs a clean 5 and 8.6 degree ramp from rest and stalls 5 m up a 12 degree one (a box on its wheel rays,
    /// no suspension, and a two-centimetre step stops it dead): so garage drives are built to 12 % (GarageEntrances.Grade) and meet the road
    /// and the deck with no edge. Explicit.
    /// </summary>
    [Explicit("a probe of the car's climbing, not a test")]
    public sealed class CarFlatDriveProbe
    {
        [UnityTest]
        public IEnumerator TheCarDrivesOnAFlatPlane()
        {
#if UNITY_EDITOR
            var plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plane.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            plane.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Vehicles/Mustang.prefab");
            GameObject carObject = Object.Instantiate(prefab, new Vector3(0.0f, 1.2f, 0.0f), Quaternion.identity);
            var car = carObject.GetComponent<Car>();
            car.enabled = false;
            var body = carObject.GetComponent<Rigidbody>();
            for (float t = 0.0f; t < 3.0f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                car.TickDriving(0.0f, 1.0f, false, Time.fixedDeltaTime);
                if (Mathf.Abs(t - Mathf.Round(t * 4.0f) / 4.0f) < Time.fixedDeltaTime * 0.5f)
                {
                    Debug.Log($"CityX car probe t={t:F2}: z {carObject.transform.position.z:F2} y {carObject.transform.position.y:F2} v {body.linearVelocity} sleeping {body.IsSleeping()} kinematic {body.isKinematic}");
                }
            }
            Debug.Log($"CityX car probe: moved {carObject.transform.position.z:F2} m in 3 s");
            Object.Destroy(carObject);

            // A ramp: a box tilted up at each angle, its low edge sunk flush with the plane, 8 m ahead of a car at rest.
            foreach (float angle in new[] { 5.0f, 8.6f, 12.0f })
            {
                var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                float length = 20.0f;
                ramp.transform.localScale = new Vector3(8.0f, 0.4f, length);
                ramp.transform.rotation = Quaternion.Euler(-angle, 0.0f, 0.0f);
                // The top face's low edge at (0, 0, 8): the box's centre half its length up the slope and 0.2 m under the top face.
                Vector3 up = ramp.transform.up, along = ramp.transform.forward;
                ramp.transform.position = new Vector3(0.0f, 0.0f, 8.0f) + along * (length * 0.5f) - up * 0.2f;
                yield return new WaitForFixedUpdate();
                carObject = Object.Instantiate(prefab, new Vector3(0.0f, 1.1f, 0.0f), Quaternion.identity);
                car = carObject.GetComponent<Car>();
                car.enabled = false;
                body = carObject.GetComponent<Rigidbody>();
                for (float t = 0.0f; t < 6.0f; t += Time.fixedDeltaTime)
                {
                    yield return new WaitForFixedUpdate();
                    car.TickDriving(0.0f, Vector3.Dot(body.linearVelocity, Vector3.forward) < 5.0f ? 1.0f : 0.0f, false, Time.fixedDeltaTime);
                }
                Debug.Log($"CityX car probe ramp {angle:F1} deg: the car got to z {carObject.transform.position.z:F2} (the ramp starts at 8), y {carObject.transform.position.y:F2}, pitch {carObject.transform.eulerAngles.x:F0}");
                Object.Destroy(carObject);
                Object.Destroy(ramp);
                yield return null;
            }
            Object.Destroy(plane);
#else
            yield break;
#endif
        }
    }
}
