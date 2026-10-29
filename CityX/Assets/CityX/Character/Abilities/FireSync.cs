using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE FIRE AS OTHER PLAYERS SEE IT (docs/FIRE.md, docs/MULTIPLAYER_STATE.md): on the player body, this forwards everything this machine
    /// does to the fire (<see cref="FireScene.Happened"/>: a pour, a lighting, a dousing) as ability events, and replays the ones that arrive
    /// from other players as cosmetic fire -- the same gasoline, the same flames, burning nobody here (their machine deals the damage, which
    /// reaches this player through their copy of them, as every hit does). A copy's jerry can is shown pouring with its owner's.
    /// </summary>
    public sealed class FireSync : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "fire";

        /// <summary>The events (<see cref="AbilityEvent.Kind"/>): Origin the point, A the litres or the radius, N 1 for a lighting that takes only fuel.</summary>
        public const int Poured = 0;

        public const int Lit = 1;
        public const int Doused = 2;

        /// <summary>The most a replayed pour, lighting or dousing may be: a stranger's packet is trusted no further.</summary>
        public const float MaxLitres = 2.0f;

        public const float MaxRadius = 6.0f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        private void OnEnable()
        {
            FireScene.Happened += Forward;
        }

        private void OnDisable()
        {
            FireScene.Happened -= Forward;
        }

        private void Forward(FireEvent fire)
        {
            int kind = fire.Act == FireAct.Pour ? Poured : fire.Act == FireAct.Ignite ? Lit : Doused;
            Fired?.Invoke(new AbilityEvent(kind, fire.Point, Vector3.up, fire.Amount, 0.0f, fire.OnlyFuel ? 1 : 0));
        }

        public void PlayRemote(AbilityEvent evt)
        {
            if (float.IsNaN(evt.A) || float.IsInfinity(evt.A) || evt.A <= 0.0f)
            {
                return;
            }
            switch (evt.Kind)
            {
                case Poured:
                    FireScene.Pour(evt.Origin, Mathf.Min(evt.A, MaxLitres), true);
                    GetComponentInChildren<IPouredCan>()?.ShowPouring(evt.Origin);
                    break;
                case Lit:
                    FireScene.Ignite(evt.Origin, Mathf.Min(evt.A, MaxRadius), evt.N == 1, null, true);
                    break;
                case Doused:
                    FireScene.Douse(evt.Origin, Mathf.Min(evt.A, MaxRadius), true);
                    break;
            }
        }
    }
}
