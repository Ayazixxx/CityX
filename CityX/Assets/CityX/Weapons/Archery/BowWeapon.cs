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
    ///
    /// THE SLINGSHOT is this too (docs/SLINGSHOT.md; owner, 2026-10-30: "make it work similarly to the bow and arrow where you hold to draw and
    /// shoot at full power, make the draw quicker than the bow and just make it shoot a little pebble"), as the crossbow is a gun with its own
    /// profile: <see cref="Kind"/> picks the numbers, the hold and the sounds (<see cref="DrawnProfile"/>). Its "arrow" is a pebble, its string
    /// the bands from the prongs' tips through the pouch, and a pebble is fetched into the pouch after each shot before the next draw.
    /// </summary>
    public sealed class BowWeapon : MonoBehaviour, IEquippableItem, IEquippableGun, IDrawnWeapon, ICosmeticMountable, ICosmeticKeepsRunning
    {
        [Tooltip("The bow, or the slingshot (docs/SLINGSHOT.md): drawn and loosed alike, its numbers and its hold its own (DrawnProfile).")]
        [SerializeField] private DrawnKind _kind;

        public DrawnKind Kind
        {
            get => _kind;
            set => _kind = value;
        }

        public DrawnProfile Profile => DrawnProfile.For(_kind);

        public string ItemId => Profile.ItemId;

        [Tooltip("The arrow (or pebble) loosed (ArrowProjectile on it), and the one shown in the hand and on the string while drawing (a child of the bow).")]
        public GameObject ArrowPrefab;

        public Transform NockedArrow;

        [Tooltip("The string, drawn from the top tip through the nock to the bottom tip every frame.")]
        public LineRenderer String;

        public Transform TopTip;

        public Transform BottomTip;

        /// <summary>Where the bow's string rests, in its frame: just behind the grip.</summary>
        public static readonly Vector3 StringRest = new Vector3(0.0f, 0.0f, -0.08f);

        [Tooltip("Where the string (a slingshot's pouch) rests, in the weapon's frame.")]
        public Vector3 RestPoint = StringRest;

        /// <summary>The string's twang after a loose: this far forward and back at first, this many times a second, gone in this long.</summary>
        public const float TwangMetres = 0.05f;

        public const float TwangHz = 22.0f;

        public const float TwangSeconds = 0.2f;

        /// <summary>A slingshot's pouch hangs slack until the draw, and is in the hand by this share of the pull.</summary>
        public const float PouchInHandByPull = 0.25f;

        public event System.Action<GunShot> ShotResolved;

        private PlayerLocomotion _locomotion;
        private Archery _archery;
        private AudioSource _creak;
        private bool _drawing;
        private bool _up;
        private float _draw;
        private float _sendIn;
        private float _twangLeft;
        private float _reload;
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

        public void LetDown()
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
        }

        public void OnUnequip(GameObject player)
        {
            LetDown();
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
            _locomotion?.SetBow(transform, Profile.Style);
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
            _reload = Mathf.Max(0.0f, _reload - deltaTime);
            // A draw starts only once the next shot is in hand (a slingshot's pebble fetched into the pouch); held till then, it starts then.
            if (held && (_drawing || _reload <= 0.0f))
            {
                if (!_drawing)
                {
                    _drawing = true;
                    _draw = 0.0f;
                    _sendIn = 0.0f;
                    StartCreak();
                }
                _draw = Mathf.Min(1.0f, _draw + deltaTime / Profile.FullDrawSeconds);
            }
            else if (!held && _drawing)
            {
                Loose(origin, _aim, aiming, wielder);
                _reload = Profile.ReloadSeconds;
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

        /// <summary>The shot goes: the hit decided from the camera as a gun's, shown from the nock; the twang and the flick; the HUD told (GunFeel).</summary>
        private void Loose(Vector3 origin, Vector3 direction, bool aiming, GameObject wielder)
        {
            DrawnProfile profile = Profile;
            float draw = profile.Clamp(_draw);
            PlayerVitals shooter = wielder != null ? wielder.GetComponent<PlayerVitals>() : null;
            int firearmDamage = shooter != null ? shooter.Stats[StatId.FirearmDamage] : 0;
            float penetration = shooter != null ? shooter.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            float damage = CombatTuning.DamageFor(profile.DamageFor(draw), firearmDamage);
            Vector3 nock = _locomotion != null ? _locomotion.BowNock : transform.position;
            Collider[] own = wielder != null ? wielder.GetComponentsInChildren<Collider>() : null;
            // The Fire Enhancement: an arrow burns (a pebble has no shaft to burn along: it leaves the enhancement waiting).
            bool burning = _kind == DrawnKind.Bow && FireEnhancementAbility.TakeShot(wielder);
            ArrowProjectile.Loose(ArrowPrefab, origin, nock, direction, profile.FlatRangeFor(draw), profile.FallSpeedFor(draw), damage,
                penetration, wielder, own, cosmetic: false, out bool hit, out Vector3 point, burning);
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(ArcherySounds.LooseOf(_kind), nock, 0.8f);
            }
            _locomotion?.LooseBow();
            Twang();
            _archery?.TellLoosed(nock, direction, draw, burning, _kind);
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

        public string GetAmmoText() => _drawing ? $"DRAW {Mathf.RoundToInt(_draw * 100.0f)}%" : Profile.AmmoName;

        public bool IsBarrelBlocked(Vector3 origin, Vector3 direction, GameObject wielder) => false;

        public void AimTrace(Vector3 origin, Vector3 direction, GameObject wielder, out Vector3 point, out bool barrelBlocked)
        {
            barrelBlocked = false;
            point = origin + direction * Profile.FullFlatRange;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, Profile.FullFlatRange, ~0, QueryTriggerInteraction.Ignore))
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
            bool inHand = up && _reload <= 0.0f && _locomotion != null && _locomotion.BowArrowInHand;
            Vector3 hand = _locomotion != null ? transform.InverseTransformPoint(_locomotion.BowNock) : RestPoint;

            bool slingshot = Profile.Style.Slingshot;
            Vector3 nock = RestPoint;
            if (pull > 0.01f)
            {
                nock = slingshot ? Vector3.Lerp(RestPoint, hand, Mathf.SmoothStep(0.0f, 1.0f, pull / PouchInHandByPull)) : hand;
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
                    NockedArrow.localPosition = slingshot ? nock : hand;
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
