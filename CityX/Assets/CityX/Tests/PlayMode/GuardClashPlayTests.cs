using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A blade striking a guard (owner, 2026-11-04: "it should feel impactful hitting an opponent who is blocking and when you block an attack the opponents
    /// weapon should bounce off with a loud noise"), on the real rig with two fighters: both held still at the blow, the attacker's blade stopped on the guard and
    /// then thrown back, the blocker's guard knocked back, the clash loud where the game's camera hears it, and all of it on other players' copies.
    /// </summary>
    public sealed class GuardClashPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        private readonly List<GameObject> _objects = new List<GameObject>();
        private int _capturedFramerate;
        private GameObject _attacker;
        private GameObject _blocker;
        private SoulsSwordCombat _attackerCombat;
        private SoulsSwordCombat _blockerCombat;
        private bool _blockHeld;

        [SetUp]
        public void SetUp()
        {
            _capturedFramerate = Time.captureFramerate;
            Time.captureFramerate = 60;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = _capturedFramerate;
            foreach (GameObject thing in _objects)
            {
                if (thing != null) Object.Destroy(thing);
            }
            _objects.Clear();
            // A clash outlives its fight by up to two seconds: not into the next test's count.
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (source.gameObject.name == BlockDeflectEffect.ClashSoundName)
                {
                    Object.Destroy(source.gameObject);
                }
            }
        }

#if UNITY_EDITOR
        private GameObject Fighter(Vector3 position, float yaw, out SoulsSwordCombat combat)
        {
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab");
            GameObject body = Object.Instantiate(playerPrefab, position, Quaternion.Euler(0.0f, yaw, 0.0f));
            _objects.Add(body);
            body.GetComponent<PlayerInputReader>().enabled = false;
            var vitals = body.GetComponent<PlayerVitals>();
            vitals.InfiniteStamina = true;
            vitals.Roll = () => 1.0f;   // no outright blocks (Block Chance)
            GameObject sword = Object.Instantiate(swordPrefab, position + new Vector3(10.0f, 1.0f, 10.0f), Quaternion.identity);
            _objects.Add(sword);
            sword.GetComponent<SwordWeapon>().OnEquip(body, CityX.Items.ItemInstanceData.None);
            combat = body.GetComponent<SoulsSwordCombat>();
            return body;
        }

        private void Ground()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(ground);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        /// <summary>Two fighters face to face, in reach, one of them with the guard long up (past the parry's instant).</summary>
        private IEnumerator Duel()
        {
            Ground();
            _attacker = Fighter(new Vector3(0.0f, 0.05f, 0.0f), 0.0f, out _attackerCombat);
            _blocker = Fighter(new Vector3(0.0f, 0.05f, 1.9f), 180.0f, out _blockerCombat);
            _blockHeld = true;
            yield return Run(0.8f);
        }

        private void TickOnce()
        {
            var idle = new LocomotionInput { Sword1HEquipped = true };
            _blockerCombat.SetBlockHeld(_blockHeld);
            foreach ((GameObject body, SoulsSwordCombat combat) in new[] { (_attacker, _attackerCombat), (_blocker, _blockerCombat) })
            {
                combat.Tick(Dt);
                body.GetComponent<PlayerVitals>().TickHitstun(Dt);
                body.GetComponent<PlayerLocomotion>().Tick(idle, Dt);
            }
        }

        private IEnumerator Run(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Dt)
            {
                TickOnce();
                yield return null;
            }
        }

        /// <summary>The attacker swings until the blow has met the guard (<see cref="SoulsSwordCombat.Rebounded"/>).</summary>
        private IEnumerator SwingIntoTheGuard()
        {
            bool rebounded = false;
            void OnRebound() => rebounded = true;
            _attackerCombat.Rebounded += OnRebound;
            _attackerCombat.RequestLight(0.0f);
            for (int frames = 0; !rebounded; frames++)
            {
                Assert.Less(frames, 90, "the blade never met the guard");
                TickOnce();
                yield return null;
            }
            _attackerCombat.Rebounded -= OnRebound;
        }

        private static Vector3 Tip(GameObject body) => body.GetComponent<PlayerLocomotion>().HeldSword.BladeTipWorld;

        private static List<AudioSource> ClashSounds()
        {
            var found = new List<AudioSource>();
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (source.gameObject.name == BlockDeflectEffect.ClashSoundName)
                {
                    found.Add(source);
                }
            }
            return found;
        }
