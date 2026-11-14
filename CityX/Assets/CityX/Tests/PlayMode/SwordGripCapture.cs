using System.Collections;
using System.IO;
using System.Text;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the two-handed hold on the spike sword up close (docs/SOULSLIKE_COMBAT.md) to Logs/CityShots/grip2h-*.png -- the
    /// carry and the guard, each from the thumb's side of the right hand, from the back of that hand, from above, and from the front with both
    /// hands in view. Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SwordGripCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SwordGripCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheTwoHandedHold()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(2000.0f, 1.0f, 2000.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.nearClipPlane = 0.03f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/SpikeSword.prefab"),
                new Vector3(10.0f, 1.0f, 10.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            sword.OnEquip(player, ItemInstanceData.None);
            var combat = player.GetComponent<SoulsSwordCombat>();
            var idle = new LocomotionInput { SwordEquipped = true };
            MeshFilter blade = sword.GetComponentInChildren<MeshFilter>();
            Bounds bounds = blade.sharedMesh.bounds;
            float bottom = sword.transform.InverseTransformPoint(blade.transform.TransformPoint(bounds.min)).y;
            Debug.Log($"CityX grip: the spike sword's handle ends {-bottom:0.00} m below the guard; the fists sit {SwordWeapon.DefaultPrimaryGripDown:0.00} and {SwordWeapon.DefaultSupportGripDown:0.00} below it");

            for (int i = 0; i < 40; i++)
            {
                combat.Tick(Step);
                locomotion.Tick(idle, Step);
                yield return null;
            }
            yield return Views(camera, player, sword, "grip2h-carry");

            for (int i = 0; i < 40; i++)
            {
                combat.SetBlockHeld(true);
                combat.Tick(Step);
                locomotion.Tick(idle, Step);
                yield return null;
            }
            yield return Views(camera, player, sword, "grip2h-guard");
            combat.SetBlockHeld(false);

            Object.Destroy(sword.gameObject);
            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        private const int Cell = 480;

        /// <summary>
        /// THE WHOLE BODY IN EACH TWO-HANDED HOLD (owner, 2026-11-13, of the carry: "the way he holds the two handed sword is totally wrong,
        /// his arms are all bent awkardly"): standing, walking, sprinting, the stance after a swing and the guard, each from the front-right,
        /// the left, behind like the game's camera and high off the right shoulder, to Logs/CityShots/hold2h-&lt;weapon&gt;-&lt;stance&gt;.png;
        /// and each arm's shape logged: how far the elbow is bent, where it is from its shoulder, how far the wrist is bent, how far the arm
        /// is from the body.
        /// </summary>
        [UnityTest]
        public IEnumerator RenderTheTwoHandedHoldsWholeBody()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(2000.0f, 1.0f, 2000.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            var log = new StringBuilder();

            foreach (string weapon in new[] { "SpikeSword", "BaseballBat", "Sledgehammer" })
            {
                foreach ((string stance, Vector2 stick, bool sprint, float seconds, bool swingFirst, bool guard) in new[]
                {
                    ("carry", Vector2.zero, false, 1.6f, false, false),
                    ("walk", new Vector2(0.0f, 1.0f), false, 1.6f, false, false),
                    ("sprint", new Vector2(0.0f, 1.0f), true, 2.2f, false, false),
                    ("ready", Vector2.zero, false, 1.5f, true, false),
                    ("guard", Vector2.zero, false, 1.0f, false, true),
                })
                {
                    GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                        new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
                    player.GetComponent<PlayerInputReader>().enabled = false;
                    player.GetComponent<PlayerVitals>().InfiniteStamina = true;
                    var locomotion = player.GetComponent<PlayerLocomotion>();
                    var sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Weapons/{weapon}.prefab"),
                        new Vector3(10.0f, 1.0f, 10.0f), Quaternion.identity).GetComponent<SwordWeapon>();
                    sword.OnEquip(player, ItemInstanceData.None);
                    var combat = player.GetComponent<SoulsSwordCombat>();
                    combat.HitStopSeconds = 0.0f;
                    var input = new LocomotionInput { SwordEquipped = true, Move = stick, Sprint = sprint };
                    if (swingFirst)
                    {
                        combat.RequestLight(0.0f);
                    }
                    for (float t = 0.0f; t < seconds; t += Step)
                    {
                        combat.SetBlockHeld(guard);
                        combat.Tick(Step);
                        locomotion.Tick(input, Step);
                        yield return null;
                    }
                    var sheet = new Texture2D(Cell * 4, Cell, TextureFormat.RGB24, false);
                    for (int view = 0; view < 4; view++)
                    {
                        ShootWhole(camera, sheet, view, player);
                    }
                    sheet.Apply();
                    BuildingShapeCapture.Save(sheet, directory, $"hold2h-{weapon}-{stance}");
                    Object.Destroy(sheet);
                    log.AppendLine($"{weapon} {stance}: {ArmShape(player, true)}; {ArmShape(player, false)}; tip {((ISwordBlade)sword).BladeTipWorld.y:0.00} m up");
                    combat.SetBlockHeld(false);
                    Object.Destroy(sword.gameObject);
                    Object.Destroy(player);
                    yield return null;
                }
            }
            Debug.Log("CityX two-handed holds:\n" + log);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        /// <summary>One arm's shape in words: the elbow's bend, where the elbow is from its shoulder (out to its own side, up, forward), the wrist's bend, the arm's clearance of the torso.</summary>
        private static string ArmShape(GameObject player, bool right)
        {
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform upper = animator.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            Transform lower = animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            Transform hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Vector3 upperArm = lower.position - upper.position;
            Vector3 forearm = hand.position - lower.position;
            Vector3 fingers = right ? hand.right : -hand.right;
            Vector3 elbow = player.transform.InverseTransformDirection(upperArm);
            Vector3 wrist = player.transform.InverseTransformPoint(hand.position);
            float clear = ElbowClearance.Clearance(upper.position, lower.position, hand.position, animator.GetBoneTransform(HumanBodyBones.Hips).position,
                animator.GetBoneTransform(HumanBodyBones.Neck).position, FusedBodyShape.TorsoRadius + FusedBodyShape.ArmRadius * 0.8f);
            return $"{(right ? "R" : "L")} elbow bent {Vector3.Angle(upperArm, forearm):0} deg, {(right ? elbow.x : -elbow.x) * 100.0f:0} cm out {elbow.y * 100.0f:0} up {elbow.z * 100.0f:0} fwd of the shoulder, "
                + $"wrist bent {Vector3.Angle(forearm, fingers):0} deg at ({wrist.x:0.00}, {wrist.y:0.00}, {wrist.z:0.00}), {clear * 100.0f:0} cm clear";
        }

        private static void ShootWhole(Camera camera, Texture2D sheet, int view, GameObject player)
        {
            Transform body = player.transform;
            Vector3 chest = body.position + Vector3.up * 1.3f;
            Vector3 position;
            Vector3 lookAt = chest + body.forward * 0.5f;
            camera.fieldOfView = 40.0f;
            switch (view)
            {
                case 0: position = chest + body.forward * 5.0f + body.right * 3.0f + Vector3.up * 0.8f; break;
                case 1: position = chest - body.right * 6.0f + body.forward * 0.5f + Vector3.up * 0.3f; break;
                case 2:
                    position = body.position - body.forward * 5.0f + Vector3.up * 2.5f + body.right * 0.87f;
                    lookAt = chest + body.forward * 1.2f;
                    camera.fieldOfView = 50.0f;
                    break;
                default: position = chest + body.right * 3.6f - body.forward * 1.2f + Vector3.up * 3.0f; break;
            }
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            var target = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            sheet.SetPixels(view * Cell, 0, Cell, Cell, image.GetPixels());
            Object.Destroy(image);
        }

        private static IEnumerator Views(Camera camera, GameObject player, SwordWeapon sword, string name)
        {
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Vector3 grip = sword.transform.position;
            Vector3 thumbSide = hand.TransformDirection(Vector3.forward);
            Vector3 back = hand.TransformDirection(Vector3.up);
            Vector3 mid = (grip + leftHand.position) * 0.5f;
            yield return Shoot(camera, name + "-thumb", grip + thumbSide * 0.6f + Vector3.up * 0.15f, grip);
            yield return Shoot(camera, name + "-back", grip + back * 0.6f + Vector3.up * 0.1f, grip);
            yield return Shoot(camera, name + "-above", grip + Vector3.up * 0.7f + player.transform.forward * 0.2f, grip);
            yield return Shoot(camera, name + "-front", mid + player.transform.forward * 1.3f + Vector3.up * 0.1f, mid);
            yield return Shoot(camera, name + "-whole", player.transform.position + new Vector3(2.5f, 1.6f, 2.5f), player.transform.position + Vector3.up * 1.2f);
            yield return Shoot(camera, name + "-left", player.transform.position + new Vector3(-2.8f, 1.5f, 1.8f), player.transform.position + Vector3.up * 1.2f);
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
