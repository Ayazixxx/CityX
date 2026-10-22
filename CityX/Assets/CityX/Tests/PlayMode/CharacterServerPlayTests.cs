using System.Collections;
using System.Collections.Generic;
using CityX.Core;
using CityX.Net;
using FishNet.Managing;
using FishNet.Transporting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A player who joins a server over real sockets keeps their characters on that server (docs/SERVER_SAVING.md): the character screen
    /// lists, makes, plays and deletes them through it, a new one starts at the start whatever was sent, and saving while playing lands in
    /// the server's storage.
    /// </summary>
    public sealed class CharacterServerPlayTests
    {
        private static ushort _nextPort = 46300;
        private const float TimeoutSeconds = 30.0f;

        private readonly List<GameObject> _made = new List<GameObject>();
        private readonly List<NetworkManager> _managers = new List<NetworkManager>();
        private RemoteCharacterSource _remote;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = true;
            _remote?.Dispose();
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

        private NetworkManager Machine(string name)
        {
            GameObject prefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(prefab, "missing SpikeNetworkManager prefab -- run CityX/Spike/Rebuild Spike Assets");
            GameObject go = Object.Instantiate(prefab);
            go.name = name;
            _made.Add(go);
            var manager = go.GetComponent<NetworkManager>();
            _managers.Add(manager);
            return manager;
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

        [UnityTest]
        public IEnumerator AJoiningPlayerListsMakesPlaysSavesAndDeletesTheirCharactersOnTheServer()
        {
            LogAssert.ignoreFailingMessages = true;
            ushort port = _nextPort++;
            NetworkManager server = Machine("Character Server");
            server.gameObject.AddComponent<CharacterServer>();
            Transport st = server.TransportManager.Transport;
            st.SetPort(port);
            st.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => server.IsServerStarted, "the server to start");

            NetworkManager client = Machine("Joining Player");
            client.TransportManager.Transport.SetPort(port);
            client.ClientManager.StartConnection("127.0.0.1", port);
            yield return WaitUntil(() => client.IsClientStarted, "the player to join");
            _remote = new RemoteCharacterSource(client);

            List<CharacterSave> listed = null;
            _remote.List(list => listed = list);
            yield return WaitUntil(() => listed != null, "the first list");
            Assert.IsEmpty(listed);

            CharacterSave asked = CharacterSave.Create("Joiner", new Color(0.2f, 0.5f, 0.3f));
            asked.Level = 99;
            asked.HairStyle = "hyde";
            CharacterSave made = null;
            bool answered = false;
            _remote.Create(asked, m => { made = m; answered = true; });
            yield return WaitUntil(() => answered, "the server to make the character");
            Assert.IsNotNull(made, "the server refused a first character");
            Assert.AreEqual(1, made.Level, "the server took the level the player sent");
            Assert.AreEqual("hyde", made.HairStyle);

            listed = null;
            _remote.List(list => listed = list);
            yield return WaitUntil(() => listed != null, "the list after making one");
            Assert.AreEqual(1, listed.Count);
            Assert.AreEqual(made.Id, listed[0].Id);

            CharacterSave served = null;
            bool isAdmin = false;
            answered = false;
            _remote.Play(made.Id, (s, admin) => { served = s; isAdmin = admin; answered = true; });
            yield return WaitUntil(() => answered, "the server to hand the character over");
            Assert.IsNotNull(served);
            Assert.IsTrue(isAdmin, "everyone is an admin while the admin list says so");

            // Playing: some XP and a shirt put on. The server takes it.
            served.Experience = 60;
            served.Wearing = new List<string> { "t_shirt" };
            _remote.Save(served, adminEdited: false);
            yield return WaitUntil(() => LocalServer.Characters.List(PlayerKey.Local).Exists(c => c.Id == made.Id && c.Experience == 60),
                "the save to reach the server's storage");
            CollectionAssert.AreEqual(new[] { "t_shirt" }, LocalServer.Characters.Play(PlayerKey.Local, made.Id).Wearing);

            // Making it up: level 50 from nowhere, over the network. The server keeps level 1 (step 2), but takes the new look.
            Assert.IsNotNull(CharacterService.Rules, "the server's progress rules were not installed as the game loaded");
            served.Level = 50;
            served.HairStyle = "hyde";
            _remote.Save(served, adminEdited: false);
            yield return WaitUntil(() => LocalServer.Characters.Play(PlayerKey.Local, made.Id).HairStyle == "hyde", "the second save to arrive");
            Assert.AreEqual(1, LocalServer.Characters.Play(PlayerKey.Local, made.Id).Level, "the server took a made-up level");

            bool deleted = false;
            answered = false;
            _remote.Delete(made.Id, ok => { deleted = ok; answered = true; });
            yield return WaitUntil(() => answered, "the server to delete the character");
            Assert.IsTrue(deleted);
            Assert.IsEmpty(LocalServer.Characters.List(PlayerKey.Local));
        }
    }
}
