using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// ShouldCapture/Tick are driven directly with an explicit talkKeyHeld
    /// bool, not through Keyboard.current -- same reasoning
    /// PistolWeaponPlayTests' own class doc comment gives for TryPickup's
    /// ePressed flag: "Keyboard.current (null outside a real Input System
    /// device -- nothing in this project's tests exercises it)".
    ///
    /// NEEDS THE REAL Player.prefab, NOT A BARE GameObject -- see
    /// PlayerLocomotionHipsPlayTests's own comment on why; there is no
    /// PlayerVoiceCapture on a bare GameObject the way PlayerRigBuilder
    /// actually attaches it.
    /// </summary>
    public sealed class PlayerVoiceCapturePlayTests
    {
        private GameObject _player;
        private PlayerVoiceCapture _capture;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
        }

        private bool Spawn()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab);
            _capture = _player.GetComponent<PlayerVoiceCapture>();
            Assert.IsNotNull(_capture, "Player.prefab has no PlayerVoiceCapture");
            return true;
#else
            return false;
#endif
        }

        [Test]
        public void PushToTalkOnlyCapturesWhileTheTalkKeyIsHeld()
        {
            Assert.IsFalse(PlayerVoiceCapture.ShouldCapture(pushToTalk: true, talkKeyHeld: false));
            Assert.IsTrue(PlayerVoiceCapture.ShouldCapture(pushToTalk: true, talkKeyHeld: true));
        }

        [Test]
        public void OpenMicCapturesRegardlessOfTheTalkKey()
        {
            Assert.IsTrue(PlayerVoiceCapture.ShouldCapture(pushToTalk: false, talkKeyHeld: false));
            Assert.IsTrue(PlayerVoiceCapture.ShouldCapture(pushToTalk: false, talkKeyHeld: true));
        }

        [UnityTest]
        public IEnumerator TickNeverThrowsWithNoMicrophoneAttached()
        {
            if (!Spawn())
            {
                Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
                yield break;
            }

            // This headless test runner has no real microphone hardware --
            // the point of this test is that Tick tolerates that silently,
            // the same "nothing to play/nothing to catch" shape
            // PlayerAudio's own PlayFallImpact/PlayDash already apply to a
            // null AudioClip, rather than throwing.
            Assert.DoesNotThrow(() => _capture.Tick(true));
            Assert.DoesNotThrow(() => _capture.Tick(false));
            yield return null;

            if (Microphone.devices.Length == 0)
            {
                Assert.IsFalse(_capture.IsCapturing,
                    "should not report capturing with no microphone device available");
            }
        }
    }
}
