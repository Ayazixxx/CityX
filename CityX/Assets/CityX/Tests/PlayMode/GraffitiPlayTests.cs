using System.Collections;
using CityX.Character;
using CityX.UI;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE SPRAY CAN ON THE REAL BODY (owner, 2026-10-15): with the can in hand, a right-click at a wall opens the graffiti canvas on that wall;
    /// what is sprayed on it goes up on the wall when Done is pressed, lit, facing out of the wall; painting the same spot again carries the
    /// piece on; a right-click at nothing opens nothing.
    /// </summary>
    public sealed class GraffitiPlayTests : InputTestFixture
    {
        private Mouse _mouse;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            if (GraffitiMenuView.IsAnyOpen)
            {
                GraffitiMenuView.Current.Close(apply: false);
            }
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            GraffitiWall.Clear();
            foreach (UnityEngine.EventSystems.EventSystem events in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
            {
                Object.Destroy(events.gameObject);   // the canvas makes one if there is none
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            base.TearDown();
        }

        private T Made<T>(T thing) where T : Object
        {
            GameObject go = thing is Component c ? c.gameObject : thing as GameObject;
            _made.Add(go);
            return thing;
        }

        /// <summary>Ground, a wall 3 m ahead (its face toward the player), the player with the can in hand, a camera behind looking at the wall.</summary>
        private (PlayerInputReader reader, GraffitiMenuView menu, Camera camera) Stage()
        {
#if UNITY_EDITOR
            var ground = Made(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);
            var wall = Made(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0.0f, 3.0f, 3.25f);
            wall.transform.localScale = new Vector3(12.0f, 6.0f, 0.5f);
            Physics.SyncTransforms();

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Made(Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity));
            var reader = player.GetComponent<PlayerInputReader>();
            var canPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/SprayCan.prefab");
            Assert.IsNotNull(canPrefab, "no spray can prefab -- rebuild the scene");
            var can = Object.Instantiate(canPrefab).GetComponent<SprayCanItem>();
            can.OnEquip(player, default);
            Assert.AreSame(can, reader.EquippedSprayCan, "the can in hand is not the reader's");

            var cameraObject = Made(new GameObject("TestCamera") { tag = "MainCamera" });
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0.0f, 2.2f, -4.0f), Quaternion.LookRotation(new Vector3(0.0f, 0.0f, 1.0f)));

            var menu = Made(new GameObject("Graffiti").AddComponent<GraffitiMenuView>());
            return (reader, menu, camera);
#else
            return default;
