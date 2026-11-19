using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The half of shooting that is not the bullet: camera kick, the
    /// confirm-tick when you hit something, the dry click, and the "barrel
    /// blocked" state the reticle shows. Added to the body by whatever gun is
    /// equipped (PistolWeapon.OnEquip) and driven entirely by the gun's
    /// ShotResolved event, so a gun that raises it gets all of this for free
    /// and the gun code stays ignorant of the camera and the HUD (its assembly
    /// cannot see either).
    ///
    /// WHAT THE THIRD-PERSON-SHOOTER RESEARCH SAID (docs/THIRD_PERSON_PISTOL.md):
    /// a shot needs feedback on three channels -- the gun (kick), the camera
    /// (a small settling recoil), and the reticle + audio (a hit was
    /// CONFIRMED, a kill is distinct) -- and the feedback must never lie about
    /// where the bullet went. That last rule is why there is no reticle bloom
    /// here: bullets land exactly on the crosshair (an explicit ask), so a
    /// crosshair that swelled with movement would be claiming otherwise.
    /// </summary>
    public sealed class GunFeel : MonoBehaviour
    {
        [Tooltip("Volume of the hit-confirm tick, dry click and kill sound.")]
        public float FeedbackVolume = 0.35f;

        [Tooltip("Camera kick scale on top of the gun's own number. 0 turns camera recoil off.")]
        public float CameraRecoilScale = 1.0f;

        private IEquippableGun _gun;
        private IEquippableGun _leftGun;   // the second of a pair (docs/DUAL_PISTOLS.md): its shots kick and confirm the same
        private ThirdPersonCamera _camera;
        private AudioSource _audio;
        private PlayerInputReader _reader;
        private PlayerLocomotion _locomotion;

        private static AudioClip _hitTick;
        private static AudioClip _killTick;
        private static AudioClip _dryClick;

        /// <summary>Re-raised for every trigger pull so a HUD can flash a hit marker.</summary>
        public event System.Action<GunShot> Shot;

        /// <summary>Whether a gun is currently equipped (the reticle only reacts while one is).</summary>
        public bool HasGun => _gun != null;

        /// <summary>Scenery is between the barrel and the crosshair's target -- the shot would hit the scenery.</summary>
        public bool BarrelBlocked { get; private set; }

        /// <summary>Where a shot fired now would end -- the point the arm aims the gun at, so barrel, bullet and tracer agree.</summary>
        public Vector3 AimPoint { get; private set; }

        public bool HasAimPoint { get; private set; }

        public bool IsReloading => (_gun != null && _gun.IsReloading) || (_leftGun != null && _leftGun.IsReloading);

        public void Attach(IEquippableGun gun)
        {
            Detach(_gun);
            _gun = gun;
            _gun.ShotResolved += OnShot;
            EnsureAudio();
        }

        /// <summary>The left hand's gun of a pair: its shots kick the camera, confirm hits and click dry as the right's do. The aim stays the right's.</summary>
        public void AttachLeft(IEquippableGun gun)
        {
            Detach(_leftGun);
            _leftGun = gun;
            _leftGun.ShotResolved += OnShot;
            EnsureAudio();
        }

        public void Detach(IEquippableGun gun)
        {
            if (gun != null && ReferenceEquals(gun, _leftGun))
            {
                _leftGun.ShotResolved -= OnShot;
                _leftGun = null;
                return;
            }
            if (gun == null || !ReferenceEquals(gun, _gun))
            {
                return;
            }
            _gun.ShotResolved -= OnShot;
            _gun = null;
            BarrelBlocked = false;
            HasAimPoint = false;
        }

        private void EnsureAudio()
        {
            if (_audio != null)
            {
                return;
            }
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0.0f;   // a UI-style confirmation, not a sound in the world
        }

        private void Update()
        {
            if (_gun == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Object.FindFirstObjectByType<ThirdPersonCamera>();
            }
            if (_reader == null)
            {
                _reader = GetComponent<PlayerInputReader>();
            }
            if (_camera == null)
            {
                return;
            }

            // The obstacle prompt: refreshed every frame so the reticle is
            // never showing "clear" while a shot would go into the wall.
            Transform view = _reader != null && _reader.YawSource != null ? _reader.YawSource : _camera.transform;
            _gun.AimTrace(view.position, view.forward, gameObject, out Vector3 aimPoint, out bool blocked);
            AimPoint = aimPoint;
            HasAimPoint = true;
            BarrelBlocked = blocked;
        }

        private void OnShot(GunShot shot)
        {
            if (shot.Fired)
            {
                // Raise the arm down the barrel and face the camera for half a
                // second, aimed or not (hip fire used to leave the gun wherever
                // the walk cycle carried it).
                if (_locomotion == null)
                {
                    _locomotion = GetComponent<PlayerLocomotion>();
                }
                if (_locomotion != null)
                {
                    _locomotion.NotifyGunFired();
                }

                if (_camera == null)
                {
                    _camera = Object.FindFirstObjectByType<ThirdPersonCamera>();
                }
                if (_camera != null && CameraRecoilScale > 0.0f)
                {
                    _camera.AddRecoil(shot.RecoilPitchDegrees * CameraRecoilScale,
                        shot.ShakeDegrees * CameraRecoilScale, shot.RecoilYawDegrees * CameraRecoilScale, shot.RecoilHoldSeconds);
                }

                if (shot.Killed)
                {
                    Play(KillTick());
                }
                else if (shot.HitDamageable)
                {
                    Play(HitTick());
                }
            }
            else if (shot.DryClick)
            {
                Play(DryClick());
            }

            Shot?.Invoke(shot);
        }

        private void Play(AudioClip clip)
        {
            if (_audio == null || clip == null || FeedbackVolume <= 0.0f)
            {
                return;
            }
            _audio.PlayOneShot(clip, FeedbackVolume);
        }

        // --- the sounds are synthesised, not shipped: three tiny PCM clips,
        // built once. A short high tick is what shooters use for "hit", a
        // lower two-note for "kill", and a dry noise burst for the click.
        // Deterministic (no randomness), so a test can rely on them. ---

        private const int SampleRate = 44100;

        private static AudioClip HitTick()
        {
            if (_hitTick == null)
            {
                _hitTick = Synthesise("gun-hit-tick", 0.05f, (t, i) =>
                    Mathf.Sin(2.0f * Mathf.PI * 2400.0f * t) * Mathf.Exp(-t * 70.0f));
            }
            return _hitTick;
        }

        private static AudioClip KillTick()
        {
            if (_killTick == null)
            {
                _killTick = Synthesise("gun-kill-tick", 0.14f, (t, i) =>
                {
                    float first = Mathf.Sin(2.0f * Mathf.PI * 1500.0f * t) * Mathf.Exp(-t * 45.0f);
                    float t2 = Mathf.Max(0.0f, t - 0.055f);
                    float second = t >= 0.055f
                        ? Mathf.Sin(2.0f * Mathf.PI * 2200.0f * t2) * Mathf.Exp(-t2 * 45.0f)
                        : 0.0f;
                    return first + second;
                });
            }
            return _killTick;
        }

        private static AudioClip DryClick()
        {
            if (_dryClick == null)
            {
                uint state = 2463534242u;
                _dryClick = Synthesise("gun-dry-click", 0.03f, (t, i) =>
                {
                    // xorshift noise: repeatable, and needs no UnityEngine.Random state.
                    state ^= state << 13;
                    state ^= state >> 17;
                    state ^= state << 5;
                    float noise = (state / (float)uint.MaxValue) * 2.0f - 1.0f;
                    return noise * Mathf.Exp(-t * 180.0f);
                });
            }
            return _dryClick;
        }

        private static AudioClip Synthesise(string name, float seconds, System.Func<float, int, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = Mathf.Clamp(sample(i / (float)SampleRate, i), -1.0f, 1.0f) * 0.6f;
            }
            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
