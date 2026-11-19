using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE PARACHUTE ON THE REAL BODY (owner, 2026-11-19; docs/PARACHUTE.md), driven as PlayerInputReader drives it (the parachute ticked with
    /// Space and the keys, then the body): a jump with the pack on stays a jump; a long drop spreads the body out face down, Space opens the
    /// canopy over the shoulders and it flies down hanging under it; a flared landing hurts no one, a dive into the ground knocks the body down;
    /// another player's copy shows the same jump from the words it is sent.
    /// </summary>
    public sealed class ParachuteFallPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            _spawned.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

#if UNITY_EDITOR
        private GameObject Spawn(string path, Vector3 at)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is missing -- rebuild the scene (ParachuteBuilder)");
            GameObject go = Object.Instantiate(prefab, at, Quaternion.identity);
            _spawned.Add(go);
            return go;
        }

        private GameObject Body(Vector3 at)
        {
            GameObject body = Spawn("Assets/CityX/Character/Player.prefab", at);
            body.GetComponent<PlayerInputReader>().enabled = false;
            return body;
        }

        private IEnumerator Ground(float size = 400.0f)
        {
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(size, 1, size);
            _spawned.Add(ground);
            yield return null;
        }

        private (GameObject body, Parachute chute) Wearing(Vector3 at)
        {
            GameObject body = Body(at);
            var chute = Spawn("Assets/CityX/Weapons/Parachute.prefab", at + Vector3.right * 30.0f).GetComponent<Parachute>();
            chute.OnEquip(body, ItemInstanceData.None);
            return (body, chute);
        }
