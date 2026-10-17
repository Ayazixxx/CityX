using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// WHERE THE HANDS ARE, ON THE REAL RIG, WITH THE REAL WEAPON IN THEM.
    ///
    /// Mixamo's pistol and two-handed-sword clips were authored for Mixamo's own character holding
    /// Mixamo's own prop. Retargeted onto this body and used with these models, the hands land
    /// wherever the maths puts them -- often NOT on the weapon -- and nothing in the project pinned
    /// them there (there was no IK anywhere). Every practical guide to using mocap for two-handed
    /// weapons says the same thing: put the primary hand where the animation puts it, then IK the
    /// support hand onto the weapon. These tests measure the result, in real frames, and write
    /// pictures to Logs/WeaponPoses/ when a graphics device is present.
    /// </summary>
    public sealed class WeaponHandsPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private GameObject _weaponObject;
        private PlayerLocomotion _locomotion;
        private Animator _animator;
        private GameObject _light;

        private WeaponGripSettings _realSettings;

        /// <summary>These tests measure the DERIVED hold, so they run against untouched settings, whatever has been saved in the project.</summary>
        [SetUp]
        public void SetUp()
        {
            _realSettings = WeaponGripSettings.Current;
            WeaponGripSettings.Current = ScriptableObject.CreateInstance<WeaponGripSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            WeaponGripSettings.Current = _realSettings;
            if (_player != null) Object.Destroy(_player);
            if (_weaponObject != null) Object.Destroy(_weaponObject);
            if (_ground != null) Object.Destroy(_ground);
            if (_light != null) Object.Destroy(_light);
        }

        private void BuildPlayer()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");
            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.1f, 0.0f), Quaternion.identity);
            var reader = _player.GetComponent<PlayerInputReader>();
            if (reader != null) reader.enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();

            _light = new GameObject("Light");
            Light sun = _light.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
