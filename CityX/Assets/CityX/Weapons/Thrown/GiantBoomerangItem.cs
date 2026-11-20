using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE GIANT BOOMERANG (docs/BOOMERANG.md; owner, 2026-11-12: "make a giant version of the boomerang like the girl from inuyasha", as tall as the player,
    /// carried on the back; "make an animation when you hold right click where it grabs the boomerang and does like an aiming it pose and aims in. also make it
    /// so you can press click and it throws it right away"). It rides on the back (<see cref="OnTheBack"/>) until the hand takes it: right-click draws it into
    /// the aim and a click throws it from there, or a click alone throws it straight off the back. It is not spent: gone until it comes back, then caught in the
    /// hand (owner, 2026-11-12: "he actually catches it and puts it back or just holds it in his hand if you holding right click"), held in the aim if right-click
    /// is, else put back on the back. Seen on other players (ICosmeticMountable); their copy draws, aims, throws and catches it through the body's
    /// <see cref="BoomerangThrower"/>.
    /// </summary>
    public sealed class GiantBoomerangItem : MonoBehaviour, IEquippableItem, IEquippableWeapon, ICosmeticMountable, IAimedThrow, IHandGripStyle
    {
        /// <summary>
        /// On the back at a slant: the grip up behind the right shoulder where the draw's hand closes on it (giant_boomerang_pose.py's GRAB), the length running
        /// down across the back to the left hip, the flat face to the back.
        /// </summary>
        public static readonly BackCarry OnTheBack = new BackCarry(new Vector3(0.24f, 1.12f, -0.36f), new Vector3(-0.42f, -0.9f, 0.0f), Vector3.back);

        /// <summary>
        /// In the right hand, by its side (owner, 2026-11-20, of the bone one: "make it so he holds it by the side instead of his fingers clipping through"):
        /// the palm up under its inner edge at the leather, the edge 6 cm to the thumb's side of the palm's middle, the thumb folded over it
        /// (<see cref="SideGrip"/>), its length running on from the forearm, its middle plane 3 cm over the palm, the face that is up as it flies turned
        /// down (the hand that takes it off the back is on that face). Worked out from the aim's hand (the draw's last frame, giant_boomerang_pose.py's
        /// AIM: the lunge, the arm back and low), where it trails level behind. Held by its middle with the forearm stood up under it, the hand went up
        /// through the blade and the fingers stood out of its top.
        /// </summary>
        public static readonly Vector3 GripHandPosition = new Vector3(0.1000f, -0.0842f, -0.1038f);

        public static readonly Vector3 GripHandRotationEuler = new Vector3(0.0f, 90.0f, -6.6f);

        /// <summary>Taken off the back or put back on it, it glides from where it was into the hand's grip (or the back's) over this long, seconds.</summary>
        public const float HandOverSeconds = 0.12f;

        /// <summary>The child that holds the model, set off from the grip (BoomerangBuilder.BuildGiant).</summary>
        public const string ModelName = "Model";

        [Tooltip("Which giant boomerang this is: its item, its blow and its model in the air. Set by BoomerangBuilder.")]
        public GiantBoomerangModel Model;

        [Tooltip("On the back its model lies at BackModelPosition/Rotation (in this frame) rather than where the hand holds it. Set by BoomerangBuilder.")]
        public bool HasBackPose;

        [Tooltip("Where its model lies on the back: the Iron Spiked one along the bone one's line, turned over. Set by BoomerangBuilder.")]
        public Vector3 BackModelPosition;

        public Quaternion BackModelRotation = Quaternion.identity;

        [Tooltip("Its outline in its own frame as it lies on the back (the grip at the origin, z along it, y out of its face), for the hair it lies over (BackClip): one tip x, z and the elbow x, z. Set by BoomerangBuilder.")]
        public Vector4 OutlineTipAndElbow;

        [Tooltip("The other tip x, z, each arm's half width at the elbow and at its tip (it narrows from the elbow). Set by BoomerangBuilder.")]
        public Vector4 OutlineTipAndWidths;

        [Tooltip("It hangs on the back at HangPosition/HangEuler (from the spine bone) rather than OnTheBack: the Iron Spiked one, where the owner set it by hand. Set by BoomerangBuilder.")]
        public bool HasOwnHang;

        public Vector3 HangPosition;

        public Vector3 HangEuler;

        [Tooltip("The hand holds it at HoldPosition/HoldEuler (from the right hand bone) rather than by the bone one's grip: the Iron Spiked one, the palm on top of its wrap. Set by BoomerangBuilder.")]
        public bool HasOwnHold;

        public Vector3 HoldPosition;

        public Vector3 HoldEuler;

        private BoomerangThrower _thrower;
        private GameObject _body;
        private bool _inHand;
        private Vector3 _toPosition;
        private Quaternion _toRotation;
        private float _handOver;

        /// <summary>Its model and where the hand holds it (the prefab's own place), for one that lies elsewhere on the back (<see cref="HasBackPose"/>).</summary>
        private Transform _model;
        private Vector3 _gripModelPosition;
        private Quaternion _gripModelRotation;
        private Transform _spine;

        /// <summary>
        /// One that lies elsewhere on the back (<see cref="HasBackPose"/>) is handed between where it lies and the hand over this long, seconds. Its moves
        /// put the hand's hold exactly on it as it hangs (iron_spiked_moves.py's fitted grab), so this only smooths a grab made before a move has the whole
        /// body.
        /// </summary>
        public const float GlideSeconds = 0.08f;

        /// <summary>Seconds of that still to go, and where its model was when it set off, from the spine bone.</summary>
        private float _glide;
        private Vector3 _glideFromPosition;
        private Quaternion _glideFromRotation;

        /// <summary>The wielder's melee, while equipped: it fights bare-handed while the boomerang is out.</summary>
        private MeleeAttacker _melee;

        /// <summary>
        /// The hand on the bone one's side (<see cref="GripHandPosition"/>): the fingers laid out flat under it (the modelled curl taken out) and the thumb
        /// folded up over its edge. Closed as round a handle, the fingers came up through the blade.
        /// </summary>
        public static FingerAmounts SideGrip => new FingerAmounts(0.0f, 0.0f, 0.0f, 0.0f, 0.0f)
        {
            Authored = 1.0f,
            IndexRootDegrees = -20.0f,
            IndexTipDegrees = -30.0f,
            FingerRootDegrees = -20.0f,
            FingerTipDegrees = -30.0f,
            ThumbRootDegrees = 75.0f,
            ThumbTipDegrees = 45.0f,
        };

        /// <summary>The Iron Spiked one is gripped as a handle is (its fingers fold into its leather); the bone one by its side.</summary>
        public FingerAmounts StrongHandGrip => HasOwnHold ? FingerAmounts.Wrap(1.0f) : SideGrip;

        public FingerAmounts SupportHandGrip => FingerAmounts.Wrap(1.0f);

        public string ItemId => Model == GiantBoomerangModel.IronSpiked ? ItemRegistry.IronSpikedGiantBoomerang : ItemRegistry.GiantBoomerang;

        public float GetMeleeDamage() => 0.0f;

        public float MeleeCooldownSeconds => BoomerangRules.ThrowEverySeconds;

        public void RegisterLandedMeleeHit()
        {
        }

        public bool CanThrow() => true;

        public bool InHand => _thrower == null || !_thrower.IsOut;

        public float ThrowSpeed => GiantBoomerangRules.OutSpeed;

        public string MovesetId => null;

        public ComboStage? HeavyStage => null;

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            _melee = player.GetComponent<MeleeAttacker>();
            Carry(player);
            ArmTheFists();
        }

        public void OnUnequip(GameObject player)
        {
            if (_melee != null && ReferenceEquals(_melee.EquippedWeapon, this))
            {
                _melee.EquippedWeapon = null;
            }
            _melee = null;
            if (_thrower != null)
            {
                _thrower.StopGiantMoves();
            }
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            Carry(body);
        }

        public void UnmountCosmetic(GameObject body)
        {
            Watch(null);
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        public void SetAiming(GameObject player, bool aiming)
        {
            var thrower = player != null ? player.GetComponent<BoomerangThrower>() : null;
            if (thrower != null)
            {
                thrower.SetGiantAiming(aiming);
            }
        }

        public void Throw(GameObject player, Vector3 origin, Vector3 direction)
        {
            var thrower = player != null ? player.GetComponent<BoomerangThrower>() : null;
            if (thrower == null || !thrower.CanThrow)
            {
                return;
            }
            var vitals = player.GetComponent<PlayerVitals>();
            float damage = CombatTuning.DamageFor(GiantBoomerangRules.DamageOf(Model), vitals != null ? vitals.Strength : 0);
            float penetration = vitals != null ? vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            thrower.StartGiantThrow(BoomerangItem.AimedWay(origin, direction, player, BoomerangProfile.Giant.Range), damage, penetration, Model);
        }

        private void Carry(GameObject body)
        {
            if (_model == null)
            {
                _model = transform.Find(ModelName);
                if (_model != null)
                {
                    _gripModelPosition = _model.localPosition;
                    _gripModelRotation = _model.localRotation;
                }
            }
            _body = body;
            Animator animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            _spine = animator != null ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
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
                _thrower.GiantHeldModel = null;
                _thrower.OutChanged -= Show;
                if (_thrower.GiantMoves != null) _thrower.GiantMoves.HandChanged -= Show;
            }
            _thrower = thrower;
            if (_thrower != null)
            {
                _thrower.GiantMoves.Clips = GiantBoomerangMoves.For(Model);
                _thrower.GiantHeldModel = _model;
                _thrower.OutChanged += Show;
                _thrower.GiantMoves.HandChanged += Show;
            }
            Place(true);
        }

        private void Show()
        {
            ArmTheFists();
            Place(false);
        }

        /// <summary>Out, the hands are bare: the fists' own swings, not the boomerang's.</summary>
        private void ArmTheFists()
        {
            if (_melee == null)
            {
                return;
            }
            if (InHand) _melee.EquippedWeapon = this;
            else if (ReferenceEquals(_melee.EquippedWeapon, this)) _melee.EquippedWeapon = null;
        }

        /// <summary>On the back, in the hand, or (out) nowhere; <paramref name="anew"/> puts it where it goes even if it is already there.</summary>
        private void Place(bool anew)
        {
            bool out_ = _thrower != null && _thrower.IsOut;
            foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
            {
                part.enabled = !out_;
            }
            bool inHand = _thrower != null && _thrower.GiantMoves.InHand;
            if ((!anew && inHand == _inHand) || _body == null)
            {
                return;
            }
            _inHand = inHand;
            Vector3 wasAt = transform.position;
            Quaternion wasTurned = transform.rotation;
            bool flies = HasBackPose && !anew && !out_ && _model != null && _spine != null;
            if (flies)
            {
                _glideFromPosition = _spine.InverseTransformPoint(_model.position);
                _glideFromRotation = Quaternion.Inverse(_spine.rotation) * _model.rotation;
            }
            if (inHand)
            {
                CosmeticMount.ToBone(transform, _body, HumanBodyBones.RightHand, HasOwnHold ? HoldPosition : GripHandPosition,
                    HasOwnHold ? HoldEuler : GripHandRotationEuler, 1.0f);
            }
            else if (HasOwnHang)
            {
                CosmeticMount.ToBone(transform, _body, HumanBodyBones.Spine, HangPosition, HangEuler, 1.0f);
            }
            else
            {
                OnTheBack.Hang(transform, _body);
            }
            _toPosition = transform.localPosition;
            _toRotation = transform.localRotation;
            if (inHand && !out_ && _thrower != null && _thrower.TakeCatchPose(out Vector3 caughtAt, out Quaternion caughtTurned))
            {
                // Caught: from where it was flying, its model where the spinning one was, into the grip.
                PlaceModel();
                Quaternion turn = _model != null ? caughtTurned * Quaternion.Inverse(_model.localRotation) : caughtTurned;
                transform.SetPositionAndRotation(_model != null ? caughtAt - turn * _model.localPosition : caughtAt, turn);
                _handOver = HandOverSeconds;
            }
            else if (flies)
            {
                // Where it goes at once; its model flies there from where it was (LateUpdate).
                _handOver = 0.0f;
                _glide = GlideSeconds;
                PlaceModel();
                Glide();
                return;
            }
            else if (!anew && !out_)
            {
                // From where it was, so the grab and the putting back never jump.
                transform.SetPositionAndRotation(wasAt, wasTurned);
                _handOver = HandOverSeconds;
            }
            else
            {
                _handOver = 0.0f;
            }
            PlaceModel();
        }

        /// <summary>Its model as it lies on the back or in the grip: only for one that lies elsewhere on the back (<see cref="HasBackPose"/>).</summary>
        private void PlaceModel()
        {
            if (_model == null || !HasBackPose)
            {
                return;
            }
            _model.localPosition = _inHand ? _gripModelPosition : BackModelPosition;
            _model.localRotation = _inHand ? _gripModelRotation : BackModelRotation;
        }

        private void LateUpdate()
        {
            // The hair under it is tucked beneath it only while it lies settled on the back: the moment the hand takes it, or as it glides back on, the hair
            // is whole again (owner, 2026-11-12: "when you throw it, the hair returns to normal and isnt cropped as you pull the boomerang away"). Asked
            // every frame, so no move can leave the hair cut.
            bool onTheBack = _body != null && !_inHand && _handOver <= 0.0f && _glide <= 0.0f && (_thrower == null || (!_thrower.IsOut && !_thrower.GiantMoves.InHand));
            if (onTheBack)
            {
                BackClip.Show(transform, OutlineTipAndElbow, OutlineTipAndWidths);
            }
            else
            {
                BackClip.Hide(transform);
            }
            if (_handOver > 0.0f)
            {
                float share = Mathf.Min(1.0f, Time.deltaTime / _handOver);
                _handOver -= Time.deltaTime;
                transform.localPosition = Vector3.Lerp(transform.localPosition, _toPosition, share);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, _toRotation, share);
            }
            PlaceModel();
            if (_glide > 0.0f)
            {
                _glide = Mathf.Max(0.0f, _glide - Time.deltaTime);
                Glide();
            }
        }

        /// <summary>Its model on its way between where it was and its placed pose (<see cref="GlideSeconds"/>).</summary>
        private void Glide()
        {
            if (_model == null || _spine == null)
            {
                return;
            }
            float there = Mathf.SmoothStep(0.0f, 1.0f, 1.0f - _glide / GlideSeconds);
            _model.SetPositionAndRotation(Vector3.Lerp(_spine.TransformPoint(_glideFromPosition), _model.position, there),
                Quaternion.Slerp(_spine.rotation * _glideFromRotation, _model.rotation, there));
        }

        private void OnDestroy()
        {
            BackClip.Hide(transform);
            if (_thrower != null)
            {
                _thrower.OutChanged -= Show;
                _thrower.GiantMoves.HandChanged -= Show;
            }
        }
    }
}
