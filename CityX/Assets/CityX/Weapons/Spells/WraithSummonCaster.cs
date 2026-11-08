using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE WRAITH STAFF'S SUMMON (docs/WRAITH_STAFF.md): a click, while no Wraith of yours is about, calls one up a few steps ahead of you, on
    /// your side (Wraith.Summon). One at a time: "when he dies you can summon another". In a session the summon is the networked
    /// "wraith_summon", asked of the server and run by your machine (EnemySession.TrySpawn), so every player sees it; offline it is made here.
    /// Other players see the staff thrust out and hear the call (PlayRemote).
    /// </summary>
    public sealed class WraithSummonCaster : MonoBehaviour, IStaffCaster, IReplicatedAbility
    {
        public const string Id = "wraith-summon";

        /// <summary>What a summon costs, mana.</summary>
        public const float ManaCost = 40.0f;

        /// <summary>It rises this far ahead of you, metres.</summary>
        public const float AheadMetres = 2.5f;

        /// <summary>
        /// Asked of the server, it takes a moment to come in: no second ask for this long, seconds (a summon that never comes is asked again
        /// after it).
        /// </summary>
        public const float WaitForItSeconds = 2.0f;

        /// <summary>The networked summon's kind (NetworkedEnemyBuilder.WraithSummonKind).</summary>
        public const string SummonKind = "wraith_summon";

        /// <summary>Its call, heard where it rises.</summary>
        public const float CallVolume = 0.8f;

        [Tooltip("The Wraith, made here when there is no session to ask (WraithBuilder's prefab).")]
        public GameObject WraithPrefab;

        public event System.Action<AbilityEvent> Fired;

        string IReplicatedAbility.AbilityId => Id;

        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private bool _wasHeld;
        private float _waiting;
        private float _staffOut;

        /// <summary>Your Wraith, while one is about; null when none is.</summary>
        public Wraith Current
        {
            get
            {
                Bind();
                foreach (IOpponent summon in Opponents.Summons)
                {
                    if (summon is Wraith wraith && Opponents.Alive(summon) && wraith.Master != null && wraith.Master == _vitals)
                    {
                        return wraith;
                    }
                }
                return null;
            }
        }

        /// <summary>A click now would summon one: none of yours about, none on its way, and you alive.</summary>
        public bool CanSummon => Current == null && _waiting <= 0.0f && (_vitals == null || !_vitals.IsDead);

        private void Awake()
        {
            Bind();
        }

        private void Bind()
        {
            if (_vitals == null)
            {
                _vitals = GetComponent<PlayerVitals>();
                _locomotion = GetComponent<PlayerLocomotion>();
            }
        }

        public void TickStaff(bool held, Transform tip, Vector3 eye, Vector3 look, float deltaTime)
        {
            bool pressed = held && !_wasHeld;
            _wasHeld = held;
            if (pressed)
            {
                TrySummon(look);
            }
        }

        public void CancelStaffCharge()
        {
            _wasHeld = false;
        }

        /// <summary>Summons your Wraith ahead of you along <paramref name="look"/>, if one can be: the mana paid. True when it was called.</summary>
        public bool TrySummon(Vector3 look)
        {
            Bind();
            if (!CanSummon || (_vitals != null && !_vitals.TrySpendMana(ManaCost)))
            {
                return false;
            }
            Vector3 ahead = new Vector3(look.x, 0.0f, look.z);
            ahead = ahead.sqrMagnitude > 1e-4f ? ahead.normalized : transform.forward;
            Vector3 at = transform.position + ahead * AheadMetres + Vector3.up * WraithDrift.HoverHeight;
            Quaternion facing = Quaternion.LookRotation(ahead, Vector3.up);
            if (EnemySession.TrySpawn(SummonKind, at, facing))
            {
                _waiting = WaitForItSeconds;
            }
            else if (WraithPrefab != null)
            {
                Instantiate(WraithPrefab, at, facing).GetComponent<Wraith>().Summon(_vitals);
            }
            Called(at);
            Fired?.Invoke(new AbilityEvent(0, at, ahead));
            return true;
        }

        /// <summary>Another player's summon, on this copy of them: the staff thrust out and the call (the Wraith itself comes over the network).</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            Bind();
            Called(evt.Origin);
        }

        private void Called(Vector3 at)
        {
            _staffOut = StaffCharge.FollowThroughSeconds;
            if (Application.isPlaying && Wraith.AttackClip() != null)
            {
                AudioSource.PlayClipAtPoint(Wraith.AttackClip(), at, CallVolume);
            }
        }

        /// <summary>The staff held out as it calls, on this body and on other players' copies of it.</summary>
        private void Update()
        {
            if (_waiting > 0.0f)
            {
                _waiting = Current != null ? 0.0f : _waiting - Time.deltaTime;
            }
            if (_staffOut > 0.0f)
            {
                _staffOut -= Time.deltaTime;
                if (_locomotion != null && _locomotion.Staff != null)
                {
                    _locomotion.HoldStaffOut();
                }
            }
        }
    }
}
