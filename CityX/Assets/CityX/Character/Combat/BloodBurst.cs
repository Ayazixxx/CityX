using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A BURST OF BLOOD OUT OF A WOUND (owner, 2026-10-17: "make the blood effects better too"), drawn in code: no prefab, no texture file.
    /// Two sprays at once, out of the wound AWAY from the blow (a cut opens on the far side; the old burst sprayed fourteen slow dots back at
    /// the attacker): heavy drops that arc, streak as they fly and stop where they land, and a fine fast spray that is gone in a quarter of a
    /// second. A heavier blow throws more, further. What stays on the ground and the walls is <see cref="BloodSplats"/>.
    /// </summary>
    public static class BloodBurst
    {
        public const int Droplets = 26;
        public const int SprayDroplets = 12;
        public const float DropletLifeMin = 0.45f;
        public const float DropletLifeMax = 0.9f;
        public const float DropletSpeedMin = 2.4f;
        public const float DropletSpeedMax = 6.5f;
        public const float DropletSizeMin = 0.025f;
        public const float DropletSizeMax = 0.07f;
        public const float SpraySpeedMin = 6.0f;
        public const float SpraySpeedMax = 11.0f;
        public const float SprayLifeSeconds = 0.28f;
        public const float ConeDegrees = 36.0f;
        public const float SprayConeDegrees = 55.0f;

        /// <summary>How much of straight up is mixed into the spray's direction: blood goes up and out, not along the floor.</summary>
        public const float UpwardTilt = 0.3f;

        public static readonly Color Red = new Color(0.42f, 0.02f, 0.025f, 1.0f);

        private static GameObject _template;
        private static Material _material;
        private static Texture2D _dot;

        /// <summary>How many bursts have been spawned (for tests).</summary>
        public static int Spawned { get; private set; }

        /// <summary>How many splashes of another liquid have been spawned (for tests).</summary>
        public static int Splashed { get; private set; }

        /// <summary>
        /// One burst at <paramref name="point"/>, spraying along <paramref name="direction"/> (away from whoever struck; tilted up here).
        /// <paramref name="strength"/> scales the amount and the speed: 1 is a light cut, about 1.6 a heavy, 0.6 a bullet. Destroys itself.
        /// </summary>
        public static GameObject Spawn(Vector3 point, Vector3 direction, float strength)
        {
            Spawned++;
            strength = Mathf.Clamp(float.IsNaN(strength) ? 1.0f : strength, 0.3f, 3.0f);
            Vector3 flat = direction;
            if (flat.sqrMagnitude < 1e-6f)
            {
                flat = Vector3.up;
            }
            Vector3 aim = (flat.normalized + Vector3.up * UpwardTilt).normalized;
            GameObject burst = Object.Instantiate(Template(), point, Quaternion.LookRotation(aim));
            burst.name = "BloodBurst";
            burst.SetActive(true);
            ParticleSystem drops = burst.GetComponent<ParticleSystem>();
            ParticleSystem spray = burst.transform.GetChild(0).GetComponent<ParticleSystem>();
            Scale(drops, Droplets, DropletSpeedMin, DropletSpeedMax, strength);
            Scale(spray, SprayDroplets, SpraySpeedMin, SpraySpeedMax, strength);
            drops.Play(true);
            return burst;
        }

        private static void Scale(ParticleSystem system, int count, float speedMin, float speedMax, float strength)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, (short)Mathf.Max(1, Mathf.RoundToInt(count * strength))) });
            ParticleSystem.MainModule main = system.main;
            float pace = 0.8f + 0.2f * strength;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin * pace, speedMax * pace);
        }

        /// <summary>The burst every spawn is a copy of: built once, kept inactive and hidden.</summary>
        private static GameObject Template()
        {
            if (_template != null)
            {
                return _template;
            }
            _template = new GameObject("BloodBurstTemplate") { hideFlags = HideFlags.HideAndDontSave };
            _template.SetActive(false);

            ParticleSystem drops = _template.AddComponent<ParticleSystem>();
            Configure(drops, Droplets, DropletLifeMin, DropletLifeMax, DropletSpeedMin, DropletSpeedMax, DropletSizeMin, DropletSizeMax,
                ConeDegrees, gravity: 1.2f, stretch: 2.0f, collide: true);
            ParticleSystem.MainModule main = drops.main;
            main.duration = 1.0f;
            main.stopAction = ParticleSystemStopAction.Destroy;   // the burst takes itself (and the spray) away once every drop has landed

            var sprayObject = new GameObject("Spray");
            sprayObject.transform.SetParent(_template.transform, false);
            ParticleSystem spray = sprayObject.AddComponent<ParticleSystem>();
            Configure(spray, SprayDroplets, SprayLifeSeconds * 0.7f, SprayLifeSeconds, SpraySpeedMin, SpraySpeedMax, 0.012f, 0.03f,
                SprayConeDegrees, gravity: 0.6f, stretch: 3.0f, collide: false);
            return _template;
        }

        /// <summary>
        /// The same burst in another liquid's <paramref name="colour"/> (a drink splashing out of a broken bottle or a burst can: docs/PHIZZ_CAN.md). Not blood: not
        /// counted in <see cref="Spawned"/>.
        /// </summary>
        public static GameObject Splash(Vector3 point, Vector3 direction, float strength, Color colour)
        {
            Spawned--;
            Splashed++;
            GameObject burst = Spawn(point, direction, strength);
            burst.name = "Splash";
            foreach (ParticleSystem system in burst.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = system.main;
                main.startColor = colour;
                ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
                fade.color = new ParticleSystem.MinMaxGradient(Fading(colour));
            }
            return burst;
        }

        /// <summary>A colour darkening as it flies and gone in the last third of its life.</summary>
        private static Gradient Fading(Color colour)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(colour, 0.0f), new GradientColorKey(colour * 0.75f, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.65f), new GradientAlphaKey(0.0f, 1.0f) });
            return gradient;
        }

        private static void Configure(ParticleSystem system, int count, float lifeMin, float lifeMax, float speedMin, float speedMax,
            float sizeMin, float sizeMax, float coneDegrees, float gravity, float stretch, bool collide)
        {
            ParticleSystem.MainModule main = system.main;
            main.duration = lifeMax;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = Red;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count * 4;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0.0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, (short)count) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneDegrees;
            shape.radius = 0.02f;

            // Deep red, darkening as it flies, and gone in the last third of its life.
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(Fading(Red));

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.45f));

            if (collide)
            {
                // Drops stop where they land (the floor, a wall, the body) instead of falling through the street.
                ParticleSystem.CollisionModule collision = system.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D;
                collision.quality = ParticleSystemCollisionQuality.Medium;
                collision.bounce = 0.05f;
                collision.dampen = 0.85f;
                collision.lifetimeLoss = 0.5f;
                collision.radiusScale = 0.5f;
            }

            // Streaks: a drop in flight is drawn stretched along its own velocity, a round dot once it slows.
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = stretch;
            renderer.velocityScale = 0.08f;
            renderer.sharedMaterial = DropMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Unlit, alpha-blended, with a soft round dot: a square particle with no texture reads as a hard square.</summary>
        internal static Material DropMaterial()
        {
            if (_material != null)
            {
                return _material;
            }
            _material = BloodSplats.AlphaBlended("BloodDrop");
            _material.mainTexture = SoftDot();
            if (_material.HasProperty("_BaseMap"))
            {
                _material.SetTexture("_BaseMap", SoftDot());
            }
            return _material;
        }

        /// <summary>A 32-pixel dot, solid in the middle and soft at the rim.</summary>
        private static Texture2D SoftDot()
        {
            if (_dot != null)
            {
                return _dot;
            }
            const int size = 32;
            _dot = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "BloodDot", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2.0f;
                    float alpha = Mathf.Clamp01((1.0f - r) / 0.35f);
                    pixels[y * size + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
            }
            _dot.SetPixels(pixels);
            _dot.Apply(false, false);
            return _dot;
        }
    }
}
