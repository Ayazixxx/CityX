using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A brief visible streak from muzzle to impact -- Requested: "i want
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
    // LAST: the start is read from the muzzle after the arms are solved and the pistol's kick is laid on (order 100).
    [DefaultExecutionOrder(200)]
    public sealed class BulletTracer : MonoBehaviour
    {
        /// <summary>
        /// How long the streak's start stays glued to the muzzle. Long enough to cover the couple of frames the
        /// gun takes to swing up for a hip-fired shot; after that the streak is a fixed line in the world, the
        /// way a real tracer is -- glued for its whole life it drew every wobble of a moving or recoiling gun
        /// as a wobble in the beam ("the bullet trails are spazzy").
        /// </summary>
        public const float FollowSeconds = 0.05f;

        /// <summary>
        /// How wide the streak is ON SCREEN, in pixels, wherever it is --
        /// Bug report: "bullet trails ... most of the time they arent showing up
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
        /// The streak is drawn this many times as wide as its core (<see cref="PixelWidth"/>): the rest is the soft gold glow round it, fading to
        /// nothing at its edges (owner, 2026-10-25: a flat colour "feel kind of roblox like"; Counter-Strike's tracers are a hot core in a glow).
        /// </summary>
        public const float GlowWidthRatio = 5.0f;

        /// <summary>How much each frame's brightness may waver, as a share: a live flame, not a lit tube.</summary>
        public const float Flicker = 0.18f;

        private static readonly Color CoreColour = new Color(1.0f, 0.97f, 0.86f);

        private static readonly Color GlowColour = new Color(1.0f, 0.68f, 0.22f);

        private static Texture2D _texture;

        /// <summary>
        /// How long the streak stays visible -- short enough to read as an
        /// instantaneous flash rather than a lingering laser beam, long
        /// enough that the eye actually catches it (about seven frames at 60fps;
        /// 0.05s was three).
        /// </summary>
        public const float LifetimeSeconds = 0.12f;

        public const float Width = 0.008f;   // world-space FLOOR only (was 0.015, which at close range was wider than PixelWidth asks for); see PixelWidth

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
                if (_elapsed < FollowSeconds)
                {
                    _line.SetPosition(0, _follow.position);
                }
                else
                {
                    _follow = null;   // let go: from here on the beam stays where it is
                }
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
            _line.startWidth = WorldWidthAt(start) * GlowWidthRatio;
            _line.endWidth = WorldWidthAt(end) * GlowWidthRatio;
            _line.startColor = Color.white;   // the texture carries the colours
            _line.endColor = Color.white;
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
                // No game camera (a capture's own): as thin as the floor, not 5 cm, which drew a bar as thick as the gun (owner, 2026-10-25).
                return Width;
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

        /// <summary>Makes the shared material now (a gun being drawn), so the first shot does not pay for it.</summary>
        public static void Warm()
        {
            EnsureMaterial();
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
            // Truly additive: given only _Surface and _Blend it drew as a solid, and the cel look's black lines traced it into a black streak.
            CityX.Character.SeeThroughMaterial.Apply(_material, additive: true);
            _material.SetTexture("_BaseMap", StreakTexture());
            _material.mainTexture = StreakTexture();
            return _material;
        }

        /// <summary>
        /// The streak's look, made once in code (no image file): across it a near-white core in a gold glow fading to nothing at the edges, with
        /// three faint wisps winding through the glow; along it (muzzle to hit) brightening toward the front, the bullet's end. Additive, so its
        /// dark is see-through.
        /// </summary>
        public static Texture2D StreakTexture()
        {
            if (_texture != null)
            {
                return _texture;
            }
            const int along = 128, across = 32;
            _texture = new Texture2D(along, across, TextureFormat.RGBA32, false)
            {
                name = "BulletTracerStreak", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[along * across];
            for (int x = 0; x < along; x++)
            {
                float u = (x + 0.5f) / along;
                float front = 0.7f + 0.3f * u;                                                          // brighter toward the bullet's end
                float ends = Mathf.Clamp01(u / 0.03f) * Mathf.Clamp01((1.0f - u) / 0.02f);              // soft tips, no square cut
                for (int y = 0; y < across; y++)
                {
                    float v = ((y + 0.5f) / across) * 2.0f - 1.0f;                                       // -1 .. 1 across
                    float core = Mathf.Exp(-(v * v) / (0.13f * 0.13f));                                  // a fifth of the width: the old streak
                    float glow = 0.55f * Mathf.Exp(-(v * v) / (0.45f * 0.45f));
                    float wisps = 0.0f;
                    for (int k = 0; k < 3; k++)
                    {
                        float path = 0.45f * Mathf.Sin(u * (5.0f + 3.0f * k) + k * 2.1f);
                        float d = (v - path) / 0.06f;
                        wisps += 0.35f * Mathf.Exp(-d * d) * u;
                    }
                    float glowShare = glow + wisps;
                    float strength = Mathf.Clamp01((core + glowShare) * front * ends);
                    Color colour = Color.Lerp(GlowColour, CoreColour, core / Mathf.Max(core + glowShare, 1e-4f));
                    pixels[y * along + x] = new Color(colour.r, colour.g, colour.b, strength);
                }
            }
            _texture.SetPixels32(pixels);
            _texture.Apply(false, true);
            return _texture;
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
            float alpha = (1.0f - (_elapsed / LifetimeSeconds)) * (1.0f - Flicker * Random.value);
            Color faded = Color.white;
            faded.a = alpha;
            _line.startColor = faded;
            _line.endColor = faded;
        }
    }
}
