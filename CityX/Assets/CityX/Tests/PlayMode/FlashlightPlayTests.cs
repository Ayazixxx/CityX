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
    /// The flashlight on the real rig (docs/FLASHLIGHT.md): a click switches it on and off; lit, the arm holds it out and points it where the
    /// player looks, a little late; its beam is a shadowed spot shaped by its cookie; the body's FlashlightSync tells other players, and their
    /// copy is lit and pointed by that word alone.
    /// </summary>
    public sealed class FlashlightPlayTests
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
            Assert.IsNotNull(made, $"no {path} -- rebuild the scene");
            _made.Add(made);
            return made;
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            return null;
#endif
        }

        private (GameObject body, FlashlightItem torch) Holder(Vector3 at)
        {
            GameObject body = Make("Assets/CityX/Character/Player.prefab", at);
            body.GetComponent<PlayerInputReader>().enabled = false;
            var torch = Make("Assets/CityX/Weapons/Flashlight.prefab", at + Vector3.right * 20.0f).GetComponent<FlashlightItem>();
            torch.OnEquip(body, ItemInstanceData.None);
            return (body, torch);
        }

        /// <summary>Frames of the torch held with the camera behind the body looking along <paramref name="look"/>.</summary>
        private static IEnumerator Hold(GameObject body, FlashlightItem torch, bool click, float seconds, Vector3 look)
        {
            var locomotion = body.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = body.transform.position + Vector3.up * 2.4f - look * 4.0f;
                torch.Tick(click && t < Step, eye, look, body, Step);
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }
        }

        private static float AngleTo(FlashlightItem torch, Vector3 point)
        {
            return Vector3.Angle(torch.Beam.transform.forward, point - torch.Beam.transform.position);
        }

        [UnityTest]
        public IEnumerator AClickSwitchesItOnAndOffAndTheArmPointsItWhereThePlayerLooks()
        {
            (GameObject body, FlashlightItem torch) = Holder(Vector3.zero);
            yield return Hold(body, torch, false, 0.3f, Vector3.forward);
            Assert.IsFalse(torch.IsLit, "it came on by itself");
            Assert.IsFalse(torch.Beam.enabled);

            yield return Hold(body, torch, true, 0.6f, Vector3.forward);
            Assert.IsTrue(torch.IsLit, "a click did not switch it on");
            Assert.IsTrue(torch.Beam.enabled, "lit, but no light");
            Assert.IsTrue(body.GetComponent<PlayerLocomotion>().TorchLit, "the arm was not told to hold it up");
            Assert.Less(AngleTo(torch, torch.AimPoint), 8.0f, "the beam does not point where the player looks");

            Vector3 right = new Vector3(0.5f, -0.1f, 1.0f).normalized;
            yield return Hold(body, torch, false, 0.6f, right);
            Assert.Less(AngleTo(torch, torch.AimPoint), 8.0f, "the beam did not follow the look");

            yield return Hold(body, torch, true, 0.2f, right);
            Assert.IsFalse(torch.IsLit, "a second click did not switch it off");
            Assert.IsFalse(torch.Beam.enabled);
        }

        [UnityTest]
        public IEnumerator ItFollowsTheLookALittleLate()
        {
            (GameObject body, FlashlightItem torch) = Holder(Vector3.zero);
            yield return Hold(body, torch, true, 0.6f, Vector3.forward);
            Vector3 turned = new Vector3(0.8f, 0.0f, 0.6f).normalized;
            yield return Hold(body, torch, false, Step, turned);
            Assert.Greater(AngleTo(torch, torch.AimPoint), 10.0f, "the beam snapped onto the new look at once");
            yield return Hold(body, torch, false, 0.5f, turned);
            Assert.Less(AngleTo(torch, torch.AimPoint), 8.0f, "the beam never caught up");
        }

        [UnityTest]
        public IEnumerator ItsBeamIsAShadowedSpotShapedByItsCookie()
        {
            (GameObject body, FlashlightItem torch) = Holder(Vector3.zero);
            yield return Hold(body, torch, true, 0.2f, Vector3.forward);
            Light beam = torch.Beam;
            Assert.AreEqual(LightType.Spot, beam.type);
            Assert.AreEqual(LightShadows.Soft, beam.shadows, "walls and bodies must cut the beam");
            Assert.IsNotNull(beam.cookie, "no cookie: a stage light, not a torch");
            Assert.AreEqual(FlashlightRules.SpillDegrees, beam.spotAngle, 0.01f);
            Assert.AreEqual(FlashlightRules.Range, beam.range, 0.5f);
        }

        [UnityTest]
        public IEnumerator TheBodyTellsOtherPlayersAndTheirCopyIsLitAndPointedByThatWordAlone()
        {
            (GameObject body, FlashlightItem torch) = Holder(Vector3.zero);
            var sync = body.GetComponent<FlashlightSync>();
            if (sync == null)
            {
                sync = body.AddComponent<FlashlightSync>();
            }
            var sent = new List<AbilityEvent>();
            sync.Fired += evt => sent.Add(evt);
            yield return Hold(body, torch, true, 0.3f, Vector3.forward);
            Assert.IsTrue(sent.Exists(e => e.Kind == FlashlightSync.On), "switching it on was not told");
            yield return Hold(body, torch, false, 0.4f, new Vector3(0.6f, 0.0f, 0.8f).normalized);
            Assert.IsTrue(sent.Exists(e => e.Kind == FlashlightSync.Aimed), "where it points was not told");

            GameObject copy = Make("Assets/CityX/Character/Player.prefab", new Vector3(6.0f, 0.0f, 0.0f));
            copy.GetComponent<PlayerInputReader>().enabled = false;
            var copyTorch = Make("Assets/CityX/Weapons/Flashlight.prefab", new Vector3(30.0f, 0.0f, 0.0f)).GetComponent<FlashlightItem>();
            copyTorch.MountCosmetic(copy);
            var copySync = copy.GetComponent<FlashlightSync>();
            if (copySync == null)
            {
                copySync = copy.AddComponent<FlashlightSync>();
            }
            Vector3 point = new Vector3(6.0f, 1.0f, 20.0f);
            copySync.PlayRemote(new AbilityEvent(FlashlightSync.On, point, Vector3.zero));
            for (int i = 0; i < 30; i++)
            {
                copy.GetComponent<PlayerLocomotion>().Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }
            Assert.IsTrue(copyTorch.IsLit, "the copy's torch did not come on with its owner's");
            Assert.Less(AngleTo(copyTorch, point), 8.0f, "the copy's beam does not point where its owner's does");
            copySync.PlayRemote(new AbilityEvent(FlashlightSync.Off, point, Vector3.zero));
            Assert.IsFalse(copyTorch.IsLit, "the copy's torch did not go off with its owner's");
        }
    }
}
