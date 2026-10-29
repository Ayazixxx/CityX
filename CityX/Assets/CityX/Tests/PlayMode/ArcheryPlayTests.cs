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
    /// The bow and the crossbow on the real body (docs/ARCHERY.md): a tap is a weak lob and a full draw hits harder and further; past its flat
    /// range the arrow falls and lands in the ground; the arms come up at the aim and the string hand draws back to the cheek; the crossbow
    /// fires its bolt at once at full power and then spans for two and a half seconds; another player's copy draws with the archer and looses
    /// an arrow of its own.
    /// </summary>
    public sealed class ArcheryPlayTests
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
            foreach (ArrowProjectile arrow in Object.FindObjectsByType<ArrowProjectile>(FindObjectsSortMode.None)) Object.Destroy(arrow.gameObject);
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
            archery.ArrowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ArcheryBuilderPaths.Arrow);
            Assert.IsNotNull(archery.ArrowPrefab, "no Arrow.prefab -- rebuild the scene");
            return player;
#else
            return null;
#endif
        }

        private T Weapon<T>(string path) where T : Component
        {
#if UNITY_EDITOR
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path));
            Assert.IsNotNull(model, $"no {path} -- rebuild the scene");
            _made.Add(model);
            return model.GetComponent<T>();
#else
            return null;
