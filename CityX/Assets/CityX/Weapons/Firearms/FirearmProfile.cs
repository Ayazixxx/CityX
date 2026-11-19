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
    /// scripts put it there: CityX/SourceArt/Glock/glock_model.py, CityX/SourceArt/AK47/ak47_model.py), so a hold
    /// (<see cref="HoldPosition"/>, <see cref="HoldDipDegrees"/>) means the same on every gun. The rifles share one; the Glock has its own, measured against the hand. The points are the model's own .points.json.
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

        [Tooltip("A visible tracer on every n-th round of a burst (always on its first), as Counter-Strike's rifles do. 1: every round, " +
                 "which every gun draws since 2026-10-25 (owner: \"not every bullet is leaving a trail\" on the AK).")]
        public int TracerEvery = 1;

        [Tooltip("Pitch of the shot sound (1 = the clip as recorded).")]
        public float ShotPitch = 1.0f;

        [Header("Reload")]
        [Tooltip("The baked reload motion in Resources (BakedMotion), with 'tactical' and 'empty' segments.")]
        public string MotionResource = "Glock/GlockReload";

        [Tooltip("The reload of a PAIR of these guns, one in each hand (Dual Pistols, docs/DUAL_PISTOLS.md): both magazines out and in together.")]
        public string PairMotionResource = "Glock/GlockPairReload";

        [Tooltip("How long a reload takes when there is no motion to time it (a bare test rig).")]
        public float ReloadSeconds = 1.4f;

        public float EmptyReloadSeconds = 1.73f;

        [Tooltip("The gun's own recording of a reload with rounds still in the magazine, made exactly as long as the tactical reload (the motion's " +
                 "tactical segment), played from its start. None: the motion's events make the synthesised clicks instead (FirearmEffects).")]
        public AudioClip ReloadSound;

        [Tooltip("The same for the empty reload (the action racked too), as long as the motion's empty segment.")]
        public AudioClip EmptyReloadSound;

        [Tooltip("The gun's own recording of its action worked by hand (a pump), or none: the synthesised rack. Played on the pump after a shot and on the rack of a reload.")]
        public AudioClip RackSound;

        [Tooltip("Where in RackSound the action reaches the rear, seconds: it is played from there as the action gets there, so the rest of it (the push home) follows in time.")]
        public float RackSoundBackAt;

        [Tooltip("A tube's: the gun's own recording of ONE shell going in, played from the start of each shell's piece of the reload; its click should " +
                 "land where the motion seats the shell. None: the synthesised click at the seat.")]
        public AudioClip ShellSound;

        /// <summary>The recording for this kind of reload, or null when the gun has none (its reload clicks are synthesised).</summary>
        public AudioClip ReloadSoundFor(bool empty) => empty ? EmptyReloadSound : ReloadSound;

        [Header("The hold (the gun's frame in the right hand bone's)")]
        [Tooltip("Where the gun's origin (the web of its grip) sits in the hand bone's frame. The Glock's is measured against the hand (FusedBodyShape): its " +
                 "right panel flat on the palm (half the palm's thickness and half the frame's width to the palm side), its front strap one finger's radius " +
                 "short of the finger roots, the trigger guard level with the index finger.")]
        public Vector3 HoldPosition = new Vector3(-0.056f, -0.0795f, 0.037f);

        [Tooltip("The barrel dips this many degrees below the hand's own knuckle line. The Glock's is negative: the barrel rises by the grip's rake, so the " +
                 "grip runs along the line of the finger roots and the fingers close square round it.")]
        public float HoldDipDegrees = -22.0f;

        public float HoldScale = 1.0f;

        [Tooltip("How much bigger it lies in the world than in the hand, so it can be seen on the ground.")]
        public float WorldPickupScale = 1.0f;

        [Header("The support hand (gun frame)")]
        [Tooltip("Where the free hand's palm goes on the gun. The Glock's is flat on the left panel, a finger's thickness forward of the shooting hand's " +
                 "mirror image (its fingers close over the shooting fingers) and low enough that its index finger is under the trigger guard.")]
        public Vector3 SupportHandLocal = new Vector3(-0.0795f, -0.165f, 0.127f);

        [Tooltip("True for a pistol: the free hand comes onto the gun only when it is raised to aim. A rifle is held two-handed always.")]
        public bool SupportHandOnlyWhenAiming = true;

        [Tooltip("A long gun whose free hand CRADLES its forend, palm up under it and the fingers round its far side (a shotgun): which way the " +
                 "fingers run and the palm faces, gun frame. Zero: the hand is not laid (the AK-47's).")]
        public Vector3 CradleFingersLocal;

        public Vector3 CradlePalmLocal = Vector3.up;

        public bool CradlesForend => CradleFingersLocal.sqrMagnitude > 1e-6f;

        [Header("Points on the gun (gun frame, from the model's .points.json)")]
        public Vector3 MuzzleLocal = new Vector3(0.0f, 0.05625f, 0.4625f);

        public Vector3 EjectionPortLocal = new Vector3(0.031875f, 0.06625f, 0.225f);

        [Tooltip("Which way a spent case leaves the port: right, up, a little back.")]
        public Vector3 EjectionDirectionLocal = new Vector3(1.0f, 0.75f, -0.3f);

        [Tooltip("A rifle's butt, where it meets the shoulder (unused on a pistol).")]
        public Vector3 ButtLocal = new Vector3(0.0f, -0.04f, -0.42f);

        [Tooltip("Which way the magazine leaves the gun, for the dropped magazine.")]
        public Vector3 MagazineOutLocal = new Vector3(0.0f, -0.924f, -0.383f);

        [Header("Moving parts (the names the reload motion's props use)")]
        public string ActionPartName = "Slide";
        public string MagazinePartName = "Magazine";
        public string TriggerPartName = "Trigger";

        [Tooltip("How far the slide or bolt travels back on a shot, metres (the model's own travel).")]
        public float ActionTravel = 0.1125f;

        [Tooltip("How long the action takes to cycle on a shot, seconds: back, then home.")]
        public float ActionCycleSeconds = 0.075f;

        public float TriggerPullDegrees = 14.0f;

        [Tooltip("A spent case's size, metres: a pistol's short and fat, a rifle's long and bottlenecked.")]
        public float CasingLength = 0.047f;

        public float CasingRadius = 0.0125f;
        [Header("A crossbow (docs/ARCHERY.md)")]
        [Tooltip("Its shot is an arrow (PistolWeapon.ArrowPrefab) that falls on past the range at this speed, m/s. 0 for a gun.")]
        public float ArrowFallSpeed;
        [Tooltip("Spans itself after every shot (a crossbow holds one bolt): the reload starts the moment it fires.")]
        public bool ReloadsAfterEveryShot;

        [Header("A rocket launcher (docs/RPG.md)")]
        [Tooltip("Its shot is its loaded round flying as a rocket and bursting where it meets something (FlyingRocket, RocketRules), not a bullet. " +
                 "The round (the magazine part) shows only while it is loaded.")]
        public bool FiresRocket;

        [Tooltip("Where the back-blast leaves, gun frame (the blast cone's mouth).")]
        public Vector3 BackblastLocal;

        [Header("A shotgun (docs/SHOTGUN.md)")]
        [Tooltip("How many pellets a shot throws (1: a bullet). Damage is one pellet's. They fly a fixed pattern (PelletPattern): the first down the crosshair.")]
        public int Pellets = 1;

        [Tooltip("The pattern's cone, degrees from its middle to its edge.")]
        public float PelletConeDegrees;

        [Tooltip("The cone while aiming, as a share of it from the hip.")]
        public float AimedConeShare = 1.0f;

        [Tooltip("A hit keeps all its damage up to this far, metres...")]
        public float FalloffFullMetres;

        [Tooltip("...and falls in a straight line to FalloffFloorShare of it by this far. Not past FalloffFullMetres: no falloff.")]
        public float FalloffFloorMetres;

        public float FalloffFloorShare = 1.0f;

        [Tooltip("The action is worked by hand after the shot (a pump): it starts back this long after the shot, and the spent case leaves when it is back. 0: it cycles with the shot.")]
        public float ActionDelaySeconds;

        [Tooltip("The share of the action's cycle spent going back; the rest comes home.")]
        public float ActionBackShare = 0.35f;

        [Tooltip("The support hand holds the action (a pump's forend) and rides back and forward with it.")]
        public bool SupportHandOnAction;

        [Tooltip("Loaded one round at a time into a tube (GunReload's shell stages: the motion's 'start', 'shell', 'end' and 'endempty' segments), and a shot once a round is in breaks the loading off.")]
        public bool ShellByShell;

        [Tooltip("How long each piece of a shell-by-shell reload takes when there is no motion to time it.")]
        public float ShellStartSeconds = 0.4f;

        public float ShellSeconds = 0.567f;

        public float ShellEndSeconds = 0.4f;

        public float ShellEndEmptySeconds = 0.8f;

        /// <summary>The share of its damage a hit keeps at <paramref name="metres"/> from the muzzle.</summary>
        public float DamageShareAt(float metres) => PelletPattern.DamageShare(metres, FalloffFullMetres, FalloffFloorMetres, FalloffFloorShare);

        /// <summary>The pattern's cone, hip or aimed.</summary>
        public float ConeDegrees(bool aiming) => PelletConeDegrees * (aiming ? AimedConeShare : 1.0f);

        /// <summary>How far back the slide, bolt or pump is <paramref name="sinceShot"/> seconds after a shot, 0 home .. 1 fully back.</summary>
        public float ActionBackAt(float sinceShot)
        {
            float into = sinceShot - ActionDelaySeconds;
            if (into < 0.0f || ActionCycleSeconds <= 0.0f)
            {
                return 0.0f;
            }
            float cycle = into / ActionCycleSeconds;
            float turn = Mathf.Clamp(ActionBackShare, 0.05f, 0.95f);
            return cycle < turn ? cycle / turn : (cycle < 1.0f ? 1.0f - (cycle - turn) / (1.0f - turn) : 0.0f);
        }

        /// <summary>When the spent case leaves after a shot, seconds: at once, or as a hand-worked action reaches the rear.</summary>
        public float EjectDelaySeconds => ActionDelaySeconds > 0.0f ? ActionDelaySeconds + ActionCycleSeconds * Mathf.Clamp(ActionBackShare, 0.05f, 0.95f) : 0.0f;

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

        /// <summary>
        /// The free hand's rotation on a pistol, in the gun's frame: the shooting hand's mirrored across the gun, palm on the left panel, fingers the way the
        /// shooting fingers run, thumb up the grip (the left hand bone's +X runs back up the arm, -Y is the palm, +Z the thumb).
        /// </summary>
        /// <remarks>A gun that cradles its forend (<see cref="CradlesForend"/>) has the hand its fingers and palm say instead.</remarks>
        public Quaternion SupportHandRotation => CradlesForend ? CradleRotation
            : Quaternion.LookRotation(Quaternion.Inverse(HoldRotation) * Vector3.forward, Vector3.left);

        /// <summary>The left hand bone with its fingers (its -X) along <see cref="CradleFingersLocal"/> and its palm (its -Y) toward <see cref="CradlePalmLocal"/>, square to them.</summary>
        private Quaternion CradleRotation
        {
            get
            {
                Vector3 fingers = CradleFingersLocal.normalized;
                Vector3 palm = Vector3.ProjectOnPlane(CradlePalmLocal, fingers).normalized;
                Vector3 x = -fingers, y = -palm;
                return Quaternion.LookRotation(Vector3.Cross(x, y), y);
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
                HoldPosition = new Vector3(0.10f, -0.01f, 0.095f),
                HoldDipDegrees = 15.0f,
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
                // Cradled as a soldier holds an AK (owner, 2026-11-19, with photos: "the left hand is clipping through ... something like this"): palm
                // up under the lower handguard, the fingers up its right side, the thumb along its left; the shotgun's hold, fitted to this handguard
                // (ak47_reload.py, ON_HANDGUARD, the same). Measured joint by joint: FirearmPlayTests.TheAK47sLeftHandCradlesTheHandguard...
                SupportHandLocal = new Vector3(-0.008f, -0.024f, 0.45f),
                CradleFingersLocal = new Vector3(0.95f, 0.15f, 0.1f),
                CradlePalmLocal = new Vector3(0.15f, 1.0f, 0.0f),
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

        /// <summary>
        /// THE PUMP SHOTGUN (owner, 2026-11-18: "make a shotgun with reload animations and parts to it that move like our ak47 and pistol ...
        /// research how games do shotgun mechanics vs other guns and implement what you learn"; docs/SHOTGUN.md). Nine pellets a shot (PUBG's and
        /// Counter-Strike's Nova's count) in a fixed pattern, each losing damage with distance; the forend worked by hand after every shot, so
        /// about 70 shots a minute (the Nova's 68); six shells in the tube, loaded one at a time at about half a second each (the Nova's 0.5 s),
        /// and a shot breaks the loading off. Held and aimed as a rifle. The damage, the cone and the falloff are mine.
        /// </summary>
        public static FirearmProfile Shotgun()
        {
            return new FirearmProfile
            {
                ItemId = ItemRegistry.Shotgun,
                DisplayName = "Pump Shotgun",
                Kind = FirearmKind.Rifle,
                HoldPosition = new Vector3(0.10f, -0.01f, 0.095f),
                HoldDipDegrees = 15.0f,
                MagazineSize = 6,
                StartingReserveAmmo = 24,
                Damage = 12.0f,
                Range = 45.0f,
                Pellets = 9,
                PelletConeDegrees = 3.2f,
                AimedConeShare = 0.8f,
                FalloffFullMetres = 10.0f,
                FalloffFloorMetres = 30.0f,
                FalloffFloorShare = 0.35f,
                FireRatePerSecond = 1.0f / 0.85f,
                Automatic = false,
                RecoilPattern = new[] { new Vector2(0.0f, 4.5f) },
                RecoilResetSeconds = 0.3f,
                AdsRecoilMultiplier = 0.8f,
                ShakeDegrees = 0.8f,
                KickBackMetres = 0.07f,
                KickUpDegrees = 5.0f,
                KickRecoverPerSecond = 11.0f,
                ShotPitch = 1.0f,   // its own recording (ShotgunBuilder.ShotSoundPath), as it is
                MotionResource = "Shotgun/ShotgunReload",
                ShellByShell = true,
                // A whole tube, for a body with no motion to time it and for the item's card: the start, six shells, the end (with the pump when it was dry).
                ReloadSeconds = 0.4f + 6.0f * 0.567f + 0.4f,
                EmptyReloadSeconds = 0.4f + 6.0f * 0.567f + 0.8f,
                WorldPickupScale = 1.0f,
                // Cradled as a shotgunner holds a forend (owner, 2026-11-18, with a photo: "the left hand is clipping through, it should be holding it
                // like the other image"): palm up under it, the fingers up its right side, the thumb along its left. The reload's own hand on the forend
                // (shotgun_reload.py, on_forend: the wrist, and these fingers and palm), so a reload starts and ends where the hold is. The hand runs
                // nearly flat across the underside, its knuckles in a row along the forend's lower far edge, so only the fingers curl up its side, toward its back end where the arm reaches it carried low too (measured:
                // ShotgunPlayTests.TheLeftHandCradlesTheForend...).
                SupportHandLocal = new Vector3(0.0016f, -0.0366f, 0.4632f),
                CradleFingersLocal = new Vector3(0.95f, 0.15f, 0.1f),
                CradlePalmLocal = new Vector3(0.15f, 1.0f, 0.0f),
                SupportHandOnlyWhenAiming = false,
                SupportHandOnAction = true,
                MuzzleLocal = new Vector3(0.0f, 0.06815f, 0.94975f),
                EjectionPortLocal = new Vector3(0.0254f, 0.0638f, 0.1523f),
                EjectionDirectionLocal = new Vector3(1.0f, 0.5f, -0.1f),
                ButtLocal = new Vector3(0.0f, -0.0435f, -0.435f),
                ActionPartName = "Pump",
                MagazinePartName = "Shell",
                ActionTravel = 0.12325f,
                ActionDelaySeconds = 0.2f,
                // Timed to the owner's recording of a pump (2026-11-19, ShotgunBuilder.RackSoundPath): its pull back lands 0.17 s in and its push home
                // 0.25 s after that, so the forend is back 0.18 s after it starts and home 0.25 s later.
                ActionCycleSeconds = 0.43f,
                ActionBackShare = 0.42f,
                RackSoundBackAt = 0.17f,
                TriggerPullDegrees = 12.0f,
                CasingLength = 0.0899f,
                CasingRadius = 0.0146f,
            };
        }

        /// <summary>
        /// THE RPG (owner, 2026-11-19: "make a basic rpg rocket launcher that is animated"; docs/RPG.md): after the RPG-7, one round at a time,
        /// shouldered as a rifle is (its "butt" a point under the tube, so the tube lies on the shoulder beside the head: RPG.points.json), the
        /// round a rocket that flies and bursts (RocketRules), reloaded the moment it fires from the pouch at the hip (SourceArt/RPG/rpg_reload.py:
        /// 2.3 s). Four rounds spare. The numbers are mine.
        /// </summary>
        public static FirearmProfile RPG()
        {
            return new FirearmProfile
            {
                ItemId = ItemRegistry.RPG,
                DisplayName = "RPG",
                Kind = FirearmKind.Rifle,
                HoldPosition = new Vector3(0.10f, -0.01f, 0.095f),
                HoldDipDegrees = 15.0f,
                MagazineSize = 1,
                StartingReserveAmmo = 4,
                Damage = RocketRules.DirectHitDamage,
                Range = RocketRules.Range,
                FireRatePerSecond = 1.0f,
                Automatic = false,
                FiresRocket = true,
                ReloadsAfterEveryShot = true,
                RecoilPattern = new[] { new Vector2(0.0f, 3.0f) },
                RecoilResetSeconds = 0.5f,
                AdsRecoilMultiplier = 0.8f,
                ShakeDegrees = 1.4f,
                KickBackMetres = 0.06f,
                KickUpDegrees = 4.0f,
                KickRecoverPerSecond = 8.0f,
                ShotPitch = 0.55f,   // the shotgun's recording, deep: the booster's thump
                MotionResource = "RPG/RPGReload",
                PairMotionResource = "",
                ReloadSeconds = 140.0f / 60.0f,
                EmptyReloadSeconds = 140.0f / 60.0f,
                WorldPickupScale = 1.0f,
                // The support hand cradles the front guard as a shotgunner's does a forend (rpg_reload.py, on_guard: the same wrist, fingers, palm).
                SupportHandLocal = new Vector3(0.0016f, 0.0098f, 0.2602f),
                CradleFingersLocal = new Vector3(0.95f, 0.15f, 0.1f),
                CradlePalmLocal = new Vector3(0.15f, 1.0f, 0.0f),
                SupportHandOnlyWhenAiming = false,
                MuzzleLocal = new Vector3(0.0f, 0.0899f, 0.7105f),
                BackblastLocal = new Vector3(0.0f, 0.0899f, -0.667f),
                ButtLocal = new Vector3(0.0f, -0.1798f, -0.0725f),
                ActionPartName = "Hammer",
                MagazinePartName = "Rocket",
                TriggerPartName = "Trigger",
                ActionTravel = 0.0f,
                TriggerPullDegrees = 12.0f,
                CasingLength = 0.0f,
            };
        }

        /// <summary>
        /// The crossbow (docs/ARCHERY.md): one bolt, aimed and fired as a rifle, hitting hard (nearly three AK rounds) over a long flat range and
        /// falling on past it, then 2.5 s to span and lay the next (CrossbowSpan keys the spanning to the timer). No casing, no flash, no baked
        /// motion. The numbers are mine.
        /// </summary>
        public static FirearmProfile Crossbow()
        {
            return new FirearmProfile
            {
                ItemId = ItemRegistry.Crossbow,
                DisplayName = "Crossbow",
                Kind = FirearmKind.Rifle,
                HoldPosition = new Vector3(0.10f, -0.01f, 0.095f),
                HoldDipDegrees = 15.0f,
                MagazineSize = 1,
                StartingReserveAmmo = 40,
                Damage = ArcheryRules.BoltDamage,
                Range = ArcheryRules.BoltFlatRange,
                FireRatePerSecond = 1.0f,
                Automatic = false,
                RecoilPattern = new[] { new Vector2(0.0f, 0.5f) },
                RecoilResetSeconds = 0.3f,
                AdsRecoilMultiplier = 0.7f,
                ShakeDegrees = 0.3f,
                KickBackMetres = 0.02f,
                KickUpDegrees = 2.0f,
                KickRecoverPerSecond = 16.0f,
                MotionResource = "",
                ReloadSeconds = ArcheryRules.CrossbowReloadSeconds,
                EmptyReloadSeconds = ArcheryRules.CrossbowReloadSeconds,
                WorldPickupScale = 1.0f,
                SupportHandLocal = new Vector3(-0.02f, -0.035f, 0.15f),
                SupportHandOnlyWhenAiming = false,
                MuzzleLocal = new Vector3(0.0f, 0.065f, 0.35f),
                ButtLocal = new Vector3(0.0f, -0.01f, -0.45f),
                ActionPartName = "",
                MagazinePartName = "",
                TriggerPartName = "",
                CasingLength = 0.0f,
                ArrowFallSpeed = ArcheryRules.BoltFallSpeed,
                ReloadsAfterEveryShot = true,
            };
        }
    }
}
