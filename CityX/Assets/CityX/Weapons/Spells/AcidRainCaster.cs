using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// ACID RAIN (key 5): the acid tree's god-tier spell, built first as a test ability on a spare key (owner, 2026-10-10: "make the acid rain ...
    /// in a large area but not the entire world"). Press 5 and a storm of green acid gathers over where the crosshair points, up to
    /// <see cref="Reach"/> away, and burns everyone out in the open under it for 20 s. Roofs shelter. The caster is never burned.
    ///
    /// MULTIPLAYER FROM THE START (IReplicatedAbility, id "acid-rain"): the owner's copy raises Fired with the storm's centre, and every other
    /// player's copy of the caster calls down the same storm, cosmetic (it looks and sounds the same, burns nothing). The damage is the caster's
    /// machine's, and reaches other players through their copies like every hit.
    /// </summary>
    public sealed class AcidRainCaster : MonoBehaviour, IAcidRainCaster, IReplicatedAbility
    {
        public const string Id = "acid-rain";

        /// <summary>
        /// How far away the storm can be called down. Kept inside the 60 m the network allows between a caster and the origin of their ability
        /// (NetworkedAbilitySync.MaxOriginDistanceMetres): the centre is sent as that origin.
        /// </summary>
        public const float Reach = 50.0f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        [Tooltip("Seconds between storms. Long: it is a god-tier spell.")]
        public float CooldownSeconds = 60.0f;

        public GameObject StormPrefab;

        private float _cooldown;
        private PlayerVitals _vitals;

        public bool CanCast => _cooldown <= 0.0f && (_vitals == null || !_vitals.IsDead);

        public float CooldownRemaining => Mathf.Max(0.0f, _cooldown);

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
        }

        private void Update()
        {
            if (_cooldown > 0.0f)
            {
                _cooldown -= Time.deltaTime;
            }
        }

        public bool TryCast(Vector3 eye, Vector3 direction, Collider[] ignoreColliders)
        {
            if (!CanCast || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            // Cooldown Reduction shortens it (2026-10-14).
            _cooldown = CooldownSeconds * PlayerVitals.CooldownFactorOf(_vitals);
            Vector3 centre = Target(eye, direction.normalized, ignoreColliders, transform.position);
            // A spell: Spell Power makes it burn harder (2% a point, 2026-10-14).
            int spellPower = _vitals != null ? _vitals.Stats[StatId.SpellPower] : 0;
            AcidRainStorm.Spawn(StormPrefab, centre, spellPower, ignoreColliders, striker: gameObject);
            Fired?.Invoke(new AbilityEvent(0, centre, Vector3.zero));
            return true;
        }

        /// <summary>
        /// Where the storm is centred: the first solid thing the crosshair meets (not the caster), or, past <see cref="Reach"/>, the ground below
        /// the point at that distance. Never further than Reach from <paramref name="caster"/>.
        /// </summary>
        public static Vector3 Target(Vector3 eye, Vector3 direction, Collider[] ignore, Vector3 caster)
        {
            Vector3 point = eye + direction * Reach;
            RaycastHit[] hits = Physics.RaycastAll(eye, direction, Reach + Vector3.Distance(eye, caster), ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || (ignore != null && System.Array.IndexOf(ignore, hit.collider) >= 0) || hit.distance >= nearest)
                {
                    continue;
                }
                nearest = hit.distance;
                point = hit.point;
            }
            if (nearest == float.MaxValue && Physics.Raycast(point + Vector3.up * 5.0f, Vector3.down, out RaycastHit ground, 300.0f, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                point = ground.point;
            }
            Vector3 offset = point - caster;
            return offset.magnitude > Reach ? caster + offset.normalized * Reach : point;
        }

        /// <summary>Another player's storm, on this machine: the same clouds, rain and hiss, burning nothing.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (float.IsNaN(evt.Origin.x) || float.IsInfinity(evt.Origin.x))
            {
                return;
            }
            AcidRainStorm.Spawn(StormPrefab, evt.Origin, 0, null, cosmetic: true);
        }
    }
}
