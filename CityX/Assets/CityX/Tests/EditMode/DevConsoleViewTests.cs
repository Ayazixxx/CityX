using NUnit.Framework;
using UnityEngine;
using CityX.Character;
using CityX.UI;
using CityX.View;
using CityX.World;

namespace CityX.Tests
{
    /// <summary>
    /// /celshade and /outlines drive the cel look (CelShading). Also the console's own Open/Close handling of the player's Animator
    /// -- Requested, after seven rounds of the console/grapple
    /// distortion saga: "remove the console function and rebuild it
    /// entirely if you have to, just get rid of it." Open() used to
    /// disable the Animator to stop whatever animation was playing from
    /// looping while the console stayed open -- a cosmetic nicety that
    /// carried a real, structural risk (freezing on a rare implausible
    /// Mecanim transition frame, made permanent for as long as the console
    /// stayed open). DevConsoleView.Open's own doc comment has the full
    /// story; these tests now assert the animator is left alone entirely.
    /// </summary>
    public class DevConsoleViewTests
    {
        private GameObject _consoleObject;
        private GameObject _bodyObject;
        private GameObject _streamerObject;
        private float _timeScaleBefore;

        [SetUp]
        public void SetUp()
        {
            _timeScaleBefore = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            if (_consoleObject != null) Object.DestroyImmediate(_consoleObject);
            if (_bodyObject != null) Object.DestroyImmediate(_bodyObject);
            if (_streamerObject != null) Object.DestroyImmediate(_streamerObject);
            // A failed assertion mid-test can leave Open() having paused
            // real time -- restored here so one failure cannot leave every
            // later test (in this file or any other) silently running at
            // zero speed.
            Time.timeScale = _timeScaleBefore;
        }

        private DevConsoleView MakeConsoleWithAnimator(out Animator animator, out PlayerVitals vitals)
        {
            _bodyObject = new GameObject("Body");
            vitals = _bodyObject.AddComponent<PlayerVitals>();
            vitals.Revive();
            animator = _bodyObject.AddComponent<Animator>();

            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Vitals = vitals;
            view.Bind();
            return view;
        }

        [Test]
        public void OpeningTheConsoleLeavesThePlayerAnimatorRunning()
        {
            DevConsoleView view = MakeConsoleWithAnimator(out Animator animator, out _);
            Assert.IsTrue(animator.enabled, "setup failed -- the animator should start enabled");

            view.Open();

            Assert.IsTrue(animator.enabled,
                "opening the console must never disable the animator -- see Open's own doc comment for why " +
                "that freeze was removed entirely rather than made safer");
        }

        [Test]
        public void ClosingTheConsoleLeavesThePlayerAnimatorRunning()
        {
            DevConsoleView view = MakeConsoleWithAnimator(out Animator animator, out _);
            view.Open();

            view.Close();

            Assert.IsTrue(animator.enabled, "closing the console should never touch the animator at all");
        }

        /// <summary>
        /// Ragdoll.Activate disabled this same Animator for a real reason
        /// (a corpse's own bones need to be free for physics) -- closing
        /// the console while the player happens to be dead must not
        /// revive it. Open()/Close() no longer touch _playerAnimator at
        /// all, so this is now trivially true by construction -- kept as a
        /// regression guard against a future version reintroducing the
        /// touch.
        /// </summary>
        [Test]
        public void ClosingTheConsoleDoesNotReEnableTheAnimatorIfThePlayerDied()
        {
            DevConsoleView view = MakeConsoleWithAnimator(out Animator animator, out PlayerVitals vitals);
            view.Open();
            IDamageable target = vitals;
            target.ApplyHit(vitals.MaxHealth * 2.0f, Vector3.zero, 0.0f);
            Assert.IsTrue(vitals.IsDead, "setup failed -- the body should be dead");
            animator.enabled = false; // Ragdoll.Activate's own real job; not present on this bare fixture.

            view.Close();

            Assert.IsFalse(animator.enabled, "closing the console revived the animator on a dead body");
        }

