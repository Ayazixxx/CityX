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
    /// The slingshot on the real body (docs/SLINGSHOT.md): in the left hand, drawn fully in half the bow's time, the pouch hand back to the chin and
    /// the pebble in the pouch; a full draw hits a body 20 m off for its full damage and the pebble drops off it rather than sticking; a second
    /// shot waits for the next pebble in the pouch; another player's copy draws with the shooter and flings a pebble of its own.
    /// </summary>
    public sealed class SlingshotPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private readonly List<GameObject> _made = new List<GameObject>();

        private sealed class Dummy : MonoBehaviour, IDamageable
        {
            public float Taken;
            public int Hits;

            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Taken += amount;
                Hits++;
                return HitResponse.Landed;
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            foreach (ArrowProjectile shot in Object.FindObjectsByType<ArrowProjectile>(FindObjectsSortMode.None)) Object.Destroy(shot.gameObject);
        }

        private GameObject Body(Vector3 at, float yawDegrees = 0.0f)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
                _made.Add(ground);
            }
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.Euler(0.0f, yawDegrees, 0.0f));
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var archery = player.GetComponent<Archery>();
            if (archery == null)
            {
                archery = player.AddComponent<Archery>();
            }
            archery.PebblePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SlingshotPaths.Pebble);
            Assert.IsNotNull(archery.PebblePrefab, "no Pebble.prefab -- rebuild the scene");
            return player;
#else
            return null;
#endif
        }

        private BowWeapon Slingshot()
        {
#if UNITY_EDITOR
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SlingshotPaths.Slingshot));
            Assert.IsNotNull(model, "no Slingshot.prefab -- rebuild the scene");
            _made.Add(model);
            return model.GetComponent<BowWeapon>();
#else
            return null;
