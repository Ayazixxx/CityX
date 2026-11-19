using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the held sword guard at every angle a bullet can knock it to (docs/BULLET_DEFLECTION.md), one-handed and two-handed, from
    /// behind as the game's camera sees it and from the front (Logs/CityShots/deflect-{sword}-{angle}-{view}.png); then a real bullet turned
    /// aside, frame by frame through the snap (deflect-{sword}-snap-{frame}.png). Needs a graphics device.
    /// </summary>
    [Explicit("capture")]
    public sealed class SwordDeflectCapture
    {
        private const float Dt = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator FilmTheGuardKnockedAbout()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            camera.fieldOfView = 40.0f;
            camera.nearClipPlane = 0.05f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            string folder = BuildingShapeCapture.ShotsFolder;
            foreach (string weapon in new[] { "CombatSword", "SpikeSword" })
            {
                GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                    new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var vitals = player.GetComponent<PlayerVitals>();
                vitals.InfiniteStamina = true;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                var sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Weapons/{weapon}.prefab"),
                    new Vector3(30.0f, 1.0f, 30.0f), Quaternion.identity).GetComponent<SwordWeapon>();
                sword.OnEquip(player, CityX.Items.ItemInstanceData.None);
                var combat = player.GetComponent<SoulsSwordCombat>();   // (added by the sword as it is equipped)
                var poser = player.GetComponent<SwordPoseDriver>();
                bool oneHanded = ((ISwordBlade)sword).IsOneHanded;
                var input = new LocomotionInput { SwordEquipped = !oneHanded, Sword1HEquipped = oneHanded };
                float height = locomotion.StandingHeight;

                IEnumerator Guard(int count)
                {
                    for (int i = 0; i < count; i++)
                    {
                        combat.SetBlockHeld(true);
                        combat.Tick(Dt);
                        locomotion.Tick(input, Dt);
                        yield return null;
                    }
                }

                void Behind(string name)
                {
                    // As the game's camera sits (docs: Saints Row framing): 5 m back, a little to the right, the head's top in the middle.
                    Vector3 body = player.transform.position;
                    BuildingShapeCapture.Shoot(camera, folder, name, body + new Vector3(0.87f, height * 1.05f, -5.0f), body + new Vector3(0.3f, height * 0.8f, 2.0f));
                }

                void Front(string name)
                {
                    Vector3 at = player.transform.position + Vector3.up * (height * 0.65f);
                    BuildingShapeCapture.Shoot(camera, folder, name, at + new Vector3(-2.4f, 0.5f, 5.4f), at);
                }

                yield return Guard(40);
                for (int angle = 0; angle < SwordStrikes.DeflectPoseCount; angle++)
                {
                    poser.Deflect(angle);
                    yield return Guard(20);
                    Behind($"deflect-{weapon}-{angle}-behind");
                    Front($"deflect-{weapon}-{angle}-front");
                }

                // A real bullet turned aside from the right, frame by frame: the glance off the blade, the snap and its settle.
                vitals.Roll = () => 0.0f;
                poser.Deflect(0);
                yield return Guard(30);
                DamageContext.Strike(vitals, 10.0f, player.transform.position + new Vector3(8.0f, 2.2f, 8.0f), 0.0f, DamageKind.Bullet, 0.0f);
                Behind($"deflect-{weapon}-snap-0");
                for (int frame = 1; frame <= 8; frame++)
                {
                    yield return Guard(1);
                    Behind($"deflect-{weapon}-snap-{frame}");
                }
                Object.Destroy(sword.gameObject);
                Object.Destroy(player);
                yield return null;
            }
            Object.Destroy(ground);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sun.gameObject);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }
    }
}