        /// <summary>
        /// GrappleSwing was rebuilt from square one after eight rounds of
        /// live pose-distortion bugs traced to its old bone-owning pose
        /// (see GrappleSwing's own class doc comment) -- it no longer
        /// disables the Animator at all, ever, so opening/closing the
        /// console mid-swing has nothing left to fight over. Kept as a
        /// regression guard against a future version reintroducing either
        /// side of that touch.
        /// </summary>
        [Test]
        public void OpeningAndClosingTheConsoleMidSwingNeverTouchesTheAnimator()
        {
            DevConsoleView view = MakeConsoleWithAnimator(out Animator animator, out _);
            GrappleSwing swing = _bodyObject.AddComponent<GrappleSwing>();
            swing.Bind();
            view.Swing = swing;
            swing.Attach(_bodyObject.transform.position + Vector3.up * 5.0f);
            Assert.IsTrue(animator.enabled, "setup failed -- attaching the swing must never disable the animator any more");

            view.Open();
            Assert.IsTrue(animator.enabled, "opening the console mid-swing disabled the animator");

            view.Close();
            Assert.IsTrue(animator.enabled, "closing the console mid-swing disabled the animator");
        }

        /// <summary>
        /// No NetworkBootstrap wired at all -- the common case for every
        /// test in this file, and for a bare scene that never touched
        /// networking. Treated as Singleplayer (see Open's own doc
        /// comment), matching NetworkedPlayerSpawner's identical
        /// null-bootstrap fallback.
        /// </summary>
        [Test]
        public void OpeningTheConsoleWithNoNetworkingWiredPausesTime()
        {
            DevConsoleView view = MakeConsoleWithAnimator(out _, out _);
            Time.timeScale = 1.0f;

            view.Open();

            Assert.AreEqual(0.0f, Time.timeScale, 0.001f,
                "opening the console with no NetworkBootstrap in the scene should pause time, same as singleplayer");
        }

        [Test]
        public void ClosingTheConsoleRestoresTheTimeScaleFromBeforeItOpened()
        {
            DevConsoleView view = MakeConsoleWithAnimator(out _, out _);
            Time.timeScale = 1.0f;
            view.Open();

            view.Close();

            Assert.AreEqual(1.0f, Time.timeScale, 0.001f, "closing the console never restored the original time scale");
        }

        /// <summary>
        /// Not a hardcoded 1.0 -- if something else already had time frozen
        /// before the console opened (nothing does today, but PauseMenuView
        /// uses this exact mechanism), Close() must hand back exactly that,
        /// not unconditionally resume.
        /// </summary>
        [Test]
        public void ClosingTheConsoleRestoresAnAlreadyPausedTimeScaleRatherThanForcingOne()
        {
            DevConsoleView view = MakeConsoleWithAnimator(out _, out _);
            Time.timeScale = 0.0f; // e.g. the Escape pause menu was already open
            view.Open();

            view.Close();

            Assert.AreEqual(0.0f, Time.timeScale, 0.001f,
                "closing the console forced time back to 1.0 even though something else had already paused it");
        }

        /// <summary>/celshade turns the cel look off and on (with no word, the other way), and /outlines sets the lines' width.</summary>
        [Test]
        public void CelShadeAndOutlinesDriveTheCelLook()
        {
            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Bind();
            bool wasOn = CelShading.Enabled;
            try
            {
                view.Submit("/celshade off");
                Assert.IsFalse(CelShading.Enabled, "/celshade off left the cel look on");
                view.Submit("/celshade");
                Assert.IsTrue(CelShading.Enabled, "/celshade did not turn the cel look back on");
                view.Submit("/outlines 3.5");
                Assert.AreEqual(3.5f, CelShading.OutlineWidth, 1e-4f, "/outlines");
                Assert.AreEqual(3.5f, Shader.GetGlobalVector("_CityXOutline").x, 1e-4f, "the lines' width never reached the shader");
                view.Submit("/outlines -1");
                Assert.AreEqual(3.5f, CelShading.OutlineWidth, 1e-4f, "a width under 0 was taken");
            }
            finally
            {
                CelShading.SetOutlineWidth(CelShading.DefaultOutlineWidth);
                CelShading.Set(wasOn);
            }
        }

