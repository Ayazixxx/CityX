using CityX.Character;
using CityX.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Weapons
{
    /// <summary>
    /// The arcane spells' look, drawn in code (no assets to ship): magenta light. Glows are soft spots on quads turned to the camera, additive
    /// (SoftGlow); a cursed orb's core is a dark solid sphere in the game's lit shader, so it reads as a thing in the world with the cel line
    /// round it; trails and motes use the same glow. Made once and shared.
    /// </summary>
    public static class ArcaneVisuals
    {
        public static readonly Color Magenta = new Color(1.0f, 0.16f, 0.82f);

        /// <summary>The white-hot middle of the ball and of a burst.</summary>
        public static readonly Color Hot = new Color(1.0f, 0.72f, 0.96f);

        /// <summary>A cursed orb's dark body.</summary>
        public static readonly Color CursedCore = new Color(0.07f, 0.0f, 0.09f);

        private static Material _template;
        private static Material _glow;
        private static Material _hot;
        private static Material _core;
        private static Mesh _quad;
        private static Mesh _sphere;

        /// <summary>The editor-made light material to copy (SelfSmiteAbility.LightMaterial: soft particles, shipped in a build), when there is one.</summary>
        public static void UseTemplate(Material template)
        {
            if (template == null || template == _template)
            {
                return;
            }
            _template = template;
            _glow = null;
            _hot = null;
        }

        public static Material Glow => _glow != null ? _glow : (_glow = SoftGlow.Make("ArcaneGlow", Magenta, true, _template));

        public static Material HotGlow => _hot != null ? _hot : (_hot = SoftGlow.Make("ArcaneHot", Hot, true, _template));

        /// <summary>A cursed orb's body: dark plum with a faint magenta glow of its own.</summary>
        public static Material CoreMaterial
        {
            get
            {
                if (_core != null)
                {
                    return _core;
                }
                Shader shader = LitShader.Find();
                _core = new Material(shader) { name = "ArcaneCursedCore", hideFlags = HideFlags.DontSave };
                _core.SetColor("_BaseColor", CursedCore);
                _core.EnableKeyword("_EMISSION");
                _core.SetColor("_EmissionColor", Magenta * 0.12f);
                _core.SetFloat("_Smoothness", 0.6f);
                return _core;
            }
        }

        /// <summary>A soft glow of <paramref name="size"/> metres across, turned to the camera every frame.</summary>
        public static MeshRenderer Sprite(Transform parent, Material material, float size, string name = "Glow")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = Quad;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            Unlit(renderer);
            go.AddComponent<FacesCamera>();
            return renderer;
        }

        /// <summary>A solid sphere <paramref name="diameter"/> across.</summary>
        public static MeshRenderer Sphere(Transform parent, Material material, float diameter, string name = "Core")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * diameter;
            go.AddComponent<MeshFilter>().sharedMesh = SphereMesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        /// <summary>A streak left behind <paramref name="go"/>: <paramref name="width"/> across at its head, gone after <paramref name="seconds"/>.</summary>
        public static TrailRenderer Trail(GameObject go, float seconds, float width)
        {
            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = seconds;
            trail.minVertexDistance = 0.04f;
            trail.widthMultiplier = width;
            trail.widthCurve = AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.0f);
            var colours = new Gradient();
            colours.SetKeys(
                new[] { new GradientColorKey(Hot, 0.0f), new GradientColorKey(Magenta, 0.35f), new GradientColorKey(Magenta, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
            trail.colorGradient = colours;
            trail.sharedMaterial = Glow;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }

        /// <summary>
        /// Little sparks of magenta: <paramref name="perSecond"/> drifting off a sphere of <paramref name="radius"/> (0 a second for one that only
        /// bursts, by <see cref="ParticleSystem.Emit(int)"/>), flying up to <paramref name="speed"/>, fading as they shrink.
        /// </summary>
        public static ParticleSystem Motes(Transform parent, float perSecond, float radius, float speed, float size, float lifetime)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(parent, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(Magenta, Hot);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = perSecond;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.01f, radius);
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
            fade.color = alpha;
            ParticleSystem.SizeOverLifetimeModule shrink = particles.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.0f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Glow;
            Unlit(renderer);
            if (perSecond > 0.0f)
            {
                particles.Play();
            }
            return particles;
        }

        /// <summary>A magenta point light (no shadow: a moving point light's shadow is six renders a frame).</summary>
        public static Light Light(Transform parent, float intensity, float range)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Magenta;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>Sets a glow's strength (0..1) on its own renderer, the shared material untouched.</summary>
        public static void Fade(Renderer renderer, Color colour, float strength, MaterialPropertyBlock block)
        {
            if (renderer == null)
            {
                return;
            }
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", colour * Mathf.Clamp01(strength));
            renderer.SetPropertyBlock(block);
        }

        private static void Unlit(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Mesh Quad
        {
            get
            {
                if (_quad != null)
                {
                    return _quad;
                }
                _quad = new Mesh { name = "ArcaneQuad", hideFlags = HideFlags.DontSave };
                _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0.0f), new Vector3(0.5f, -0.5f, 0.0f), new Vector3(0.5f, 0.5f, 0.0f), new Vector3(-0.5f, 0.5f, 0.0f) };
                _quad.uv = new[] { new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f) };
                _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                _quad.RecalculateBounds();
                return _quad;
            }
        }

        /// <summary>
        /// A small round sphere, a metre across, made once: rings of latitude and longitude. Made here rather than taken from a primitive, which
        /// would leave a collider in the world for a frame (a ball's hit makes the first orb, inside the physics step, where nothing may be
        /// destroyed at once).
        /// </summary>
        private static Mesh SphereMesh
        {
            get
            {
                if (_sphere != null)
                {
                    return _sphere;
                }
                const int rings = 8, segments = 12;
                var vertices = new Vector3[(rings + 1) * (segments + 1)];
                var triangles = new int[rings * segments * 6];
                int t = 0;
                for (int r = 0; r <= rings; r++)
                {
                    float polar = r * Mathf.PI / rings;
                    for (int s = 0; s <= segments; s++)
                    {
                        float around = s * 2.0f * Mathf.PI / segments;
                        vertices[r * (segments + 1) + s] =
                            new Vector3(Mathf.Sin(polar) * Mathf.Cos(around), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(around)) * 0.5f;
                        if (r < rings && s < segments)
                        {
                            int a = r * (segments + 1) + s, b = a + segments + 1;
                            triangles[t++] = a;
                            triangles[t++] = a + 1;
                            triangles[t++] = b;
                            triangles[t++] = a + 1;
                            triangles[t++] = b + 1;
                            triangles[t++] = b;
                        }
                    }
                }
                _sphere = new Mesh { name = "ArcaneSphere", hideFlags = HideFlags.DontSave, vertices = vertices, triangles = triangles };
                _sphere.normals = System.Array.ConvertAll(vertices, v => v.normalized);
                _sphere.RecalculateBounds();
                return _sphere;
            }
        }
    }
}
