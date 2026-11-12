using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
        internal const string ScenePath ="Assets/CityX/Scenes/Title.unity";

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
        internal static IEnumerator LoadTitle()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            // The city comes in behind the black once its plan is worked out (TitleStage): the tests look at the title with it there.
            var stage = Object.FindFirstObjectByType<CityX.UI.TitleStage>();
            for (float waited = 0.0f; stage != null && !stage.Placed && waited < 20.0f; waited += Time.unscaledDeltaTime)
            {
                yield return null;
            }
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

        private Vector2 ChestOnScreen() => ChestOnScreen(_creator);

        /// <summary>
        /// The point on screen where the body's chest is. Taken from the rig
        /// rather than guessed, so it stays right if the framing changes.
        /// </summary>
        internal static Vector2 ChestOnScreen(CharacterCreator creator)
        {
            Vector3 chest = creator.BodyCollider.transform.TransformPoint(
                creator.BodyCollider.sharedMesh.bounds.center);
            return creator.Camera.WorldToScreenPoint(chest);
        }

        /// <summary>Where the middle of a part of the interface is on the screen, in pixels.</summary>
        internal static Vector2 ScreenCentre(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(uiCamera, (corners[0] + corners[2]) * 0.5f);
        }

        /// <summary>The front-most part of the interface at a screen point, the one a click there reaches; null where there is none.</summary>
        internal static GameObject InterfaceAt(Vector2 screen)
        {
            EventSystem events = EventSystem.current;
            Assert.IsNotNull(events, "no event system to click with");
            var hits = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = screen }, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
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
            // It keeps its ragdoll whole (it is dropped off the belt's end), every part of it held until then.
            foreach (Rigidbody part in _creator.GetComponentsInChildren<Rigidbody>(true))
            {
                Assert.IsTrue(part.isKinematic, $"{part.name} is loose: the creator's body is being simulated");
            }

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
        /// The creator shows the body in a T-pose but its mesh is modelled arms-down. The click target and the brush were the mesh at rest, so a
        /// click on an outstretched arm went through it (and one beside the hips painted an arm nobody could see). Now both are the body as shown.
        /// </summary>
        [UnityTest]
        public IEnumerator AClickOnAnOutstretchedArmPaintsThatArm()
        {
            _creator.Colour = Color.red;
            _creator.BrushRadiusPixels = 1;
            Animator animator = _creator.BodyRenderer.GetComponentInParent<Animator>(true);
            Assert.IsNotNull(animator, "the creator's body has no animator to find its bones by");
            Transform forearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Assert.IsNotNull(forearm);
            Assert.IsNotNull(hand);
            Vector3 onForearm = Vector3.Lerp(forearm.position, hand.position, 0.4f);

            Color32[] before = _creator.Paint.Atlas.GetPixels32();
            _creator.PressAt(_camera.WorldToScreenPoint(onForearm));
            yield return null;
            Color32[] after = _creator.Paint.Atlas.GetPixels32();

            int armRow = PaintAtlas.RowOf(PaintAtlas.PaintBoneFor(HumanBodyBones.LeftLowerArm));
            int onArm = 0;
            int elsewhere = 0;
            for (int i = 0; i < before.Length; i++)
            {
                if (before[i].r == after[i].r && before[i].g == after[i].g && before[i].b == after[i].b)
                {
                    continue;
                }
                if (i / PaintAtlas.Width / PaintAtlas.RowHeight == armRow) onArm++;
                else elsewhere++;
            }
            Assert.Greater(onArm, 0, "clicking the outstretched forearm painted nothing on the arm -- the click went through it");
            Assert.AreEqual(0, elsewhere, "clicking the forearm painted somewhere other than the arm");
        }

        /// <summary>Owner, 2026-11-05: "can you make it zoom into wherever you mouse is hovering? so you can zoom into the head or feet or anywhere".</summary>
        [UnityTest]
        public IEnumerator ScrollingZoomsTowardWhatTheMouseIsOverAndOutBringsTheWholeBodyBack(
            [Values(HumanBodyBones.Head, HumanBodyBones.LeftFoot, HumanBodyBones.RightHand)] HumanBodyBones part)
        {
            yield return null;
            Vector3 home = _camera.transform.localPosition;
            Transform bone = _creator.GetComponentInChildren<Animator>(true).GetBoneTransform(part);
            Assert.IsNotNull(bone);
            Vector3 world = bone.position;
            Vector2 before = _camera.WorldToScreenPoint(world);
            float distanceBefore = Vector3.Distance(_camera.transform.position, world);

            for (int i = 0; i < 12; i++)
            {
                _creator.ZoomAt(before, closer: true);
            }
            Vector2 after = _camera.WorldToScreenPoint(world);
            // Up and down only (owner, 2026-11-05): the part stays at the mouse's height, and the camera on the body's middle line across.
            Assert.Less(Mathf.Abs(before.y - after.y), 0.03f * Screen.height, $"zooming in moved the {part} up or down away from the mouse");
            Assert.AreEqual(home.x, _camera.transform.localPosition.x, 1e-3f, $"zooming at the {part} slid the camera off to the side");
            Assert.Less(Vector3.Distance(_camera.transform.position, world), distanceBefore * 0.5f, $"it did not come in on the {part}");

            for (int i = 0; i < 12; i++)
            {
                _creator.ZoomAt(new Vector2(Screen.width * 0.9f, Screen.height * 0.9f), closer: false);
            }
            Vector3 back = _camera.transform.localPosition;
            Assert.AreEqual(home.x, back.x, 1e-3f, "zoomed out, the camera is off the middle");
            Assert.AreEqual(home.y, back.y, 1e-3f, "zoomed out, the camera is off the middle");
            Assert.GreaterOrEqual(back.z, home.z, "twelve notches out did not bring it back out");
        }

        /// <summary>
        /// Saves Logs/CityShots/brush-sizes.png: taps of sizes 1 to 8 across the chest, a wavy size-1 and size-3 line, a stroke down the
        /// outstretched arm, and the brush ring. Run with a graphics device and look at it.
        /// </summary>
        [UnityTest, Explicit]
        public IEnumerator BrushCapture()
        {
            _creator.Colour = new Color(0.1f, 0.1f, 0.12f);
            // Close, as a player painting detail would be.
            for (int i = 0; i < 7; i++)
            {
                _creator.Zoom(-CharacterCreator.ZoomStep);
            }
            yield return null;
            Vector2 chest = ChestOnScreen();
            float scale = Screen.height / 1000.0f;
            for (int size = 1; size <= 8; size++)
            {
                _creator.BrushRadiusPixels = size;
                _creator.PressAt(chest + new Vector2((size - 4.5f) * 30.0f * scale, 90.0f * scale));
                yield return null;
            }
            foreach (int size in new[] { 1, 3 })
            {
                _creator.BrushRadiusPixels = size;
                float y = size == 1 ? 20.0f : -50.0f;
                Vector2 start = chest + new Vector2(-110.0f, y) * scale;
                _creator.PressAt(start);
                for (int i = 1; i <= 60; i++)
                {
                    Vector2 at = chest + new Vector2(-110.0f + i * 3.6f, y + Mathf.Sin(i * 0.25f) * 18.0f) * scale;
                    _creator.Drag(at, Vector2.zero);
                }
                // A release, so the next press starts a stroke of its own.
                yield return null;
            }

            Animator animator = _creator.BodyRenderer.GetComponentInParent<Animator>(true);
            Vector3 shoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
            Vector3 hand = animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
            _creator.BrushRadiusPixels = 2;
            _creator.Colour = new Color(0.8f, 0.15f, 0.1f);
            _creator.PressAt(_camera.WorldToScreenPoint(shoulder));
            for (int i = 1; i <= 40; i++)
            {
                _creator.Drag(_camera.WorldToScreenPoint(Vector3.Lerp(shoulder, hand, i / 40.0f)), Vector2.zero);
            }
            _creator.Paint.Apply();

            _creator.BrushRadiusPixels = 6;
            _creator.ShowBrushRing(chest + new Vector2(0.0f, -150.0f) * scale);
            yield return null;
            _creator.ShowBrushRing(chest + new Vector2(0.0f, -150.0f) * scale);

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                yield break;
            }
            int w = Screen.width, h = Screen.height;
            var rt = RenderTexture.GetTemporary(w, h, 24);
            RenderTexture previous = _camera.targetTexture;
            _camera.targetTexture = rt;
            _camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            image.Apply();
            _camera.targetTexture = previous;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            string directory = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(directory);
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, "brush-sizes.png"), (byte[])encode.Invoke(null, new object[] { image }));
            Object.Destroy(image);
        }

        [UnityTest]
        public IEnumerator TheBrushRingSitsOnTheBodyAtTheBrushsSize()
        {
            _creator.Current = CharacterCreator.Tool.Brush;
            _creator.BrushRadiusPixels = 10;
            _creator.ShowBrushRing(ChestOnScreen());
            yield return null;
            _creator.ShowBrushRing(ChestOnScreen());

            Assert.IsTrue(_creator.BrushRingVisible, "no brush ring over the chest");
            LineRenderer ring = _creator.GetComponentsInChildren<LineRenderer>(true).First(r => r.name == "BrushRing");
            Assert.AreNotEqual(0, _camera.cullingMask & (1 << ring.gameObject.layer), "the creator's camera does not draw the ring's layer");
            var points = new Vector3[ring.positionCount];
            ring.GetPositions(points);
            Vector3 centre = points.Aggregate(Vector3.zero, (sum, p) => sum + p) / points.Length;
            float expected = BodyPaint.BrushRadiusMetres(10) * _creator.BodyCollider.transform.lossyScale.x;
            foreach (Vector3 p in points)
            {
                Assert.AreEqual(expected, Vector3.Distance(centre, p), expected * 0.02f, "the ring is not the brush's size");
            }
            Ray ray = _camera.ScreenPointToRay(ChestOnScreen());
            Assert.IsTrue(_creator.BodyCollider.Raycast(ray, out RaycastHit hit, 50.0f));
            Assert.Less(Vector3.Distance(hit.point, centre), 0.02f, "the ring is not where the cursor meets the body");

            _creator.SetBrushSize(99);
            Assert.AreEqual(CharacterCreator.MaximumBrushSize, _creator.BrushRadiusPixels, "the size keys go past the biggest brush");
            _creator.SetBrushSize(0);
            Assert.AreEqual(CharacterCreator.MinimumBrushSize, _creator.BrushRadiusPixels, "the size keys go below the smallest brush");
        }

        /// <summary>
        /// The face, which is a DIFFERENT MESH ON A DIFFERENT COLLIDER.
        ///
        /// The skull left the body's shape field when it stopped being fused
        /// into the neck, so a click on the face is a hit on its own collider
        /// with its own triangle index counted from its own zero. Getting the
        /// offset into the shared index space wrong does not throw -- it paints
        /// somewhere else on the body entirely.
        /// </summary>
        /// <summary>
        /// The bucket on the face pours over the whole body, a little each frame (owner, 2026-11-11: "make the paint bucket tool work across
        /// the entire body right now head and body our separated. also there is a small drop in frames whenever i use the paint bucket tool"):
        /// on the creator's own body, as it stands to be painted.
        /// </summary>
        [UnityTest]
        public IEnumerator TheBucketOnTheFacePoursOverTheWholeBodyOverFrames()
        {
            _creator.Colour = Color.red;
            _creator.Current = CharacterCreator.Tool.Bucket;
            Vector3 face = _creator.HeadCollider.transform.position + new Vector3(0.03f, 0.04f, 0.0f);
            _creator.PressAt(_creator.Camera.WorldToScreenPoint(face));
            Assert.IsTrue(_creator.Paint.BucketFilling, "the whole body was filled in the frame of the click: the game stops while it is done");
            int frames = 0;
            while (_creator.Paint.BucketFilling && frames < 300)
            {
                frames++;
                yield return null;
            }
            Assert.IsFalse(_creator.Paint.BucketFilling, "the fill never finished pouring");

            Color32[] atlas = _creator.Paint.Atlas.GetPixels32();
            foreach (HumanBodyBones part in PaintAtlas.Parts)
            {
                int y = PaintAtlas.RowOf(part) * PaintAtlas.RowHeight + PaintAtlas.RowHeight / 2;
                int red = 0;
                for (int x = 0; x < PaintAtlas.Width; x++)
                {
                    Color32 texel = atlas[y * PaintAtlas.Width + x];
                    if (texel.r > 200 && texel.g < 40 && texel.b < 40)
                    {
                        red++;
                    }
                }
                Assert.Greater(red, PaintAtlas.Width / 4, $"the bucket on the face did not pour onto the {part}");
            }
        }

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
            int fromY = headRow * PaintAtlas.RowHeight;
            int toY = (headRow + 1) * PaintAtlas.RowHeight;

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

        /// <summary>
        /// Clicking past the silhouette must not paint. If it did, the
        /// backdrop would be dragging colour onto whatever part of the atlas a
        /// stray ray happened to resolve to.
        /// </summary>
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

            _creator.SetHairStyle(CharacterHair.Hair1);
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

            foreach (string style in new[] { CharacterHair.Hair1 })
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

        /// <summary>The title scene's button called <paramref name="name"/>, whichever tab is up: a page is hidden, never switched off.</summary>
        private static Button SceneButton(string name)
        {
            foreach (Button candidate in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (candidate.gameObject.name == name && candidate.gameObject.scene.IsValid() && candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>Puts the HAIR tab up through its own button: the hairstyles and the hair colour are on its page.</summary>
        private static IEnumerator ShowTheHairTab()
        {
            Button tab = SceneButton("HairTab");
            Assert.IsNotNull(tab, "the creator has no HAIR tab");
            tab.onClick.Invoke();
            yield return null;
            Assert.AreEqual(CreatorTabs.Hair, Object.FindFirstObjectByType<CreatorTabs>().Current, "the HAIR tab did not come up");
        }

        /// <summary>
        /// The HAIR tab's tiles -- the "select hairstyle" function -- each put their own style
        /// on. Pressed through the real button, so a tile wired to the wrong index shows up.
        /// </summary>
        [UnityTest]
        public IEnumerator EachHairButtonPutsItsOwnStyleOnTheHead()
        {
            yield return ShowTheHairTab();
            CharacterHair hair = _creator.Hair;

            foreach (string style in CharacterHair.ValidStyles)
            {
                Button button = SceneButton("Hair" + style + "Button");
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
                    // (Any renderer: a style baked to bend is skinned, HairBend.)
                    Assert.IsNotNull(hair.Worn.GetComponent<Renderer>(), $"the \"{style}\" button produced no hair");
                }
            }
        }

        /// <summary>Every hairstyle picture's hair takes the hair colour as it is picked (owner, 2026-11-05: "make the icons change color as you change hair color").</summary>
        [UnityTest]
        public IEnumerator TheHairstylePicturesTakeTheHairColour()
        {
            yield return ShowTheHairTab();
            foreach (Color colour in new[] { Color.red, HairPage.Swatches[2] })
            {
                _creator.SetHairColour(colour);
                yield return null;
                int tinted = 0;
                foreach (string style in CharacterHair.ValidStyles)
                {
                    Transform tint = SceneButton("Hair" + style + "Button").transform.Find("HairTint");
                    if (style == CharacterHair.None)
                    {
                        Assert.IsNull(tint, "the bald head's picture has hair to tint");
                        continue;
                    }
                    Assert.IsNotNull(tint, $"\"{style}\"'s picture has no hair layer: rebake the icons and rebuild the title");
                    Assert.AreEqual(colour, tint.GetComponent<RawImage>().color, $"\"{style}\"'s hair is not the colour picked");
                    tinted++;
                }
                Assert.Greater(tinted, 0);
            }
        }

        /// <summary>The tile of the style the head wears is framed in gold and no other: on the HAIR tab, that is what says which style is on.</summary>
        [UnityTest]
        public IEnumerator OnlyTheWornStylesTileIsFramedInGold()
        {
            yield return ShowTheHairTab();
            string last = CharacterHair.ValidStyles[CharacterHair.ValidStyles.Length - 1];
            foreach (string worn in new[] { CharacterHair.Hair1, last, CharacterHair.None })
            {
                _creator.SetHairStyle(worn);
                yield return null;
                foreach (string style in CharacterHair.ValidStyles)
                {
                    Image frame = SceneButton("Hair" + style + "Button").GetComponent<Image>();
                    Assert.AreEqual(style == worn ? HairStyleTiles.Worn : HairStyleTiles.Idle, frame.color,
                        $"wearing \"{worn}\", the \"{style}\" tile is {(style == worn ? "not framed" : "framed as well")}");
                }
            }
        }

        /// <summary>
        /// On the HAIR tab every hair button can be SEEN AND CLICKED, bald or with hair (owner, 2026-10-13: "i dont see the hyde hairstyle in the
        /// character creator" -- its row sat under the left column's opaque bottom bar). Scrolled into view where the grid has to scroll to it,
        /// a UI raycast at the button's centre must land on that very button.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryHairButtonIsOnScreenAndNothingCoversIt()
        {
            yield return ShowTheHairTab();
            // With hair, the colour card comes up on the same page.
            foreach (string worn in new[] { CharacterHair.None, CharacterHair.Hair1 })
            {
                _creator.SetHairStyle(worn);
                yield return null;
                yield return null;
                foreach (string style in CharacterHair.ValidStyles)
                {
                    Button button = SceneButton("Hair" + style + "Button");
                    Assert.IsNotNull(button, $"there is no visible hair button for \"{style}\"");
                    var rect = (RectTransform)button.transform;
                    yield return ScrollIntoView(rect);
                    Vector2 centre = ScreenCentre(rect);
                    Assert.IsTrue(centre.x > 0 && centre.x < Screen.width && centre.y > 0 && centre.y < Screen.height,
                        $"the \"{style}\" button is off screen at {centre}");
                    GameObject top = InterfaceAt(centre);
                    Assert.IsNotNull(top, $"nothing at all under the \"{style}\" button");
                    Assert.IsTrue(top.transform.IsChildOf(button.transform),
                        $"wearing \"{worn}\", the \"{style}\" button is covered by {top.name}");
                }
            }
        }

        /// <summary>
        /// Scrolls the grid <paramref name="item"/> sits in until the item is inside the grid's window, and lets the grid settle: one that
        /// cannot scroll that far clamps itself back, and the test says so.
        /// </summary>
        private static IEnumerator ScrollIntoView(RectTransform item)
        {
            ScrollRect scroll = item.GetComponentInParent<ScrollRect>();
            if (scroll == null)
            {
                yield break;
            }
            RectTransform window = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            Bounds placed = RectTransformUtility.CalculateRelativeRectTransformBounds(window, item);
            Vector2 at = scroll.content.anchoredPosition;
            at.y += Mathf.Max(0.0f, window.rect.yMin - placed.min.y) - Mathf.Max(0.0f, placed.max.y - window.rect.yMax);
            scroll.content.anchoredPosition = at;
            yield return null;
            placed = RectTransformUtility.CalculateRelativeRectTransformBounds(window, item);
            Assert.IsTrue(placed.min.y >= window.rect.yMin - 0.5f && placed.max.y <= window.rect.yMax + 0.5f,
                $"{item.name} cannot be scrolled into view");
        }

        private static Color WornColour(CharacterHair hair)
        {
            var block = new MaterialPropertyBlock();
            hair.Worn.GetComponent<MeshRenderer>().GetPropertyBlock(block, 0);
            return block.GetColor("_BaseColor");
        }

        /// <summary>The hair colour card is only there while the character has hair: a colour for a bald head is a control that does nothing.</summary>
        [UnityTest]
        public IEnumerator TheHairColourCardAppearsOnlyWhileTheCharacterHasHair()
        {
            yield return ShowTheHairTab();
            var picker = Object.FindFirstObjectByType<HairColourControls>(FindObjectsInactive.Include);
            Assert.IsNotNull(picker, "the creator has no hair colour picker");

            _creator.SetHairStyle(CharacterHair.None);
            yield return null;
            Assert.AreEqual(0.0f, picker.Card.alpha, "the colour card is showing over a bald head");
            Assert.IsFalse(picker.Card.blocksRaycasts, "the hidden colour card still takes clicks");

            _creator.SetHairStyle(CharacterHair.Hair1);
            yield return null;
            Assert.AreEqual(1.0f, picker.Card.alpha, "the colour card did not appear when hair was chosen");
            Assert.IsTrue(picker.Card.blocksRaycasts);
        }

        /// <summary>Every swatch, pressed through its real button, dyes the hair that swatch's colour.</summary>
        [UnityTest]
        public IEnumerator EveryHairSwatchDyesTheHairItsOwnColour()
        {
            yield return ShowTheHairTab();
            _creator.SetHairStyle(CharacterHair.Hair1);
            yield return null;

            var swatches = new List<Button>();
            foreach (Button candidate in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (candidate.gameObject.name.StartsWith("HairSwatch") && candidate.gameObject.scene.IsValid())
                {
                    swatches.Add(candidate);
                }
            }
            Assert.AreEqual(HairPage.Swatches.Length, swatches.Count, "a hair colour swatch is missing");

            foreach (Button swatch in swatches)
            {
                Color wanted = swatch.GetComponent<Image>().color;
                swatch.onClick.Invoke();
                yield return null;

                Assert.AreEqual(wanted.r, _creator.HairColour.r, 0.001f, $"{swatch.name} did not dye the hair");
                Assert.AreEqual(wanted.g, _creator.HairColour.g, 0.001f, $"{swatch.name} did not dye the hair");
                Assert.AreEqual(wanted.b, _creator.HairColour.b, 0.001f, $"{swatch.name} did not dye the hair");
                Assert.AreEqual(wanted.b, WornColour(_creator.Hair).b, 0.001f, $"{swatch.name}: the worn mesh is not that colour");
            }
        }

        /// <summary>Any colour, not just a swatch: the hue strip and a click in the square reach the hair.</summary>
        [UnityTest]
        public IEnumerator ThePickerReachesAnyColourAndDyesTheHairWithIt()
        {
            yield return ShowTheHairTab();
            _creator.SetHairStyle(CharacterHair.Hair1);
            yield return null;

            var picker = Object.FindFirstObjectByType<HairColourControls>(FindObjectsInactive.Include);
            picker.Hue.value = 0.6f;                              // a blue hue
            yield return null;

            // A click in the middle of the square: half saturation, half value.
            var square = (RectTransform)picker.transform;
            Vector2 middle = RectTransformUtility.WorldToScreenPoint(null, square.position);
            picker.OnPointerClick(new PointerEventData(EventSystem.current)
            {
                position = middle,
            });

            Color expected = Color.HSVToRGB(0.6f, 0.5f, 0.5f);
            Assert.AreEqual(expected.r, _creator.HairColour.r, 0.03f, "a click in the colour square did not dye the hair");
            Assert.AreEqual(expected.g, _creator.HairColour.g, 0.03f);
            Assert.AreEqual(expected.b, _creator.HairColour.b, 0.03f);
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
                _creator.SetHairStyle(CharacterHair.Hair1);
                yield return null;

                Transform worn = hair.Worn;
                Assert.IsNotNull(worn, "no anime hair to move");
                Vector3 automatic = worn.localPosition;

                var tuned = new CharacterHair.Fit(automatic + new Vector3(0.0f, 0.05f, -0.02f), new Vector3(0.0f, 8.0f, 0.0f), Vector3.one * 0.5f);
                CharacterHair.SaveFit(CharacterHair.Hair1, tuned);

                Assert.AreEqual(tuned.Position.y, worn.localPosition.y, 0.0001f, "saving the fit did not move the worn hair");
                Assert.AreEqual(8.0f, worn.localEulerAngles.y, 0.01f, "saving the fit did not turn the worn hair");
                Assert.AreEqual(0.5f, worn.localScale.x, 0.0001f, "saving the fit did not resize the worn hair");

                CharacterHair.ResetFit(CharacterHair.Hair1);
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