        /// <summary>
        /// /goto slope has to land on a real, climbing road of the plan city -- the middle of a road whose two ends differ in height
        /// by a noticeable grade -- not just anywhere.
        /// </summary>
        [Test]
        public void GotoSlopeLandsOnARealClimbingRoad()
        {
            _bodyObject = new GameObject("Body");
            PlayerVitals vitals = _bodyObject.AddComponent<PlayerVitals>();
            vitals.Revive();
            _bodyObject.AddComponent<CharacterController>();

            _streamerObject = new GameObject("Streamer");
            CityStreamer streamer = _streamerObject.AddComponent<CityStreamer>();
            streamer.WorldSeed = 20260810;
            streamer.RadiusChunks = 0;

            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Vitals = vitals;
            view.Streamer = streamer;
            view.Bind();

            view.Submit("/goto slope");

            Vector3 at = vitals.transform.position;
            var world = new CityWorld(streamer.WorldSeed);
            double bestGrade = -1.0;
            double bestDistance = double.MaxValue;
            foreach (CityX.World.Plan.EdgeGeometry g in world.Plan.Geometry.Edges)
            {
                if (g == null || g.Edge.Bridge) continue;
                double mx = (g.A.X + g.B.X) * 0.5 - at.x, mz = (g.A.Y + g.B.Y) * 0.5 - at.z;
                double d = mx * mx + mz * mz;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestGrade = System.Math.Abs(world.Plan.GroundHeight(g.A.X, g.A.Y) - world.Plan.GroundHeight(g.B.X, g.B.Y)) / g.Length;
                }
            }
            Assert.Less(bestDistance, 4.0, $"/goto slope landed at ({at.x:0.0},{at.z:0.0}), which is not the middle of a road");
            Assert.Greater(bestGrade, 0.01, "the road /goto slope found is flat");
        }

        /// <summary>/goto highway lands the player on an expressway's main deck, a couple of metres over its running surface.</summary>
        [Test]
        public void GotoHighwayLandsOnAnExpresswaysDeck()
        {
            _bodyObject = new GameObject("Body");
            PlayerVitals vitals = _bodyObject.AddComponent<PlayerVitals>();
            vitals.Revive();
            _bodyObject.AddComponent<CharacterController>();

            _streamerObject = new GameObject("Streamer");
            CityStreamer streamer = _streamerObject.AddComponent<CityStreamer>();
            streamer.WorldSeed = 20260810;
            streamer.RadiusChunks = 0;

            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Vitals = vitals;
            view.Streamer = streamer;
            view.Bind();

            view.Submit("/goto highway");

            Vector3 at = vitals.transform.position;
            var world = new CityWorld(streamer.WorldSeed);
            CityX.World.Plan.ExpresswayNetwork xw = world.Plan.Data.Expressways;
            double top = xw.DeckOver(new CityX.World.Plan.Vec2(at.x, at.z), 0.0, out int path);
            Assert.IsFalse(double.IsNaN(top), $"/goto highway landed at ({at.x:0.0},{at.z:0.0}), off every deck");
            Assert.AreEqual(CityX.World.Plan.ViaductKind.Mainline, xw.Paths[path].Kind, "not on a main line");
            Assert.AreEqual(top + 2.0, at.y, 0.5, "not just over the deck's surface");
        }

        [Test]
        public void GotoWithNoArgumentPrintsUsageRatherThanThrowing()
        {
            _bodyObject = new GameObject("Body");
            PlayerVitals vitals = _bodyObject.AddComponent<PlayerVitals>();
            vitals.Revive();
            _bodyObject.AddComponent<CharacterController>();

            _streamerObject = new GameObject("Streamer");
            CityStreamer streamer = _streamerObject.AddComponent<CityStreamer>();
            streamer.WorldSeed = 20260810;

            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Vitals = vitals;
            view.Streamer = streamer;
            view.Bind();

            Vector3 before = vitals.transform.position;
            Assert.DoesNotThrow(() => view.Submit("/goto"));
            Assert.AreEqual(before, vitals.transform.position, "an invalid /goto still moved the player");
        }