#endif
        }

        private Dummy Target(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = at;
            go.transform.localScale = new Vector3(0.8f, 1.3f, 0.8f);
            // A primitive is born at the origin, inside the archer: synced now, or the controller's next move is shoved out of it (0.79 m, measured).
            Physics.SyncTransforms();
            _made.Add(go);
            return go.AddComponent<Dummy>();
        }

        /// <summary>The shot's origin and way, as the camera would give them: from the chest straight ahead.</summary>
        private static Vector3 Origin(GameObject player) => player.transform.position + Vector3.up * 1.6f + player.transform.forward * 0.3f;

        /// <summary>Holds the bow's click (or not), aiming (or not), for <paramref name="seconds"/>, ticking the body.</summary>
        private static IEnumerator Hold(GameObject player, BowWeapon bow, bool held, bool aiming, float seconds)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                bow.Draw(held, Origin(player), player.transform.forward, aiming, player, Step);
                locomotion.Tick(new LocomotionInput { YawDegrees = player.transform.eulerAngles.y, Aiming = aiming }, Step);
                yield return null;
            }
        }

        private static IEnumerator Frames(GameObject player, float seconds)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = player.transform.eulerAngles.y }, Step);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ATapIsAWeakLobAndAFullDrawHitsHarderAndFurther()
        {
            GameObject player = Body(Vector3.zero);
            BowWeapon bow = Weapon<BowWeapon>(ArcheryBuilderPaths.Bow);
            bow.OnEquip(player, ItemInstanceData.None);
            Dummy near = Target(new Vector3(0.0f, 1.6f, 8.0f));
            Vector3 stood = player.transform.position;
            yield return Hold(player, bow, false, true, 0.3f);

            // A tap: the near one, within the tap's flat range, is hit for the tap's damage, at once.
            yield return Hold(player, bow, true, true, 0.03f);
            yield return Hold(player, bow, false, true, 0.05f);
            Assert.AreEqual(1, near.Hits, "a tap did not hit the body 8 m ahead");
            Assert.AreEqual(ArcheryRules.TapDamage, near.Taken, 0.5f, "a tap is the least draw's damage");
            Object.Destroy(near.gameObject);
            yield return null;

            // A full draw: a body 40 m away, past any tap, is hit for full damage; the archer stands still through all of it.
            Dummy far = Target(new Vector3(0.0f, 1.6f, 40.0f));
            yield return Hold(player, bow, true, true, ArcheryRules.FullDrawSeconds + 0.2f);
            Assert.AreEqual(1.0f, bow.DrawShare, 1e-3f, "not fully drawn after a second");
            yield return Hold(player, bow, false, true, 0.05f);
            Assert.Less(Vector3.Distance(player.transform.position, stood), 0.15f, "the body moved while aiming and drawing with no movement input");
            Assert.AreEqual(1, far.Hits, "a full draw did not hit the body 40 m ahead");
            Assert.AreEqual(ArcheryRules.FullDamage, far.Taken, 0.5f);
            yield return Hold(player, bow, false, true, 0.6f);
            ArrowProjectile[] arrows = Object.FindObjectsByType<ArrowProjectile>(FindObjectsSortMode.None);
            Assert.IsTrue(System.Array.Exists(arrows, a => a.CurrentPhase == ArrowProjectile.Phase.Stuck && a.StuckTo == far.transform), "the arrow is not stuck in the body it hit");
        }

        [UnityTest]
        public IEnumerator PastItsFlatRangeTheArrowFallsAndLandsInTheGround()
        {
            GameObject player = Body(Vector3.zero);
            BowWeapon bow = Weapon<BowWeapon>(ArcheryBuilderPaths.Bow);
            bow.OnEquip(player, ItemInstanceData.None);
            Dummy far = Target(new Vector3(0.0f, 1.6f, 40.0f));
            yield return Hold(player, bow, false, true, 0.2f);
            // A tap at it: 12 m flat, then the arrow falls short of it.
            yield return Hold(player, bow, true, true, 0.03f);
            yield return Hold(player, bow, false, true, 0.05f);
            Assert.AreEqual(0, far.Hits, "a tap reached 40 m");
            ArrowProjectile arrow = Object.FindFirstObjectByType<ArrowProjectile>();
            Assert.IsNotNull(arrow, "no arrow in the air");
            for (float t = 0.0f; t < ArcheryRules.FallSeconds && arrow != null && arrow.CurrentPhase != ArrowProjectile.Phase.Stuck; t += Step)
            {
                yield return Frames(player, Step);
            }
            Assert.IsNotNull(arrow, "the arrow vanished instead of landing");
            Assert.AreEqual(ArrowProjectile.Phase.Stuck, arrow.CurrentPhase, "the arrow never landed");
            Assert.Less(arrow.transform.position.y, 1.0f, "it did not come down");
            Assert.Greater(arrow.transform.position.z, ArcheryRules.TapFlatRange - 1.0f, "it landed before its flat range ended");
            Assert.Less(arrow.transform.position.z, 39.0f, "it carried to the far body as if it never fell");
            Assert.AreEqual(0, far.Hits);
        }

        [UnityTest]
        public IEnumerator TheArmsComeUpAtTheAimAndTheStringHandDrawsBackToTheCheek()
        {
            GameObject player = Body(Vector3.zero);
            BowWeapon bow = Weapon<BowWeapon>(ArcheryBuilderPaths.Bow);
            bow.OnEquip(player, ItemInstanceData.None);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand), rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), head = animator.GetBoneTransform(HumanBodyBones.Head);
            yield return Hold(player, bow, false, false, 0.3f);
            Assert.Less(locomotion.BowRaise, 0.01f, "the bow is up with nothing held");

            yield return Hold(player, bow, false, true, 0.4f);
            Assert.Greater(locomotion.BowRaise, 0.99f, "aiming did not bring the bow up");
            Vector3 aim = player.transform.forward;
            float ahead = Vector3.Dot(leftHand.position - leftShoulder.position, aim);
            Assert.Greater(ahead, 0.45f, $"the bow arm is not out along the aim ({ahead:F2} m)");
            Assert.Greater(Vector3.Dot(bow.transform.forward, aim), 0.95f, "the bow does not point the way the body aims");
            Assert.Greater(Vector3.Dot(bow.transform.up, Vector3.up), 0.9f, "the bow is not upright");
            float restBehind = Vector3.Dot(leftHand.position - rightHand.position, aim);
            Assert.That(restBehind, Is.InRange(0.0f, 0.25f), $"undrawn, the string hand is not at the nock just behind the grip ({restBehind:F2} m)");

            yield return Hold(player, bow, true, true, ArcheryRules.FullDrawSeconds + 0.3f);
            Assert.Greater(locomotion.BowDraw, 0.95f, "the draw did not reach full");
            float drawnBehind = Vector3.Dot(leftHand.position - rightHand.position, aim);
            Assert.Greater(drawnBehind, 0.5f, $"the string hand is not drawn back ({drawnBehind:F2} m)");
            Assert.Less(Vector3.Distance(rightHand.position, head.position), 0.5f, "the string hand is not at the cheek");
            Assert.IsTrue(bow.NockedArrow.gameObject.activeSelf, "no arrow nocked while drawing");
            Assert.Less(Vector3.Distance(bow.NockedArrow.position, rightHand.position), 0.12f, "the nocked arrow is not at the string hand");
        }

        [UnityTest]
        public IEnumerator TheCrossbowFiresItsBoltAtOnceAtFullPowerThenSpansForTwoAndAHalfSeconds()
        {
            // On the ground, not in it: a gun's shot spreads in the air, and a body spawned level with the ground reads as airborne.
            GameObject player = Body(new Vector3(0.0f, 0.05f, 0.0f));
            var crossbow = Weapon<PistolWeapon>(ArcheryBuilderPaths.Crossbow);
            crossbow.OnEquip(player, ItemInstanceData.None);
            Dummy far = Target(new Vector3(0.0f, 1.6f, 50.0f));
            yield return Frames(player, 0.6f);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            Assert.IsTrue(locomotion.IsGrounded, "setup: the archer is not on the ground (an airborne shot spreads)");
            Assert.IsTrue(crossbow.Fire(Origin(player), player.transform.forward, true, player), "the loaded crossbow did not fire");
            Assert.AreEqual(1, far.Hits, "the bolt did not hit the body 50 m ahead at once");
            Assert.AreEqual(ArcheryRules.BoltDamage, far.Taken, 0.5f);
            yield return null;
            Assert.IsTrue(crossbow.IsReloading, "it did not start spanning after its shot");
            yield return Frames(player, 1.0f);
            Assert.IsFalse(crossbow.Fire(Origin(player), player.transform.forward, true, player), "it fired while still being spanned");
            Assert.AreEqual(1, far.Hits);
            yield return Frames(player, ArcheryRules.CrossbowReloadSeconds);
            Assert.IsFalse(crossbow.IsReloading, "still spanning after its time");
            Assert.IsTrue(crossbow.Fire(Origin(player), player.transform.forward, true, player), "spanned, it did not fire again");
            Assert.AreEqual(2, far.Hits);
            ArrowProjectile[] arrows = Object.FindObjectsByType<ArrowProjectile>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(arrows.Length, 1, "no bolt was shown");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyDrawsWithTheArcherAndLoosesAnArrowOfItsOwn()
        {
            GameObject archer = Body(Vector3.zero);
            GameObject copy = Body(new Vector3(20.0f, 0.0f, 0.0f), 90.0f);
            BowWeapon bow = Weapon<BowWeapon>(ArcheryBuilderPaths.Bow);
            bow.OnEquip(archer, ItemInstanceData.None);
            BowWeapon copyBow = Weapon<BowWeapon>(ArcheryBuilderPaths.Bow);
            copyBow.MountCosmetic(copy);
            var archery = archer.GetComponent<Archery>();
            var copyArchery = copy.GetComponent<Archery>();
            archery.Fired += copyArchery.PlayRemote;
            var copyLocomotion = copy.GetComponent<PlayerLocomotion>();

            yield return Hold(archer, bow, true, true, 0.6f);
            Assert.Greater(copyLocomotion.BowRaise, 0.9f, "the copy's bow is not up");
            Assert.Greater(copyLocomotion.BowDraw, 0.3f, "the copy is not drawing with the archer");
            yield return Hold(archer, bow, false, true, 0.1f);
            ArrowProjectile[] arrows = Object.FindObjectsByType<ArrowProjectile>(FindObjectsSortMode.None);
            Assert.IsTrue(System.Array.Exists(arrows, a => a.Cosmetic), "the copy loosed no arrow of its own");
            Assert.IsTrue(System.Array.Exists(arrows, a => !a.Cosmetic), "the archer's own arrow is missing");
            yield return Hold(archer, bow, false, false, 0.5f);
            Assert.Less(copyLocomotion.BowRaise, 0.1f, "the copy's bow did not come down with the archer's");
            archery.Fired -= copyArchery.PlayRemote;
        }
    }

    /// <summary>Where the archery prefabs are (ArcheryBuilder is editor-only; a PlayMode test cannot name it).</summary>
    internal static class ArcheryBuilderPaths
    {
        public const string Bow = "Assets/CityX/Weapons/Bow.prefab";
        public const string Arrow = "Assets/CityX/Weapons/Arrow.prefab";
        public const string Crossbow = "Assets/CityX/Weapons/Crossbow.prefab";
    }
}
