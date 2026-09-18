using NUnit.Framework;
using UnityEngine;
using CityX.Character;
using CityX.UI;
using CityX.View;
using CityX.World;

namespace CityX.Tests
{
    /// <summary>
    /// IsToonableMaterial's own rule for /celshade -- see that method's own
    /// doc comment for why shader identity replaced a name allowlist, and
    /// why Transparent is excluded specifically to keep glass see-through.
    /// Also the console's own Open/Close handling of the player's Animator
    /// -- EXPLICIT ASK, after seven rounds of the console/grapple
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

        private static Material LitMaterial(float surface)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_Surface", surface);
            return material;
        }

        [Test]
        public void AnOpaqueLitMaterialIsToonable()
        {
            Material material = LitMaterial(0.0f);
            Assert.IsTrue(DevConsoleView.IsToonableMaterial(material));
            Object.DestroyImmediate(material);
        }

        /// <summary>
        /// Glass is built on this exact same shader with Surface Type set
        /// to Transparent rather than a different shader asset -- ToonLit
        /// has no Blend state at all, so toon-shading a window would not
        /// restyle it, it would make it suddenly solid.
        /// </summary>
        [Test]
        public void ATransparentLitMaterialIsNotToonable()
        {
            Material material = LitMaterial(1.0f);
            Assert.IsFalse(DevConsoleView.IsToonableMaterial(material));
            Object.DestroyImmediate(material);
        }

        [Test]
        public void ANonLitShaderIsNeverToonable()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            Assert.IsFalse(DevConsoleView.IsToonableMaterial(material));
            Object.DestroyImmediate(material);
        }

        /// <summary>
        /// EXPLICIT ASK, after repeated reports of "there are no sloped
        /// roads" that turned out to mean "I never actually walked to one":
        /// /goto slope has to land on a chunk that is a REAL street-vs-street
        /// terrace drop rolled sloped (mirrors AddStreetEdgeFeatures's own
        /// roll in CityMeshBuilder.cs exactly), not just anywhere on a
        /// street.
        /// </summary>
        [Test]
        public void GotoSlopeLandsOnARealSlopedStreetEdge()
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
            int chunkX = Mathf.FloorToInt(at.x / CityLayout.ChunkSize);
            int chunkY = Mathf.FloorToInt(at.z / CityLayout.ChunkSize);
            Assert.IsTrue(CityLayout.IsStreetChunk(chunkX, chunkY),
                $"/goto slope landed at ({at.x:0.0},{at.z:0.0}), chunk ({chunkX},{chunkY}), which is not a street");

            bool landedOnASlope = false;
            var world = new CityWorld(streamer.WorldSeed);
            float pad = world.Terrain.PadHeightFor(chunkX, chunkY);
            foreach ((int nx, int ny) in new[] { (chunkX - 1, chunkY), (chunkX + 1, chunkY), (chunkX, chunkY - 1), (chunkX, chunkY + 1) })
            {
                if (!CityLayout.IsStreetChunk(nx, ny))
                {
                    continue;
                }
                if (world.Terrain.PadHeightFor(nx, ny) >= pad)
                {
                    continue;
                }
                if (new DeterministicRandom(world.ChunkSeed(nx, ny)).Below(2) == 0)
                {
                    landedOnASlope = true;
                }
            }
            Assert.IsTrue(landedOnASlope,
                $"chunk ({chunkX},{chunkY}) has no sloped-street edge -- /goto slope did not find a real one");
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
    }
}
