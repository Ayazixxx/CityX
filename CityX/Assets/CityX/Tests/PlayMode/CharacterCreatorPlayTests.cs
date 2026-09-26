using System.Collections;
using System.Linq;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Drives the real creator, in the real title scene, and checks that a
    /// click on the body actually leaves paint on it.
    ///
    /// EVERY STEP OF THAT CHAIN FAILS SILENTLY. The camera can miss the body,
    /// the collider can be absent, the raycast can find the wrong thing, the
    /// triangle index can come back negative, the atlas can be unbound, the
    /// texture can never be applied. Not one of them throws or logs. The whole
    /// feature had been written and rewritten without anything ever confirming
    /// that a press turns into a coloured texel.
    ///
    /// It runs in play mode on purpose: OnEnable, Awake and the raycast against
    /// a freshly enabled collider are exactly the parts that do not happen in
    /// the editor, and those are where this has gone wrong before.
    /// </summary>
    public class CharacterCreatorPlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/Title.unity";

        private CharacterCreator _creator;
        private Camera _camera;

        private Scene _title;

        [UnitySetUp]
        public IEnumerator OpenTheCreator()
        {
            yield return LoadTitle();
            _title = SceneManager.GetSceneByPath(ScenePath);

            var title = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(title, "the title scene has no TitleScreen");
            title.Show(TitleScreen.Panel.Creator);

            // A frame, so the panel's OnEnable runs and switches the stage on.
            yield return null;

            _creator = Object.FindFirstObjectByType<CharacterCreator>();
            Assert.IsNotNull(_creator, "showing the creator did not switch its stage on");
            _camera = _creator.Camera;
            Assert.IsNotNull(_camera, "the creator has no camera");
        }

        /// <summary>
        /// ADDITIVELY, AND UNLOADED AGAIN AFTERWARDS. Loading it as the single
        /// scene took the test runner's own scene down with it and left the
        /// whole night city standing for everything that ran next: the
        /// locomotion tests spawned their bodies inside a building and
        /// measured a walk speed of 0.13 m/s against a wall. Three tests that
        /// pass on their own failed only in a full run, which is the worst
        /// kind of failure to be handed.
        /// </summary>
        private static IEnumerator LoadTitle()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
#else
            yield break;
