using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Tests
{
    /// <summary>
    /// The bullet trail is not see-through (owner, 2026-10-26: "make the bullet trails less transparent"): it covers what is behind it rather than
    /// adding light to it, and it stays nearly whole for most of its short life.
    /// </summary>
    public sealed class BulletTracerLookTests
    {
        [Test]
        public void TheTrailCoversWhatIsBehindItInsteadOfAddingLight()
        {
            Material material = BulletTracer.EnsureMaterial();
            Assert.AreEqual((float)BlendMode.SrcAlpha, material.GetFloat("_SrcBlend"));
            Assert.AreEqual((float)BlendMode.OneMinusSrcAlpha, material.GetFloat("_DstBlend"), "the trail is additive: it vanishes against a bright sky");
            Assert.AreEqual((int)RenderQueue.Transparent, material.renderQueue, "the trail is drawn with the opaque world: the black lines trace it");
        }

        [Test]
        public void ItStaysNearlyWholeForMostOfItsLife()
        {
            Assert.AreEqual(1.0f, BulletTracer.Opacity(0.0f), 1e-6f);
            Assert.Greater(BulletTracer.Opacity(0.5f), 0.7f, "half way through its life the trail is already half gone");
            Assert.AreEqual(0.0f, BulletTracer.Opacity(1.0f), 1e-6f, "the trail does not fade all the way out");
        }
    }
}
