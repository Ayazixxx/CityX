using CityX.Core;
using FishNet;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What makes a networked Player actually behave like a player instead
    /// of a phantom copy: the owner's own local input keeps driving their
    /// own body (PlayerInputReader stays enabled, exactly as it always has),
    /// while every OTHER copy of this same prefab -- everyone else's screen's
    /// view of that player -- goes quiet and waits to be told what to look
    /// like and how to hold still.
    ///
    /// PlayerInputReader.enabled = false ON A REMOTE COPY, NOTHING ELSE
    /// TOUCHED. PlayerLocomotion.Tick, and therefore the entire single-player
    /// movement simulation (jetpack, ragdoll, climbing, wall-stick, every
    /// Animator write) only ever runs from PlayerInputReader's own Update --
    /// confirmed directly, it is the sole caller. Disabling that one
    /// MonoBehaviour is a complete, zero-touch authority gate: a remote copy
    /// simply never runs any of that code, the same way Godot's own player.gd
    /// guards each of ~30 call sites with `if is_multiplayer_authority()`,
    /// just achieved here by turning the single driver off instead of adding
    /// guards throughout a system this port never had to write with
    /// networking in mind. Position/rotation for a remote copy comes from
    /// FishNet's own NetworkTransform (added alongside this component, owner-
    /// authoritative by default) instead; animation state comes from
    /// NetworkedAnimatorSync, added the same way.
    ///
    /// APPEARANCE (skin colour, painted atlas, hair) BROADCASTS ONCE, ON
    /// START, THE SAME WAY main.gd's OWN _rpc_announce_player DOES --
    /// carrying whatever PlaySession.Character already gave this owner's own
    /// local body (see CharacterIdentity.Awake, which every player, owned or
    /// not, already ran before this point). ObserversRpc's own BufferLast
    /// replays the most recent broadcast to a LATE joiner automatically --
    /// no hand-rolled catch-up dictionary needed the way Godot's own
    /// _on_peer_connected replay loop is.
    ///
    /// DELIBERATELY NOT CharacterIdentity.Apply: that method also resets
    /// level/experience/stat points and refills vitals, which is each
    /// peer's own business locally, not something a skin broadcast should
    /// touch. This applies exactly the appearance slice -- hair, paint,
    /// skin colour -- the same scope Godot's own atlas_png/hair_style
    /// broadcast had.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedPlayerIdentity : NetworkBehaviour
    {
        /// <summary>
        /// How far away another player is still worth seeing, in metres.
        /// Requested: "lets say there was 1000 players in the same areas
        /// all wearing outfits do you think the game would be able to
        /// handle that" -- answered honestly as no at the time: every
        /// player observed every other player regardless of distance
        /// (FishNet's own NetworkObserver had no condition attached at
        /// all), so a client's traffic -- and every other player's actually
        /// INSTANTIATED body, Animator and skinned garments -- grew with
        /// TOTAL player count in the area rather than with how many were
        /// actually near you. See NetworkedPlayerBuilder.AddViewDistance
        /// Observer for where this gets wired onto a real NetworkObserver +
        /// DistanceCondition, the exact mechanism CityX.Spike's own
        /// InterestManagementPlayTests/InterestManagementPlayerToPlayer
        /// PlayTests already prove works for both NPC-shaped and
        /// player-shaped entities.
        ///
        /// PICKED AGAINST TWO REAL NUMBERS ALREADY IN THIS PROJECT, not
        /// guessed: comfortably past PistolWeapon.Range (60m, so a firefight
        /// never has one side vanish from the other's observed set mid-
        /// fight) and comfortably short of the city scene's own
        /// fogEndDistance (380m, see CityMassingSceneBuilder), so culling
        /// still meaningfully reduces the observed set rather than covering
        /// the whole visible skyline. The Spike sandbox's own
        /// SpikeWorld.ObserverRadius (40m) is deliberately different and
        /// left alone -- it is tuned to be a large fraction of the SPIKE's
        /// own small 100m test field for a strong, easily-measured effect,
        /// not a production distance.
        /// </summary>
        public const float ViewRadius = 100.0f;

        private PlayerInputReader _inputReader;
        private CharacterIdentity _identity;
        private PlayerAudio _audio;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerInputReader>();
            _identity = GetComponent<CharacterIdentity>();
            _audio = GetComponent<PlayerAudio>();

            // NetworkObject.Start() (TryStartDeactivation) deactivates any
            // scene-placed NetworkObject outright whenever no FishNet
            // server or client is actually running -- correct for a pure
            // multiplayer game, but this exact GameObject IS the baked
            // scene player and doubles as the whole SINGLEPLAYER body, which
            // has no session at all (see CityMassingSceneBuilder's own "the
            // baked player above works exactly as it always has when
            // nobody hosted or joined" comment -- FishNet's own default
            // behaviour silently broke that promise until now). SetIsNetworked
            // must be called before Start; Awake is the only place every
            // component's own callback is guaranteed to run first, on both
            // the baked player and this project's spawned NetworkedPlayer
            // clones -- for the latter, a session is definitionally already
            // active (nothing spawns a clone without one), so this is a
            // no-op there and only ever changes behaviour for the baked,
            // no-session case.
            NetworkManager manager = InstanceFinder.NetworkManager;
            bool sessionActive = manager != null && (manager.IsServerStarted || manager.IsClientStarted);
            if (!sessionActive)
            {
                GetComponent<NetworkObject>().SetIsNetworked(false);
            }
            else
            {
                // Silent and deaf until known to be ours: a host holds every player's body, and one beyond the view radius never gets OnStartClient to be told.
                // Left on, the host's keys walked a far friend's body and cast its spells.
                if (_audio != null)
                {
                    _audio.SetOwnBody(false);
                }
                if (_inputReader != null)
                {
                    _inputReader.enabled = false;
                }
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            TellAudioWhoseBodyThisIs();
            TakeTheControlsIfOurs();

            // THE SKILL MENU AND THE BAR FOLLOW THE BODY THIS MACHINE CONTROLS:
            // somebody else's body gives the claim up, our own takes it.
            PlayerSkills skills = GetComponent<PlayerSkills>();
            if (!IsOwner)
            {
                if (skills != null && PlayerSkills.Local == skills)
                {
                    PlayerSkills.Local = null;
                }
                return;
            }
            PlayerSkills.Local = skills != null ? skills : PlayerSkills.On(gameObject);

            // A dynamically-spawned clone (a joining peer's own body -- see
            // NetworkedPlayerSpawner) has no camera of its own: the scene's
            // one ThirdPersonCamera was built at edit time pointed at the
            // BAKED scene player, which on THIS client represents whoever
            // already owns it (the host), not us. Retarget it at our own
            // body instead. The scene-placed object itself (IsSceneObject
            // true) skips this entirely -- it already has its camera from
            // the original bake, and retargeting it here would just point
            // the same camera at the same transform a second time.
            if (!NetworkObject.IsSceneObject)
            {
                var camera = Object.FindAnyObjectByType<ThirdPersonCamera>();
                if (camera != null)
                {
                    camera.Target = transform;
                    camera.Ragdoll = GetComponent<Ragdoll>();
                    var swing = GetComponent<GrappleSwing>();
                    if (swing != null)
                    {
                        swing.OrbitCamera = camera;   // the swing pulls the camera back, as it does for the host
                    }
                }
                else
                {
                    Debug.LogWarning("CityX: no ThirdPersonCamera in the scene to retarget onto " +
                                     "this networked player -- they will have no camera.");
                }
                LocalPlayerBody.Claim(gameObject);
            }

            CharacterSave character = PlaySession.Character;
            if (character == null)
            {
                return;
            }

            byte[] paint = character.GetPaint() ?? System.Array.Empty<byte>();
            SendAppearanceToServer(character.Name, character.Skin, character.HairStyle, character.HairColour, paint);
        }

        public override void OnOwnershipClient(FishNet.Connection.NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            TellAudioWhoseBodyThisIs();
            TakeTheControlsIfOurs();
        }

        private void TakeTheControlsIfOurs()
        {
            if (_inputReader != null)
            {
                _inputReader.enabled = IsOwner;
            }
        }

        private void TellAudioWhoseBodyThisIs()
        {
            if (_audio != null)
            {
                _audio.SetOwnBody(IsOwner);
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendAppearanceToServer(string characterName, Color skin, string hairStyle, Color hairColour, byte[] paint)
        {
            BroadcastAppearance(characterName, skin, hairStyle, hairColour, paint);
        }

        [ObserversRpc(BufferLast = true)]
        private void BroadcastAppearance(string characterName, Color skin, string hairStyle, Color hairColour, byte[] paint)
        {
            var hair = GetComponentInChildren<CharacterHair>(includeInactive: true);
            if (hair != null)
            {
                hair.SetStyle(hairStyle);
                hair.SetColour(hairColour);
            }

            if (_identity == null)
            {
                _identity = GetComponent<CharacterIdentity>();
            }
            if (_identity == null)
            {
                return;
            }

            if (paint != null && paint.Length > 0)
            {
                var bodyPaint = new BodyPaint(skin);
                if (bodyPaint.Decode(paint))
                {
                    bodyPaint.ShrinkForWearing();
                    _identity.ApplyPaint(bodyPaint.Atlas);
                    return;
                }
            }
            _identity.Recolour(skin);
        }
    }
}
