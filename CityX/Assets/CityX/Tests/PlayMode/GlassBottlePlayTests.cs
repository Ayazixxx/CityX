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
    /// The glass bottles on the real player (docs/GLASS_BOTTLE.md): carried raised by the neck; swung as a one-handed club, breaking to a half
    /// with sharp points on the first thing a swing hits, a body, a wall or anything, its pieces flying and the half hitting harder; whole,
    /// wound back by the aim and thrown from the hand at the throw's release; in the air it shatters whole on whatever it lands on, hurting a
    /// body; and on another player's copy the same throw and the same break, hurting no one.
    /// </summary>
    public sealed class GlassBottlePlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        private sealed class Dummy : MonoBehaviour, IDamageable
        {
            public int Hits;
            public float LastDamage = -1.0f;

            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Hits++;
                LastDamage = amount;
                return HitResponse.Landed;
            }
        }

        private readonly List<Object> _made = new List<Object>();
        private GameObject _body;
        private BottleWeapon _bottle;

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);
            _made.Add(ground);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (Object made in _made)
            {
                if (made != null) Object.Destroy(made);
            }
            _made.Clear();
            foreach (BottleShatter left in Object.FindObjectsByType<BottleShatter>(FindObjectsSortMode.None))
            {
                Object.Destroy(left.gameObject);
            }
            foreach (ThrownBottle left in Object.FindObjectsByType<ThrownBottle>(FindObjectsSortMode.None))
            {
                Object.Destroy(left.gameObject);
            }
        }

