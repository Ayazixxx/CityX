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
    /// The Frost Staff on real bodies (docs/FROST.md): held on a swordsman it hurts him a little and chills him, slowing him the colder he gets,
    /// and after two seconds he freezes solid (his mind stops, his animation stops, his body tinted icy blue), then thaws and cannot freeze again
    /// straight away; nobody beside the cone is touched; the spray stops when let go; another player's copy shows the spray and a freeze told to it.
    /// </summary>
    public sealed class FrostStaffPlayTests
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
            foreach (FrostSpray spray in Object.FindObjectsByType<FrostSpray>(FindObjectsSortMode.None)) Object.Destroy(spray.gameObject);
        }

        private GameObject Spawn(string path, Vector3 at, float yawDegrees = 0.0f)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
                _made.Add(ground);
            }
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path), at, Quaternion.Euler(0.0f, yawDegrees, 0.0f));
            _made.Add(body);
            return body;
#else
            return null;
#endif
        }

        private GameObject Caster()
        {
            GameObject caster = Spawn("Assets/CityX/Character/Player.prefab", Vector3.zero);
            caster.GetComponent<PlayerInputReader>().enabled = false;
            if (caster.GetComponent<FrostSprayCaster>() == null)
            {
                caster.AddComponent<FrostSprayCaster>();
            }
            return caster;
        }

        private StaffItem Staff()
        {
#if UNITY_EDITOR
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/FrostStaff.prefab"));
            Assert.IsNotNull(model, "no FrostStaff.prefab -- rebuild the scene");
            _made.Add(model);
            return model.GetComponent<StaffItem>();
#else
            return null;
#endif
        }

        private static IEnumerator Hold(GameObject caster, IEquippableStaff staff, bool held, float seconds, System.Action each = null)
        {
            var locomotion = caster.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = caster.transform.position + Vector3.up * 2.4f - caster.transform.forward * 4.0f;
                Vector3 look = (new Vector3(0.0f, 1.4f, 6.0f) - eye).normalized;
                staff.Tick(held, eye, look, caster, Step);
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                each?.Invoke();
                yield return null;
            }
        }

        /// <summary>How much of the frost layer shows over <paramref name="renderer"/> (0 with no layer on it).</summary>
        private static float SheenShown(Renderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            int last = materials.Length - 1;
            if (last < 0 || materials[last] != FrozenLook.Sheen) return 0.0f;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, last);
            return block.GetColor("_BaseColor").a;
        }

        [UnityTest]
        public IEnumerator HeldOnASwordsmanItChillsHimSlowsHimAndFreezesHimThenHeThaws()
        {
            GameObject caster = Caster();
            StaffItem staff = Staff();
            staff.OnEquip(caster, ItemInstanceData.None);
            GameObject swordsman = Spawn("Assets/CityX/Character/Swordsman.prefab", new Vector3(0.0f, 0.0f, 6.0f), 180.0f);
            GameObject beside = Spawn("Assets/CityX/Character/Swordsman.prefab", new Vector3(6.0f, 0.0f, 6.0f), 180.0f);
            var chill = swordsman.GetComponent<Chill>();
            Renderer body = swordsman.GetComponentInChildren<SkinnedMeshRenderer>();
            Material[] own = body.sharedMaterials;
            Assert.IsNotNull(chill, "the swordsman's body cannot be chilled");
            var vitals = swordsman.GetComponent<PlayerVitals>();
            vitals.InfiniteHealth = true;   // the freeze is what is measured, not the kill
            yield return Hold(caster, staff, false, 0.2f);

            yield return Hold(caster, staff, true, 1.0f);
            Assert.Greater(chill.Level, 0.3f, "a second in the spray did not chill him");
            Assert.Less(chill.SpeedShare, 0.9f, "chilled, he is not slowed");
            Assert.IsFalse(chill.IsFrozen, "frozen before two seconds");
            Assert.AreEqual(0.0f, beside.GetComponent<Chill>().Level, 1e-4f, "the swordsman beside the spray was chilled");

            yield return Hold(caster, staff, true, 1.2f);
            Assert.IsTrue(chill.IsFrozen, "two seconds in the spray did not freeze him");
            Assert.AreEqual(0.0f, chill.SpeedShare, 1e-4f, "frozen, he can still move");
            Assert.AreEqual(0.0f, swordsman.GetComponentInChildren<Animator>().speed, 1e-4f, "frozen, his animation runs on");
            foreach (IHostile mind in swordsman.GetComponents<IHostile>())
            {
                Assert.IsFalse(((Behaviour)mind).enabled, "frozen, his mind runs on");
            }

            float sheen = 0.0f;
            yield return Hold(caster, staff, false, FrostRules.FreezeSeconds + 0.2f, () =>
            {
                if (chill.IsFrozen) sheen = Mathf.Max(sheen, SheenShown(body));
            });
            Assert.AreEqual(FrozenLook.SheenAlpha, sheen, 0.01f, "frozen, his body never took the icy tinge");
            Assert.IsFalse(chill.IsFrozen, "he never thawed");
            CollectionAssert.AreEqual(own, body.sharedMaterials, "thawed, his body kept the icy tinge");
            foreach (IHostile mind in swordsman.GetComponents<IHostile>())
            {
                Assert.IsTrue(((Behaviour)mind).enabled, "thawed, his mind is still stopped");
            }
            yield return Hold(caster, staff, true, 0.5f);
            Assert.IsFalse(chill.IsFrozen, "he froze again the moment he thawed");
        }

        [UnityTest]
        public IEnumerator TheSprayDoesALittleDamageAndStopsWhenLetGo()
        {
            GameObject caster = Caster();
            StaffItem staff = Staff();
            staff.OnEquip(caster, ItemInstanceData.None);
            GameObject target = Spawn("Assets/CityX/Character/Player.prefab", new Vector3(0.0f, 0.0f, 6.0f), 180.0f);
            target.GetComponent<PlayerInputReader>().enabled = false;
            var vitals = target.GetComponent<PlayerVitals>();
            yield return Hold(caster, staff, false, 0.2f);
            float before = vitals.Health;
            yield return Hold(caster, staff, true, 1.0f);
            float taken = before - vitals.Health;
            Assert.Greater(taken, 0.0f, "the spray did no damage");
            Assert.LessOrEqual(taken, FrostRules.DamagePerSecond + FrostRules.DamagePerTick + 0.01f, "the spray did more than a little damage");
            var caster_ = caster.GetComponent<FrostSprayCaster>();
            Assert.IsTrue(caster_.IsSpraying);
            yield return Hold(caster, staff, false, 0.1f);
            Assert.IsFalse(caster_.IsSpraying, "let go, the spray runs on");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyShowsTheSprayAndAFreezeToldToIt()
        {
            GameObject copy = Spawn("Assets/CityX/Character/Player.prefab", new Vector3(10.0f, 0.0f, 0.0f));
            copy.GetComponent<PlayerInputReader>().enabled = false;
            if (copy.GetComponent<FrostSprayCaster>() == null)
            {
                copy.AddComponent<FrostSprayCaster>();
            }
            StaffItem staff = Staff();
            staff.MountCosmetic(copy);
            yield return null;
            var caster = copy.GetComponent<FrostSprayCaster>();
            caster.PlayRemote(new AbilityEvent(FrostSprayCaster.SprayStarted, copy.transform.position, Vector3.forward));
            yield return null;
            FrostSpray spray = copy.GetComponentInChildren<FrostSpray>();
            Assert.IsNotNull(spray, "the copy shows no spray");
            Assert.IsTrue(spray.IsRunning);
            caster.PlayRemote(new AbilityEvent(FrostSprayCaster.SprayStopped, copy.transform.position, Vector3.zero));
            Assert.IsFalse(spray.IsRunning, "the copy's spray did not stop with its owner's");

            GameObject frozen = Spawn("Assets/CityX/Character/Player.prefab", new Vector3(14.0f, 0.0f, 0.0f));
            frozen.GetComponent<PlayerInputReader>().enabled = false;
            var chill = frozen.GetComponent<Chill>();
            chill.PlayRemote(new AbilityEvent(Chill.Froze, frozen.transform.position, Vector3.zero, 1.0f));
            Assert.IsTrue(chill.IsFrozen, "a copy told its owner froze does not show it");
            chill.PlayRemote(new AbilityEvent(Chill.Thawed, frozen.transform.position, Vector3.zero));
            Assert.IsFalse(chill.IsFrozen, "a copy told its owner thawed stays frozen");
        }
    }
}
