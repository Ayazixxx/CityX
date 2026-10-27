using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.View
{
    /// <summary>
    /// Rings spreading on standing water, as a particle system emitted one ring at a time: flat on the surface (a horizontal billboard),
    /// swelling from a point to full size, fast at first then easing out, while fading. The rain's rings on soaked ground and the rings round
    /// a body wading are both these (RainEffect, WaterSplashes).
    /// </summary>
    public static class RippleRings
    {
        public static ParticleSystem Build(Transform parent, string name, Material material, int maxRings, float lifetime, float size, Color colour)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);

            var rings = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = rings.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = maxRings;
            main.startLifetime = lifetime;
            main.startSpeed = 0.0f;
            main.startSize = size;
            main.startColor = colour;
            main.gravityModifier = 0.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = rings.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = rings.shape;
            shape.enabled = false;

            // Swells from a dot to full size, fast at first then easing out.
            ParticleSystem.SizeOverLifetimeModule sizeOverLife = rings.sizeOverLifetime;
            sizeOverLife.enabled = true;
            var swell = new AnimationCurve(
                new Keyframe(0.0f, 0.12f, 0.0f, 2.2f),
                new Keyframe(1.0f, 1.0f, 0.4f, 0.0f));
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1.0f, swell);

            ParticleSystem.ColorOverLifetimeModule fade = rings.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.12f), new GradientAlphaKey(0.0f, 1.0f) });
            fade.color = alpha;

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return rings;
        }
    }
}
