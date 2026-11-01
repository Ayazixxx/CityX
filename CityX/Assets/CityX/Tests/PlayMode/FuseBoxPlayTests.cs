using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.UI;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using House = CityX.World.OldGuysHouse;

namespace CityX.Tests
{
    /// <summary>
    /// Old Guy's house is dark until the fuse box's lever is thrown (docs/OLD_GUYS_HOUSE.md): the lever names itself under the crosshair, E
    /// throws it with a click, every lamp in the house comes on, and the power stays on through a rebuild of the house.
    /// </summary>
    public sealed class FuseBoxPlayTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private readonly List<GameObject> _made = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            TutorialHousePower.Shared.Reset();
            DroppedItems.Shared.Clear();
        }

        public override void TearDown()
        {
            TutorialHousePower.Shared.Reset();
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
            base.TearDown();
        }

        private static bool AllLights(OldGuysHouseSite site, bool on)
        {
            Light[] lights = site.Built.GetComponentsInChildren<Light>(true);
            Assert.Greater(lights.Length, 10, "the house has lost its lamps");
            foreach (Light light in lights)
            {
                if (light.enabled != on) return false;
            }
            return true;
        }

        [UnityTest]
        public IEnumerator TheHouseIsDarkUntilTheLeverIsThrownThenLitForGood()
        {
#if UNITY_EDITOR
            OldGuysHouseSite site = OldGuysHousePlayTests.MakeHouse(_made, false, false);
            Assert.IsTrue(AllLights(site, false), "the house should be dark before its power is turned on");
            FuseBoxSwitch lever = site.Built.GetComponentInChildren<FuseBoxSwitch>();
            Assert.IsNotNull(lever, "the house has no fuse box lever");
            Assert.IsTrue(lever.Available);

            // The player in the workshop, looking at the lever on the east wall.
            Vector3 box = Vector3.zero;
            foreach (House.Furniture piece in House.FurnitureList)
            {
                if (piece.Kind == House.FurnitureKind.FuseBox) box = piece.Position;
            }
            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(box.x - 2.5f, 0.05f, box.z), Quaternion.Euler(0.0f, 90.0f, 0.0f));
            _made.Add(player);
            var eye = new GameObject("Eye");
            _made.Add(eye);
            eye.transform.position = player.transform.position + Vector3.up * 2.2f;
            eye.transform.rotation = Quaternion.LookRotation(lever.Centre - eye.transform.position, Vector3.up);
            player.GetComponent<PlayerInputReader>().YawSource = eye.transform;
            var prompt = new GameObject("ItemPrompt").AddComponent<ItemPromptView>();
            _made.Add(prompt.gameObject);
            ItemInspector inspector = player.GetComponent<ItemInspector>();
            for (int i = 0; i < 12; i++) yield return null;
            Assert.AreSame(lever, inspector.HoveredTarget, "the lever is not what the player is looking at");
            Assert.IsTrue(prompt.Showing, "no prompt for the lever");
            Assert.AreEqual(FuseBoxSwitch.Name, prompt.NameText);
            Assert.AreEqual(FuseBoxSwitch.UseKeys, prompt.KeysText);

            Press(_keyboard.eKey);
            yield return null;
            yield return null;
            Release(_keyboard.eKey);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(TutorialHousePower.Shared.IsOn, "E on the lever did not turn the power on");
            Assert.IsTrue(AllLights(site, true), "the lamps did not come on with the power");
            Assert.IsFalse(lever.Available, "a thrown lever still offers itself");
            Assert.IsFalse(prompt.Showing, "the prompt should go once the lever is thrown");
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(FuseBoxSwitch.ThrownDegrees, lever.LeverDegrees, 1e-3f, "the lever did not swing over");

            // Rebuilt (the streamer places the house afresh): still lit, the lever down from the start.
            site.Build();
            yield return null;
            yield return null;
            Assert.IsTrue(AllLights(site, true), "the rebuilt house lost its power");
            FuseBoxSwitch again = site.Built.GetComponentInChildren<FuseBoxSwitch>();
            Assert.AreEqual(FuseBoxSwitch.ThrownDegrees, again.LeverDegrees, 1e-3f, "the rebuilt lever stands up though the power is on");
#else
            yield break;
#endif
        }
    }
}
