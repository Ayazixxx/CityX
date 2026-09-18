using CityX.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CityX.View
{
    /// <summary>
    /// Rain, ported from player/rain_effect.gd, then substantially built out
    /// past the port -- EXPLICIT ASK: "make it look reallllly good."
    ///
    /// It still hangs off the PLAYER, not the camera (Godot's own reasoning,
    /// unchanged): the emission box follows where the player IS, so there is
    /// always rain around them, but camera bob and aiming never drag it
    /// about. Drops are still emitted into WORLD space and left there, so
    /// they fall straight down regardless of the player moving underneath
    /// them.
    ///
    /// NO LONGER "purely local, nothing else can tell it is raining" -- that
    /// was true of the original port and is not true any more. While active
    /// this now also: darkens and wets the shared ground materials, dampens
    /// the sun/ambient light and tightens fog (through LateUpdate, so it
    /// always applies AFTER DayNightCycle's own Update has run this frame --
    /// see its own doc comment), spawns real per-drop ground splashes, and
    /// throws the occasional lightning flash. DayNightCycle itself is
    /// untouched -- it stays exactly as thin as its own doc comment says;
    /// everything above layers on top of whatever it already computed.
    /// </summary>
    public sealed class RainEffect : MonoBehaviour
    {
        public const int DropCount = 900;

        /// <summary>Forty metres square around the player. Past that it is not visible anyway.</summary>
        public static readonly Vector3 EmissionBox = new Vector3(40.0f, 2.0f, 40.0f);

        public const float EmissionHeight = 18.0f;

        /// <summary>
        /// Constant, not accelerating. Rain reaches terminal velocity long
        /// before it gets anywhere near a city, so a drop that speeds up as it
        /// falls reads as something that was dropped rather than as weather.
        /// </summary>
        public const float FallSpeed = 22.0f;

        /// <summary>
        /// Final rendered streak length in metres (see Build's own doc
        /// comment on main.startSize for the real bug this used to have --
        /// this is now a true, direct length, not one factor of a quadratic
        /// blow-up). EXPLICIT FEEDBACK: "improve upon the rain it looks
        /// terrible the drops are too large" -- confirmed via a scratch
        /// render that the ORIGINAL 0.9/0.03 pair rendered as 27-real-metre
        /// columns, not rain. 0.4m reads as a real motion-blurred streak at
        /// FallSpeed's own 22 m/s without looking like a solid rod.
        /// </summary>
        public const float DropHeight = 0.4f;

        /// <summary>Final rendered streak width/thickness in metres -- see DropHeight's own doc comment.</summary>
        public const float DropWidth = 0.02f;

        /// <summary>Pale blue-white. The same drop colour the title screen's rain uses.</summary>
        public static readonly Color DropColour =
            new Color(200.0f / 255.0f, 210.0f / 255.0f, 255.0f / 255.0f, 0.55f);

        /// <summary>A little brighter/more opaque than a drop -- a splash is a real, if brief, disturbance.</summary>
        public static readonly Color SplashColour =
            new Color(210.0f / 255.0f, 220.0f / 255.0f, 255.0f / 255.0f, 0.65f);

        /// <summary>
        /// How long the environment takes to fully settle into or out of a
        /// storm once toggled -- EXPLICIT ASK ("really good"), not an instant
        /// snap. Covers the sun/ambient dampening, the fog tightening, the
        /// grade shift and the wet ground -- one ramp, everything storm-
        /// related rides on it together, so nothing can visibly lead or lag
        /// the others.
        /// </summary>
        public const float StormRampSeconds = 3.0f;

        public const float SunStormMultiplier = 0.5f;
        public const float AmbientStormMultiplier = 0.6f;

        /// <summary>
        /// Reduced-visibility-in-a-storm, a classic weather cue currently
        /// unclaimed by anything -- DayNightCycle only ever touches
        /// RenderSettings.fogColor, never the start/end distance. The CLEAR
        /// values are read back from the scene at Awake rather than hard-
        /// coded here, so this always restores to whatever the scene was
        /// actually built with instead of a guess baked into this file.
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

        /// <summary>
        /// Matches Jetpack.WindFadeSeconds' own convention -- a real
        /// transition rather than the instant Play()/Stop() this used to be.
        /// </summary>
        public const float RainSoundFadeSeconds = 1.5f;

        [Tooltip("rain_noises.mp3, looping while it rains.")]
        public AudioClip RainSound;

        [Tooltip("The unlit transparent material each drop is drawn with.")]
        public Material DropMaterial;

        [Tooltip("The unlit transparent material each ground splash burst is drawn with.")]
        public Material SplashMaterial;

        [Tooltip("The sun -- dimmed while it storms, briefly spiked for lightning. The same Light DayNightCycle drives.")]
        public Light Sun;

        [Tooltip("The shared post-process volume profile (CityGrade) -- desaturated and vignetted a little heavier while it storms.")]
        public VolumeProfile Grade;

        [Tooltip("CityGround/CityPavement/CityConcrete -- darkened and given a wet sheen while it storms.")]
        public Material[] WetGroundMaterials;

        /// <summary>
        /// The scene's own clear-weather fog distances -- EXPLICIT FIELDS,
        /// wired by CityMassingSceneBuilder to the exact same numbers it
        /// sets on RenderSettings itself, rather than read back from
        /// RenderSettings at runtime. EXPLICIT BUG, caught by a real test:
        /// RenderSettings is per-ACTIVE-scene, and there is no single frame
        /// guaranteed to be both "after everything has finished loading" and
        /// "before this component's own Update might already have run" in
        /// every context this scene can be loaded in (multi-scene test
        /// loads, additive loads, whichever order Unity happens to call
        /// different components' Update in) -- discovering the clear value
        /// from RenderSettings at some arbitrary first-Update moment kept
        /// reading a stale/wrong number depending on exactly when that
        /// moment landed. Matches the same values CityMassingSceneBuilder
        /// already sets directly below its own RainEffect wiring, so there
        /// is nothing left to drift out of step.
        /// </summary>
        public float ClearFogStart = 150.0f;
        public float ClearFogEnd = 380.0f;

        private static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private ParticleSystem _particles;
        private ParticleSystem _splashes;
        private AudioSource _sound;
        private Transform _splashPlane;

        private bool _raining;
        private float _stormStrength;
        private float _timeUntilNextLightning;
        private float _lightningFlashRemaining;
        private bool _wasActiveLastFrame;

        private Color[] _clearGroundColours;
        private float[] _clearGroundSmoothness;
        private ColorAdjustments _colorAdjustments;
        private Vignette _vignette;
        private float _clearSaturation;
        private float _clearPostExposure;
        private float _clearVignetteIntensity;

        private bool _capturedClearState;

        public bool IsActive => _particles != null && _particles.isEmitting;

        /// <summary>0..1, how far into/out of a storm the environment currently is. Exposed for tests -- see Jetpack.CurrentTilt's own reasoning.</summary>
        public float StormStrength => _stormStrength;

        private void Awake()
        {
            Build();
            SetActive(false);
        }

        /// <summary>
        /// Reads back whatever the ground materials/grade profile actually
        /// look like, once, so the wet-ground and grade shift below always
        /// have an exact value to ease back to on /weather clear.
        ///
        /// LAZY, NOT IN Awake -- Awake() also fires immediately at scene-
        /// BUILD time (CityMassingSceneBuilder's own
        /// `AddComponent&lt;RainEffect&gt;()` call), before that same builder's
        /// later lines finish wiring Grade/WetGroundMaterials at all (both
        /// are still null at that point -- see CityMassingSceneBuilder's own
        /// late-wiring comment), so capturing there would have captured
        /// nothing. Called from the first real Update instead, by which
        /// point the whole scene, wiring included, is guaranteed to have
        /// finished loading. (Fog's own clear values are NOT captured this
        /// way any more -- see ClearFogStart/ClearFogEnd's own doc comment
        /// for why discovering them from RenderSettings at runtime turned
        /// out to be unreliable in exactly this kind of multi-scene context.)
        /// </summary>
        private void CaptureClearState()
        {
            if (_capturedClearState)
            {
                return;
            }
            _capturedClearState = true;

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
            host.transform.localPosition = new Vector3(0.0f, EmissionHeight, 0.0f);

            _particles = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = DropCount;
            main.startLifetime = EmissionHeight / FallSpeed;
            main.startSpeed = new ParticleSystem.MinMaxCurve(FallSpeed, FallSpeed * 1.1f);
            // DropWidth, NOT DropHeight -- THE REAL BUG behind "the drops
            // are too large." For a Stretch-mode renderer, Unity treats
            // main.startSize as the quad's WIDTH (perpendicular to travel)
            // and stretches its LENGTH to size * lengthScale. This was
            // set to DropHeight, and lengthScale below is DropHeight /
            // DropWidth -- so the actual rendered length worked out to
            // DropHeight * (DropHeight / DropWidth) = DropHeight^2 /
            // DropWidth, a QUADRATIC blow-up neither constant's own name
            // suggested (0.9^2 / 0.03 = 27 real metres long, confirmed by
            // a scratch render showing rain as giant thick columns, not
            // streaks). Width has to seed startSize for the length formula
            // below to actually produce DropHeight metres of length.
            main.startSize = DropWidth;
            main.startColor = DropColour;
            main.gravityModifier = 0.0f;
            // WORLD, so a drop stays where it was emitted and falls straight
            // down while the player runs out from under it.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = _particles.emission;
            // The rate that keeps DropCount alive at once: each drop lives
            // exactly as long as it takes to fall EmissionHeight, so the count
            // and the rate cannot drift apart when either is retuned.
            emission.rateOverTime = DropCount / main.startLifetime.constant;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = EmissionBox;
            shape.rotation = new Vector3(90.0f, 0.0f, 0.0f);   // pointing down

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            // Stretched along its own travel rather than billboarded flat.
            // Godot achieves the same with a fixed-Y billboard: a drop has to
            // read as a falling streak from any horizontal angle, and a plain
            // camera-facing quad goes edge-on and vanishes from some of them.
            renderer.lengthScale = DropHeight / Mathf.Max(DropWidth, 0.0001f);
            renderer.velocityScale = 0.0f;
            renderer.sharedMaterial = DropMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            BuildSplashes(host.transform);

            // A SINGLE INVISIBLE PLANE, tracked to the player's own real
            // ground height every frame (see Update), rather than colliding
            // drops against the real city's own world colliders. The real
            // city is thousands of streamed colliders -- this project has
            // repeatedly flagged runtime performance as its top priority,
            // and per-drop collision against all of that would scale with
            // city size for zero real benefit this close to the player. A
            // single Planes-mode collider is O(1) regardless of city size
            // and still fires a genuine per-particle collision event exactly
            // where each drop actually meets the ground nearby.
            var planeObject = new GameObject("SplashPlane");
            planeObject.transform.SetParent(transform, false);
            _splashPlane = planeObject.transform;

            ParticleSystem.CollisionModule collision = _particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.dampen = 0.0f;
            collision.bounce = 0.0f;
            collision.lifetimeLoss = 1.0f;
            collision.SetPlane(0, _splashPlane);

            ParticleSystem.SubEmittersModule subEmitters = _particles.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(_splashes, ParticleSystemSubEmitterType.Collision,
                ParticleSystemSubEmitterProperties.InheritNothing, 1.0f);

            _sound = gameObject.AddComponent<AudioSource>();
            _sound.clip = RainSound;
            _sound.loop = true;
            _sound.playOnAwake = false;
            _sound.spatialBlend = 0.0f;
            _sound.volume = 0.0f;
        }

        /// <summary>
        /// A tiny burst of quickly-fading dots, fired automatically wherever
        /// a drop actually crosses the splash plane -- see SubEmittersModule
        /// wiring above. Never Play()'d directly; it only ever emits when
        /// triggered as a Collision sub-emitter.
        /// </summary>
        private void BuildSplashes(Transform parent)
        {
            var host = new GameObject("Splashes");
            host.transform.SetParent(parent, false);

            _splashes = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _splashes.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.35f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = SplashColour;
            main.gravityModifier = 0.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = _splashes.emission;
            emission.rateOverTime = 0.0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, 4) });

            // Sphere, not a directional cone -- a tiny omnidirectional pop
            // reads as a splash from every angle without needing the shape's
            // own rotation tuned to point along a surface normal this plane
            // does not track precisely anyway (see the plane's own comment).
            ParticleSystem.ShapeModule shape = _splashes.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = SplashMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void SetActive(bool active)
        {
            _raining = active;
            if (_particles == null)
            {
                return;
            }

            if (active)
            {
                // STARTED ALREADY MID-STORM. Turning rain on should not look
                // like an empty box visibly filling with drops over the next
                // second -- so the system is run forward by one drop's whole
                // fall before the first frame anybody sees.
                _particles.Simulate(_particles.main.startLifetime.constant, true, true);
                _particles.Play();
            }
            else
            {
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            // The sound itself fades in/out in Update rather than snapping
            // here -- see RainSoundFadeSeconds' own doc comment.
        }

        private void Update()
        {
            CaptureClearState();
            TrackSplashPlane();
            TickRainSound(Time.deltaTime);
            _stormStrength = StormRamp(_stormStrength, _raining ? 1.0f : 0.0f, Time.deltaTime);
            ApplyWetGround(_stormStrength);
            TickLightning(Time.deltaTime);
        }

        /// <summary>
        /// Keeps the collision plane under the player at their REAL current
        /// ground height (CurrentWorld's own pure terrain function, correct
        /// regardless of whether the nearby chunk mesh has actually streamed
        /// in yet -- same reasoning PlayerLocomotion.RecoverFromFallingThroughTheWorld
        /// already relies on it for). X/Z track the player; Y does not --
        /// splashes have to stay at true ground level even while the player
        /// is himself well above it (mid-jump, using the jetpack, etc).
        /// </summary>
        private void TrackSplashPlane()
        {
            if (_splashPlane == null)
            {
                return;
            }
            TerrainField terrain = CurrentWorld.Instance?.Terrain;
            if (terrain == null)
            {
                return;
            }
            double groundY = terrain.ContinuousHeightAt(transform.position.x, transform.position.z);
            _splashPlane.position = new Vector3(transform.position.x, (float)groundY, transform.position.z);
            _splashPlane.rotation = Quaternion.identity;
        }

        private void TickRainSound(float deltaTime)
        {
            if (_sound == null || _sound.clip == null)
            {
                return;
            }
            if (_raining && !_sound.isPlaying)
            {
                _sound.volume = 0.0f;
                _sound.Play();
            }
            float target = _raining ? 1.0f : 0.0f;
            _sound.volume = Mathf.MoveTowards(_sound.volume, target, deltaTime / RainSoundFadeSeconds);
            if (!_raining && _sound.volume <= 0.0f && _sound.isPlaying)
            {
                _sound.Stop();
            }
        }

        private void ApplyWetGround(float stormStrength)
        {
            if (WetGroundMaterials == null)
            {
                return;
            }
            for (int i = 0; i < WetGroundMaterials.Length; i++)
            {
                Material material = WetGroundMaterials[i];
                if (material == null)
                {
                    continue;
                }
                material.SetColor(BaseColourId, WetColour(_clearGroundColours[i], stormStrength));
                material.SetFloat(SmoothnessId, WetSmoothnessFor(_clearGroundSmoothness[i], stormStrength));
            }
        }

        private void TickLightning(float deltaTime)
        {
            if (_lightningFlashRemaining > 0.0f)
            {
                _lightningFlashRemaining -= deltaTime;
            }

            if (!_raining)
            {
                return;
            }

            _timeUntilNextLightning -= deltaTime;
            if (_timeUntilNextLightning <= 0.0f)
            {
                _lightningFlashRemaining = LightningFlashSeconds + LightningDecaySeconds;
                _timeUntilNextLightning =
                    Random.Range(LightningMinIntervalSeconds, LightningMaxIntervalSeconds);
            }
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
        /// the instant _stormStrength first reaches exactly 0 (the naive,
        /// "nothing to do" guard) meant the frame BEFORE that -- the last
        /// one that still had a tiny nonzero storm strength, and so still
        /// wrote a very slightly tightened fog value -- was the LAST time
        /// fog ever got written at all. Fog then sat at that almost-but-
        /// not-quite-clear value forever, since nothing else ever resets
        /// it. `_wasActiveLastFrame` lets this run exactly one more time
        /// once storm strength/lightning both reach zero, long enough to
        /// Lerp at t=0 (which resolves to the exact clear value) before the
        /// early-return skip actually takes over for real.
        /// </summary>
        private void LateUpdate()
        {
            bool activeNow = _stormStrength > 0.0f || _lightningFlashRemaining > 0.0f;
            if (!activeNow && !_wasActiveLastFrame)
            {
                return;
            }
            _wasActiveLastFrame = activeNow;

            float lightningBoost = LightningBoost(
                _lightningFlashRemaining, LightningFlashSeconds, LightningDecaySeconds);

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
        /// to 0 over decaySeconds. Pure and static: given nothing but the
        /// countdown and the two durations, always answers the same way,
        /// which is what makes "still fully bright partway through the
        /// hold" and "half-decayed at the decay's own midpoint" both directly
        /// testable without waiting out a real random interval.
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
        /// with `*` would scale alpha too, which is the kind of thing that
        /// only shows up once a material happens to have alpha < 1).
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
