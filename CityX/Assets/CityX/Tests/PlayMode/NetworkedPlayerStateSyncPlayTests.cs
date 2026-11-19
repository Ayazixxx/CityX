using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// What one player does or has must show on ANOTHER player's copy of their body, over real sockets: a dedicated server and two real clients, each with
    /// its own body. Player A's body as client 0 holds it (the owner copy), and player B's body as client 0 holds it (a copy), and the reverse.
    ///
    /// Covered: the climbing pose and the hands on a ledge, the mantle's pull-up, held items and back items (and their removal), hairstyle and colour,
    /// and damage dealt to a COPY landing on the real body's owner (hits, poison, health shown back).
    /// </summary>
    public sealed partial class NetworkedPlayerStateSyncPlayTests
    {
        private const string NetworkedPlayerPrefabPath = "Assets/CityX/Character/NetworkedPlayer.prefab";
        private static ushort _nextPort = 44700;
        private const float TimeoutSeconds = 30.0f;

        private GameObject _ground;
        private GameObject _serverGo;
        private NetworkManager _server;
        private readonly List<GameObject> _clientGos = new List<GameObject>();
        private readonly List<NetworkManager> _clients = new List<NetworkManager>();

        // The two bodies as each client sees them.
        private NetworkObject _aOnA, _aOnB, _bOnA, _bOnB;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = true;
            PlaySession.Clear();
            foreach (NetworkManager client in _clients)
            {
                if (client != null && client.ClientManager.Started)
                {
                    client.ClientManager.StopConnection();
                }
            }
            if (_server != null && _server.ServerManager.Started)
            {
                _server.ServerManager.StopConnection(true);
            }
            yield return null;
#if UNITY_EDITOR
            TearDownEnemies();
#endif
            foreach (NetworkedPlayerIdentity identity in Object.FindObjectsByType<NetworkedPlayerIdentity>(FindObjectsSortMode.None))
            {
                if (identity != null)
                {
                    Object.DestroyImmediate(identity.gameObject);
                }
            }
            foreach (GameObject go in _clientGos)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _clientGos.Clear();
            _clients.Clear();
            if (_serverGo != null) Object.DestroyImmediate(_serverGo);
            if (_ground != null) Object.Destroy(_ground);
            // What a test's dismemberment threw about (chunks, cut-off parts) would lie in the next test's way (docs/DISMEMBERMENT.md).
            foreach (Debris debris in Object.FindObjectsByType<Debris>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(debris.gameObject);
            }
            LogAssert.ignoreFailingMessages = false;
        }