#endif
        }

        private Transform Bone(HumanBodyBones bone) => _animator.GetBoneTransform(bone);

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            return Vector3.Distance(p, a + ab * t);
        }

        // ---- pictures ----

        private static bool CanPhotograph => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        private static void SavePicture(string name, Texture2D image)
        {
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "WeaponPoses");
            Directory.CreateDirectory(folder);
            System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            System.Reflection.MethodInfo encode = conversion?.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            if (encode?.Invoke(null, new object[] { image }) is byte[] bytes)
            {
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), bytes);
            }
        }

        /// <summary>The body from a point beside and in front of it, looking at the chest.</summary>
        private void Photograph(string name, Vector3 from, float fov = 38.0f)
        {
            if (!CanPhotograph)
            {
                return;
            }
            const int width = 720, height = 720;
            var cameraObject = new GameObject("PoseCamera");
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.66f, 0.82f);
                camera.fieldOfView = fov;
                camera.nearClipPlane = 0.05f;
                Vector3 chest = _player.transform.position + Vector3.up * 1.7f;
                cameraObject.transform.position = _player.transform.TransformPoint(from);
                cameraObject.transform.rotation = Quaternion.LookRotation(chest - cameraObject.transform.position, Vector3.up);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                SavePicture(name, image);
                Object.Destroy(image);
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(cameraObject);
            }
        }

        // ---- the pistol ----

        private PistolWeapon EquipPistol()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Pistol.prefab");
            Assert.IsNotNull(prefab, "Pistol.prefab is missing");
            _weaponObject = Object.Instantiate(prefab, new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
            var pistol = _weaponObject.GetComponent<PistolWeapon>();
            pistol.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            return pistol;
#else
            return null;
#endif
        }

        /// <summary>Aims the drawn pistol at a camera pitched by <paramref name="pitchDegrees"/> (positive looks UP) for a second of real frames.</summary>
        private IEnumerator AimAt(float pitchDegrees, float seconds)
        {
            Vector3 chest = _player.transform.position + Vector3.up * 1.4f;
            Quaternion look = Quaternion.Euler(-pitchDegrees, 0.0f, 0.0f);
            Vector3 forward = look * Vector3.forward;
            Vector3 cameraPosition = chest + Vector3.right * 0.65f - forward * 4.0f;

            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                _locomotion.Tick(new LocomotionInput
                {
                    Aiming = true,
                    GunEquipped = true,
                    YawDegrees = 0.0f,
                    AimCameraPosition = cameraPosition,
                    AimCameraForward = forward,
                }, dt);
                yield return null;
                elapsed += dt;
            }
        }

        /// <summary>
        /// The support hand's distance from the shooting hand while aiming a pistol at three
        /// pitches. A two-handed pistol grip has the palms a hand's width apart; with the arms
        /// posed separately the hands drift apart as the gun tips up or down. Logged, and
        /// asserted to stay within a hand's reach.
        /// </summary>
        [UnityTest]
        public IEnumerator BothHandsStayOnThePistolWhereverItIsAimed()
        {
#if UNITY_EDITOR
            BuildPlayer();
            PistolWeapon pistol = EquipPistol();
            Assert.IsNotNull(pistol.MuzzleFlashObject, "the pistol has no muzzle");
            yield return AimAt(0.0f, 0.6f);

            foreach (float pitch in new[] { 0.0f, 40.0f, -40.0f })
            {
                yield return AimAt(pitch, 0.9f);

                Transform right = Bone(HumanBodyBones.RightHand);
                Transform left = Bone(HumanBodyBones.LeftHand);
                Vector3 leftInGun = pistol.transform.InverseTransformPoint(left.position);
                Vector3 rightInGun = pistol.transform.InverseTransformPoint(right.position);
                float handsApart = Vector3.Distance(left.position, right.position);
                float aimError = Vector3.Angle(pistol.MuzzleFlashObject.transform.forward,
                    Quaternion.Euler(-pitch, 0.0f, 0.0f) * Vector3.forward);

                Debug.Log($"CityX pistol geometry @pitch {pitch:+0;-0;0}: pistol lossyScale {pistol.transform.lossyScale.ToString("0.000")}, " +
                          $"left hand to right hand in RIGHT-HAND space {right.InverseTransformPoint(left.position).ToString("0.000")}, " +
                          $"pistol origin in right-hand space {right.InverseTransformPoint(pistol.transform.position).ToString("0.000")}, " +
                          $"muzzle in pistol space {pistol.transform.InverseTransformPoint(pistol.MuzzleFlashObject.transform.position).ToString("0.000")}, " +
                          $"left forearm {Vector3.Distance(Bone(HumanBodyBones.LeftLowerArm).position, left.position) * 100:0}cm, " +
                          $"left upper arm {Vector3.Distance(Bone(HumanBodyBones.LeftUpperArm).position, Bone(HumanBodyBones.LeftLowerArm).position) * 100:0}cm, " +
                          $"left shoulder to right hand {Vector3.Distance(Bone(HumanBodyBones.LeftUpperArm).position, right.position) * 100:0}cm");
                Vector3 rightPalm = right.position + (right.position - Bone(HumanBodyBones.RightLowerArm).position).normalized * PlayerLocomotion.PalmOffsetMetres;
                Transform body = _player.transform;
                Debug.Log($"CityX pistol axes @pitch {pitch:+0;-0;0}: gun-space of body-left {pistol.transform.InverseTransformDirection(-body.right).ToString("0.00")}, " +
                          $"body-up {pistol.transform.InverseTransformDirection(body.up).ToString("0.00")}, " +
                          $"barrel {pistol.transform.InverseTransformDirection(pistol.MuzzleFlashObject.transform.forward).ToString("0.00")}, " +
                          $"right palm in gun space {pistol.transform.InverseTransformPoint(rightPalm).ToString("0.000")}, " +
                          $"right hand fingers dir in gun {pistol.transform.InverseTransformDirection((rightPalm - right.position).normalized).ToString("0.00")}");
                Debug.Log($"CityX pistol @pitch {pitch:+0;-0;0}: hands {handsApart * 100:0}cm apart; " +
                          $"right hand in gun space {rightInGun.ToString("0.000")}, left hand {leftInGun.ToString("0.000")}; " +
                          $"barrel {aimError:0.0} deg off the camera's pitch");
                Photograph($"pistol-pitch{pitch:+0;-0;0}-side", new Vector3(2.3f, 0.6f, 0.4f));
                Photograph($"pistol-pitch{pitch:+0;-0;0}-front", new Vector3(0.6f, 0.5f, 2.3f));

                Debug.Log($"CityX pistol support @pitch {pitch:+0;-0;0}: left palm {_locomotion.SupportHandError * 100:0.0}cm from its place on the gun");
                Assert.Less(handsApart, 0.32f,
                    $"aiming at pitch {pitch}: the support hand is {handsApart * 100:0}cm from the shooting hand -- it has come off the gun");
                Assert.Less(_locomotion.SupportHandError, 0.03f,
                    $"aiming at pitch {pitch}: the support palm is {_locomotion.SupportHandError * 100:0}cm from where it grips the gun");
            }
#else
            yield break;
#endif
        }

        // ---- the sword ----

        private SwordWeapon EquipSword()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Sword.prefab");
            Assert.IsNotNull(prefab, "Sword.prefab is missing");
            _weaponObject = Object.Instantiate(prefab, new Vector3(20.0f, 0.5f, 20.0f), Quaternion.identity);
            var sword = _weaponObject.GetComponent<SwordWeapon>();
            sword.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            return sword;
