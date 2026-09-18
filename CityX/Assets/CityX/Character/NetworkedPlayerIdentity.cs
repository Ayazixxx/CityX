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
        private PlayerInputReader _inputReader;
        private CharacterIdentity _identity;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerInputReader>();
            _identity = GetComponent<CharacterIdentity>();

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
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsOwner)
            {
                if (_inputReader != null)
                {
                    _inputReader.enabled = false;
                }
                return;
            }

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
                }
                else
                {
                    Debug.LogWarning("CityX: no ThirdPersonCamera in the scene to retarget onto " +
                                     "this networked player -- they will have no camera.");
                }
            }

            CharacterSave character = PlaySession.Character;
            if (character == null)
            {
                return;
            }

            byte[] paint = character.GetPaint() ?? System.Array.Empty<byte>();
            SendAppearanceToServer(character.Name, character.Skin, character.HairStyle, paint);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendAppearanceToServer(string characterName, Color skin, string hairStyle, byte[] paint)
        {
            BroadcastAppearance(characterName, skin, hairStyle, paint);
        }

        [ObserversRpc(BufferLast = true)]
        private void BroadcastAppearance(string characterName, Color skin, string hairStyle, byte[] paint)
        {
            var hair = GetComponentInChildren<CharacterHair>(includeInactive: true);
            if (hair != null)
            {
                hair.SetStyle(hairStyle);
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
                    _identity.ApplyPaint(bodyPaint.Atlas);
                    return;
                }
            }
            _identity.Recolour(skin);
        }
    }
}