#endif
        }

        [UnityTearDown]
        public IEnumerator CloseTheCreator()
        {
            if (_title.IsValid() && _title.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_title);
            }
        }

        /// <summary>
        /// The point on screen where the body's chest is. Taken from the rig
        /// rather than guessed, so it stays right if the framing changes.
        /// </summary>
        private Vector2 ChestOnScreen()
        {
            Vector3 chest = _creator.BodyCollider.transform.TransformPoint(
                _creator.BodyCollider.sharedMesh.bounds.center);
            return _camera.WorldToScreenPoint(chest);
        }

        /// <summary>
        /// The body must stand perfectly still, and stay standing still while
        /// it is turned.
        ///
        /// IT DID NOT. The prefab it is cloned from is a full ragdoll, and the
        /// strip that was meant to take that off destroyed the rigidbodies
        /// before the joints -- Unity refuses to remove a component another one
        /// depends on, so ten of each survived with their colliders gone. They
        /// had nothing to rest on. The instant play started the bones fell away
        /// and the skin stretched out behind them; turning the model just made
        /// it obvious. Every check made of this screen passed, because none of
        /// them ran physics.
        /// </summary>
        [UnityTest]
        public IEnumerator TheBodyStandsStillEvenWhileItIsTurned()
        {
            Assert.IsEmpty(_creator.GetComponentsInChildren<Rigidbody>(true),
                "the creator's body is still a ragdoll");
            Assert.IsEmpty(_creator.GetComponentsInChildren<Joint>(true),
                "the creator's body still has ragdoll joints");

            Transform[] bones = _creator.BodyRenderer.bones;
            Assert.IsNotEmpty(bones, "the body has no bones");
            var before = bones.Select(b => _creator.RotationPivot.InverseTransformPoint(b.position))
                .ToArray();

            // A second of real frames, with a turn part-way through, which is
            // all it took for the melted version to be unmistakable.
            for (int frame = 0; frame < 60; frame++)
            {
                if (frame == 20)
                {
                    _creator.PressAt(new Vector2(Screen.width - 4.0f, Screen.height * 0.5f));
                    _creator.Drag(new Vector2(Screen.width - 4.0f, Screen.height * 0.5f),
                        new Vector2(60.0f, 20.0f));
                }
                yield return new WaitForFixedUpdate();
            }

            for (int i = 0; i < bones.Length; i++)
            {
                // In the pivot's own space, so turning the model is not itself
                // counted as the bones having moved.
                Vector3 now = _creator.RotationPivot.InverseTransformPoint(bones[i].position);
                Assert.Less(Vector3.Distance(before[i], now), 0.001f,
                    $"{bones[i].name} drifted -- the body is being simulated, not held");
            }
        }

        [UnityTest]
        public IEnumerator AClickOnTheBodyLandsPaintOnIt()
        {
            _creator.Colour = Color.red;
            _creator.BrushRadiusPixels = 6;

            Texture2D atlas = _creator.Paint.Atlas;
            Assert.IsNotNull(atlas, "the creator has no paint atlas");

            Color32[] before = atlas.GetPixels32();

            _creator.PressAt(ChestOnScreen());
            yield return null;

            Color32[] after = atlas.GetPixels32();

            int changed = 0;
            int red = 0;
            for (int i = 0; i < before.Length; i++)
            {
                if (before[i].r == after[i].r && before[i].g == after[i].g
                    && before[i].b == after[i].b)
                {
                    continue;
                }
                changed++;
                if (after[i].r > after[i].g && after[i].r > after[i].b)
                {
                    red++;
                }
            }

            Assert.Greater(changed, 0,
                "clicking the middle of the character painted nothing at all -- the ray "
                + "missed the body, or the hit never became a triangle");
            Assert.Greater(red, 0, "something changed, but not to the colour being painted");
        }

        /// <summary>
        /// Clicking past the silhouette must not paint. If it did, the
        /// backdrop would be dragging colour onto whatever part of the atlas a
        /// stray ray happened to resolve to.
        /// </summary>
        /// <summary>
        /// The face, which is a DIFFERENT MESH ON A DIFFERENT COLLIDER.
        ///
        /// The skull left the body's shape field when it stopped being fused
        /// into the neck, so a click on the face is a hit on its own collider
        /// with its own triangle index counted from its own zero. Getting the
        /// offset into the shared index space wrong does not throw -- it paints
        /// somewhere else on the body entirely.
        /// </summary>
        [UnityTest]
        public IEnumerator AClickOnTheFaceLandsPaintOnTheHead()
        {
            Assert.IsNotNull(_creator.HeadCollider, "the head has no collider of its own");

            _creator.Colour = Color.red;
            _creator.BrushRadiusPixels = 5;

            Texture2D atlas = _creator.Paint.Atlas;
            Color32[] before = atlas.GetPixels32();

            // At the middle of the skull, which is the head bone -- but a few
            // centimetres OFF it. A ray aimed exactly at the sphere's centre from
            // the creator camera can pass exactly through the equator vertex
            // where the front triangles meet, and PhysX lets a ray through an
            // exact vertex or edge (measured: dead centre missed, a hand's
            // breadth above or below hit at the same distance). Whether the
            // camera lines up with a vertex depends on the exact framing the
            // title scene was built with, so an exact-centre aim can flip from
            // pass to fail just by rebuilding that scene.
            Vector3 face = _creator.HeadCollider.transform.position + new Vector3(0.03f, 0.04f, 0.0f);
            _creator.PressAt(_creator.Camera.WorldToScreenPoint(face));
            yield return null;

            Color32[] after = atlas.GetPixels32();

            int headRow = PaintAtlas.RowOf(HumanBodyBones.Head);
            int fromY = headRow * PaintAtlas.Cell;
            int toY = (headRow + 1) * PaintAtlas.Cell;

            int onHead = 0;
            int elsewhere = 0;
            for (int i = 0; i < before.Length; i++)
            {
                if (before[i].r == after[i].r && before[i].g == after[i].g
                    && before[i].b == after[i].b)
                {
                    continue;
                }
                int y = i / PaintAtlas.Width;
                if (y >= fromY && y < toY)
                {
                    onHead++;
                }
                else
                {
                    elsewhere++;
                }
            }

            Assert.Greater(onHead, 0, "clicking the face painted nothing on the head's row");
            Assert.AreEqual(0, elsewhere,
                "clicking the face also painted somewhere else -- the head's triangles are "
                + "landing at the wrong offset in the shared index space");
        }

        [UnityTest]
        public IEnumerator AClickOnEmptySpaceLeavesTheBodyAlone()
        {
            Texture2D atlas = _creator.Paint.Atlas;
            Color32[] before = atlas.GetPixels32();

            _creator.Colour = Color.green;
            // Hard against the right edge, well clear of the body and of the
            // tool column on the left.
            _creator.PressAt(new Vector2(Screen.width - 4.0f, Screen.height * 0.5f));
            yield return null;

            Color32[] after = atlas.GetPixels32();
            for (int i = 0; i < before.Length; i++)
            {
                Assert.AreEqual(before[i].r, after[i].r, "the empty backdrop painted the body");
                Assert.AreEqual(before[i].g, after[i].g, "the empty backdrop painted the body");
                Assert.AreEqual(before[i].b, after[i].b, "the empty backdrop painted the body");
            }
        }

        /// <summary>
        /// A missed click has to start a rotate instead, which is the whole
        /// "decided once, at mouse-down" rule the creator is built around.
        /// </summary>
        [UnityTest]
        public IEnumerator MissingTheBodyTurnsItInstead()
        {
            Quaternion before = _creator.RotationPivot.localRotation;

            _creator.PressAt(new Vector2(Screen.width - 4.0f, Screen.height * 0.5f));
            yield return null;
            _creator.Drag(new Vector2(Screen.width - 4.0f, Screen.height * 0.5f),
                new Vector2(40.0f, 0.0f));
            yield return null;

            Assert.AreNotEqual(before.eulerAngles.y,
                _creator.RotationPivot.localRotation.eulerAngles.y,
                "dragging off the body did not turn it");
        }

        [UnityTest]
        public IEnumerator UndoPutsThePaintBack()
        {
            _creator.Colour = Color.blue;
            _creator.BrushRadiusPixels = 6;

            Texture2D atlas = _creator.Paint.Atlas;
            Color32[] clean = atlas.GetPixels32();

            _creator.PressAt(ChestOnScreen());
            yield return null;
            Assert.IsTrue(_creator.CanUndo, "painting did not record anything to undo");

            _creator.Undo();
            yield return null;

            Color32[] after = atlas.GetPixels32();
            for (int i = 0; i < clean.Length; i++)
            {
                Assert.AreEqual(clean[i].r, after[i].r, $"texel {i} did not come back");
                Assert.AreEqual(clean[i].g, after[i].g, $"texel {i} did not come back");
                Assert.AreEqual(clean[i].b, after[i].b, $"texel {i} did not come back");
            }
            Assert.IsTrue(_creator.CanRedo, "the undone stroke cannot be redone");
        }

        /// <summary>
        /// What ENTER THE CITY hands over. An atlas that will not survive the
        /// round trip is a character who loads back blank, and the encoding is
        /// gzipped raw pixels rather than a PNG precisely because the image
        /// conversion module is not referenced here.
        /// </summary>
        [UnityTest]
        public IEnumerator ThePaintSurvivesBeingSavedAndLoaded()
        {
            _creator.Colour = new Color(0.2f, 0.7f, 0.9f);
            _creator.BrushRadiusPixels = 8;
            _creator.PressAt(ChestOnScreen());
            yield return null;

            byte[] encoded = _creator.Paint.Encode();
            Assert.IsNotNull(encoded);
            Assert.Greater(encoded.Length, 0, "the painted atlas encoded to nothing");

            Color32[] expected = _creator.Paint.Atlas.GetPixels32();

            var reloaded = new BodyPaint(CharacterCreator.EraseColour);
            Assert.IsTrue(reloaded.Decode(encoded), "the encoded paint would not load back");

            Color32[] actual = reloaded.Atlas.GetPixels32();
            Assert.AreEqual(expected.Length, actual.Length, "the atlas came back a different size");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].r, actual[i].r, $"texel {i} changed on the way back");
                Assert.AreEqual(expected[i].g, actual[i].g, $"texel {i} changed on the way back");
                Assert.AreEqual(expected[i].b, actual[i].b, $"texel {i} changed on the way back");
            }
        }

        /// <summary>
        /// Choosing a hairstyle has to put a real mesh on the head bone, above
        /// the shoulders rather than down the back -- the exact way the Godot
        /// build first got this wrong.
        /// </summary>
        [UnityTest]
        public IEnumerator ChoosingAHairstylePutsItOnTheHead()
        {
            CharacterHair hair = _creator.Hair;
            Assert.IsNotNull(hair, "the creator has no hair component");
            Assert.IsNotNull(hair.Head, "the hair has no head bone to sit on");

            _creator.SetHairStyle(CharacterHair.Embermane);
            yield return null;

            // BY NAME, not "any MeshFilter under the head". The skull is a mesh
            // on the head bone too now -- see HeadMesh -- so the loose search
            // this used to do found the head and reported the hairstyle as
            // present whether or not it had been built.
            MeshFilter worn = Worn(hair);
            Assert.IsNotNull(worn, "choosing a hairstyle produced no mesh");
            Assert.IsNotNull(worn.sharedMesh, "the hairstyle's mesh is empty");

            float crown = hair.Head.position.y + FusedBodyShape.HeadRadius;
            var renderer = worn.GetComponent<MeshRenderer>();
            Assert.IsNotNull(renderer, "the hair has no renderer");
            Assert.Greater(renderer.bounds.max.y, crown,
                "the hair does not reach above the crown -- it is hanging below the head");
            Assert.Greater(renderer.bounds.center.y, hair.Head.position.y,
                "the hair's bulk is below the middle of the head");

            _creator.SetHairStyle(CharacterHair.None);
            yield return null;
            Assert.IsNull(Worn(hair), "going back to bald left the hair on");
        }

        /// <summary>
        /// Each hairstyle that comes from a model file, on the real head in the real scene, lands on the crown -- neither
        /// down the neck nor floating over the head, and centred on it. The fit's own maths is
        /// tested without a scene (CharacterHairFitTests); this is the model, the head bone and
        /// the fit together.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryModelHairstyleSitsOnTheCrownOfTheRealHead()
        {
            CharacterHair hair = _creator.Hair;

            foreach (string style in new[] { CharacterHair.AnimeHair0, CharacterHair.Hair1 })
            {
                Assert.IsTrue(System.Array.IndexOf(CharacterHair.ValidStyles, style) > 0, $"\"{style}\" is not a valid style");

                _creator.SetHairStyle(style);
                yield return null;

                MeshFilter worn = Worn(hair);
                Assert.IsNotNull(worn, $"choosing \"{style}\" produced no mesh -- is its baked mesh wired?");
                Assert.IsNotNull(worn.sharedMesh, $"\"{style}\"'s mesh is empty");

                var renderer = worn.GetComponent<MeshRenderer>();
                Vector3 head = hair.Head.position;
                float crown = head.y + FusedBodyShape.HeadRadius;
                Assert.Greater(renderer.bounds.max.y, crown, $"\"{style}\" does not reach the crown");
                Assert.Less(renderer.bounds.max.y, crown + 0.25f, $"\"{style}\" floats above the head");

                Vector3 centre = renderer.bounds.center;
                float sideways = Mathf.Sqrt((centre.x - head.x) * (centre.x - head.x) + (centre.z - head.z) * (centre.z - head.z));
                Assert.Less(sideways, FusedBodyShape.HeadRadius * 0.5f,
                    $"\"{style}\" is off to one side or hanging off the front or back of the head");
            }
        }

        /// <summary>
        /// The creator's hair buttons -- the "select hairstyle" function -- each put their own style
        /// on. Pressed through the real button, so a button wired to the wrong index shows up.
        /// </summary>
        [UnityTest]
        public IEnumerator EachHairButtonPutsItsOwnStyleOnTheHead()
        {
            CharacterHair hair = _creator.Hair;

            foreach (string style in CharacterHair.ValidStyles)
            {
                UnityEngine.UI.Button button = null;
                foreach (UnityEngine.UI.Button candidate in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>())
                {
                    if (candidate.gameObject.name == "Hair" + style + "Button" && candidate.gameObject.scene.IsValid())
                    {
                        button = candidate;
                        break;
                    }
                }
                Assert.IsNotNull(button, $"there is no hair button for \"{style}\"");

                button.onClick.Invoke();
                yield return null;

                Assert.AreEqual(style, hair.Style, $"the \"{style}\" button did not select \"{style}\"");
                if (style == CharacterHair.None)
                {
                    Assert.IsNull(Worn(hair), "the bald button left hair on");
                }
                else
                {
                    Assert.IsNotNull(Worn(hair), $"the \"{style}\" button produced no hair");
                }
            }
        }

        /// <summary>
        /// Saving a fit re-places the hair on every head wearing the style at once, and resetting
        /// puts it back -- the Inspector's Save and Reset buttons, without the Inspector. Uses a
        /// settings object of its own so the saved asset is left alone.
        /// </summary>
        [UnityTest]
        public IEnumerator SavingAFitMovesTheWornHairAndResettingPutsItBack()
        {
            HairFitSettings previous = HairFitSettings.Current;
            var scratch = ScriptableObject.CreateInstance<HairFitSettings>();
            HairFitSettings.Current = scratch;
            try
            {
                CharacterHair hair = _creator.Hair;
                _creator.SetHairStyle(CharacterHair.AnimeHair0);
                yield return null;

                Transform worn = hair.Worn;
                Assert.IsNotNull(worn, "no anime hair to move");
                Vector3 automatic = worn.localPosition;

                var tuned = new CharacterHair.Fit(automatic + new Vector3(0.0f, 0.05f, -0.02f), new Vector3(0.0f, 8.0f, 0.0f), Vector3.one * 0.5f);
                CharacterHair.SaveFit(CharacterHair.AnimeHair0, tuned);

                Assert.AreEqual(tuned.Position.y, worn.localPosition.y, 0.0001f, "saving the fit did not move the worn hair");
                Assert.AreEqual(8.0f, worn.localEulerAngles.y, 0.01f, "saving the fit did not turn the worn hair");
                Assert.AreEqual(0.5f, worn.localScale.x, 0.0001f, "saving the fit did not resize the worn hair");

                CharacterHair.ResetFit(CharacterHair.AnimeHair0);
                Assert.AreEqual(automatic.y, worn.localPosition.y, 0.0001f, "resetting the fit did not put the hair back");
            }
            finally
            {
                HairFitSettings.Current = previous;
                Object.Destroy(scratch);
            }
        }

        private static MeshFilter Worn(CharacterHair hair)
        {
            Transform found = hair.Head.Find("Hair");
            return found != null ? found.GetComponent<MeshFilter>() : null;
        }
    }
}
