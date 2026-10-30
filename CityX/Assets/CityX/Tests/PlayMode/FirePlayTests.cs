using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>Fire in a scene (docs/FIRE.md): lit by the flame staff, running along poured gasoline, burning whoever stands in it, seen by other players.</summary>
    public sealed class FirePlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private readonly List<GameObject> _made = new List<GameObject>();

        private sealed class Dummy : MonoBehaviour, IDamageable
        {
            public float Taken;
            public int Hits;
            public bool Bled;

            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Taken += amount;
                Hits++;
                Bled |= !DamageContext.Bloodless;
                return HitResponse.Landed;
            }
        }

        [SetUp]
        public void SetUp()
        {
            FireScene.Clear();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            _made.Add(ground);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            FireScene.Clear();
            if (FireScene.Instance != null)
            {
                Object.Destroy(FireScene.Instance.gameObject);
            }
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private GameObject Body(Vector3 at, float yawDegrees = 0.0f)
        {
#if UNITY_EDITOR
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.Euler(0.0f, yawDegrees, 0.0f));
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.GetComponent<PlayerVitals>().Revive();
            player.GetComponent<PlayerLocomotion>().Bind();
            _made.Add(player);
            return player;
#else
            return null;
#endif
        }

        private Dummy Target(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = at + Vector3.up * 1.0f;
            Physics.SyncTransforms();
            _made.Add(go);
            return go.AddComponent<Dummy>();
        }

        /// <summary>Gasoline along +Z from <paramref name="from"/>, a cell at a time.</summary>
        private static void Trail(Vector3 from, int cells, float litresEach = 0.15f)
        {
            for (int i = 0; i < cells; i++)
            {
                FireScene.Pour(from + Vector3.forward * (i * FireRules.CellMetres), litresEach);
            }
        }

        private StaffItem FlameStaff()
        {
#if UNITY_EDITOR
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/FlameStaff.prefab"));
            Assert.IsNotNull(model, "no FlameStaff.prefab -- rebuild the scene");
            _made.Add(model);
            return model.GetComponent<StaffItem>();
#else
            return null;
#endif
        }

        private static IEnumerator Frames(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Step)
            {
                yield return null;
            }
        }

        /// <summary>Made when the first fire was lit, the noise stopped the game for a tenth of a second: it is worked out as the game starts.</summary>
        [UnityTest]
        public IEnumerator TheFiresNoiseIsWorkedOutAsTheGameStartsNotWhenTheFirstFireIsLit()
        {
            for (float waited = 0.0f; waited < 3.0f && !FireSprites.NoiseWorkedOut; waited += Time.unscaledDeltaTime)
            {
                yield return null;
            }
            Assert.IsTrue(FireSprites.NoiseWorkedOut, "the fire's noise was not worked out by itself as the game started");
        }

        [UnityTest]
        public IEnumerator TheFlameStaffLightsAPouredTrailAndTheFireRunsAlongIt()
        {
            GameObject caster = Body(Vector3.zero);
            if (caster.GetComponent<FlameStreamCaster>() == null)
            {
                caster.AddComponent<FlameStreamCaster>();   // the plain test body has no abilities wired
            }
            StaffItem staff = FlameStaff();
            staff.OnEquip(caster, ItemInstanceData.None);
            var locomotion = caster.GetComponent<PlayerLocomotion>();

            Vector3 start = new Vector3(0.0f, 0.0f, 3.0f);
            Trail(start, 9);
            Vector3 farEnd = start + Vector3.forward * (8 * FireRules.CellMetres);
            Assert.Greater(FireMarks.GasolineCount, 6, "the poured trail shows no gasoline on the ground");
            Assert.AreEqual(0, FireScene.BurningCount);

            // The staff held on the trail's near end for a second and a half.
            for (float t = 0.0f; t < 1.5f; t += Step)
            {
                Vector3 eye = caster.transform.position + Vector3.up * 2.4f - caster.transform.forward * 4.0f;
                staff.Tick(true, eye, (start - eye).normalized, caster, Step);
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }
            staff.CancelCharge(caster);
            Assert.Greater(FireScene.BurningCount, 0, "the flame staff lit nothing");
            Assert.IsTrue(FireScene.Instance.Field.TryBurningUnder(start, out _), "the trail's near end is not burning");

            yield return Frames(7.0f);
            Assert.IsTrue(FireScene.Instance.Field.TryBurningUnder(farEnd, out FireCell far), "the fire did not run along the trail to its far end");
            Assert.IsTrue(far.Gasoline);
            Assert.Greater(FireMarks.ScorchCount, 0, "burning ground leaves no black spot");

            yield return Frames(FireRules.TrailFuelSeconds + FireRules.BareFuelSeconds + 4.0f);
            Assert.AreEqual(0, FireScene.BurningCount, "the trail never burned out");
            Assert.GreaterOrEqual(FireScene.Instance.Field.CountBurned(), 9, "not every cell of the trail is scorched");
            Assert.AreEqual(0, FireMarks.GasolineCount, "gasoline is still on the ground after it burned off");
        }

        [UnityTest]
        public IEnumerator StandingInTheFireBurnsAndKeepsBurningAfterLeaving()
        {
            Vector3 spot = new Vector3(6.0f, 0.0f, 6.0f);
            Dummy dummy = Target(spot);
            FireScene.Pour(spot, 1.0f);
            Assert.Greater(FireScene.Ignite(spot, 0.3f, true, null), 0, "the puddle did not light");

            yield return Frames(1.0f);
            Assert.Greater(dummy.Hits, 0, "standing in the fire did not burn");
            Assert.IsFalse(dummy.Bled, "fire drew blood");
            Assert.IsTrue(FireScene.IsBurning(dummy), "the body in the fire is not on fire");
            float inFire = dummy.Taken;

            // Out of the fire: still burning a while, then not.
            dummy.transform.position = new Vector3(60.0f, 1.0f, 60.0f);
            Physics.SyncTransforms();
            yield return Frames(2.0f);
            Assert.Greater(dummy.Hits, 1, "the body stopped burning the moment it left the fire");
            Assert.Greater(dummy.Taken, inFire, "no afterburn damage");
            yield return Frames(FireRules.AfterburnSeconds);
            Assert.IsFalse(FireScene.IsBurning(dummy), "the body never stopped burning");
            int hits = dummy.Hits;
            yield return Frames(1.5f);
            Assert.AreEqual(hits, dummy.Hits, "a body out of the fire for long enough still takes burns");
        }

        [UnityTest]
        public IEnumerator WaterPutsABurningBodyOut()
        {
            Vector3 spot = new Vector3(-6.0f, 0.0f, 6.0f);
            Dummy dummy = Target(spot);
            FireScene.Pour(spot, 1.0f);
            FireScene.Ignite(spot, 0.3f, true, null);
            yield return Frames(0.6f);
            Assert.IsTrue(FireScene.IsBurning(dummy));
            dummy.transform.position = new Vector3(60.0f, 1.0f, -60.0f);
            Physics.SyncTransforms();
            FireScene.DouseBody(dummy);
            int hits = dummy.Hits;
            yield return Frames(1.0f);
            Assert.IsFalse(FireScene.IsBurning(dummy));
            Assert.AreEqual(hits, dummy.Hits, "a doused body kept burning");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersPourAndFireShowHereAndBurnNobodyUntilThisMachineLightsIt()
        {
            GameObject copy = Body(new Vector3(20.0f, 0.0f, 0.0f));
            var sync = copy.GetComponent<FireSync>();
            if (sync == null)
            {
                sync = copy.AddComponent<FireSync>();
            }
            Vector3 spot = new Vector3(24.0f, 0.0f, 4.0f);
            Dummy dummy = Target(spot);

            sync.PlayRemote(new AbilityEvent(FireSync.Poured, spot, Vector3.up, 0.8f));
            Assert.Greater(FireScene.Instance.Field.LitresUnder(spot), FireRules.MinLitres, "the other player's gasoline is not on the ground here");
            sync.PlayRemote(new AbilityEvent(FireSync.Lit, spot, Vector3.up, 0.5f, 0.0f, 1));
            Assert.IsTrue(FireScene.Instance.Field.TryBurningUnder(spot, out FireCell cell), "the other player's fire does not burn here");
            Assert.IsTrue(cell.Cosmetic, "another machine's fire counts as this machine's");

            yield return Frames(1.5f);
            Assert.AreEqual(0, dummy.Hits, "another machine's fire burned a body on this one (its machine deals that damage)");

            // This machine lights the same spot: now it is its own fire, and it burns.
            var events = new List<AbilityEvent>();
            ((IReplicatedAbility)sync).Fired += events.Add;
            FireScene.Ignite(spot, 0.5f, true, copy);
            Assert.IsFalse(cell.Cosmetic, "lighting the spot ourselves did not make the fire our own");
            Assert.IsTrue(events.Exists(e => e.Kind == FireSync.Lit), "this machine's lighting was not told to other players");
            yield return Frames(1.0f);
            Assert.Greater(dummy.Hits, 0, "our own fire burns nobody");

            events.Clear();
            FireScene.Pour(spot + Vector3.right * 2.0f, 0.2f);
            yield return Frames(FireScene.PourSendSeconds + 0.1f);
            Assert.IsTrue(events.Exists(e => e.Kind == FireSync.Poured && e.A > 0.1f), "this machine's pour was not told to other players");
        }
    }
}
