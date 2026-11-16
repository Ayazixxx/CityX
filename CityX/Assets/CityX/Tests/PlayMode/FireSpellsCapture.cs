using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.View;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the fire spells (docs/FIRE.md) in the real city to Logs/CityShots/fire-spells-*.png, from the game's own camera
    /// behind the player (<c>-game</c>) and close (<c>-close</c>, <c>-side</c>): a fireball in flight, its burst on the ground and on a wall,
    /// the scorch cooling after it, a charged ball's burst, the Flame Staff's stream on a dummy and on the floor, and a gasoline fire; by day
    /// and at midnight. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.FireSpellsCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FireSpellsCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const string FireballPath = "Assets/CityX/Weapons/FireballProjectile.prefab";
        private const float Step = 1.0f / 60.0f;
        private static readonly float Day = SkyCycle.DaySeconds * 0.42f;
        private static readonly float Midnight = SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f;

        private Scene _scene;
        private readonly List<GameObject> _made = new List<GameObject>();
        private Camera _game;
        private Camera _side;
        private ThirdPersonCamera _follow;
        private PlayerLocomotion _locomotion;
        private Transform _body;
        private Vector3 _heading;
        private Vector3 _street;
        private GameObject _fireball;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            FireScene.Clear();
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderTheFireSpells()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            BuildingShapeCapture.UseTheScenesAir();
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            SetTime(cycle, Day);

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            _locomotion = vitals.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 400 && !_locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            vitals.InfiniteHealth = true;
            _body = vitals.transform;
            _heading = OpenRun.Face(vitals, _made);
            _street = _heading;
            vitals.GetComponent<PlayerInputReader>().enabled = false;
            _follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            Assert.IsNotNull(_follow, "the scene has no follow camera");
            _game = _follow.GetComponent<Camera>();
            _side = new GameObject("SideCam").AddComponent<Camera>();
            _made.Add(_side.gameObject);
            _side.fieldOfView = 50.0f;
            _side.farClipPlane = 2000.0f;
            _side.allowHDR = true;
            _side.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            _side.enabled = false;
            _fireball = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FireballPath);
            Assert.IsNotNull(_fireball, "no fireball prefab -- rebuild the scene");
            yield return Face(_street, 30);

            yield return Everything("fire-spells-day", true);
            SetTime(cycle, Midnight);
            yield return Frames(12);
            yield return Everything("fire-spells-night", false);
#else
            yield break;
