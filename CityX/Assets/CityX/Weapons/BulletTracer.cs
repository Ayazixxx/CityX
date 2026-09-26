using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A brief visible streak from muzzle to impact -- EXPLICIT ASK: "i want
    /// you to do bullets the same as csgo... effectively the same (zero
    /// travel time) for all guns... the visible bullet tracers are purely
    /// cosmetic particle effects. They do not affect hit registration."
    ///
    /// PistolWeapon.Fire ALREADY WORKS THIS WAY -- its own class comment
    /// already says "hitscan (instant raycast, not a travelling
    /// projectile)", and the Physics.Raycast call that resolves damage runs
    /// synchronously in the same call, same frame, before this tracer is
    /// even spawned. There is no bullet object, no velocity, nothing to
    /// change there. What was actually missing was the cosmetic half: a
    /// shot with nothing visibly connecting the muzzle to what it hit reads
    /// as invisible, not instant. This is purely decorative, spawned AFTER
    /// the real hit has already resolved, exactly the relationship CS:GO's
    /// own tracers have to its (also hitscan) shots.
    ///
    /// A PLAIN LineRenderer, NOT A MOVING PROJECTILE -- drawing the full
    /// line immediately and fading it out is what "zero travel time" looks
    /// like; animating a dot crawling from A to B would visually reintroduce
    /// the travel time this is explicitly trying not to have.
    /// </summary>
    public sealed class BulletTracer : MonoBehaviour
    {
        /// <summary>
        /// How wide the streak is ON SCREEN, in pixels, wherever it is -- EXPLICIT
        /// BUG REPORT: "bullet trails ... most of the time they arent showing up
        /// at all." The streak was a fixed 1.5cm wide, which at 4-8m from the
        /// camera is about ONE pixel, and it lived for 0.05s (three frames): a
        /// hairline that came and went before the eye could catch it. A
        /// pixel-constant width (computed per end, so it stays constant along the
        /// perspective line) keeps it readable at any range.
        ///
        /// 2.2 now, down from 3: a follow-up asked for it "a little thinner". Still well above the
        /// one-pixel hairline that could not be seen at all.
        /// </summary>
        public const float PixelWidth = 2.2f;

        /// <summary>Never wider than this in the world, however far the camera is.</summary>
        public const float MaxWorldWidth = 0.4f;

        /// <summary>
        /// How long the streak stays visible -- short enough to read as an
        /// instantaneous flash rather than a lingering laser beam, long
        /// enough that the eye actually catches it (about seven frames at 60fps;
        /// 0.05s was three).
        /// </summary>
        public const float LifetimeSeconds = 0.12f;

        public const float Width = 0.008f;   // world-space FLOOR only (was 0.015, which at close range was wider than PixelWidth asks for); see PixelWidth

        /// <summary>
        /// A pale, warm white: between pure white and the brass-yellow of a real tracer round. It was
        /// (1.0, 0.85, 0.35), which read as plainly yellow; a follow-up asked for it whiter but "not totally
        /// white". Roughly two thirds of the way from that yellow to white.
        /// </summary>
        private static readonly Color TracerColour = new Color(1.0f, 0.96f, 0.72f);

        private static Material _material;

        private LineRenderer _line;
        private float _elapsed;

        /// <summary>
        /// Draws one streak from <paramref name="start"/> (the gun's own
        /// muzzle, not the camera the hit-detection ray actually uses --
        /// firing the visual from the camera would look like the shot came
        /// out of the player's own face) to <paramref name="end"/> (the real
        /// hit point, or the ray's own max range on a miss, so a miss still
        /// shows where the shot actually went).
        /// </summary>
        public static BulletTracer Spawn(Vector3 start, Vector3 end)
        {
            return Spawn(start, end, null);
        }

        /// <summary>
        /// As above, but the START stays glued to <paramref name="follow"/> (the
        /// muzzle) for the streak's short life while the END stays fixed on the
        /// hit. The gun swings up to point at the target over the first couple of
        /// frames of a shot (the fire pose), and a start frozen at wherever the
        /// muzzle WAS on the click frame left the streak hanging in the air
        /// beside a gun that had already moved -- the "wrong direction" look.
        /// </summary>
        public static BulletTracer Spawn(Vector3 start, Vector3 end, Transform follow)
        {
            var go = new GameObject("BulletTracer");
            var tracer = go.AddComponent<BulletTracer>();
            tracer._follow = follow;
            tracer.Init(start, end);
            return tracer;
        }

        private Transform _follow;

        private void LateUpdate()
        {
            if (_follow != null && _line != null)
            {
                _line.SetPosition(0, _follow.position);
            }
        }

        private void Init(Vector3 start, Vector3 end)
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.material = EnsureMaterial();
            _line.textureMode = LineTextureMode.Stretch;
            _line.numCapVertices = 0;
            _line.numCornerVertices = 0;
            _line.useWorldSpace = true;
            _line.startWidth = WorldWidthAt(start);
            _line.endWidth = WorldWidthAt(end);
            _line.startColor = TracerColour;
            _line.endColor = TracerColour;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.positionCount = 2;
            _line.SetPosition(0, start);
            _line.SetPosition(1, end);
        }

        /// <summary>
        /// The world-space width that draws PixelWidth pixels at a point, given
        /// the main camera's field of view and resolution. Public and static so
        /// a test can check the maths without a renderer.
        /// </summary>
        public static float WorldWidthAt(Vector3 point)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return Mathf.Max(Width, 0.05f);
            }
            return WorldWidthFor(Vector3.Distance(camera.transform.position, point),
                camera.fieldOfView, camera.pixelHeight);
        }

        public static float WorldWidthFor(float distanceMetres, float fieldOfViewDegrees, float pixelHeight)
        {
            // A headless run can report a zero-height camera; assume a small window.
            float pixels = Mathf.Max(pixelHeight, 480.0f);
            float worldPerPixel = 2.0f * Mathf.Max(distanceMetres, 1.0f)
                * Mathf.Tan(fieldOfViewDegrees * 0.5f * Mathf.Deg2Rad) / pixels;
            return Mathf.Clamp(worldPerPixel * PixelWidth, Width, MaxWorldWidth);
        }

        /// <summary>
        /// One shared, additive-blended material for every tracer -- the
        /// same "Particles/Unlit + additive" combination FlamethrowerBuilder
        /// already established for a bright, glowing streak in this URP
        /// project (see its own doc comment on why an unlit particle
        /// shader needs a texture to avoid rendering as a hard-edged block --
        /// irrelevant here, a LineRenderer needs no texture at all to
        /// render as a solid streak).
        /// </summary>
        private static Material EnsureMaterial()
        {
            if (_material != null)
            {
                return _material;
            }
            _material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            _material.color = Color.white;
            _material.SetFloat("_Surface", 1.0f);
            _material.SetFloat("_Blend", 2.0f); // Additive
            return _material;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= LifetimeSeconds)
            {
                Destroy(gameObject);
                return;
            }

            // Fades out rather than popping off after a flat lifetime --
            // an instant cut at LifetimeSeconds is visible as a blink even
            // at this short a duration.
            float alpha = 1.0f - (_elapsed / LifetimeSeconds);
            Color faded = TracerColour;
            faded.a = alpha;
            _line.startColor = faded;
            _line.endColor = faded;
        }
    }
}
