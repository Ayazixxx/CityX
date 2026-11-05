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
    /// The staffs that are held (water, flame, frost, the arcane and healing beams) with the mana gone, and with the click tapped: neither
    /// may make one hit oftener than holding it on a full bar does. Out of mana with the click kept down, mana trickling back started the
    /// stream again every few frames, and every start landed its first tick at once: an empty staff hit several times as hard as a full one,
    /// and harder the higher the frame rate. Tapping the click did the same on a full bar, for a frame's mana a tick.
    /// </summary>
    public sealed class HeldStreamManaPlayTests
    {
        private const float Step = 1.0f / 60.0f;

        private readonly List<GameObject> _made = new List<GameObject>();

        private sealed class Dummy : MonoBehaviour, IDamageable
        {
            public int Hits;

            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Hits++;
                return HitResponse.Landed;
            }
        }

        /// <summary>One held staff: its prefab, the caster a body needs for it, the event that says its stream stopped, and its numbers.</summary>
        private sealed class Held
        {
            public string StaffPrefab;
            public System.Type Caster;
            public int StoppedKind;
            public float ManaPerSecond;
            public float TickSeconds;
            public bool Heals;
        }

        private static readonly Held Water = new Held
        {
            StaffPrefab = "Assets/CityX/Weapons/WaterStaff.prefab", Caster = typeof(WaterJetCaster), StoppedKind = WaterJetCaster.JetStopped,
            ManaPerSecond = WaterJetRules.ManaPerSecond, TickSeconds = WaterJetRules.TickSeconds,
        };

        private static readonly Held Flame = new Held
        {
            StaffPrefab = "Assets/CityX/Weapons/FlameStaff.prefab", Caster = typeof(FlameStreamCaster), StoppedKind = FlameStreamCaster.StreamStopped,
            ManaPerSecond = FlameStreamRules.ManaPerSecond, TickSeconds = FlameStreamRules.TickSeconds,
        };

        private static readonly Held Frost = new Held
        {
            StaffPrefab = "Assets/CityX/Weapons/FrostStaff.prefab", Caster = typeof(FrostSprayCaster), StoppedKind = FrostSprayCaster.SprayStopped,
            ManaPerSecond = FrostRules.ManaPerSecond, TickSeconds = FrostRules.TickSeconds,
        };

        private static readonly Held Arcane = new Held
        {
            StaffPrefab = "Assets/CityX/Weapons/ArcaneBeamStaff.prefab", Caster = typeof(ArcaneBeamCaster), StoppedKind = ArcaneBeamCaster.BeamStopped,
            ManaPerSecond = ArcaneBeamRules.ManaPerSecond, TickSeconds = ArcaneBeamRules.TickSeconds,
        };

        private static readonly Held Healing = new Held
        {
            StaffPrefab = "Assets/CityX/Weapons/HealingStaff.prefab", Caster = typeof(HealingBeamCaster), StoppedKind = HealingBeamCaster.LetGo,
            ManaPerSecond = HealingBeamRules.ManaPerSecond, TickSeconds = HealingBeamRules.TickSeconds, Heals = true,
        };

        /// <summary>A body holding one of the staffs on something 6 m ahead of it, and the count of what the staff has done to it.</summary>
        private sealed class Stand
        {
            public GameObject Wielder;
            public StaffItem Staff;
            public PlayerVitals Vitals;
            public PlayerLocomotion Locomotion;
            public Vector3 Eye;
            public Vector3 Look;
            public System.Func<int> Ticks;
            public int Stops;
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
            foreach (WaterJet jet in Object.FindObjectsByType<WaterJet>(FindObjectsSortMode.None)) Object.Destroy(jet.gameObject);
            foreach (FrostSpray spray in Object.FindObjectsByType<FrostSpray>(FindObjectsSortMode.None)) Object.Destroy(spray.gameObject);
        }

        private GameObject Body(Vector3 at, bool player)
        {
#if UNITY_EDITOR
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            _made.Add(body);
            body.GetComponent<PlayerInputReader>().enabled = player;   // (on, with no keyboard here, it does nothing: but the body is then a player's)
            return body;
#else
            return null;
#endif
        }

        private Stand Make(Held held)
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _made.Add(ground);

            var stand = new Stand { Wielder = Body(Vector3.zero, player: false) };
            if (stand.Wielder.GetComponent(held.Caster) == null)
            {
                stand.Wielder.AddComponent(held.Caster);
            }
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(held.StaffPrefab));
            Assert.IsNotNull(model, $"no {held.StaffPrefab} -- rebuild the scene");
            _made.Add(model);
            stand.Staff = model.GetComponent<StaffItem>();
            stand.Staff.OnEquip(stand.Wielder, ItemInstanceData.None);
            stand.Vitals = stand.Wielder.GetComponent<PlayerVitals>();
            stand.Locomotion = stand.Wielder.GetComponent<PlayerLocomotion>();
            ((IReplicatedAbility)stand.Wielder.GetComponent(held.Caster)).Fired += evt =>
            {
                if (evt.Kind == held.StoppedKind)
                {
                    stand.Stops++;
                }
            };

            Vector3 aimAt;
            if (held.Heals)
            {
                GameObject friend = Body(new Vector3(0.0f, 0.0f, 6.0f), player: true);
                var friendVitals = friend.GetComponent<PlayerVitals>();
                friendVitals.TakeDamage(70.0f);
                float hurt = friendVitals.Health;
                stand.Ticks = () => Mathf.RoundToInt((friendVitals.Health - hurt) / HealingBeamRules.HealPerTick);
                aimAt = HealingBeamCaster.ChestOf(friend.transform);
            }
            else
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                post.transform.position = new Vector3(0.0f, 2.2f, 6.0f);
                post.transform.localScale = new Vector3(0.6f, 1.0f, 0.6f);
                _made.Add(post);
                Dummy dummy = post.AddComponent<Dummy>();
                stand.Ticks = () => dummy.Hits;
                aimAt = post.transform.position;
            }
            Physics.SyncTransforms();
            stand.Eye = new Vector3(0.0f, 2.4f, -4.0f);
            stand.Look = (aimAt - stand.Eye).normalized;
            return stand;