#endif
        }

        private IEnumerator Everything(string prefix, bool all)
        {
            yield return FireballInFlight(prefix);
            yield return FireballOnTheGround(prefix + "-ground", 1.0f, all);
            if (all)
            {
                yield return FireballOnAWall(prefix + "-wall");
                yield return FireballOnTheGround(prefix + "-charged", FireballCaster.StaffMostSize, false);
            }
            yield return FlameStream(prefix);
            yield return GunAndJetpack(prefix);
            yield return Gasoline(prefix);
        }

        // --- the flamethrower and a jetpack (2026-11-15: the fire spells' stream, "just try to improve all the fire") ---

        private IEnumerator GunAndJetpack(string prefix)
        {
            Vector3 side = OpenSide(_body.position + Vector3.up);
            Vector3 at = Ground(_body.position + _heading * 2.5f + side * 2.0f);
            GameObject gun = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Flamethrower.prefab"),
                at + Vector3.up * 1.5f, Quaternion.LookRotation(_heading));
            _made.Add(gun);
            var flamethrower = gun.GetComponent<Flamethrower>();
            Assert.IsNotNull(flamethrower.Flame, "the flamethrower has no stream of fire");
            flamethrower.Flame.Run();
            GameObject pack = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Jetpack.prefab"),
                Ground(at - _heading * 2.0f) + Vector3.up * 1.6f, Quaternion.LookRotation(_heading));
            _made.Add(pack);
            var jetpack = pack.GetComponent<Jetpack>();
            jetpack.FlameEffect.Run(1.0f);
            yield return Frames(45);
            Vector3 middle = at + Vector3.up * 1.4f;
            Shoot(prefix + "-gun-side", middle + side * 4.5f + Vector3.up * 0.5f - _heading * 0.5f, middle + _heading * 2.5f);
            Shoot(prefix + "-jetpack-close", pack.transform.position + side * 2.4f + Vector3.up * 0.2f, pack.transform.position + Vector3.down * 0.7f);
            flamethrower.Flame.Stop();
            jetpack.FlameEffect.Stop();
            Object.Destroy(gun);
            Object.Destroy(pack);
            yield return Frames(60);
        }

        // --- the fireball ---

        private IEnumerator FireballInFlight(string prefix)
        {
            yield return Face(_street, 10);
            Vector3 hand = Hand();
            FireballProjectile ball = Launch(hand, _heading);
            yield return Frames(10);   // about 3.3 m out
            Assert.IsTrue(ball != null, "the fireball burst before its picture");
            Vector3 at = ball.transform.position;
            Vector3 side = OpenSide(at);
            Game(prefix + "-flight-game");
            Shoot(prefix + "-flight-side", at + side * 5.0f + Vector3.up * 0.6f - _heading * 1.0f, at - _heading * 1.2f);
            Shoot(prefix + "-flight-close", at + side * 1.8f + Vector3.up * 0.3f + _heading * 0.6f, at - _heading * 0.4f);
            if (ball != null) Object.Destroy(ball.gameObject);
            yield return Frames(30);
        }

        private IEnumerator FireballOnTheGround(string prefix, float size, bool cooling)
        {
            yield return Face(_street, 10);
            Vector3 hand = Hand();
            Vector3 target = Ground(_body.position + _heading * 7.0f);
            FireballProjectile ball = Launch(hand, (target - hand).normalized, size, size > 1.0f ? FireballCaster.StaffMostBlastRadius : 0.0f);
            for (int i = 0; i < 120 && ball != null; i++)
            {
                yield return null;
            }
            yield return Frames(3);
            Vector3 side = OpenSide(target + Vector3.up);
            float back = 4.0f + 2.0f * size;
            Game(prefix + "-burst-game");
            Shoot(prefix + "-burst-close", target - _heading * back + side * back * 0.6f + Vector3.up * 2.0f * size, target + Vector3.up * 0.6f * size);
            yield return Frames(18);
            Game(prefix + "-burst2-game");
            Shoot(prefix + "-burst2-close", target - _heading * back + side * back * 0.6f + Vector3.up * 2.0f * size, target + Vector3.up * 0.8f * size);
            if (!cooling)
            {
                yield return Frames(240);
                yield break;
            }
            // The scorch and its cracks cooling: so long after the burst (21 frames have gone since it).
            float since = 21.0f / 60.0f;
            foreach (float seconds in new[] { 1.5f, 3.0f, 6.0f })
            {
                yield return Frames(Mathf.RoundToInt((seconds - since) * 60.0f));
                since = seconds;
                Shoot($"{prefix}-scorch-{seconds:0.0}s", target - _heading * 2.2f + side * 1.2f + Vector3.up * 2.6f, target);
            }
        }

        private IEnumerator FireballOnAWall(string prefix)
        {
            Vector3 chest = _body.position + Vector3.up * 1.6f;
            if (!FindWall(chest, out RaycastHit wall))
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _made.Add(box);
                box.transform.position = Ground(_body.position + _heading * 10.0f) + Vector3.up * 2.0f;
                box.transform.rotation = Quaternion.LookRotation(_heading);
                box.transform.localScale = new Vector3(8.0f, 4.0f, 0.5f);
                Physics.SyncTransforms();
                Physics.Raycast(chest, _heading, out wall, 30.0f);
            }
            Vector3 toWall = Vector3.ProjectOnPlane(wall.point - _body.position, Vector3.up).normalized;
            yield return Face(toWall, 30);
            Vector3 hand = Hand();
            Vector3 aim = wall.point + Vector3.up * 0.4f;
            FireballProjectile ball = Launch(hand, (aim - hand).normalized);
            for (int i = 0; i < 120 && ball != null; i++)
            {
                yield return null;
            }
            yield return Frames(3);
            Vector3 side = Vector3.Cross(Vector3.up, wall.normal).normalized;
            Game(prefix + "-burst-game");
            Shoot(prefix + "-burst-close", aim + wall.normal * 4.5f + side * 2.0f + Vector3.up * 0.8f, aim);
            yield return Frames(60);
            Shoot(prefix + "-scorch-1.0s", aim + wall.normal * 3.0f + side * 1.2f + Vector3.up * 0.4f, aim);
            yield return Frames(150);
            Shoot(prefix + "-scorch-3.5s", aim + wall.normal * 3.0f + side * 1.2f + Vector3.up * 0.4f, aim);
            yield return Face(_street, 30);
        }

        private FireballProjectile Launch(Vector3 from, Vector3 direction, float size = 1.0f, float blast = 0.0f)
        {
            FireballProjectile ball = FireballProjectile.Spawn(_fireball, from, direction * FireballCaster.FireballSpeed, 0.0f,
                _body.GetComponentsInChildren<Collider>(), cosmetic: true, size: size, blastRadius: blast);
            Assert.IsNotNull(ball, "no fireball");
            return ball;
        }

        private Vector3 Hand() => _body.position + Vector3.up * 1.9f + _body.right * 0.35f + _body.forward * 0.8f;

        // --- the flame staff ---

        private IEnumerator FlameStream(string prefix)
        {
            yield return Face(_street, 10);
            var inventory = _body.GetComponent<Inventory>();
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.FlameStaff, _body.gameObject), "the flame staff would not go in hand");
            var staff = _body.GetComponentInChildren<StaffItem>() as IEquippableStaff;
            Assert.IsNotNull(staff, "no staff in hand");
            GameObject dummy = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                Ground(_body.position + _heading * 5.5f) + Vector3.up * 0.1f, Quaternion.LookRotation(-_heading));
            dummy.GetComponent<PlayerInputReader>().enabled = false;
            dummy.GetComponent<PlayerVitals>().InfiniteHealth = true;
            _made.Add(dummy);
            yield return Frames(20);

            Vector3 chest = dummy.transform.position + Vector3.up * 1.4f;
            yield return Hold(staff, true, 1.0f, chest);
            Vector3 side = OpenSide(_body.position + _street * 3.0f + Vector3.up * 1.5f);
            Vector3 middle = _body.position + _heading * 3.0f + Vector3.up * 1.5f;
            Game(prefix + "-stream-dummy-game");
            Shoot(prefix + "-stream-dummy-side", middle + side * 7.5f + Vector3.up * 0.8f, middle);
            Shoot(prefix + "-stream-dummy-close", chest + side * 2.6f - _heading * 1.6f + Vector3.up * 0.3f, chest - _heading * 1.2f);
            Object.Destroy(dummy);
            yield return Hold(staff, false, 0.6f, null);

            Vector3 floor = Ground(_body.position + _heading * 4.5f);
            yield return Hold(staff, true, 1.5f, floor);
            Game(prefix + "-stream-floor-game");
            Shoot(prefix + "-stream-floor-side", middle + side * 7.5f + Vector3.up * 1.5f, floor + Vector3.up * 0.8f);
            yield return Hold(staff, false, 1.0f, null);
            Shoot(prefix + "-stream-floor-after", floor - _heading * 2.5f + side * 2.0f + Vector3.up * 2.4f, floor);
            staff.CancelCharge(_body.gameObject);
            yield return Frames(240);
        }

        private IEnumerator Hold(IEquippableStaff staff, bool held, float seconds, Vector3? at)
        {
            float yaw = Quaternion.LookRotation(_heading).eulerAngles.y;
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = _game.transform.position;
                Vector3 look = at.HasValue ? (at.Value - eye).normalized : _heading;
                staff.Tick(held, eye, look, _body.gameObject, Step);
                _locomotion.Tick(new LocomotionInput { YawDegrees = yaw }, Step);
                yield return null;
            }
        }

        // --- gasoline ---

        private IEnumerator Gasoline(string prefix)
        {
            Vector3 side = OpenSide(_body.position + Vector3.up);
            Vector3 start = Ground(_body.position + _heading * 6.0f + side * 2.5f);
            for (int i = 0; i < 6; i++)
            {
                FireScene.Pour(start + _heading * (i * 0.7f) + Vector3.up * 0.3f, 0.4f);
            }
            FireScene.Ignite(start, 0.5f, true, null);
            yield return Frames(150);
            Vector3 middle = start + _heading * 1.8f;
            Game(prefix + "-gasoline-game");
            Shoot(prefix + "-gasoline-close", middle - _heading * 3.5f - side * 3.0f + Vector3.up * 1.6f, middle + Vector3.up * 0.5f);
            FireScene.Clear();
        }

        // --- helpers ---

        private IEnumerator Face(Vector3 heading, int frames)
        {
            _heading = heading;
            float yaw = Quaternion.LookRotation(heading).eulerAngles.y;
            _body.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);
            _follow.Yaw = yaw;
            _follow.Pitch = ThirdPersonCamera.DefaultPitch;
            for (int i = 0; i < frames; i++)
            {
                _locomotion.Tick(new LocomotionInput { YawDegrees = yaw }, Step);
                yield return null;
            }
        }

        /// <summary>The side of the street with no wall within 9 m of <paramref name="at"/>, for a camera looking across it.</summary>
        private Vector3 OpenSide(Vector3 at)
        {
            Vector3 side = Vector3.Cross(Vector3.up, _street);
            return BloodSplats.Surface(at, side, 9.0f, _body, out _) ? -side : side;
        }

        private bool FindWall(Vector3 chest, out RaycastHit wall)
        {
            for (int a = 0; a < 36; a++)
            {
                var direction = Quaternion.Euler(0.0f, a * 10.0f, 0.0f) * _heading;
                if (BloodSplats.Surface(chest, direction, 18.0f, _body, out wall) && wall.distance > 7.0f && Mathf.Abs(wall.normal.y) < 0.3f)
                {
                    return true;
                }
            }
            wall = default;
            return false;
        }

        private void Game(string name)
        {
            Texture2D image = BuildingShapeCapture.Render(_game);
            BuildingShapeCapture.Save(image, BuildingShapeCapture.ShotsFolder, name);
            Object.Destroy(image);
        }

        private void Shoot(string name, Vector3 position, Vector3 lookAt)
        {
            BuildingShapeCapture.Shoot(_side, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static Vector3 Ground(Vector3 at) =>
            Physics.Raycast(at + Vector3.up * 15.0f, Vector3.down, out RaycastHit hit, 40.0f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : at;

        private static void SetTime(DayNightCycle cycle, float seconds)
        {
            if (cycle == null)
            {
                return;
            }
            cycle.Running = false;
            cycle.TimeOfDay = seconds;
            cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
        }
    }
}
