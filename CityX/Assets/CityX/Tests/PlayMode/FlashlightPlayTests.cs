using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.View;
using CityX.Weapons;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The flashlight on the real rig (docs/FLASHLIGHT.md): a click switches it on and off; lit, the arm holds it out and points it where the
    /// player looks, a little late; its beam is a shadowed spot shaped by its cookie, with a dim light all round it under the ground and
    /// none under the sky; the body's FlashlightSync tells other players, and their copy is lit and pointed by that word alone.
    /// </summary>
    public sealed class FlashlightPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private const int Seed = 48398181;
        private readonly List<GameObject> _made = new List<GameObject>();
        private CityWorld _world;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            if (_world != null && CurrentWorld.Instance == _world)
            {
                CurrentWorld.Instance = null;
            }
            _world = null;
        }

        /// <summary>
        /// The walkway of a tunnel of the city's sewers, with a floor under it to stand on: the world is published (the torch asks it how
        /// far out of the sky's light it is), none of it is built. <paramref name="along"/> is the way the tunnel runs.
        /// </summary>
        private Vector3 InATunnel(out Vector3 along)
        {
            _world = new CityWorld(Seed);
            CurrentWorld.Instance = _world;
            SewerPlan sewers = SewerPlan.For(_world.Plan);
            foreach (RoadEdge edge in _world.Plan.Data.Edges)
            {
                SewerTube tube = sewers.Tube(edge.Id);
                if (tube == null || tube.End - tube.Start < 30.0)
                {
                    continue;
                }
                double middle = (tube.Start + tube.End) * 0.5;
                Vec2 at = tube.At(middle);
                var feet = new Vector3((float)at.X, (float)tube.FloorAt(middle), (float)at.Y);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = feet + Vector3.down * 0.5f;
                floor.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
                _made.Add(floor);
                Physics.SyncTransforms();
                along = new Vector3((float)tube.Dir.X, 0.0f, (float)tube.Dir.Y);
                return feet;
            }
            Assert.Fail("no tunnel in this city");
            along = Vector3.forward;
            return Vector3.zero;
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
            AssertACarriedBeam(beam, "the torch");
        }

        [UnityTest]
        public IEnumerator LitInATunnelItGlowsDimlyAllRoundAndTheGlowGoesOutWithTheBeam()
        {
            (GameObject body, FlashlightItem torch) = Holder(InATunnel(out _));
            yield return Hold(body, torch, false, 0.2f, Vector3.forward);
            Light glow = torch.Glow;
            Assert.IsNotNull(glow, "no light round the torch: the walls beside whoever carries it stay black");
            Assert.IsFalse(glow.enabled, "it glows switched off");

            yield return Hold(body, torch, true, 0.2f, Vector3.forward);
            Assert.IsTrue(glow.enabled, "lit in a tunnel, but no light round it");
            Assert.AreEqual(LightType.Point, glow.type, "the glow is not all round");
            Assert.AreEqual(LightShadows.None, glow.shadows, "the glow is the beam's light come back off the walls: it has no one place to cast from");
            Assert.AreEqual(torch.Beam.intensity * FlashlightRules.GlowShare, glow.intensity, 0.001f);
            Assert.AreEqual(FlashlightRules.GlowReach, glow.range, 0.1f);
            AssertACarriedBeam(glow, "the torch's glow");

            yield return Hold(body, torch, true, 0.2f, Vector3.forward);
            Assert.IsFalse(glow.enabled, "the glow stayed on with the torch off");
        }

        [UnityTest]
        public IEnumerator LitUnderTheSkyItDoesNotGlow()
        {
            (GameObject body, FlashlightItem torch) = Holder(Vector3.zero);
            yield return Hold(body, torch, true, 0.3f, Vector3.forward);
            Assert.IsTrue(torch.Beam.enabled, "lit, but no light");
            Assert.IsFalse(torch.Glow.enabled, "on the street the torch puts a pool of light at its carrier's feet");
        }

        /// <summary>The mark by which the city's shaders let a torch reach down a tunnel (CityLightLayers.Beam), and no side of a wall.</summary>
        private static void AssertACarriedBeam(Light beam, string whose)
        {
            uint layers = CityLightLayers.Of(beam);
            Assert.AreNotEqual(0u, layers & CityLightLayers.Beam, $"{whose} is not marked a carried beam: it is gone ten metres down a tunnel");
            Assert.AreNotEqual(0u, layers & CityLightLayers.Default, $"{whose} lights no body");
            Assert.AreEqual(0u, layers & (CityLightLayers.Interior | CityLightLayers.Exterior), $"{whose} is on one side of a wall: it is carried through doors");
        }

        [UnityTest]
        public IEnumerator TheBodyTellsOtherPlayersAndTheirCopyIsLitAndPointedByThatWordAlone()
        {
            // In a tunnel: it is there that a torch lights what is round it too.
            Vector3 tunnel = InATunnel(out Vector3 along);
            (GameObject body, FlashlightItem torch) = Holder(tunnel);
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

            Vector3 further = tunnel + along * 6.0f;
            GameObject copy = Make("Assets/CityX/Character/Player.prefab", further);
            copy.GetComponent<PlayerInputReader>().enabled = false;
            var copyTorch = Make("Assets/CityX/Weapons/Flashlight.prefab", further + Vector3.right * 30.0f).GetComponent<FlashlightItem>();
            copyTorch.MountCosmetic(copy);
            var copySync = copy.GetComponent<FlashlightSync>();
            if (copySync == null)
            {
                copySync = copy.AddComponent<FlashlightSync>();
            }
            Vector3 point = further + new Vector3(0.0f, 1.0f, 20.0f);
            copySync.PlayRemote(new AbilityEvent(FlashlightSync.On, point, Vector3.zero));
            for (int i = 0; i < 30; i++)
            {
                copy.GetComponent<PlayerLocomotion>().Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }
            Assert.IsTrue(copyTorch.IsLit, "the copy's torch did not come on with its owner's");
            Assert.Less(AngleTo(copyTorch, point), 8.0f, "the copy's beam does not point where its owner's does");
            AssertACarriedBeam(copyTorch.Beam, "another player's torch");
            Assert.IsTrue(copyTorch.Glow.enabled, "another player's torch lights nothing round them");
            AssertACarriedBeam(copyTorch.Glow, "another player's torch's glow");
            copySync.PlayRemote(new AbilityEvent(FlashlightSync.Off, point, Vector3.zero));
            Assert.IsFalse(copyTorch.IsLit, "the copy's torch did not go off with its owner's");
            Assert.IsFalse(copyTorch.Glow.enabled, "the copy's torch still lights what is round it, switched off");
        }
    }
}
