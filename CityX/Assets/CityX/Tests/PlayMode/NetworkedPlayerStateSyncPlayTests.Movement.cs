using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using FishNet.Managing;
using FishNet.Object;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// How another player's copy MOVES (docs/MULTIPLAYER_MOVEMENT.md; owner after the first online duel, 2026-11-13: "really kinda choppy and
    /// laggy", "at some point the characters stop being synced up and when i looked where he was on his screen i didnt see him at the same
    /// place on my screen"): a body walked at a steady pace walks at a steady pace on the copy, a little behind, and stands where its owner
    /// stands once it stops, at the frame rates games really run at and over a line that loses and delays packets as Wi-Fi does. These run in
    /// real time: a session's clock is the real one.
    /// </summary>
    public sealed partial class NetworkedPlayerStateSyncPlayTests
    {
        private struct WalkSeen
        {
            public float Speed, Roughness, Behind, RestError, ServerRestError;
            public int Stalls, Lurches, Frames;

            public override string ToString() =>
                $"copy {Speed:F2} m/s, roughness {Roughness:P0}, {Stalls} stalls + {Lurches} lurches in {Frames} frames, {Behind * 1000.0f:F0} ms behind, "
                + $"{RestError * 100.0f:F1} cm off at rest ({ServerRestError * 100.0f:F1} cm on the server)";
        }

        /// <summary>Every machine's line delays each packet and loses or reorders this share (0 to 1) of the unreliable ones.</summary>
        private void WorsenTheLine(long latencyMilliseconds, double lostShare, double outOfOrderShare)
        {
            var managers = new List<NetworkManager>(_clients) { _server };
            foreach (NetworkManager manager in managers)
            {
                var simulator = manager.TransportManager.LatencySimulator;
                simulator.SetLatency(latencyMilliseconds);
                simulator.SetPacketLoss(lostShare);
                simulator.SetOutOfOrder(outOfOrderShare);
                simulator.SetEnabled(latencyMilliseconds > 0 || lostShare > 0.0 || outOfOrderShare > 0.0);
            }
        }

        /// <summary>
        /// The most a copy may trail its owner here, seconds: the playback's own delay, and a frame or two at each of the two hops these
        /// tests' line has (through a server that is nobody's own machine; a host and a friend have one hop).
        /// </summary>
        private const float FurthestBehindSeconds = 0.35f;

#if UNITY_EDITOR
        private static IEnumerator Drive(PlayerLocomotion owner, float seconds, System.Func<float, LocomotionInput> input)
        {
            for (float t = 0.0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                owner.Tick(input(t), Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                yield return null;
            }
        }

        /// <summary>
        /// Player A walks straight for a while, then turns on the spot and stands, as a fighter does; what player B's copy of A did
        /// meanwhile, at <paramref name="framesPerSecond"/>.
        /// </summary>
        private IEnumerator WalkAndWatch(int framesPerSecond, System.Action<WalkSeen> done)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = framesPerSecond;
            _aOnA.GetComponent<PlayerInputReader>().enabled = false;
            var owner = _aOnA.GetComponent<PlayerLocomotion>();
            Transform real = _aOnA.transform, copy = _aOnB.transform;
            var walk = new LocomotionInput { Move = Vector2.up, YawDegrees = 0.0f };

            // Back to the start: each walk would otherwise carry the body off the ground and out of the other player's sight.
            var controller = _aOnA.GetComponent<CharacterController>();
            controller.enabled = false;
            real.position = new Vector3(0.0f, 0.5f, -30.0f);
            controller.enabled = true;
            Physics.SyncTransforms();
            yield return Drive(owner, 1.0f, _ => default);
            yield return Drive(owner, 1.5f, _ => walk);

            var speeds = new List<float>();
            float behind = 0.0f;
            Vector3 was = copy.position;
            for (float t = 0.0f; t < 4.0f; t += Time.unscaledDeltaTime)
            {
                owner.Tick(walk, Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                yield return null;
                Vector3 now = copy.position;
                speeds.Add(Vector3.Dot(now - was, Vector3.forward) / Mathf.Max(Time.unscaledDeltaTime, 1e-4f));
                was = now;
                behind += real.position.z - now.z;
            }
            float mean = 0.0f;
            foreach (float s in speeds) mean += s;
            mean /= speeds.Count;
            float variance = 0.0f;
            int stalls = 0, lurches = 0;
            foreach (float s in speeds)
            {
                variance += (s - mean) * (s - mean);
                if (s < mean * 0.5f) stalls++;
                if (s > mean * 1.5f) lurches++;
            }
            var seen = new WalkSeen
            {
                Speed = mean,
                Roughness = Mathf.Sqrt(variance / speeds.Count) / Mathf.Max(mean, 0.01f),
                Stalls = stalls,
                Lurches = lurches,
                Frames = speeds.Count,
                Behind = behind / speeds.Count / Mathf.Max(mean, 0.01f),
            };

            yield return Drive(owner, 2.0f, t => new LocomotionInput { YawDegrees = Mathf.Min(t, 1.0f) * 90.0f });
            seen.RestError = Vector3.Distance(real.position, copy.position);
            NetworkObject onServer = _server.ServerManager.Objects.Spawned[_aOnA.ObjectId];
            seen.ServerRestError = Vector3.Distance(real.position, onServer.transform.position);
            done(seen);
        }

        private IEnumerator WatchWalks(string line, int[] frameRates, int mostStallsAndLurches)
        {
            int wasRate = Application.targetFrameRate, wasSync = QualitySettings.vSyncCount;
            try
            {
                // The first walk only warms the machine up (shaders, the first garbage collection): its frames are not the game's.
                yield return WalkAndWatch(frameRates[0], _ => { });
                foreach (int framesPerSecond in frameRates)
                {
                    WalkSeen seen = default;
                    yield return WalkAndWatch(framesPerSecond, s => seen = s);
                    Debug.Log($"CityX copy walk, {line}, {framesPerSecond} fps: {seen}");
                    // Frames, not the spread of their speeds: one long frame of this test's own (the editor's, the collector's) is a stall and a lurch.
                    Assert.LessOrEqual(seen.Stalls + seen.Lurches, mostStallsAndLurches, $"at {framesPerSecond} fps over {line} the copy stalls and lurches: {seen}");
                    Assert.Less(seen.Behind, FurthestBehindSeconds, $"at {framesPerSecond} fps over {line} the copy is far behind its owner: {seen}");
                    Assert.Less(seen.RestError, 0.02f, $"at {framesPerSecond} fps over {line} the copy does not stand where its owner stands: {seen}");
                    Assert.Less(seen.ServerRestError, 0.02f, $"at {framesPerSecond} fps over {line} the server has the body somewhere else: {seen}");
                }
            }
            finally
            {
                Application.targetFrameRate = wasRate;
                QualitySettings.vSyncCount = wasSync;
            }
        }
#endif

        [UnityTest]
        public IEnumerator ACopyWalksSteadilyAtTheFrameRatesGamesRunAt()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            yield return WatchWalks("a clean line", new[] { 60, 45, 30, 90 }, mostStallsAndLurches: 8);
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ACopyWalksSteadilyAndComesToRestInPlaceOverALineThatLosesPackets()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            WorsenTheLine(latencyMilliseconds: 30, lostShare: 0.05, outOfOrderShare: 0.02);
            yield return WatchWalks("a line 30 ms late that loses 5% and reorders 2%", new[] { 60, 45 }, mostStallsAndLurches: 10);
            WorsenTheLine(latencyMilliseconds: 0, lostShare: 0.3, outOfOrderShare: 0.0);
            yield return WatchWalks("a line that loses 30%", new[] { 60 }, mostStallsAndLurches: 24);
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }
    }
}