#if UNITY_EDITOR
        /// <summary>Starts a server and two clients, spawns one body for each, and finds each body as each client holds it.</summary>
        private IEnumerator StartTwoPlayers()
        {
            LogAssert.ignoreFailingMessages = true;
            GameObject managerPrefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(managerPrefab, "missing SpikeNetworkManager prefab -- run CityX/Spike/Rebuild Spike Assets");
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPrefabPath);
            Assert.IsNotNull(playerPrefab, "no NetworkedPlayer.prefab -- run CityX/Rebuild Massing Scene first");

            ushort port = _nextPort++;
            _serverGo = Object.Instantiate(managerPrefab);
            _serverGo.name = "State Server";
            _server = _serverGo.GetComponent<NetworkManager>();
            _server.TimeManager.SetTickRate(CityX.Net.NetworkBootstrap.TicksPerSecond);   // as a session of the game runs
            Transport serverTransport = _server.TransportManager.Transport;
            serverTransport.SetPort(port);
            serverTransport.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            _server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => _server.IsServerStarted, "the server to start");

            for (int i = 0; i < 2; i++)
            {
                GameObject go = Object.Instantiate(managerPrefab);
                go.name = $"State Client {i}";
                NetworkManager client = go.GetComponent<NetworkManager>();
                client.TimeManager.SetTickRate(CityX.Net.NetworkBootstrap.TicksPerSecond);
                _clientGos.Add(go);
                _clients.Add(client);
                Transport t = client.TransportManager.Transport;
                t.SetPort(port);
                t.SetClientAddress("127.0.0.1");
                client.ClientManager.StartConnection("127.0.0.1", port);
                yield return null;
            }
            yield return WaitUntil(() => _server.ServerManager.Clients.Count >= 2, "both clients to connect");

            var ids = new List<int>(_server.ServerManager.Clients.Keys);
            ids.Sort();
            for (int i = 0; i < 2; i++)
            {
                NetworkConnection connection = _server.ServerManager.Clients[ids[i]];
                NetworkObject body = _server.GetPooledInstantiated(playerPrefab, new Vector3(i * 2.0f, 0.5f, 0.0f), Quaternion.identity, asServer: true);
                _server.ServerManager.Spawn(body, connection);
            }
            yield return WaitUntil(
                () => _clients[0].ClientManager.Objects.Spawned.Count >= 2 && _clients[1].ClientManager.Objects.Spawned.Count >= 2,
                "both players to appear on both clients");

            foreach (NetworkObject nob in _clients[0].ClientManager.Objects.Spawned.Values)
            {
                if (nob.IsOwner) _aOnA = nob; else _bOnA = nob;
            }
            foreach (NetworkObject nob in _clients[1].ClientManager.Objects.Spawned.Values)
            {
                if (nob.IsOwner) _bOnB = nob; else _aOnB = nob;
            }
            Assert.IsNotNull(_aOnA);
            Assert.IsNotNull(_bOnA);
            Assert.IsNotNull(_aOnB);
            Assert.IsNotNull(_bOnB);
            Assert.AreEqual(_aOnA.ObjectId, _aOnB.ObjectId, "the two clients disagree about which body is player A's");
            Assert.AreEqual(_bOnA.ObjectId, _bOnB.ObjectId);

            // A body takes the keyboard only on the machine that plays it, and that machine's screens follow it.
            Assert.IsTrue(_aOnA.GetComponent<PlayerInputReader>().enabled, "player A's own body does not obey player A");
            Assert.IsFalse(_aOnB.GetComponent<PlayerInputReader>().enabled, "player B's copy of player A obeys player B's keyboard");
            Assert.IsTrue(LocalPlayerBody.Current == _aOnA.gameObject || LocalPlayerBody.Current == _bOnB.gameObject, "a joining player's body did not claim this machine's screens");

            // EVERY MACHINE OF THIS TEST SHARES ONE SCENE: the server's copy of each body and the other client's copy stand inside the real one.
            // A's real body walking into a ledge was shoved by its own copies' capsules -- now and then off its feet, which the copy then showed
            // as a fall (AStepUpOnOnePlayersBodyShowsOnTheCopyAsAStrideNotAFall, flaky in full runs). In a game each machine has its own scene;
            // here the copies, which only show and relay, are made untouchable (the hit tests strike them directly, not by colliders).
            foreach (NetworkObject nob in Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
            {
                if (nob == null || nob.GetComponent<NetworkedPlayerIdentity>() == null || nob == _aOnA || nob == _bOnB)
                {
                    continue;
                }
                foreach (Collider c in nob.GetComponentsInChildren<Collider>(true))
                {
                    c.enabled = false;
                }
            }
        }
