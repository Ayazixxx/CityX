using CityX.Character;
using CityX.Items;
using CityX.World;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE JERRY CAN (owner, 2026-10-28: "make the gas can function like the gta v jerry can and look like it as well, it lets you pour
    /// gasoline then you can light it on fire"; docs/FIRE.md). A red steel can (drawn in code by JerryCanBuilder) carried by its handle in the
    /// right hand; hold left-click to pour: the can tips over the fingers and an amber stream falls from the spout to the ground ahead, laying
    /// gasoline where it lands (FireScene.Pour), <see cref="FireRules.PourLitresPerSecond"/> out of its <see cref="FireRules.CanLitres"/>.
    /// Empty, it pours nothing. Held like the flamethrower (IEquippableFlamethrower: ticked every frame with the trigger), equipped from the
    /// console (/equip jerrycan), seen in other players' hands (ICosmeticMountable) and pouring with its owner (FireSync shows a copy's stream).
    /// </summary>
    public sealed class JerryCanItem : MonoBehaviour, IEquippableItem, IEquippableFlamethrower, ICosmeticMountable, IPouredCan
    {
        public string ItemId => ItemRegistry.JerryCan;

        /// <summary>The handle across the fist: the can hangs down the fingers (hand +X) when the arm hangs, its handle along the thumb (hand +Z).</summary>
        public static readonly Vector3 GripHandPosition = new Vector3(0.1f, -0.03f, 0.0f);

        public static readonly Vector3 GripHandRotationEuler = new Vector3(90.0f, 0.0f, 90.0f);

        /// <summary>Pouring, the can tips over the fingers (about the palm's axis) until its spout points down and ahead, this far, this fast.</summary>
        public const float PourTiltDegrees = 75.0f;

        public const float TiltDegreesPerSecond = 300.0f;

        /// <summary>How long a copy's stream shows after the last word of its owner's pour.</summary>
        public const float ShowSeconds = 0.3f;

        /// <summary>The stream's drops fall this long from the spout to the ground.</summary>
        public const float DropSeconds = 0.4f;

        public const float DropsPerSecond = 45.0f;

        public static readonly Color GasolineColour = new Color(0.9f, 0.7f, 0.25f, 0.85f);

        [Tooltip("Where the gasoline comes out. Set by JerryCanBuilder.")]
        public Transform Spout;

        [Tooltip("Litres left in the can.")]
        public float Litres = FireRules.CanLitres;

        private ParticleSystem _stream;
        private Transform _wearer;
        private Vector3 _landing;
        private bool _hasLanding;
        private bool _equipped;
        private float _showUntil;
        private float _dropDebt;
        private float _tilt;

        /// <summary>Pouring right now (the trigger held with gasoline left).</summary>
        public bool IsPouring { get; private set; }

        /// <summary>Litres laid on the ground so far (for tests).</summary>
        public float Poured { get; private set; }

        private void Awake()
        {
            _stream = BuildStream();
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
                transform.localScale = Vector3.one;
            }
            _wearer = player.transform;
            _equipped = true;
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedFlamethrower = this;
            }
        }

        public void OnUnequip(GameObject player)
        {
            var input = player != null ? player.GetComponent<PlayerInputReader>() : null;
            if (input != null && ReferenceEquals(input.EquippedFlamethrower, this))
            {
                input.EquippedFlamethrower = null;
            }
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            CosmeticMount.ToBone(transform, body, HumanBodyBones.RightHand, GripHandPosition, GripHandRotationEuler, 1.0f);
            _wearer = body.transform;
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        /// <summary>One frame held: pours while the trigger is down and there is gasoline left, onto the ground ahead of the spout.</summary>
        public void Tick(bool triggerHeld, Vector3 origin, Vector3 direction, GameObject wielder, float deltaTime)
        {
            bool pouring = triggerHeld && Litres > 0.0f && Spout != null;
            IsPouring = pouring;
            Tip(pouring, deltaTime);
            if (!pouring)
            {
                return;
            }
            float litres = Mathf.Min(Litres, FireRules.PourLitresPerSecond * deltaTime);
            Litres -= litres;
            Vector3 ahead = direction;
            ahead.y = 0.0f;
            ahead = ahead.sqrMagnitude > 0.0001f ? ahead.normalized : (_wearer != null ? _wearer.forward : Vector3.forward);
            Vector3 over = Spout.position + ahead * FireRules.PourMetresAhead + Vector3.up * 0.3f;
            Transform body = wielder != null ? wielder.transform : _wearer;
            if (BloodSplats.Surface(over, Vector3.down, FireRules.ProbeReachMetres + 1.0f, body, out RaycastHit floor) && floor.normal.y > 0.5f)
            {
                _landing = floor.point;
                _hasLanding = true;
                if (FireScene.Pour(floor.point, litres))
                {
                    Poured += litres;
                }
            }
            if (_hasLanding)
            {
                Stream(deltaTime);
            }
        }

        /// <summary>A copy's can, pouring with its owner: the stream to where their gasoline is landing, for a moment.</summary>
        public void ShowPouring(Vector3 landing)
        {
            _landing = landing;
            _hasLanding = true;
            _showUntil = Time.time + ShowSeconds;
        }

        private void Update()
        {
            if (_equipped)
            {
                return;   // the owner's can is ticked with the trigger
            }
            bool showing = _showUntil > Time.time && _hasLanding;
            Tip(showing, Time.deltaTime);
            if (showing)
            {
                Stream(Time.deltaTime);
            }
        }

        /// <summary>The can tipped over the fingers while it pours (its spout at the far end of the handle swings down and ahead), righted again after.</summary>
        private void Tip(bool pouring, float deltaTime)
        {
            float target = pouring ? PourTiltDegrees : 0.0f;
            if (Mathf.Approximately(_tilt, target))
            {
                return;
            }
            _tilt = Mathf.MoveTowards(_tilt, target, TiltDegreesPerSecond * deltaTime);
            transform.localRotation = Quaternion.AngleAxis(_tilt, Vector3.up) * Quaternion.Euler(GripHandRotationEuler);
        }

        /// <summary>The drops: thrown from the spout so that they land where the gasoline goes, falling under gravity on the way.</summary>
        private void Stream(float deltaTime)
        {
            if (_stream == null || Spout == null)
            {
                return;
            }
            _dropDebt += DropsPerSecond * deltaTime;
            int count = Mathf.Min((int)_dropDebt, 8);
            _dropDebt -= count;
            if (count <= 0)
            {
                return;
            }
            Vector3 from = Spout.position;
            Vector3 velocity = (_landing - from) / DropSeconds - 0.5f * Physics.gravity * DropSeconds;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false, startColor = GasolineColour, startLifetime = DropSeconds + 0.02f };
            for (int i = 0; i < count; i++)
            {
                emit.position = from + Random.insideUnitSphere * 0.015f;
                emit.velocity = velocity + Random.insideUnitSphere * 0.12f;
                emit.startSize = Random.Range(0.035f, 0.06f);
                _stream.Emit(emit, 1);
            }
        }

        /// <summary>The stream's particles: small amber drops stretched along their fall, in the world, under gravity.</summary>
        private ParticleSystem BuildStream()
        {
            var go = new GameObject("Stream");
            go.transform.SetParent(transform, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.0f;
            main.maxParticles = 200;
            main.startSpeed = 0.0f;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0.0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1.2f;
            renderer.sharedMaterial = FireSprites.Particles("GasolineStream", FireSprites.Dot(), ParticleBlend.Alpha, 1);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }
    }
}
