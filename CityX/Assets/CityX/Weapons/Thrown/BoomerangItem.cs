using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE WOODEN BOOMERANG in the hand (docs/BOOMERANG.md). A click throws it, with or without right-click's aim, toward what the crosshair is on. It is
    /// not spent: the hand is empty until it comes back, and clicks do nothing till then. Drawn in code (BoomerangBuilder), a stand-in until the
    /// owner's model. Seen in other players' hands (ICosmeticMountable); the throw reaches them through the body's <see cref="BoomerangThrower"/>.
    /// </summary>
    public sealed class BoomerangItem : MonoBehaviour, IEquippableItem, IEquippableWeapon, ICosmeticMountable
    {
        /// <summary>Lying flat in the fist: one arm across the palm, its elbow past the thumb, the other arm out past the back of the hand, away from the body.</summary>
        public static readonly Vector3 GripHandPosition = new Vector3(0.09f, 0.085f, 0.175f);

        public static readonly Vector3 GripHandRotationEuler = new Vector3(50.0f, 0.0f, -90.0f);

        /// <summary>A way that comes out this far from where the player looks (a wall right behind the body) is ignored for where they look.</summary>
        private const float LeastAgreement = 0.2f;

        private BoomerangThrower _thrower;

        /// <summary>The wielder's melee, while equipped: it fights bare-handed while the boomerang is out.</summary>
        private MeleeAttacker _melee;

        public string ItemId => ItemRegistry.WoodenBoomerang;

        public bool InHand => _thrower == null || !_thrower.IsOut;

        public float GetMeleeDamage() => 0.0f;

        public float MeleeCooldownSeconds => BoomerangRules.ThrowEverySeconds;

        public void RegisterLandedMeleeHit()
        {
        }

        public bool CanThrow() => true;

        public float ThrowSpeed => BoomerangRules.OutSpeed;

        public string MovesetId => null;

        public ComboStage? HeavyStage => null;

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            _melee = player.GetComponent<MeleeAttacker>();
            Hold(player);
        }

        public void OnUnequip(GameObject player)
        {
            if (_melee != null && ReferenceEquals(_melee.EquippedWeapon, this))
            {
                _melee.EquippedWeapon = null;
            }
            _melee = null;
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            Hold(body);
        }

        public void UnmountCosmetic(GameObject body)
        {
            Watch(null);
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        public void Throw(GameObject player, Vector3 origin, Vector3 direction)
        {
            var thrower = player != null ? player.GetComponent<BoomerangThrower>() : null;
            if (thrower == null || !thrower.CanThrow)
            {
                return;
            }
            var vitals = player.GetComponent<PlayerVitals>();
            float damage = CombatTuning.DamageFor(BoomerangRules.Damage, vitals != null ? vitals.Strength : 0);
            float penetration = vitals != null ? vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            thrower.Throw(origin, AimedWay(origin, direction, player, thrower.ProfileOf(giant: false).Range), damage, penetration);
        }

        /// <summary>
        /// From the hand to what the crosshair is on within its range, or to the end of that range: the camera looks past the shoulder, so a throw
        /// along the camera's own line would pass to one side of what it is pointing at.
        /// </summary>
        internal static Vector3 AimedWay(Vector3 origin, Vector3 looking, GameObject thrower, float range)
        {
            Vector3 along = looking.normalized;
            Camera eye = Camera.main;
            if (eye == null)
            {
                return along;
            }
            Vector3 from = eye.transform.position;
            float reach = range + Vector3.Distance(from, origin);
            Vector3 target = from + along * reach;
            float nearest = reach;
            foreach (RaycastHit hit in Physics.RaycastAll(from, along, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < nearest && !hit.collider.transform.IsChildOf(thrower.transform))
                {
                    nearest = hit.distance;
                    target = hit.point;
                }
            }
            Vector3 way = (target - origin).normalized;
            return Vector3.Dot(way, along) >= LeastAgreement ? way : along;
        }

        private void Hold(GameObject body)
        {
            CosmeticMount.ToBone(transform, body, HumanBodyBones.RightHand, GripHandPosition, GripHandRotationEuler, 1.0f);
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
            Watch(body != null ? body.GetComponent<BoomerangThrower>() : null);
        }

        private void Watch(BoomerangThrower thrower)
        {
            if (_thrower != null)
            {
                _thrower.OutChanged -= ShowInHand;
            }
            _thrower = thrower;
            if (_thrower != null)
            {
                _thrower.OutChanged += ShowInHand;
            }
            ShowInHand();
        }

        private void ShowInHand()
        {
            bool inHand = InHand;
            foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
            {
                part.enabled = inHand;
            }
            if (_melee != null)
            {
                // Out, the hands are bare: the fists' own swings, not the boomerang's.
                if (inHand) _melee.EquippedWeapon = this;
                else if (ReferenceEquals(_melee.EquippedWeapon, this)) _melee.EquippedWeapon = null;
            }
        }

        private void OnDestroy()
        {
            if (_thrower != null)
            {
                _thrower.OutChanged -= ShowInHand;
            }
        }
    }
}
