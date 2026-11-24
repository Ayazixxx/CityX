using UnityEngine;

namespace CityX.Character
{
    /// <summary>Something worn with a hood that goes up and down (the Hooded Coat).</summary>
    public interface IHooded
    {
        bool HoodUp { get; }

        void SetHood(bool up);
    }

    /// <summary>
    /// A WORN HOOD, UP OR DOWN (docs/BROWN_COAT.md; owner, 2026-11-23: "hood off by default but if you type /hoodon in the text chat ... it turns
    /// into the hood on version"). On the player body: the chat's /hoodon and /hoodoff ask it, it tells what is worn (<see cref="IHooded"/>), and
    /// tells other players, whose copies of the body do the same. While the hood is up it says so again every few seconds, so a player who comes
    /// near or joins later sees it up too (an event alone is heard once).
    /// </summary>
    public sealed class HoodSync : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "hood";

        /// <summary>The events (<see cref="AbilityEvent.Kind"/>).</summary>
        public const int Down = 0;

        public const int Up = 1;

        /// <summary>How often a raised hood is told again, seconds.</summary>
        public const float RepeatSeconds = 4.0f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        /// <summary>What is worn that has a hood (it says so when put on and taken off); null with none.</summary>
        public IHooded Worn { get; private set; }

        private float _sinceTold;

        /// <summary>A hooded thing is put on this body: its hood is as it comes (down).</summary>
        public void Wear(IHooded hooded)
        {
            Worn = hooded;
            _sinceTold = 0.0f;
        }

        public void TakeOff(IHooded hooded)
        {
            if (ReferenceEquals(Worn, hooded))
            {
                Worn = null;
            }
        }

        /// <summary>The wearer's own ask (the chat's /hoodon, /hoodoff): false when nothing worn has a hood.</summary>
        public bool SetHood(bool up)
        {
            if (Worn == null)
            {
                return false;
            }
            Worn.SetHood(up);
            _sinceTold = 0.0f;
            Fired?.Invoke(new AbilityEvent(up ? Up : Down, transform.position, Vector3.zero));
            return true;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>The owner's side: a raised hood told again now and then; public so a test can step it.</summary>
        public void Tick(float deltaTime)
        {
            if (Worn == null || !Worn.HoodUp || Fired == null)
            {
                return;
            }
            _sinceTold += deltaTime;
            if (_sinceTold >= RepeatSeconds)
            {
                _sinceTold = 0.0f;
                Fired.Invoke(new AbilityEvent(Up, transform.position, Vector3.zero));
            }
        }

        /// <summary>Another player's hood, on this copy of their body.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (Worn != null && Worn.HoodUp != (evt.Kind == Up))
            {
                Worn.SetHood(evt.Kind == Up);
            }
        }
    }
}
