using System.Collections;
using System.Collections.Generic;
using System.IO;
using CityX.Character;
using CityX.Core;
using CityX.Wearables;
using FishNet.Managing;
using FishNet.Transporting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// PRESSED DESIGNS OVER REAL SOCKETS (docs/HEAT_PRESS.md; owner, 2026-11-24: "visible across computers"): a dedicated server and two players.
    /// A design player A pressed reaches the server; player B, meeting its id on a shirt, asks for it and gets it; a design asked for before its
    /// wearer's upload has arrived is asked for again until it comes. (One process stands in for three machines: each player's own store is
    /// swapped in while it acts.)
    /// </summary>
    public sealed class NetworkedDesignsPlayTests
    {
        private static ushort _nextPort = 45900;
        private const float TimeoutSeconds = 30.0f;

        private readonly List<GameObject> _made = new List<GameObject>();
        private readonly List<NetworkManager> _managers = new List<NetworkManager>();
        private readonly List<string> _folders = new List<string>();

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
            DesignLibrary.Store = null;
            DesignLibrary.Reset();
            foreach (string folder in _folders) if (Directory.Exists(folder)) Directory.Delete(folder, true);
            LogAssert.ignoreFailingMessages = false;
        }

        private DesignStore Store()
        {
            string folder = Path.Combine(Path.GetTempPath(), "cityx-netdesigns-" + System.Guid.NewGuid().ToString("N"));
            _folders.Add(folder);
            return new DesignStore(folder);
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

        private (NetworkManager manager, DesignNetwork designs) Machine(string name)
        {
            GameObject prefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(prefab, "missing SpikeNetworkManager prefab -- run CityX/Spike/Rebuild Spike Assets");
            GameObject go = Object.Instantiate(prefab);
            go.name = name;
            _made.Add(go);
            var manager = go.GetComponent<NetworkManager>();
            _managers.Add(manager);
            var relay = new GameObject("DesignNetwork " + name);
            _made.Add(relay);
            var designs = relay.AddComponent<DesignNetwork>();
            designs.Manager = manager;
            return (manager, designs);
        }

        private static IEnumerator Join(NetworkManager client, ushort port)
        {
            Transport t = client.TransportManager.Transport;
            t.SetPort(port);
            t.SetClientAddress("127.0.0.1");
            client.ClientManager.StartConnection("127.0.0.1", port);
            yield return WaitUntil(() => client.IsClientStarted && client.ClientManager.Connection.IsAuthenticated, client.name + " to join");
        }

        private static byte[] Design(Color colour)
        {
            var pixels = new Color32[ShirtDesignArea.Width * ShirtDesignArea.Height];
            for (int i = 0; i < pixels.Length; i += 3) pixels[i] = colour;
            return ShirtDesignBlob.Pack(DesignPng.Encode(pixels), null);
        }

        [UnityTest]
        public IEnumerator APressedDesignReachesTheServerAndAnotherPlayerAsksForItAndGetsIt()
        {
            LogAssert.ignoreFailingMessages = true;
            ushort port = _nextPort++;
            (NetworkManager server, DesignNetwork _) = Machine("Designs Server");
            Transport st = server.TransportManager.Transport;
            st.SetPort(port);
            st.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => server.IsServerStarted, "the server to start");
            (NetworkManager a, DesignNetwork onA) = Machine("Designs Client A");
            (NetworkManager b, DesignNetwork onB) = Machine("Designs Client B");
            yield return Join(a, port);
            yield return Join(b, port);

            // A presses a design: it goes to the server, which keeps it.
            byte[] red = Design(Color.red);
            string redId = DesignStore.IdOf(red);
            onA.Upload(redId, red);
            yield return WaitUntil(() => DesignStore.Local.Has(redId), "the server to keep A's design");

            // B, with a store of its own that has never seen it, meets the id on a shirt and asks.
            DesignLibrary.Store = Store();
            DesignLibrary.Reset();
            byte[] got = null;
            DesignLibrary.Get(redId, blob => got = blob);
            Assert.IsNull(got, "B had A's design without asking");
            onB.Request(redId);
            yield return WaitUntil(() => got != null, "B to be sent A's design");
            CollectionAssert.AreEqual(red, got);
            Assert.IsTrue(DesignLibrary.Store.Has(redId), "B did not keep it");

            // B asks for one whose wearer has not sent it yet: the server has nothing; B asks again a little later, and gets it once it is there.
            byte[] blue = Design(Color.blue);
            string blueId = DesignStore.IdOf(blue);
            byte[] late = null;
            DesignLibrary.Get(blueId, blob => late = blob);
            onB.Request(blueId);
            float asked = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - asked < 1.0f) yield return null;
            Assert.IsNull(late, "a design the server never had arrived");
            onA.Upload(blueId, blue);
            yield return WaitUntil(() => late != null, "B to get the design once its wearer sent it");
            CollectionAssert.AreEqual(blue, late);

            // Junk under a design's name is not kept by the server.
            byte[] junk = { 1, 2, 3, 4 };
            onA.Upload(DesignStore.IdOf(junk), junk);
            float sent = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - sent < 1.5f) yield return null;
            Assert.IsFalse(DesignStore.Local.Has(DesignStore.IdOf(junk)), "the server kept junk");
            Assert.IsNotNull(b);
        }
    }
}
