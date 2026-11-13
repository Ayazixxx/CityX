using CityX.Core;
using CityX.Net;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The title screen's own Host/Join panel -- ported from title_screen.gd's
    /// identical panel, direct-IP only, no lobby/matchmaking (that stays
    /// future work on both sides, see net/network.gd's own class comment).
    ///
    /// HOST HAS A SYNCHRONOUS ANSWER, JOIN DOES NOT -- same asymmetry
    /// title_screen.gd's own comments call out. NetworkBootstrap.Begin's
    /// StartConnection calls return whether the socket actually came up
    /// (a bad host port fails immediately), so Host can react right away.
    /// A CLIENT connection is inherently asynchronous -- it has to actually
    /// reach the other machine -- so Join instead subscribes to the
    /// NetworkManager's own ClientManager.OnClientConnectionState and reacts
    /// once a real Started/Stopped arrives, exactly mirroring Net's own
    /// connected_to_server/connection_failed signals.
    /// </summary>
    public sealed class TitleMultiplayerView : MonoBehaviour
    {
        public InputField HostPortField;
        public InputField JoinAddressField;
        public InputField JoinPortField;
        public Text StatusLabel;

        public NetworkBootstrap Bootstrap;
        public TitleScreen Title;

        private bool _joinPending;

        /// <summary>Opened: this computer's addresses, for a friend to join (HostAddresses).</summary>
        private void OnEnable()
        {
            SetStatus(HostAddresses.Describe(HostAddresses.Find()));
        }

        private void OnDisable()
        {
            // The panel is hidden the moment Back is pressed, or the moment
            // a successful Join hands off to CharacterSelect -- either way
            // a listener left dangling here would fire this component's own
            // callback after it has nothing left to do (or, worse, after a
            // LATER join attempt already replaced it).
            Unsubscribe();
        }

        /// <summary>
        /// Straight to hosting on the given port, no separate confirmation
        /// step -- StartConnection fails synchronously (e.g. the port is
        /// already in use), so unlike Join there is no "connecting..." wait
        /// state to show.
        /// </summary>
        public void Host()
        {
            SetStatus(string.Empty);
            if (!TryParsePort(HostPortField, out ushort port))
            {
                SetStatus("Host port must be a number between 1 and 65535.");
                return;
            }
            if (Bootstrap == null)
            {
                SetStatus("No networking available.");
                return;
            }

            // Only from here on, not from Awake/OnEnable -- see
            // TitleScreenBuilder.BuildNetworkRoot's own doc comment for why
            // this can't be unconditional: it would otherwise survive into
            // every later PlayMode test in the same batch run, the instant
            // ANY test so much as opened the title screen.
            Object.DontDestroyOnLoad(Bootstrap.gameObject);

            bool started = Bootstrap.Begin(GameMode.HostedWorld,
                LaunchArgs.For(GameMode.HostedWorld, NetworkBootstrap.LoopbackBindAddress, port));
            if (!started)
            {
                SetStatus($"Couldn't host on port {port} -- is another instance already running?");
                return;
            }
            SetStatus($"Hosting on port {port}.");
            EnterGame();
        }

        /// <summary>
        /// Starts connecting; Update() below decides success or failure once
        /// FishNet's own connection attempt actually resolves.
        /// </summary>
        public void Join()
        {
            SetStatus(string.Empty);
            string address = JoinAddressField != null ? JoinAddressField.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(address))
            {
                SetStatus("Enter an IP address to join.");
                return;
            }

            if (!TryParsePort(JoinPortField, out ushort port))
            {
                SetStatus("Port must be a number between 1 and 65535.");
                return;
            }

            // A pasted "1.2.3.4:30000" is how an address usually gets
            // shared, so it's accepted directly and the port taken from it.
            // Guarded on there being exactly ONE colon: a real IPv6 address
            // is full of them, and splitting one of those would mangle it.
            int firstColon = address.IndexOf(':');
            if (firstColon >= 0 && address.IndexOf(':', firstColon + 1) < 0)
            {
                string suffix = address.Substring(firstColon + 1).Trim();
                if (ushort.TryParse(suffix, out ushort suffixPort) && suffixPort >= 1)
                {
                    address = address.Substring(0, firstColon).Trim();
                    port = suffixPort;
                }
            }

            if (Bootstrap == null)
            {
                SetStatus("No networking available.");
                return;
            }

            Unsubscribe();
            // See Host()'s own comment on why this is here and not on Awake.
            Object.DontDestroyOnLoad(Bootstrap.gameObject);
            bool starting = Bootstrap.Begin(GameMode.RemoteClient, LaunchArgs.For(GameMode.RemoteClient, address, port));
            if (!starting)
            {
                SetStatus("Couldn't start connecting.");
                return;
            }

            SetStatus($"Connecting to {address}:{port}...");
            _joinPending = true;
            if (Bootstrap.Manager != null)
            {
                Bootstrap.Manager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            }
        }

        /// <summary>
        /// Also shuts down networking, covering the case where Back is
        /// pressed while a join attempt is still pending ("Connecting..."
        /// showing), not just backing out of an idle panel with nothing in
        /// progress.
        /// </summary>
        public void Back()
        {
            Unsubscribe();
            UseThisPcsCharacters();
            if (Bootstrap != null)
            {
                Bootstrap.Shutdown();
            }
            if (Title != null)
            {
                Title.Show(TitleScreen.Panel.Menu);
            }
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (!_joinPending)
            {
                return;
            }

            if (args.ConnectionState == LocalConnectionState.Started)
            {
                _joinPending = false;
                Unsubscribe();
                // The characters are now the joined server's (docs/SERVER_SAVING.md), not this PC's.
                UseThisPcsCharacters();
                CharacterSources.Current = new RemoteCharacterSource(Bootstrap.Manager);
                EnterGame();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _joinPending = false;
                Unsubscribe();
                SetStatus("Couldn't connect -- check the address and that they're actually hosting.");
            }
        }

        private void Unsubscribe()
        {
            if (Bootstrap != null && Bootstrap.Manager != null)
            {
                Bootstrap.Manager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            }
            _joinPending = false;
        }

        /// <summary>
        /// A session now exists; reuse the exact same title -> select ->
        /// creator pipeline Singleplayer already goes through (see
        /// CharacterSelect.Play) -- there is no separate "multiplayer
        /// character creator" on either engine.
        /// </summary>
        private void EnterGame()
        {
            if (Title != null)
            {
                Title.Show(TitleScreen.Panel.CharacterSelect);
            }
        }

        /// <summary>Back to the characters of this PC's own server, letting go of a joined server's.</summary>
        private static void UseThisPcsCharacters()
        {
            (CharacterSources.Current as System.IDisposable)?.Dispose();
            CharacterSources.Current = null;
        }

        /// <summary>Blank means NetworkBootstrap's own LaunchArgs.DefaultPort, so the common case needs no typing at all.</summary>
        private static bool TryParsePort(InputField field, out ushort port)
        {
            port = LaunchArgs.DefaultPort;
            string text = field != null ? field.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            return ushort.TryParse(text, out port) && port >= 1;
        }

        private void SetStatus(string message)
        {
            if (StatusLabel != null)
            {
                StatusLabel.text = message;
            }
        }
    }
}