#endif

        [UnityTest]
        public IEnumerator TheBladeStopsOnTheGuardThenIsThrownBackAndTheAttackerWithIt()
        {
#if UNITY_EDITOR
            yield return Duel();
            yield return SwingIntoTheGuard();
            var driver = _attacker.GetComponent<PlayerAnimationDriver>();
            var locomotion = _attacker.GetComponent<PlayerLocomotion>();
            Assert.IsTrue(driver.SwordPoser.IsRebounding, "the rebound did not start");
            Assert.IsTrue(locomotion.IsImpactFrozen, "the attacker is not held still at the blow");
            float standing = _attacker.transform.position.z;

            // Stopped on the guard for a moment, the blades crossing, the point short of the blocker's body.
            yield return Run(BlockDeflectEffect.ClashFreezeSeconds * 0.6f);
            Assert.IsTrue(locomotion.IsImpactFrozen, "the hold let go too soon");
            ISwordBlade attackerBlade = locomotion.HeldSword;
            ISwordBlade guardBlade = _blocker.GetComponent<PlayerLocomotion>().HeldSword;
            Vector3 guardPoint = Vector3.Lerp(guardBlade.BladeBaseWorld, guardBlade.BladeTipWorld, BlockDeflectEffect.BladeMeetShare);
            float toGuard = float.MaxValue;
            Vector3 blocker = _blocker.transform.position;
            for (float along = 0.0f; along <= 1.0f; along += 0.05f)
            {
                Vector3 point = Vector3.Lerp(attackerBlade.BladeBaseWorld, attackerBlade.BladeTipWorld, along);
                toGuard = Mathf.Min(toGuard, Vector3.Distance(point, guardPoint));
                bool atTorsoHeight = point.y > blocker.y + BodyRig.HipY && point.y < blocker.y + BodyRig.ShoulderY;
                float fromTheAxis = new Vector2(point.x - blocker.x, point.z - blocker.z).magnitude;
                Assert.IsFalse(atTorsoHeight && fromTheAxis < 0.3f, $"the blade goes through the blocker's body, {along:0.00} along it");
                Assert.Greater(Vector3.Distance(point, blocker + Vector3.up * BodyRig.HeadCentreY), FusedBodyShape.HeadRadius, $"the blade goes through the blocker's head, {along:0.00} along it");
            }
            Assert.Less(toGuard, 0.45f, "the blade stopped nowhere near the guard");
            float tipStopped = Tip(_attacker).y;

            yield return Run(0.3f);
            Assert.AreNotEqual(tipStopped, Tip(_attacker).y);
            Assert.Greater(Tip(_attacker).y, tipStopped + 0.2f, "the blade was not thrown up off the guard");
            Assert.Less(Tip(_attacker).z, attackerBlade.BladeBaseWorld.z, "the blade was not thrown back");
            float knockedBack = standing - _attacker.transform.position.z;
            Assert.Greater(knockedBack, SoulsSwordCombat.ReboundShoveMetres * 0.7f, $"the attacker was knocked back only {knockedBack:0.00} m");
            Assert.LessOrEqual(knockedBack, SoulsSwordCombat.ReboundShoveMetres + 0.05f, "the attacker is thrown out of the blocker's reach");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheBlockersGuardIsKnockedBackAndTheBodySlides()
        {
#if UNITY_EDITOR
            yield return Duel();
            Vector3 restingTip = Tip(_blocker);
            float standing = _blocker.transform.position.z;
            yield return SwingIntoTheGuard();
            var poser = _blocker.GetComponent<PlayerAnimationDriver>().SwordPoser;
            Assert.IsTrue(poser.IsGuardJolting, "the guard does not take the blow");
            Assert.IsTrue(_blocker.GetComponent<PlayerLocomotion>().IsImpactFrozen, "the blocker is not held still at the blow");

            float most = 0.0f;
            for (float t = 0.0f; t < 0.3f; t += Dt)
            {
                TickOnce();
                yield return null;
                most = Mathf.Max(most, Vector3.Distance(Tip(_blocker), restingTip));
            }
            Assert.Greater(most, 0.25f, "the guard is hardly moved by the blow");
            float slid = _blocker.transform.position.z - standing;
            Assert.AreEqual(BlockDeflectEffect.GuardShoveMetres, slid, 0.05f, "the blocker does not slide back from the blow");
            yield return Run(0.3f);
            Assert.IsFalse(poser.IsGuardJolting, "the guard never comes back");
            Assert.IsTrue(poser.IsGuarding, "the blow knocked the guard down");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AHeavyBlowDrivesTheGuardBackFurther()
        {
#if UNITY_EDITOR
            yield return Duel();
            var vitals = _blocker.GetComponent<PlayerVitals>();
            float standing = _blocker.transform.position.z;
            DamageContext.Strike(vitals, 5.0f, _attacker.transform.position, 0.4f, DamageKind.Physical, 0.0f);
            yield return Run(0.4f);
            float light = _blocker.transform.position.z - standing;
            standing = _blocker.transform.position.z;
            using (DamageContext.LandingHeavily(true))
            {
                DamageContext.Strike(vitals, 5.0f, _attacker.transform.position, 0.4f, DamageKind.Physical, 0.0f);
            }
            yield return Run(0.4f);
            float heavy = _blocker.transform.position.z - standing;
            Assert.Greater(light, 0.05f);
            Assert.Greater(heavy, light * 1.5f, $"a heavy blow slid the guard {heavy:0.00} m, a light one {light:0.00} m");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AParryStandsFirmOnlyTheAttackerMoves()
        {
#if UNITY_EDITOR
            yield return Duel();
            _blockHeld = false;
            yield return Run(0.4f);
            _blockHeld = true;
            TickOnce();
            yield return null;
            var vitals = _blocker.GetComponent<PlayerVitals>();
            Assert.Greater(vitals.PerfectGuardRemaining, 0.0f, "not in the parry's instant");
            float standing = _blocker.transform.position.z;
            var effect = _blocker.GetComponent<BlockDeflectEffect>();
            int clashes = effect.ClashesShown;

            HitResponse response = DamageContext.Strike(vitals, 5.0f, _attacker.transform.position, 0.4f, DamageKind.Physical, 0.0f);

            Assert.AreEqual(HitResponse.Blocked, response);
            Assert.AreEqual(clashes + 1, effect.ClashesShown, "a parry clashes too");
            Assert.IsFalse(_blocker.GetComponent<PlayerAnimationDriver>().SwordPoser.IsGuardJolting, "a caught blow knocked the guard back");
            yield return Run(0.3f);
            Assert.AreEqual(standing, _blocker.transform.position.z, 0.02f, "a caught blow moved the one who caught it");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheClashIsLoudWhereTheCameraHearsItWithAThunkUnderIt()
        {
#if UNITY_EDITOR
            yield return Duel();
            yield return SwingIntoTheGuard();
            List<AudioSource> sounds = ClashSounds();
            Assert.AreEqual(2, sounds.Count, "a clash is the recording and the thunk");
            var clips = new List<AudioClip>();
            foreach (AudioSource source in sounds)
            {
                clips.Add(source.clip);
                Assert.IsTrue(source.isPlaying);
                Assert.Greater(source.minDistance, 6.0f, "the camera is 5 m behind the player: the clash fades before it gets there");
                Assert.GreaterOrEqual(source.volume, 0.8f);
                Assert.AreEqual(1.0f, source.spatialBlend, 0.001f, "a far-off fight should be quieter");
            }
            CollectionAssert.Contains(clips, BlockDeflectEffect.ClashBody());
            Assert.IsTrue(clips.Exists(clip => System.Array.IndexOf(BlockDeflectEffect.Clashes(), clip) >= 0), "the recording did not play");
            Assert.IsNotNull(GameObject.Find("AttackGlint"), "no flash where the blades met");
            yield return Run(BlockDeflectEffect.ClashBody().length + 0.3f);
            foreach (AudioSource source in ClashSounds())
            {
                Assert.AreNotEqual(BlockDeflectEffect.ClashBody(), source.clip, "the thunk's object was left behind");
            }
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ABulletGlancingOffIsStillTheSmallTing()
        {
#if UNITY_EDITOR
            yield return Duel();
            var vitals = _blocker.GetComponent<PlayerVitals>();
            vitals.Roll = () => 0.0f;
            float standing = _blocker.transform.position.z;
            DamageContext.Strike(vitals, 5.0f, _attacker.transform.position, 0.0f, DamageKind.Bullet, 0.0f);
            Assert.AreEqual(0, ClashSounds().Count, "a bullet clashed like a blade");
            Assert.IsFalse(_blocker.GetComponent<PlayerAnimationDriver>().SwordPoser.IsGuardJolting);
            yield return Run(0.3f);
            Assert.AreEqual(standing, _blocker.transform.position.z, 0.02f, "a bullet shoved the guard");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ABladeStoppedOutrightByBlockChanceClashesAndBouncesToo()
        {
#if UNITY_EDITOR
            yield return Duel();
            _blocker.GetComponent<PlayerVitals>().Roll = () => 0.0f;
            yield return SwingIntoTheGuard();
            Assert.AreEqual(2, ClashSounds().Count, "the stopped blade did not clash");
            Assert.IsTrue(_attacker.GetComponent<PlayerAnimationDriver>().SwordPoser.IsRebounding, "the stopped blade did not bounce");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopiesShowItAllAndAreNotMovedByIt()
        {
#if UNITY_EDITOR
            yield return Duel();
            var blockerDriver = _blocker.GetComponent<PlayerAnimationDriver>();
            float standing = _blocker.transform.position.z;
            int clashes = _blocker.GetComponent<BlockDeflectEffect>().ClashesShown;
            blockerDriver.PlayRemoteState(BlockDeflectEffect.ClashStateName, 1.0f, 0.0f);
            Assert.AreEqual(clashes + 1, _blocker.GetComponent<BlockDeflectEffect>().ClashesShown, "a copy shows the clash");
            Assert.IsTrue(blockerDriver.SwordPoser.IsGuardJolting, "the copy's guard does not take the blow");
            Assert.IsTrue(_blocker.GetComponent<PlayerLocomotion>().IsImpactFrozen, "the copy is not held still");
            Assert.AreEqual(2, ClashSounds().Count, "the copy's clash is as loud");

            var attackerDriver = _attacker.GetComponent<PlayerAnimationDriver>();
            attackerDriver.PlayRemoteState(SwordStrikes.Rebound1HName, 1.0f, 0.0f);
            Assert.IsTrue(attackerDriver.SwordPoser.IsRebounding, "the copy's blade does not bounce");
            Assert.IsTrue(_attacker.GetComponent<PlayerLocomotion>().IsImpactFrozen, "the copy of the attacker is not held still");
            Vector3 attackerAt = _attacker.transform.position;
            yield return Run(0.3f);
            Assert.AreEqual(standing, _blocker.transform.position.z, 0.02f, "a copy's place is the network's: the clash must not move it");
            Assert.AreEqual(attackerAt.z, _attacker.transform.position.z, 0.02f, "a copy's place is the network's: the rebound must not move it");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