#endif

        // --- climbing ---

        [UnityTest]
        public IEnumerator TheClimbingPoseAndTheHandsOnALedgeShowOnAnotherPlayersCopy()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var ownerLocomotion = _aOnA.GetComponent<PlayerLocomotion>();
            var ownerAnimator = _aOnA.GetComponentInChildren<Animator>();
            var copyLocomotion = _aOnB.GetComponent<PlayerLocomotion>();
            var copyAnimator = _aOnB.GetComponentInChildren<Animator>();

            Vector3 plane = new Vector3(3.0f, 10.0f, 4.0f);
            Vector3 normal = Vector3.back;
            Vector3 lip = new Vector3(3.2f, 12.0f, 4.0f);

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            bool seen = false;
            while (Time.realtimeSinceStartup < deadline && !seen)
            {
                // Held every frame, as the real climb does: the owner's Update may rewrite its animator.
                ownerAnimator.SetBool("Climbing", true);
                ownerAnimator.SetInteger("ClimbDirection", 1);
                ownerLocomotion.SetClimbWall(plane, normal, 1.0f);
                ownerLocomotion.SetLedgeHands(0.8f, lip, normal);
                seen = copyAnimator.GetBool("Climbing") && copyAnimator.GetInteger("ClimbDirection") == 1
                       && copyLocomotion.ClimbWallWeight > 0.9f && copyLocomotion.LedgeHandsWeight > 0.7f;
                yield return null;
            }
            Assert.IsTrue(seen, "the climbing pose never reached another player's copy");
            Assert.Less(Vector3.Distance(copyLocomotion.ClimbWallPoint, plane), 0.01f, "the wall plane arrived wrong");
            Assert.Less(Vector3.Distance(copyLocomotion.ClimbWallNormal, normal), 0.01f);
            Assert.Less(Vector3.Distance(copyLocomotion.LedgeLip, lip), 0.01f, "the ledge the hands reach for arrived wrong");

            // Letting go lets go on the copy too.
            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            bool released = false;
            while (Time.realtimeSinceStartup < deadline && !released)
            {
                ownerAnimator.SetBool("Climbing", false);
                ownerLocomotion.SetClimbWall(Vector3.zero, Vector3.forward, 0.0f);
                ownerLocomotion.SetLedgeHands(0.0f, Vector3.zero, Vector3.zero);
                released = !copyAnimator.GetBool("Climbing") && copyLocomotion.ClimbWallWeight < 0.05f && copyLocomotion.LedgeHandsWeight < 0.05f;
                yield return null;
            }
            Assert.IsTrue(released, "the copy kept climbing after the owner let go");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AMantleOnOnePlayersBodyPlaysThePullUpOnAnothersCopy()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var climbing = _aOnA.GetComponent<PlayerClimbing>();
            Animator copyAnimator = _aOnB.GetComponentInChildren<Animator>();

            // The event BeginMantle raises (it is private to the climb); the copy must play ClimbTopOut.
            System.Reflection.FieldInfo field = typeof(PlayerClimbing).GetField("MantleStarted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "PlayerClimbing no longer raises MantleStarted");
            var raise = field.GetValue(climbing) as System.Action;
            Assert.IsNotNull(raise, "nothing is listening for the mantle: it would not be sent");
            raise.Invoke();
            yield return WaitForState(copyAnimator, "ClimbTopOut");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        // --- stepping up ---

        /// <summary>
        /// A STEP UP IS A STRIDE ON THE COPY TOO (PlayerLocomotion.StepUp.cs, 2026-10-15): one player's body steps onto a 0.55 m ledge; the other
        /// player's copy of it rises onto the ledge (the transform), and its Animator is never told the body left the ground (Grounded, sent with the
        /// other parameters): a copy would otherwise play a fall for the few frames of the rise.
        /// </summary>
        [UnityTest]
        public IEnumerator AStepUpOnOnePlayersBodyShowsOnTheCopyAsAStrideNotAFall()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            GameObject ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name = "Ledge";
            ledge.transform.position = new Vector3(0.0f, 0.275f, 7.0f);   // its face at z = 3, in front of player A
            ledge.transform.localScale = new Vector3(1.6f, 0.55f, 8.0f);
            Physics.SyncTransforms();
            try
            {
                _aOnA.GetComponent<PlayerInputReader>().enabled = false;   // driven by the test, not the (empty) keyboard
                var owner = _aOnA.GetComponent<PlayerLocomotion>();
                Animator copyAnimator = _aOnB.GetComponentInChildren<Animator>();
                for (int i = 0; i < 40; i++)
                {
                    owner.Tick(new LocomotionInput { YawDegrees = 0.0f }, 1.0f / 60.0f);
                    yield return null;
                }
                yield return WaitUntil(() => copyAnimator.GetBool("Grounded"), "the copy to stand on the ground");

                bool steppedUp = false, copyLeftTheGround = false, copyUp = false;
                float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline && !copyUp)
                {
                    owner.Tick(new LocomotionInput { Move = Vector2.up, YawDegrees = 0.0f }, 1.0f / 60.0f);
                    steppedUp |= owner.IsSteppingUp;
                    copyLeftTheGround |= steppedUp && !copyAnimator.GetBool("Grounded");
                    copyUp = steppedUp && _aOnB.transform.position.y > 0.5f;
                    yield return null;
                }
                Assert.IsTrue(steppedUp, "the owner's body never stepped up onto the 0.55 m ledge");
                Assert.IsTrue(copyUp, $"the copy never rose onto the ledge: it is at {_aOnB.transform.position}");
                Assert.IsFalse(copyLeftTheGround, "the copy's Animator was told the body left the ground: other players would see a fall");
            }
            finally
            {
                Object.Destroy(ledge);
            }
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        // --- /pushups ---

        /// <summary>/pushups on one player's body are seen on the other's copy: down, then push-ups on repeat (the Animator's PushUpPhase, sent with the other parameters).</summary>
        [UnityTest]
        public IEnumerator PushUpsOnOnePlayersBodyShowOnAnothersCopy()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            // The body is ticked here with the emote's phases, as PlayerInputReader ticks it (these tests have no keyboard for the reader).
            _aOnA.GetComponent<PlayerInputReader>().enabled = false;
            var owner = _aOnA.GetComponent<PlayerLocomotion>();
            Animator copyAnimator = _aOnB.GetComponentInChildren<Animator>();
            IEnumerator Hold(int phase, System.Func<bool> until, string what)
            {
                float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
                while (!until())
                {
                    Assert.Less(Time.realtimeSinceStartup, deadline, $"timed out waiting for {what}");
                    owner.Tick(new LocomotionInput { YawDegrees = 0.0f, PushUpPhase = phase }, 1.0f / 60.0f);
                    yield return null;
                }
            }
            yield return Hold(0, () => owner.IsGrounded, "player A to stand on the ground");
            yield return Hold(1, () => copyAnimator.GetCurrentAnimatorStateInfo(0).IsName("PushUpEnter"), "the copy to go down");
            yield return Hold(2, () => copyAnimator.GetCurrentAnimatorStateInfo(0).IsName("PushUpLoop"), "the copy to do push-ups");
            yield return Hold(3, () => copyAnimator.GetCurrentAnimatorStateInfo(0).IsName("PushUpExit"), "the copy to get up");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        // --- held items ---

        [UnityTest]
        public IEnumerator AHeldSwordAndABackJetpackShowOnAnotherPlayersCopyAndGoWhenPutAway()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var inventory = _aOnA.GetComponent<Inventory>();
            Assert.IsNotNull(inventory, "a joining player's body has no inventory to hold anything in");
            var copySync = _aOnB.GetComponent<NetworkedEquipmentSync>();
            Assert.IsNotNull(copySync);
            Animator copyAnimator = _aOnB.GetComponentInChildren<Animator>();

            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.SpikeSword, _aOnA.gameObject), "the sword could not be equipped on player A");
            yield return WaitUntil(() => copySync.ShownItem(ItemRegistry.SpikeSword) != null, "the sword to appear on player A's copy");

            GameObject model = copySync.ShownItem(ItemRegistry.SpikeSword);
            Assert.AreEqual(copyAnimator.GetBoneTransform(HumanBodyBones.RightHand), model.transform.parent, "the sword is not in the copy's right hand");
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            {
                Assert.IsFalse(c.enabled, "a copy's sword can be hit or picked up");
            }
            foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Assert.IsFalse(b.enabled, $"{b.GetType().Name} is running on a copy's sword");
            }

            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.Jetpack, _aOnA.gameObject), "the jetpack could not be equipped on player A");
            yield return WaitUntil(() => copySync.ShownItem(ItemRegistry.Jetpack) != null, "the jetpack to appear on player A's copy");
            Assert.AreEqual(copyAnimator.GetBoneTransform(HumanBodyBones.Spine), copySync.ShownItem(ItemRegistry.Jetpack).transform.parent, "the jetpack is not on the copy's back");
            Assert.IsNotNull(copySync.ShownItem(ItemRegistry.SpikeSword), "equipping the jetpack took the sword off the copy");

            // Putting the sword away takes it out of the copy's hand.
            Assert.IsTrue(inventory.UnequipToSlot(Inventory.PrimarySlot, _aOnA.gameObject));
            yield return WaitUntil(() => copySync.ShownItem(ItemRegistry.SpikeSword) == null, "the sword to leave player A's copy");
            Assert.IsNotNull(copySync.ShownItem(ItemRegistry.Jetpack), "putting the sword away took the jetpack off");

            // The owner's own body is not given a second, cosmetic sword.
            Assert.IsEmpty(new List<string>(_aOnA.GetComponent<NetworkedEquipmentSync>().ShownItemIds), "the owner's own body was shown a copy of its own items");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AWornKnightHelmetShowsOnAnotherPlayersCopyAndGoesWhenTakenOff()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var inventory = _aOnA.GetComponent<Inventory>();
            var copySync = _aOnB.GetComponent<NetworkedEquipmentSync>();
            Animator copyAnimator = _aOnB.GetComponentInChildren<Animator>();

            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.KnightHelmet, _aOnA.gameObject), "the knight helmet could not be equipped on player A");
            yield return WaitUntil(() => copySync.ShownItem(ItemRegistry.KnightHelmet) != null, "the helmet to appear on player A's copy");
            GameObject model = copySync.ShownItem(ItemRegistry.KnightHelmet);
            Assert.AreEqual(copyAnimator.GetBoneTransform(HumanBodyBones.Head), model.transform.parent, "the helmet is not on the copy's head");
            Assert.AreEqual(Vector3.zero, model.transform.localPosition, "the copy wears the helmet somewhere else than its owner does");
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            {
                Assert.IsFalse(c.enabled, "a copy's helmet can be picked up");
            }
            var copyHair = _aOnB.GetComponentInChildren<CharacterHair>(true);
            Assert.IsTrue(copyHair.IsCovered, "other players see player A's hair poking through the helmet");

            Assert.IsTrue(inventory.UnequipToSlot(EquipSlots.Helmet, _aOnA.gameObject));
            yield return WaitUntil(() => copySync.ShownItem(ItemRegistry.KnightHelmet) == null, "the helmet to leave player A's copy");
            yield return null;   // the copy's helmet model is destroyed at the end of the frame
            Assert.IsFalse(copyHair.IsCovered, "player A's copy stayed bald after the helmet came off");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator WornClothesAndAHelmetShowOnAnotherPlayersCopyAndGoWhenTakenOff()
        {
#if UNITY_EDITOR
            // 2026-10-22: clothes were seen only by their wearer; other players saw them bare.
            yield return StartTwoPlayers();
            var inventory = _aOnA.GetComponent<Inventory>();
            var copySync = _aOnB.GetComponent<NetworkedEquipmentSync>();
            var copySkin = TransformSearch.FindDeep(_aOnB.transform, BodySurface.SkinName).GetComponent<SkinnedMeshRenderer>();
            Mesh wholeBody = copySkin.sharedMesh;
            Transform copyHead = _aOnB.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);

            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.LongSleeveTShirt, _aOnA.gameObject), "the shirt could not be worn by player A");
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.KnightHelmet, _aOnA.gameObject), "the knight helmet could not be worn by player A");
            yield return WaitUntil(() => copySync.ShownItem(ItemRegistry.LongSleeveTShirt) != null && copySync.ShownItem(ItemRegistry.KnightHelmet) != null,
                "the shirt and the helmet to appear on player A's copy");

            Transform worn = TransformSearch.FindDeep(_aOnB.transform, "CombinedGarmentSkin");
            Assert.IsNotNull(worn, "the copy draws no clothes");
            var wornSkin = worn.GetComponent<SkinnedMeshRenderer>();
            Assert.IsTrue(wornSkin.enabled && wornSkin.sharedMesh != null && wornSkin.sharedMesh.vertexCount > 0, "the copy's clothes are empty");
            Assert.AreNotSame(wholeBody, copySkin.sharedMesh, "the copy's body is drawn whole under the shirt and pokes through it");
            Assert.AreEqual(copyHead, copySync.ShownItem(ItemRegistry.KnightHelmet).transform.parent, "the helmet is not on the copy's head");
            foreach (Collider c in copySync.ShownItem(ItemRegistry.LongSleeveTShirt).GetComponentsInChildren<Collider>(true))
            {
                Assert.IsFalse(c.enabled, "a copy's shirt can be picked up");
            }

            Assert.IsTrue(inventory.UnequipToSlot(EquipSlots.Torso, _aOnA.gameObject));
            Assert.IsTrue(inventory.UnequipToSlot(EquipSlots.Helmet, _aOnA.gameObject));
            yield return WaitUntil(() => copySync.ShownItem(ItemRegistry.LongSleeveTShirt) == null && copySync.ShownItem(ItemRegistry.KnightHelmet) == null,
                "the shirt and the helmet to leave player A's copy");
            Assert.AreSame(wholeBody, copySkin.sharedMesh, "the copy's body stayed cut after the shirt came off");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        // --- hair ---

        [UnityTest]
        public IEnumerator HairstyleAndColourShowOnAnotherPlayersCopy()
        {
#if UNITY_EDITOR
            var character = new CharacterSave { Name = "Tester", HairStyle = CharacterHair.SpikeX, HairColour = new Color(0.9f, 0.1f, 0.1f) };
            PlaySession.Begin(character, false);
            yield return StartTwoPlayers();

            var copyHair = _aOnB.GetComponentInChildren<CharacterHair>(true);
            Assert.IsNotNull(copyHair, "the copy has no hair component");
            yield return WaitUntil(() => copyHair.Style == CharacterHair.SpikeX, "the hairstyle to arrive on player A's copy");
            Assert.Less(Mathf.Abs(copyHair.Colour.r - 0.9f), 0.02f, "the hair colour did not arrive");
            Assert.Less(Mathf.Abs(copyHair.Colour.g - 0.1f), 0.02f);
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        // --- damage ---

        /// <summary>
        /// THE STATS ACROSS THE NETWORK (2026-10-14): a hit on another player's copy carries its kind and the attacker's armour penetration to
        /// that player's real body, whose own resistances answer it; their Max Health shows on the copy; and a blow their block stops outright
        /// throws its sparks on the copy too.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRealBodysStatsAnswerAHitThroughTheRelayAndItsDeflectionShowsOnTheCopy()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var realVitals = _bOnB.GetComponent<PlayerVitals>();
            var copyVitals = _bOnA.GetComponent<PlayerVitals>();
            realVitals.Stats[StatId.MagicResistance] = 20;   // 100: half of a spell stopped
            realVitals.Stats[StatId.MaxHealth] = 5;          // +50 health
            realVitals.Revive();
            yield return WaitUntil(() => Mathf.Approximately(copyVitals.MaxHealth, PlayerVitals.BaseMaxHealth + 50.0f),
                "B's Max Health to show on A's copy of B");

            float before = realVitals.Health;
            DamageContext.Strike(copyVitals, 20.0f, _aOnA.transform.position, 0.0f, DamageKind.Magic, 0.0f);
            yield return WaitUntil(() => realVitals.Health < before - 1.0f, "the spell to reach B's real body");
            Assert.AreEqual(before - 10.0f, realVitals.Health, 0.5f, "B's magic resistance should have stopped half of it");

            // Penetration travels with the hit: a whole penetration ignores the resistance.
            before = realVitals.Health;
            DamageContext.Strike(copyVitals, 20.0f, _aOnA.transform.position, 0.0f, DamageKind.Magic, 1.0f);
            yield return WaitUntil(() => realVitals.Health < before - 1.0f, "the second spell to reach B");
            Assert.AreEqual(before - 20.0f, realVitals.Health, 0.5f, "full penetration should ignore the resistance");

            // A block that stops the blow outright: the sparks show on A's copy of B as well as on B.
            var copyEffect = _bOnA.GetComponent<BlockDeflectEffect>();
            var realEffect = _bOnB.GetComponent<BlockDeflectEffect>();
            Assert.IsNotNull(copyEffect, "the copy has no deflect effect");
            realVitals.Roll = () => 0.0f;
            realVitals.StartBlock();
            before = realVitals.Health;
            DamageContext.Strike(copyVitals, 20.0f, _aOnA.transform.position, 0.2f, DamageKind.Bullet, 0.0f);
            yield return WaitUntil(() => realEffect.Shown > 0, "B's block to stop the bullet");
            Assert.AreEqual(before, realVitals.Health, 0.01f, "a deflected bullet does no damage");
            yield return WaitUntil(() => copyEffect.Shown > 0, "the deflection's sparks to show on A's copy of B");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        /// <summary>
        /// Armour stops at most half of a player's blow and two thirds of anything else's (docs/ARMOUR.md): the hit forwarded from A's machine
        /// says a player struck it, so B's real body takes it under the player cap.
        /// </summary>
        [UnityTest]
        public IEnumerator APlayersBlowThroughTheRelayIsCappedAsAPlayersBlow()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var realVitals = _bOnB.GetComponent<PlayerVitals>();
            var copyVitals = _bOnA.GetComponent<PlayerVitals>();
            realVitals.Stats[StatId.Armor] = 200;   // 1000 armour: 91% uncapped
            realVitals.Revive();

            float before = realVitals.Health;
            DamageContext.Strike(copyVitals, 20.0f, _aOnA.transform.position, 0.0f, DamageKind.Physical, 0.0f, null, _aOnA.gameObject);
            yield return WaitUntil(() => realVitals.Health < before - 1.0f, "player A's blow to reach B's real body");
            Assert.AreEqual(before - 20.0f * (1.0f - ArmourRules.PlayerBlowCap), realVitals.Health, 0.5f, "B's armour stopped more than a player's cap");

            before = realVitals.Health;
            DamageContext.Strike(copyVitals, 20.0f, _aOnA.transform.position, 0.0f, DamageKind.Physical, 0.0f, null, null);
            yield return WaitUntil(() => realVitals.Health < before - 1.0f, "a blow nobody struck to reach B's real body");
            Assert.AreEqual(before - 20.0f * (1.0f - ArmourRules.OtherBlowCap), realVitals.Health, 0.5f, "a blow no player struck was capped as a player's");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AHitOnAnotherPlayersCopyLandsOnThatPlayersRealBody()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var realVitals = _bOnB.GetComponent<PlayerVitals>();      // player B's real body, on B's own machine
            var copyVitals = _bOnA.GetComponent<PlayerVitals>();      // the copy of B that player A's machine sees and swings at
            float before = realVitals.Health;

            // Player A hits what is, on A's machine, only a copy.
            HitResponse response = ((IDamageable)copyVitals).ApplyHit(25.0f, _aOnA.transform.position, 0.2f);
            Assert.AreEqual(HitResponse.Landed, response, "the attacker was not told the hit landed");
            Assert.AreEqual(before, copyVitals.Health, 0.001f, "the copy took the damage itself instead of forwarding it");

            yield return WaitUntil(() => realVitals.Health < before - 1.0f, "the hit to reach player B's real body");
            Assert.AreEqual(before - 25.0f, realVitals.Health, 6.0f, "the real body took a very different amount of damage than was dealt");

            // And A's copy of B then shows B's real health.
            yield return WaitUntil(() => copyVitals.Health < before - 1.0f, "B's real health to be shown on A's copy of B");

            // Poison is forwarded the same way.
            float afterHit = realVitals.Health;
            copyVitals.TakePoison(10.0f);
            yield return WaitUntil(() => realVitals.Health < afterHit - 1.0f, "the poison to reach player B's real body");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        /// <summary>A guard-breaking blow (the held-click heavy) struck at another player's copy breaks that player's real guard on their own machine.</summary>
        [UnityTest]
        public IEnumerator AGuardBreakingHitOnACopyBreaksThatPlayersRealGuard()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var realVitals = _bOnB.GetComponent<PlayerVitals>();
            var copyVitals = _bOnA.GetComponent<PlayerVitals>();
            realVitals.Roll = () => 1.0f;   // no outright blocks (Block Chance)
            bool broken = false;
            realVitals.GuardBroken += () => broken = true;
            realVitals.StartBlock();
            Assert.IsTrue(realVitals.IsBlocking);

            using (DamageContext.GuardBreaking(true))
            {
                ((IDamageable)copyVitals).ApplyHit(20.0f, _aOnA.transform.position, 0.4f);
            }
            yield return WaitUntil(() => broken, "the guard-breaking hit to break player B's real guard");
            Assert.IsTrue(realVitals.IsStunned, "player B's broken guard does not leave them staggered");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        /// <summary>A finisher struck at another player's copy lands as a heavy blow on their own machine too.</summary>
        [UnityTest]
        public IEnumerator AHeavyBlowOnACopyLandsHeavilyOnThatPlayersRealBody()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var realVitals = _bOnB.GetComponent<PlayerVitals>();
            var copyVitals = _bOnA.GetComponent<PlayerVitals>();
            realVitals.Roll = () => 1.0f;
            bool wounded = false, heavily = false;
            realVitals.Wounded += (from, damage, stun) =>
            {
                wounded = true;
                heavily = DamageContext.HeavyBlow;
            };

            using (DamageContext.LandingHeavily(true))
            {
                ((IDamageable)copyVitals).ApplyHit(20.0f, _aOnA.transform.position, 0.5f);
            }
            yield return WaitUntil(() => wounded, "the heavy blow to reach player B's real body");
            Assert.IsTrue(heavily, "the blow arrived on player B's machine as a light one");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator YouCannotHurtYourselfThroughTheRelayAndForgedHitsAreRefused()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var ownVitals = _aOnA.GetComponent<PlayerVitals>();
            float before = ownVitals.Health;

            // A hit on your OWN body is applied normally (not relayed): one hit, once.
            ((IDamageable)ownVitals).ApplyHit(10.0f, Vector3.zero, 0.0f);
            Assert.AreEqual(before - 10.0f, ownVitals.Health, 0.01f, "a hit on one's own body was not applied exactly once");

            // The numbers the server will not forward.
            Assert.IsFalse(NetworkedDamage.Acceptable(float.NaN));
            Assert.IsFalse(NetworkedDamage.Acceptable(float.PositiveInfinity));
            Assert.IsFalse(NetworkedDamage.Acceptable(-5.0f));
            Assert.IsFalse(NetworkedDamage.Acceptable(0.0f));
            Assert.IsFalse(NetworkedDamage.Acceptable(1.0e9f));
            Assert.IsTrue(NetworkedDamage.Acceptable(25.0f));
            yield break;
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        /// <summary>
        /// A CROUCH SHOWS ON THE COPY (2026-10-27, docs/CROUCH.md): one player crouches; the other player's copy of them goes down too, its
        /// controller (what a shot at it hits) as short as the owner's, and stands back up with them.
        /// </summary>
        [UnityTest]
        public IEnumerator ACrouchShowsOnAnotherPlayersCopyAndShrinksItsHitbox()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            _aOnA.GetComponent<PlayerInputReader>().enabled = false;   // driven by the test, not the (empty) keyboard
            var owner = _aOnA.GetComponent<PlayerLocomotion>();
            var copy = _aOnB.GetComponent<PlayerLocomotion>();
            var copyController = _aOnB.GetComponent<CharacterController>();
            yield return WaitUntil(() =>
            {
                owner.Tick(new LocomotionInput { Crouch = true }, 1.0f / 60.0f);
                return copy.IsDucked && Mathf.Abs(copyController.height - copy.CrouchedHeight) < 1e-3f;
            }, "the copy to crouch with its owner");
            Assert.Greater(copy.CrouchShown, 0.99f, "the copy's crouch pose is not all the way down");
            yield return WaitUntil(() =>
            {
                owner.Tick(new LocomotionInput { Crouch = false }, 1.0f / 60.0f);
                return !copy.IsDucked && Mathf.Abs(copyController.height - copy.StandingHeight) < 1e-3f;
            }, "the copy to stand back up with its owner");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        /// <summary>
        /// A TURNED-ASIDE BULLET'S STREAK SHOWS ON THE COPY (2026-10-27, docs/BULLET_DEFLECTION.md): one player's guard turns a bullet aside
        /// and it flies off; the other player sees it fly off from that player's copy.
        /// </summary>
        [UnityTest]
        public IEnumerator ABulletAPlayersGuardTurnsAsideIsSeenFlyingOffOnTheirCopy()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var vitals = _aOnA.GetComponent<PlayerVitals>();
            var copy = _aOnB.GetComponent<CityX.Weapons.BulletRicochet>();
            Assert.IsNotNull(copy, "the joining player's body cannot send a bullet flying (PlayerAbilityWiring)");
            vitals.Roll = () => 0.0f;
            yield return WaitUntil(() =>
            {
                vitals.StartBlock();
                DamageContext.Strike(vitals, 10.0f, _aOnA.transform.position + new Vector3(0.0f, 2.2f, 12.0f), 0.0f, DamageKind.Bullet, 0.0f);
                return copy.RemoteShown > 0;
            }, "the turned-aside bullet's streak to show on the other player's copy");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        /// <summary>
        /// A SHIELD SHOWS ON THE COPY, AND GOES UP WITH ITS OWNER'S (2026-10-27, docs/SHIELD.md): one player straps on the round shield; the
        /// other player's copy of them wears it on its forearm, and raises it when they do.
        /// </summary>
        [UnityTest]
        public IEnumerator AShieldShowsOnAnotherPlayersCopyAndIsRaisedWithItsOwners([Values(ItemRegistry.RoundShield, ItemRegistry.RiotShield)] string shieldId)
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            _aOnA.GetComponent<PlayerInputReader>().enabled = false;   // driven by the test, not the (empty) keyboard
            var owner = _aOnA.GetComponent<PlayerLocomotion>();
            var copy = _aOnB.GetComponent<PlayerLocomotion>();
            Assert.IsTrue(_aOnA.GetComponent<Inventory>().EquipItemId(shieldId, _aOnA.gameObject), "the shield would not go on");
            yield return WaitUntil(() => copy.Shield != null, "the shield to show on the other player's copy");
            yield return WaitUntil(() =>
            {
                owner.Tick(new LocomotionInput { ShieldRaised = true }, 1.0f / 60.0f);
                return copy.ShieldRaise > 0.99f;
            }, "the copy to raise its shield with its owner");
            Assert.Less(Vector3.Angle(copy.Shield.forward, _aOnB.transform.forward), 5.0f, "the copy's raised shield does not face forward");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        // --- helpers ---

        private static IEnumerator WaitForState(Animator animator, string stateName)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (true)
            {
                if (animator.GetCurrentAnimatorStateInfo(0).IsName(stateName) || animator.GetNextAnimatorStateInfo(0).IsName(stateName))
                {
                    yield break;
                }
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"the animator never entered '{stateName}' within {TimeoutSeconds:0.#}s");
                }
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"timed out after {TimeoutSeconds:0.#}s waiting for {what}");
                }
                yield return null;
            }
        }
    }
}