#if UNITY_EDITOR
        private static GameObject Load(string path) => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private GameObject Player(Vector3 at)
        {
            GameObject body = Object.Instantiate(Load("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            _made.Add(body);
            body.GetComponent<PlayerInputReader>().enabled = false;
            body.GetComponent<PlayerVitals>().InfiniteStamina = true;
            // (On the game's player from the start: PlayerAbilityWiring.)
            var thrower = body.GetComponent<BottleThrower>();
            if (thrower == null)
            {
                thrower = body.AddComponent<BottleThrower>();
            }
            var thrown = new GameObject[GlassBottleKinds.All.Length];
            for (int i = 0; i < thrown.Length; i++)
            {
                thrown[i] = Load(GlassBottleKinds.ThrownPrefabPath(GlassBottleKinds.All[i].ItemId));
            }
            thrower.ThrownPrefabs = thrown;
            return body;
        }

        private BottleWeapon Bottle(string itemId)
        {
            GameObject prefab = Load(GlassBottleKinds.PrefabPath(itemId));
            Assert.IsNotNull(prefab, $"no prefab for {itemId} -- rebuild the scene");
            var bottle = Object.Instantiate(prefab, new Vector3(20.0f, 1.0f, 20.0f), Quaternion.identity).GetComponent<BottleWeapon>();
            _made.Add(bottle.gameObject);
            return bottle;
        }

        private void Hold(string itemId)
        {
            _body = Player(new Vector3(0.0f, 0.05f, 0.0f));
            _bottle = Bottle(itemId);
            _bottle.OnEquip(_body, ItemInstanceData.None);
            _body.GetComponent<SoulsSwordCombat>().HitStopSeconds = 0.0f;
        }

        private Dummy Target(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = at;
            go.transform.localScale = new Vector3(1.0f, 1.3f, 1.0f);
            _made.Add(go);
            Physics.SyncTransforms();
            return go.AddComponent<Dummy>();
        }

        private IEnumerator Frames(int count, bool aiming = false)
        {
            for (int i = 0; i < count; i++)
            {
                _body.GetComponent<SoulsSwordCombat>().Tick(Dt);
                _body.GetComponent<PlayerVitals>().TickHitstun(Dt);
                _body.GetComponent<PlayerLocomotion>().Tick(new LocomotionInput { ThrowAiming = aiming }, Dt);
                yield return null;
            }
        }

        private Transform RightHand => _body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
#endif

        [UnityTest]
        public IEnumerator ItIsCarriedRaisedByTheNeckInTheRightHand()
        {
#if UNITY_EDITOR
            foreach (GlassBottleKinds.Kind kind in GlassBottleKinds.All)
            {
                Hold(kind.ItemId);
                yield return Frames(40);
                Assert.AreSame(RightHand, _bottle.transform.parent, $"{kind.ItemId} is not in the right hand");
                var locomotion = _body.GetComponent<PlayerLocomotion>();
                Assert.Greater(locomotion.RaisedCarryWeight, 0.95f, "the arm is not in the raised carry");
                Assert.Greater(locomotion.RightGrip, 0.5f, "the fingers are not closed round the neck");
                // Like the Demoman's: the fist up before the chest, the bottle standing up out of it.
                Vector3 shoulder = _body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                Assert.Greater(RightHand.position.y, shoulder.y - 0.75f, "the hand hangs at the side");
                Assert.Greater(_body.transform.InverseTransformPoint(RightHand.position).z, 0.25f, "the hand is not out before the body");
                Vector3 along = (_bottle.BladeTipWorld - _bottle.BladeBaseWorld).normalized;
                Assert.Greater(along.y, 0.6f, $"{kind.ItemId} does not stand up out of the fist: it points {along}");
                var souls = _body.GetComponent<SoulsSwordCombat>();
                Assert.IsTrue(souls.IsEquipped && souls.IsOneHanded, "it is not swung as a one-handed weapon");
                _bottle.OnUnequip(_body);
                Object.Destroy(_body);
                yield return null;
            }
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ASwingThatHitsABodyBreaksItToAHalfThatHitsHarder()
        {
#if UNITY_EDITOR
            Hold(GlassBottleKinds.Beer);
            Dummy target = Target(new Vector3(0.0f, 1.3f, 1.3f));
            yield return Frames(20);
            var souls = _body.GetComponent<SoulsSwordCombat>();
            souls.RequestLight(0.0f);
            yield return Frames(70);
            Assert.AreEqual(1, target.Hits, "the swing did not land");
            // (A one-handed light is scaled by its own attack: only that it is the whole bottle's, and less than the half's after it.)
            float whole = target.LastDamage;
            Assert.That(whole, Is.InRange(BottleWeapon.IntactMeleeDamage * 0.7f, BottleWeapon.IntactMeleeDamage * 1.6f), "the first blow is the whole bottle's");
            Assert.AreEqual(BottleWeapon.DurabilityState.Broken, _bottle.State, "the bottle did not break on the blow");
            Assert.IsTrue(_bottle.BrokenVisual.activeSelf && !_bottle.IntactVisual.activeSelf, "the half with its points does not show");
            Assert.IsFalse(_bottle.CanThrow(), "a broken bottle is thrown");
            BottleShatter shatter = Object.FindAnyObjectByType<BottleShatter>();
            Assert.IsNotNull(shatter, "nothing flew off as it broke");
            Assert.Greater(shatter.Flying, 0);
            Assert.Less(shatter.Flying, shatter.Pieces.Length, "the half still in the hand flew off too");

            souls.RequestLight(0.0f);
            yield return Frames(70);
            Assert.AreEqual(2, target.Hits);
            // (The second swing of the chain is scaled by its own attack too.)
            Assert.AreEqual(BottleWeapon.BrokenMeleeDamage, _bottle.GetMeleeDamage(), "the broken half does not hit harder");
            Assert.That(target.LastDamage, Is.InRange(BottleWeapon.BrokenMeleeDamage * 0.7f, BottleWeapon.BrokenMeleeDamage * 1.6f));
            Assert.AreEqual(BottleWeapon.DurabilityState.Broken, _bottle.State, "the half broke down on its first blow");
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ASwingThatHitsAWallBreaksItToo()
        {
#if UNITY_EDITOR
            Hold(GlassBottleKinds.Classic);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 1.5f, 1.25f);
            wall.transform.localScale = new Vector3(6.0f, 3.0f, 0.4f);
            _made.Add(wall);
            Physics.SyncTransforms();
            yield return Frames(20);
            Assert.AreEqual(BottleWeapon.DurabilityState.Intact, _bottle.State, "it broke before it was swung");
            _body.GetComponent<SoulsSwordCombat>().RequestLight(0.0f);
            yield return Frames(70);
            Assert.AreEqual(BottleWeapon.DurabilityState.Broken, _bottle.State,
                "the bottle swung into a wall is still whole (owner: \"when swung and hits anything while in hand it turns into the half broken version\")");
            Assert.IsNotNull(Object.FindAnyObjectByType<BottleShatter>(), "nothing flew off as it broke on the wall");
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ASwingThroughTheAirLeavesItWhole()
        {
#if UNITY_EDITOR
            Hold(GlassBottleKinds.Wine);
            yield return Frames(20);
            _body.GetComponent<SoulsSwordCombat>().RequestLight(0.0f);
            yield return Frames(70);
            Assert.AreEqual(BottleWeapon.DurabilityState.Intact, _bottle.State, "the bottle broke on nothing (or on the ground under the swing)");
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator HeldRightClickWindsTheArmBackAndAClickThrowsItFromTheHand()
        {
#if UNITY_EDITOR
            Hold(GlassBottleKinds.Soda);
            Dummy target = Target(new Vector3(0.0f, 1.3f, 7.0f));
            yield return Frames(30);
            var driver = _body.GetComponent<BottleThrower>().Driver;
            float carried = RightHand.position.y;
            _bottle.SetAiming(_body, true);
            yield return Frames(30, aiming: true);
            Assert.AreEqual(BottlePoseDriver.Move.Aiming, driver.Current, "the arm did not wind back");
            Assert.Greater(RightHand.position.y, carried + 0.3f, "the hand did not go up for the throw");
            Assert.Less(_body.transform.InverseTransformPoint(RightHand.position).z, 0.1f, "the hand did not go back for the throw");

            Vector3 chest = _body.transform.position + Vector3.up * 1.7f;
            _bottle.Throw(_body, chest, Vector3.forward);
            Assert.IsTrue(_bottle.IsThrowing);
            Assert.IsNull(Object.FindAnyObjectByType<ThrownBottle>(), "the bottle left the hand before the arm came forward");
            yield return Frames(Mathf.CeilToInt(BottlePoseDriver.ReleaseSeconds / Dt) + 2);
            ThrownBottle inTheAir = Object.FindAnyObjectByType<ThrownBottle>();
            Assert.IsNotNull(inTheAir, "the bottle never left the hand");
            Assert.IsTrue(_bottle == null, "the bottle is still in the hand");
            Assert.Less(Vector3.Distance(inTheAir.transform.position, RightHand.position), 1.6f, "it did not set off from the hand");
            Assert.Greater(inTheAir.GetComponent<Rigidbody>().linearVelocity.z, BottleWeapon.ThrowSpeedConst * 0.7f, "it is not thrown where it was aimed");
            Assert.Greater(inTheAir.GetComponent<Rigidbody>().angularVelocity.magnitude, 3.0f, "it does not tumble");

            for (int i = 0; i < 90 && target.Hits == 0; i++)
            {
                yield return Frames(1);
            }
            Assert.AreEqual(1, target.Hits, "the thrown bottle missed the body it was thrown at");
            Assert.AreEqual(CombatTuning.DamageFor(ThrownBottle.ThrowDamage, 0), target.LastDamage, 0.01f);
            yield return null;
            Assert.IsNull(Object.FindAnyObjectByType<ThrownBottle>(), "the bottle is still there after it hit");
            BottleShatter shatter = Object.FindAnyObjectByType<BottleShatter>();
            Assert.IsNotNull(shatter, "it vanished instead of shattering");
            Assert.AreEqual(shatter.Pieces.Length, shatter.Flying, "thrown, the whole bottle shatters: no half is left");
            Assert.IsNull(Object.FindAnyObjectByType<BottleWeapon>(), "a bottle was left to pick up");
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator LettingGoOfTheAimBringsTheArmBackToTheCarry()
        {
#if UNITY_EDITOR
            Hold(GlassBottleKinds.Beer);
            yield return Frames(30);
            var driver = _body.GetComponent<BottleThrower>().Driver;
            _bottle.SetAiming(_body, true);
            yield return Frames(30, aiming: true);
            _bottle.SetAiming(_body, false);
            yield return Frames(40);
            Assert.AreEqual(BottlePoseDriver.Move.None, driver.Current);
            Assert.Greater(_body.GetComponent<PlayerLocomotion>().RaisedCarryWeight, 0.95f, "the arm did not come back to the carry");
            Assert.IsTrue(_bottle != null && _bottle.State == BottleWeapon.DurabilityState.Intact);
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ThrownAtAWallItShattersWholeAndHurtsNothing()
        {
#if UNITY_EDITOR
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 2.0f, 4.0f);
            wall.transform.localScale = new Vector3(6.0f, 4.0f, 0.4f);
            _made.Add(wall);
            Dummy bystander = Target(new Vector3(0.0f, 1.3f, 6.0f));
            Physics.SyncTransforms();
            ThrownBottle thrown = ThrownBottle.Spawn(Load(GlassBottleKinds.ThrownPrefabPath(GlassBottleKinds.Wine)), new Vector3(0.0f, 2.0f, 0.0f),
                Quaternion.identity, Vector3.forward * BottleWeapon.ThrowSpeedConst, 0, null);
            Vector3 shatteredAt = Vector3.zero;
            thrown.Shattered += at => shatteredAt = at;
            for (int i = 0; i < 60 && thrown != null; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.IsTrue(thrown == null, "the bottle did not break on the wall");
            Assert.AreEqual(3.8f, shatteredAt.z, 0.3f, "it did not shatter at the wall");
            Assert.AreEqual(0, bystander.Hits);
            BottleShatter shatter = Object.FindAnyObjectByType<BottleShatter>();
            Assert.IsNotNull(shatter);
            Assert.AreEqual(shatter.Pieces.Length, shatter.Flying);
            // The pieces carry on along the wall and fall: none goes through it.
            for (int i = 0; i < 40; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            foreach (Rigidbody piece in shatter.Pieces)
            {
                Assert.Less(piece.position.z, 4.3f, "a piece went through the wall");
            }
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyThrowsAndBreaksTheSameBottleAndHurtsNoOne()
        {
#if UNITY_EDITOR
            _body = Player(new Vector3(0.0f, 0.05f, 0.0f));
            _bottle = Bottle(GlassBottleKinds.Wine);
            _bottle.MountCosmetic(_body);
            var copy = _body.GetComponent<BottleThrower>();
            Dummy target = Target(new Vector3(0.0f, 1.3f, 5.0f));
            yield return null;

            // Broken in the other player's hand.
            copy.PlayRemote(new AbilityEvent(BottleThrower.Broke, _body.transform.position, Vector3.forward, 0.0f, 0.0f, (int)BottleWeapon.DurabilityState.Broken));
            Assert.AreEqual(BottleWeapon.DurabilityState.Broken, _bottle.State, "the copy's bottle did not break");
            Assert.IsNotNull(Object.FindAnyObjectByType<BottleShatter>(), "nothing flew off the copy's bottle");

            // Aimed and thrown.
            copy.PlayRemote(new AbilityEvent(BottleThrower.Aimed, _body.transform.position, Vector3.zero, 0.0f, 0.0f, 1));
            Assert.IsTrue(copy.Driver.IsAiming, "the copy did not wind its arm back");
            copy.PlayRemote(new AbilityEvent(BottleThrower.Thrown, new Vector3(0.0f, 2.0f, 0.5f), Vector3.forward, BottleWeapon.ThrowSpeedConst, 0.0f,
                GlassBottleKinds.IndexOf(GlassBottleKinds.Wine)));
            ThrownBottle thrown = Object.FindAnyObjectByType<ThrownBottle>();
            Assert.IsNotNull(thrown, "the copy threw nothing");
            Assert.IsTrue(thrown.Harmless, "a copy's bottle would hurt: the thrower's machine dealt the blow");
            for (int i = 0; i < 80 && thrown != null; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.IsTrue(thrown == null, "the copy's bottle never landed");
            Assert.AreEqual(0, target.Hits, "a copy's bottle hurt someone");
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            yield break;
#endif
        }
    }
}
