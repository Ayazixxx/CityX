using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the Wraith Staff rendered (owner, 2026-11-08). In the hand, its orb glowing the Wraith's eyes' red, by day and by night
    /// (wraithstaff-held-*.png), and its summon filmed, a frame every 1/5 s, following its summoner and then fighting a Crimson Claw
    /// (wraithstaff-summon-NN.png). Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class WraithStaffCapture
    {
#if UNITY_EDITOR
        private GameObject _ground;
        private Light _sun;
        private Camera _camera;
        private UnityEngine.Rendering.Volume _grade;
#endif

        [UnityTest]
        public IEnumerator ShowTheStaffInTheHand()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Stage();
            PlayerVitals player = Player(Vector3.zero, 160.0f);
            var staff = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/WraithStaff.prefab"))
                .GetComponent<WraithStaff>();
            staff.MountCosmetic(player.gameObject);
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }
            Vector3 orb = staff.Tip.position;
            Vector3 chest = player.transform.position + Vector3.up * 1.5f;
            Vector3 front = player.transform.forward;
            Vector3 right = player.transform.right;
            Shoot("wraithstaff-held-day", chest + front * 4.5f + right * 1.8f + Vector3.up * 0.6f, chest);
            Shoot("wraithstaff-held-day-orb", orb + front * 1.3f + right * 0.5f + Vector3.up * 0.2f, orb);
            Night();
            yield return null;
            Shoot("wraithstaff-held-night", chest + front * 4.5f + right * 1.8f + Vector3.up * 0.6f, chest);
            Shoot("wraithstaff-held-night-orb", orb + front * 1.3f + right * 0.5f + Vector3.up * 0.2f, orb);
            Object.Destroy(staff.gameObject);
            Object.Destroy(player.gameObject);
            Clear();
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator FilmTheSummonFollowingAndFighting()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Stage();
            PlayerVitals player = Player(Vector3.zero, 0.0f);
            var caster = player.gameObject.AddComponent<WraithSummonCaster>();
            caster.WraithPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Wraith.prefab");
            Assert.IsTrue(caster.TrySummon(Vector3.forward), "nothing was summoned");
            Wraith summon = caster.Current;
            GameObject claw = null;
            for (int shot = 0; shot < 40; shot++)
            {
                if (shot < 15)
                {
                    player.transform.position += Vector3.forward * 0.6f;   // walking on: it keeps up
                }
                if (shot == 15)
                {
                    claw = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/CrimsonClaw.prefab"),
                        player.transform.position + new Vector3(0.0f, CrimsonClaw.HoverHeightAboveGround, 9.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
                }
                Vector3 middle = player.transform.position + Vector3.forward * (shot < 15 ? 0.0f : 4.5f) + Vector3.up * 1.2f;
                Shoot($"wraithstaff-summon-{shot:00}", middle + new Vector3(9.0f, 4.0f, -4.0f), middle);
                for (int i = 0; i < 12; i++)
                {
                    yield return null;
                }
            }
            Assert.IsTrue(summon == null || !summon.IsAlive || summon.Health < Wraith.SummonedMaxHealth || claw.GetComponent<CrimsonClaw>().Health < CrimsonClaw.MaxHealth,
                "the summon and the Claw never fought");
            foreach (Wraith wraith in new System.Collections.Generic.List<Wraith>(Wraith.All))
            {
                Object.Destroy(wraith.gameObject);
            }
            if (claw != null)
            {
                Object.Destroy(claw);
            }
            Object.Destroy(player.gameObject);
            Clear();
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private void Stage()
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
            _camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            _camera.allowHDR = true;
            _grade = new GameObject("Grade").AddComponent<UnityEngine.Rendering.Volume>();
            _grade.isGlobal = true;
            _grade.sharedProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/CityX/View/Materials/CityGrade.asset");
            _camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
        }

        private static PlayerVitals Player(Vector3 at, float yaw)
        {
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at + Vector3.up * 0.05f, Quaternion.Euler(0.0f, yaw, 0.0f));
            body.GetComponent<PlayerInputReader>().enabled = false;
            var vitals = body.GetComponent<PlayerVitals>();
            vitals.Revive();
            return vitals;
        }

        private void Night()
        {
            _sun.intensity = 0.0f;
            _camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            _ground.GetComponent<Renderer>().material.color = new Color(0.08f, 0.08f, 0.09f);
        }

        private void Shoot(string name, Vector3 from, Vector3 at) => BuildingShapeCapture.Shoot(_camera, BuildingShapeCapture.ShotsFolder, name, from, at);

        private void Clear()
        {
            Object.Destroy(_grade.gameObject);
            Object.Destroy(_camera.gameObject);
            Object.Destroy(_sun.gameObject);
            Object.Destroy(_ground);
        }
#endif
    }
}