        /// <summary>/camera tunes the follow camera's framing live, and /camera reset puts every number back to its default.</summary>
        [Test]
        public void TheCameraCommandTunesTheFollowCameraAndResetPutsItBack()
        {
            _bodyObject = new GameObject("Camera");
            var follow = _bodyObject.AddComponent<ThirdPersonCamera>();
            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.OrbitCamera = follow;
            view.Bind();

            view.Submit("/camera height 2");
            view.Submit("/camera distance 6");
            view.Submit("/camera shoulder 0.8");
            view.Submit("/camera fov 60");
            Assert.AreEqual(2.0f, follow.FramingHeight, 1e-4f, "/camera height");
            Assert.AreEqual(6.0f, follow.Distance, 1e-4f, "/camera distance");
            Assert.AreEqual(0.8f, follow.ShoulderOffset, 1e-4f, "/camera shoulder");
            Assert.AreEqual(60.0f, follow.RestFieldOfView, 1e-4f, "/camera fov");

            view.Submit("/camera reset");
            Assert.AreEqual(ThirdPersonCamera.DefaultFramingHeight, follow.FramingHeight, 1e-4f);
            Assert.AreEqual(ThirdPersonCamera.DefaultDistance, follow.Distance, 1e-4f);
            Assert.AreEqual(ThirdPersonCamera.DefaultShoulderOffset, follow.ShoulderOffset, 1e-4f);
            Assert.AreEqual(ThirdPersonCamera.DefaultFieldOfView, follow.RestFieldOfView, 1e-4f);

            Assert.DoesNotThrow(() => view.Submit("/camera"));
            Assert.DoesNotThrow(() => view.Submit("/camera height"));
            Assert.DoesNotThrow(() => view.Submit("/camera nonsense 3"));
            Assert.AreEqual(ThirdPersonCamera.DefaultFramingHeight, follow.FramingHeight, 1e-4f, "a bad /camera changed the framing");
        }

        /// <summary>Owner, 2026-10-24: "/spawn ... if i add a number at the end of it after the item or mob i type out it will spawn that many".</summary>
        [Test]
        public void SpawnWithANumberPutsDownThatManyInARow()
        {
            _bodyObject = new GameObject("Body");
            var vitals = _bodyObject.AddComponent<PlayerVitals>();
            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Vitals = vitals;
            view.Bind();
            try
            {
                view.Submit("/spawn dummy 4");
                SparringDummy[] dummies = Object.FindObjectsByType<SparringDummy>(FindObjectsSortMode.None);
                Assert.AreEqual(4, dummies.Length, "/spawn dummy 4 did not put down four");
                for (int i = 0; i < dummies.Length; i++)
                {
                    for (int j = i + 1; j < dummies.Length; j++)
                    {
                        Assert.Greater(Vector3.Distance(dummies[i].transform.position, dummies[j].transform.position), 1.0f, "two spawned in one spot");
                    }
                }
                view.Submit("/spawn dummy");
                Assert.AreEqual(5, Object.FindObjectsByType<SparringDummy>(FindObjectsSortMode.None).Length, "no number did not spawn one");
                view.Submit("/spawn dummy lots");
                view.Submit("/spawn dummy 0");
                Assert.AreEqual(5, Object.FindObjectsByType<SparringDummy>(FindObjectsSortMode.None).Length, "a bad number spawned something");
                view.Submit("/spawn dummy 500");
                Assert.AreEqual(5 + DevConsoleView.MaxSpawnCount, Object.FindObjectsByType<SparringDummy>(FindObjectsSortMode.None).Length,
                    "the count was not capped");
            }
            finally
            {
                foreach (SparringDummy dummy in Object.FindObjectsByType<SparringDummy>(FindObjectsSortMode.None))
                {
                    Object.DestroyImmediate(dummy.gameObject);
                }
            }
        }

        [Test]
        public void ASentCommandIsKeptInTheConsolesOwnHistory()
        {
            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Bind();
            view.Submit("/help");
            Assert.AreEqual("/help", view.History.Older(""));
        }
    }
}
