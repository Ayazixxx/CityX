using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Weapons
{
    /// <summary>
    /// A pick-up-able flamethrower: hold left-click to spray a continuous
    /// stream of fire that damages anything alive within a short cone in
    /// front. No Godot equivalent to port -- an explicit new-mechanic ask,
    /// same footing as Jetpack.cs/FireballCaster.cs.
    ///
    /// NOT AN IEquippableWeapon OR IEquippableGun -- same reasoning
    /// PistolWeapon.cs's own class comment gives for skipping the melee
    /// contract, one step further: this has no magazine/reload either, and
    /// fires continuously for as long as the trigger is held rather than
    /// once per click. IEquippableFlamethrower's own doc comment covers why
    /// this is closer in shape to IJetpack.
    ///
    /// NO FIRE SPREAD, NO BUILDING/ENVIRONMENT DAMAGE -- an explicit scope
    /// limit from the ask ("no fire spread yet or building damage, just try
    /// to get the flames looking good"). Only a living IDamageable actually
    /// standing in the stream takes damage, every frame it stays there;
    /// nothing here ignites scenery, and damage stops the instant a target
    /// steps out -- there is no lingering burn/DoT status left behind.
    ///
    /// NO FUEL OR AMMO -- not asked for, and Jetpack.cs already set this
    /// project's own precedent for "hold to activate, no resource to
    /// track" among its new mechanics ("INFINITE FUEL, on purpose").
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Flamethrower : MonoBehaviour, IEquippableItem, IEquippableFlamethrower, ICosmeticMountable
    {
        public string ItemId => ItemRegistry.Flamethrower;

        /// <summary>
        /// Short, on purpose -- a flamethrower is a close-range weapon in
        /// reality (a few metres at most before the fuel-air stream loses
        /// coherence), and pistol.gd's own 60m hitscan range would read as
        /// absurd for a stream of fire you can visibly see falling short.
        /// </summary>
        public const float Range = 7.0f;

        /// <summary>Half-angle of the stream's own damage cone -- narrower than the visual spread, so the flame reads wider than what it can actually burn.</summary>
        public const float ConeHalfAngleDegrees = 10.0f;

        /// <summary>
        /// Damage per second of continuous contact, before Firearm Damage scaling
        /// -- roughly 1.5x a pistol shot's worth of damage per second
        /// (Damage=20 at FireRatePerSecond=5.5 -> ~110/s at point-blank
        /// full-auto), deliberately short of that: a flamethrower rewards
        /// staying on target over several seconds rather than deleting
        /// something the instant the stream touches it.
        /// </summary>
        public const float DamagePerSecond = 30.0f;

        public const float InteractRange = 2.5f;
        public const float LookDotThreshold = 0.82f; // cos(35 degrees), matching PistolWeapon's own gate

        public static readonly Vector3 GripHandPosition = new Vector3(0.02f, -0.05f, 0.10f);
        public static readonly Vector3 GripHandRotationEuler = Vector3.zero;
        public const float GripScale = 1.0f;

        [Tooltip("Plays while the trigger is held, stopped otherwise -- same Play()/Stop() shape as Jetpack.FlameEffect. The core flame and smoke layers are children of this one in the prefab, so Play()/Stop()'s own default withChildren:true starts and stops all three together -- no separate field needed for either.")]
        public ParticleSystem FlameEffect;

        [Tooltip("Flickers while firing -- see TickFlicker's own doc comment.")]
        public Light FlameLight;

        [Tooltip("Loops while the trigger is held. No clip provided yet -- silent until one is wired, matching every other optional-audio field in this project.")]
        public AudioSource FireLoopAudio;

        public LayerMask TargetLayers = ~0;

        private SphereCollider _pickupTrigger;
        private Transform _nearbyPlayer;
        private bool _equipped;
        private float _flickerPhase;

        /// <summary>Reused every tick rather than allocated fresh, since this runs every frame the trigger is held (unlike a swing's own once-per-attack list).</summary>
        private readonly List<IDamageable> _hitThisTick = new List<IDamageable>();

        public bool IsFiring { get; private set; }

        private void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
        }

        private void Update()
        {
            if (_equipped)
            {
                return;
            }
            TickPickup();
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerVitals vitals = other.GetComponentInParent<PlayerVitals>();
            if (vitals != null)
            {
                _nearbyPlayer = vitals.transform;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!CityX.Character.InteractionReach.IsBodyLeaving(other))
            {
                return;   // a limb's collider swinging out of the trigger is not the player walking away
            }
            if (other.transform == _nearbyPlayer || other.GetComponentInParent<PlayerVitals>() != null)
            {
                _nearbyPlayer = null;
            }
        }

        private void TickPickup()
        {
            if (_nearbyPlayer == null)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            bool ePressed = keyboard != null && keyboard.eKey.wasPressedThisFrame;
            TryPickup(_nearbyPlayer, ePressed);
        }

        /// <summary>
        /// The actual pickup decision, factored out so a test can drive it
        /// with an explicit "was E pressed" flag -- same split
        /// PistolWeapon.TryPickup already establishes and for the same
        /// reason (Keyboard.current is null outside a real Input System
        /// device).
        /// </summary>
        public bool TryPickup(Transform player, bool ePressed)
        {
            if (!ePressed)
            {
                return false;
            }

            Vector3 toPlayer = transform.position - player.position;
            if (toPlayer.sqrMagnitude > InteractRange * InteractRange)
            {
                return false;
            }

            Transform lookSource = InteractionReach.LookSource(player);
            Vector3 toSelf = transform.position - lookSource.position;
            if (toSelf.sqrMagnitude > 0.0001f &&
                Vector3.Dot(lookSource.forward, toSelf.normalized) < LookDotThreshold)
            {
                return false;
            }

            var inventory = player.GetComponentInParent<Inventory>();
            if (inventory == null)
            {
                return false;
            }
            if (inventory.AddItem(ItemRegistry.Flamethrower, 1))
            {
                Destroy(gameObject);
                return true;
            }
            return false;
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            var animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = GripHandPosition;
                transform.localEulerAngles = GripHandRotationEuler;
                transform.localScale = Vector3.one * GripScale;
            }

            _equipped = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }

            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedFlamethrower = this;
            }
        }

        public void OnUnequip(GameObject player)
        {
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null && ReferenceEquals(input.EquippedFlamethrower, this))
            {
                input.EquippedFlamethrower = null;
            }
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            CosmeticMount.ToBone(transform, body, HumanBodyBones.RightHand, GripHandPosition, GripHandRotationEuler, GripScale);
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        public void Tick(bool triggerHeld, Vector3 origin, Vector3 direction, GameObject wielder, float deltaTime)
        {
            IsFiring = triggerHeld;

            if (FlameEffect != null)
            {
                if (triggerHeld && !FlameEffect.isEmitting)
                {
                    FlameEffect.Play();
                }
                else if (!triggerHeld && FlameEffect.isEmitting)
                {
                    FlameEffect.Stop();
                }
            }
            if (FireLoopAudio != null)
            {
                if (triggerHeld && !FireLoopAudio.isPlaying)
                {
                    FireLoopAudio.Play();
                }
                else if (!triggerHeld && FireLoopAudio.isPlaying)
                {
                    FireLoopAudio.Stop();
                }
            }

            TickFlicker(triggerHeld, deltaTime);

            if (triggerHeld && direction.sqrMagnitude > 0.0001f)
            {
                ApplyStreamDamage(origin, direction.normalized, wielder, deltaTime);
            }
        }

        /// <summary>
        /// A real flame's own light never sits still -- Perlin noise over
        /// time rather than Random.Range every frame, so the flicker is
        /// smooth (a wandering value) instead of a strobe (a fresh random
        /// jump every single frame reads as flickering fluorescent tube,
        /// not fire). Off entirely, not just dim, the instant the trigger
        /// releases -- an idle flamethrower casts no light.
        /// </summary>
        private void TickFlicker(bool triggerHeld, float deltaTime)
        {
            if (FlameLight == null)
            {
                return;
            }
            if (!triggerHeld)
            {
                FlameLight.intensity = 0.0f;
                return;
            }
            _flickerPhase += deltaTime * FlickerSpeed;
            float noise = Mathf.PerlinNoise(_flickerPhase, 0.0f);
            FlameLight.intensity = Mathf.Lerp(FlickerMinIntensity, FlickerMaxIntensity, noise);
        }

        public const float FlickerSpeed = 14.0f;
        public const float FlickerMinIntensity = 2.0f;
        public const float FlickerMaxIntensity = 4.5f;

        /// <summary>
        /// Every living, non-self IDamageable within Range and inside the
        /// stream's own cone takes DamagePerSecond * deltaTime this frame --
        /// applied every frame rather than a lump sum on a periodic tick, so
        /// pulling off target stops the damage on the very next frame
        /// rather than up to a whole tick period late.
        ///
        /// A SINGLE OverlapSphere COVERING THE WHOLE RANGE, centred halfway
        /// along the aim direction with radius = Range/2, rather than
        /// sampling a true cone -- matches MeleeAttacker.ContactsInReach's
        /// own "sphere first, angle filter second" shape, just centred
        /// forward of the body instead of on it. The angle filter below is
        /// what actually gives it a cone's shape; the sphere is only ever
        /// the broad-phase candidate set.
        /// </summary>
        private void ApplyStreamDamage(Vector3 origin, Vector3 direction, GameObject wielder, float deltaTime)
        {
            // RAIN PUTS FIRE OUT -- open-world games from Breath of the Wild
            // on treat rain as a real opponent of flame. A stream falling out
            // in the rain loses half its bite and a quarter of its reach in a
            // downpour; the same flamethrower under a roof or a highway deck
            // is unaffected (CurrentWeather.RainOn is zero under cover).
            float flame = Weather.FlameStrength(CurrentWeather.RainOn(origin));
            float reach = Range * (0.5f + 0.5f * flame);

            Vector3 center = origin + direction * (reach * 0.5f);
            Collider[] hits = Physics.OverlapSphere(center, reach * 0.5f, TargetLayers, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
            {
                return;
            }

            float cosLimit = Mathf.Cos(ConeHalfAngleDegrees * Mathf.Deg2Rad);
            IDamageable self = wielder != null ? wielder.GetComponent<IDamageable>() : null;
            // A gun of a kind: Firearm Damage makes it hurt more (2% a point), and Armor Penetration goes with it (2026-10-14).
            PlayerVitals wielderVitals = wielder != null ? wielder.GetComponent<PlayerVitals>() : null;
            int firearmDamage = wielderVitals != null ? wielderVitals.Stats[StatId.FirearmDamage] : 0;
            float penetration = wielderVitals != null ? wielderVitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            float damageThisTick = CombatTuning.DamageFor(DamagePerSecond * deltaTime, firearmDamage) * flame;

            _hitThisTick.Clear();
            foreach (Collider collider in hits)
            {
                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || ReferenceEquals(target, self))
                {
                    continue;
                }
                // One body can own several colliders -- only burn it once per tick.
                if (_hitThisTick.Contains(target))
                {
                    continue;
                }

                Vector3 toTarget = collider.transform.position - origin;
                if (toTarget.sqrMagnitude > 0.0001f &&
                    Vector3.Dot(toTarget.normalized, direction) < cosLimit)
                {
                    continue;   // outside the stream's own cone
                }

                _hitThisTick.Add(target);
                // No hitstun -- matches PistolWeapon.Fire's own choice: a
                // continuous stream that also locked its target in place
                // every single frame would never let anything move again.
                DamageContext.Strike(target, damageThisTick, origin, 0.0f, DamageKind.Physical, penetration);
            }
        }
    }
}
