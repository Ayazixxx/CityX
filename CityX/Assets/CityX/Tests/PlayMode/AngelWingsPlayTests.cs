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
    /// The angel wings on the real body (docs/WINGS.md): worn folded on the back and opened on taking off, mirrored; the beat following the
    /// flight (slow hovering, quick climbing, a glide at speed, a tuck descending); the body bobbed with the beat and surging in a climb;
    /// Ctrl to the ground landing and folding them; a copy of another player carrying them and beating them as its wearer flies; the Flight
    /// Speed stat making them faster.
    /// </summary>
    public sealed class AngelWingsPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            foreach (AngelWings w in Object.FindObjectsByType<AngelWings>(FindObjectsSortMode.None)) Object.Destroy(w.gameObject);
        }

        private GameObject Body(Vector3 at)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
                _made.Add(ground);
            }
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            return player;
#else
            return null;
#endif
        }

        private AngelWings Wings()
        {
#if UNITY_EDITOR
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(AngelWings.PrefabPath));
            Assert.IsNotNull(model, "no AngelWings.prefab -- rebuild the scene");
            _made.Add(model);
            return model.GetComponent<AngelWings>();
#else
            return null;
#endif
        }

        private static IEnumerator Fly(GameObject player, AngelWings wings, float seconds, bool thrust = false, bool descend = false, bool boost = false,
            Vector2 move = default, float pitch = 0.0f, System.Action each = null)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                wings.Tick(new FlightInput
                {
                    ThrustHeld = thrust,
                    DescendHeld = descend,
                    BoostHeld = boost,
                    Move = move,
                    CameraPitchDegrees = pitch,
                    Airborne = !locomotion.IsGrounded,
                }, Step);
                locomotion.Tick(new LocomotionInput
                {
                    YawDegrees = player.transform.eulerAngles.y,
                    Move = move,
                    JetpackThrusting = wings.IsFlying,
                    FlightSpeed = wings.HorizontalSpeed,
                    FlightAcceleration = wings.HorizontalAcceleration,
                }, Step);
                each?.Invoke();
                yield return null;
            }
        }

        /// <summary>A jump, then Space held in the air: the flight begins.</summary>
        private static IEnumerator TakeOff(GameObject player, AngelWings wings)
        {
            player.GetComponent<PlayerLocomotion>().SetVerticalVelocity(6.0f);
            yield return Fly(player, wings, 0.1f);
            yield return Fly(player, wings, 0.5f, thrust: true);
            Assert.IsTrue(wings.IsFlying, "Space in the air did not take off");
        }

        private static Vector3 Tip(GameObject player, AngelWings.Wing wing, bool left)
        {
            // The outermost primary's far end (the last flight feather on the hand, row 2).
            Transform feather = wing.Feathers[wing.Feathers.Length - 1];
            for (int i = 0; i < wing.Feathers.Length; i++)
            {
                if (wing.Feathers[i].name.StartsWith("F2")) feather = wing.Feathers[i];
            }
            // A left feather's strip runs along its bone's -X (the right wing's mirror: AngelWingsBuilder).
            Vector3 alongFeather = (left ? Vector3.left : Vector3.right) * AngelWings.LongestPrimary;
            return player.transform.InverseTransformPoint(feather.TransformPoint(alongFeather));
        }

        [UnityTest]
        public IEnumerator WornTheyFoldOnTheBackAndOpenWideOnTakingOff()
        {
            GameObject player = Body(Vector3.zero);
            AngelWings wings = Wings();
            wings.OnEquip(player, ItemInstanceData.None);
            Animator animator = player.GetComponentInChildren<Animator>();
            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.Spine), wings.transform.parent, "the wings are not on the spine");
            yield return Fly(player, wings, 1.0f);
            Assert.AreEqual(1.0f, wings.Beat.Fold, 0.01f, "on the ground the wings should be folded");
            Vector3 foldedRight = Tip(player, wings.Right, false), foldedLeft = Tip(player, wings.Left, true);
            Assert.Less(foldedRight.z, -0.1f, "folded, the wing should lie behind the back");
            Assert.Less(Mathf.Abs(foldedRight.x), 1.4f, "folded, the wing should be drawn in to the body");
            Assert.AreEqual(-foldedRight.x, foldedLeft.x, 0.05f, "the left wing is not the right's mirror");

            yield return TakeOff(player, wings);
            Assert.Less(wings.Beat.Fold, 0.01f, "in the air the wings should be open");
            Vector3 openRight = Tip(player, wings.Right, false), openLeft = Tip(player, wings.Left, true);
            Assert.Greater(openRight.x, 1.2f, "open, the right wingtip should reach out to the side");
            Assert.Less(openLeft.x, -1.2f, "open, the left wingtip should reach out to the side");
            Assert.Greater(openRight.magnitude, 2.0f, "open, the wing should reach over two metres from the spine");
            Assert.AreEqual(-openRight.x, openLeft.x, 0.05f);
            Assert.AreEqual(openRight.y, openLeft.y, 0.05f);
        }

        [UnityTest]
        public IEnumerator TheBeatFollowsTheFlightSlowHoveringQuickClimbingGlidingAtSpeedTuckedDescending()
        {
            GameObject player = Body(Vector3.zero);
            AngelWings wings = Wings();
            wings.OnEquip(player, ItemInstanceData.None);
            yield return TakeOff(player, wings);
            int beats = 0;
            void Count()
            {
                if (wings.Beat.DownstrokeBegan) beats++;
            }
            yield return Fly(player, wings, 1.0f);
            beats = 0;
            yield return Fly(player, wings, 3.0f, each: Count);
            int hovering = beats;
            Assert.AreEqual(WingBeat.HoverHz * 3.0f, hovering, 1.0f, "hovering should beat slowly");
            beats = 0;
            yield return Fly(player, wings, 3.0f, thrust: true, each: Count);
            int climbing = beats;
            Assert.Greater(climbing, hovering, "climbing should beat quicker than hovering");
            yield return Fly(player, wings, 3.0f, move: Vector2.up);
            Assert.Greater(wings.Beat.Glide, 0.9f, "at cruising speed the wings should glide");
            beats = 0;
            yield return Fly(player, wings, 3.0f, move: Vector2.up, each: Count);
            Assert.Less(beats, hovering, "gliding should beat less than hovering");
            yield return Fly(player, wings, 1.0f, descend: true);
            Assert.Greater(wings.Beat.Tuck, 0.9f, "descending should tuck the wings");
        }

        [UnityTest]
        public IEnumerator HoveringBobsTheBodyWithTheBeatAndAClimbSurgesWithIt()
        {
            GameObject player = Body(Vector3.zero);
            AngelWings wings = Wings();
            wings.OnEquip(player, ItemInstanceData.None);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            yield return TakeOff(player, wings);
            yield return Fly(player, wings, 1.0f);
            float height = player.transform.position.y;
            float highest = float.MinValue, lowest = float.MaxValue;
            yield return Fly(player, wings, 2.0f, each: () =>
            {
                highest = Mathf.Max(highest, locomotion.Velocity.y);
                lowest = Mathf.Min(lowest, locomotion.Velocity.y);
            });
            Assert.Greater(highest, 0.15f, "hovering, the body should rise on the downstroke");
            Assert.Less(lowest, -0.15f, "and sink on the upstroke");
            Assert.AreEqual(height, player.transform.position.y, 0.15f, "but hold its height overall");

            highest = float.MinValue;
            lowest = float.MaxValue;
            yield return Fly(player, wings, 2.0f, thrust: true, each: () =>
            {
                highest = Mathf.Max(highest, locomotion.Velocity.y);
                lowest = Mathf.Min(lowest, locomotion.Velocity.y);
            });
            Assert.Greater(lowest, AngelWings.ClimbSpeed * 0.5f, "a climb never stops climbing");
            Assert.Greater(highest - lowest, 2.0f, "a climb should surge with the beats");
            Assert.Greater(player.transform.position.y, height + 8.0f, "a 2 s climb should have gained height");
        }

        [UnityTest]
        public IEnumerator CtrlToTheGroundLandsAndFoldsTheWings()
        {
            GameObject player = Body(Vector3.zero);
            AngelWings wings = Wings();
            wings.OnEquip(player, ItemInstanceData.None);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            yield return TakeOff(player, wings);
            yield return Fly(player, wings, 0.6f, thrust: true);
            Assert.Greater(player.transform.position.y, 2.0f, "setup: did not climb");
            Assert.IsTrue(locomotion.MovementRooted == false);
            yield return Fly(player, wings, 3.0f, descend: true);
            Assert.IsFalse(wings.IsFlying, "holding Ctrl to the ground should land");
            Assert.IsTrue(locomotion.IsGrounded);
            yield return Fly(player, wings, 1.0f);
            Assert.Greater(wings.Beat.Fold, 0.95f, "landed, the wings should fold");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyCarriesTheWingsAndBeatsThemAsItsWearerFlies()
        {
            GameObject copy = Body(new Vector3(10.0f, 0.0f, 0.0f));
            AngelWings wings = Wings();
            // What NetworkedEquipmentSync does to a copy's item: mounted as a model only, its scripts off but for those that keep running.
            wings.MountCosmetic(copy);
            InertModel.Strip(wings.gameObject);
            foreach (MonoBehaviour behaviour in wings.GetComponents<MonoBehaviour>())
            {
                if (behaviour is ICosmeticKeepsRunning) behaviour.enabled = true;
            }
            Assert.IsTrue(wings.enabled, "the wings' own script must keep running on a copy, or they never beat");
            Assert.AreEqual(copy.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Spine), wings.transform.parent);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(1.0f, wings.Beat.Fold, 0.01f, "the copy's wings should be folded while its wearer is on the ground");

            // The wearer flies (the synced JetpackThrusting parameter) and the copy is seen climbing.
            wings.SetCosmeticActive(true);
            var controller = copy.GetComponent<CharacterController>();
            controller.enabled = false;
            float phaseBefore = -1.0f;
            for (int i = 0; i < 60; i++)
            {
                copy.transform.position += Vector3.up * (3.0f * Time.deltaTime);
                if (i == 30) phaseBefore = wings.Beat.Phase;
                yield return null;
            }
            Assert.Less(wings.Beat.Fold, 0.01f, "the copy's wings should open when its wearer flies");
            Assert.AreNotEqual(phaseBefore, wings.Beat.Phase, "the copy's wings should beat");
            Assert.AreEqual(WingBeat.ClimbHz, wings.Beat.Rate, 0.3f, "seen climbing, the copy's wings should beat at the climb's rate");
            wings.SetCosmeticActive(false);
            for (int i = 0; i < 60; i++) yield return null;
            Assert.Greater(wings.Beat.Fold, 0.95f, "the copy's wings should fold when its wearer lands");
            controller.enabled = true;
        }

        [UnityTest]
        public IEnumerator TheFlightSpeedStatMakesThemFaster()
        {
            GameObject player = Body(Vector3.zero);
            AngelWings wings = Wings();
            wings.OnEquip(player, ItemInstanceData.None);
            var vitals = player.GetComponent<PlayerVitals>();
            yield return TakeOff(player, wings);
            yield return Fly(player, wings, 0.2f, move: Vector2.up);
            float plain = wings.HorizontalSpeed;
            Assert.AreEqual(AngelWings.CruiseSpeed, plain, 0.01f);
            vitals.Stats[StatId.FlightSpeed] = 20;
            yield return Fly(player, wings, 0.2f, move: Vector2.up);
            Assert.Greater(wings.HorizontalSpeed, plain * 1.1f, "20 points of Flight Speed should fly noticeably faster");
            Assert.AreEqual(AngelWings.CruiseSpeed * PlayerVitals.SpeedOf(vitals, StatId.FlightSpeed), wings.HorizontalSpeed, 0.01f);
        }
    }
}
