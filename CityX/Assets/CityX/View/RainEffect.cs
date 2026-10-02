using System.Collections.Generic;
using CityX.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CityX.View
{
    /// <summary>
    /// Rain, ported from player/rain_effect.gd, built out into a storm
    /// (EXPLICIT ASK: "make it look reallllly good"), and now rebuilt around
    /// what shipped games do with rain (EXPLICIT ASK: "research good rain
    /// mechanics in video games like mine and implement what you learn" --
    /// see docs/RAIN.md for the sources).
    ///
    /// It still hangs off the PLAYER, not the camera (Godot's own reasoning,
    /// unchanged): the emission box follows where the player IS, so there is
    /// always rain around them, but camera bob and aiming never drag it about.
    /// Drops are emitted into WORLD space and left there, so they fall
    /// straight down whatever the player does underneath them.
    ///
    /// WHAT CHANGED, AND WHY EACH THING IS THERE:
    ///
    ///  * RAIN HAS COVER. A <see cref="RainCover"/> map (a grid of downward
    ///    ray casts around the player -- the affordable form of Remember Me's
    ///    "depth map from above") says where the first surface is in every
    ///    column. A drop is only emitted where it can fall, and its life ends
    ///    exactly ON that surface, so nothing falls inside a building or under
    ///    a highway deck, and rain seen through a window falls past it.
    ///  * SPLASHES AND RIPPLES LAND WHERE THE DROP LANDS -- on the roof, the
    ///    skybridge, the car -- not on a fake plane at ground level. Each
    ///    landing is scheduled for the moment its drop arrives. Rings form only
    ///    where enough water has gathered (wet enough ground).
    ///  * WETNESS IS ITS OWN STATE. It rises quickly when rain starts and
    ///    falls slowly when it stops, so the city glistens long after the sky
    ///    clears; sheen fades before darkening does. The storm's MOOD (dim
    ///    sun, tight fog, grey grade) still ramps on its own 3-second clock.
    ///  * INTENSITY. Drizzle to downpour: how many drops, how loud, how dark,
    ///    how wet, and whether there is lightning at all.
    ///  * THE SOUND KNOWS WHERE YOU ARE. Under cover the rain is quieter and
    ///    muffled, thunder included; thunder arrives after its flash by the
    ///    speed of sound.
    ///  * LIGHTNING IS A BOLT, NOT JUST A BRIGHT SKY: a jagged bolt strikes a
    ///    tall building at a real distance, the sky flashes by how close it
    ///    was, and synthesised thunder (ThunderSynth) follows.
    ///
    /// It also PUBLISHES itself through <see cref="CurrentWeather"/> so the
    /// rest of the game (traction, climbing, flames, pedestrians, the wet
    /// look of the player) reacts to the same rain the player can see.
    /// DayNightCycle itself is untouched: everything here layers on top of
    /// whatever it already computed.
    /// </summary>
    public sealed class RainEffect : MonoBehaviour
    {
        public const int DropCount = 900;

        /// <summary>Forty metres square around the player. Past that it is not visible anyway.</summary>
        public static readonly Vector3 EmissionBox = new Vector3(40.0f, 2.0f, 40.0f);

        public const float EmissionHeight = 18.0f;

        /// <summary>
        /// The longest a drop is drawn falling, in metres: from a rooftop over
        /// a street canyon the ground is far below, and a drop that long would
        /// cost a great many live particles for rain nobody can see. A drop cut
        /// short by this simply ends in mid-air, unseen at that range, with no
        /// splash.
        /// </summary>
        public const float MaxDropFall = 36.0f;

        /// <summary>
        /// Constant, not accelerating. Rain reaches terminal velocity long
        /// before it gets anywhere near a city, so a drop that speeds up as it
        /// falls reads as something that was dropped rather than as weather.
        /// </summary>
        public const float FallSpeed = 22.0f;

        /// <summary>
        /// Final rendered streak length in metres (see Build's own doc
        /// comment on main.startSize for the real bug this used to have --
        /// this is now a true, direct length). EXPLICIT FEEDBACK: "improve upon
        /// the rain it looks terrible the drops are too large".
        /// </summary>
        public const float DropHeight = 0.55f;

        /// <summary>Final rendered streak width/thickness in metres -- see DropHeight's own doc comment.</summary>
        public const float DropWidth = 0.03f;

        /// <summary>
        /// How far above the surface a ripple ring is laid, in metres. A ring is
        /// flat and about a metre across, and a street tilts with the hill it is
        /// on: a few percent of grade buries one side of a ring that hugs the
        /// ground, which showed as a broken, dashed arc.
        /// </summary>
        public const float RippleLift = 0.09f;

        /// <summary>The widest a ring spreads, in metres: rain on a puddle is a dense scatter of small rings, not a few big ones.</summary>
        public const float RippleSize = 0.55f;

        /// <summary>The share of Weather.RippleChance actually drawn: every landing on soaked ground would put a ring on it, which reads as a carpet of hoops.</summary>
        public const float RippleDensity = 0.5f;

        /// <summary>The smallest a drop is allowed to be drawn, as a fraction of the screen: about 1.5 pixels at 720p.</summary>
        public const float MinDropScreenFraction = 0.002f;

        /// <summary>Pale blue-white. The same drop colour the title screen's rain uses.</summary>
        public static readonly Color DropColour =
            new Color(200.0f / 255.0f, 210.0f / 255.0f, 255.0f / 255.0f, 0.8f);

        /// <summary>A little brighter/more opaque than a drop -- a splash is a real, if brief, disturbance.</summary>
        public static readonly Color SplashColour =
            new Color(210.0f / 255.0f, 220.0f / 255.0f, 255.0f / 255.0f, 0.65f);

        /// <summary>The pale ring of a ripple on standing water.</summary>
        public static readonly Color RippleColour =
            new Color(220.0f / 255.0f, 230.0f / 255.0f, 255.0f / 255.0f, 0.6f);

        /// <summary>
        /// How long the environment takes to fully settle into or out of a
        /// storm once toggled -- EXPLICIT ASK ("really good"), not an instant
        /// snap. Covers the sun/ambient dampening, the fog tightening and the
        /// grade shift; rain thickening and thinning rides the same clock.
        /// </summary>
        public const float StormRampSeconds = 3.0f;

        public const float SunStormMultiplier = 0.5f;
        public const float AmbientStormMultiplier = 0.6f;

        /// <summary>
        /// Reduced-visibility-in-a-storm, a classic weather cue currently
        /// unclaimed by anything -- DayNightCycle only ever touches
        /// RenderSettings.fogColor, never the start/end distance.
        /// </summary>
        public const float StormFogStart = 60.0f;
        public const float StormFogEnd = 170.0f;

        public const float WetSmoothness = 0.6f;

        /// <summary>How much darker the ground reads once properly soaked.</summary>
        public const float WetColourMultiplier = 0.65f;

        public const float LightningMinIntervalSeconds = 8.0f;
        public const float LightningMaxIntervalSeconds = 20.0f;
        public const float LightningFlashSeconds = 0.08f;
        public const float LightningDecaySeconds = 0.4f;
        public const float LightningSunMultiplier = 4.0f;
        public const float LightningAmbientMultiplier = 2.5f;

        /// <summary>The nearest and furthest a random strike lands, in metres from the player.</summary>
        public const float StrikeMinDistance = 150.0f;
        public const float StrikeMaxDistance = 1600.0f;

        /// <summary>How long the bolt itself is on screen.</summary>
        public const float BoltSeconds = 0.18f;

        /// <summary>
        /// Matches Jetpack.WindFadeSeconds' own convention -- a real
        /// transition rather than the instant Play()/Stop() this used to be.
        /// </summary>
        public const float RainSoundFadeSeconds = 1.5f;

        /// <summary>The slowest the cover-driven muffling of the sound moves, so stepping under a roof fades rather than clicks.</summary>
        public const float ShelterEaseSecondsPerUnit = 0.35f;

        [Tooltip("rain_noises.mp3, looping while it rains.")]
        public AudioClip RainSound;

        [Tooltip("The unlit transparent material each drop is drawn with.")]
        public Material DropMaterial;

        [Tooltip("The unlit transparent material each ground splash burst is drawn with.")]
        public Material SplashMaterial;

        [Tooltip("The unlit transparent ring material each ripple on wet ground is drawn with.")]
        public Material RippleMaterial;

        [Tooltip("The sun -- dimmed while it storms, briefly spiked for lightning. The same Light DayNightCycle drives.")]
        public Light Sun;

        [Tooltip("The day/night cycle -- greys the sky, fog and camera background while it storms and lights them for lightning. Found in the scene if left empty.")]
        public DayNightCycle Cycle;

        [Tooltip("The shared post-process volume profile (CityGrade) -- desaturated and vignetted a little heavier while it storms.")]
        public VolumeProfile Grade;

        [Tooltip("CityGround/CityPavement/CityConcrete -- darkened and given a wet sheen while the city is wet.")]
        public Material[] WetGroundMaterials;

        /// <summary>
        /// What each of <see cref="WetGroundMaterials"/> looks like DRY -- the fixed truth,
        /// set by the scene builder from the same constants that build the materials.
        ///
        /// WHY THE ASSETS ARE NOT TRUSTED TO SAY. Rain edits those shared materials in
        /// place. This used to remember "dry" by reading the assets when it started -- so
        /// if anything ever left them soaked on disk (a crash, an older build, a save
        /// while it was raining), the next run captured the SOAKED look as normal and
        /// restored to it forever: the street stayed wet with the rain long off. With
        /// the dry look stated here, a stale soaked asset is put right the moment the
        /// game loads.
        /// </summary>
        public Color[] DryGroundColours;

        public float[] DryGroundSmoothness;

        /// <summary>
        /// The scene's own clear-weather fog distances -- EXPLICIT FIELDS,
        /// wired by CityMassingSceneBuilder to the exact same numbers it
        /// sets on RenderSettings itself, rather than read back from
        /// RenderSettings at runtime. EXPLICIT BUG, caught by a real test:
        /// RenderSettings is per-ACTIVE-scene, and there is no single frame
        /// guaranteed to be both "after everything has finished loading" and
        /// "before this component's own Update might already have run" in
        /// every context this scene can be loaded in.
        /// </summary>
        public float ClearFogStart = 150.0f;
        public float ClearFogEnd = 380.0f;

        private static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        /// <summary>The facade shader reads how wet the city is from this global (0 dry .. 1 soaked): walls darken and shine like the ground.</summary>
        private static readonly int FacadeWetnessId = Shader.PropertyToID("_CityXWetness");

        private ParticleSystem _particles;
        private ParticleSystem _splashes;
        private ParticleSystem _ripples;
        private AudioSource _sound;
        private AudioLowPassFilter _soundFilter;
        private AudioSource _thunderSound;
        private AudioLowPassFilter _thunderFilter;

        private RainCover _cover;

        private float _targetIntensity;
        private float _intensity;
        private float _wetness;
        private float _appliedWetness = -1.0f;
        private float _stormStrength;
        private float _timeUntilNextLightning;
        private float _lightningFlashRemaining;
        private float _flashStrength = 1.0f;
        private bool _wasActiveLastFrame;
        private float _warmupRemaining;
        private float _dropDebt;
        private float _soundShelter;

        private struct Landing
        {
            public float Time;
            public Vector3 Position;
        }

        private readonly List<Landing> _landings = new List<Landing>(2048);

        private struct PendingThunder
        {
            public float Time;
            public float Distance;
        }

        private readonly List<PendingThunder> _thunder = new List<PendingThunder>(8);
        private readonly Dictionary<int, AudioClip> _thunderClips = new Dictionary<int, AudioClip>();

        private LineRenderer[] _boltLines;
        private float _boltRemaining;
        private Material _boltMaterial;
        private Material _haloMaterial;
        private int _strikeCounter;

        private Color[] _clearGroundColours;
        private float[] _clearGroundSmoothness;
        private ColorAdjustments _colorAdjustments;
        private Vignette _vignette;
        private float _clearSaturation;
        private float _clearPostExposure;
        private float _clearVignetteIntensity;

        private bool _capturedClearState;

        /// <summary>Whether rain has been asked for (and has not since been cleared) -- the storm may still be easing in or out.</summary>
        public bool IsActive => _targetIntensity > 0.0f;

        /// <summary>The intensity asked for, 0..1: 0 clear, 0.25 light, 0.55 moderate, 1 heavy.</summary>
        public float TargetIntensity => _targetIntensity;

        /// <summary>The precipitation right now, 0..1 -- eases toward the target over StormRampSeconds so rain builds and thins rather than snaps.</summary>
        public float Intensity => _intensity;

        /// <summary>0..1, how soaked the city is. Lags the rain both ways -- see Weather.StepWetness.</summary>
        public float Wetness => _wetness;

        /// <summary>0..1, how far into/out of a storm the environment currently is. Exposed for tests -- see Jetpack.CurrentTilt's own reasoning.</summary>
        public float StormStrength => _stormStrength;

        /// <summary>The sky-to-surface map; null until Awake has run.</summary>
        public RainCover Cover => _cover;

        /// <summary>0..1 how sheltered the player's ears are right now (eased) -- what the muffling follows.</summary>
        public float SoundShelter => _soundShelter;

        /// <summary>Drops in the air and rings and splashes alive, for tests and profiling.</summary>
        public int LiveDropCount => _particles != null ? _particles.particleCount : 0;

        public int LiveRippleCount => _ripples != null ? _ripples.particleCount : 0;

        /// <summary>Rings ever started, for tests and profiling.</summary>
        public int RipplesEmitted { get; private set; }

        public int PendingLandingCount => _landings.Count;

        public int PendingThunderCount => _thunder.Count;

        /// <summary>0..1 how hard a strike is lighting the world right now (0 between strikes), for tests.</summary>
        public float FlashNow => LightningBoost(_lightningFlashRemaining, LightningFlashSeconds, LightningDecaySeconds) * _flashStrength;

        /// <summary>True while a bolt is drawn.</summary>
        public bool BoltVisible => _boltRemaining > 0.0f;

        /// <summary>Splash particles alive right now.</summary>
        public int LiveSplashCount => _splashes != null ? _splashes.particleCount : 0;

        /// <summary>Thunder claps actually played so far -- for tests.</summary>
        public int ThunderPlayCount { get; private set; }

        private ParticleSystem.Particle[] _scratch = new ParticleSystem.Particle[0];

        /// <summary>
        /// How many raindrops are in the air inside a world-space box right now.
        /// What lets a test say "nothing is falling under that roof" as a measurement
        /// rather than a hope.
        /// </summary>
        public int CountLiveDropsIn(Bounds worldBounds)
        {
            if (_particles == null)
            {
                return 0;
            }
            int live = _particles.particleCount;
            if (_scratch.Length < live)
            {
                _scratch = new ParticleSystem.Particle[Mathf.Max(live, 512)];
            }
            live = _particles.GetParticles(_scratch, live);
            int inside = 0;
            for (int i = 0; i < live; i++)
            {
                if (worldBounds.Contains(_scratch[i].position))
                {
                    inside++;
                }
            }
            return inside;
        }

        /// <summary>
        /// Puts the city at a wetness at once -- for tests and the console, where
        /// waiting a minute of rain to see wet streets is not a way to iterate.
        /// </summary>
        public void Soak(float wetness)
        {
            _wetness = Mathf.Clamp01(wetness);
        }

        private void Awake()
        {
            _cover = new RainCover(RainCover.CastFromSky);
            Build();
            SetRain(0.0f);
        }

        private void OnDisable()
        {
            CurrentWeather.Clear();
            RestoreGroundMaterials();
        }

        /// <summary>
        /// Puts the shared ground, pavement and concrete materials back to
        /// exactly how they looked dry.
        ///
        /// THE WETNESS USED TO STICK. Those three are SHARED ASSETS, and rain
        /// edits them in place -- so anything that stopped the rain without
        /// waiting for it to dry (a test ending, leaving Play Mode, the
        /// component being destroyed) left them dark and glossy IN THE ASSET,
        /// and the next editor tool that saved assets (every scene builder does)
        /// wrote that soaked look to disk: the whole city then looked different
        /// in the game, in dry weather. Restoring on disable and destroy means
        /// the assets are only ever wet while rain is actually running.
        /// </summary>
        private void RestoreGroundMaterials()
        {
            if (WetGroundMaterials == null || _clearGroundColours == null || _clearGroundSmoothness == null)
            {
                return;
            }
            for (int i = 0; i < WetGroundMaterials.Length && i < _clearGroundColours.Length; i++)
            {
                Material material = WetGroundMaterials[i];
                if (material == null)
                {
                    continue;
                }
                material.SetColor(BaseColourId, _clearGroundColours[i]);
                material.SetFloat(SmoothnessId, _clearGroundSmoothness[i]);
            }
            _appliedWetness = -1.0f;
            Shader.SetGlobalFloat(FacadeWetnessId, 0.0f);
        }

        private void OnDestroy()
        {
            RestoreGroundMaterials();
            if (CurrentWeather.Cover == _cover)
            {
                CurrentWeather.Clear();
            }

            // Runtime-made assets belong to this component.
            if (_boltMaterial != null)
            {
                Destroy(_boltMaterial);
            }
            if (_haloMaterial != null)
            {
                Destroy(_haloMaterial);
            }
            foreach (AudioClip clip in _thunderClips.Values)
            {
                if (clip != null)
                {
                    Destroy(clip);
                }
            }
            _thunderClips.Clear();
        }

        /// <summary>
        /// Reads back whatever the ground materials/grade profile actually
        /// look like, once, so the wet-ground and grade shift below always
        /// have an exact value to ease back to on /weather clear.
        ///
        /// LAZY, NOT IN Awake -- Awake() also fires immediately at scene-
        /// BUILD time (CityMassingSceneBuilder's own
        /// `AddComponent&lt;RainEffect&gt;()` call), before that same builder's
        /// later lines finish wiring Grade/WetGroundMaterials at all, so
        /// capturing there would have captured nothing. Called from the first
        /// real Update instead.
        /// </summary>
        private void CaptureClearState()
        {
            if (_capturedClearState)
            {
                return;
            }
            _capturedClearState = true;

            if (Cycle == null)
            {
                Cycle = FindFirstObjectByType<DayNightCycle>();
            }

            if (WetGroundMaterials != null)
            {
                _clearGroundColours = new Color[WetGroundMaterials.Length];
                _clearGroundSmoothness = new float[WetGroundMaterials.Length];
                for (int i = 0; i < WetGroundMaterials.Length; i++)
                {
                    Material material = WetGroundMaterials[i];
                    if (material == null)
                    {
                        continue;
                    }
                    _clearGroundColours[i] = material.GetColor(BaseColourId);
                    _clearGroundSmoothness[i] = material.GetFloat(SmoothnessId);

                    // The stated dry look wins over whatever the asset says right now
                    // (see DryGroundColours): a stale soaked asset is healed by the
                    // first ApplyWetGround, which writes this at zero wetness.
                    if (DryGroundColours != null && i < DryGroundColours.Length)
                    {
                        _clearGroundColours[i] = DryGroundColours[i];
                    }
                    if (DryGroundSmoothness != null && i < DryGroundSmoothness.Length)
                    {
                        _clearGroundSmoothness[i] = DryGroundSmoothness[i];
                    }
                }
            }

            if (Grade != null)
            {
                Grade.TryGet(out _colorAdjustments);
                Grade.TryGet(out _vignette);
                if (_colorAdjustments != null)
                {
                    _clearSaturation = _colorAdjustments.saturation.value;
                    _clearPostExposure = _colorAdjustments.postExposure.value;
                }
                if (_vignette != null)
                {
                    _clearVignetteIntensity = _vignette.intensity.value;
                }
            }
        }

        private void Build()
        {
            var host = new GameObject("Drops");
            host.transform.SetParent(transform, false);

            _particles = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 3200;
            // Set per drop when emitted (EmitOneDrop): each lives exactly as
            // long as it takes to reach the first surface below it.
            main.startLifetime = EmissionHeight / FallSpeed;
            main.startSpeed = FallSpeed;
            // DropWidth, NOT DropHeight -- THE REAL BUG behind "the drops
            // are too large." For a Stretch-mode renderer, Unity treats
            // main.startSize as the quad's WIDTH (perpendicular to travel)
            // and stretches its LENGTH to size * lengthScale. It was set to
            // DropHeight, and lengthScale below is DropHeight / DropWidth --
            // so the rendered length worked out to DropHeight^2 / DropWidth,
            // 27 real metres at the original numbers.
            main.startSize = DropWidth;
            main.startColor = DropColour;
            main.gravityModifier = 0.0f;
            // WORLD, so a drop stays where it was emitted and falls straight
            // down while the player runs out from under it.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Nothing is emitted by rate or by shape: every drop is placed by
            // script, in a column that has somewhere to fall (EmitOneDrop).
            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = false;

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            // Stretched along its own travel rather than billboarded flat: a
            // drop has to read as a falling streak from any horizontal angle.
            renderer.lengthScale = DropHeight / Mathf.Max(DropWidth, 0.0001f);
            renderer.velocityScale = 0.0f;
            // A drop far down the street would be thinner than a pixel and
            // vanish; a floor on its on-screen size keeps distant rain a fine
            // haze of streaks rather than nothing.
            renderer.minParticleSize = MinDropScreenFraction;
            renderer.sharedMaterial = DropMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            BuildSplashes(transform);
            BuildRipples(transform);

            _sound = gameObject.AddComponent<AudioSource>();
            _sound.clip = RainSound;
            _sound.loop = true;
            _sound.playOnAwake = false;
            _sound.spatialBlend = 0.0f;
            _sound.volume = 0.0f;
            _soundFilter = gameObject.AddComponent<AudioLowPassFilter>();
            _soundFilter.cutoffFrequency = 22000.0f;

            var thunderHost = new GameObject("Thunder");
            thunderHost.transform.SetParent(transform, false);
            _thunderSound = thunderHost.AddComponent<AudioSource>();
            _thunderSound.playOnAwake = false;
            _thunderSound.spatialBlend = 0.0f;
            _thunderFilter = thunderHost.AddComponent<AudioLowPassFilter>();
            _thunderFilter.cutoffFrequency = 22000.0f;
        }

        /// <summary>
        /// A tiny burst of quickly-fading dots where a drop lands. Never
        /// Play()'d as a stream; landings are emitted one by one at the
        /// moment their drop arrives (see ProcessLandings).
        /// </summary>
        private void BuildSplashes(Transform parent)
        {
            var host = new GameObject("Splashes");
            host.transform.SetParent(parent, false);

            _splashes = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _splashes.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 1500;
            main.startLifetime = 0.3f;
            main.startSpeed = 0.0f;
            // A real splash is a crown a few centimetres across, not a puff:
            // seen from a metre away the old 15-35cm dots read as glowing balls.
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = SplashColour;
            main.gravityModifier = 0.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = _splashes.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _splashes.shape;
            shape.enabled = false;

            // Fading out rather than popping out at the end of its life.
            ParticleSystem.ColorOverLifetimeModule fadeOut = _splashes.colorOverLifetime;
            fadeOut.enabled = true;
            var splashFade = new Gradient();
            splashFade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
            fadeOut.color = splashFade;

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = SplashMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Rings on standing water: flat on the surface (a horizontal
        /// billboard), swelling from a point to about a metre across over
        /// three quarters of a second while fading out. Only fired where
        /// enough water has gathered -- see Weather.RippleChance.
        /// </summary>
        private void BuildRipples(Transform parent)
        {
            var host = new GameObject("Ripples");
            host.transform.SetParent(parent, false);

            _ripples = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _ripples.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 700;
            main.startLifetime = 0.6f;
            main.startSpeed = 0.0f;
            main.startSize = RippleSize;
            main.startColor = RippleColour;
            main.gravityModifier = 0.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = _ripples.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _ripples.shape;
            shape.enabled = false;

            // Swells from a dot to full size, fast at first then easing out.
            ParticleSystem.SizeOverLifetimeModule size = _ripples.sizeOverLifetime;
            size.enabled = true;
            var swell = new AnimationCurve(
                new Keyframe(0.0f, 0.12f, 0.0f, 2.2f),
                new Keyframe(1.0f, 1.0f, 0.4f, 0.0f));
            size.size = new ParticleSystem.MinMaxCurve(1.0f, swell);

            ParticleSystem.ColorOverLifetimeModule colour = _ripples.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.12f), new GradientAlphaKey(0.0f, 1.0f) });
            colour.color = fade;

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = RippleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Rain at full strength -- the original on/off switch, kept for every existing caller.</summary>
        public void SetActive(bool active)
        {
            SetRain(active ? Weather.HeavyRain : 0.0f);
        }

        /// <summary>
        /// Rain at an intensity: 0 clear, up to 1 a downpour (see the
        /// Weather.LightRain/ModerateRain/HeavyRain names). Eases in and out.
        /// </summary>
        public void SetRain(float intensity)
        {
            float wanted = Mathf.Clamp01(intensity);
            bool starting = wanted > 0.0f && _targetIntensity <= 0.0f && _intensity <= 0.0f;
            _targetIntensity = wanted;
            if (_particles == null)
            {
                return;
            }

            if (wanted > 0.0f)
            {
                if (!_particles.isPlaying)
                {
                    _particles.Play();
                }
                if (starting)
                {
                    // STARTED ALREADY MID-STORM: for the first drop's whole fall
                    // new drops appear at every height in their column, not only
                    // at the top, so the box does not visibly fill from above.
                    _warmupRemaining = (EmissionHeight + 8.0f) / FallSpeed;
                    _timeUntilNextLightning = Random.Range(LightningMinIntervalSeconds, LightningMaxIntervalSeconds);
                }
            }
        }

        /// <summary>Stops the rain AND dries everything at once -- the console's "dry": clear weather that has not been soaking for two minutes.</summary>
        public void Dry()
        {
            SetRain(0.0f);
            _intensity = 0.0f;
            _wetness = 0.0f;
            _landings.Clear();
            if (_particles != null)
            {
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _splashes.Clear();
                _ripples.Clear();
            }
        }

        private void Update()
        {
            CaptureClearState();
            float deltaTime = Time.deltaTime;

            _intensity = Mathf.MoveTowards(_intensity, _targetIntensity, deltaTime / StormRampSeconds);
            _wetness = Weather.StepWetness(_wetness, _intensity, deltaTime);
            _stormStrength = StormRamp(_stormStrength, Weather.StormTarget(_targetIntensity), deltaTime);

            Vector3 centre = transform.position;
            bool wet = _intensity > 0.0f || _wetness > 0.0f;
            if (wet)
            {
                _cover.Refresh(centre, deltaTime);
            }

            Publish();
            EmitDrops(centre, deltaTime);
            ProcessLandings();
            TickRainSound(centre, deltaTime);
            ApplyWetGround();
            TickLightning(centre, deltaTime);
            TickThunder(centre);
            TickBolt(deltaTime);

            if (_intensity <= 0.0f && _targetIntensity <= 0.0f && _particles.isEmitting)
            {
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void Publish()
        {
            CurrentWeather.Intensity = _intensity;
            CurrentWeather.Wetness = _wetness;
            CurrentWeather.Cover = _cover;
        }

        // --- drops ---

        /// <summary>
        /// Puts this frame's share of drops into the air. The flux is per
        /// square metre of OPEN sky (Weather.DropFlux); a column with a roof
        /// over it is skipped, so the drops that would have fallen there are
        /// simply not made -- which is exactly the rain a sheltered spot
        /// gets: none.
        /// </summary>
        private void EmitDrops(Vector3 centre, float deltaTime)
        {
            if (_intensity <= 0.0f)
            {
                _dropDebt = 0.0f;
                return;
            }
            if (_warmupRemaining > 0.0f)
            {
                _warmupRemaining -= deltaTime;
            }

            float area = EmissionBox.x * EmissionBox.z;
            _dropDebt += Weather.DropFlux(_intensity) * area * deltaTime;
            int count = Mathf.Min((int)_dropDebt, 600);
            _dropDebt -= (int)_dropDebt;
            for (int i = 0; i < count; i++)
            {
                EmitOneDrop(centre, _warmupRemaining > 0.0f ? Random.value : 0.0f);
            }
        }

        /// <summary>
        /// One drop, in a random column of the box. <paramref name="progress"/>
        /// is how far down its fall it starts (0 at the top -- ordinary; more
        /// while a storm is warming up).
        /// </summary>
        private void EmitOneDrop(Vector3 centre, float progress)
        {
            float x = centre.x + Random.Range(-EmissionBox.x * 0.5f, EmissionBox.x * 0.5f);
            float z = centre.z + Random.Range(-EmissionBox.z * 0.5f, EmissionBox.z * 0.5f);
            float top = centre.y + EmissionHeight;

            float surface = _cover.SurfaceHeightAt(x, z);
            if (float.IsNaN(surface))
            {
                surface = centre.y - 2.0f;   // an unmapped cell: fall past the feet
            }
            if (surface >= top - 0.3f)
            {
                return;   // a roof, deck or ceiling above where drops begin: nothing falls here
            }

            float bottom = Mathf.Max(surface, top - MaxDropFall);
            bool lands = bottom <= surface + 0.001f;
            float speed = FallSpeed * Random.Range(1.0f, 1.1f);
            float fall = top - bottom;
            float remaining = fall * (1.0f - progress) / speed;
            if (remaining <= 0.01f)
            {
                return;
            }

            var drop = new ParticleSystem.EmitParams
            {
                position = new Vector3(x, top - fall * progress, z),
                velocity = new Vector3(0.0f, -speed, 0.0f),
                startLifetime = remaining,
            };
            _particles.Emit(drop, 1);

            if (lands && _landings.Count < 3500)
            {
                _landings.Add(new Landing { Time = Time.time + remaining, Position = new Vector3(x, surface, z) });
            }
        }

        /// <summary>
        /// Splashes (and, on wet enough ground, ripples) at every landing
        /// whose drop has now arrived. Swap-removes as it goes: order does not
        /// matter, and a few thousand entries a second is nothing.
        /// </summary>
        private void ProcessLandings()
        {
            if (_landings.Count == 0)
            {
                return;
            }
            float now = Time.time;
            float ripple = Weather.RippleChance(_wetness, _intensity);
            for (int i = _landings.Count - 1; i >= 0; i--)
            {
                Landing landing = _landings[i];
                if (landing.Time > now)
                {
                    continue;
                }
                _landings[i] = _landings[_landings.Count - 1];
                _landings.RemoveAt(_landings.Count - 1);

                Vector3 at = landing.Position + Vector3.up * 0.03f;
                for (int spray = 0; spray < 3; spray++)
                {
                    Vector3 direction = Random.insideUnitSphere;
                    direction.y = Mathf.Abs(direction.y) + 0.6f;
                    _splashes.Emit(new ParticleSystem.EmitParams
                    {
                        position = at,
                        velocity = direction.normalized * Random.Range(0.3f, 0.8f),
                    }, 1);
                }
                if (ripple > 0.0f && Random.value < ripple * RippleDensity)
                {
                    _ripples.Emit(new ParticleSystem.EmitParams { position = landing.Position + Vector3.up * RippleLift }, 1);
                    RipplesEmitted++;
                }
            }
        }

        // --- sound ---

        private void TickRainSound(Vector3 centre, float deltaTime)
        {
            if (_sound == null || _sound.clip == null)
            {
                return;
            }

            // Where the listener stands decides how the rain sounds: under a
            // roof it is quieter and duller. Eased so stepping in and out of
            // cover fades instead of clicking.
            float shelter = CurrentWeather.Shelter(centre + Vector3.up * 1.6f);
            _soundShelter = Mathf.MoveTowards(_soundShelter, shelter, deltaTime / ShelterEaseSecondsPerUnit);

            bool raining = _targetIntensity > 0.0f;
            if (raining && !_sound.isPlaying)
            {
                _sound.volume = 0.0f;
                _sound.Play();
            }
            float target = Weather.RainVolume(_targetIntensity) * Weather.ShelteredVolume(_soundShelter);
            _sound.volume = Mathf.MoveTowards(_sound.volume, target, deltaTime / RainSoundFadeSeconds);
            if (_soundFilter != null)
            {
                _soundFilter.cutoffFrequency = Weather.ShelteredCutoffHz(_soundShelter);
            }
            if (!raining && _sound.volume <= 0.0f && _sound.isPlaying)
            {
                _sound.Stop();
            }
        }

        // --- the ground ---

        /// <summary>
        /// The shared ground materials follow the WETNESS, not the rain: they
        /// darken with it and take on a sheen that is squared, so a drying
        /// street loses its shine well before it loses its darkness.
        /// </summary>
        private void ApplyWetGround()
        {
            if (WetGroundMaterials == null || _clearGroundColours == null)
            {
                return;
            }
            if (Mathf.Approximately(_wetness, _appliedWetness))
            {
                return;
            }
            _appliedWetness = _wetness;
            float sheen = Weather.SheenFactor(_wetness);
            Shader.SetGlobalFloat(FacadeWetnessId, Mathf.Clamp01(_wetness));
            for (int i = 0; i < WetGroundMaterials.Length; i++)
            {
                Material material = WetGroundMaterials[i];
                if (material == null)
                {
                    continue;
                }
                material.SetColor(BaseColourId, WetColour(_clearGroundColours[i], _wetness));
                material.SetFloat(SmoothnessId, WetSmoothnessFor(_clearGroundSmoothness[i], sheen));
            }
        }

        // --- lightning and thunder ---

        private void TickLightning(Vector3 centre, float deltaTime)
        {
            if (_lightningFlashRemaining > 0.0f)
            {
                _lightningFlashRemaining -= deltaTime;
            }

            // Only a real storm has lightning; a shower or drizzle never does.
            if (_targetIntensity < Weather.ThunderstormThreshold)
            {
                return;
            }

            _timeUntilNextLightning -= deltaTime;
            if (_timeUntilNextLightning <= 0.0f)
            {
                float distance = Mathf.Lerp(StrikeMinDistance, StrikeMaxDistance, Mathf.Pow(Random.value, 1.5f));
                StrikeLightning(distance);
                _timeUntilNextLightning = Random.Range(LightningMinIntervalSeconds, LightningMaxIntervalSeconds);
            }
        }

        /// <summary>
        /// A strike at a distance from the player, in front of the camera
        /// where it can be seen: a jagged bolt from the cloud base to the top
        /// of a tall building there, a flash of the sky as bright as that
        /// distance earns, and the thunder queued to arrive after the
        /// speed-of-sound delay. Callable any time (the dev console does).
        /// </summary>
        public void StrikeLightning(float distanceMetres, float bearingDegrees = float.NaN)
        {
            Vector3 origin = transform.position;
            float bearing = Random.Range(0.0f, 360.0f);
            Camera camera = Camera.main;
            if (!float.IsNaN(bearingDegrees))
            {
                bearing = bearingDegrees;   // a compass bearing from world +Z, asked for outright
            }
            else if (camera != null)
            {
                Vector3 forward = camera.transform.forward;
                forward.y = 0.0f;
                if (forward.sqrMagnitude > 0.01f)
                {
                    bearing = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg + Random.Range(-70.0f, 70.0f);
                }
            }
            Vector3 heading = Quaternion.Euler(0.0f, bearing, 0.0f) * Vector3.forward;
            Vector3 ground = origin + heading * distanceMetres;
            Vector3 strike = new Vector3(ground.x, HighestNearby(ground.x, ground.z), ground.z);

            float cloudY = Mathf.Max(strike.y + 380.0f, 480.0f);
            var cloud = new Vector3(strike.x + Random.Range(-60.0f, 60.0f), cloudY, strike.z + Random.Range(-60.0f, 60.0f));
            int seed = 1000 + _strikeCounter++;
            Vector3[] trunk = LightningBolt.Path(cloud, strike, seed, 6);
            Vector3[] fork = LightningBolt.Branch(trunk, seed + 7, 4);
            ShowBolt(trunk, fork, distanceMetres);

            _lightningFlashRemaining = LightningFlashSeconds + LightningDecaySeconds;
            _flashStrength = Weather.FlashStrength(distanceMetres);
            _thunder.Add(new PendingThunder
            {
                Time = Time.time + Weather.ThunderDelaySeconds(distanceMetres),
                Distance = distanceMetres,
            });
        }

        /// <summary>The height lightning would strike near a point: the tallest roof within a few chunks, else the ground -- it goes for tall things.</summary>
        private static float HighestNearby(float x, float z)
        {
            CityWorld world = CurrentWorld.Instance;
            if (world == null)
            {
                return 0.0f;
            }
            int chunkX = Mathf.FloorToInt(x / CityLayout.ChunkSize);
            int chunkY = Mathf.FloorToInt(z / CityLayout.ChunkSize);
            float best = (float)world.Terrain.ContinuousHeightAt(x, z);
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    System.Collections.Generic.List<int> owned = world.BuildingsOwnedBy(chunkX + dx, chunkY + dy);
                    for (int i = 0; i < owned.Count; i++)
                    {
                        BuildingSite site = world.Building(owned[i]);
                        if (site.Top > best)
                        {
                            best = site.Top;
                        }
                    }
                }
            }
            return best;
        }

        private void ShowBolt(Vector3[] trunk, Vector3[] fork, float distanceMetres)
        {
            if (_boltLines == null)
            {
                _boltLines = new LineRenderer[4];
                for (int i = 0; i < _boltLines.Length; i++)
                {
                    var go = new GameObject(BoltLineNames[i]);
                    go.transform.SetParent(transform, false);
                    LineRenderer line = go.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.numCapVertices = 0;
                    line.shadowCastingMode = ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    line.sharedMaterial = i >= 2 ? HaloMaterial() : BoltMaterial();
                    line.enabled = false;
                    _boltLines[i] = line;
                }
            }

            // Wide enough to read at any distance: a bolt a kilometre away is
            // still a few pixels thick.
            float width = Mathf.Clamp(distanceMetres * 0.0035f, 0.35f, 7.0f);
            SetBolt(_boltLines[0], trunk, width);
            SetBolt(_boltLines[1], fork, width * 0.45f);
            // Two dim, wider copies of the trunk under it: stacked additively they
            // fall off in steps, which reads as the glow a real bolt has.
            SetBolt(_boltLines[2], trunk, width * 3.0f);
            SetBolt(_boltLines[3], trunk, width * 8.0f);
            _boltRemaining = BoltSeconds;
        }

        private static void SetBolt(LineRenderer line, Vector3[] path, float width)
        {
            if (path == null)
            {
                line.enabled = false;
                return;
            }
            line.positionCount = path.Length;
            line.SetPositions(path);
            line.startWidth = width;
            line.endWidth = width * 0.5f;
            line.enabled = true;
        }

        private static readonly string[] BoltLineNames = { "BoltTrunk", "BoltFork", "BoltGlow", "BoltGlowWide" };

        /// <summary>The dim material of the bolt's glow layers: the bolt's own colour at a small fraction of its brightness.</summary>
        private Material HaloMaterial()
        {
            if (_haloMaterial != null)
            {
                return _haloMaterial;
            }
            _haloMaterial = new Material(BoltMaterial());
            Color glow = _boltMaterial.color;
            _haloMaterial.color = new Color(glow.r * 0.12f, glow.g * 0.12f, glow.b * 0.12f, 1.0f);
            return _haloMaterial;
        }

        private Material BoltMaterial()
        {
            if (_boltMaterial != null)
            {
                return _boltMaterial;
            }
            // Same "Particles/Unlit + additive" recipe BulletTracer uses for a
            // bright streak in this URP project.
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            _boltMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            _boltMaterial.color = new Color(0.78f, 0.86f, 1.0f, 1.0f);
            _boltMaterial.SetFloat("_Surface", 1.0f);
            _boltMaterial.SetFloat("_Blend", 2.0f);   // additive
            return _boltMaterial;
        }

        /// <summary>The bolt flickers -- on, a beat dark, on again -- and is gone: a real strike is a stutter, not a steady line.</summary>
        private void TickBolt(float deltaTime)
        {
            if (_boltRemaining <= 0.0f)
            {
                return;
            }
            _boltRemaining -= deltaTime;
            float elapsed = BoltSeconds - _boltRemaining;
            bool dark = elapsed > 0.05f && elapsed < 0.085f;
            bool done = _boltRemaining <= 0.0f;
            for (int i = 0; i < _boltLines.Length; i++)
            {
                if (_boltLines[i].positionCount > 0)
                {
                    _boltLines[i].enabled = !dark && !done;
                }
            }
        }

        /// <summary>Plays whichever thunder has now had time to arrive.</summary>
        private void TickThunder(Vector3 centre)
        {
            if (_thunder.Count == 0)
            {
                return;
            }
            float now = Time.time;
            for (int i = _thunder.Count - 1; i >= 0; i--)
            {
                if (_thunder[i].Time > now)
                {
                    continue;
                }
                PendingThunder due = _thunder[i];
                _thunder.RemoveAt(i);
                PlayThunder(due.Distance, CurrentWeather.Shelter(centre + Vector3.up * 1.6f));
            }
        }

        private void PlayThunder(float distanceMetres, float shelter)
        {
            AudioClip clip = ThunderClipFor(distanceMetres);
            if (clip == null || _thunderSound == null)
            {
                return;
            }
            _thunderFilter.cutoffFrequency = Weather.ShelteredCutoffHz(shelter);
            _thunderSound.PlayOneShot(clip, Weather.ThunderVolume(distanceMetres) * Weather.ShelteredVolume(shelter));
            ThunderPlayCount++;
        }

        /// <summary>
        /// A thunder clip for a distance: built the first time it is needed
        /// (a few milliseconds of noise shaping) and kept, in one of four
        /// distance bands with two variants each so it is not the same
        /// clap every time.
        /// </summary>
        private AudioClip ThunderClipFor(float distanceMetres)
        {
            int band = distanceMetres < 300.0f ? 0 : distanceMetres < 700.0f ? 1 : distanceMetres < 1200.0f ? 2 : 3;
            float representative = band == 0 ? 180.0f : band == 1 ? 480.0f : band == 2 ? 950.0f : 1500.0f;
            int variant = Random.value < 0.5f ? 0 : 1;
            int key = band * 2 + variant;
            if (!_thunderClips.TryGetValue(key, out AudioClip clip))
            {
                float[] samples = ThunderSynth.Generate(representative, 4000 + key);
                clip = AudioClip.Create($"thunder-{key}", samples.Length, 1, ThunderSynth.SampleRate, false);
                clip.SetData(samples, 0);
                _thunderClips[key] = clip;
            }
            return clip;
        }

        /// <summary>
        /// AFTER DayNightCycle's own Update, guaranteed -- Unity always runs
        /// every component's Update before any component's LateUpdate, so
        /// this only ever dampens whatever DayNightCycle.Apply just set this
        /// same frame, never a stale value from the frame before. Each
        /// frame starts fresh from SkyCycle's own pure math either way, so
        /// there is no accumulation/drift risk in reading and re-writing the
        /// same properties every frame like this.
        ///
        /// STILL RUNS ONE LAST TIME ONCE FULLY CLEAR, NOT JUST WHILE ACTIVE
        /// -- EXPLICIT BUG, caught by a real test asserting the exact
        /// restore rather than "close enough": skipping this body outright
        /// the instant _stormStrength first reaches exactly 0 meant the
        /// frame BEFORE that -- the last one that still had a tiny nonzero
        /// storm strength -- was the LAST time fog ever got written at all.
        /// `_wasActiveLastFrame` lets this run exactly one more time once
        /// storm strength/lightning both reach zero.
        /// </summary>
        private void LateUpdate()
        {
            bool activeNow = _stormStrength > 0.0f || _lightningFlashRemaining > 0.0f;
            if (!activeNow && !_wasActiveLastFrame)
            {
                return;
            }
            _wasActiveLastFrame = activeNow;

            // A far strike is a faint flicker, a near one lights the whole city.
            float lightningBoost = LightningBoost(
                _lightningFlashRemaining, LightningFlashSeconds, LightningDecaySeconds) * _flashStrength;

            // The sky itself: a storm is a grey cloud lid, and a strike lights
            // it. Applied straight away rather than left for DayNightCycle's next
            // Update, so the frame that a flash starts on already has it and the
            // final clear frame puts the sky back exactly.
            if (Cycle != null)
            {
                Cycle.Overcast = _stormStrength;
                Cycle.Flash = lightningBoost;
                Cycle.Apply(SkyCycle.At(Cycle.TimeOfDay));
            }

            float sunMultiplier = Mathf.Lerp(1.0f, SunStormMultiplier, _stormStrength)
                * Mathf.Lerp(1.0f, LightningSunMultiplier, lightningBoost);
            float ambientMultiplier = Mathf.Lerp(1.0f, AmbientStormMultiplier, _stormStrength)
                * Mathf.Lerp(1.0f, LightningAmbientMultiplier, lightningBoost);

            if (Sun != null)
            {
                Sun.intensity *= sunMultiplier;
            }
            RenderSettings.ambientLight *= ambientMultiplier;

            RenderSettings.fogStartDistance = Mathf.Lerp(ClearFogStart, StormFogStart, _stormStrength);
            RenderSettings.fogEndDistance = Mathf.Lerp(ClearFogEnd, StormFogEnd, _stormStrength);

            if (_colorAdjustments != null)
            {
                _colorAdjustments.saturation.value = Mathf.Lerp(_clearSaturation, _clearSaturation - 15.0f, _stormStrength);
                _colorAdjustments.postExposure.value = Mathf.Lerp(_clearPostExposure, _clearPostExposure - 0.3f, _stormStrength);
            }
            if (_vignette != null)
            {
                _vignette.intensity.value = Mathf.Lerp(_clearVignetteIntensity, _clearVignetteIntensity + 0.08f, _stormStrength);
            }
        }

        /// <summary>
        /// A linear ease toward 0 or 1 over StormRampSeconds -- pure and
        /// static so the ramp's own shape is testable without a scene, the
        /// same reasoning Jetpack.EaseTilt/TiltTarget are split out for.
        /// </summary>
        public static float StormRamp(float current, float target, float deltaTime)
        {
            return Mathf.MoveTowards(current, target, deltaTime / StormRampSeconds);
        }

        /// <summary>
        /// How much of a lightning spike to apply right now, 0..1 -- full
        /// brightness for the initial flashSeconds, then a linear decay back
        /// to 0 over decaySeconds. Pure and static.
        /// </summary>
        public static float LightningBoost(float remainingSeconds, float flashSeconds, float decaySeconds)
        {
            if (remainingSeconds <= 0.0f)
            {
                return 0.0f;
            }
            if (remainingSeconds > decaySeconds)
            {
                return 1.0f;
            }
            return Mathf.Clamp01(remainingSeconds / decaySeconds);
        }

        /// <summary>
        /// Darkens toward WetColourMultiplier of the original brightness as
        /// wetFactor rises to 1 -- alpha is deliberately left untouched
        /// (these are opaque ground materials, but scaling a Color directly
        /// with `*` would scale alpha too).
        /// </summary>
        public static Color WetColour(Color original, float wetFactor)
        {
            var darker = new Color(
                original.r * WetColourMultiplier,
                original.g * WetColourMultiplier,
                original.b * WetColourMultiplier,
                original.a);
            return Color.Lerp(original, darker, wetFactor);
        }

        /// <summary>Rises toward WetSmoothness (a slick, reflective wet-asphalt value) as wetFactor rises to 1.</summary>
        public static float WetSmoothnessFor(float original, float wetFactor)
        {
            return Mathf.Lerp(original, WetSmoothness, wetFactor);
        }
    }
}