#endif

        /// <summary>One frame as the input reader runs it: the parachute, then the body (given no keys while the parachute has it).</summary>
        private static IEnumerator Frames(Parachute chute, PlayerLocomotion body, float seconds, Vector2 keys = default, bool spaceFirst = false, bool jumpFirst = false)
        {
            bool first = true;
            for (float t = 0.0f; t < seconds; t += Dt)
            {
                chute.Tick(first && spaceFirst, keys, Dt);
                body.Tick(new LocomotionInput { Move = chute.DrivesBody ? Vector2.zero : keys, Jump = first && jumpFirst }, Dt);
                first = false;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AJumpWithThePackOnStaysAJump()
        {
#if UNITY_EDITOR
            yield return Ground();
            (GameObject body, Parachute chute) = Wearing(new Vector3(0, 0.05f, 0));
            var locomotion = body.GetComponent<PlayerLocomotion>();
            yield return Frames(chute, locomotion, 0.3f);
            Assert.IsTrue(locomotion.IsGrounded, "setup: the body is not standing");
            yield return Frames(chute, locomotion, 0.2f, jumpFirst: true);
            Assert.IsFalse(locomotion.IsGrounded, "setup: the body did not jump");
            for (int i = 0; i < 6 && !locomotion.IsGrounded; i++)
            {
                yield return Frames(chute, locomotion, 0.1f, spaceFirst: true);
                Assert.IsFalse(chute.DrivesBody, "Space in a jump's fall opened the parachute");
            }
            yield return Frames(chute, locomotion, 1.0f);
            Assert.IsTrue(locomotion.IsGrounded, "the jump never came down");
            Assert.IsFalse(chute.Canopy.Showing);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator DroppedFromHighItSpreadsOutThenOpensOnSpaceAndFliesDownHangingUnderTheCanopy()
        {
#if UNITY_EDITOR
            yield return Ground();
            (GameObject body, Parachute chute) = Wearing(new Vector3(0, 160.0f, 0));
            var locomotion = body.GetComponent<PlayerLocomotion>();
            Animator animator = body.GetComponentInChildren<Animator>();
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);

            yield return Frames(chute, locomotion, 1.5f, spaceFirst: true);
            Assert.AreEqual(ChuteState.Packed, chute.Flight.State, "it opened after a second and a half of falling");
            yield return Frames(chute, locomotion, 1.2f);
            Assert.AreEqual(ChuteState.Freefall, chute.Flight.State, "two seconds of falling did not spread the body out");
            yield return Frames(chute, locomotion, 0.4f, new Vector2(0.0f, 0.0f));
            TestContext.WriteLine($"freefall: head {head.position.y - hips.position.y:0.00} m over the hips, falling {-locomotion.Velocity.y:0.0} m/s");
            Assert.Less(Mathf.Abs(head.position.y - hips.position.y), 0.6f, "falling, the body is not laid out flat");

            yield return Frames(chute, locomotion, ParachuteFlight.OpeningSeconds + 0.3f, spaceFirst: true);
            Assert.AreEqual(ChuteState.Open, chute.Flight.State, "Space did not open it");
            yield return Frames(chute, locomotion, 1.5f);
            Renderer canopy = chute.Canopy.GetComponent<Renderer>();
            Assert.IsTrue(chute.Canopy.Showing && canopy.enabled, "no canopy shows over the body");
            float above = canopy.bounds.center.y - head.position.y;
            TestContext.WriteLine($"open: canopy {above:0.00} m over the head, head {head.position.y - hips.position.y:0.00} m over the hips, " +
                                  $"velocity {locomotion.Velocity}");
            Assert.That(above, Is.InRange(2.0f, 5.5f), "the canopy is not over the head");
            Assert.Less(Vector2.Distance(new Vector2(canopy.bounds.center.x, canopy.bounds.center.z), new Vector2(head.position.x, head.position.z)), 1.5f,
                "the canopy is not over the body");
            Assert.Greater(head.position.y - hips.position.y, 0.8f, "under the canopy the body does not hang upright");
            Assert.That(-locomotion.Velocity.y, Is.InRange(2.5f, 5.5f), "it does not sink as a canopy does");
            Assert.That(new Vector2(locomotion.Velocity.x, locomotion.Velocity.z).magnitude, Is.InRange(8.0f, 14.0f), "it does not glide forward");

            float heading = body.transform.eulerAngles.y;
            yield return Frames(chute, locomotion, 1.0f, new Vector2(-1.0f, 0.0f));
            Assert.Less(Mathf.DeltaAngle(heading, body.transform.eulerAngles.y), -25.0f, "A does not turn it left");
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand), rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.Less(leftHand.position.y, rightHand.position.y - 0.3f, "turning left, the left toggle is not pulled down");
#else
            yield break;
#endif
        }

        private IEnumerator DownUnderTheCanopy(Parachute chute, PlayerLocomotion locomotion, float height)
        {
            yield return Frames(chute, locomotion, ParachuteFlight.ReadySeconds + 0.2f);
            yield return Frames(chute, locomotion, ParachuteFlight.OpeningSeconds + 0.3f, spaceFirst: true);
            Assert.AreEqual(ChuteState.Open, chute.Flight.State);
            for (float t = 0.0f; locomotion.transform.position.y > height && t < 30.0f; t += Dt)
            {
                yield return Frames(chute, locomotion, Dt);
            }
        }

        [UnityTest]
        public IEnumerator AFlaredLandingHurtsNoOne()
        {
#if UNITY_EDITOR
            yield return Ground();
            (GameObject body, Parachute chute) = Wearing(new Vector3(0, 70.0f, 0));
            var locomotion = body.GetComponent<PlayerLocomotion>();
            var vitals = body.GetComponent<PlayerVitals>();
            float before = vitals.Health;
            yield return DownUnderTheCanopy(chute, locomotion, 2.5f);
            for (float t = 0.0f; chute.DrivesBody && t < 5.0f; t += Dt)
            {
                yield return Frames(chute, locomotion, Dt, new Vector2(0.0f, -1.0f));
            }
            Assert.IsFalse(chute.DrivesBody, "it never landed");
            Assert.AreEqual(ChuteLanding.Soft, chute.Flight.Landing, $"flared, it landed sinking at {chute.Flight.LandingSink:0.0} m/s");
            yield return Frames(chute, locomotion, 0.5f);
            Assert.AreEqual(before, vitals.Health, 0.01f, "a flared landing hurt");
            Assert.IsFalse(body.GetComponent<Ragdoll>().IsLimp, "a flared landing knocked the body down");
            Assert.IsTrue(locomotion.IsGrounded);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ADiveIntoTheGroundKnocksTheBodyDownAndHurts()
        {
#if UNITY_EDITOR
            yield return Ground();
            (GameObject body, Parachute chute) = Wearing(new Vector3(0, 70.0f, 0));
            var locomotion = body.GetComponent<PlayerLocomotion>();
            var vitals = body.GetComponent<PlayerVitals>();
            float before = vitals.Health;
            yield return DownUnderTheCanopy(chute, locomotion, 12.0f);
            for (float t = 0.0f; chute.DrivesBody && t < 5.0f; t += Dt)
            {
                yield return Frames(chute, locomotion, Dt, new Vector2(0.0f, 1.0f));
            }
            Assert.AreEqual(ChuteLanding.Hard, chute.Flight.Landing, $"diving, it landed sinking at only {chute.Flight.LandingSink:0.0} m/s");
            yield return null;
            Assert.Less(vitals.Health, before, "a dive into the ground did not hurt");
            Assert.IsTrue(body.GetComponent<Ragdoll>().IsLimp, "a dive into the ground did not knock the body down");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyShowsTheJumpFromTheWordsItIsSent()
        {
#if UNITY_EDITOR
            yield return Ground();
            GameObject copy = Body(new Vector3(0, 0.05f, 0));
            var model = Spawn("Assets/CityX/Weapons/Parachute.prefab", new Vector3(40, 1, 40)).GetComponent<Parachute>();
            model.MountCosmetic(copy);
            foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                b.enabled = false;
            }
            ParachuteRider rider = ParachuteRider.On(copy);
            var told = new List<AbilityEvent>();
            ((IReplicatedAbility)rider).Fired += e => told.Add(e);
            Animator animator = copy.GetComponentInChildren<Animator>();
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);

            rider.PlayRemote(Word(ChuteState.Freefall, 0));
            for (int i = 0; i < 30; i++) yield return null;
            Assert.Less(Mathf.Abs(head.position.y - hips.position.y), 0.6f, "the copy is not laid out falling");
            rider.PlayRemote(Word(ChuteState.Opening, 0));
            for (float t = 0.0f; t < ParachuteFlight.OpeningSeconds + 0.2f; t += Time.deltaTime) yield return null;
            rider.PlayRemote(Word(ChuteState.Open, ParachuteRider.KeyA));
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsTrue(model.Canopy.Showing, "the copy shows no canopy");
            Assert.Greater(model.Canopy.GetComponent<Renderer>().bounds.center.y, head.position.y + 2.0f, "the copy's canopy is not over its head");
            Assert.Less(animator.GetBoneTransform(HumanBodyBones.LeftHand).position.y, animator.GetBoneTransform(HumanBodyBones.RightHand).position.y - 0.3f,
                "the copy's left toggle is not pulled for the left turn it was told of");

            rider.PlayRemote(new AbilityEvent(ParachuteRider.StateChanged, copy.transform.position, Vector3.forward, 0.0f, ParachuteRider.Landed, (int)ChuteState.Packed));
            for (float t = 0.0f; t < ParachuteRider.CollapseSeconds + 0.3f; t += Time.deltaTime) yield return null;
            Assert.IsFalse(model.Canopy.Showing, "the copy's canopy stayed after it landed");
            Assert.Greater(head.position.y - hips.position.y, 0.8f, "landed, the copy is not standing");
            Assert.IsEmpty(told, "a copy told other players about a jump");
#else
            yield break;
#endif
        }

        private static AbilityEvent Word(ChuteState state, int keys) =>
            new AbilityEvent(ParachuteRider.StateChanged, Vector3.zero, Vector3.forward, 0.0f, ParachuteRider.Landed, (int)state + 4 * keys);
    }
}
