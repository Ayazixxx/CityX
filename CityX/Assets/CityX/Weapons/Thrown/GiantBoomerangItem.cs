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
    public sealed class GiantBoomerangItem : MonoBehaviour, IEquippableItem, IEquippableWeapon, ICosmeticMountable, IAimedThrow
    {
        /// <summary>
        /// On the back at a slant: the grip up behind the right shoulder where the draw's hand closes on it (giant_boomerang_pose.py's GRAB), the length running
        /// down across the back to the left hip, the flat face to the back.
        /// </summary>
        public static readonly BackCarry OnTheBack = new BackCarry(new Vector3(0.24f, 1.12f, -0.36f), new Vector3(-0.42f, -0.9f, 0.0f), Vector3.back);

        /// <summary>
        /// In the right hand: the grip in the fist, turned so that in the aim (the draw's last frame) it lies flat out behind the shoulder, a little out to the
        /// right. Measured from the aim's hand on the real body (GiantBoomerangCapture), not guessed.
        /// </summary>
        public static readonly Vector3 GripHandPosition = new Vector3(0.09f, 0.0f, 0.0f);

        public static readonly Vector3 GripHandRotationEuler = new Vector3(-55.2f, 220.2f, 46.4f);

        /// <summary>Taken off the back or put back on it, it glides from where it was into the hand's grip (or the back's) over this long, seconds.</summary>
        public const float HandOverSeconds = 0.12f;

        /// <summary>The child that holds the model, set off from the grip (BoomerangBuilder.BuildGiant).</summary>
        public const string ModelName = "Model";

        [Tooltip("Its outline in its own frame (the grip at the origin, z along it, y out of its face), for the hair it lies over (BackClip): one tip x, z and the elbow x, z. Set by BoomerangBuilder.")]
        public Vector4 OutlineTipAndElbow;

        [Tooltip("The other tip x, z, each arm's half width and half its thickness. Set by BoomerangBuilder.")]
        public Vector4 OutlineTipWidthDepth;

        private BoomerangThrower _thrower;
        private GameObject _body;
        private bool _inHand;
        private Vector3 _toPosition;
        private Quaternion _toRotation;
        private float _handOver;

        public string ItemId => ItemRegistry.GiantBoomerang;

        public float GetMeleeDamage() => 0.0f;

        public float MeleeCooldownSeconds => BoomerangRules.ThrowEverySeconds;

        public void RegisterLandedMeleeHit()
        {
        }

        public bool CanThrow() => true;

        public float ThrowSpeed => GiantBoomerangRules.OutSpeed;

        public string MovesetId => null;

        public ComboStage? HeavyStage => null;

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            Carry(player);
            var melee = player.GetComponent<MeleeAttacker>();
            if (melee != null)
            {
                melee.EquippedWeapon = this;
            }
        }

        public void OnUnequip(GameObject player)
        {
            var melee = player != null ? player.GetComponent<MeleeAttacker>() : null;
            if (melee != null && ReferenceEquals(melee.EquippedWeapon, this))
            {
                melee.EquippedWeapon = null;
            }
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
            float damage = CombatTuning.DamageFor(GiantBoomerangRules.Damage, vitals != null ? vitals.Strength : 0);
            float penetration = vitals != null ? vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            thrower.StartGiantThrow(BoomerangItem.AimedWay(origin, direction, player, BoomerangProfile.Giant.Range), damage, penetration);
        }

        private void Carry(GameObject body)
        {
            _body = body;
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
                _thrower.OutChanged -= Show;
                if (_thrower.GiantMoves != null) _thrower.GiantMoves.HandChanged -= Show;
            }
            _thrower = thrower;
            if (_thrower != null)
            {
                _thrower.OutChanged += Show;
                _thrower.GiantMoves.HandChanged += Show;
            }
            Place(true);
        }

        private void Show() => Place(false);

        /// <summary>On the back, in the hand, or (out) nowhere; <paramref name="anew"/> puts it where it goes even if it is already there.</summary>
        private void Place(bool anew)
        {
            bool out_ = _thrower != null && _thrower.IsOut;
            foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
            {
                part.enabled = !out_;
            }
            bool inHand = _thrower != null && _thrower.GiantMoves.InHand;
            // On the back and there to be seen, the hair under it is tucked beneath it (BackClip); in the hand or in the air, nothing is.
            if (!inHand && !out_ && _body != null)
            {
                BackClip.Show(transform, OutlineTipAndElbow, OutlineTipWidthDepth);
            }
            else
            {
                BackClip.Hide(transform);
            }
            if ((!anew && inHand == _inHand) || _body == null)
            {
                return;
            }
            _inHand = inHand;
            Vector3 wasAt = transform.position;
            Quaternion wasTurned = transform.rotation;
            if (inHand)
            {
                CosmeticMount.ToBone(transform, _body, HumanBodyBones.RightHand, GripHandPosition, GripHandRotationEuler, 1.0f);
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
                Transform model = transform.Find(ModelName);
                Quaternion turn = model != null ? caughtTurned * Quaternion.Inverse(model.localRotation) : caughtTurned;
                transform.SetPositionAndRotation(model != null ? caughtAt - turn * model.localPosition : caughtAt, turn);
                _handOver = HandOverSeconds;
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
        }

        private void LateUpdate()
        {
            if (_handOver <= 0.0f)
            {
                return;
            }
            float share = Mathf.Min(1.0f, Time.deltaTime / _handOver);
            _handOver -= Time.deltaTime;
            transform.localPosition = Vector3.Lerp(transform.localPosition, _toPosition, share);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, _toRotation, share);
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
