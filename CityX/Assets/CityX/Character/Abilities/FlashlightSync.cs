using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE FLASHLIGHT AS OTHER PLAYERS SEE IT (docs/FLASHLIGHT.md, docs/MULTIPLAYER_STATE.md): on the player body, this watches the light in its
    /// hand (<see cref="ITorch"/>) and tells other players when it goes on or off and, while it is on, where it points, ten times a second; on
    /// their copy of the body it switches the copy's light the same and points it there (the arm follows, PlayerLocomotion.Torch).
    /// </summary>
    public sealed class FlashlightSync : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "flashlight";

        /// <summary>The events (<see cref="AbilityEvent.Kind"/>): switched On (Origin the point it is aimed at), Off, and Aimed while on.</summary>
        public const int On = 0;

        public const int Off = 1;
        public const int Aimed = 2;

        /// <summary>How often a lit light's aim is sent, seconds, and how far its point must have moved since the last, metres.</summary>
        public const float AimEverySeconds = 0.1f;

        public const float AimMovedMetres = 0.2f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        private bool _sentLit;
        private Vector3 _sentPoint;
        private float _sinceAim;

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>The owner's side: what the light in hand does, told; public so a test can step it.</summary>
        public void Tick(float deltaTime)
        {
            ITorch torch = GetComponentInChildren<ITorch>();
            bool lit = torch != null && torch.IsLit;
            if (lit != _sentLit)
            {
                _sentLit = lit;
                _sentPoint = lit ? torch.AimPoint : transform.position;
                _sinceAim = 0.0f;
                Fired?.Invoke(new AbilityEvent(lit ? On : Off, _sentPoint, Vector3.zero));
                return;
            }
            if (!lit)
            {
                return;
            }
            _sinceAim += deltaTime;
            if (_sinceAim >= AimEverySeconds && Vector3.Distance(torch.AimPoint, _sentPoint) > AimMovedMetres)
            {
                _sinceAim = 0.0f;
                _sentPoint = torch.AimPoint;
                Fired?.Invoke(new AbilityEvent(Aimed, _sentPoint, Vector3.zero));
            }
        }

        /// <summary>Another player's light, on this copy of their body: on or off, pointed where theirs points.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            ITorch torch = GetComponentInChildren<ITorch>();
            if (torch == null)
            {
                return;
            }
            switch (evt.Kind)
            {
                case On:
                case Aimed:
                    torch.ShowLit(true, evt.Origin);
                    break;
                case Off:
                    torch.ShowLit(false, evt.Origin);
                    break;
            }
        }
    }
}
