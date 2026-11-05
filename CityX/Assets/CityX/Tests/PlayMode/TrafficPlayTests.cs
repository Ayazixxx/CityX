using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.UI;
using CityX.View;
using CityX.World;
using FishNet.Managing;
using FishNet.Transporting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The traffic lights in the running game (docs/TRAFFIC_CONTROL.md): the real city has them, drawn from the shared clock; and two machines in a session over real
    /// sockets read the same clock, so they see the same lights.
    /// </summary>
    public sealed class TrafficPlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private static ushort _nextPort = 46400;
        private const float TimeoutSeconds = 30.0f;

        private readonly List<GameObject> _made = new List<GameObject>();
        private readonly List<NetworkManager> _managers = new List<NetworkManager>();
        private Scene _scene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = true;
            SignalClock.Shared = null;
            foreach (NetworkManager m in _managers)
            {
                if (m == null) continue;
                if (m.ClientManager.Started) m.ClientManager.StopConnection();
                if (m.ServerManager.Started) m.ServerManager.StopConnection(true);
            }
            yield return null;
            foreach (GameObject go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            _managers.Clear();
            if (_scene.IsValid() && _scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private static IEnumerator WaitUntil(System.Func<bool> done, string what)
        {
            float start = Time.realtimeSinceStartup;
            while (!done())
            {
                Assert.Less(Time.realtimeSinceStartup - start, TimeoutSeconds, $"timed out waiting for {what}");
                yield return null;
            }
        }

        private (NetworkManager manager, NetworkedSignalClock clock) Machine(string name)
        {
            GameObject prefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(prefab, "missing SpikeNetworkManager prefab -- run CityX/Spike/Rebuild Spike Assets");
            GameObject go = Object.Instantiate(prefab);
            go.name = name;
            _made.Add(go);
            var manager = go.GetComponent<NetworkManager>();
            _managers.Add(manager);
            var clockObject = new GameObject("SignalClock " + name);
            _made.Add(clockObject);
            var clock = clockObject.AddComponent<NetworkedSignalClock>();
            clock.Manager = manager;
            return (manager, clock);
        }

        [UnityTest]
        public IEnumerator TwoMachinesInASessionReadTheSameClockSoTheySeeTheSameLights()
        {
            ushort port = _nextPort++;
            var host = Machine("Host");
            var friend = Machine("Friend");
            host.manager.TransportManager.Transport.SetPort(port);
            host.manager.ServerManager.StartConnection(port);
            yield return WaitUntil(() => host.manager.IsServerStarted, "the server to start");
            // The server has been up a while before the friend joins: the friend must come in on the server's time, not its own.
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 1.5f) yield return null;

            Transport t = friend.manager.TransportManager.Transport;
            t.SetPort(port);
            t.SetClientAddress("127.0.0.1");
            friend.manager.ClientManager.StartConnection("127.0.0.1", port);
            yield return WaitUntil(() => friend.manager.IsClientStarted && friend.manager.ClientManager.Connection.IsAuthenticated, "the friend to join");
            start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 2.0f) yield return null;

            double hostSeconds = host.clock.Seconds, friendSeconds = friend.clock.Seconds;
            Assert.Greater(hostSeconds, 3.0, "the session's clock is not running");
            Assert.AreEqual(hostSeconds, friendSeconds, 0.35, "the friend's lights would be out of step with the host's");

            // The same reading gives the same lamp: a light is a function of the clock alone.
            var timing = new SignalTiming { Cycle = 90.0, GreenMajor = 50.0, YellowMajor = 3.5, AllRedMajor = 1.0, GreenMinor = 31.0, YellowMinor = 3.0, AllRedMinor = 1.5 };
            Assert.AreEqual(timing.VehicleAt(true, hostSeconds), timing.VehicleAt(true, hostSeconds));
            double before = host.clock.Seconds;
            yield return null;
            yield return null;
            Assert.GreaterOrEqual(host.clock.Seconds, before, "the clock ran backwards");
        }

        [UnityTest]
        public IEnumerator TheRealCityHasItsLightsAndTheyRunOnTheSharedClock()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);
            var console = Object.FindFirstObjectByType<DevConsoleView>();
            CityWorld world = CurrentWorld.Instance;
            world.Plan.TryFindSpawn(out double sx, out double sz);
            console.Streamer.StreamAround(new Vector3((float)sx, (float)world.Plan.GroundHeight(sx, sz), (float)sz), int.MaxValue);
            yield return null;
            yield return null;

            // The spawn stands at lights: their lamps are in the chunks round it, one mesh a chunk.
            int lampMeshes = 0, lampQuads = 0;
            foreach (MeshFilter filter in console.Streamer.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.name != CityStreamer.SignalLampsName) continue;
                lampMeshes++;
                lampQuads += filter.sharedMesh.vertexCount / 4;
                Assert.AreEqual("CityX/SignalLamp", filter.GetComponent<MeshRenderer>().sharedMaterial.shader.name);
            }
            Assert.Greater(lampMeshes, 0, "no traffic lights' lamps anywhere round the spawn");
            Assert.Greater(lampQuads, 60, "hardly a lamp round the spawn");

            SignalClock.Shared = () => 1234.5;
            yield return null;
            yield return null;
            Assert.AreEqual(1234.5f, Shader.GetGlobalFloat("_CityXSignalClock"), 0.01f, "the lamps are not told the shared clock");
            SignalClock.Shared = () => SignalTiming.ClockWrap + 7.0;
            yield return null;
            yield return null;
            Assert.AreEqual(7.0f, Shader.GetGlobalFloat("_CityXSignalClock"), 0.01f, "the clock is not wrapped at the hour: a float would lose the seconds in a long session");
#else
            yield break;
#endif
        }
    }
}
