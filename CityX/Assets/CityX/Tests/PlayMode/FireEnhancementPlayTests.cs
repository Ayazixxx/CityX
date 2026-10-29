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
    /// The Fire Enhancement on the real rig (docs/FIRE_ENHANCEMENT.md): cast, the sword burns along its blade; its next swing burns what it
    /// hits (more damage, the target alight) and the fire is spent; the next bullet takes it the same way; a staff neither shows nor takes it;
    /// another player's copy shows the flames and the bodies set alight, hurting nobody.
    /// </summary>
    public sealed class FireEnhancementPlayTests
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
            FireScene.Clear();
        }

        private GameObject Make(string path, Vector3 at)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
                _made.Add(ground);
            }
            GameObject made = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path), at, Quaternion.identity);
            Assert.IsNotNull(made, $"no {path}");
            _made.Add(made);
            return made;
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            return null;
#endif
        }

        /// <summary>A body of the player's, driven by the test alone (its reader off), with the spell.</summary>
        private (GameObject body, FireEnhancementAbility fire) Caster()
        {
            GameObject body = Make("Assets/CityX/Character/Player.prefab", new Vector3(0.0f, 0.1f, 0.0f));
            body.GetComponent<PlayerInputReader>().enabled = false;
            body.GetComponent<PlayerVitals>().Roll = () => 1.0f;
            var fire = body.GetComponent<FireEnhancementAbility>();
            if (fire == null)
            {
                fire = body.AddComponent<FireEnhancementAbility>();
            }
            fire.Bind();
            return (body, fire);
        }

        private SparringDummy Dummy(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = at;
            _made.Add(go);
            var dummy = go.AddComponent<SparringDummy>();
            dummy.Reset();
            Physics.SyncTransforms();
            return dummy;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Swing(GameObject body, float seconds)
        {
            var combat = body.GetComponent<SoulsSwordCombat>();
            var vitals = body.GetComponent<PlayerVitals>();
            var locomotion = body.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                combat.Tick(Step);
                vitals.TickStamina(false, Step);
                locomotion.Tick(new LocomotionInput { SwordEquipped = true }, Step);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator CastTheSwordBurnsAndItsNextSwingSetsWhatItCutsAlightThenTheFireIsSpent()
        {
            (GameObject body, FireEnhancementAbility fire) = Caster();
            GameObject sword = Make("Assets/CityX/Weapons/SpikeSword.prefab", new Vector3(20.0f, 0.5f, 20.0f));
            sword.GetComponent<SwordWeapon>().OnEquip(body, ItemInstanceData.None);
            var combat = body.GetComponent<SoulsSwordCombat>();
            combat.HitStopSeconds = 0.0f;
            yield return Swing(body, 0.4f);

            float mana = body.GetComponent<PlayerVitals>().Mana;
            Assert.IsTrue(fire.TryCast(), "the spell would not cast");
            Assert.AreEqual(mana - FireEnhancementRules.ManaCost, body.GetComponent<PlayerVitals>().Mana, 0.5f, "it cost no mana");
            Assert.IsFalse(fire.TryCast(), "it stacked");
            yield return Swing(body, 0.1f);
            var ablaze = Object.FindAnyObjectByType<WeaponAblaze>();
            Assert.IsTrue(ablaze != null && ablaze.Burning, "the sword is not ablaze");
            var blade = sword.GetComponent<SwordWeapon>();
            Assert.Less(Vector3.Distance(ablaze.From, blade.BladeBaseWorld), 0.01f, "the flames are not on the blade");
            Assert.Less(Vector3.Distance(ablaze.To, blade.BladeTipWorld), 0.01f, "the flames do not run to the tip");

            SparringDummy dummy = Dummy(new Vector3(0.0f, 1.0f, 1.6f));
            combat.RequestLight(0.0f);
            yield return Swing(body, combat.Light1.Startup + combat.Light1.Active + 0.1f);
            Assert.GreaterOrEqual(dummy.TimesHit, 2, "the cut did not add its fire");
            Assert.IsTrue(FireScene.IsBurning(dummy), "the cut did not set the dummy alight");
            Assert.IsFalse(fire.IsArmed, "the swing did not take the fire");
            yield return Swing(body, 1.5f);
            Assert.IsFalse(fire.IsAblaze, "the sword still burns after its swing");
            Assert.IsFalse(ablaze.Burning);

            int hits = dummy.TimesHit;
            FireScene.Clear();
            combat.RequestLight(0.0f);
            yield return Swing(body, combat.Light1.Startup + combat.Light1.Active + 0.1f);
            Assert.AreEqual(hits + 1, dummy.TimesHit, "the swing after burned too");
            Assert.IsFalse(FireScene.IsBurning(dummy));
        }

        [UnityTest]
        public IEnumerator TheNextBulletTakesTheFireAndSetsItsTargetAlight()
        {
            (GameObject body, FireEnhancementAbility fire) = Caster();
            GameObject pistol = Make("Assets/CityX/Weapons/Pistol.prefab", new Vector3(20.0f, 0.5f, 20.0f));
            var gun = pistol.GetComponent<PistolWeapon>();
            gun.OnEquip(body, ItemInstanceData.None);
            yield return null;
            SparringDummy dummy = Dummy(new Vector3(0.0f, 1.5f, 8.0f));
            Assert.IsTrue(fire.TryCast());
            yield return Frames(3);   // the flames take their place after the frame they are lit
            Assert.IsTrue(Object.FindAnyObjectByType<WeaponAblaze>().Burning, "the gun hand does not burn");
            Vector3 eye = new Vector3(0.0f, 1.5f, 1.0f);
            Assert.IsTrue(gun.Fire(eye, Vector3.forward, false, body), "the pistol did not fire");
            Assert.GreaterOrEqual(dummy.TimesHit, 2, "the bullet did not add its fire");
            Assert.IsTrue(FireScene.IsBurning(dummy), "the bullet did not set the dummy alight");
            Assert.IsFalse(fire.IsAblaze, "the shot did not take the fire");
        }

        [UnityTest]
        public IEnumerator AStaffNeitherShowsTheFireNorTakesIt()
        {
            (GameObject body, FireEnhancementAbility fire) = Caster();
            GameObject staffModel = Make("Assets/CityX/Weapons/ArcaneStaff.prefab", new Vector3(20.0f, 0.5f, 20.0f));
            var staff = staffModel.GetComponent<StaffItem>();
            staff.OnEquip(body, ItemInstanceData.None);
            yield return null;
            Assert.IsTrue(fire.TryCast());
            yield return Frames(3);
            Assert.IsFalse(Object.FindAnyObjectByType<WeaponAblaze>().Burning, "the staff burns");
            Vector3 eye = body.transform.position + Vector3.up * 2.4f - Vector3.forward * 4.0f;
            staff.Tick(true, eye, Vector3.forward, body, Step);
            yield return null;
            staff.Tick(false, eye, Vector3.forward, body, Step);
            yield return null;
            Assert.IsTrue(fire.IsArmed, "the staff's attack took the fire");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyShowsTheFlamesAndSetsTheBodyAlightForShowOnly()
        {
            GameObject copy = Make("Assets/CityX/Character/Player.prefab", new Vector3(0.0f, 0.1f, 0.0f));
            copy.GetComponent<PlayerInputReader>().enabled = false;
            var fire = copy.GetComponent<FireEnhancementAbility>();
            if (fire == null)
            {
                fire = copy.AddComponent<FireEnhancementAbility>();
            }
            GameObject victim = Make("Assets/CityX/Character/Player.prefab", new Vector3(3.0f, 0.1f, 0.0f));
            victim.GetComponent<PlayerInputReader>().enabled = false;
            var victimVitals = victim.GetComponent<PlayerVitals>();
            yield return null;
            fire.PlayRemote(new AbilityEvent(FireEnhancementAbility.Lit, copy.transform.position, Vector3.zero, FireEnhancementRules.ArmedSeconds));
            yield return Frames(3);
            Assert.IsTrue(fire.IsAblaze, "the copy shows no fire");
            Assert.IsTrue(Object.FindAnyObjectByType<WeaponAblaze>().Burning, "the copy's hand does not burn");
            Assert.IsFalse(fire.TryCast(), "a copy cast the spell");

            float health = victimVitals.Health;
            fire.PlayRemote(new AbilityEvent(FireEnhancementAbility.Burned, victim.transform.position + Vector3.up * 1.6f, Vector3.zero));
            Assert.IsTrue(FireScene.IsBurning(victimVitals), "the copy's blow did not show the body alight");
            for (float t = 0.0f; t < 1.0f; t += Time.deltaTime)
            {
                yield return null;
            }
            Assert.AreEqual(health, victimVitals.Health, 1e-3f, "the copy's fire hurt the body: the owner's machine deals it");

            fire.PlayRemote(new AbilityEvent(FireEnhancementAbility.Out, copy.transform.position, Vector3.zero));
            yield return null;
            Assert.IsFalse(fire.IsAblaze, "the copy's fire did not go out with its owner's");
        }
    }
}