#endif
        }

        private Dummy Target(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = at;
            go.transform.localScale = new Vector3(0.8f, 1.3f, 0.8f);
            Physics.SyncTransforms();
            _made.Add(go);
            return go.AddComponent<Dummy>();
        }

        private static Vector3 Origin(GameObject player) => player.transform.position + Vector3.up * 1.6f + player.transform.forward * 0.3f;

        private static IEnumerator Hold(GameObject player, BowWeapon sling, bool held, bool aiming, float seconds)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                sling.Draw(held, Origin(player), player.transform.forward, aiming, player, Step);
                locomotion.Tick(new LocomotionInput { YawDegrees = player.transform.eulerAngles.y, Aiming = aiming }, Step);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ItIsDrawnToTheChinInHalfTheBowsTimeThePebbleInThePouch()
        {
            GameObject player = Body(Vector3.zero);
            BowWeapon sling = Slingshot();
            Assert.AreEqual(DrawnKind.Slingshot, sling.Kind);
            Assert.AreEqual(ItemRegistry.Slingshot, sling.ItemId);
            sling.OnEquip(player, ItemInstanceData.None);
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand), rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.AreEqual(leftHand, sling.transform.parent, "the slingshot is not in the left hand");
            yield return Hold(player, sling, false, true, 0.4f);

            yield return Hold(player, sling, true, true, SlingshotRules.FullDrawSeconds + 2.0f * Step);
            Assert.AreEqual(1.0f, sling.DrawShare, 1e-3f, "not fully drawn in half a second (the bow takes a whole one)");
            yield return Hold(player, sling, true, true, 0.3f);
            Vector3 aim = player.transform.forward;
            float drawnBehind = Vector3.Dot(leftHand.position - rightHand.position, aim);
            Assert.Greater(drawnBehind, 0.35f, $"the pouch hand is not drawn back ({drawnBehind:F2} m)");
            Assert.IsTrue(sling.NockedArrow.gameObject.activeSelf, "no pebble in the pouch while drawing");
            Assert.Less(Vector3.Distance(sling.NockedArrow.position, rightHand.position), 0.12f, "the pebble is not at the pouch hand");
            float aboveGrip = Vector3.Dot(rightHand.position - leftHand.position, Vector3.up);
            Assert.Greater(aboveGrip, 0.0f, "the pouch is drawn from below the fork");

            // Held as Bully's Jimmy holds it (the owner, 2026-11-01: "hold it further away from his head unbend his arm"): the fork arm
            // straight, the pouch hand beside the jaw and outside the head, the fork well out in front of the face.
            Transform leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            float straight = Vector3.Angle(leftLower.position - leftUpper.position, leftHand.position - leftLower.position);
            Assert.Less(straight, 15.0f, $"the fork arm is bent {straight:F0} degrees at the elbow");
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            float headRadius = FusedBodyShape.HeadRadius * player.GetComponent<PlayerLocomotion>().WorldHeight / BodyRig.StandingHeight;
            Assert.Greater(Vector3.Distance(rightHand.position, head.position), headRadius, "the pouch hand is inside the head");
            float forkOut = Vector3.Dot(sling.transform.position - head.position, aim);
            Assert.Greater(forkOut, 0.6f, $"the fork is only {forkOut:F2} m out before the head");
            Transform rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Assert.Greater(Vector3.Distance(rightLower.position, head.position), headRadius, "the drawing forearm goes through the head");
            Assert.Greater(Vector3.Distance(leftLower.position, head.position), headRadius, "the fork arm goes through the head");
        }

        /// <summary>Owner, 2026-11-05: "when you hold right click the animation stretches the string ... make it so the string goes back as you hold click not just when aiming in".</summary>
        [UnityTest]
        public IEnumerator AimingLeavesTheBandsSlackAndOnlyHoldingTheClickPullsThem()
        {
            GameObject player = Body(Vector3.zero);
            BowWeapon sling = Slingshot();
            sling.OnEquip(player, ItemInstanceData.None);
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand), rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 aim = player.transform.forward;
            float Stretch() => Vector3.Distance(sling.String.GetPosition(1), sling.RestPoint);

            yield return Hold(player, sling, false, true, 0.4f);
            Assert.Greater(player.GetComponent<PlayerLocomotion>().BowRaise, 0.9f, "right click did not bring it up");
            Assert.Less(Stretch(), 0.005f, "aiming alone stretches the bands");
            Assert.Less(Vector3.Distance(sling.NockedArrow.localPosition, sling.RestPoint), 0.005f, "the pebble is not in the slack pouch");
            float aimedBack = Vector3.Dot(leftHand.position - rightHand.position, aim);

            // The arm waiting forward of the cheek must not lie through the head.
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm), rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            float headRadius = FusedBodyShape.HeadRadius * player.GetComponent<PlayerLocomotion>().WorldHeight / BodyRig.StandingHeight;
            Assert.Greater(DistanceToSegment(head.position, rightLower.position, rightHand.position), headRadius, "the waiting forearm goes through the head");
            Assert.Greater(DistanceToSegment(head.position, rightUpper.position, rightLower.position), headRadius, "the waiting upper arm goes through the head");

            yield return Hold(player, sling, true, true, SlingshotRules.FullDrawSeconds * 0.5f);
            float halfWay = Stretch();
            Assert.Greater(halfWay, 0.1f, "half a draw has not pulled the bands");

            yield return Hold(player, sling, true, true, SlingshotRules.FullDrawSeconds * 0.5f + 0.3f);
            Assert.Greater(Stretch(), halfWay + 0.1f, "the bands do not go on stretching as the click is held");
            float drawnBack = Vector3.Dot(leftHand.position - rightHand.position, aim);
            Assert.Greater(drawnBack, aimedBack + 0.15f, $"the hand did not draw back ({aimedBack:F2} m to {drawnBack:F2} m)");

            yield return Hold(player, sling, false, true, SlingshotRules.ReloadSeconds + 0.1f);
            Assert.Less(Stretch(), 0.005f, "the bands stay stretched after the shot");
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 along = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, along) / Mathf.Max(1e-6f, along.sqrMagnitude));
            return Vector3.Distance(point, a + along * t);
        }

        [UnityTest]
        public IEnumerator AFullDrawHitsForFullDamageAndThePebbleDropsOffInsteadOfSticking()
        {
            GameObject player = Body(Vector3.zero);
            BowWeapon sling = Slingshot();
            sling.OnEquip(player, ItemInstanceData.None);
            Dummy far = Target(new Vector3(0.0f, 1.6f, 20.0f));
            yield return Hold(player, sling, false, true, 0.3f);
            yield return Hold(player, sling, true, true, SlingshotRules.FullDrawSeconds + 0.1f);
            yield return Hold(player, sling, false, true, Step);
            Assert.AreEqual(1, far.Hits, "a full draw did not hit the body 20 m ahead");
            Assert.AreEqual(SlingshotRules.FullDamage, far.Taken, 0.5f);
            ArrowProjectile pebble = Object.FindFirstObjectByType<ArrowProjectile>();
            Assert.IsNotNull(pebble, "no pebble was flung");
            Assert.IsTrue(pebble.Blunt, "the slingshot flung something that is not a pebble");
            yield return Hold(player, sling, false, true, 0.5f);
            Assert.AreEqual(ArrowProjectile.Phase.Dropped, pebble.CurrentPhase, "the pebble did not drop off what it hit");
            Assert.IsNull(pebble.StuckTo, "a pebble stuck in the body like an arrow");
            yield return Hold(player, sling, false, false, SlingshotRules.LieSeconds + 0.2f);
            Assert.IsTrue(pebble == null, "the pebble lay there for ever");
        }

        [UnityTest]
        public IEnumerator ASecondShotWaitsForTheNextPebbleInThePouch()
        {
            GameObject player = Body(Vector3.zero);
            BowWeapon sling = Slingshot();
            sling.OnEquip(player, ItemInstanceData.None);
            int shots = 0;
            sling.ShotResolved += _ => shots++;
            yield return Hold(player, sling, false, true, 0.3f);
            yield return Hold(player, sling, true, true, 2.0f * Step);
            yield return Hold(player, sling, false, true, Step);
            Assert.AreEqual(1, shots, "a tap flung nothing");
            yield return Hold(player, sling, true, true, 2.0f * Step);
            yield return Hold(player, sling, false, true, Step);
            Assert.AreEqual(1, shots, "a second pebble went before the pouch was loaded again");
            yield return Hold(player, sling, false, true, SlingshotRules.ReloadSeconds);
            yield return Hold(player, sling, true, true, 2.0f * Step);
            yield return Hold(player, sling, false, true, Step);
            Assert.AreEqual(2, shots, "loaded again, a tap flung nothing");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyDrawsWithTheShooterAndFlingsAPebbleOfItsOwn()
        {
            GameObject shooter = Body(Vector3.zero);
            GameObject copy = Body(new Vector3(20.0f, 0.0f, 0.0f), 90.0f);
            BowWeapon sling = Slingshot();
            sling.OnEquip(shooter, ItemInstanceData.None);
            BowWeapon copySling = Slingshot();
            copySling.MountCosmetic(copy);
            var archery = shooter.GetComponent<Archery>();
            var copyArchery = copy.GetComponent<Archery>();
            archery.Fired += copyArchery.PlayRemote;
            var copyLocomotion = copy.GetComponent<PlayerLocomotion>();

            yield return Hold(shooter, sling, false, true, 0.3f);
            Assert.Greater(copyLocomotion.BowRaise, 0.9f, "the copy's slingshot does not come up with the aim");
            Assert.Less(Vector3.Distance(copySling.String.GetPosition(1), copySling.RestPoint), 0.005f, "the shooter only aims, and the copy's bands stretch");

            yield return Hold(shooter, sling, true, true, 0.35f);
            Assert.Greater(copyLocomotion.BowRaise, 0.9f, "the copy's slingshot is not up");
            Assert.Greater(copyLocomotion.BowPull, 0.3f, "the copy is not drawing with the shooter");
            yield return Hold(shooter, sling, false, true, 0.1f);
            ArrowProjectile[] pebbles = Object.FindObjectsByType<ArrowProjectile>(FindObjectsSortMode.None);
            Assert.IsTrue(System.Array.Exists(pebbles, p => p.Cosmetic && p.Blunt), "the copy flung no pebble of its own");
            Assert.IsFalse(System.Array.Exists(pebbles, p => !p.Blunt), "the copy loosed an arrow from a slingshot");
            archery.Fired -= copyArchery.PlayRemote;
        }
    }

    /// <summary>Where the slingshot's prefabs are (ArcheryBuilder is editor-only).</summary>
    internal static class SlingshotPaths
    {
        public const string Slingshot = "Assets/CityX/Weapons/Slingshot.prefab";
        public const string Pebble = "Assets/CityX/Weapons/Pebble.prefab";
    }
}
