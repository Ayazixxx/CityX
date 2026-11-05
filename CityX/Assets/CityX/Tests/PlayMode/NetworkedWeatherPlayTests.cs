using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using FishNet.Managing;
using FishNet.Transporting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The weather over real sockets (a server and players in this process, each machine with its own weather): an order given on one machine changes all of them, the giver only when the server
    /// says so; a player who joins later is told how it is, how much lies too; a forged order is refused. docs/SNOW.md.
    /// </summary>
    public sealed class NetworkedWeatherPlayTests
    {
        private static ushort _nextPort = 46300;
        private const float TimeoutSeconds = 30.0f;

        private readonly List<GameObject> _made = new List<GameObject>();
        private readonly List<NetworkManager> _managers = new List<NetworkManager>();

        private sealed class Sky : IWeatherControl
        {
            public WeatherOrder Current = new WeatherOrder { Cover = 0.0f };
            public int Told;
            public bool Dried;

            public WeatherOrder Now => Current;

            public void Apply(in WeatherOrder order)
            {
                Told++;
                float cover = order.Cover >= 0.0f ? order.Cover : Current.Cover;
                Current = order;
                Current.Cover = cover;
                Dried |= order.Dry;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = true;
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

        private (NetworkManager manager, Sky sky, NetworkedWeather network) Machine(string name)
        {
            GameObject prefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(prefab, "missing SpikeNetworkManager prefab -- run CityX/Spike/Rebuild Spike Assets");
            GameObject go = Object.Instantiate(prefab);
            go.name = name;
            _made.Add(go);
            var manager = go.GetComponent<NetworkManager>();
            _managers.Add(manager);
            var sky = new Sky();
            var networkObject = new GameObject("NetworkedWeather " + name);
            _made.Add(networkObject);
            var network = networkObject.AddComponent<NetworkedWeather>();
            network.Manager = manager;
            network.Target = sky;
            return (manager, sky, network);
        }

        private static IEnumerator Join(NetworkManager client, ushort port)
        {
            Transport t = client.TransportManager.Transport;
            t.SetPort(port);
            t.SetClientAddress("127.0.0.1");
            client.ClientManager.StartConnection("127.0.0.1", port);
            yield return WaitUntil(() => client.IsClientStarted && client.ClientManager.Connection.IsAuthenticated, client.name + " to join");
        }

        private IEnumerator Session(ushort port, (NetworkManager manager, Sky sky, NetworkedWeather network) host)
        {
            host.manager.TransportManager.Transport.SetPort(port);
            host.manager.ServerManager.StartConnection(port);
            yield return WaitUntil(() => host.manager.IsServerStarted, "the server to start");
            yield return Join(host.manager, port);
        }

        [UnityTest]
        public IEnumerator AnOrderOnOneMachineChangesEveryMachineTheGiverOnlyWhenTheServerSaysSo()
        {
            ushort port = _nextPort++;
            var host = Machine("Host");
            var friend = Machine("Friend");
            yield return Session(port, host);
            yield return Join(friend.manager, port);
            yield return WaitUntil(() => friend.sky.Told > 0, "the friend to be told how it is");

            friend.network.Send(new WeatherOrder { Snow = SnowRules.Blizzard, Cover = -1.0f });
            Assert.AreEqual(0.0f, friend.sky.Current.Snow, "it changed on the giver's machine before the server said so");
            yield return WaitUntil(() => host.sky.Current.Snow > 0.0f && friend.sky.Current.Snow > 0.0f, "the snow to reach both");
            Assert.AreEqual(SnowRules.Blizzard, host.sky.Current.Snow, 1e-4f);
            Assert.AreEqual(SnowRules.Blizzard, friend.sky.Current.Snow, 1e-4f);

            host.network.Send(new WeatherOrder { Rain = Weather.HeavyRain, Snow = 0.0f, Cover = -1.0f, Dry = false });
            yield return WaitUntil(() => friend.sky.Current.Rain > 0.0f, "the rain to reach the friend");
            Assert.AreEqual(0.0f, friend.sky.Current.Snow, 1e-4f, "the snow did not stop");
        }

        [UnityTest]
        public IEnumerator APlayerWhoJoinsLaterIsToldWhatFallsAndHowMuchLies()
        {
            ushort port = _nextPort++;
            var host = Machine("Host");
            yield return Session(port, host);
            host.network.Send(new WeatherOrder { Snow = SnowRules.ModerateSnow, Cover = 0.7f });
            yield return WaitUntil(() => host.sky.Current.Snow > 0.0f, "the snow at the host");

            var late = Machine("Late");
            yield return Join(late.manager, port);
            yield return WaitUntil(() => late.sky.Told > 0, "the latecomer to be told");
            Assert.AreEqual(SnowRules.ModerateSnow, late.sky.Current.Snow, 1e-4f);
            Assert.AreEqual(0.7f, late.sky.Current.Cover, 1e-4f, "it joined a snowy city with bare streets");
        }

        [UnityTest]
        public IEnumerator AForgedOrderIsRefused()
        {
            ushort port = _nextPort++;
            var host = Machine("Host");
            var friend = Machine("Friend");
            yield return Session(port, host);
            yield return Join(friend.manager, port);
            yield return WaitUntil(() => friend.sky.Told > 0, "the friend to be told how it is");
            int told = host.sky.Told;

            friend.manager.ClientManager.Broadcast(new WeatherMessage { Snow = 50.0f, Cover = -1.0f });
            friend.manager.ClientManager.Broadcast(new WeatherMessage { Rain = float.NaN, Cover = -1.0f });
            friend.manager.ClientManager.Broadcast(new WeatherMessage { Cover = 9.0f });
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 0.5f)
            {
                yield return null;
            }
            Assert.AreEqual(told, host.sky.Told, "a forged order changed the weather");
        }
    }
}