#endif
        }

        private IEnumerator RightClick()
        {
            Press(_mouse.rightButton);
            yield return null;
            Release(_mouse.rightButton);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ARightClickAtAWallOpensTheCanvasAndDonePutsThePaintOnTheWall()
        {
            (PlayerInputReader reader, GraffitiMenuView menu, Camera camera) = Stage();
            yield return null;
            yield return RightClick();
            Assert.IsTrue(menu.IsOpen, "right-clicking the wall with the can did not open the canvas");
            Assert.IsFalse(reader.enabled, "the player should stand still while painting");
            Assert.AreEqual(3.0f, menu.Centre.z, 0.1f, "the canvas is not on the wall's face");
            Assert.Less(menu.Normal.z, -0.9f, "the wall faces the player: its outward side is toward -z");

            menu.SetColour(new Color(0.1f, 0.9f, 0.2f));
            menu.SetBrush(10);
            for (float u = 0.3f; u <= 0.7f; u += 0.02f)
            {
                menu.SprayAt(new Vector2(u, 0.5f));
            }
            menu.LiftBrush();
            Assert.IsFalse(menu.Canvas.IsEmpty);
            menu.Close(apply: true);
            yield return null;

            Assert.IsFalse(menu.IsOpen);
            Assert.IsTrue(reader.enabled, "the player was not given the controls back");
            Assert.AreEqual(1, GraffitiWall.Count, "the piece did not go up");
            GraffitiPanel panel = Object.FindFirstObjectByType<GraffitiPanel>();
            Assert.IsNotNull(panel);
            Assert.AreEqual(3.0f - GraffitiWall.Proud, panel.transform.position.z, 0.02f, "the panel is not on the wall");
            Assert.Greater(Vector3.Dot(panel.transform.forward, Vector3.forward), 0.99f, "the panel's back is not to the wall");
            var put = new GraffitiCanvas();
            Assert.IsTrue(put.Decode(panel.Piece.Paint));
            Color32 middle = put.Pixels[(GraffitiCanvas.Size / 2) * GraffitiCanvas.Size + GraffitiCanvas.Size / 2];
            Assert.Greater(middle.a, 128, "the paint is not on the panel");
            Assert.Greater(middle.g, middle.r, "the paint is not the colour sprayed");
            Material material = panel.GetComponent<Renderer>().sharedMaterial;
            Assert.IsTrue(material.shader.name.Contains("Lit"), $"the paint is not lit ({material.shader.name})");
            Assert.AreSame(panel.Texture, material.GetTexture("_BaseMap"), "the panel does not show its paint");
            Assert.IsFalse(panel.Texture.isReadable, "each piece keeps a spare copy of its paint in memory");
        }

        [UnityTest]
        public IEnumerator PaintingTheSameSpotAgainCarriesThePieceOn()
        {
            (PlayerInputReader reader, GraffitiMenuView menu, Camera camera) = Stage();
            yield return null;
            yield return RightClick();
            menu.SprayAt(new Vector2(0.5f, 0.5f));
            menu.Close(apply: true);
            yield return null;
            yield return RightClick();
            Assert.IsTrue(menu.IsOpen);
            Assert.IsTrue(menu.CarryingOn, "it should carry on the piece already there");
            Assert.IsFalse(menu.Canvas.IsEmpty, "the piece already there is not on the canvas");
            menu.SprayAt(new Vector2(0.2f, 0.2f));
            menu.Close(apply: true);
            yield return null;
            Assert.AreEqual(1, GraffitiWall.Count, "painting the same spot made a second piece");
        }

        [UnityTest]
        public IEnumerator PaintPastTheEdgeOfTheWallDoesNotStick()
        {
            (PlayerInputReader reader, GraffitiMenuView menu, Camera camera) = Stage();
            yield return null;
            // The wall's right-hand end is at x 6 (seen from the front, +x is to the right): a piece centred 20 cm short of it hangs mostly off.
            menu.OpenAt(reader, new Vector3(5.8f, 2.2f, 3.0f), Vector3.back);
            Assert.IsNotNull(menu.Canvas.Mask, "the canvas does not know where the wall is");
            menu.SprayAt(new Vector2(0.95f, 0.5f));   // 1.17 m right of the centre: past the end of the wall
            menu.LiftBrush();
            Assert.IsTrue(menu.Canvas.IsEmpty, "paint stuck to the air past the wall's end");
            menu.SprayAt(new Vector2(0.1f, 0.5f));   // 1 m left of the centre: on the wall
            menu.LiftBrush();
            Assert.IsFalse(menu.Canvas.IsEmpty, "paint did not stick to the wall");
            menu.Close(apply: true);
            yield return null;
            Assert.AreEqual(1, GraffitiWall.Count);
        }

        [UnityTest]
        public IEnumerator APillarStandingOutOfTheWallTakesNoPaintButSomethingLooseInFrontIsLookedPast()
        {
            (PlayerInputReader reader, GraffitiMenuView menu, Camera camera) = Stage();
            var pillar = Made(GameObject.CreatePrimitive(PrimitiveType.Cube));   // 35 cm out of the wall, 50 cm wide, round x 2
            pillar.transform.position = new Vector3(2.0f, 3.0f, 2.875f);
            pillar.transform.localScale = new Vector3(0.5f, 6.0f, 0.35f);
            var loose = Made(GameObject.CreatePrimitive(PrimitiveType.Cube));   // something loose (a body, a thrown crate) at the wall, round x 0.5
            loose.transform.position = new Vector3(0.5f, 2.2f, 2.55f);
            loose.transform.localScale = Vector3.one * 0.3f;
            loose.AddComponent<Rigidbody>().useGravity = false;
            Physics.SyncTransforms();
            yield return null;

            menu.OpenAt(reader, new Vector3(1.5f, 2.2f, 3.0f), Vector3.back);
            menu.SprayAt(new Vector2(0.69f, 0.5f));   // x 1.99: the pillar's face, 35 cm proud of where the piece lies
            menu.LiftBrush();
            Assert.IsTrue(menu.Canvas.IsEmpty, "paint went on the wall behind the pillar, where it would be hidden");
            menu.SprayAt(new Vector2(0.115f, 0.5f));   // x 0.5: the wall behind the loose thing
            menu.LiftBrush();
            Assert.IsFalse(menu.Canvas.IsEmpty, "something loose in front of the wall left a hole in it");
            menu.Close(apply: false);
        }

        [UnityTest]
        public IEnumerator ARightClickAtNothingOpensNothing()
        {
            (PlayerInputReader reader, GraffitiMenuView menu, Camera camera) = Stage();
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0.0f, 1.0f, 0.2f));   // at the sky
            yield return null;
            yield return RightClick();
            Assert.IsFalse(menu.IsOpen, "the canvas opened on the sky");
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0.0f, -1.0f, 0.05f));   // at the ground
            yield return RightClick();
            Assert.IsFalse(menu.IsOpen, "the canvas opened on the floor");
        }
    }
}
