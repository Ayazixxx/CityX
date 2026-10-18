using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHAT A BLOW THAT LANDS LOOKS AND FEELS LIKE, on the body that took it (owner, 2026-10-17: "the hits dont feel impactful at all. make it
    /// feel better and make the blood effects better too"). Games that are known for their hits fire several small things on the same frame
    /// rather than one big one (Street Fighter, God of War, the "game feel" writing): here, in order --
    ///
    ///  1. the impact freeze: the body held dead still for a few frames (the attacker's hit-stop holds theirs at the same instant);
    ///  2. the shove: thrown back along the blow, further for a heavier one;
    ///  3. the flash: the body brightens for an instant;
    ///  4. the blood: a burst out of the wound away from the blow (BloodBurst), splats under it and on a wall behind (BloodSplats);
    ///  5. the thud, at the wound;
    ///  6. the camera: when this is the player's own body, the picture jolts -- and when the player's own blade lands, a smaller jolt.
    ///
    /// Then the recoil of the torso and head (HitReactions) plays out of the freeze. Every body has one of these (PlayerInputReader,
    /// SwordsmanBrain and ShootaBrain add it, like BlockDeflectEffect). Other players' copies are told by name
    /// (<see cref="NameFor"/>: the side the blow came from and its weight) and show the same wound where the body is.
    /// </summary>
    public sealed class HitImpactEffects : MonoBehaviour
    {
        /// <summary>The names a wound travels to other players under: "wounded_&lt;front|back|left|right&gt;_&lt;light|heavy&gt;".</summary>
        public const string StatePrefix = "wounded_";

        /// <summary>How long a blow holds the body still: a light blow, a heavy one. The attacker's hit-stop is SoulsSwordCombat.HitStopSeconds.</summary>
        public const float LightStopSeconds = 0.07f;

        public const float HeavyStopSeconds = 0.12f;

        /// <summary>A blow with no stun to it (a bullet, a stagger shrugged off by Stability) still catches the body for an instant.</summary>
        public const float GrazeStopSeconds = 0.03f;

        /// <summary>A blow whose hitstun is this long or longer is a heavy one here: a longer freeze, a bigger burst, a harder jolt.</summary>
        public const float HeavyHitstunSeconds = 0.85f;

        /// <summary>Metres the body is thrown back for each second of hitstun the blow carries, up to <see cref="MaxShoveMetres"/>, over <see cref="ShoveSeconds"/>.</summary>
        public const float ShoveMetresPerHitstunSecond = 0.65f;

        public const float MaxShoveMetres = 1.0f;

        public const float ShoveSeconds = 0.2f;

        public const float FlashSeconds = 0.09f;

        /// <summary>How much brighter the body is at the top of the flash (0.9 = nearly twice as bright), dying away over the flash.</summary>
        public const float FlashBrightness = 0.9f;

        public const float TakenShakeMetres = 0.07f;
        public const float HeavyTakenShakeMetres = 0.12f;
        public const float LandedShakeMetres = 0.035f;
        public const float KillShakeMetres = 0.09f;
        public const float ShakeSeconds = 0.25f;
        public const float LandedShakeSeconds = 0.15f;

        public const float ThudVolume = 0.6f;

        /// <summary>Where a wound is put when nobody said where the blow met the body: this far in from the chest toward the blow.</summary>
        public const float BodyRadius = 0.3f;

        private static AudioClip _thud;

        private PlayerVitals _vitals;
        private PlayerAnimationDriver _driver;
        private PlayerLocomotion _locomotion;
        private MeleeAttacker _melee;
        private Renderer[] _renderers;
        private Color[] _baseColours;
        private MaterialPropertyBlock _block;
        private float _flashRemaining;

        /// <summary>How many wounds this body has shown, its own and ones told from the network (for tests).</summary>
        public int Shown { get; private set; }

        /// <summary>How many of those came from another player's machine.</summary>
        public int RemoteShown { get; private set; }

        /// <summary>True while the body is lit up by a blow.</summary>
        public bool IsFlashing => _flashRemaining > 0.0f;

        /// <summary>The last camera jolt this body asked for, metres (for tests; 0 when none).</summary>
        public float LastShakeMetres { get; private set; }

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            _driver = GetComponent<PlayerAnimationDriver>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _melee = GetComponent<MeleeAttacker>();
        }

        private void OnEnable()
        {
            if (_vitals != null) _vitals.Wounded += OnWounded;
            if (_driver != null) _driver.RemoteWounded += OnRemoteWounded;
            if (_melee != null) _melee.Landed += OnLanded;
        }

        private void OnDisable()
        {
            if (_vitals != null) _vitals.Wounded -= OnWounded;
            if (_driver != null) _driver.RemoteWounded -= OnRemoteWounded;
            if (_melee != null) _melee.Landed -= OnLanded;
            EndFlash();
        }

        /// <summary>The name a wound from <paramref name="direction"/> travels under.</summary>
        public static string NameFor(HitDirection direction, bool heavy)
        {
            return StatePrefix + direction.ToString().ToLowerInvariant() + (heavy ? "_heavy" : "_light");
        }

        /// <summary>Reads a wound's name back; false for any other state name.</summary>
        public static bool TryParse(string stateName, out HitDirection direction, out bool heavy)
        {
            direction = HitDirection.Front;
            heavy = false;
            if (string.IsNullOrEmpty(stateName) || !stateName.StartsWith(StatePrefix))
            {
                return false;
            }
            string[] parts = stateName.Split('_');
            if (parts.Length != 3)
            {
                return false;
            }
            switch (parts[1])
            {
                case "front": direction = HitDirection.Front; break;
                case "back": direction = HitDirection.Back; break;
                case "left": direction = HitDirection.Left; break;
                case "right": direction = HitDirection.Right; break;
                default: return false;
            }
            if (parts[2] == "heavy")
            {
                heavy = true;
                return true;
            }
            return parts[2] == "light";
        }

        /// <summary>This body took a blow (its own vitals say so, inside the blow's DamageContext): shown here, announced to its copies.</summary>
        private void OnWounded(Vector3 fromPosition, float damage, float hitstunSeconds)
        {
            Vector3 chest = Chest();
            Vector3 toward = fromPosition - chest;
            toward.y = 0.0f;
            Vector3 towardBlow = toward.sqrMagnitude > 1e-4f ? toward.normalized : transform.forward;
            bool heavy = hitstunSeconds >= HeavyHitstunSeconds;
            Vector3 point = DamageContext.ContactPoint ?? (chest + towardBlow * BodyRadius);
            if ((point - chest).sqrMagnitude > 4.0f)
            {
                point = chest + towardBlow * BodyRadius;   // a contact point nowhere near the body: the reach box's guess, not a wound
            }
            Show(point, -towardBlow, heavy, hitstunSeconds, local: true);
            _driver?.AnnounceState(NameFor(HitReactions.DirectionOf(transform.InverseTransformPoint(fromPosition)), heavy));
        }

        /// <summary>The player this body is a copy of took a blow: the same wound, placed on the side it came from.</summary>
        private void OnRemoteWounded(HitDirection direction, bool heavy)
        {
            RemoteShown++;
            Vector3 towardBlow;
            switch (direction)
            {
                case HitDirection.Back: towardBlow = -transform.forward; break;
                case HitDirection.Left: towardBlow = -transform.right; break;
                case HitDirection.Right: towardBlow = transform.right; break;
                default: towardBlow = transform.forward; break;
            }
            Show(Chest() + towardBlow * BodyRadius, -towardBlow, heavy, heavy ? HeavyHitstunSeconds : LightStopSeconds * 8.0f, local: false);
        }

        /// <summary>The whole wound. <paramref name="local"/>: this machine owns the body (it is thrown; a copy is moved by the network).</summary>
        public void Show(Vector3 point, Vector3 away, bool heavy, float hitstunSeconds, bool local)
        {
            Shown++;
            if (!Application.isPlaying)
            {
                return;
            }
            float stop = heavy ? HeavyStopSeconds : (hitstunSeconds > 0.0f ? LightStopSeconds : GrazeStopSeconds);
            _locomotion?.ImpactFreeze(stop);
            if (local && hitstunSeconds > 0.0f)
            {
                _locomotion?.Shove(away, Mathf.Min(MaxShoveMetres, hitstunSeconds * ShoveMetresPerHitstunSecond), ShoveSeconds);
            }
            BeginFlash();
            float strength = heavy ? 1.6f : (hitstunSeconds > 0.0f ? 1.0f : 0.6f);
            BloodBurst.Spawn(point, away, strength);
            BloodSplats.Mark(point, away, strength, transform);
            AudioSource.PlayClipAtPoint(Thud(), point, ThudVolume * (heavy ? 1.25f : 1.0f));
            if (local && IsTheLocalPlayer)
            {
                Shake(heavy ? HeavyTakenShakeMetres : TakenShakeMetres, ShakeSeconds);
            }
        }

        /// <summary>This body's own blow landed on someone (MeleeAttacker.Landed): the player's picture gets a small jolt, a bigger one for a kill.</summary>
        private void OnLanded(IDamageable target, float damage)
        {
            if (IsTheLocalPlayer)
            {
                bool killed = target != null && !target.IsAlive;
                Shake(killed ? KillShakeMetres : LandedShakeMetres, killed ? ShakeSeconds : LandedShakeSeconds);
            }
        }

        private bool IsTheLocalPlayer => ThirdPersonCamera.Local != null && ThirdPersonCamera.Local.Target == transform;

        private void Shake(float metres, float seconds)
        {
            LastShakeMetres = metres;
            ThirdPersonCamera.Local?.AddShake(metres, seconds);
        }

        private Vector3 Chest()
        {
            float height = _locomotion != null ? _locomotion.StandingHeight : 1.8f;
            return transform.position + Vector3.up * (height * 0.62f);
        }

        // --- the flash -------------------------------------------------------------------------------------------------------------------

        private void BeginFlash()
        {
            if (_renderers == null)
            {
                var found = new List<Renderer>();
                var bases = new List<Color>();
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
                    {
                        continue;
                    }
                    Material material = renderer.sharedMaterial;
                    Color baseColour = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                        : (material != null && material.HasProperty("_Color") ? material.color : Color.white);
                    found.Add(renderer);
                    bases.Add(baseColour);
                }
                _renderers = found.ToArray();
                _baseColours = bases.ToArray();
                _block = new MaterialPropertyBlock();
            }
            _flashRemaining = FlashSeconds;
            ApplyFlash();
            enabled = true;
        }

        private void Update()
        {
            if (_flashRemaining <= 0.0f)
            {
                return;
            }
            _flashRemaining -= Time.deltaTime;
            if (_flashRemaining <= 0.0f)
            {
                EndFlash();
                return;
            }
            ApplyFlash();
        }

        /// <summary>Brightens every renderer's own colour, through a property block so no material asset is touched.</summary>
        private void ApplyFlash()
        {
            float f = Mathf.Clamp01(_flashRemaining / FlashSeconds);
            float gain = 1.0f + FlashBrightness * f;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                {
                    continue;
                }
                Color lit = _baseColours[i] * gain;
                lit.a = _baseColours[i].a;
                _block.Clear();
                _block.SetColor("_BaseColor", lit);
                _block.SetColor("_Color", lit);
                _renderers[i].SetPropertyBlock(_block);
            }
        }

        private void EndFlash()
        {
            _flashRemaining = 0.0f;
            if (_renderers == null)
            {
                return;
            }
            foreach (Renderer renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(null);
                }
            }
        }

        // --- the thud ---------------------------------------------------------------------------------------------------------------------

        /// <summary>A short low thud with a wet slap on the front of it: a blade or a fist landing in a body.</summary>
        public static AudioClip Thud()
        {
            if (_thud != null)
            {
                return _thud;
            }
            const int rate = 44100;
            const float seconds = 0.18f;
            int count = Mathf.RoundToInt(rate * seconds);
            var samples = new float[count];
            var noise = new System.Random(17);
            float phase = 0.0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                // The low thump: a tone dropping from 150 to 60 Hz, gone in a tenth of a second.
                float frequency = 150.0f - 90.0f * Mathf.Min(1.0f, t / 0.12f);
                phase += 2.0f * Mathf.PI * frequency / rate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 22.0f) * 0.9f;
                // The slap: a burst of noise over the first few milliseconds, and a little mid ring under it.
                float slap = (float)(noise.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 140.0f) * 0.7f;
                float ring = Mathf.Sin(2.0f * Mathf.PI * 700.0f * t) * Mathf.Exp(-t * 60.0f) * 0.25f;
                samples[i] = Mathf.Clamp(thump + slap + ring, -1.0f, 1.0f) * 0.9f;
            }
            _thud = AudioClip.Create("HitThud", count, 1, rate, false);
            _thud.SetData(samples, 0);
            return _thud;
        }
    }
}
