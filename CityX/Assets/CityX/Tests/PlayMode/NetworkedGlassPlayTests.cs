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
    /// Broken glass over real sockets (a server and two clients in this process, each with its own record): a pane broken on one player's machine
    /// reaches the server and every other player (Character.GlassDamageNetwork) with its holes, going further goes round too, a late or repeated
    /// message never puts glass back, and a player who joins later is told every broken pane. The multiplayer-first rule: anything a player does,
    /// the others see (owner, 2026-10-16, with the breakable door glass).
    /// </summary>
    public sealed class NetworkedGlassPlayTests
    {
        private static ushort _nextPort = 7991;
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
            GlassDamage.Relay = null;
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

        private GlassDamageNetwork Glass(NetworkManager manager, GlassDamage record)
        {
            var go = new GameObject("GlassDamageNetwork " + manager.name);
            _made.Add(go);
            var glass = go.AddComponent<GlassDamageNetwork>();
            glass.Manager = manager;
            glass.Target = record;
            return glass;
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
        public IEnumerator APaneBrokenOnOneMachineIsBrokenOnTheOthersAndALateJoinerIsTold()
        {
            LogAssert.ignoreFailingMessages = true;
            ushort port = _nextPort++;
            NetworkManager server = Manager("Glass Server");
            Transport st = server.TransportManager.Transport;
            st.SetPort(port);
            st.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => server.IsServerStarted, "the server to start");
            Glass(server, new GlassDamage());

            var onARecord = new GlassDamage();
            var onBRecord = new GlassDamage();
            NetworkManager a = Manager("Glass Client A");
            NetworkManager b = Manager("Glass Client B");
            GlassDamageNetwork onA = Glass(a, onARecord);
            GlassDamageNetwork onB = Glass(b, onBRecord);
            yield return Join(a, port);
            yield return Join(b, port);
            yield return WaitUntil(() => onA.StatesHeard > 0 && onB.StatesHeard > 0, "both clients to be told the broken glass on joining");

            // Player A shoots a door's glass: what GlassBreaker does on A's machine, through its relay.
            var key = new PaneKey(3210, WindowGrid.Wall.South, DoorGlass.PaneColumnBase + 5 * 2, 4);
            onARecord.Shoot(key, new Vector2(0.4f, 0.5f), out _);
            onA.Send(onARecord.Now(key, new Vector3(10, 20, 30), Vector3.back, BlowStrength.Bullet));
            yield return WaitUntil(() => onBRecord.StateOf(key) == PaneState.Cracked, "player B to see player A's shot");
            Assert.AreEqual(1, onBRecord.HoleCount(key), "player B's copy has not got the bullet hole");

            // A second shot, then a blow that brings it down: both go round, and a late copy of the first message changes nothing.
            onARecord.Shoot(key, new Vector2(0.6f, 0.4f), out _);
            onA.Send(onARecord.Now(key, new Vector3(10, 20, 30), Vector3.back, BlowStrength.Bullet));
            yield return WaitUntil(() => onBRecord.HoleCount(key) == 2, "player B to see the second hole");
            var stale = GlassDamageNetwork.ToChange(GlassDamageNetwork.ToMessage(onARecord.Now(key, Vector3.zero, Vector3.zero, BlowStrength.Bullet)));
            onARecord.Hit(key, null, shatterOutright: true);
            onA.Send(onARecord.Now(key, new Vector3(10, 20, 30), Vector3.back, BlowStrength.Heavy));
            yield return WaitUntil(() => onBRecord.StateOf(key) == PaneState.Shattered, "player B to see the glass come down");
            int heard = onB.ChangesHeard;
            onA.Send(stale);
            yield return WaitUntil(() => onB.ChangesHeard > heard, "player B to hear the late message");
            Assert.AreEqual(PaneState.Shattered, onBRecord.StateOf(key), "a late message put the broken glass back up on player B's screen");

            // A player who joins now is told it is broken.
            var onCRecord = new GlassDamage();
            NetworkManager c = Manager("Glass Client C");
            GlassDamageNetwork onC = Glass(c, onCRecord);
            yield return Join(c, port);
            yield return WaitUntil(() => onC.StatesHeard > 0, "the late joiner to be told the broken glass");
            Assert.GreaterOrEqual(onC.LastStateSize, 1, "the late joiner was told no broken glass");
            Assert.AreEqual(PaneState.Shattered, onCRecord.StateOf(key), "the late joiner sees the door's glass whole");
        }
    }
}
