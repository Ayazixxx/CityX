using System;
using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The conveyor belt a character is made on, played in the real title scene with real keys (BeltIntro, docs/BELT_INTRO.md; owner,
    /// 2026-11-10: "when you hold right, it rolls out a plain white playermodel from the left of the screen ... once you have rolled him to
    /// the center of the screen the conveyer belt stops, and the character customization tools appear ... make this fade in ... a t pose
    /// mode where he actually lifts his arms ... once you complete your character. the conveyer belt start moving to the right again ...
    /// the camera angle changes ... rolls your character off the conveyer belt (it ragdolls in midair) and then ... the whole screen fades
    /// to black").
    /// </summary>
    public sealed class BeltIntroPlayTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private Scene _scene;
        private TitleScreen _title;
        private BeltIntro _belt;
        private ICharacterSource _sourceWas;
        private bool _firstStartWas;

        /// <summary>A server with a given number of characters, which makes and serves whatever it is asked for.</summary>
        private sealed class Server : ICharacterSource
        {
            public readonly List<CharacterSave> Characters = new List<CharacterSave>();

            public void List(Action<List<CharacterSave>> done) => done(new List<CharacterSave>(Characters));

            public void Create(CharacterSave requested, Action<CharacterSave> done)
            {
                Characters.Add(requested);
                done(requested);
            }

            public void Delete(string characterId, Action<bool> done) => done(true);

            public void Play(string characterId, Action<CharacterSave, bool> done) => done(Characters.Find(c => c.Id == characterId), false);

            public void Save(CharacterSave character, bool adminEdited)
            {
            }
        }

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _sourceWas = CharacterSources.Current;
            _firstStartWas = TitleScreen.BeltOnFirstStart;
            Time.captureFramerate = 60;
        }

        [UnityTearDown]
        public IEnumerator CloseTheTitle()
        {
            CharacterSources.Current = _sourceWas;
            TitleScreen.BeltOnFirstStart = _firstStartWas;
            PlaySession.Character = null;
            PlaySession.IsNewCharacter = false;
            ScreenBlack.FadeTo(0.0f, 0.0f);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
        }

        /// <summary>The title scene, on a server with <paramref name="characters"/> made already.</summary>
        private IEnumerator Open(int characters, bool beltOnFirstStart = false)
        {
            var server = new Server();
            for (int i = 0; i < characters; i++)
            {
                server.Characters.Add(CharacterSave.Create("Made" + i, CharacterSave.DefaultSkin));
            }
            CharacterSources.Current = server;
            TitleScreen.BeltOnFirstStart = beltOnFirstStart;
            yield return CharacterCreatorPlayTests.LoadTitle();
            _scene = SceneManager.GetSceneByPath(CharacterCreatorPlayTests.ScenePath);
            _title = UnityEngine.Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(_title, "the title scene has no TitleScreen");
            _belt = _title.CreatorPanel.GetComponent<BeltIntro>();
            Assert.IsNotNull(_belt, "the creator has no belt: rebuild the title scene");
        }

        private IEnumerator Begin()
        {
            yield return Open(1);
            _title.BeginCreator();
            yield return null;
            yield return null;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private IEnumerator Until(Func<bool> done, float seconds, string what)
        {
            float waited = 0.0f;
            while (!done() && waited < seconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(done(), $"{what} (waited {seconds} s)");
        }

        /// <summary>Where the body is across the making camera's picture: 0 its left edge, 1 its right.</summary>
        private float BodyAcrossTheScreen() => _belt.MakingCamera.WorldToViewportPoint(_belt.Rider.position).x;

        private IEnumerator RollIn()
        {
            Press(_keyboard.dKey);
            yield return Until(() => _belt.Ride.Now == BeltRide.Stage.Making, 10.0f, "holding D never set the body down in the middle");
            Release(_keyboard.dKey);
            yield return null;
        }

        /// <summary>
        /// Owner, 2026-11-10: "just make it say \"→\" with a box around it so its visibly a key, and have it appear in the center of the screen
        /// after two seconds with white text ... keep the flashing effect ... if you press a or any of the wasd keys then it changes to a D key
        /// ... and if you press the arrow keys then it goes back".
        /// </summary>
        [UnityTest]
        public IEnumerator TheKeyShowsInTheMiddleAfterTwoSecondsAndFollowsTheKeysPressed()
        {
            yield return Begin();
            var key = (RectTransform)_belt.PromptKey.transform;
            Assert.AreEqual(new Vector2(0.5f, 0.5f), key.anchorMin, "the key is not in the middle of the screen");
            Assert.AreEqual(Vector2.zero, key.anchoredPosition, "the key is not in the middle of the screen");
            Assert.AreEqual(Color.white, _belt.Prompt.color, "the key is not white");
            Assert.AreEqual(BeltIntro.PromptArrow, _belt.Prompt.text);
            yield return Frames(30);
            Assert.AreEqual(0.0f, _belt.PromptKey.alpha, 1e-3f, "the key shows before two seconds are up");

            yield return Frames(Mathf.CeilToInt((BeltIntro.PromptDelaySeconds + 0.2f) * 60.0f));
            float low = 1.0f, high = 0.0f;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                low = Mathf.Min(low, _belt.PromptKey.alpha);
                high = Mathf.Max(high, _belt.PromptKey.alpha);
            }
            Assert.Greater(high, 0.9f, "the key never shows in full");
            Assert.Less(low, 0.6f, "the key does not flash");

            // Owner, 2026-11-10: "make it fade away when you hold it".
            Press(_keyboard.dKey);
            yield return Frames(30);
            Assert.Less(_belt.PromptKey.alpha, 0.02f, "the key stays up while D is held");
            Release(_keyboard.dKey);
            high = 0.0f;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                high = Mathf.Max(high, _belt.PromptKey.alpha);
            }
            Assert.Greater(high, 0.9f, "the key does not come back once D is let go");

            Press(_keyboard.wKey);
            yield return null;
            Release(_keyboard.wKey);
            yield return null;
            Assert.AreEqual(BeltIntro.PromptLetter, _belt.Prompt.text, "a WASD key did not turn the key into D");
            Press(_keyboard.upArrowKey);
            yield return null;
            Release(_keyboard.upArrowKey);
            yield return null;
            Assert.AreEqual(BeltIntro.PromptArrow, _belt.Prompt.text, "an arrow key did not turn the key back into the arrow");
        }

        [UnityTest]
        public IEnumerator TheBeltStandsEmptyUntilAKeyIsHeldAndThenRollsTheBodyInToTheMiddle()
        {
            yield return Begin();
            Assert.IsNotNull(_belt.Ride, "making a character did not start on the belt");
            Assert.AreEqual(TitleScreen.Panel.Creator, _title.Showing);
            Assert.IsTrue(_belt.Prompt.gameObject.activeInHierarchy, "no prompt on the empty belt");
            Assert.AreEqual(BeltIntro.PromptArrow, _belt.Prompt.text);
            Assert.AreEqual(0.0f, _belt.Tools.alpha, "the tools are up before there is anybody to make");
            Assert.IsFalse(_belt.Tools.interactable);
            Assert.Less(BodyAcrossTheScreen(), -0.05f, "the body is on the screen before it has been rolled in");
            Assert.IsTrue(_belt.Life.Alive, "the body waits in its painting pose, not alive");
            Assert.IsTrue(_belt.Life.Animator.enabled, "the body is not idling");

            yield return Frames(30);
            Assert.AreEqual(0.0f, _belt.Belt.Travel, 1e-4f, "the belt runs with no key held");
            Assert.Less(BodyAcrossTheScreen(), -0.05f);

            // Owner, 2026-11-10: "make the player model placed a little bit farther to the left off screen so it takes about +2 seconds for him
            // to appear onto the screen where holding D" (it took under a second).
            Press(_keyboard.dKey);
            float held = 0.0f;
            while (BodyAcrossTheScreen() < 0.0f && held < 8.0f)
            {
                held += Time.deltaTime;
                yield return null;
            }
            Assert.Greater(_belt.Belt.Travel, 0.5f, "D does not run the belt");
            Assert.That(held, Is.InRange(2.5f, 3.5f), "the body does not ride into sight about two seconds later than it did");
            yield return Until(() => _belt.Ride.Now == BeltRide.Stage.Making, 10.0f, "holding D never set the body down in the middle");
            Release(_keyboard.dKey);
            Assert.AreEqual(0.5f, BodyAcrossTheScreen(), 0.01f, "the body was not set down in the middle of the screen");

            yield return null;
            Assert.IsFalse(_belt.Prompt.gameObject.activeInHierarchy, "the prompt stays up once the body has arrived");
            Assert.That(_belt.Tools.alpha, Is.InRange(0.0f, 0.5f), "the tools snapped on instead of fading in");
            yield return Until(() => _belt.Tools.alpha >= 1.0f, 2.0f, "the tools never finished fading in");
            Assert.IsTrue(_belt.Tools.interactable, "the tools are up but cannot be used");
            Assert.IsTrue(_belt.Creator.enabled, "the body cannot be turned or painted once it has arrived");
            float travelled = _belt.Belt.Travel;
            Press(_keyboard.dKey);
            yield return Frames(20);
            Release(_keyboard.dKey);
            Assert.AreEqual(travelled, _belt.Belt.Travel, 1e-4f, "D still runs the belt while the character is being made");
        }

        [UnityTest]
        public IEnumerator LeftRunsTheBeltTheOtherWay()
        {
            yield return Begin();
            Press(_keyboard.leftArrowKey);
            yield return Frames(40);
            Release(_keyboard.leftArrowKey);
            Assert.Less(_belt.Belt.Travel, -0.5f, "the left arrow does not run the belt left");
            Assert.Less(BodyAcrossTheScreen(), -0.05f, "the body came in while the belt ran away from the screen");
            Press(_keyboard.aKey);
            float before = _belt.Belt.Travel;
            yield return Frames(20);
            Release(_keyboard.aKey);
            Assert.Less(_belt.Belt.Travel, before, "A does not run the belt left");
        }

        /// <summary>Owner, 2026-11-10: "i dont like the auto tpose when you try to draw. make a t pose icon in the top right and make it a
        /// button that toggles it", and "make it so you can draw on your character when its not tposed".</summary>
        [UnityTest]
        public IEnumerator AliveItIsPaintedWhereItStandsAndOnlyTheButtonLiftsItsArms()
        {
            yield return Begin();
            yield return RollIn();
            yield return Until(() => _belt.Tools.alpha >= 1.0f, 2.0f, "the tools never came up");
            yield return Frames(30);
            CharacterCreator creator = _belt.Creator;
            Animator animator = _belt.Life.Animator;
            Assert.IsFalse(creator.HoldingStill, "the body is not alive while it is being made");
            var corner = (RectTransform)_belt.PoseButton.transform;
            Assert.AreEqual(Vector2.one, corner.anchorMin, "the T-pose button is not in the top right");
            Assert.AreEqual(CreatorKit.Paper, _belt.PoseIcon[0].color, "the button is lit with the arms down");

            // The arm, hanging at its side here, is nowhere near where it is in the T-pose the brush knows.
            Vector2 elbow = _belt.MakingCamera.WorldToScreenPoint(animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position);
            Vector2 chest = _belt.MakingCamera.WorldToScreenPoint(animator.GetBoneTransform(HumanBodyBones.Spine).position);

            creator.ShowBrushRing(elbow);
            Assert.IsTrue(creator.BrushRingVisible, "no brush ring over the living body");
            Vector3 ring = GameObject.Find("BrushRing").GetComponent<LineRenderer>().GetPosition(0);
            Assert.Less(Vector3.Distance(ring, animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position), 0.35f, "the ring is not on the arm where it is now");

            creator.Colour = Color.magenta;
            Assert.AreEqual(CreatorLife.IdleSpeed, animator.speed, 1e-4f, "the idle does not play at a tenth of its speed");
            creator.BrushRadiusPixels = CharacterCreator.MaximumBrushSize;
            creator.PressAt(elbow);
            Assert.IsTrue(creator.HasEdits, "a press on the living body painted nothing");
            Assert.IsTrue(_belt.Life.Alive, "painting lifted its arms");
            Assert.IsTrue(animator.enabled, "painting stopped it idling");
            yield return Frames(2);
            Assert.AreEqual(0.0f, animator.speed, "the idle goes on while a stroke is drawn on it");
            Press(Mouse.current.leftButton);
            yield return null;
            Release(Mouse.current.leftButton);
            yield return Frames(2);
            Assert.AreEqual(CreatorLife.IdleSpeed, animator.speed, 1e-4f, "the idle stays frozen once the button is let go");

            Color Sampled(Vector2 at)
            {
                creator.Colour = Color.black;
                creator.Current = CharacterCreator.Tool.Eyedropper;
                creator.PressAt(at);
                return creator.Colour;
            }

            Assert.AreEqual(Color.magenta, Sampled(elbow), "the stroke is not on the arm where it stands");
            Assert.AreNotEqual(Color.magenta, Sampled(chest), "the stroke ran onto the chest");

            _belt.PoseButton.onClick.Invoke();
            Assert.IsFalse(_belt.Life.Alive, "the button did not lift the arms");
            Assert.IsFalse(creator.HoldingStill, "the arms snapped up instead of lifting");
            yield return Until(() => creator.HoldingStill, CreatorLife.LiftSeconds + 0.5f, "the arms never finished lifting");
            Assert.AreEqual(CreatorKit.Gold, _belt.PoseIcon[0].color, "the button is not lit with the arms up");
            Vector2 elbowOut = _belt.MakingCamera.WorldToScreenPoint(animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position);
            Assert.AreEqual(Color.magenta, Sampled(elbowOut), "what was painted on the hanging arm is not on the arm held out");

            _belt.PoseButton.onClick.Invoke();
            Assert.IsTrue(_belt.Life.Alive, "the button does not let the arms back down");
            Assert.IsFalse(creator.HoldingStill);
            yield return Frames(4);
            Assert.IsTrue(animator.enabled);
            Assert.AreEqual(CreatorKit.Paper, _belt.PoseIcon[0].color);
        }

        [UnityTest]
        public IEnumerator FinishedItIsCarriedOffDroppedOffTheEndAndTheWorldIsEnteredInTheBlack()
        {
            yield return Begin();
            yield return RollIn();
            Ragdoll ragdoll = _belt.Ragdoll;
            Assert.IsNotNull(ragdoll, "the body on the belt has no ragdoll to fall with");
            Assert.IsFalse(ragdoll.IsLimp);

            bool entered = false;
            float blackWhenEntered = 0.0f;
            _belt.RollOut(() =>
            {
                entered = true;
                blackWhenEntered = ScreenBlack.Level;
            });
            Assert.AreEqual(BeltRide.Stage.RollingOut, _belt.Ride.Now, "finishing the character does not start the belt");
            yield return Frames(60);
            Assert.Less(_belt.Tools.alpha, 1.0f, "the tools stay up as the body is carried off");
            Assert.Greater(BodyAcrossTheScreen(), 0.55f, "the belt is not carrying the body right");
            Assert.IsFalse(_belt.WideCamera.enabled);

            yield return Until(() => _belt.Ride.Now == BeltRide.Stage.OnTheEdge, 6.0f, "the body never reached the edge of the screen");
            yield return null;
            Assert.IsTrue(_belt.WideCamera.enabled, "the view did not change at the edge of the screen");
            Assert.IsFalse(_belt.MakingCamera.enabled);
            Vector3 seen = _belt.WideCamera.WorldToViewportPoint(_belt.Rider.position);
            Assert.That(seen.x, Is.InRange(0.05f, 0.5f), "the body is not in the wider view, left of the belt's end");
            Assert.IsFalse(ragdoll.IsLimp, "it fell before the belt's end");

            yield return Until(() => _belt.Ride.Now == BeltRide.Stage.Fallen, 6.0f, "the body never reached the belt's end");
            yield return null;
            Assert.IsTrue(ragdoll.IsLimp, "at the belt's end the body did not go limp");
            float top = _belt.Rider.parent.position.y;
            yield return Until(() => entered, 8.0f, "the world was never entered after the fall");
            Assert.Greater(blackWhenEntered, 0.95f, "the world is entered before the screen is black");
            Assert.Less(ragdoll.Torso.position.y, top - 2.0f, "the body did not fall off the belt");
        }

        /// <summary>The furthest either foot strays over <paramref name="frames"/>, metres, in the body's own space: on the belt, in the turntable's turn.</summary>
        private IEnumerator MeasureFeet(int frames, Action<float, float, float> done)
        {
            Animator animator = _belt.Life.Animator;
            Transform root = animator.transform;
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 leftFirst = Vector3.zero, rightFirst = Vector3.zero;
            float stray = 0.0f, highest = float.MinValue, tilt = 0.0f;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                Vector3 leftNow = root.InverseTransformPoint(left.position);
                Vector3 rightNow = root.InverseTransformPoint(right.position);
                if (i == 0)
                {
                    leftFirst = leftNow;
                    rightFirst = rightNow;
                }
                stray = Mathf.Max(stray, Vector3.Distance(leftNow, leftFirst), Vector3.Distance(rightNow, rightFirst));
                highest = Mathf.Max(highest, leftNow.y, rightNow.y);
                tilt = Mathf.Max(tilt, Vector3.Angle(left.up, root.up), Vector3.Angle(right.up, root.up));
            }
            done(stray, highest, tilt);
        }

        /// <summary>Owner, 2026-11-10, of the body idling on the belt: "make the bottom of his feet actually stuck to the conveyer belt with IK
        /// ... it appears as if his feet are gliding back and forth on the ground instead of his feet being to the floor".</summary>
        [UnityTest]
        public IEnumerator AliveItsFeetAreStuckToTheBeltAndLetGoAtItsEnd()
        {
            yield return Begin();
            yield return RollIn();

            float bare = 0.0f;
            _belt.Life.enabled = false;
            _belt.Life.Animator.speed = 1.0f;   // the idle at its own speed, to move the feet as much as it can (in the creator it plays slowly)
            yield return MeasureFeet(300, (stray, height, tilt) => bare = stray);
            _belt.Life.enabled = true;
            yield return null;
            Assert.Greater(bare, 0.01f, "the idle no longer moves the feet, so this test shows nothing");

            float soleUnderAnkle = BodyRig.FootY * _belt.Life.Animator.GetBoneTransform(HumanBodyBones.LeftFoot).lossyScale.y;
            yield return MeasureFeet(300, (stray, height, tilt) =>
            {
                Assert.Less(stray, 0.002f, "the feet still glide as it idles");
                Assert.AreEqual(_belt.Belt.transform.position.y, _belt.Life.Animator.transform.position.y, 1e-3f, "the body's floor is not the belt's top");
                Assert.AreEqual(soleUnderAnkle, height * _belt.Life.Animator.transform.lossyScale.y, 0.005f, "the soles are not on the belt");
                Assert.Less(tilt, 0.5f, "the soles are not flat on the belt");
            });

            bool entered = false;
            _belt.RollOut(() => entered = true);
            yield return MeasureFeet(90, (stray, height, tilt) => Assert.Less(stray, 0.002f, "the feet glide as the belt carries it off"));
            Assert.IsFalse(_belt.Ragdoll.IsLimp);

            yield return Until(() => _belt.Ride.Now == BeltRide.Stage.Fallen, 12.0f, "the body never reached the belt's end");
            yield return Frames(30);
            Assert.IsTrue(_belt.Ragdoll.IsLimp);
            Animator animator = _belt.Life.Animator;
            Assert.Greater(Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position, animator.transform.TransformPoint(Vector3.up * BodyRig.FootY)), 0.1f,
                "the feet are still held to the belt in the fall");
            yield return Until(() => entered, 8.0f, "the world was never entered after the fall");
        }

        /// <summary>Whether a point in the belt's own frame is inside the top run or the drum.</summary>
        private static bool InsideTheBelt(Vector3 at)
        {
            return at.x < ConveyorBelt.Length
                ? at.y < 0.0f && at.y > -2.0f * ConveyorBelt.DrumRadius
                : new Vector2(at.x - ConveyorBelt.Length, at.y + ConveyorBelt.DrumRadius).magnitude < ConveyorBelt.DrumRadius;
        }

        /// <summary>
        /// The roller at the belt's end is round where it is drawn: what the body stands on and is carried over is the circle the slats go
        /// round (owner, 2026-11-10: "are we sure the hitbox of the roller is even round?").
        /// </summary>
        [UnityTest]
        public IEnumerator TheRollerIsRoundAndWhereTheSlatsGoRoundIt()
        {
            yield return Begin();
            Transform belt = _belt.Belt.transform;
            Physics.SyncTransforms();
            const int Steps = 12;
            for (int i = 0; i <= Steps; i++)
            {
                // Down onto the top half of the roller, from above, at even steps out from its middle.
                float past = ConveyorBelt.DrumRadius * 0.98f * i / Steps;
                Vector3 from = belt.TransformPoint(new Vector3(ConveyorBelt.Length + past, 1.0f, 0.0f));
                float nearest = float.MaxValue;
                foreach (RaycastHit hit in Physics.RaycastAll(from, -belt.up, 3.0f))
                {
                    if (hit.collider.transform.IsChildOf(belt))
                    {
                        nearest = Mathf.Min(nearest, hit.distance);
                    }
                }
                Assert.Less(nearest, float.MaxValue, $"nothing solid {past:F2} m past the belt's end");
                float surface = 1.0f - nearest;
                float round = -ConveyorBelt.DrumRadius + Mathf.Sqrt(ConveyorBelt.DrumRadius * ConveyorBelt.DrumRadius - past * past);
                Assert.AreEqual(round, surface, 0.005f, $"the roller is not round {past:F2} m past the belt's end");
            }
            for (int i = 0; i <= Steps; i++)
            {
                // The slats as drawn, half the way round, lie on that circle.
                ConveyorBelt.Surface(ConveyorBelt.Length + Mathf.PI * ConveyorBelt.DrumRadius * i / Steps, out Vector3 drawn, out _);
                float fromTheAxis = new Vector2(drawn.x - ConveyorBelt.Length, drawn.y + ConveyorBelt.DrumRadius).magnitude;
                Assert.AreEqual(ConveyorBelt.DrumRadius, fromTheAxis, 0.01f, "the slats are not drawn round the roller the body is carried over");
            }
        }

        /// <summary>
        /// Owner, 2026-11-10: "make it look natural like a person getting rolled off", with a drawing of it pitched out over the drop, its
        /// back foot on the roller; "feel like he needs a little tilt it still feels like his left leg hovers in air and visible goes limp,
        /// looks very game engine like not like a cool cutscene".
        /// </summary>
        [UnityTest]
        public IEnumerator ItLeansOverTheRollerOnItsFrontFootThenGoesLimpWithoutAJumpAndPitchesOverItsBackFoot()
        {
            yield return Begin();
            yield return RollIn();
            _belt.RollOut(() => { });
            Animator animator = _belt.Life.Animator;
            Transform belt = _belt.Belt.transform;
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            Transform[] feet = { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
            float r = ConveyorBelt.DrumRadius, soleUnder = BodyRig.FootY * feet[0].lossyScale.y;

            // Over the roller, still alive: the front ankle rides the roller, the back one the top, and it leans.
            float hovered = 0.0f, mostLean = 0.0f;
            var heads = new List<(Vector3 at, bool limp)>();
            var sampler = _belt.gameObject.AddComponent<AfterEveryLateUpdate>();
            sampler.Sample = () =>
            {
                heads.Add((head.position, _belt.Ragdoll.IsLimp));
                if (_belt.Ride.Now != BeltRide.Stage.OnTheEdge)
                {
                    return;
                }
                Vector3 a = belt.InverseTransformPoint(feet[0].position), b = belt.InverseTransformPoint(feet[1].position);
                Vector3 front = a.x > b.x ? a : b;
                float past = front.x - ConveyorBelt.Length;
                if (past > 0.02f && past < r)
                {
                    float fromAxis = new Vector2(past, front.y + r).magnitude;
                    hovered = Mathf.Max(hovered, Mathf.Abs(fromAxis - r - soleUnder));
                }
                mostLean = Mathf.Max(mostLean, Vector3.Angle(belt.up, neck.position - hips.position));
            };
            yield return Until(() => _belt.Ride.Now == BeltRide.Stage.Fallen, 15.0f, "the body was never let go");
            yield return Frames(3);
            UnityEngine.Object.Destroy(sampler);
            Assert.Less(hovered, 0.04f, "the front foot left the roller before the body was let go (owner: \"his left leg hovers in air\")");
            Assert.That(mostLean, Is.InRange(BeltIntro.HandOverAtDegrees - 6.0f, BeltIntro.HandOverAtDegrees + 10.0f), "it did not lean a little over the roller");

            Ragdoll ragdoll = _belt.Ragdoll;
            Assert.IsTrue(ragdoll.IsLimp, "it is not the ragdoll's");
            Assert.IsTrue(ragdoll.IsPinned, "its back foot is not held");
            // No jump as physics takes it: the head moves on, frame to frame, as it was moving.
            int first = heads.FindIndex(h => h.limp);
            // Leaning faster and faster as the foot goes round the roller, so measured against where its last steps were heading.
            Assert.Greater(first, 2, "never seen alive before it went limp");
            Vector3 step1 = heads[first - 2].at - heads[first - 3].at, step2 = heads[first - 1].at - heads[first - 2].at;
            Vector3 heading = step2 + (step2 - step1), stepAfter = heads[first].at - heads[first - 1].at;
            Assert.Less((stepAfter - heading).magnitude, 0.03f, "the body jumped as it went limp");

            Transform back = belt.InverseTransformPoint(feet[0].position).x < belt.InverseTransformPoint(feet[1].position).x ? feet[0] : feet[1];
            float tiltWhenLetGo = -1.0f, backFootPast = float.MaxValue;
            int inTheBelt = 0;
            float waited = 0.0f;
            HumanBodyBones[] legs = { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg };
            while (waited < 6.0f && belt.InverseTransformPoint(hips.position).y > -3.0f)
            {
                waited += Time.deltaTime;
                yield return null;
                if (tiltWhenLetGo < 0.0f && !ragdoll.IsPinned)
                {
                    tiltWhenLetGo = Vector3.Angle(belt.up, neck.position - hips.position);
                    backFootPast = belt.InverseTransformPoint(back.position).x - ConveyorBelt.Length;
                    Assert.Greater(belt.InverseTransformPoint(neck.position).x, belt.InverseTransformPoint(hips.position).x + 0.3f, "it did not pitch out over the drop");
                }
                foreach (HumanBodyBones bone in legs)
                {
                    if (InsideTheBelt(belt.InverseTransformPoint(animator.GetBoneTransform(bone).position)))
                    {
                        inTheBelt++;
                    }
                }
            }
            Assert.Greater(tiltWhenLetGo, 0.0f, "its back foot was never let go");
            Assert.That(tiltWhenLetGo, Is.InRange(BeltIntro.LetGoAtDegrees, BeltIntro.LetGoAtDegrees + 25.0f), "its back foot was not held until it had pitched 45 degrees over");
            Assert.AreEqual(0.0f, backFootPast, 0.15f, "its back foot was not on the top of the roller as it pitched over");
            Assert.Less(belt.InverseTransformPoint(hips.position).y, -3.0f, "it never fell off the belt");
            Assert.AreEqual(0, inTheBelt, "a leg went through the belt");
        }

        [DefaultExecutionOrder(10000)]
        private sealed class AfterEveryLateUpdate : MonoBehaviour
        {
            public Action Sample;

            private void LateUpdate()
            {
                Sample?.Invoke();
            }
        }

        private static float ElbowBend(Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand)
        {
            Vector3 elbow = animator.GetBoneTransform(lower).position;
            return Vector3.Angle(elbow - animator.GetBoneTransform(upper).position, animator.GetBoneTransform(hand).position - elbow);
        }

        /// <summary>Owner, 2026-11-10: "during the roll off, the player puts his hands on his waist right as the conveyer belt rolls them off,
        /// can you make them just maintain their idle pose until the ragdoll dropoff".</summary>
        [UnityTest]
        public IEnumerator GoingLimpAtTheBeltsEndTheArmsDoNotSnapOutOfTheIdle()
        {
            yield return Begin();
            yield return RollIn();
            _belt.RollOut(() => { });
            Animator animator = _belt.Life.Animator;
            float left = 0.0f, right = 0.0f;
            while (_belt.Ride.Now != BeltRide.Stage.Fallen)
            {
                left = ElbowBend(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
                right = ElbowBend(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
                yield return null;
            }
            float worst = 0.0f;
            for (int i = 0; i < 8; i++)
            {
                yield return null;
                worst = Mathf.Max(worst,
                    Mathf.Abs(ElbowBend(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand) - left),
                    Mathf.Abs(ElbowBend(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) - right));
            }
            TestContext.WriteLine($"elbows moved {worst:0.0} degrees in the first 8 frames limp");
            // Wrenched to the limits measured from the T-pose they moved 46 degrees; pitching over the roller, limp arms swing some 20 of their own.
            Assert.Less(worst, 30.0f, "the arms snapped into another shape as the body went limp");
        }

        [UnityTest]
        public IEnumerator HoldingStillItsFeetAreExactlyWhereTheyArePainted()
        {
            yield return Begin();
            yield return RollIn();
            yield return Frames(30);
            Animator animator = _belt.Life.Animator;
            _belt.TogglePose();
            yield return Until(() => _belt.Life.Still, 3.0f, "the arms never finished coming up");
            Vector3 left = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            yield return Frames(10);
            Assert.AreEqual(0.0f, Vector3.Distance(left, animator.GetBoneTransform(HumanBodyBones.LeftFoot).position), 1e-5f, "the planted feet moved a body being painted");
        }

        /// <summary>Owner, 2026-11-10: "make this noise play when going right or left on the conveyer belt, and at the end when the
        /// conveyer belt rolls your character off".</summary>
        [UnityTest]
        public IEnumerator TheBeltIsHeardWhileItRunsEitherWayAndAsItRollsTheBodyOff()
        {
            yield return Begin();
            AudioSource hum = _belt.Hum;
            Assert.IsNotNull(hum, "the belt has no sound");
            Assert.IsNotNull(hum.clip, "the belt's sound has no recording");
            Assert.IsTrue(hum.loop, "the belt's sound ends while the belt still runs");
            yield return Frames(20);
            Assert.IsFalse(hum.isPlaying, "the belt is heard standing still");

            Press(_keyboard.dKey);
            yield return Frames(40);
            Assert.IsTrue(hum.isPlaying, "run right, the belt makes no sound");
            Assert.AreEqual(BeltIntro.HumVolume, hum.volume, 0.01f, "at its full run the belt is not at its full sound");
            Release(_keyboard.dKey);
            yield return Frames(30);
            Assert.IsFalse(hum.isPlaying, "the belt is heard on after it has stopped");

            Press(_keyboard.aKey);
            yield return Frames(10);
            Assert.IsTrue(hum.isPlaying, "run left, the belt makes no sound");
            Release(_keyboard.aKey);
            yield return null;

            yield return RollIn();
            yield return Frames(5);
            Assert.IsFalse(hum.isPlaying, "the belt is heard while the character is made");

            bool entered = false;
            float heardWhenEntered = 1.0f;
            _belt.RollOut(() =>
            {
                entered = true;
                heardWhenEntered = hum.volume;
            });
            yield return Frames(40);
            Assert.IsTrue(hum.isPlaying, "the belt carries the body off without a sound");
            Assert.AreEqual(BeltIntro.HumVolume, hum.volume, 0.01f, "close to, the belt is not at its full sound");
            yield return Until(() => _belt.Ride.Now == BeltRide.Stage.Fallen, 12.0f, "the body never reached the belt's end");
            yield return Frames(5);
            Assert.IsTrue(hum.isPlaying, "the belt goes quiet the moment the body falls off it");
            Assert.AreEqual(BeltIntro.HumVolume * BeltIntro.WideHumShare, hum.volume, 0.01f, "from the wider view the belt does not sound further away");
            yield return Until(() => entered, 8.0f, "the world was never entered after the fall");
            Assert.Less(heardWhenEntered, 0.05f, "the belt is still loud when the screen is black");
        }

        [UnityTest]
        public IEnumerator ANewCharactersWorldWaitsForTheRideAndAMadeOnesDoesNot()
        {
            yield return Begin();
            Assert.IsNotNull(_title.Select.Departure, "nothing is set to carry a new character off before its world loads");
            Action load = null;
            _title.Select.Departure = enter => load = enter;
            CharacterSave made = CharacterSave.Create("New", CharacterSave.DefaultSkin);
            ((Server)CharacterSources.Current).Characters.Add(made);
            _title.Select.Play(made, isNew: true);
            Assert.IsNotNull(load, "a new character's world loaded at once, with no ride");
            Assert.AreEqual(CharacterCreatorPlayTests.ScenePath, _scene.path);
            Assert.IsTrue(_scene.isLoaded, "the title was left before the ride");

            _title.Show(TitleScreen.Panel.Menu);
            yield return null;
            Assert.IsNull(_title.Select.Departure, "leaving the creator leaves the ride set for whoever is played next");
        }

        [UnityTest]
        public IEnumerator ShownWithoutTheBeltTheCreatorStandsReadyAsItAlwaysDid()
        {
            yield return Begin();
            Press(_keyboard.dKey);
            yield return Frames(20);
            Release(_keyboard.dKey);
            _title.Show(TitleScreen.Panel.Creator);
            yield return null;
            Assert.IsNull(_belt.Ride, "the ride goes on under a creator shown ready");
            Assert.AreEqual(1.0f, _belt.Tools.alpha);
            Assert.IsTrue(_belt.Tools.interactable);
            Assert.IsTrue(_belt.Creator.HoldingStill, "a creator shown ready is not in its painting pose");
            Assert.IsFalse(_belt.Prompt.gameObject.activeInHierarchy);
            Assert.AreEqual(0.5f, BodyAcrossTheScreen(), 0.01f, "the body is not in the middle");
            Assert.IsTrue(_belt.MakingCamera.enabled);
        }

        [UnityTest]
        public IEnumerator AStartWithNobodyMadeOpensOnTheEmptyBeltAndOneWithSomebodyOnTheTitle()
        {
            yield return Open(0, beltOnFirstStart: true);
            yield return null;
            Assert.AreEqual(TitleScreen.Panel.Creator, _title.Showing, "a first start does not open on the belt");
            Assert.IsNotNull(_belt.Ride, "a first start shows the creator without the ride");
            Assert.AreEqual(BeltRide.Stage.RollingIn, _belt.Ride.Now);
            Assert.IsFalse(_title.Vignette.activeSelf, "the title's dark corners are drawn over the belt's daylight sky");
            yield return SceneManager.UnloadSceneAsync(_scene);

            yield return Open(2, beltOnFirstStart: true);
            yield return null;
            Assert.IsFalse(_title.CreatorPanel.activeSelf, "a start with characters made opens on the belt");
            Assert.IsTrue(_title.PromptLabel.gameObject.activeSelf, "the title's own first screen is gone");
        }
    }
}