#else
            return null;
#endif
        }

        /// <summary>One frame of the game with the click down or up: the staff, the body, and the mana coming back as it does in play.</summary>
        private static void Frame(Stand stand, bool clickDown)
        {
            stand.Staff.Tick(clickDown, stand.Eye, stand.Look, stand.Wielder, Step);
            stand.Locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
            stand.Vitals.TickMana(Step);
        }

        private IEnumerator StaysOffUntilItsManaIsBack(Held held)
        {
            Stand stand = Make(held);
            for (int i = 0; i < 20; i++)
            {
                Frame(stand, false);
                yield return null;
            }
            stand.Vitals.TrySpendMana(stand.Vitals.Mana);
            Assert.Less(stand.Vitals.Mana, 0.01f, "setup: the bar is not empty");

            // The click kept down on an empty bar, until well over half the mana a restart needs has come back: nothing may have started.
            for (int i = 0; i < 600 && stand.Vitals.Mana < held.ManaPerSecond * StreamFeed.RestartSeconds * 0.6f; i++)
            {
                Frame(stand, true);
                yield return null;
            }
            Assert.AreEqual(0, stand.Ticks(), "out of mana with the click held, it went on hitting as the mana trickled back");
            Assert.AreEqual(0, stand.Stops, "out of mana with the click held, the stream started and stopped again and again");

            // Held on: once the mana is back it runs again, as a held stream does, a tick at a time.
            float before = Time.time;
            int frames = 0;
            for (; frames < 600 && stand.Ticks() < 2; frames++)
            {
                Frame(stand, true);
                yield return null;
            }
            Assert.GreaterOrEqual(stand.Ticks(), 2, "its mana back, a staff still held never ran again");
            float seconds = Mathf.Max(frames * Step, Time.time - before);
            Assert.LessOrEqual(stand.Stops, 1, "its mana back, the stream stuttered");
            Assert.LessOrEqual(stand.Ticks(), Mathf.CeilToInt(seconds / held.TickSeconds) + 1, "it hit oftener than its tick");
        }

        private IEnumerator TappingHitsNoOftenerThanHolding(Held held)
        {
            Stand stand = Make(held);
            for (int i = 0; i < 20; i++)
            {
                Frame(stand, false);
                yield return null;
            }
            float before = Time.time;
            const int Frames = 60;
            for (int i = 0; i < Frames; i++)
            {
                Frame(stand, i % 2 == 0);   // down, up, down, up: thirty clicks in the second
                yield return null;
            }
            float seconds = Mathf.Max(Frames * Step, Time.time - before);
            Assert.Greater(stand.Ticks(), 0, "setup: the staff never reached what it was held on");
            Assert.LessOrEqual(stand.Ticks(), Mathf.CeilToInt(seconds / held.TickSeconds) + 1,
                $"tapping the click hit {stand.Ticks()} times in {seconds:0.00} s: oftener than holding it does");
        }

        [UnityTest]
        public IEnumerator OutOfManaTheWaterJetStaysOffUntilItsManaIsBack() => StaysOffUntilItsManaIsBack(Water);

        [UnityTest]
        public IEnumerator OutOfManaTheFlameStaysOffUntilItsManaIsBack() => StaysOffUntilItsManaIsBack(Flame);

        [UnityTest]
        public IEnumerator OutOfManaTheFrostSprayStaysOffUntilItsManaIsBack() => StaysOffUntilItsManaIsBack(Frost);

        [UnityTest]
        public IEnumerator OutOfManaTheArcaneBeamStaysOffUntilItsManaIsBack() => StaysOffUntilItsManaIsBack(Arcane);

        [UnityTest]
        public IEnumerator OutOfManaTheHealingBeamStaysOffUntilItsManaIsBack() => StaysOffUntilItsManaIsBack(Healing);

        [UnityTest]
        public IEnumerator TappingTheWaterJetHitsNoOftenerThanHoldingIt() => TappingHitsNoOftenerThanHolding(Water);

        [UnityTest]
        public IEnumerator TappingTheFlameHitsNoOftenerThanHoldingIt() => TappingHitsNoOftenerThanHolding(Flame);

        [UnityTest]
        public IEnumerator TappingTheFrostSprayHitsNoOftenerThanHoldingIt() => TappingHitsNoOftenerThanHolding(Frost);

        [UnityTest]
        public IEnumerator TappingTheArcaneBeamHitsNoOftenerThanHoldingIt() => TappingHitsNoOftenerThanHolding(Arcane);

        [UnityTest]
        public IEnumerator TappingTheHealingBeamHitsNoOftenerThanHoldingIt() => TappingHitsNoOftenerThanHolding(Healing);
    }
}
