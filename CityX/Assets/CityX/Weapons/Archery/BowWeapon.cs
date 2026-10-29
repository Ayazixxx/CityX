using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE BOW (owner, 2026-10-28; docs/ARCHERY.md): held in the left hand. Right-click aims it (the gun's aim camera; PlayerInputReader treats it
    /// as a gun), left-click held draws it (<see cref="ArcheryRules.FullDrawSeconds"/> to full: the first part the nock, the rest the pull), and
    /// letting go looses an arrow: a hitscan over a flat range that grows with the draw, the arrow then falling (<see cref="ArrowProjectile"/>),
    /// its damage growing with the draw too. A tap is a weak lob. Arrows are not counted (yet). The body poses both arms itself
    /// (PlayerLocomotion.Bow.cs), so the gun's own arm pose stays off (<see cref="IDrawnWeapon"/>). The string follows the string hand once the
    /// arrow is nocked and twangs back on the loose; the arrow rides the hand from the quiver to the string; here and on other players'
    /// copies, which see the draw and the arrow through <see cref="Archery"/> on the body.
    /// </summary>
    public sealed class BowWeapon : MonoBehaviour, IEquippableItem, IEquippableGun, IDrawnWeapon, ICosmeticMountable, ICosmeticKeepsRunning
    {
        public string ItemId => ItemRegistry.Bow;

        [Tooltip("The arrow loosed (ArrowProjectile on it), and the one shown in the hand and on the string while drawing (a child of the bow).")]
        public GameObject ArrowPrefab;

        public Transform NockedArrow;

        [Tooltip("The string, drawn from the top tip through the nock to the bottom tip every frame.")]
        public LineRenderer String;

        public Transform TopTip;

        public Transform BottomTip;

        /// <summary>Where the string rests, in the bow's frame: just behind the grip.</summary>
        public static readonly Vector3 StringRest = new Vector3(0.0f, 0.0f, -0.08f);

        /// <summary>The string's twang after a loose: this far forward and back at first, this many times a second, gone in this long.</summary>
        public const float TwangMetres = 0.05f;

        public const float TwangHz = 22.0f;

        public const float TwangSeconds = 0.2f;

        public event System.Action<GunShot> ShotResolved;

        private PlayerLocomotion _locomotion;
        private Archery _archery;
        private AudioSource _creak;
        private bool _drawing;
        private bool _up;
        private float _draw;
        private float _sendIn;
        private float _twangLeft;
        private Vector3 _aim = Vector3.forward;

        public bool IsAutomatic => false;

        public bool IsReloading => false;

        /// <summary>How far drawn, 0..1 (the raw draw, before the least draw a loose has).</summary>
        public float DrawShare => _drawing ? _draw : 0.0f;

        public bool IsDrawing => _drawing;

        // --- in the hand ---

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            Hold(player);
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedGun = this;
            }
            var feel = player.GetComponent<GunFeel>();
            if (feel == null)
            {
                feel = player.AddComponent<GunFeel>();
            }
            feel.Attach(this);
            _archery = player.GetComponent<Archery>();
        }

        public void OnUnequip(GameObject player)
        {
            if (_drawing)
            {
                _drawing = false;
                _draw = 0.0f;
                StopCreak();
            }
            if (_up)
            {
                _up = false;
                _archery?.TellLowered();
            }
            var input = player != null ? player.GetComponent<PlayerInputReader>() : null;
            if (input != null && ReferenceEquals(input.EquippedGun, this))
            {
                input.EquippedGun = null;
            }
            Release();
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            Hold(body);
        }

        public void UnmountCosmetic(GameObject body)
        {
            Release();
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        /// <summary>Into the left hand (the body places it from there every frame: PlayerLocomotion.Bow.cs), the body told it holds a bow.</summary>
        private void Hold(GameObject body)
        {
            var animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            Transform hand = animator != null && animator.avatar != null ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;
            }
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
            _locomotion = body != null ? body.GetComponent<PlayerLocomotion>() : null;
            _locomotion?.SetBow(transform);
        }

        private void Release()
        {
            _locomotion?.ClearBow(transform);
        }

        private void OnDestroy()
        {
            Release();
        }

        // --- the draw and the loose (IDrawnWeapon) ---

        public void Draw(bool held, Vector3 origin, Vector3 direction, bool aiming, GameObject wielder, float deltaTime)
        {
            if (direction.sqrMagnitude > 1e-6f)
            {
                _aim = direction.normalized;
            }
            if (held)
            {
                if (!_drawing)
                {
                    _drawing = true;
                    _draw = 0.0f;
                    _sendIn = 0.0f;
                    StartCreak();
                }
                _draw = Mathf.Min(1.0f, _draw + deltaTime / ArcheryRules.FullDrawSeconds);
            }
            else if (_drawing)
            {
                Loose(origin, _aim, aiming, wielder);
                _drawing = false;
                _draw = 0.0f;
                StopCreak();
            }

            bool up = aiming || _drawing;
            if (up)
            {
                _locomotion?.HoldBow(_aim, DrawShare);
                _sendIn -= deltaTime;
                if (_sendIn <= 0.0f)
                {
                    _sendIn = Archery.DrawSendSeconds;
                    _archery?.TellDrawing(_aim, DrawShare);
                }
            }
            else if (_up)
            {
                _archery?.TellLowered();
            }
            _up = up;
        }

        /// <summary>The arrow goes: the hit decided from the camera as a gun's, shown from the nock; the twang and the flick; the HUD told (GunFeel).</summary>
        private void Loose(Vector3 origin, Vector3 direction, bool aiming, GameObject wielder)
        {
            float draw = ArcheryRules.Clamp(_draw);
            PlayerVitals shooter = wielder != null ? wielder.GetComponent<PlayerVitals>() : null;
            int firearmDamage = shooter != null ? shooter.Stats[StatId.FirearmDamage] : 0;
            float penetration = shooter != null ? shooter.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            float damage = CombatTuning.DamageFor(ArcheryRules.DamageFor(draw), firearmDamage);
            Vector3 nock = _locomotion != null ? _locomotion.BowNock : transform.position;
            Collider[] own = wielder != null ? wielder.GetComponentsInChildren<Collider>() : null;
            ArrowProjectile.Loose(ArrowPrefab, origin, nock, direction, ArcheryRules.FlatRangeFor(draw), ArcheryRules.FallSpeedFor(draw), damage,
                penetration, wielder, own, cosmetic: false, out bool hit, out Vector3 point);
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(ArcherySounds.Twang, nock, 0.8f);
            }
            _locomotion?.LooseBow();
            Twang();
            _archery?.TellLoosed(nock, direction, draw);
            ShotResolved?.Invoke(new GunShot
            {
                Fired = true,
                HitSomething = hit,
                HitDamageable = hit && HitSomeone(point),
                Point = point,
                Aiming = aiming,
            });
        }

        /// <summary>The string springs back and shivers (after a loose, here or on a copy).</summary>
        public void Twang()
        {
            _twangLeft = TwangSeconds;
        }

        private static bool HitSomeone(Vector3 point)
        {
            foreach (Collider c in Physics.OverlapSphere(point, 0.15f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.GetComponentInParent<IDamageable>() != null)
                {
                    return true;
                }
            }
            return false;
        }

        // --- IEquippableGun, for the HUD and the aim (the reader never calls Fire on a drawn weapon) ---

        public bool Fire(Vector3 origin, Vector3 direction, bool aiming, GameObject wielder) => false;

        public void TryReload()
        {
        }

        public string GetAmmoText() => _drawing ? $"DRAW {Mathf.RoundToInt(_draw * 100.0f)}%" : "ARROWS";

        public bool IsBarrelBlocked(Vector3 origin, Vector3 direction, GameObject wielder) => false;

        public void AimTrace(Vector3 origin, Vector3 direction, GameObject wielder, out Vector3 point, out bool barrelBlocked)
        {
            barrelBlocked = false;
            point = origin + direction * ArcheryRules.FullFlatRange;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, ArcheryRules.FullFlatRange, ~0, QueryTriggerInteraction.Ignore))
            {
                if (wielder != null && hit.collider.transform.IsChildOf(wielder.transform))
                {
                    continue;
                }
                if ((hit.point - origin).sqrMagnitude < (point - origin).sqrMagnitude)
                {
                    point = hit.point;
                }
            }
        }

        // --- the string and the arrow in the hand, here and on copies ---

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            _twangLeft = Mathf.Max(0.0f, _twangLeft - deltaTime);
            bool up = _locomotion != null && _locomotion.BowRaise > 0.5f;
            float pull = up && _locomotion != null ? _locomotion.BowPull : 0.0f;
            bool inHand = up && _locomotion != null && _locomotion.BowArrowInHand;
            Vector3 hand = _locomotion != null ? transform.InverseTransformPoint(_locomotion.BowNock) : StringRest;

            Vector3 nock = StringRest;
            if (pull > 0.01f)
            {
                nock = hand;
            }
            else if (_twangLeft > 0.0f)
            {
                float t = TwangSeconds - _twangLeft;
                nock += Vector3.forward * (TwangMetres * Mathf.Sin(2.0f * Mathf.PI * TwangHz * t) * (_twangLeft / TwangSeconds));
            }
            if (String != null && TopTip != null && BottomTip != null)
            {
                String.positionCount = 3;
                String.useWorldSpace = false;
                String.SetPosition(0, TopTip.localPosition);
                String.SetPosition(1, nock);
                String.SetPosition(2, BottomTip.localPosition);
            }
            if (NockedArrow != null)
            {
                if (NockedArrow.gameObject.activeSelf != inHand)
                {
                    NockedArrow.gameObject.SetActive(inHand);
                }
                if (inHand)
                {
                    NockedArrow.localPosition = hand;
                    NockedArrow.localRotation = Quaternion.identity;
                }
            }
        }

        private void StartCreak()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            if (_creak == null)
            {
                _creak = gameObject.AddComponent<AudioSource>();
                _creak.clip = ArcherySounds.Creak;
                _creak.spatialBlend = 1.0f;
                _creak.volume = 0.5f;
            }
            _creak.Play();
        }

        private void StopCreak()
        {
            if (_creak != null && _creak.isPlaying)
            {
                _creak.Stop();
            }
        }
    }
}
