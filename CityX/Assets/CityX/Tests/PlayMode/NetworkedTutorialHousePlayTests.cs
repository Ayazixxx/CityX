using System;
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
    /// Old Guy's house over real sockets (a server and two clients in this process): a plank or a pane broken on one player's machine reaches the
    /// server and every other player (Character.TutorialHouseNetwork), a piece the house does not have is refused, and a player who joins later is
    /// told what is broken. The multiplayer-first rule: anything a player does, the others see.
    /// </summary>
    public sealed class NetworkedTutorialHousePlayTests
    {
        private static ushort _nextPort = 7981;
        private const float TimeoutSeconds = 20.0f;

        private readonly List<GameObject> _made = new List<GameObject>();
        private readonly List<NetworkManager> _managers = new List<NetworkManager>();

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
            foreach (GameObject go in _made) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _made.Clear();
            _managers.Clear();
            TutorialHouseDamage.Shared.Clear();
            TutorialHouseDamage.Shared.Relay = null;
            TutorialHousePower.Shared.Reset();
            TutorialHousePower.Shared.Relay = null;
            LogAssert.ignoreFailingMessages = false;
        }

        private static IEnumerator WaitUntil(Func<bool> done, string what)
        {
            float start = Time.realtimeSinceStartup;
            while (!done())
            {
                Assert.Less(Time.realtimeSinceStartup - start, TimeoutSeconds, $"timed out waiting for {what}");
                yield return null;
            }
        }

        private NetworkManager Manager(string name)
        {
            GameObject prefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(prefab, "missing SpikeNetworkManager prefab -- run CityX/Spike/Rebuild Spike Assets");
            GameObject go = UnityEngine.Object.Instantiate(prefab);
            go.name = name;
            _made.Add(go);
            var manager = go.GetComponent<NetworkManager>();
            _managers.Add(manager);
            return manager;
        }

        private TutorialHouseNetwork House(NetworkManager manager)
        {
            var go = new GameObject("TutorialHouseNetwork " + manager.name);
            _made.Add(go);
            var house = go.AddComponent<TutorialHouseNetwork>();
            house.Manager = manager;
            return house;
        }

        private IEnumerator Join(NetworkManager client, ushort port)
        {
            Transport t = client.TransportManager.Transport;
            t.SetPort(port);
            t.SetClientAddress("127.0.0.1");
            client.ClientManager.StartConnection("127.0.0.1", port);
            yield return WaitUntil(() => client.IsClientStarted && client.ClientManager.Connection.IsAuthenticated, client.name + " to join");
        }

        [UnityTest]
        public IEnumerator APieceBrokenOnOneMachineReachesTheOthersAndALateJoinerIsTold()
        {
            LogAssert.ignoreFailingMessages = true;
            TutorialHouseDamage.Shared.Clear();
            ushort port = _nextPort++;
            NetworkManager server = Manager("House Server");
            Transport st = server.TransportManager.Transport;
            st.SetPort(port);
            st.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => server.IsServerStarted, "the server to start");
            House(server);

            NetworkManager a = Manager("House Client A");
            NetworkManager b = Manager("House Client B");
            TutorialHouseNetwork onA = House(a);
            TutorialHouseNetwork onB = House(b);
            yield return Join(a, port);
            yield return Join(b, port);
            yield return WaitUntil(() => onA.StatesHeard > 0 && onB.StatesHeard > 0, "both clients to be told what is broken on joining");

            // A plank of the front door, torn off on A's machine (what TutorialHouseDamage.Break does there, through its relay).
            onA.Send(1);
            yield return WaitUntil(() => onB.BreaksHeard > 0, "player B to hear player A's plank");
            Assert.IsTrue(TutorialHouseDamage.Shared.IsBroken(1), "the server did not take the plank");

            // A piece the house does not have is refused by the server: nobody hears it.
            int heard = onB.BreaksHeard;
            onA.Send(OldGuysHouse.BreakableCount + 5);
            onA.Send(OldGuysHouse.FrontDoorPlanks);   // the first pane, a real one, after it
            yield return WaitUntil(() => onB.BreaksHeard > heard, "player B to hear the pane");
            Assert.AreEqual(heard + 1, onB.BreaksHeard, "the server passed on a piece the house does not have");
            Assert.IsFalse(TutorialHouseDamage.Shared.IsBroken(OldGuysHouse.BreakableCount + 5));

            // A player who joins now is told both.
            NetworkManager c = Manager("House Client C");
            TutorialHouseNetwork onC = House(c);
            yield return Join(c, port);
            yield return WaitUntil(() => onC.StatesHeard > 0, "the late joiner to be told what is broken");
            Assert.AreEqual(2, onC.LastStateSize, "the late joiner was not told the plank and the pane");
        }

        /// <summary>
        /// The fuse box lights the house for everyone (bug hunt M12, 2026-11-06: it lit it for whoever threw the lever alone): thrown on A's
        /// machine, B hears it, and a player who joins later finds the power on.
        /// </summary>
        [UnityTest]
        public IEnumerator ThePowerTurnedOnByOnePlayerIsOnForEveryoneAndALateJoiner()
        {
            LogAssert.ignoreFailingMessages = true;
            TutorialHousePower.Shared.Reset();
            ushort port = _nextPort++;
            NetworkManager server = Manager("Power Server");
            Transport st = server.TransportManager.Transport;
            st.SetPort(port);
            st.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => server.IsServerStarted, "the server to start");
            House(server);

            NetworkManager a = Manager("Power Client A");
            NetworkManager b = Manager("Power Client B");
            TutorialHouseNetwork onA = House(a);
            TutorialHouseNetwork onB = House(b);
            yield return Join(a, port);
            yield return Join(b, port);
            yield return WaitUntil(() => onA.StatesHeard > 0 && onB.StatesHeard > 0, "both clients to be told the house's state on joining");

            // The lever thrown on A's machine (what FuseBoxSwitch.Use does there: TutorialHousePower.TurnOn, through its relay).
            onA.SendPowerOn();
            yield return WaitUntil(() => onB.PowerHeard > 0, "player B to hear the power come on");

            // A player who joins with the house dark here is told it is lit.
            TutorialHousePower.Shared.Reset();
            NetworkManager c = Manager("Power Client C");
            TutorialHouseNetwork onC = House(c);
            yield return Join(c, port);
            yield return WaitUntil(() => onC.StatesHeard > 0, "the late joiner to be told the house's state");
            Assert.IsTrue(TutorialHousePower.Shared.IsOn, "the late joiner finds the house dark");
        }
    }
}
