using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    public enum FirearmKind
    {
        /// <summary>Held in one hand, the other steadying it when aimed; the arm is aimed down the barrel.</summary>
        Pistol,

        /// <summary>The stock in the shoulder pocket and both hands on it always: the gun is placed and the arms follow (PlayerLocomotion.RifleAim).</summary>
        Rifle,
    }

    /// <summary>
    /// WHAT MAKES ONE GUN DIFFERENT FROM ANOTHER (owner, 2026-10-14: "remember there will be all sorts of firearms"): the numbers and the
    /// geometry a <see cref="PistolWeapon"/> reads instead of hardcoding, so the same component is the Glock and the AK-47 (and the next gun) with
    /// a different profile on its prefab. What does not vary (the three-trace hitscan shot, the camera kick's recovery, the reload played by name)
    /// stays in the weapon.
    ///
    /// GEOMETRY is in the gun model's own frame: x the shooter's right, y up, z the muzzle, the origin at the WEB of the pistol grip (the model
    /// scripts put it there: CityX/SourceArt/Glock/glock_model.py, CityX/SourceArt/AK47/ak47_model.py), which is why one hold
    /// (<see cref="HoldPosition"/>, <see cref="HoldDipDegrees"/>) fits every gun with a pistol grip. The points are the model's own .points.json.
    /// </summary>
    [System.Serializable]
    public sealed class FirearmProfile
    {
        [Header("What it is")]
        public string ItemId = ItemRegistry.Pistol;
        public string DisplayName = "Glock";
        public FirearmKind Kind = FirearmKind.Pistol;

        [Header("Ammunition and damage")]
        public int MagazineSize = 15;
        public int StartingReserveAmmo = 45;
        public float Damage = 20.0f;
        public float Range = 60.0f;

        [Header("Firing")]
        [Tooltip("Shots per second (the trigger held, for an automatic).")]
        public float FireRatePerSecond = 5.5f;

        [Tooltip("True: fires for as long as the button is held, at FireRatePerSecond. False: one shot per press.")]
        public bool Automatic;

        [Tooltip("The camera kick of each shot of a burst, degrees: x to the right, y up. The last entry repeats; a burst ends after " +
                 "RecoilResetSeconds without a shot. Learnable, never random: every shot still lands where the crosshair is.")]
        public Vector2[] RecoilPattern = { new Vector2(0.0f, 1.2f) };

        public float RecoilResetSeconds = 0.3f;

        [Tooltip("How long after a shot the camera holds its kick before settling (0: settles at once). An automatic holds through a burst, " +
                 "so a spray climbs and must be pulled down.")]
        public float RecoilHoldSeconds;

        [Tooltip("The camera kick while aiming is scaled by this.")]
        public float AdsRecoilMultiplier = 0.7f;

        [Tooltip("Camera roll jolt per shot, degrees.")]
        public float ShakeDegrees = 0.25f;

        [Tooltip("The gun itself is shoved back this far (metres) and tips its muzzle up this many degrees on a shot, then settles.")]
        public float KickBackMetres = 0.035f;

        public float KickUpDegrees = 7.0f;

        public float KickRecoverPerSecond = 16.0f;

        [Tooltip("A visible tracer on every n-th round of a burst (always on its first), as Counter-Strike's rifles do: one on every " +
                 "round of an automatic reads as a hose, not as shots. 1: every round.")]
        public int TracerEvery = 1;

        [Tooltip("Pitch of the shot sound (1 = the clip as recorded).")]
        public float ShotPitch = 1.0f;

        [Header("Reload")]
        [Tooltip("The baked reload motion in Resources (BakedMotion), with 'tactical' and 'empty' segments.")]
        public string MotionResource = "Glock/GlockReload";

        [Tooltip("How long a reload takes when there is no motion to time it (a bare test rig).")]
        public float ReloadSeconds = 1.4f;

        public float EmptyReloadSeconds = 1.73f;

        [Tooltip("The gun's own recording of a reload with rounds still in the magazine, made exactly as long as the tactical reload (the motion's " +
                 "tactical segment), played from its start. None: the motion's events make the synthesised clicks instead (FirearmEffects).")]
        public AudioClip ReloadSound;

        [Tooltip("The same for the empty reload (the action racked too), as long as the motion's empty segment.")]
        public AudioClip EmptyReloadSound;

        /// <summary>The recording for this kind of reload, or null when the gun has none (its reload clicks are synthesised).</summary>
        public AudioClip ReloadSoundFor(bool empty) => empty ? EmptyReloadSound : ReloadSound;

        [Header("The hold (the gun's frame in the right hand bone's)")]
        [Tooltip("Where the gun's origin (the web of its grip) sits in the hand bone's frame: the web of the hand, between thumb and index.")]
        public Vector3 HoldPosition = new Vector3(0.10f, -0.01f, 0.095f);

        [Tooltip("The barrel dips this many degrees below the hand's own knuckle line (a high grip).")]
        public float HoldDipDegrees = 15.0f;

        public float HoldScale = 1.0f;

        [Tooltip("How much bigger it lies in the world than in the hand, so it can be seen on the ground.")]
        public float WorldPickupScale = 1.3f;

        [Header("The support hand (gun frame)")]
        [Tooltip("Where the free hand's palm goes on the gun. The Glock's is on the left of the grip, a little behind its middle: with the " +
                 "shooting arm out at full reach, the other arm (0.89 m) only just gets there (WeaponHandsPlayTests measures it).")]
        public Vector3 SupportHandLocal = new Vector3(-0.07f, -0.105f, -0.035f);

        [Tooltip("True for a pistol: the free hand comes onto the gun only when it is raised to aim. A rifle is held two-handed always.")]
        public bool SupportHandOnlyWhenAiming = true;

        [Header("Points on the gun (gun frame, from the model's .points.json)")]
        public Vector3 MuzzleLocal = new Vector3(0.0f, 0.058f, 0.367f);

        public Vector3 EjectionPortLocal = new Vector3(0.024f, 0.066f, 0.258f);

        [Tooltip("Which way a spent case leaves the port: right, up, a little back.")]
        public Vector3 EjectionDirectionLocal = new Vector3(1.0f, 0.75f, -0.3f);

        [Tooltip("A rifle's butt, where it meets the shoulder (unused on a pistol).")]
        public Vector3 ButtLocal = new Vector3(0.0f, -0.04f, -0.42f);

        [Tooltip("Which way the magazine leaves the gun, for the dropped magazine.")]
        public Vector3 MagazineOutLocal = new Vector3(0.0f, -0.951f, -0.309f);

        [Header("Moving parts (the names the reload motion's props use)")]
        public string ActionPartName = "Slide";
        public string MagazinePartName = "Magazine";
        public string TriggerPartName = "Trigger";

        [Tooltip("How far the slide or bolt travels back on a shot, metres (the model's own travel).")]
        public float ActionTravel = 0.0855f;

        [Tooltip("How long the action takes to cycle on a shot, seconds: back, then home.")]
        public float ActionCycleSeconds = 0.075f;

        public float TriggerPullDegrees = 14.0f;

        [Tooltip("A spent case's size, metres: a pistol's short and fat, a rifle's long and bottlenecked.")]
        public float CasingLength = 0.036f;

        public float CasingRadius = 0.0095f;

        /// <summary>The gun's rotation in the hand bone's frame: its right to the back of the hand (+y), its top to the thumb's side (+z), the barrel down the fingers dipped by <see cref="HoldDipDegrees"/>.</summary>
        public Quaternion HoldRotation
        {
            get
            {
                float d = HoldDipDegrees * Mathf.Deg2Rad;
                var barrel = new Vector3(Mathf.Cos(d), 0.0f, -Mathf.Sin(d));
                var top = new Vector3(Mathf.Sin(d), 0.0f, Mathf.Cos(d));
                return Quaternion.LookRotation(barrel, top);
            }
        }

        /// <summary>The camera kick of the n-th shot of a burst (0-based): the pattern's entry, its last repeating.</summary>
        public Vector2 RecoilFor(int shotInBurst)
        {
            if (RecoilPattern == null || RecoilPattern.Length == 0)
            {
                return new Vector2(0.0f, 1.2f);
            }
            return RecoilPattern[Mathf.Clamp(shotInBurst, 0, RecoilPattern.Length - 1)];
        }

        public float FireCooldownSeconds => FireRatePerSecond > 0.0f ? 1.0f / FireRatePerSecond : 0.2f;

        public bool IsRifle => Kind == FirearmKind.Rifle;

        /// <summary>THE GLOCK (2026-10-14): a 15-round 9 mm pistol, made in Blender (CityX/SourceArt/Glock) to replace the placeholder Beretta.</summary>
        public static FirearmProfile Glock()
        {
            return new FirearmProfile();
        }

        /// <summary>
        /// THE AK-47 (owner, 2026-10-14: "add an ak47 with reload animation too ... research ak47 mechanics in 3rd person games"). From the
        /// research (docs/THIRD_PERSON_PISTOL.md, "The AK-47"): 30 rounds at about 600 a minute, hard-hitting and slow to handle; its spray climbs
        /// straight up for the first shots, drifts right, then wanders left and right (CS:GO's pattern, the best-documented one); a long
        /// reload that rocks the magazine out and in, and a charging handle on the right for an empty gun (PUBG's AKM 2.25 s tactical, 2.9 s
        /// empty). It ejects to the right and forward.
        /// </summary>
        public static FirearmProfile AK47()
        {
            return new FirearmProfile
            {
                ItemId = ItemRegistry.AK47,
                DisplayName = "AK-47",
                Kind = FirearmKind.Rifle,
                MagazineSize = 30,
                StartingReserveAmmo = 90,
                Damage = 34.0f,
                Range = 120.0f,
                FireRatePerSecond = 10.0f,
                Automatic = true,
                RecoilPattern = new[]
                {
                    // 1-7: straight up, building; 8-12: still climbing, drifting right; 13-18: flattening, swinging left; then a slow wander.
                    new Vector2(0.0f, 0.55f), new Vector2(0.0f, 0.8f), new Vector2(0.03f, 0.95f), new Vector2(0.05f, 1.05f), new Vector2(0.08f, 1.05f),
                    new Vector2(0.1f, 1.0f), new Vector2(0.15f, 0.9f), new Vector2(0.3f, 0.75f), new Vector2(0.4f, 0.6f), new Vector2(0.45f, 0.5f),
                    new Vector2(0.35f, 0.4f), new Vector2(0.1f, 0.35f), new Vector2(-0.3f, 0.3f), new Vector2(-0.5f, 0.25f), new Vector2(-0.55f, 0.2f),
                    new Vector2(-0.45f, 0.2f), new Vector2(-0.2f, 0.18f), new Vector2(0.15f, 0.15f), new Vector2(0.4f, 0.15f), new Vector2(0.45f, 0.15f),
                    new Vector2(0.25f, 0.12f), new Vector2(-0.15f, 0.12f), new Vector2(-0.4f, 0.12f), new Vector2(-0.4f, 0.12f), new Vector2(-0.1f, 0.12f),
                    new Vector2(0.25f, 0.1f), new Vector2(0.4f, 0.1f), new Vector2(0.2f, 0.1f), new Vector2(-0.2f, 0.1f), new Vector2(-0.35f, 0.1f),
                },
                RecoilResetSeconds = 0.35f,
                TracerEvery = 3,
                RecoilHoldSeconds = 0.13f,
                AdsRecoilMultiplier = 0.8f,
                ShakeDegrees = 0.35f,
                KickBackMetres = 0.035f,
                KickUpDegrees = 2.5f,
                KickRecoverPerSecond = 20.0f,
                ShotPitch = 0.78f,
                MotionResource = "AK47/AK47Reload",
                ReloadSeconds = 2.17f,
                EmptyReloadSeconds = 2.8f,
                WorldPickupScale = 1.0f,
                SupportHandLocal = new Vector3(-0.025f, -0.035f, 0.435f),
                SupportHandOnlyWhenAiming = false,
                MuzzleLocal = new Vector3(0.0f, 0.0522f, 0.896f),
                EjectionPortLocal = new Vector3(0.0232f, 0.058f, 0.171f),
                EjectionDirectionLocal = new Vector3(1.0f, 0.55f, 0.3f),
                ButtLocal = new Vector3(0.0f, -0.0435f, -0.416f),
                MagazineOutLocal = new Vector3(0.0f, -0.95f, 0.31f),
                ActionPartName = "Bolt",
                ActionTravel = 0.174f,
                ActionCycleSeconds = 0.07f,
                TriggerPullDegrees = 12.0f,
                CasingLength = 0.057f,
                CasingRadius = 0.0082f,
            };
        }
    }
}