#else
            return null;
#endif
        }

        /// <summary>The distance of a point from the sword's grip: the stretch of its handle a hand can hold.</summary>
        private static float DistanceFromGrip(SwordWeapon sword, Vector3 world)
        {
            // The grip runs from the pommel (0.4m down the handle) to the guard, along the sword's own axis.
            Vector3 pommel = sword.transform.TransformPoint(new Vector3(0.0f, -0.40f, 0.0f));
            Vector3 guard = sword.transform.TransformPoint(new Vector3(0.0f, -0.04f, 0.0f));
            return DistanceToSegment(world, pommel, guard);
        }

        private IEnumerator SwordFrames(float seconds, SoulsSwordCombat combat)
        {
            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                combat.Tick(dt);
                _player.GetComponent<PlayerVitals>()?.TickStamina(false, dt);
                _locomotion.Tick(new LocomotionInput { SwordEquipped = true }, dt);
                yield return null;
                elapsed += dt;
            }
        }

        /// <summary>
        /// Both hands on the two-handed sword's grip, standing and through a swing. Measured as each
        /// hand's distance from the handle. The right hand is placed by the animation; the left is the
        /// one that drifts off it.
        /// </summary>
        [UnityTest]
        public IEnumerator BothHandsStayOnTheSwordsGripStandingAndSwinging()
        {
#if UNITY_EDITOR
            BuildPlayer();
            SwordWeapon sword = EquipSword();
            var combat = _player.GetComponent<SoulsSwordCombat>();
            Assert.IsNotNull(combat, "equipping the sword did not add the combat");
            yield return SwordFrames(0.8f, combat);

            Transform right = Bone(HumanBodyBones.RightHand);
            Transform left = Bone(HumanBodyBones.LeftHand);
            float worstLeft = 0.0f;
            float worstRight = 0.0f;

            Vector3 Palm(Transform hand, Transform forearm) =>
                hand.position + (hand.position - forearm.position).normalized * PlayerLocomotion.PalmOffsetMetres;

            void Measure(string label)
            {
                float l = DistanceFromGrip(sword, Palm(left, Bone(HumanBodyBones.LeftLowerArm)));
                float r = DistanceFromGrip(sword, Palm(right, Bone(HumanBodyBones.RightLowerArm)));
                worstLeft = Mathf.Max(worstLeft, l);
                worstRight = Mathf.Max(worstRight, r);
                Debug.Log($"CityX sword {label}: left hand {l * 100:0}cm from the grip, right hand {r * 100:0}cm; " +
                          $"left in sword space {sword.transform.InverseTransformPoint(left.position).ToString("0.000")}");
            }

            Measure("idle");
            Debug.Log($"CityX sword geometry: sword lossyScale {sword.transform.lossyScale.ToString("0.000")}, " +
                      $"player scale {_player.transform.lossyScale.ToString("0.000")}, " +
                      $"right hand in sword space {sword.transform.InverseTransformPoint(right.position).ToString("0.000")}, " +
                      $"left {sword.transform.InverseTransformPoint(left.position).ToString("0.000")}, " +
                      $"hands {Vector3.Distance(right.position, left.position) * 100:0}cm apart, " +
                      $"forearm(L) {Vector3.Distance(Bone(HumanBodyBones.LeftLowerArm).position, left.position) * 100:0}cm, " +
                      $"upper arm(L) {Vector3.Distance(Bone(HumanBodyBones.LeftUpperArm).position, Bone(HumanBodyBones.LeftLowerArm).position) * 100:0}cm, " +
                      $"pommel {sword.transform.TransformPoint(new Vector3(0, -0.40f, 0)).ToString("0.00")}, " +
                      $"hand offsets to rightHand: sword origin {(right.InverseTransformPoint(sword.transform.position)).ToString("0.000")}, " +
                      $"sword up in hand space {(right.InverseTransformDirection(sword.transform.up)).ToString("0.000")}");
            Photograph("sword-idle-side", new Vector3(2.4f, 0.3f, 0.2f));
            Photograph("sword-idle-front", new Vector3(0.5f, 0.3f, 2.4f));

            combat.RequestLight(0.0f);
            for (int i = 0; i < 14; i++)
            {
                yield return SwordFrames(0.06f, combat);
                Measure($"swing t+{(i + 1) * 0.06f:0.00}s");
                if (i == 3 || i == 6 || i == 9)
                {
                    Photograph($"sword-swing-{i}-side", new Vector3(2.6f, 0.3f, 0.2f));
                }
            }

            Assert.Less(worstLeft, 0.10f,
                $"the left hand strays {worstLeft * 100:0}cm from the sword's grip during the swing");
            Assert.Less(worstRight, 0.10f,
                $"the right hand strays {worstRight * 100:0}cm from the sword's grip during the swing");
#else
            yield break;
#endif
        }

        /// <summary>
        /// The sword is CARRIED the way a two-handed sword is between fights (owner, 2026-10-16: "my character holds ... the sword awkwardly";
        /// it used to be held out at arm's length at the shoulders, blade straight up): low at the right side, the point forward and down, the
        /// cutting edge down, the hilt between the fists with the right hand at the guard and the left below it, both hands on the handle.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSwordIsCarriedLowWithItsEdgeDownAndBothHandsOnTheHilt()
        {
#if UNITY_EDITOR
            BuildPlayer();
            SwordWeapon sword = EquipSword();
            var combat = _player.GetComponent<SoulsSwordCombat>();
            yield return SwordFrames(0.8f, combat);

            Vector3 blade = sword.transform.up;
            Vector3 right = Bone(HumanBodyBones.RightHand).position;
            Vector3 left = Bone(HumanBodyBones.LeftHand).position;
            Vector3 handLine = (right - left).normalized;
            Vector3 shoulder = Bone(HumanBodyBones.RightUpperArm).position;

            Debug.Log($"CityX sword carried: blade {blade.ToString("0.00")}, edge {sword.EdgeDirectionWorld.ToString("0.00")}, " +
                      $"blade vs the line between the hands {Vector3.Angle(blade, handLine):0} deg, hands {shoulder.y - right.y:0.00} m below the shoulder");
            Assert.Less(blade.y, -0.2f, $"the carried blade should point down, not {blade.ToString("0.00")}");
            Assert.Greater(Vector3.Dot(blade, _player.transform.forward), 0.5f, "the carried blade should point ahead");
            Assert.Less(sword.EdgeDirectionWorld.y, -0.5f, "the cutting edge should face down");
            Assert.Greater(shoulder.y - right.y, 0.4f, "the hands should be low at the side, not up at the shoulders");
            Assert.Less(Vector3.Angle(blade, handLine), 40.0f, "the blade should run on from the lower hand through the upper");
            Assert.Less(DistanceFromGrip(sword, right), 0.08f, "the right hand is off the hilt");
            Assert.Less(_locomotion.SupportHandError, 0.05f, $"the left palm is {_locomotion.SupportHandError * 100:0} cm from its grip point");
#else
            yield break;
#endif
        }

        /// <summary>
        /// The torso holds still for walking and standing (the locomotion clips' hips sway is what that
        /// lock is for), is turned by the swing's own pose through a committed swing (where the twist IS the
        /// swing: SwordStrikes, the hips leading), settles into the ready stance's small turn after it, and is
        /// back straight once the sword is only carried again. It never follows the sword idle clip's stooped
        /// spine (owner, 2026-10-16: no awkward turns).
        /// </summary>
        [UnityTest]
        public IEnumerator TheTorsoStandsStillUntilASwingAndThenFollowsThePose()
        {
#if UNITY_EDITOR
            BuildPlayer();
            EquipSword();
            var combat = _player.GetComponent<SoulsSwordCombat>();
            combat.HitStopSeconds = 0.0f;
            Transform spine = Bone(HumanBodyBones.Spine);
            yield return SwordFrames(0.8f, combat);

            Assert.IsFalse(_locomotion.TorsoFollowsClip, "standing with a sword should keep the torso locked");
            Assert.Less(Quaternion.Angle(spine.localRotation, Quaternion.identity), 0.5f, "the spine should be at identity standing");

            combat.RequestLight(0.0f);
            float mostLean = 0.0f;
            for (int i = 0; i < 12; i++)
            {
                yield return SwordFrames(0.05f, combat);
                mostLean = Mathf.Max(mostLean, Quaternion.Angle(spine.localRotation, Quaternion.identity));
            }
            Assert.IsTrue(combat.IsCommitted || mostLean > 0.0f);
            Debug.Log($"CityX torso: most the spine turned during a light swing {mostLean:0.0} deg");
            Assert.Greater(mostLean, 5.0f, "the swing never used the torso");
            Assert.IsFalse(_locomotion.TorsoFollowsClip, "a posed swing turns the torso itself: it must not also follow the sword idle clip's stoop");

            yield return SwordFrames(1.5f, combat);
            Assert.IsFalse(_locomotion.TorsoFollowsClip, "the torso should be released once the swing is over");
            Assert.Less(Quaternion.Angle(spine.localRotation, Quaternion.identity), 8.0f, "after the swing the spine should be in the ready stance's small turn");
            yield return SwordFrames(SwordPoseDriver.ReadyLingerSeconds + SwordPoseDriver.ReadyToCarrySeconds + 0.3f, combat);
            Assert.Less(Quaternion.Angle(spine.localRotation, Quaternion.identity), 0.5f, "the spine should be straight again once the sword is only carried");
#else
            yield break;
#endif
        }

        /// <summary>A carried pistol has a free hand: it only comes up to steady the gun once the gun is raised to aim.</summary>
        [UnityTest]
        public IEnumerator TheFreeHandStaysFreeWhileThePistolIsOnlyCarried()
        {
#if UNITY_EDITOR
            BuildPlayer();
            PistolWeapon pistol = EquipPistol();
            for (int i = 0; i < 40; i++)
            {
                _locomotion.Tick(new LocomotionInput { GunEquipped = true }, Time.deltaTime);
                yield return null;
            }
            float carried = _locomotion.SupportHandError;
            Debug.Log($"CityX pistol carried: left palm {carried * 100:0}cm from the grip point");
            Assert.Greater(carried, 0.25f, "the free hand should not be clamped to the gun while it is only carried");

            yield return AimAt(0.0f, 0.8f);
            Assert.Less(_locomotion.SupportHandError, 0.06f, "raising the gun should bring the free hand onto it");

            yield return null;
            Assert.IsNotNull(pistol);
#else
            yield break;
#endif
        }

        /// <summary>Putting a weapon away lets go of it: nothing keeps reaching for a sword that is gone.</summary>
        [UnityTest]
        public IEnumerator PuttingTheSwordAwayReleasesTheSupportHand()
        {
#if UNITY_EDITOR
            BuildPlayer();
            SwordWeapon sword = EquipSword();
            var combat = _player.GetComponent<SoulsSwordCombat>();
            yield return SwordFrames(0.6f, combat);
            Assert.Less(_locomotion.SupportHandError, 0.06f, "the support hand should be on the sword before it is put away");

            sword.OnUnequip(_player);
            _weaponObject = null;   // destroyed by OnUnequip
            for (int i = 0; i < 20; i++)
            {
                _locomotion.Tick(new LocomotionInput(), Time.deltaTime);
                yield return null;
            }

            Assert.AreEqual(0.0f, _locomotion.SupportHandError, 1e-6f, "the support hand is still reaching for a sword that was put away");
#else
            yield break;
#endif
        }
    }
}
