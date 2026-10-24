using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// What a sword swing SHOWS AND SOUNDS LIKE (2026-10-23): the blade trails light while, and only while, it is live (no wind-up smear), and the
    /// swing is heard as it leaves the hold -- on the body that swings it, and on a copy of someone else's body that is only told the attack's name.
    /// </summary>
    public sealed class SwordSwingShowPlayTests
    {
        private GameObject _player;
        private GameObject _sword;
        private GameObject _ground;
        private PlayerLocomotion _locomotion;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_sword != null) Object.Destroy(_sword);
            if (_ground != null) Object.Destroy(_ground);
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                Object.Destroy(source.gameObject);   // the one-shots PlayClipAtPoint leaves behind
            }
        }

        /// <summary>The real player with the two-handed spike sword in its hands and its input switched off.</summary>
        private SoulsSwordCombat Build()
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

            var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/SpikeSword.prefab");
            Assert.IsNotNull(swordPrefab, "SpikeSword.prefab is missing");
            _sword = Object.Instantiate(swordPrefab, new Vector3(20.0f, 0.5f, 20.0f), Quaternion.identity);
            _sword.GetComponent<SwordWeapon>().OnEquip(_player, CityX.Items.ItemInstanceData.None);

            var melee = _player.GetComponent<MeleeAttacker>();
            Assert.IsNotNull(melee != null ? melee.SpinSlashMaterial : null, "the streak material is not on the player: nothing to draw the trail in");
            return _player.GetComponent<SoulsSwordCombat>();
#else
            return null;
#endif
        }

        private static bool IsHeard(AudioClip clip)
        {
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (source.clip == clip)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Runs frames while <paramref name="going"/> holds, noting whether the trail lit while the blade was live, while it was not, and whether the swish played.</summary>
        private IEnumerator Watch(SwordPoseDriver driver, SoulsSwordCombat combat, System.Func<bool> going, bool heavy, System.Action<bool, bool, bool> report)
        {
            bool litWhileLive = false, litWhileDead = false, heard = false;
            float elapsed = 0.0f;
            while (going() && elapsed < 4.0f)
            {
                float dt = Time.deltaTime;
                combat?.Tick(dt);
                _locomotion.Tick(new LocomotionInput { SwordEquipped = true }, dt);
                yield return null;
                elapsed += dt;
                bool emitting = driver.Trail != null && driver.Trail.IsEmitting;
                if (driver.BladeIsLive)
                {
                    litWhileLive |= emitting;
                }
                else
                {
                    litWhileDead |= emitting;
                }
                heard |= IsHeard(SwingWhoosh.Clip(heavy));
            }
            report(litWhileLive, litWhileDead, heard);
        }

        [UnityTest]
        public IEnumerator TheBladeTrailsLightOnlyWhileItIsLiveAndTheSwingIsHeard()
        {
#if UNITY_EDITOR
            SoulsSwordCombat combat = Build();
            combat.HitStopSeconds = 0.0f;
            var driver = _player.GetComponent<SwordPoseDriver>();
            Assert.IsNotNull(driver);
            yield return Watch(driver, combat, () => false, false, (_, __, ___) => { });
            yield return null;

            combat.RequestLight(0.0f);
            Assert.AreEqual(SoulsPhase.Startup, combat.Phase);
            bool live = false, dead = false, heard = false;
            yield return Watch(driver, combat, () => combat.Phase != SoulsPhase.Idle, false, (l, d, h) => { live = l; dead = d; heard = h; });
            Assert.IsTrue(live, "the blade trailed no light while it was live");
            Assert.IsFalse(dead, "the blade trailed light outside its live window: a wind-up smear");
            Assert.IsTrue(heard, "the swing made no sound");
            Assert.IsFalse(driver.Trail.IsEmitting, "the trail is still being drawn after the swing");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ACopyOfTheBodyShowsAndSoundsTheSameFromTheAttacksNameAlone()
        {
#if UNITY_EDITOR
            Build();   // the sword mounted in the hands; no combat is ever ticked, as on another player's copy
            var animation = _player.GetComponent<PlayerAnimationDriver>();
            var driver = _player.GetComponent<SwordPoseDriver>();
            yield return null;

            animation.PlayRemoteState(SwordStrikes.NameOf(SwordStrikeKind.Slam2H));
            Assert.IsTrue(driver.IsStriking, "the copy did not start the slam from its name");
            Assert.AreEqual(SwordStrikeKind.Slam2H, driver.Kind);
            bool live = false, dead = false, heard = false;
            yield return Watch(driver, null, () => driver.IsStriking, true, (l, d, h) => { live = l; dead = d; heard = h; });
            Assert.IsTrue(live, "the copy's blade trailed no light while it was live");
            Assert.IsFalse(dead, "the copy's blade trailed light outside its live window");
            Assert.IsTrue(heard, "the copy's swing made no sound (the heavy's swish)");
#else
            yield break;
#endif
        }
    }
}
