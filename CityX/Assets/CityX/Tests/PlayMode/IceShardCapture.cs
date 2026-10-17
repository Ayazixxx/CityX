using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the ice shards in flight and stuck (in a wall, in a character) to Logs/CityShots/ice-*.png so they can be LOOKED AT.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.IceShardCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class IceShardCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private string _directory;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private void Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            const int width = 1024, height = 768;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
                File.WriteAllBytes(Path.Combine(_directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
                Object.Destroy(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static IEnumerator Seconds(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RenderTheShards()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            _directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(_directory);

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            var caster = vitals.GetComponent<IceShardCaster>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            yield return Seconds(0.5f);

            var cameraObject = new GameObject("IceCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 55.0f;
            camera.farClipPlane = 600.0f;
            camera.allowHDR = true;

            Transform body = vitals.transform;
            Vector3 chest = body.position + Vector3.up * 1.77f;
            Collider[] own = vitals.GetComponentsInChildren<Collider>();

            // A wall some way off: the first direction round the compass that meets something upright between 9 and 28 m away.
            Vector3 wallDirection = Vector3.zero;
            RaycastHit wallHit = default;
            for (int degrees = 0; degrees < 360 && wallDirection == Vector3.zero; degrees += 10)
            {
                Vector3 d = Quaternion.Euler(0.0f, degrees, 0.0f) * Vector3.forward;
                if (Physics.Raycast(chest, d, out RaycastHit hit, 28.0f, ~0, QueryTriggerInteraction.Ignore)
                    && hit.distance > 9.0f && Mathf.Abs(hit.normal.y) < 0.2f && !hit.collider.transform.IsChildOf(body))
                {
                    wallDirection = d;
                    wallHit = hit;
                }
            }
            Assert.AreNotEqual(Vector3.zero, wallDirection, "found no wall to shoot at");
            body.rotation = Quaternion.LookRotation(wallDirection);
            Physics.SyncTransforms();

            // A character standing between, a third of the way there: its own capsule so the shards have something living to meet.
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "IceTarget";
            Vector3 sideways = Vector3.Cross(Vector3.up, wallDirection);
            dummy.transform.position = body.position + wallDirection * 6.0f + sideways * 3.5f + Vector3.up * 1.0f;   // off to one side, out of the first volley's way
            dummy.transform.localScale = new Vector3(0.9f, 1.3f, 0.9f);
            dummy.AddComponent<SparringDummy>().Reset();
            Physics.SyncTransforms();

            // Volley 1 at the wall, aim sweeping across it; in flight shots first.
            Vector3 shoulder = chest + sideways * 0.3f;
            Assert.IsTrue(caster.TryCast(shoulder, (wallHit.point + sideways * -2.5f + Vector3.up * 0.4f - shoulder).normalized, own));
            float t = 0.0f;
            bool shotFlight = false;
            while (t < 0.34f)
            {
                float k = t / 0.34f;
                Vector3 aimPoint = wallHit.point + sideways * Mathf.Lerp(-2.5f, 2.5f, k) + Vector3.up * Mathf.Lerp(0.4f, 1.0f, k);
                caster.Aim(shoulder, (aimPoint - shoulder).normalized);
                if (!shotFlight && t > 0.14f)
                {
                    shotFlight = true;
                    Shoot(camera, "ice-flight-side", chest + sideways * 4.0f - wallDirection * 3.0f + Vector3.up * 0.6f, chest + wallDirection * 5.0f);
                    Shoot(camera, "ice-flight-behind", chest - wallDirection * 4.0f + Vector3.up * 1.0f, chest + wallDirection * 8.0f);
                }
                t += Time.deltaTime;
                yield return null;
            }
            yield return Seconds(0.8f);

            // The volley stuck in the wall, close up and from an angle: looked for where the shards actually ended up.
            Vector3 centre = Vector3.zero;
            int stuck = 0;
            var report = new System.Text.StringBuilder();
            foreach (IceShardProjectile shard in caster.LastVolley)
            {
                if (shard == null) { report.Append(" [gone]"); continue; }
                report.Append($" [{shard.CurrentPhase} {shard.transform.position:0.0} on {(shard.StuckTo != null ? shard.StuckTo.name : "-")}]");
                if (shard.CurrentPhase == IceShardProjectile.Phase.StuckInWorld)
                {
                    centre += shard.transform.position;
                    stuck++;
                }
            }
            Debug.Log($"CityX: volley 1 ended:{report} (wall point {wallHit.point:0.0}, normal {wallHit.normal:0.00})");
            Vector3 focus = stuck > 0 ? centre / stuck : wallHit.point;
            Vector3 normal = wallHit.normal;
            Shoot(camera, "ice-wall-closeup", focus + normal * 4.5f + sideways * 1.5f, focus);
            Shoot(camera, "ice-wall-oblique", focus + normal * 3.0f + sideways * 4.5f, focus);

            // Volley 2 at the character.
            yield return Seconds(2.6f);
            var victim = dummy.GetComponent<SparringDummy>();
            Vector3 toDummy = dummy.transform.position + Vector3.up * 0.3f - chest;
            Assert.IsTrue(caster.TryCast(chest, toDummy.normalized, own));
            yield return Seconds(0.7f);
            Shoot(camera, "ice-character-side", dummy.transform.position + sideways * 4.0f + Vector3.up * 0.6f, dummy.transform.position);
            Shoot(camera, "ice-character-front", dummy.transform.position - wallDirection * 4.5f + Vector3.up * 0.8f, dummy.transform.position);
            Debug.Log($"CityX: ice capture wrote to {_directory}; the character was hit {victim.TimesHit} time(s)");
            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }
    }
}
