using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the Crimson Claw rendered (owner, 2026-11-07). One attack filmed ("improve upon the crimson claw attack animation make it look
    /// more fluid less robotic"), a frame every 1/20 s from its wind-up to after it has recovered, from three-quarters in front and from the
    /// side: Logs/CityShots/crimsonclaw-attack-{front|side}-NN.png. Its eyes through the game's grade (the bloom takes their glow), by day and by
    /// night, and going out as it dies ("make it glow red from within the head only visible through the eyes, the glow fades away upon
    /// death"): crimsonclaw-eyes-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CrimsonClawCapture
    {
        private const int FramesBetweenShots = 3;   // at 60 frames a second: 20 shots a second
        private const int Shots = 26;               // 1.3 s: the whole attack and a little after

        private static readonly Color DaySky = new Color(0.62f, 0.68f, 0.76f);
        private static readonly Color NightSky = new Color(0.02f, 0.02f, 0.03f);

        [UnityTest]
        public IEnumerator FilmOneAttack()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            var stage = new Stage();
            // The player 2.2 m in front of it: in reach, so it attacks as soon as it is ready.
            GameObject player = stage.Player(new Vector3(0.0f, 0.05f, 2.2f));
            CrimsonClaw claw = stage.Claw();
            for (int i = 0; i < 300 && !claw.IsAttacking; i++)
            {
                yield return null;
            }
            Assert.IsTrue(claw.IsAttacking, "it never attacked the player in front of it");

            for (int shot = 0; shot < Shots; shot++)
            {
                Vector3 middle = claw.transform.position + Vector3.up * 0.3f;
                Vector3 forward = claw.transform.forward;
                Vector3 right = claw.transform.right;
                stage.Shoot($"crimsonclaw-attack-front-{shot:00}", middle + forward * 6.5f - right * 4.0f + Vector3.up * 2.5f, middle);
                stage.Shoot($"crimsonclaw-attack-side-{shot:00}", middle + right * 7.5f + Vector3.up * 1.2f, middle);
                for (int i = 0; i < FramesBetweenShots; i++)
                {
                    yield return null;
                }
            }
            Object.Destroy(player);
            stage.Clear(claw.gameObject);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ShowItsEyes()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            var stage = new Stage();
            CrimsonClaw claw = stage.Claw();
            stage.Graded(true);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
            Vector3 face = claw.transform.position + Vector3.up * 0.8f + claw.transform.forward * 0.6f;
            Vector3 forward = claw.transform.forward;
            Vector3 right = claw.transform.right;
            stage.Shoot("crimsonclaw-eyes-day-front", face + forward * 3.2f + Vector3.up * 1.4f, face);
            stage.Shoot("crimsonclaw-eyes-day-threequarter", face + forward * 2.6f + right * 2.0f + Vector3.up * 0.9f, face);
            stage.Shoot("crimsonclaw-eyes-day-far", face + forward * 8.0f + Vector3.up * 1.5f, face);

            yield return stage.Night();
            stage.Shoot("crimsonclaw-eyes-night-front", face + forward * 3.2f + Vector3.up * 1.4f, face);
            stage.Shoot("crimsonclaw-eyes-night-far", face + forward * 8.0f + Vector3.up * 1.5f, face);
            stage.Shoot("crimsonclaw-eyes-night-side", face + right * 3.5f + Vector3.up * 0.4f, face);
            stage.Shoot("crimsonclaw-eyes-night-below", claw.transform.position - Vector3.up * 1.2f + forward * 2.5f, face);

            // Dead: the head falls loose and its eyes go out.
            ((IDamageable)claw).ApplyHit(CrimsonClaw.MaxHealth, claw.transform.position + forward, 0.0f);
            Transform head = claw.BodyVisual;
            for (int shot = 0; shot < 5; shot++)
            {
                Vector3 at = head.position + Vector3.up * 0.8f;
                stage.Shoot($"crimsonclaw-eyes-dying-{shot}", at + forward * 4.0f + Vector3.up * 2.0f, at);
                for (int i = 0; i < 24; i++)   // 0.4 s
                {
                    yield return null;
                }
            }
            foreach (GameObject debris in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (debris.name == "CrimsonClawDebris")
                {
                    Object.Destroy(debris);
                }
            }
            stage.Clear(claw.gameObject);
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        /// <summary>The ground, the sun and a camera, the light of day; a Claw and a player to stand with it.</summary>
        private sealed class Stage
        {
            private readonly GameObject _ground;
            private readonly Light _sun;
            private readonly Camera _camera;
            private readonly string _folder = BuildingShapeCapture.ShotsFolder;
            private UnityEngine.Rendering.Volume _grade;

            public Stage()
            {
                LogAssert.ignoreFailingMessages = true;
                Time.captureFramerate = 60;
                _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                _ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
                _ground.GetComponent<Renderer>().material.color = new Color(0.32f, 0.33f, 0.36f);
                _sun = new GameObject("Sun").AddComponent<Light>();
                _sun.type = LightType.Directional;
                _sun.intensity = 1.3f;
                _sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
                _camera = new GameObject("Cam").AddComponent<Camera>();
                _camera.fieldOfView = 35.0f;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = DaySky;
            }

            public GameObject Player(Vector3 at)
            {
                GameObject player = Object.Instantiate(
                    UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), at, Quaternion.Euler(0.0f, 180.0f, 0.0f));
                player.GetComponent<PlayerInputReader>().enabled = false;
                return player;
            }

            public CrimsonClaw Claw()
            {
                return Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/CrimsonClaw.prefab"),
                    new Vector3(0.0f, CrimsonClaw.HoverHeightAboveGround, 0.0f), Quaternion.identity).GetComponent<CrimsonClaw>();
            }

            /// <summary>Through the game's own grade (its bloom, its tonemap), or not.</summary>
            public void Graded(bool on)
            {
                if (on && _grade == null)
                {
                    _grade = new GameObject("Grade").AddComponent<UnityEngine.Rendering.Volume>();
                    _grade.isGlobal = true;
                    _grade.sharedProfile =
                        UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/CityX/View/Materials/CityGrade.asset");
                    _camera.allowHDR = true;
                }
                if (!_camera.TryGetComponent(out UnityEngine.Rendering.Universal.UniversalAdditionalCameraData cameraData))
                {
                    cameraData = _camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                }
                cameraData.renderPostProcessing = on;
            }

            /// <summary>No sun and a near-black world: its eyes are the light.</summary>
            public IEnumerator Night()
            {
                _sun.intensity = 0.0f;
                _camera.backgroundColor = NightSky;
                _ground.GetComponent<Renderer>().material.color = new Color(0.08f, 0.08f, 0.09f);
                for (int i = 0; i < 10; i++)
                {
                    yield return null;
                }
            }

            public void Shoot(string name, Vector3 from, Vector3 at) => BuildingShapeCapture.Shoot(_camera, _folder, name, from, at);

            public void Clear(params GameObject[] also)
            {
                foreach (GameObject go in also)
                {
                    Object.Destroy(go);
                }
                if (_grade != null)
                {
                    Object.Destroy(_grade.gameObject);
                }
                Object.Destroy(_camera.gameObject);
                Object.Destroy(_sun.gameObject);
                Object.Destroy(_ground);
            }
        }
#endif
    }
}
