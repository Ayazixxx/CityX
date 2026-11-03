using System.Collections;
using System.Linq;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A WINDOW BREAKING INTO ITS OWN PIECES (owner, 2026-10-15: "would procedurally window fracturing effect performance and be not worth it?
    /// find out ... either way improve upon the window fracturing system"). A struck pane becomes its own pieces where it stood; the slivers
    /// round the blow go through the way the blow went, the pieces on the frame hang on a moment, everything falls, lands and is gone after its
    /// time; the pieces are pooled and capped. And the answer to the owner's question, measured: what a break and the falling glass cost.
    /// </summary>
    public sealed class GlassFracturePlayTests
    {
        private GameObject _ground;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
#if UNITY_EDITOR
            _material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/GlassShards.mat");
#endif
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            GlassFractureBurst.Clear();
            if (_ground != null) Object.Destroy(_ground);
        }

        /// <summary>A 1.3 x 1.9 m pane 3 m up in a wall facing -z (its outside), its bottom-left corner at x -0.65.</summary>
        private static PaneFrame Pane() =>
            new PaneFrame(new Vector3(-0.65f, 3.0f, 5.0f), Vector3.right, Vector3.up, Vector3.back, 1.3f, 1.9f);

        private static GameObject[] Pieces() =>
            Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.gameObject.name == "GlassPiece" && r.enabled)   // pooled ones are parked, unseen
                .Select(r => r.gameObject).ToArray();

        [UnityTest]
        public IEnumerator AStruckPaneFallsAsItsOwnPiecesFromWhereItStood()
        {
            Assert.IsNotNull(_material, "no glass material");
            PaneFrame pane = Pane();
            Vector3 struck = pane.At(new Vector2(0.65f, 0.95f));
            int count = GlassFractureBurst.Shatter(_material, pane, struck, Vector3.forward, BlowStrength.Light, 7);
            Assert.Greater(count, 20, "a struck pane should break into a few dozen pieces");
            Assert.AreEqual(count, GlassFractureBurst.LiveShards);

            // At first every piece is in the pane, where it was part of it.
            GameObject[] pieces = Pieces();
            Assert.AreEqual(count, pieces.Length);
            foreach (GameObject piece in pieces)
            {
                Vector2 at = pane.Of(piece.transform.position);
                Assert.That(at.x, Is.InRange(-0.01f, 1.31f));
                Assert.That(at.y, Is.InRange(-0.01f, 1.91f));
                Assert.AreEqual(5.0f, piece.transform.position.z, 0.01f, "a piece is out of the pane's plane before it fell");
            }
            Vector3[] before = pieces.Select(p => p.transform.position).ToArray();

            // A quarter of a second on: the slivers round the blow have gone in, the way the blow went; those on the frame mostly still hang.
            for (int i = 0; i < 15; i++) yield return null;
            int wentIn = 0, stillHanging = 0;
            for (int i = 0; i < pieces.Length; i++)
            {
                Vector3 moved = pieces[i].transform.position - before[i];
                if (moved.z > 0.2f) wentIn++;
                if (moved.sqrMagnitude < 1e-6f) stillHanging++;
            }
            Assert.Greater(wentIn, 5, "the pieces round the blow should go in through the pane, the way the blow went");
            Assert.Greater(stillHanging, 0, "the big pieces on the frame should hang on a moment before they drop");

            // Two seconds on, all of it is down on the ground (the wall here is only a picture: nothing stops them going in).
            for (int i = 0; i < 120; i++) yield return null;
            float highest = pieces.Max(p => p.transform.position.y);
            Assert.Less(highest, 0.5f, "glass still in the air two seconds after the break");

            // And after its time it is all gone, back in the pool.
            for (int i = 0; i < Mathf.CeilToInt((GlassFractureBurst.LifetimeSeconds * 1.2f + 1.0f) * 60.0f); i++) yield return null;
            Assert.AreEqual(0, GlassFractureBurst.LiveShards, "broken glass left lying for ever");
        }

        [UnityTest]
        public IEnumerator ThePoolMakesNoNewPiecesForTheNextWindowAndCapsTheGlassInTheAir()
        {
            PaneFrame pane = Pane();
            GlassFractureBurst.Shatter(_material, pane, pane.At(new Vector2(0.5f, 1.0f)), Vector3.forward, BlowStrength.Heavy, 1);
            yield return null;
            GlassFractureBurst.Clear();
            int made = GlassFractureBurst.ShardsMade;
            GlassFractureBurst.Shatter(_material, pane, pane.At(new Vector2(0.4f, 0.9f)), Vector3.forward, BlowStrength.Heavy, 1);
            Assert.AreEqual(made, GlassFractureBurst.ShardsMade, "the second window made new pieces instead of reusing the first one's");

            for (int i = 0; i < 12; i++)
            {
                GlassFractureBurst.Shatter(_material, pane, pane.At(new Vector2(0.2f + i * 0.07f, 0.9f)), Vector3.forward, BlowStrength.Light, 100 + i);
            }
            Assert.LessOrEqual(GlassFractureBurst.LiveShards, GlassFractureBurst.MaxLiveShards, "more glass in the air than the cap");
            yield return null;
        }

        /// <summary>
        /// A body runs straight through a floor-length pane that has just broken, and on over its glass on the ground, neither lifted onto a piece
        /// hanging in the frame nor tripped by one lying there (in a full run one held a body 0.58 m up): the glass is walked through.
        /// </summary>
        [UnityTest]
        public IEnumerator ABodyRunsThroughAJustBrokenWindowAndOverItsGlassUnlifted()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, -2.0f), Quaternion.identity);
            try
            {
                player.GetComponent<CityX.Character.PlayerInputReader>().enabled = false;
                var body = player.GetComponent<CityX.Character.PlayerLocomotion>();
                for (int i = 0; i < 20; i++)
                {
                    body.Tick(new CityX.Character.LocomotionInput(), 1.0f / 60.0f);
                    yield return null;
                }
                // A floor-length pane across the way, 2 m ahead, struck from this side: its glass falls ahead, the frame's pieces hang a moment.
                var pane = new PaneFrame(new Vector3(-1.0f, 0.02f, 1.0f), Vector3.right, Vector3.up, Vector3.back, 2.0f, 2.4f);
                GlassFractureBurst.Shatter(_material, pane, pane.At(new Vector2(1.0f, 1.0f)), Vector3.forward, BlowStrength.Heavy, 9);
                var run = new CityX.Character.LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = 0.0f, Sprint = true };
                float highest = 0.0f;
                for (int i = 0; i < 90; i++)
                {
                    body.Tick(run, 1.0f / 60.0f);
                    highest = Mathf.Max(highest, player.transform.position.y);
                    yield return null;
                }
                Assert.Greater(player.transform.position.z, 5.0f, $"the body was stopped by the broken glass at {player.transform.position}");
                Assert.Less(highest, 0.15f, $"the body was lifted {highest:0.00} m onto the broken glass");
            }
            finally
            {
                Object.Destroy(player);
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// A falling piece knee high in the way is not a step: the step up took it for one (it looks for solid things by its own rays, which the
        /// controller's LetThrough does not reach) and lifted the body. Where the pieces were depended on frame times, so the run above only sometimes met one.
        /// </summary>
        [UnityTest]
        public IEnumerator AKneeHighPieceInTheWayIsNotAStepToClimb()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, -2.0f), Quaternion.identity);
            var piece = new GameObject("GlassPiece") { layer = GlassFractureBurst.ShardLayer };
            piece.transform.position = new Vector3(0.0f, 0.25f, 1.0f);
            piece.AddComponent<BoxCollider>().size = new Vector3(3.0f, 0.5f, 0.5f);
            Physics.SyncTransforms();
            try
            {
                player.GetComponent<CityX.Character.PlayerInputReader>().enabled = false;
                var body = player.GetComponent<CityX.Character.PlayerLocomotion>();
                for (int i = 0; i < 20; i++)
                {
                    body.Tick(new CityX.Character.LocomotionInput(), 1.0f / 60.0f);
                    yield return null;
                }
                var run = new CityX.Character.LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = 0.0f, Sprint = true };
                float highest = 0.0f;
                for (int i = 0; i < 90; i++)
                {
                    body.Tick(run, 1.0f / 60.0f);
                    highest = Mathf.Max(highest, player.transform.position.y);
                    yield return null;
                }
                Assert.Greater(player.transform.position.z, 2.0f, $"the body was stopped by the piece at {player.transform.position}");
                Assert.Less(highest, 0.15f, $"the body was lifted {highest:0.00} m onto the piece");
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(piece);
            }
