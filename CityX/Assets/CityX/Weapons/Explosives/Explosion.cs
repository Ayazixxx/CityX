using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Weapons
{
    /// <summary>
    /// AN EXPLOSION (docs/GRENADE.md; owner, 2026-10-29: "work on explosions try making a grenade that looks really good research video game
    /// grenades and make it look AAA game level"). Built in layers, the way AAA games build one (Battlefront II's, taken apart frame by frame
    /// by Simon Schreibt; the Godot game's own weapons/explosion.gd had four of them):
    ///  1. the FLASH: a white-hot core for a few frames and a light that lights the street round it for a moment, falling off on a curve;
    ///  2. the FIREBALL: puffs of fire bursting out fast and slowing, white-hot to orange to deep red as they burn out, and the fire's own
    ///     tongues licking up out of it (FireScene's look);
    ///  3. the SHOCKWAVE: a bright ring racing out over the ground past the blast's reach, and dust rolling out behind it (DustBurst);
    ///  4. SPARKS: streaks flung out every way, falling and bouncing;
    ///  5. DEBRIS: dark chunks thrown up and out, tumbling, bouncing where they land;
    ///  6. SMOKE: a dark column billowing up, lit orange from inside while it is young, drifting away over seconds (FireScene's look);
    ///  7. a SCORCH left on the ground (FireMarks), the camera of a player near it shaking, and a crack, a thump and a rumble.
    /// The BLAST (<see cref="Blast"/>, on the machine whose grenade it was): everything in its reach that a wall does not shield takes its
    /// damage, most at the middle; bodies are thrown down and away, loose things pushed, gasoline lit; enough to kill outright bursts a body
    /// (docs/DISMEMBERMENT.md: a blast has no edge). Every other machine shows it (<see cref="Show"/>) and hurts no one.
    /// </summary>
    public sealed class Explosion : MonoBehaviour
    {
        /// <summary>How long the whole thing lasts before it is taken away, seconds (the smoke and the scorch live on in the fire's look).</summary>
        public const float LifeSeconds = 3.0f;

        /// <summary>The flash: the light's brightness at its peak (the game's lights are inverse-square: 2 at 8 m), its reach as a share of the blast's, how long it falls off.</summary>
        public const float FlashIntensity = 130.0f;

        public const float FlashReachShare = 3.2f;
        public const float FlashSeconds = 0.35f;

        /// <summary>How many puffs make the fireball, sparks, chunks of debris and puffs of smoke.</summary>
        public const int FireballPuffs = 36;

        public const int Sparks = 70;
        public const int Chunks = 28;

        /// <summary>The shockwave ring: how far it runs past the blast's reach (a real blast's visible edge outruns its lethal one), and how fast.</summary>
        public const float RingOvershoot = 1.6f;

        public const float RingSeconds = 0.22f;

        /// <summary>A player's camera shakes this hard at the middle, metres, falling to nothing at four times the blast's reach.</summary>
        public const float ShakeMetres = 0.45f;

        public const float ShakeSeconds = 0.6f;

        public static readonly Color FlashColour = new Color(1.0f, 0.85f, 0.6f);
        public static readonly Color RingColour = new Color(1.0f, 0.8f, 0.5f, 0.8f);

        private static readonly Collider[] Overlaps = new Collider[256];
        private static readonly HashSet<IDamageable> Struck = new HashSet<IDamageable>();
        private static readonly HashSet<Rigidbody> Pushed = new HashSet<Rigidbody>();
        private static Material _fireMaterial;
        private static Material _sparkMaterial;
        private static Material _chunkMaterial;
        private static Material _coreMaterial;
        private static Material _ringMaterial;

        private Light _flash;
        private Transform _ring;
        private MeshRenderer _ringRenderer;
        private MaterialPropertyBlock _ringTint;
        private float _radius;
        private float _age;

        /// <summary>Raised on every machine whenever an explosion goes off (where, how big): for tests, and for whatever else needs to hear it.</summary>
        public static event System.Action<Vector3, float> WentOff;

        private MaterialPropertyBlock RingTint => _ringTint ??= new MaterialPropertyBlock();

        /// <summary>
        /// Everything an explosion is drawn and heard with, made ahead (GrenadeThrower, as the first body comes in): made at the first blast, it
        /// stopped the game for about 70 ms just as the grenade went off.
        /// </summary>
        public static void Prewarm()
        {
            FireMaterial();
            SparkMaterial();
            CoreMaterial();
            ChunkMaterial();
            RingMaterial();
            _ = RockChunk.Mesh;
            _ = ExplosionSounds.Boom;
            _ = ExplosionSounds.Pin;
            FireMarks.PrewarmScorches();
            DustBurst.Prewarm();
        }

        /// <summary>
        /// The grenade's machine: the blast at <paramref name="at"/> (thrown by <paramref name="source"/>, who is credited with what it does)
        /// and the look. Returns how many it hurt.
        /// </summary>
        public static int Blast(Vector3 at, GameObject source)
        {
            int hurt = Hurt(at, source);
            Push(at);
            FireScene.Ignite(at, GrenadeRules.Radius * 0.35f, true, source);   // gasoline in it goes up (docs/FIRE.md)
            Show(at);
            return hurt;
        }

        /// <summary>The explosion as everyone sees and hears it, hurting no one.</summary>
        public static void Show(Vector3 at)
        {
            var go = new GameObject("Explosion");
            go.transform.position = at;
            go.AddComponent<Explosion>().Build(GrenadeRules.Radius);
            ThirdPersonCamera.ShakeNear(at, GrenadeRules.Radius * 4.0f, ShakeMetres, ShakeSeconds);
            WentOff?.Invoke(at, GrenadeRules.Radius);
        }

        /// <summary>Every body in reach not shielded by a wall: its share of the damage, then thrown down and away if it lives.</summary>
        private static int Hurt(Vector3 at, GameObject source)
        {
            Struck.Clear();
            int count = Physics.OverlapSphereNonAlloc(at, GrenadeRules.Radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            int hurt = 0;
            for (int i = 0; i < count; i++)
            {
                var target = Overlaps[i] != null ? Overlaps[i].GetComponentInParent<IDamageable>() : null;
                if (target == null || !(target is Component body) || Struck.Contains(target)
                    || (!target.IsAlive && !Dismemberer.TakesCorpseHits(target)))
                {
                    continue;
                }
                Struck.Add(target);
                Vector3 chest = NearestOfTheBody(body.transform, at);
                if (Shielded(at, chest, body.transform))
                {
                    continue;
                }
                Vector3 away = chest - at;
                float share = GrenadeRules.DamageShare(away.magnitude);
                if (share <= 0.0f)
                {
                    continue;
                }
                HitResponse response = DamageContext.Strike(target, GrenadeRules.Damage * share, at, 0.6f, DamageKind.Physical, 0.0f, chest, source);
                hurt++;
                Vector3 flat = new Vector3(away.x, 0.0f, away.z);
                Vector3 outward = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
                Vector3 thrown = outward * GrenadeRules.ThrowAwaySpeed * share + Vector3.up * GrenadeRules.ThrowUpSpeed * share;
                if (response != HitResponse.Landed)
                {
                    continue;   // dashed through it, or behind a barrier, a guard or a shield: a blast that never reached the body does not throw it
                }
                if (target.IsAlive)
                {
                    Throws.Limp(target, thrown);
                }
            }
            return hurt;
        }

        /// <summary>
        /// Where a blast reaches a body: its chest, for a body of a person's size. A grown one (a Giant, a player under Grow) is reached
        /// at the height of the blast, from chest height on a person up to its own chest: measured to a chest twelve metres up, a grenade
        /// at a giant's feet was out of reach of it and did nothing.
        /// </summary>
        private static Vector3 NearestOfTheBody(Transform body, Vector3 at)
        {
            float scale = body.lossyScale.y;
            float lowest = ChestHeight * Mathf.Min(1.0f, scale), highest = ChestHeight * scale;
            return body.position + Vector3.up * Mathf.Clamp(at.y - body.position.y, lowest, highest);
        }

        /// <summary>A person's chest above their feet, metres (the 2.6 m body).</summary>
        private const float ChestHeight = 1.2f;

        /// <summary>A wall (anything solid that is not the body itself nor another body) between the blast and the body's chest.</summary>
        private static bool Shielded(Vector3 at, Vector3 chest, Transform body)
        {
            Vector3 from = at + Vector3.up * 0.3f;
            Vector3 to = chest - from;
            float distance = to.magnitude;
            if (distance < 0.3f)
            {
                return false;
            }
            return BloodSplats.Surface(from, to / distance, distance - 0.2f, body, out RaycastHit wall) && !wall.collider.transform.IsChildOf(body);
        }

        /// <summary>Loose things in reach pushed away and up: dropped weapons, bottles, a corpse's limbs (a living body is thrown by its own knockdown).</summary>
        private static void Push(Vector3 at)
        {
            Pushed.Clear();
            float reach = GrenadeRules.Radius * GrenadeRules.PushReachShare;
            int count = Physics.OverlapSphereNonAlloc(at, reach, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody rigidbody = Overlaps[i] != null ? Overlaps[i].attachedRigidbody : null;
                if (rigidbody == null || rigidbody.isKinematic || !Pushed.Add(rigidbody))
                {
                    continue;
                }
                var owner = rigidbody.GetComponentInParent<IDamageable>();
                if (owner != null && owner.IsAlive)
                {
                    continue;
                }
                rigidbody.AddExplosionForce(GrenadeRules.ThrowAwaySpeed * rigidbody.mass, at, reach, 0.6f, ForceMode.Impulse);
            }
        }


        // --- the look ---

        private void Build(float radius)
        {
            _radius = radius;
            Vector3 at = transform.position;
            Flash(radius);
            Fireball(at);
            Sparkle(at);
            Debris(at);
            Ring();
            DustBurst.Roll(at, radius * 0.9f, 0.45f, transform);
            FireScene.Explode(at, radius);   // the fire's tongues licking up, and the smoke column lit from inside
            FireMarks.ScorchAt(at, radius * 0.6f);
            if (Application.isPlaying)
            {
                var boom = gameObject.AddComponent<AudioSource>();
                boom.clip = ExplosionSounds.Boom;
                boom.spatialBlend = 1.0f;
                boom.dopplerLevel = 0.0f;
                boom.minDistance = radius;
                boom.maxDistance = radius * 40.0f;
                boom.Play();
            }
            Update();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float flash = Mathf.Clamp01(_age / FlashSeconds);
            _flash.intensity = FlashIntensity * Mathf.Pow(1.0f - flash, 3.0f);
            _flash.enabled = flash < 1.0f;
            float ring = Mathf.Clamp01(_age / RingSeconds);
            float across = _radius * 2.0f * RingOvershoot * (1.0f - (1.0f - ring) * (1.0f - ring));
            _ring.localScale = new Vector3(across, 1.0f, across);
            _ringRenderer.GetPropertyBlock(RingTint);
            RingTint.SetColor("_BaseColor", new Color(RingColour.r, RingColour.g, RingColour.b, RingColour.a * (1.0f - ring)));
            _ringRenderer.SetPropertyBlock(RingTint);
            _ringRenderer.enabled = ring < 1.0f;
            if (_age >= LifeSeconds)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>The light, and the white-hot core: one big bright puff for a few frames.</summary>
        private void Flash(float radius)
        {
            var lightObject = new GameObject("Flash");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.up * 1.0f;
            _flash = lightObject.AddComponent<Light>();
            _flash.type = LightType.Point;
            _flash.color = FlashColour;
            _flash.range = radius * FlashReachShare;
            _flash.shadows = LightShadows.None;
            ParticleSystem core = System("Core", CoreMaterial(), ParticleSystemRenderMode.Billboard, 2);
            Fade(core, new Color(1.0f, 1.0f, 0.95f), new Color(1.0f, 0.8f, 0.4f), 0.0f);
            Grow(core, 0.6f, 1.3f);
            core.Emit(new ParticleSystem.EmitParams
            {
                position = transform.position + Vector3.up * 0.8f, velocity = Vector3.zero, startSize = radius * 0.7f, startLifetime = 0.12f,
                applyShapeToPosition = false,
            }, 1);
        }

        /// <summary>Fire puffs bursting out fast and slowing, burning from white to orange to deep red as they thin away.</summary>
        private void Fireball(Vector3 at)
        {
            ParticleSystem fire = System("Fireball", FireMaterial(), ParticleSystemRenderMode.Billboard, FireballPuffs + 4);
            Drag(fire, 0.06f);
            Grow(fire, 0.55f, 1.6f);
            // Blended, not added: a fireball by day is a solid billowing thing, not a glow the sky washes out. Bright at its heart (past 1, for
            // the bloom), burning down through orange quickly to the dark of smoke as each puff thins: lingering in deep red, the whole ball
            // read as one flat red blob.
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.0f, 0.92f, 0.62f), 0.0f), new GradientColorKey(new Color(1.0f, 0.58f, 0.15f), 0.18f),
                    new GradientColorKey(new Color(0.7f, 0.25f, 0.06f), 0.38f), new GradientColorKey(new Color(0.2f, 0.12f, 0.09f), 0.6f),
                    new GradientColorKey(new Color(0.1f, 0.09f, 0.08f), 1.0f),
                },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.95f, 0.45f), new GradientAlphaKey(0.55f, 0.75f), new GradientAlphaKey(0.0f, 1.0f) });
            ParticleSystem.ColorOverLifetimeModule colour = fire.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
            ParticleSystem.RotationOverLifetimeModule spin = fire.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            var renderer = fire.GetComponent<ParticleSystemRenderer>();
            renderer.sortMode = ParticleSystemSortMode.Distance;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < FireballPuffs; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.8f + 0.2f;
                direction.Normalize();
                emit.position = at + Vector3.up * 0.6f + direction * Random.Range(0.0f, 0.8f);
                emit.velocity = direction * Random.Range(5.0f, 11.0f);
                emit.startSize = Random.Range(2.4f, 4.2f);
                emit.startLifetime = Random.Range(0.5f, 1.2f);   // spread, so some puffs are smoke while others still burn
                emit.startColor = Color.Lerp(Color.white, new Color(0.85f, 0.8f, 0.75f), Random.value);
                emit.rotation = Random.Range(0.0f, 360.0f);
                fire.Emit(emit, 1);
            }
        }

        /// <summary>Streaks flung out every way, falling and bouncing, yellow-hot to red.</summary>
        private void Sparkle(Vector3 at)
        {
            ParticleSystem sparks = System("Sparks", SparkMaterial(), ParticleSystemRenderMode.Stretch, Sparks + 8);
            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = 0.02f;
            renderer.lengthScale = 1.0f;
            ParticleSystem.MainModule main = sparks.main;
            main.gravityModifier = 1.0f;
            Fade(sparks, new Color(1.0f, 0.86f, 0.45f), new Color(1.0f, 0.35f, 0.05f), 0.0f);
            Bounce(sparks, 0.3f, 0.4f);
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < Sparks; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.8f + 0.1f;
                emit.position = at + Vector3.up * 0.4f;
                emit.velocity = direction.normalized * Random.Range(12.0f, 28.0f);
                emit.startSize = Random.Range(0.05f, 0.1f);
                emit.startLifetime = Random.Range(0.5f, 1.3f);
                sparks.Emit(emit, 1);
            }
        }

        /// <summary>Dark chunks thrown up and out, tumbling, bouncing where they land.</summary>
        private void Debris(Vector3 at)
        {
            ParticleSystem chunks = System("Debris", ChunkMaterial(), ParticleSystemRenderMode.Mesh, Chunks + 4);
            var renderer = chunks.GetComponent<ParticleSystemRenderer>();
            renderer.mesh = RockChunk.Mesh;
            ParticleSystem.MainModule main = chunks.main;
            main.gravityModifier = 1.2f;
            main.startSize3D = true;
            main.startRotation3D = true;
            ParticleSystem.RotationOverLifetimeModule spin = chunks.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-7.0f, 7.0f);
            spin.y = new ParticleSystem.MinMaxCurve(-7.0f, 7.0f);
            spin.z = new ParticleSystem.MinMaxCurve(-7.0f, 7.0f);
            Bounce(chunks, 0.25f, 0.4f);
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < Chunks; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.6f + 0.4f;
                emit.position = at + Vector3.up * 0.3f;
                emit.velocity = direction.normalized * Random.Range(8.0f, 16.0f);
                float size = Random.Range(0.08f, 0.25f);
                emit.startSize3D = new Vector3(size, size * Random.Range(0.5f, 1.0f), size * Random.Range(0.6f, 1.2f));
                emit.rotation3D = new Vector3(Random.Range(0.0f, 360.0f), Random.Range(0.0f, 360.0f), Random.Range(0.0f, 360.0f));
                emit.startLifetime = Random.Range(1.6f, 2.6f);
                emit.startColor = Color.Lerp(new Color(0.16f, 0.14f, 0.12f), new Color(0.32f, 0.29f, 0.25f), Random.value);
                chunks.Emit(emit, 1);
            }
        }

        /// <summary>The shockwave: a bright ring lying on the ground, racing out past the blast's reach.</summary>
        private void Ring()
        {
            _ring = new GameObject("Shockwave").transform;
            _ring.SetParent(transform, false);
            _ring.localPosition = Vector3.up * 0.15f;
            _ring.gameObject.AddComponent<MeshFilter>().sharedMesh = GlowShapes.FlatQuad;
            _ringRenderer = _ring.gameObject.AddComponent<MeshRenderer>();
            _ringRenderer.sharedMaterial = RingMaterial();
            _ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _ringRenderer.receiveShadows = false;
        }

        // --- particle building blocks ---

        private ParticleSystem System(string name, Material material, ParticleSystemRenderMode mode, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0.0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static void Fade(ParticleSystem particles, Color from, Color to, float endAlpha)
        {
            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(from, 0.0f), new GradientColorKey(to, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.5f), new GradientAlphaKey(endAlpha, 1.0f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void Grow(ParticleSystem particles, float from, float to)
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.EaseInOut(0.0f, from, 1.0f, to));
        }

        /// <summary>Slows to <paramref name="share"/> of its speed over its life, fast at first: a burst that spends itself.</summary>
        private static void Drag(ParticleSystem particles, float share)
        {
            ParticleSystem.VelocityOverLifetimeModule drag = particles.velocityOverLifetime;
            drag.enabled = true;
            drag.speedModifier = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(new Keyframe(0.0f, 1.0f, -4.0f, -4.0f), new Keyframe(1.0f, share)));
        }

        private static void Bounce(ParticleSystem particles, float bounce, float dampen)
        {
            ParticleSystem.CollisionModule collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.bounce = bounce;
            collision.dampen = dampen;
            collision.lifetimeLoss = 0.15f;
        }

        // --- materials and meshes, made once ---

        /// <summary>
        /// A fire puff: a full round cloud whose edge the fire's noise eats into (billowing, not a soft disc), blended so it stands solid by day,
        /// bright past 1 so its heart blooms.
        /// </summary>
        private static Material FireMaterial()
        {
            if (_fireMaterial == null)
            {
                Texture2D puff = GlowShapes.Paint("ExplosionPuff", 128, TextureWrapMode.Clamp, (u, v) =>
                {
                    float x = u * 2.0f - 1.0f, y = v * 2.0f - 1.0f;
                    float r = Mathf.Sqrt(x * x + y * y);
                    Color noise = FireSprites.Noise().GetPixelBilinear(u * 0.6f + 0.13f, v * 0.6f + 0.41f);
                    float edge = 0.62f + 0.38f * noise.g - 0.12f * noise.r;   // where the cloud ends, eaten by the noise
                    return Mathf.Clamp01((edge - r) / 0.18f) * (0.8f + 0.2f * noise.r);
                });
                _fireMaterial = FireSprites.Particles("ExplosionFire", puff, ParticleBlend.Alpha, 2, 1.7f);
            }
            return _fireMaterial;
        }

        private static Material SparkMaterial() => _sparkMaterial != null
            ? _sparkMaterial
            : (_sparkMaterial = FireSprites.Particles("ExplosionSparks", FireSprites.Dot(), ParticleBlend.Additive, 3, 2.2f));

        private static Material CoreMaterial() => _coreMaterial != null
            ? _coreMaterial
            : (_coreMaterial = FireSprites.Particles("ExplosionCore", FireSprites.Dot(), ParticleBlend.Additive, 4, 3.0f));

        /// <summary>The debris: dark, matte, a particle's own colour on it (each chunk a little lighter or darker).</summary>
        private static Material ChunkMaterial() => _chunkMaterial != null
            ? _chunkMaterial
            : (_chunkMaterial = FireSprites.Particles("ExplosionDebris", Texture2D.whiteTexture, ParticleBlend.Alpha, 0));

        /// <summary>A bright band round a disc, added; nothing at the square's edge.</summary>
        private static Material RingMaterial()
        {
            if (_ringMaterial == null)
            {
                Texture2D ring = GlowShapes.Paint("ExplosionRing", 128, TextureWrapMode.Clamp, (u, v) =>
                {
                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2.0f;
                    float band = (r - 0.82f) / 0.06f;
                    return Mathf.Exp(-band * band);
                });
                _ringMaterial = GlowShapes.Painted("ExplosionRing", RingColour, true, ring);
            }
            return _ringMaterial;
        }

    }
}
