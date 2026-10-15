using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// GROW AND SHRINK ON THE REAL BODY (owner, 2026-10-15: "10x as big" and "10x as small", 30 s each, in the Wizard tree). Key 0 grows the body
    /// to ten times its size, capsule and all; a giant walks 3.2 times as fast in strides that take 3.2 times as long, steps up onto a car,
    /// and is framed from ten times as far; under a low roof it grows only as far as there is room; Shrink takes it to a tenth, its skin and step
    /// in proportion (no error from the controller), and the same key again brings it back.
    /// </summary>
    public sealed class BodySizePlayTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private GameObject _ground;
        private GameObject _player;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            base.TearDown();
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private BodySizeAbility Stage()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(600.0f, 1.0f, 600.0f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            // The abilities go on the scene's body and every joining player's (PlayerAbilityWiring), not on Player.prefab itself.
            BodySizeAbility size = _player.GetComponent<BodySizeAbility>();
            return size != null ? size : _player.AddComponent<BodySizeAbility>();   // not ??: a missing component is a fake null in the editor
#else
            return null;
#endif
        }

        private IEnumerator Tap(KeyControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        /// <summary>Holds W for a while and gives the speed the body settled at (the reader turns the body toward the camera-less yaw: straight on).</summary>
        private IEnumerator Walk(int frames, System.Action<float> speed)
        {
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            Press(_keyboard.wKey);
            yield return Frames(frames);
            speed(locomotion.CurrentSpeed);
            Release(_keyboard.wKey);
            yield return Frames(20);
        }

        [UnityTest]
        public IEnumerator KeyZeroGrowsTheBodyTenTimesAGiantWalksFasterInSlowerStridesAndTheKeyAgainBringsItBack()
        {
            BodySizeAbility size = Stage();
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            var controller = _player.GetComponent<CharacterController>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            yield return Frames(30);
            float normal = 0.0f;
            yield return Walk(60, s => normal = s);
            Assert.Greater(normal, 3.0f, "the body did not walk at all");

            yield return Tap(_keyboard.digit0Key);
            Assert.AreEqual(BodySize.Spell.Grow, size.Size.Active, "0 did not cast Grow (it is on key 0 out of the box)");
            yield return Frames(Mathf.CeilToInt(BodySize.ChangeSeconds * 60.0f) + 10);
            Assert.AreEqual(10.0f, _player.transform.localScale.y, 0.05f, "Grow is ten times as big");
            Assert.AreEqual(26.0f, controller.bounds.size.y, 1.5f, "the capsule it walks in did not grow with it");
            Assert.AreEqual(1.0f / Mathf.Sqrt(10.0f), animator.GetFloat(BodySizeAbility.StrideRateName), 0.01f, "its stride is not paced to its size");

            float giant = 0.0f;
            yield return Walk(90, s => giant = s);
            Assert.AreEqual(Mathf.Sqrt(10.0f), giant / normal, 0.25f, $"a giant should walk sqrt(10) times as fast: {giant:0.0} m/s against {normal:0.0}");
            Assert.Greater(_player.transform.position.z, 10.0f, "the giant did not get anywhere");

            yield return Frames(30);   // the cooldown
            yield return Tap(_keyboard.digit0Key);
            Assert.AreEqual(BodySize.Spell.None, size.Size.Active, "0 again should end it early");
            yield return Frames(Mathf.CeilToInt(BodySize.ChangeSeconds * 60.0f) + 10);
            Assert.AreEqual(1.0f, _player.transform.localScale.y, 0.01f, "not back to its own size");
            Assert.IsFalse(locomotion.IsResized);
        }

        [UnityTest]
        public IEnumerator ShrinkMakesItATenthItsFeetPatterAndItsSkinAndStepShrinkWithIt()
        {
            BodySizeAbility size = Stage();
            var controller = _player.GetComponent<CharacterController>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            yield return Frames(30);
            float normal = 0.0f;
            yield return Walk(60, s => normal = s);

            Assert.IsTrue(size.TryShrink());
            yield return Frames(Mathf.CeilToInt(BodySize.ChangeSeconds * 60.0f) + 10);
            Assert.AreEqual(0.1f, _player.transform.localScale.y, 0.005f, "Shrink is a tenth");
            Assert.AreEqual(Mathf.Sqrt(10.0f), animator.GetFloat(BodySizeAbility.StrideRateName), 0.02f, "a tiny body's steps should patter");
            Assert.AreEqual(0.003f, controller.skinWidth, 0.0015f, "its skin should shrink with it: a tiny body floated on 3 cm of it");
            Assert.LessOrEqual(controller.stepOffset, (controller.height + controller.radius * 2.0f) * 0.1f, "a step taller than the body");
            Assert.Less(_player.transform.position.y, 0.06f, "a tiny body should stand on the ground, not hover over it");

            float tiny = 0.0f;
            yield return Walk(60, s => tiny = s);
            Assert.AreEqual(Mathf.Sqrt(0.1f), tiny / normal, 0.06f, $"a tiny body should walk a third as fast: {tiny:0.00} m/s against {normal:0.0}");
        }

        [UnityTest]
        public IEnumerator AGiantStepsUpOntoACar()
        {
            BodySizeAbility size = Stage();
            var car = GameObject.CreatePrimitive(PrimitiveType.Cube);   // a car-sized block 2 m tall, its near side 9 m ahead
            car.transform.position = new Vector3(0.0f, 1.0f, 13.0f);
            car.transform.localScale = new Vector3(12.0f, 2.0f, 8.0f);
            _made.Add(car);
            Physics.SyncTransforms();
            yield return Frames(20);
            Assert.IsTrue(size.TryGrow());
            yield return Frames(Mathf.CeilToInt(BodySize.ChangeSeconds * 60.0f) + 10);
            Press(_keyboard.wKey);
            float top = 0.0f;
            for (int i = 0; i < 150; i++)
            {
                yield return null;
                top = Mathf.Max(top, _player.transform.position.y);
            }
            Release(_keyboard.wKey);
            Assert.Greater(top, 1.9f, "a 26 m giant could not step onto a 2 m car (a kerb to it)");
        }

        [UnityTest]
        public IEnumerator UnderALowRoofItGrowsOnlyAsFarAsThereIsRoomAndTheRestOfTheWayOnceOut()
        {
            BodySizeAbility size = Stage();
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);   // a slab 9 m up
            roof.transform.position = new Vector3(0.0f, 9.5f, 0.0f);
            roof.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);
            _made.Add(roof);
            Physics.SyncTransforms();
            yield return Frames(20);
            Assert.IsTrue(size.TryGrow());
            yield return Frames(Mathf.CeilToInt(BodySize.ChangeSeconds * 60.0f) + 30);
            float height = _player.GetComponent<CharacterController>().bounds.max.y;
            Assert.Less(height, 9.4f, "it grew through the roof (its head may only reach the underside of it)");
            Assert.Less(_player.transform.position.y, 0.5f, "it was pushed up, not grown");
            Assert.Greater(_player.transform.localScale.y, 2.5f, "it should grow as far as there is room");
            Assert.IsTrue(size.Size.IsWaitingForRoom);

            Object.Destroy(roof);
            yield return null;
            Physics.SyncTransforms();
            yield return Frames(Mathf.CeilToInt(BodySize.ChangeSeconds * 60.0f) + 10);
            Assert.AreEqual(10.0f, _player.transform.localScale.y, 0.05f, "it did not carry on growing once the roof was gone");
        }

        [UnityTest]
        public IEnumerator TheCameraFramesAGiantFromTenTimesAsFar()
        {
            BodySizeAbility size = Stage();
            var cameraObject = new GameObject("FollowCamera") { tag = "MainCamera" };
            _made.Add(cameraObject);
            Camera view = cameraObject.AddComponent<Camera>();
            view.nearClipPlane = 0.1f;
            var follow = cameraObject.AddComponent<ThirdPersonCamera>();
            follow.Target = _player.transform;
            yield return Frames(30);
            float before = Vector3.Distance(cameraObject.transform.position, _player.transform.position);

            Assert.IsTrue(size.TryGrow());
            yield return Frames(Mathf.CeilToInt(BodySize.ChangeSeconds * 60.0f) + 10);
            float after = Vector3.Distance(cameraObject.transform.position, _player.transform.position);
            Assert.AreEqual(10.0f, after / before, 0.6f, $"the camera should be ten times as far from a giant: {after:0.0} m against {before:0.0}");

            Assert.IsTrue(size.TryShrink());
            yield return Frames(Mathf.CeilToInt(BodySize.ChangeSeconds * 60.0f) + 10);
            float tiny = Vector3.Distance(cameraObject.transform.position, _player.transform.position);
            Assert.AreEqual(0.1f, tiny / before, 0.02f, $"and a tenth as far from a tiny body: {tiny:0.00} m against {before:0.0}");
            Assert.Less(view.nearClipPlane, 0.05f, "the near plane should come in for a tiny body");
        }
    }
}
