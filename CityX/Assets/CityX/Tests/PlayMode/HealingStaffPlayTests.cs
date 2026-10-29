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
    /// The Staff of Healing on real bodies (docs/STAFFS.md): held with a wounded player under the crosshair it ties its beam to them and heals
    /// them; never a body that is not a player's; the beam stays on them while the healer looks away and lets go once they are past its reach or
    /// out of sight too long; another player's copy shows the beam tied to the same body.
    /// </summary>
    public sealed class HealingStaffPlayTests
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
            foreach (StaffBeam beam in Object.FindObjectsByType<StaffBeam>(FindObjectsSortMode.None)) Object.Destroy(beam.gameObject);
        }

        /// <summary>A body at <paramref name="at"/>; a player's (its input reader on: no keyboard here, so it does nothing) when <paramref name="player"/>.</summary>
        private GameObject Body(Vector3 at, bool player = true)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
                _made.Add(ground);
            }
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            _made.Add(body);
            body.GetComponent<PlayerInputReader>().enabled = player;
            if (body.GetComponent<HealingBeamCaster>() == null)
            {
                body.AddComponent<HealingBeamCaster>();
            }
            return body;
#else
            return null;
#endif
        }

        private StaffItem Staff()
        {
#if UNITY_EDITOR
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/HealingStaff.prefab"));
            Assert.IsNotNull(model, "no HealingStaff.prefab -- rebuild the scene");
            _made.Add(model);
            return model.GetComponent<StaffItem>();
#else
            return null;
#endif
        }

        /// <summary>Holds the staff's click for <paramref name="seconds"/>, the camera behind the healer looking along <paramref name="look"/>.</summary>
        private static IEnumerator Hold(GameObject healer, IEquippableStaff staff, bool held, float seconds, Vector3 look)
        {
            var locomotion = healer.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = healer.transform.position + Vector3.up * 2.4f - look * 4.0f;
                staff.Tick(held, eye, look, healer, Step);
                locomotion.Tick(new LocomotionInput { YawDegrees = healer.transform.eulerAngles.y }, Step);
                yield return null;
            }
        }

        private static Vector3 Toward(GameObject healer, GameObject other)
        {
            Vector3 chest = HealingBeamCaster.ChestOf(other.transform);
            Vector3 eye = healer.transform.position + Vector3.up * 2.4f;
            return (chest - eye).normalized;
        }

        [UnityTest]
        public IEnumerator AWoundedPlayerUnderTheCrosshairIsHealedAndABodyThatIsNoPlayersIsNot()
        {
            GameObject healer = Body(Vector3.zero, player: false);
            StaffItem staff = Staff();
            staff.OnEquip(healer, ItemInstanceData.None);
            GameObject friend = Body(new Vector3(0.0f, 0.0f, 9.0f));
            GameObject stranger = Body(new Vector3(-5.0f, 0.0f, 9.0f), player: false);
            var friendVitals = friend.GetComponent<PlayerVitals>();
            var strangerVitals = stranger.GetComponent<PlayerVitals>();
            friendVitals.TakeDamage(60.0f);
            strangerVitals.TakeDamage(60.0f);
            yield return Hold(healer, staff, false, 0.2f, Vector3.forward);
            float before = friendVitals.Health;
            var caster = healer.GetComponent<HealingBeamCaster>();
            yield return Hold(healer, staff, true, 1.0f, Toward(healer, friend));
            Assert.AreSame(friendVitals, caster.Target, "the beam is not on the player under the crosshair");
            float healed = friendVitals.Health - before;
            Assert.AreEqual(HealingBeamRules.HealPerSecond, healed, HealingBeamRules.HealPerTick * 1.5f, "a second of the beam did not heal about a second's worth");

            yield return Hold(healer, staff, false, 0.1f, Vector3.forward);
            float strangerBefore = strangerVitals.Health;
            yield return Hold(healer, staff, true, 0.5f, Toward(healer, stranger));
            Assert.IsNull(caster.Target, "the beam tied itself to a body that is no player's");
            Assert.AreEqual(strangerBefore, strangerVitals.Health, 1e-3f);
        }

        [UnityTest]
        public IEnumerator TheBeamStaysWhileTheHealerLooksAwayAndLetsGoPastItsReach()
        {
            GameObject healer = Body(Vector3.zero, player: false);
            StaffItem staff = Staff();
            staff.OnEquip(healer, ItemInstanceData.None);
            GameObject friend = Body(new Vector3(0.0f, 0.0f, 9.0f));
            friend.GetComponent<PlayerVitals>().TakeDamage(80.0f);
            var caster = healer.GetComponent<HealingBeamCaster>();
            yield return Hold(healer, staff, true, 0.3f, Toward(healer, friend));
            Assert.IsNotNull(caster.Target, "setup: the beam is not on the friend");
            yield return Hold(healer, staff, true, 0.5f, Vector3.right);
            Assert.IsNotNull(caster.Target, "looking away let the beam go");
            Assert.IsTrue(healer.GetComponentInChildren<StaffBeam>().IsRunning, "the beam is not drawn while it heals");

            var controller = friend.GetComponent<CharacterController>();
            controller.enabled = false;
            friend.transform.position = new Vector3(0.0f, 0.0f, HealingBeamRules.BreakRange + 3.0f);
            controller.enabled = true;
            Physics.SyncTransforms();
            yield return Hold(healer, staff, true, 0.2f, Vector3.right);
            Assert.IsNull(caster.Target, "the beam held on past its reach");
            Assert.IsFalse(healer.GetComponentInChildren<StaffBeam>().IsRunning, "the beam is still drawn after letting go");
        }

        [UnityTest]
        public IEnumerator OutOfSightTheBeamHoldsAMomentThenLetsGo()
        {
            GameObject healer = Body(Vector3.zero, player: false);
            StaffItem staff = Staff();
            staff.OnEquip(healer, ItemInstanceData.None);
            GameObject friend = Body(new Vector3(0.0f, 0.0f, 9.0f));
            friend.GetComponent<PlayerVitals>().TakeDamage(80.0f);
            var caster = healer.GetComponent<HealingBeamCaster>();
            Vector3 look = Toward(healer, friend);
            yield return Hold(healer, staff, true, 0.3f, look);
            Assert.IsNotNull(caster.Target, "setup: the beam is not on the friend");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 2.0f, 5.0f);
            wall.transform.localScale = new Vector3(6.0f, 6.0f, 0.3f);
            _made.Add(wall);
            Physics.SyncTransforms();
            yield return Hold(healer, staff, true, HealingBeamRules.SightGraceSeconds * 0.5f, look);
            Assert.IsNotNull(caster.Target, "the beam let go the moment the friend went out of sight");
            yield return Hold(healer, staff, true, HealingBeamRules.SightGraceSeconds, look);
            Assert.IsNull(caster.Target, "the beam held on someone out of sight too long");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyShowsTheBeamTiedToTheSameBody()
        {
            GameObject copy = Body(new Vector3(10.0f, 0.0f, 0.0f), player: false);
            StaffItem staff = Staff();
            staff.MountCosmetic(copy);
            GameObject friend = Body(new Vector3(10.0f, 0.0f, 8.0f));
            var caster = copy.GetComponent<HealingBeamCaster>();
            yield return null;
            Vector3 chest = HealingBeamCaster.ChestOf(friend.transform);
            caster.PlayRemote(new AbilityEvent(HealingBeamCaster.Tied, chest + Vector3.up * 0.3f, Vector3.zero));
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }
            StaffBeam beam = copy.GetComponentInChildren<StaffBeam>();
            Assert.IsNotNull(beam, "the copy shows no beam");
            Assert.IsTrue(beam.IsRunning);
            Assert.Less(Vector3.Distance(beam.End, chest), 0.3f, "the copy's beam is not tied to the body it was sent");
            caster.PlayRemote(new AbilityEvent(HealingBeamCaster.LetGo, copy.transform.position, Vector3.zero));
            Assert.IsFalse(beam.IsRunning, "the copy's beam did not let go with its owner's");
        }
    }
}
