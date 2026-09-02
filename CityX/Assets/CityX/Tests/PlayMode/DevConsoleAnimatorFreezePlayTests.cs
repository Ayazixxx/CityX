using System.Collections;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT: "just imagine the character immediately
    /// glitches out... upon opening menu and grapple swinging."
    /// Root-caused directly with a diagnostic across a freshly
    /// instantiated rig: an Animator that has never actually evaluated a
    /// single real frame yet sits at the raw imported BIND POSE (a stark,
    /// all-zero-rotation T-pose) and only snaps to a real pose on its own
    /// first Update. Opening the console the instant control is gained --
    /// spawning in and immediately opening it to gear up is a completely
    /// normal first action, not an edge case -- disabled the Animator
    /// before it ever got that first Update, freezing the character at
    /// the bind pose for as long as the console stayed open.
    ///
    /// NEEDS THE REAL Player.prefab, not DevConsoleViewTests' own bare
    /// Animator fixture -- a bare Animator with no controller and no
    /// mapped Humanoid bones has nothing to evaluate into a "real" pose in
    /// the first place, so it can never distinguish a bind-pose bug from
    /// correct behaviour.
    /// </summary>
    public sealed class DevConsoleAnimatorFreezePlayTests
    {
        private GameObject _player;
        private GameObject _consoleObject;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_consoleObject != null) Object.Destroy(_consoleObject);
        }

        [UnityTest]
        public IEnumerator OpeningTheConsoleTheInstantAfterSpawnDoesNotFreezeTheRawBindPose()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 20.0f, 0.0f), Quaternion.identity);
            Animator animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "Player.prefab has no Animator");
            PlayerVitals vitals = _player.GetComponent<PlayerVitals>();
            vitals.Revive();

            Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);

            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Vitals = vitals;
            view.Bind();

            // NO yield HERE -- zero real frames, zero real Animator
            // evaluations, matching the exact repro: open the console the
            // instant the rig exists.
            view.Open();

            Assert.Greater(
                Quaternion.Angle(Quaternion.identity, rightUpperArm.localRotation) +
                Quaternion.Angle(Quaternion.identity, leftUpperArm.localRotation), 1.0f,
                "the console froze the raw all-zero bind pose -- the character is stuck in a T-pose " +
                "instead of a real evaluated one");
            yield break;
#else
            yield break;
#endif
        }
    }
}
