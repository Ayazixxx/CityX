using System.IO;
using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the flamethrower prefab -- a small procedural tank-and-nozzle
    /// prop (matching JetpackBuilder/GrapplingHookBuilder's own reasoning:
    /// no glTF importer exists in this project for a hand-authored model,
    /// so nothing here waits on one) plus a three-layer stream of fire.
    ///
    /// THE FLAME IS THE WHOLE POINT of the explicit ask ("just try to get
    /// the flames looking good"), so unlike Jetpack's own "small, cheap
    /// cone of orange particles" this builds three separate ParticleSystems
    /// -- a hot pale-yellow core jet, a wider orange-red outer flame, and a
    /// sparse trailing smoke layer -- rather than one flat cone, following
    /// FireBurstBuilder's own richer colour-gradient/size-over-lifetime
    /// template (built for a one-shot burst) adapted into a continuous
    /// loop instead.
    ///
    /// CORE AND SMOKE ARE CHILDREN OF THE OUTER FLAME'S OWN GameObject, not
    /// separate top-level systems Flamethrower.cs has to track and
    /// Play()/Stop() individually -- ParticleSystem.Play()/Stop() both
    /// default their own withChildren argument to true, so starting/
    /// stopping only the outer flame (Flamethrower.FlameEffect) already
    /// cascades to both automatically.
    /// </summary>
    public static class FlamethrowerBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/Flamethrower.prefab";
        public const string StaffFlamePrefabPath = "Assets/CityX/Weapons/StaffFlame.prefab";
        private const string FlameMaterialPath = "Assets/CityX/Weapons/FlamethrowerFlame.mat";
        private const string SmokeMaterialPath = "Assets/CityX/Weapons/FlamethrowerSmoke.mat";
        private const string ParticleTexturePath = "Assets/CityX/Weapons/Textures/FlamethrowerParticle.png";

        private static readonly Color MetalColour = new Color(0.10f, 0.10f, 0.11f);

        private const float BodyLength = 0.34f;
        private const float BodyRadius = 0.045f;
        private const float NozzleLength = 0.10f;
        private const float NozzleRadius = 0.022f;
        private const float GripHeight = 0.14f;

        private static readonly Color CoreColour = new Color(1.0f, 0.95f, 0.65f);
        private static readonly Color OuterColour = new Color(1.0f, 0.5f, 0.1f);
        private static readonly Color SmokeColour = new Color(0.12f, 0.12f, 0.12f, 0.45f);

        [MenuItem("CityX/Rebuild Flamethrower")]
        public static GameObject Build()
        {
            var root = new GameObject("Flamethrower");
            try
            {
                BuildProp(root.transform);

                Transform nozzle = new GameObject("Nozzle").transform;
                nozzle.SetParent(root.transform, false);
                nozzle.localPosition = new Vector3(0.0f, 0.0f, BodyLength + NozzleLength);
                nozzle.localRotation = Quaternion.identity;

                Texture2D particleTexture = BuildParticleTexture();
                Material flameMaterial = BuildFlameMaterial(particleTexture);
                Material smokeMaterial = BuildSmokeMaterial(particleTexture);

                ParticleSystem outer = BuildOuterFlame(nozzle, flameMaterial);
                BuildCoreFlame(outer.transform, flameMaterial);
                BuildSmoke(outer.transform, smokeMaterial);
                Light flameLight = BuildFlameLight(nozzle);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = Flamethrower.InteractRange;

                var flamethrower = root.AddComponent<Flamethrower>();
                flamethrower.FlameEffect = outer;
                flamethrower.FlameLight = flameLight;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: flamethrower written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The flame staff's fire (2026-10-28, docs/STAFFS.md): the same three-layer stream and flickering light as the flamethrower's, on a prefab
        /// of its own (FlameStreamLook) that FlameStreamCaster puts at the staff's head, its +Z the way the fire goes.
        /// </summary>
        public static GameObject BuildStaffFlame()
        {
            var root = new GameObject("StaffFlame");
            try
            {
                Texture2D particleTexture = BuildParticleTexture();
                Material flameMaterial = BuildFlameMaterial(particleTexture);
                Material smokeMaterial = BuildSmokeMaterial(particleTexture);
                ParticleSystem outer = BuildOuterFlame(root.transform, flameMaterial);
                BuildCoreFlame(outer.transform, flameMaterial);
                BuildSmoke(outer.transform, smokeMaterial);
                StretchForStaff(outer, StaffFlameSpeed, StaffFlameLifetime, StaffFlameSize);
                StretchForStaff(outer.transform.Find("CoreFlame").GetComponent<ParticleSystem>(), StaffFlameSpeed, 1.0f, StaffFlameSize);
                StretchForStaff(outer.transform.Find("Smoke").GetComponent<ParticleSystem>(), 2.0f, 1.0f, 1.0f);
                var look = root.AddComponent<FlameStreamLook>();
                look.Flame = outer;
                look.Light = BuildFlameLight(root.transform);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, StaffFlamePrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: the staff flame written to {StaffFlamePrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The flamethrower's particles were tuned for its own short hip-fired plume (a couple of metres); the staff's fire reaches
        /// FlameStreamRules.Range, so its particles fly this many times faster (and the flame's live a little longer and grow bigger), with
        /// more of them so the longer stream is as full. Rendered 2026-10-28: at the flamethrower's own numbers the staff's fire was a pale puff.
        /// </summary>
        private const float StaffFlameSpeed = 3.5f;
        private const float StaffFlameLifetime = 1.2f;
        private const float StaffFlameSize = 1.6f;

        private static void StretchForStaff(ParticleSystem particles, float speed, float lifetime, float size)
        {
            ParticleSystem.MainModule main = particles.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(main.startSpeed.constantMin * speed, main.startSpeed.constantMax * speed);
            main.startLifetime = new ParticleSystem.MinMaxCurve(main.startLifetime.constantMin * lifetime, main.startLifetime.constantMax * lifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(main.startSize.constantMin * size, main.startSize.constantMax * size);
            main.maxParticles *= 2;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = emission.rateOverTime.constant * 2.0f;
        }

        /// <summary>
        /// A tank body, a front nozzle taper and a grip -- just enough
        /// geometry to read as a hand-held fire weapon, since the flame
        /// itself is what this build is actually about. Scaled Unity
        /// primitives throughout, not hand-copied meshes -- JetpackBuilder's
        /// own doc comment already found a hand-copied CylinderMesh
        /// produces zero-bounds renderers in this project.
        /// </summary>
        private static void BuildProp(Transform parent)
        {
            Material metal = CharacterSkin.Ensure("FlamethrowerMetal", MetalColour);
            metal.SetFloat("_Metallic", 0.7f);
            metal.SetFloat("_Smoothness", 0.3f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0.0f, 0.0f, BodyLength * 0.5f);
            body.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
            // Unity's primitive cylinder is 2 units tall with radius 1.
            body.transform.localScale = new Vector3(BodyRadius * 2.0f, BodyLength * 0.5f, BodyRadius * 2.0f);
            body.GetComponent<MeshRenderer>().sharedMaterial = metal;

            GameObject muzzle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            muzzle.name = "NozzleTube";
            Object.DestroyImmediate(muzzle.GetComponent<Collider>());
            muzzle.transform.SetParent(parent, false);
            muzzle.transform.localPosition = new Vector3(0.0f, 0.0f, BodyLength + NozzleLength * 0.5f);
            muzzle.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
            muzzle.transform.localScale = new Vector3(NozzleRadius * 2.0f, NozzleLength * 0.5f, NozzleRadius * 2.0f);
            muzzle.GetComponent<MeshRenderer>().sharedMaterial = metal;

            GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grip.name = "Grip";
            Object.DestroyImmediate(grip.GetComponent<Collider>());
            grip.transform.SetParent(parent, false);
            grip.transform.localPosition = new Vector3(0.0f, -GripHeight * 0.5f, BodyLength * 0.25f);
            grip.transform.localScale = new Vector3(0.03f, GripHeight, 0.035f);
            grip.GetComponent<MeshRenderer>().sharedMaterial = metal;
        }

        /// <summary>
        /// Shared by both flame layers -- coloured entirely by each
        /// system's own colorOverLifetime gradient rather than the material
        /// itself (matching FireBurstBuilder's own plain-white-material
        /// convention), so one additive material serves both. ADDITIVE, not
        /// plain alpha -- overlapping hot particles should brighten each
        /// other the way real fire does, which is what makes a dense
        /// stream read as glowing rather than as a pile of flat orange
        /// billboards.
        ///
        /// A REAL TEXTURE, UNLIKE FireBurstBuilder's OWN MATERIAL -- confirmed
        /// necessary by rendering without one first: Unity's Particles/Unlit
        /// shader with no texture assigned falls back to a solid opaque
        /// white square, not a soft round sprite, so every particle rendered
        /// as a hard-edged flat rectangle instead of a flame-like blob. A
        /// one-shot burst's own particles are small and short-lived enough
        /// that this apparently never got noticed there; a sustained,
        /// closely-watched stream can't get away with it.
        /// </summary>
        internal static Material BuildFlameMaterial(Texture2D particleTexture)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.color = Color.white;
            material.SetTexture("_BaseMap", particleTexture);
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 2.0f); // Additive

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(FlameMaterialPath);
            }
            AssetDatabase.CreateAsset(material, FlameMaterialPath);
            return material;
        }

        /// <summary>
        /// Plain alpha transparency, deliberately NOT additive -- smoke
        /// darkens and occludes in reality; blended additively it would
        /// brighten the flame behind it instead, reading as more fire
        /// rather than as smoke. Same soft-sprite texture as the flame
        /// material, for the same reason -- see BuildFlameMaterial's own
        /// doc comment.
        /// </summary>
        private static Material BuildSmokeMaterial(Texture2D particleTexture)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.color = Color.white;
            material.SetTexture("_BaseMap", particleTexture);
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 0.0f); // Alpha

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(SmokeMaterialPath);
            }
            AssetDatabase.CreateAsset(material, SmokeMaterialPath);
            return material;
        }

        /// <summary>
        /// A soft round sprite, generated rather than sourced -- a radial
        /// alpha falloff from opaque white at the centre to fully
        /// transparent at the edge, which is all a Shuriken particle
        /// actually needs to stop reading as a flat square. Written to a
        /// real imported PNG asset rather than kept as an in-memory
        /// Texture2D -- a loose, unimported texture does not survive
        /// AssetDatabase.CreateAsset on the material that references it any
        /// better than a loose material survives SaveAsPrefabAsset (see
        /// this file's own class comment on why FireBurstBuilder's plain-
        /// white-material choice worked without one).
        /// </summary>
        internal static Texture2D BuildParticleTexture()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxDist;
                    float alpha = Mathf.Clamp01(1.0f - dist);
                    alpha = Mathf.Pow(alpha, 1.6f); // softer than linear -- a bright core, fading gently to nothing
                    pixels[y * size + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            string directory = Path.GetDirectoryName(ParticleTexturePath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, directory));
            }
            File.WriteAllBytes(
                Path.Combine(Directory.GetParent(Application.dataPath).FullName, ParticleTexturePath), png);
            AssetDatabase.ImportAsset(ParticleTexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(ParticleTexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleTexturePath);
        }

        /// <summary>
        /// The main visible flame body -- wider, slower and longer-lived
        /// than the core, billowing outward as it travels (SizeOverLifetime
        /// grows rather than shrinks, unlike FireBurst's own dissipating
        /// embers) the way a real flame jet expands mixing with the air
        /// around it.
        /// </summary>
        internal static ParticleSystem BuildOuterFlame(Transform parent, Material material)
        {
            var go = new GameObject("OuterFlame");
            go.transform.SetParent(parent, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.0f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            main.startColor = OuterColour;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 400;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 260.0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 13.0f;
            shape.radius = 0.02f;

            ApplyFireGradient(particles, OuterColour, new Color(0.85f, 0.2f, 0.05f), new Color(0.25f, 0.05f, 0.02f));

            ParticleSystem.SizeOverLifetimeModule grow = particles.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.EaseInOut(0.0f, 0.7f, 1.0f, 1.8f));

            ApplyFlicker(particles, strength: 0.4f, frequency: 0.6f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.02f;
            renderer.lengthScale = 0.4f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particles;
        }

        /// <summary>
        /// The hot inner jet -- smaller, faster and shorter-lived than the
        /// outer flame, so it reads as a bright core the outer layer
        /// billows around rather than two identical cones stacked on each
        /// other.
        /// </summary>
        internal static void BuildCoreFlame(Transform parent, Material material)
        {
            var go = new GameObject("CoreFlame");
            go.transform.SetParent(parent, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            main.startColor = CoreColour;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 300;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 320.0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7.0f;
            shape.radius = 0.012f;

            ApplyFireGradient(particles, CoreColour, new Color(1.0f, 0.6f, 0.15f), new Color(0.6f, 0.15f, 0.02f));

            ParticleSystem.SizeOverLifetimeModule grow = particles.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.EaseInOut(0.0f, 0.6f, 1.0f, 1.4f));

            ApplyFlicker(particles, strength: 0.25f, frequency: 0.8f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.02f;
            renderer.lengthScale = 0.5f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Sparse trailing smoke -- WORLD SPACE, unlike both flame layers'
        /// own Local: a hot jet should stay rigidly pointed wherever the
        /// nozzle is currently aimed, but smoke that already left the flame
        /// should hang and drift in the air rather than snapping to follow
        /// every turn of the weapon.
        /// </summary>
        private static void BuildSmoke(Transform parent, Material material)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startColor = SmokeColour;
            main.gravityModifier = -0.04f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 18.0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20.0f;
            shape.radius = 0.02f;

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(SmokeColour, 0.0f),
                    new GradientColorKey(SmokeColour, 1.0f),
                },
                new[]
                {
                    new GradientAlphaKey(0.5f, 0.0f),
                    new GradientAlphaKey(0.3f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f),
                });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule grow = particles.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.EaseInOut(0.0f, 1.0f, 1.0f, 2.2f));

            ApplyFlicker(particles, strength: 0.3f, frequency: 0.3f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Core -> edge -> ember gradient shared by both flame layers'
        /// colorOverLifetime -- three colours, matching FireBurstBuilder's
        /// own three-stop gradient shape, just parameterised per layer
        /// rather than hardcoded, since the core and outer flames need
        /// different colour stops.
        /// </summary>
        private static void ApplyFireGradient(ParticleSystem particles, Color hot, Color mid, Color cool)
        {
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(hot, 0.0f),
                    new GradientColorKey(mid, 0.4f),
                    new GradientColorKey(cool, 1.0f),
                },
                new[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f),
                });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>
        /// Turbulence on each particle's own path -- a perfectly straight
        /// cone of particles reads as a hose, not fire. Real flame billows
        /// and wavers as it travels; this is the cheapest way Shuriken can
        /// approximate that without a custom shader.
        /// </summary>
        private static void ApplyFlicker(ParticleSystem particles, float strength, float frequency)
        {
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.5f;
            noise.damping = true;
        }

        /// <summary>
        /// Off at build time -- Flamethrower.TickFlicker turns it on (and
        /// makes it flicker) only while the trigger is actually held,
        /// matching MuzzleFlashObject's own "starts hidden" convention.
        /// </summary>
        private static Light BuildFlameLight(Transform parent)
        {
            var go = new GameObject("FlameLight");
            go.transform.SetParent(parent, false);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.55f, 0.2f);
            light.range = 5.0f;
            light.intensity = 0.0f;
            light.shadows = LightShadows.None;
            return light;
        }
    }
}
