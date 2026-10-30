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
    /// The Arcane Beam Staff on the real body (docs/STAFFS.md): held on a body it burns it a tick every fifth of a second, and nothing beside
    /// it or behind it (the beam stops on the first thing it meets); it drinks mana, and stops when let go or when the mana runs out; another
    /// player's copy shows the beam on its staff, running to what it meets.
    /// </summary>
    public sealed class ArcaneBeamStaffPlayTests
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
            foreach (StaffBeam beam in Object.FindObjectsByType<StaffBeam>(FindObjectsSortMode.None)) Object.Destroy(beam.gameObject);
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
            if (player.GetComponent<ArcaneBeamCaster>() == null)
            {
                player.AddComponent<ArcaneBeamCaster>();
            }
            return player;
#else
            return null;
#endif
        }

        private StaffItem Staff()
        {
#if UNITY_EDITOR
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/ArcaneBeamStaff.prefab"));
            Assert.IsNotNull(model, "no ArcaneBeamStaff.prefab -- rebuild the scene");
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

        /// <summary>
        /// The beam lands on what the crosshair is on, near or far, with the camera where the game's sits (behind, above and off to the
        /// right): aimed at a point a fixed distance ahead, it hit beside anything nearer or farther (owner, 2026-10-29: "the beam staff was
        /// hitting slightly to the side of the crosshair"). Every staff aims through the same PlayerInputReader.CrosshairDirection.
        /// </summary>
        [UnityTest]
        public IEnumerator TheBeamLandsOnWhatTheCrosshairIsOnNearOrFar()
        {
            GameObject player = Body(Vector3.zero);
            StaffItem staff = Staff();
            staff.OnEquip(player, ItemInstanceData.None);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            yield return Hold(player, staff, false, 0.3f);
            foreach (float distance in new[] { 5.0f, ArcaneBeamRules.Range - 3.0f })
            {
                Vector3 eye = player.transform.position + Vector3.up * 3.2f - player.transform.forward * 5.0f + player.transform.right * 0.87f;
                Vector3 onCrosshair = player.transform.position + Vector3.up * 2.0f + player.transform.forward * distance;
                Vector3 look = (onCrosshair - eye).normalized;
                Dummy post = Target(onCrosshair);
                post.transform.localScale = new Vector3(0.2f, 1.0f, 0.2f);   // thin: a beam off to the side misses it
                Physics.SyncTransforms();
                for (float t = 0.0f; t < 0.6f; t += Step)
                {
                    staff.Tick(true, eye, look, player, Step);
                    locomotion.Tick(new LocomotionInput { YawDegrees = player.transform.eulerAngles.y }, Step);
                    yield return null;
                }
                staff.Tick(false, eye, look, player, Step);
                Assert.Greater(post.Hits, 0, $"the beam missed what the crosshair was on {distance} m away");
                Object.Destroy(post.gameObject);
                yield return Hold(player, staff, false, 0.3f);
            }
        }

        [UnityTest]
        public IEnumerator HeldOnABodyItBurnsItAndNothingBesideOrBehindIt()
        {
            GameObject player = Body(Vector3.zero);
            StaffItem staff = Staff();
            staff.OnEquip(player, ItemInstanceData.None);
            var vitals = player.GetComponent<PlayerVitals>();
            Dummy ahead = Target(new Vector3(0.0f, 2.2f, 7.0f));
            Dummy beside = Target(new Vector3(3.0f, 2.2f, 7.0f));
            Dummy behind = Target(new Vector3(0.0f, 2.2f, 11.0f));
            yield return Hold(player, staff, false, 0.3f);
            float mana = vitals.Mana;
            yield return Hold(player, staff, true, 1.0f);
            var caster = player.GetComponent<ArcaneBeamCaster>();
            Assert.IsTrue(caster.IsBeaming, "held, the beam is not on");
            Assert.AreEqual(1.0f / ArcaneBeamRules.TickSeconds, ahead.Hits, 1.5f, "the body the beam is on should be burnt every fifth of a second");
            Assert.AreEqual(ahead.Hits * ArcaneBeamRules.DamagePerTick, ahead.Taken, 0.01f);
            Assert.AreEqual(0, beside.Hits, "the body beside the beam was burnt");
            Assert.AreEqual(0, behind.Hits, "the beam went through the first body to the one behind it");
            Assert.Less(vitals.Mana, mana - ArcaneBeamRules.ManaPerSecond * 0.8f, "the beam should drink mana");
            StaffBeam beam = player.GetComponentInChildren<StaffBeam>();
            Assert.IsNotNull(beam, "the beam is not drawn");
            Assert.IsTrue(beam.Lands, "the beam does not end on the body it burns");
            Assert.AreEqual(7.0f, beam.End.z, 0.5f, "the beam does not end where the body is");
        }

        [UnityTest]
        public IEnumerator ItStopsWhenLetGoOrWhenTheManaRunsOut()
        {
            GameObject player = Body(Vector3.zero);
            StaffItem staff = Staff();
            staff.OnEquip(player, ItemInstanceData.None);
            var caster = player.GetComponent<ArcaneBeamCaster>();
            yield return Hold(player, staff, true, 0.2f);
            Assert.IsTrue(caster.IsBeaming);
            yield return Hold(player, staff, false, 0.1f);
            Assert.IsFalse(caster.IsBeaming, "let go, the beam stays on");
            Assert.IsFalse(player.GetComponentInChildren<StaffBeam>().IsRunning, "let go, the beam is still drawn");

            var vitals = player.GetComponent<PlayerVitals>();
            vitals.TrySpendMana(vitals.Mana);
            yield return Hold(player, staff, true, 0.2f);
            Assert.IsFalse(caster.IsBeaming, "with no mana the beam came on");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyShowsTheBeamRunningToWhatItMeets()
        {
            GameObject copy = Body(new Vector3(10.0f, 0.0f, 0.0f));
            StaffItem staff = Staff();
            staff.MountCosmetic(copy);
            Target(new Vector3(10.0f, 2.2f, 6.0f));
            var caster = copy.GetComponent<ArcaneBeamCaster>();
            yield return null;
            Transform tip = StaffItem.TipOf(copy.GetComponent<PlayerLocomotion>());
            Assert.IsNotNull(tip, "setup: the copy holds no staff");
            Vector3 aim = (new Vector3(10.0f, 2.2f, 6.0f) - tip.position).normalized;
            caster.PlayRemote(new AbilityEvent(ArcaneBeamCaster.BeamStarted, tip.position, aim));
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }
            StaffBeam beam = copy.GetComponentInChildren<StaffBeam>();
            Assert.IsNotNull(beam, "the copy shows no beam");
            Assert.IsTrue(beam.IsRunning);
            Assert.IsTrue(beam.Lands, "the copy's beam does not end on what it meets");
            Assert.AreEqual(6.0f, beam.End.z, 0.6f);
            caster.PlayRemote(new AbilityEvent(ArcaneBeamCaster.BeamStopped, tip.position, Vector3.zero));
            Assert.IsFalse(beam.IsRunning, "the copy's beam did not stop with its owner's");
        }
    }
}
