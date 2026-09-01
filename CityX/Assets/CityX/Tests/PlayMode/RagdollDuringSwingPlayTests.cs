using System.Collections;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    internal sealed class RagdollDuringSwingFixedAnchorLauncher : IGrappleLauncher
    {
        private readonly Vector3 _anchor;
        public RagdollDuringSwingFixedAnchorLauncher(Vector3 anchor) { _anchor = anchor; }
        public bool FindAnchor(Vector3 origin, Vector3 direction, float maxRange, out Vector3 anchor)
        {
            anchor = _anchor;
            return true;
        }
        public void ShowRope(Vector3 anchor) { }
        public void UpdateRope(Vector3 anchor) { }
        public void HideRope() { }
    }

    /// <summary>
    /// EXPLICIT BUG REPORT: "the ragdolled body with arm attached to
    /// grapple gun fully [stretched]". A melee finisher landing mid-swing
    /// (PlayerVitals.TriggerFinisherKnockdown) or dying mid-swing both call
    /// Ragdoll.Activate() while GrappleSwing.IsActive can still be true --
    /// neither used to know about the other. Left alone, GrappleSwing's own
    /// Tick (called every frame by PlayerInputReader, which nothing in
    /// Ragdoll.Activate ever disables) keeps writing the SAME bones'
    /// localRotation directly, while Activate hands those same bones to
    /// real, non-kinematic Rigidbodies and joints in the same frame -- a
    /// kinematic transform write and a live physics solver fighting over
    /// one skeleton, which is exactly what stretches a ragdoll's joints to
    /// their limit instead of letting it settle. See Ragdoll.Activate's own
    /// doc comment for the fix.
    /// </summary>
    public sealed class RagdollDuringSwingPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                // Same "Destroying assets is not permitted" / RenderTexture
                // noise every other real-scene PlayMode test in this file
                // group already tolerates -- see GrappleSwingRealInputPlayTests'
                // own doc comment.
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RagdollingWhileSwingingReleasesTheSwingBeforeBonesGoDynamic()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var swing = Object.FindFirstObjectByType<GrappleSwing>();
            var console = Object.FindFirstObjectByType<DevConsoleView>();
            Assert.IsNotNull(swing, "no GrappleSwing in the real scene");
            var ragdoll = console.Vitals.GetComponent<Ragdoll>();
            Assert.IsNotNull(ragdoll, "no Ragdoll on the real player");

            Vector3 anchor = swing.transform.position + new Vector3(15.0f, 25.0f, 10.0f);
            swing.EquippedHook = new RagdollDuringSwingFixedAnchorLauncher(anchor);

            Press(_mouse.leftButton);
            for (int i = 0; i < 5 && !swing.IsActive; i++) { yield return null; }
            Assert.IsTrue(swing.IsActive, "the grapple never attached");

            // Short pre-trigger window, and bailing out of the loop the
            // instant IsActive flips -- a real swing can auto-release on
            // its own (see GrappleSwing.AttachGraceSeconds), and this test
            // needs to catch it still genuinely attached, not after it has
            // already let go on its own.
            Press(_keyboard.wKey);
            for (int i = 0; i < 20 && swing.IsActive; i++) { yield return null; }
            Assert.IsTrue(swing.IsActive, "the swing auto-released before the ragdoll trigger could even fire");

            // The same trigger PlayerVitals.TriggerFinisherKnockdown uses --
            // a finisher (or death) landing while the rope is still out.
            ragdoll.Activate(new Vector3(3.0f, 4.0f, 0.0f));
            Release(_keyboard.wKey);

            Assert.IsFalse(swing.IsActive,
                "GrappleSwing did not release when Ragdoll activated mid-swing -- " +
                "the two systems are back to fighting over the same bones");

            // Let the rig actually settle for a while -- confirms nothing
            // throws and the ragdoll behaves like an ordinary one once the
            // swing is out of the way, rather than exploding from the two
            // systems' conflicting writes.
            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }
            Assert.IsTrue(ragdoll.IsLimp, "the ragdoll should still be limp 60 frames later");
#else
            yield break;
#endif
        }
    }
}
