using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The Staff of Healing's beam (owner, 2026-10-29: "create staff of healing that functions like mercy's staff from overwatch but without
    /// the damage boost part"; docs/STAFFS.md). Held, a gold beam ties itself to the other player nearest the crosshair within reach
    /// (<see cref="HealingBeamRules"/>) and heals them a tick at a time; it stays on them while the healer looks elsewhere, until the click is let
    /// go, they go past its reach or stay out of sight too long, or they die. Never the healer, never an enemy. It drinks a little mana while it
    /// is on someone. Drawn in code (<see cref="StaffBeam"/>, its healing style).
    ///
    /// Whoever it heals is healed on their own machine (PlayerVitals.Heal sends a heal on another player's copy to its owner, as a blow is).
    /// Other players see it (<see cref="IReplicatedAbility"/>): who the beam is on (as where their chest is, ten times a second; each copy ties
    /// its beam to the body nearest it), and when it lets go.
    /// </summary>
    public sealed class HealingBeamCaster : MonoBehaviour, IStaffCaster, IReplicatedAbility
    {
        public const string Id = "healing-beam";

        /// <summary>The events other players are sent (<see cref="AbilityEvent.Kind"/>): Tied with Origin the chest of whom it is on, then Let go.</summary>
        public const int Tied = 0;

        public const int LetGo = 1;

        /// <summary>A copy's beam runs on this long past the last word of it, seconds.</summary>
        public const float RemoteGraceSeconds = 0.4f;

        /// <summary>A copy ties its beam to the player's body nearest the chest it was sent, within this, metres.</summary>
        public const float RemoteFindMetres = 3.0f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        private static readonly RaycastHit[] SightBuffer = new RaycastHit[16];
        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private StaffBeam _beam;
        private StaffBeam _remoteBeam;
        private PlayerVitals _target;
        private PlayerVitals _remoteTarget;
        private float _outOfSight;
        private float _sinceTick;
        private float _aimSendIn;
        private float _remoteLeft;

        /// <summary>Whom the beam is on (null when it is on no one).</summary>
        public PlayerVitals Target => _target;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, for tests where Awake never runs (the casters' idiom).</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        public void TickStaff(bool held, Transform tip, Vector3 eye, Vector3 look, float deltaTime)
        {
            bool alive = _vitals == null || !_vitals.IsDead;
            if (!held || !alive)
            {
                Release();
                return;
            }
            Vector3 from = tip != null ? tip.position : transform.position + Vector3.up * 1.8f;
            if (_target != null && !Keeps(eye, deltaTime))
            {
                Release();
            }
            if (_target == null)
            {
                Tie(Pick(eye, look), tip);
            }
            if (_target == null)
            {
                return;
            }
            if (_vitals != null && !_vitals.TrySpendMana(HealingBeamRules.ManaPerSecond * deltaTime))
            {
                Release();
                return;
            }
            _locomotion?.HoldStaffOut();
            Vector3 chest = ChestOf(_target);
            _beam?.Aim(from, chest, true, deltaTime);
            float spellPower = PlayerVitals.SpellPowerOf(_vitals);
            float heal = CombatTuning.DamageFor(HealingBeamRules.HealPerTick, spellPower);
            for (int due = SpellTicks.Due(ref _sinceTick, deltaTime, HealingBeamRules.TickSeconds); due > 0; due--)
            {
                _target.Heal(heal);
            }
            _aimSendIn -= deltaTime;
            if (_aimSendIn <= 0.0f)
            {
                _aimSendIn = HealingBeamRules.AimSendSeconds;
                Fired?.Invoke(new AbilityEvent(Tied, chest, Vector3.zero));
            }
        }

        public void CancelStaffCharge()
        {
            Release();
        }

        /// <summary>The other living player nearest the crosshair, within reach and in sight; null when there is none.</summary>
        private PlayerVitals Pick(Vector3 eye, Vector3 look)
        {
            PlayerVitals best = null;
            float bestScore = float.MaxValue;
            foreach (PlayerInputReader reader in Object.FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None))
            {
                if (reader.gameObject == gameObject || !PlayerBodies.IsPlayer(reader.gameObject) || !reader.TryGetComponent(out PlayerVitals other) || other.IsDead)
                {
                    continue;
                }
                Vector3 chest = ChestOf(other);
                float score = HealingBeamRules.LockScore(eye, look, transform.position, chest);
                if (score < bestScore && InSight(eye, chest, other.transform))
                {
                    bestScore = score;
                    best = other;
                }
            }
            return best;
        }

        private void Tie(PlayerVitals target, Transform tip)
        {
            if (target == null)
            {
                return;
            }
            _target = target;
            _outOfSight = 0.0f;
            _sinceTick = HealingBeamRules.TickSeconds;   // the first tick lands at once
            _aimSendIn = HealingBeamRules.AimSendSeconds;
            if (_beam == null && tip != null)
            {
                _beam = StaffBeam.Make(tip, StaffBeam.Healing);
            }
            _beam?.Run();
            Fired?.Invoke(new AbilityEvent(Tied, ChestOf(target), Vector3.zero));
        }

        /// <summary>Whether the beam stays on its target this frame: alive, within its stretch, not out of sight too long.</summary>
        private bool Keeps(Vector3 eye, float deltaTime)
        {
            if (_target == null || _target.IsDead || !_target.isActiveAndEnabled)
            {
                return false;
            }
            Vector3 chest = ChestOf(_target);
            _outOfSight = InSight(eye, chest, _target.transform) ? 0.0f : _outOfSight + deltaTime;
            return HealingBeamRules.Holds(Vector3.Distance(transform.position, chest), _outOfSight);
        }

        private void Release()
        {
            if (_target == null)
            {
                return;
            }
            _target = null;
            _beam?.Stop();
            Fired?.Invoke(new AbilityEvent(LetGo, transform.position, Vector3.zero));
        }

        /// <summary>Whether nothing but the two bodies stands between <paramref name="from"/> and <paramref name="to"/>.</summary>
        private bool InSight(Vector3 from, Vector3 to, Transform other)
        {
            Vector3 along = to - from;
            float length = along.magnitude;
            if (length < 1e-3f)
            {
                return true;
            }
            int count = Physics.RaycastNonAlloc(from, along / length, SightBuffer, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = SightBuffer[i].collider != null ? SightBuffer[i].collider.transform : null;
                if (hit != null && !hit.IsChildOf(transform) && !hit.IsChildOf(other))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Halfway up the spine (the rig has no chest bone), so the beam stays on the chest of a body lying down too.</summary>
        public static Vector3 ChestOf(Component body)
        {
            Animator animator = body.GetComponentInChildren<Animator>();
            bool human = animator != null && animator.isHuman;
            Transform spine = human ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            Transform neck = human ? animator.GetBoneTransform(HumanBodyBones.Neck) : null;
            return spine != null && neck != null
                ? Vector3.Lerp(spine.position, neck.position, 0.5f)
                : body.transform.position + Vector3.up * 1.7f * body.transform.lossyScale.y;
        }

        // --- what other players see ---

        private void Update()
        {
            if (_remoteLeft <= 0.0f)
            {
                return;
            }
            _remoteLeft -= Time.deltaTime;
            Transform tip = StaffItem.TipOf(_locomotion);
            if (_remoteLeft <= 0.0f || tip == null || _remoteTarget == null)
            {
                _remoteLeft = 0.0f;
                _remoteBeam?.Stop();
                return;
            }
            _locomotion?.HoldStaffOut();
            _remoteBeam?.Aim(tip.position, ChestOf(_remoteTarget), true, Time.deltaTime);
        }

        /// <summary>Another player's beam, on this copy of their staff: tied to the body nearest the chest they sent, letting go with theirs.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case Tied:
                    Transform tip = StaffItem.TipOf(_locomotion);
                    PlayerVitals target = NearestBody(evt.Origin);
                    if (tip == null || target == null)
                    {
                        return;
                    }
                    if (_remoteBeam == null)
                    {
                        _remoteBeam = StaffBeam.Make(tip, StaffBeam.Healing);
                    }
                    _remoteTarget = target;
                    _remoteBeam.Run();
                    _remoteLeft = RemoteGraceSeconds;
                    break;
                case LetGo:
                    _remoteLeft = 0.0f;
                    _remoteTarget = null;
                    _remoteBeam?.Stop();
                    break;
            }
        }

        /// <summary>The body (not this one) whose chest is nearest <paramref name="chest"/>, within <see cref="RemoteFindMetres"/>.</summary>
        private PlayerVitals NearestBody(Vector3 chest)
        {
            PlayerVitals best = null;
            float nearest = RemoteFindMetres;
            foreach (PlayerVitals body in Object.FindObjectsByType<PlayerVitals>(FindObjectsSortMode.None))
            {
                if (body.gameObject == gameObject)
                {
                    continue;
                }
                float distance = Vector3.Distance(ChestOf(body), chest);
                if (distance < nearest)
                {
                    nearest = distance;
                    best = body;
                }
            }
            return best;
        }
    }
}