#else
            yield break;
#endif
        }

        /// <summary>THE OWNER'S QUESTION, MEASURED: what one break costs on the frame it happens, and what a full load of falling glass costs the physics.</summary>
        [UnityTest]
        public IEnumerator ABreakIsCheapAndAFullLoadOfFallingGlassIsCheapToSimulate()
        {
            PaneFrame pane = Pane();
            GlassFractureBurst.Prewarm(GlassFractureBurst.MaxLiveShards, _material);
            yield return null;

            // The first break in a session also pays for compiling its code (a one-off of a couple of milliseconds): timed apart.
            var first = System.Diagnostics.Stopwatch.StartNew();
            GlassFractureBurst.Shatter(_material, pane, pane.At(new Vector2(0.2f, 0.7f)), Vector3.forward, BlowStrength.Light, 199);
            first.Stop();
            yield return null;
            var watch = new System.Diagnostics.Stopwatch();
            int breaks = 0;
            for (int i = 0; i < 6; i++)
            {
                watch.Start();
                GlassFractureBurst.Shatter(_material, pane, pane.At(new Vector2(0.3f + i * 0.1f, 0.8f)), Vector3.forward, BlowStrength.Light, 200 + i);
                watch.Stop();
                breaks++;
            }
            double perBreak = watch.Elapsed.TotalMilliseconds / breaks;
            yield return null;

            // A full load: as many pieces as are ever alive, falling and tumbling, the physics stepped by hand and timed.
            SimulationMode mode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            double physics = 0.0;
            int steps = 0;
            try
            {
                // Let every piece go first (the frame's hang over): the bodies are what costs.
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    var step = System.Diagnostics.Stopwatch.StartNew();
                    Physics.Simulate(1.0f / 60.0f);
                    step.Stop();
                    if (i >= 50)
                    {
                        physics += step.Elapsed.TotalMilliseconds;
                        steps++;
                    }
                }
            }
            finally
            {
                Physics.simulationMode = mode;
            }
            double perStep = physics / Mathf.Max(1, steps);
            Debug.Log($"CityX glass cost: {perBreak:0.000} ms a break (the first {first.Elapsed.TotalMilliseconds:0.000} ms; the last {GlassFractureBurst.LastBreakMs:0.000} ms, its meshes {GlassFractureBurst.LastMeshMs:0.000} ms; " +
                $"{GlassFractureBurst.LiveShards} pieces alive), {perStep:0.000} ms of physics a frame " +
                $"with them all falling");
            Assert.Less(perBreak, 1.5, $"one break took {perBreak:0.000} ms on its frame");
            Assert.Less(perStep, 4.0, $"the falling glass costs {perStep:0.000} ms of physics a frame");
        }
    }
}
