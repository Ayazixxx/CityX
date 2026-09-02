using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The hanging ragdoll dummy near spawn -- see HangingRagdollDummy's
    /// own class doc comment for why it exists: a live, walkable-up-to
    /// reference for whether Ragdoll.cs itself is stable, built directly
    /// into the game rather than only checkable through a diagnostic
    /// render. What matters here is the same thing RagdollPlayTests
    /// already checks for the death case -- it does not explode on its own
    /// first step and it actually comes to rest.
    ///
    /// BUILDS THE SETUP DIRECTLY, NOT THROUGH HangingRagdollDummyBuilder --
    /// same reason RagdollPlayTests' own stand-in rig avoids RagdollBuilder:
    /// the builder lives in an Editor-only assembly
    /// (CityX.CharacterEditor), and CityX.Tests.PlayMode deliberately
    /// carries no reference to any *Editor assembly. Uses the real
    /// Civilian.prefab (the same body the builder itself uses) rather than
    /// a synthetic stand-in, since what is actually under test here is the
    /// real rig plus one extra joint, not Ragdoll.cs's own switching logic
    /// in isolation (RagdollPlayTests already owns that).
    /// </summary>
    public sealed class HangingRagdollDummyPlayTests
    {
        private GameObject _player;
        private GameObject _ground;
        private GameObject _anchor;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
            if (_anchor != null) Object.Destroy(_anchor);
        }

        [UnityTest]
        public IEnumerator TheDummySettlesIntoARestingHangWithoutExploding()
        {
#if UNITY_EDITOR
            GameObject civilianPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Civilian.prefab");
            Assert.IsNotNull(civilianPrefab, "Civilian.prefab is missing");

            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(20.0f, 1.0f, 20.0f);

            _player = Object.Instantiate(civilianPrefab, new Vector3(0.3f, 0.0f, 0.0f), Quaternion.identity);
            var civilian = _player.GetComponent<Civilian>();
            if (civilian != null) civilian.enabled = false;
            var controller = _player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            Physics.SyncTransforms();

            Ragdoll ragdoll = _player.GetComponent<Ragdoll>();
            Assert.IsNotNull(ragdoll, "Civilian.prefab has no Ragdoll -- rebuild it first");
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Rigidbody handBody = rightLowerArm.GetComponent<Rigidbody>();
            Assert.IsNotNull(handBody, "the right lower arm has no rigidbody -- nothing to hang");

            _anchor = new GameObject("Anchor");
            _anchor.transform.position = new Vector3(0.0f, 3.4f, 0.0f);

            ragdoll.Activate(Vector3.zero);

            SpringJoint joint = handBody.gameObject.AddComponent<SpringJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = handBody.transform.InverseTransformPoint(rightHand.position);
            joint.connectedBody = null;
            joint.connectedAnchor = _anchor.transform.position;
            joint.minDistance = 0.0f;
            joint.maxDistance = 0.35f;
            joint.spring = 4000.0f;
            joint.damper = 120.0f;
            joint.tolerance = 0.01f;
            joint.enableCollision = false;

            Rigidbody hipBody = ragdoll.Torso;
            float worstSpeed = 0.0f;
            // WaitForFixedUpdate, NOT yield return null -- batch mode runs
            // render frames uncapped (confirmed directly: this whole test
            // completed in 52ms of real wall-clock time on a first pass),
            // so "yield return null 180 times" gave real PhysX physics
            // almost no real time to actually settle in, and the test
            // measured a still-mid-fall velocity as if it were a failure.
            // Real Rigidbody physics only advances on Unity's own internal
            // FixedUpdate schedule (driven by actual elapsed wall-clock
            // time), unlike PlayerLocomotion.Tick's explicit fixed-step
            // parameter -- WaitForFixedUpdate ties this loop to exactly
            // that schedule instead of however fast rendering happens to
            // run.
            for (int i = 0; i < 180; i++)
            {
                yield return new WaitForFixedUpdate();
                worstSpeed = Mathf.Max(worstSpeed, hipBody.linearVelocity.magnitude);
            }

            Assert.Less(worstSpeed, 15.0f,
                $"the hanging dummy's hip reached {worstSpeed:0.0} m/s -- it exploded instead of settling");
            Assert.Less(hipBody.linearVelocity.magnitude, 1.0f,
                $"the dummy never came to rest -- still moving at {hipBody.linearVelocity.magnitude:0.0} m/s " +
                "after 3 real seconds");
#else
            yield break;
#endif
        }
    }
}
