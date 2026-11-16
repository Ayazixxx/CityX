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
    /// The flame staff on the real body (docs/STAFFS.md): held on a body in its cone it burns it every frame and nothing beside it, drinking mana;
    /// the fire is at the staff's head while it burns and goes out when the click is let go, or when the mana runs out; another player's copy
    /// shows the fire on its staff, pointed where the wearer points it.
    /// </summary>
    public sealed class FlameStaffPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private const string FlamePath = "Assets/CityX/Weapons/StaffFlame.prefab";
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
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
                _made.Add(ground);
            }
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.Euler(0.0f, yawDegrees, 0.0f));
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var caster = player.GetComponent<FlameStreamCaster>();
            if (caster == null)
            {
                caster = player.AddComponent<FlameStreamCaster>();
            }
            caster.FlamePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FlamePath);
            Assert.IsNotNull(caster.FlamePrefab, "no StaffFlame.prefab -- rebuild the scene");
            return player;
#else
            return null;
#endif
        }

        private StaffItem Staff()
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

        private Dummy Target(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = at;
            go.transform.localScale = new Vector3(0.6f, 1.0f, 0.6f);
            _made.Add(go);
            return go.AddComponent<Dummy>();
        }

        /// <summary>Holds (or not) the staff's click for <paramref name="seconds"/>, the crosshair straight ahead of the body.</summary>
        private static IEnumerator Hold(GameObject player, IEquippableStaff staff, bool held, float seconds)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = player.transform.position + Vector3.up * 2.4f - player.transform.forward * 4.0f;
                staff.Tick(held, eye, player.transform.forward, player, Step);
                locomotion.Tick(new LocomotionInput { YawDegrees = player.transform.eulerAngles.y }, Step);
                yield return null;
            }
        }

        private static FlameStreamLook FlameOn(GameObject body)
        {
            foreach (FlameStreamLook look in body.GetComponentsInChildren<FlameStreamLook>())
            {
                return look;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator HeldOnABodyInItsConeItBurnsItEveryTickWithNoBloodAndNothingBesideIt()
        {
            GameObject player = Body(Vector3.zero);
            StaffItem staff = Staff();
            staff.OnEquip(player, ItemInstanceData.None);
            var vitals = player.GetComponent<PlayerVitals>();
            // In the cone: the staff's head is held out at about chest height and the crosshair, straight ahead, is level with it.
            Dummy ahead = Target(new Vector3(0.0f, 2.2f, 6.0f));
            Dummy beside = Target(new Vector3(3.0f, 2.2f, 6.0f));
            Dummy far = Target(new Vector3(0.0f, 2.2f, FlameStreamRules.Range + 4.0f));
            yield return Hold(player, staff, false, 0.3f);
            float mana = vitals.Mana;
            yield return Hold(player, staff, true, 1.0f);
            Assert.AreEqual(FlameStreamRules.TicksIn(1.0f - Step), ahead.Hits, 1, "the body in the cone should burn a tick every 0.15 s");
            Assert.AreEqual(ahead.Hits * FlameStreamRules.DamagePerTick, ahead.Taken, 0.01f, "each tick is 36 a second's worth");
            Assert.IsFalse(ahead.Bled, "fire drew blood");
            Assert.AreEqual(0, beside.Hits, "the body beside the cone should be untouched");
            Assert.AreEqual(0, far.Hits, "the body past its reach should be untouched");
            Assert.Less(vitals.Mana, mana - FlameStreamRules.ManaPerSecond * 0.8f, "the fire should drink mana");
        }

        [UnityTest]
        public IEnumerator TheFireIsAtTheStaffsHeadWhileItBurnsAndGoesOutWhenLetGoOrOutOfMana()
        {
            GameObject player = Body(Vector3.zero);
            StaffItem staff = Staff();
            staff.OnEquip(player, ItemInstanceData.None);
            var caster = player.GetComponent<FlameStreamCaster>();
            var vitals = player.GetComponent<PlayerVitals>();
            yield return Hold(player, staff, false, 0.3f);
            Assert.IsNull(FlameOn(player), "fire before the click");

            yield return Hold(player, staff, true, 0.3f);
            Assert.IsTrue(caster.IsBurning);
            FlameStreamLook flame = FlameOn(player);
            Assert.IsNotNull(flame, "no fire while the click is held");
            Assert.IsTrue(flame.IsOn, "the fire is not burning");
            Assert.Less(Vector3.Distance(flame.transform.position, staff.Tip.position), 0.05f, "the fire is not at the staff's head");
            Assert.Greater(Vector3.Dot(flame.transform.forward, player.transform.forward), 0.9f, "the fire does not go where the staff points");
            Assert.Greater(flame.Light.intensity, 0.0f, "the fire casts no light");

            yield return Hold(player, staff, false, 0.1f);
            Assert.IsFalse(caster.IsBurning, "the fire did not go out when the click was let go");
            Assert.IsFalse(flame.IsOn);
            Assert.AreEqual(0.0f, flame.Light.intensity, 1e-4f, "the light stayed on");

            // Nearly out of mana: it burns for a moment, then goes out by itself.
            Assert.IsTrue(vitals.TrySpendMana(vitals.Mana - 2.0f));
            yield return Hold(player, staff, true, 0.1f);
            Assert.IsTrue(caster.IsBurning, "it should burn while there is mana for it");
            yield return Hold(player, staff, true, 0.5f);
            Assert.IsFalse(caster.IsBurning, "it stayed lit with no mana to feed it");
        }

        [UnityTest]
        public IEnumerator TheFireStopsAtAWallSplashingOverItAndBlackensIt()
        {
            // docs/FIRE.md, "The fire spells": the stream's gas dies where it meets the wall, flames lick up there, and the wall is sooted.
            GameObject player = Body(Vector3.zero);
            StaffItem staff = Staff();
            staff.OnEquip(player, ItemInstanceData.None);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 2.0f, 4.5f);
            wall.transform.localScale = new Vector3(8.0f, 4.0f, 0.4f);
            _made.Add(wall);
            Physics.SyncTransforms();
            const float face = 4.3f;
            yield return Hold(player, staff, false, 0.3f);
            yield return Hold(player, staff, true, 1.0f);

            ParticleSystem.Particle[] jet = FireSpellLookPlayTests.Live("Jet");
            Assert.Greater(jet.Length, 20, "no fire in the stream");
            int atTheWall = 0;
            foreach (ParticleSystem.Particle puff in jet)
            {
                Assert.Less(puff.position.z, face + 0.4f, "the stream's fire went through the wall");
                if (puff.position.z > face - 1.2f)
                {
                    atTheWall++;
                }
            }
            Assert.Greater(atTheWall, 3, "the stream's fire does not reach the wall");
            int licking = 0;
            foreach (ParticleSystem.Particle flame in FireSpellLookPlayTests.Live("Flames"))
            {
                if (flame.position.z > face - 1.0f && flame.position.z < face + 0.2f)
                {
                    licking++;
                }
            }
            Assert.Greater(licking, 3, "no flames licking up where the stream meets the wall");
            int soot = 0;
            foreach (GroundMark mark in Object.FindObjectsByType<GroundMark>(FindObjectsSortMode.None))
            {
                if (mark.name == "SootMark" && Vector3.Dot(mark.transform.up, Vector3.back) > 0.95f && Mathf.Abs(mark.transform.position.z - face) < 0.05f)
                {
                    soot++;
                }
            }
            Assert.Greater(soot, 0, "the wall the stream played on is not blackened");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyShowsTheFireOnItsStaffPointedWhereTheWearerPointsIt()
        {
            GameObject wearer = Body(Vector3.zero);
            GameObject copy = Body(new Vector3(10.0f, 0.0f, 0.0f), 90.0f);
            StaffItem staff = Staff();
            staff.OnEquip(wearer, ItemInstanceData.None);
            StaffItem copyStaff = Staff();
            copyStaff.MountCosmetic(copy);
            var caster = wearer.GetComponent<FlameStreamCaster>();
            var copyCaster = copy.GetComponent<FlameStreamCaster>();
            caster.Fired += copyCaster.PlayRemote;
            yield return Hold(wearer, staff, false, 0.3f);

            yield return Hold(wearer, staff, true, 0.3f);
            FlameStreamLook copyFlame = FlameOn(copy);
            Assert.IsNotNull(copyFlame, "the copy shows no fire");
            Assert.IsTrue(copyFlame.IsOn, "the copy's fire is not burning");
            Assert.Less(Vector3.Distance(copyFlame.transform.position, copyStaff.Tip.position), 0.05f, "the copy's fire is not at its staff's head");
            Assert.Greater(Vector3.Dot(copyFlame.transform.forward, wearer.transform.forward), 0.9f, "the copy's fire does not go where the wearer points");
            int nearCopy = 0;
            foreach (ParticleSystem.Particle puff in FireSpellLookPlayTests.Live("Jet"))
            {
                // On the copy's own stream: ahead of its staff along where the wearer points, not off on the wearer's stream 10 m away.
                Vector3 off = puff.position - copyStaff.Tip.position;
                if (Vector3.Dot(off, wearer.transform.forward) > -0.5f && Vector3.ProjectOnPlane(off, wearer.transform.forward).magnitude < 2.0f)
                {
                    nearCopy++;
                }
            }
            Assert.Greater(nearCopy, 5, "the copy pours no fire from its own staff");

            yield return Hold(wearer, staff, false, 0.1f);
            Assert.IsFalse(copyFlame.IsOn, "the copy's fire did not go out with the wearer's");
            caster.Fired -= copyCaster.PlayRemote;
        }
    }
}
