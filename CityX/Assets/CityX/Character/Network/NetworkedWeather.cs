using CityX.World;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>A change of weather, sent to the server and from it to everyone; and the weather as it is, sent to a player who has just joined.</summary>
    public struct WeatherMessage : IBroadcast
    {
        public float Rain;
        public float Snow;

        /// <summary>How much snow lies, if the message says; below 0 it does not.</summary>
        public float Cover;

        public bool Dry;
    }

    public struct WeatherStateRequest : IBroadcast
    {
    }

    /// <summary>
    /// THE WEATHER IS THE SAME FOR EVERYONE (docs/SNOW.md, docs/MULTIPLAYER_STATE.md): an order goes to the server, which keeps it and tells everyone, the giver too, so all
    /// machines change together; a player who joins asks how it is. Before this, /weather changed only the machine it was typed on. Outside a session the order goes straight
    /// to the weather (<see cref="CurrentWeather.Order"/>). What falls and lies is all that is sent: each flake and each print is every machine's own.
    /// </summary>
    public sealed class NetworkedWeather : SessionRelay, IWeatherRelay
    {
        /// <summary>The most orders from one player that are heeded, per second.</summary>
        public const int MaxOrdersPerSecond = 4;

        /// <summary>This machine's weather. Null: the game's (<see cref="CurrentWeather.Control"/>); a test gives each of its machines its own.</summary>
        public IWeatherControl Target;

        private readonly SenderRate _rate = new SenderRate(MaxOrdersPerSecond);
        private WeatherMessage _kept;

        private IWeatherControl Control => Target ?? CurrentWeather.Control;

        protected override void HearAsServer(ServerManager server, bool on)
        {
            Hear<WeatherMessage>(server, on, OnOrderAtServer);
            Hear<WeatherStateRequest>(server, on, OnStateRequestAtServer);
            if (on)
            {
                WeatherOrder now = Control != null ? Control.Now : default;
                _kept = new WeatherMessage { Rain = now.Rain, Snow = now.Snow, Cover = -1.0f };
            }
        }

        protected override void HearAsClient(ClientManager client, bool on)
        {
            Hear<WeatherMessage>(client, on, OnWeatherAtClient);
            if (on)
            {
                if (Target == null)
                {
                    CurrentWeather.Relay = this;
                }
            }
            else
            {
                LetGoOfRelay();
            }
        }

        protected override void OnLetIn(ClientManager client)
        {
            client.Broadcast(new WeatherStateRequest());
        }

        private void OnDisable()
        {
            LetGoOfRelay();
        }

        private void LetGoOfRelay()
        {
            if (ReferenceEquals(CurrentWeather.Relay, this))
            {
                CurrentWeather.Relay = null;
            }
        }

        public void Send(in WeatherOrder order)
        {
            if (CanSend)
            {
                Session.ClientManager.Broadcast(new WeatherMessage { Rain = order.Rain, Snow = order.Snow, Cover = order.Cover, Dry = order.Dry });
            }
        }

        private void OnOrderAtServer(NetworkConnection sender, WeatherMessage message, Channel channel)
        {
            if (!_rate.Allow(sender.ClientId, Time.unscaledTime) || !InRange(message.Rain) || !InRange(message.Snow) || float.IsNaN(message.Cover) || message.Cover > 1.0f)
            {
                return;
            }
            _kept = message;
            Session.ServerManager.Broadcast(message);
        }

        private void OnStateRequestAtServer(NetworkConnection sender, WeatherStateRequest request, Channel channel)
        {
            // How much lies now, as the server's own weather has it, which an order given a while ago does not say.
            WeatherMessage state = _kept;
            state.Dry = false;
            state.Cover = Control != null ? Control.Now.Cover : -1.0f;
            Session.ServerManager.Broadcast(sender, state);
        }

        private void OnWeatherAtClient(WeatherMessage message, Channel channel)
        {
            Control?.Apply(new WeatherOrder { Rain = message.Rain, Snow = message.Snow, Cover = message.Cover, Dry = message.Dry });
        }

        private static bool InRange(float value) => !float.IsNaN(value) && value >= 0.0f && value <= 1.0f;
    }
}
