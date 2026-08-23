using NUnit.Framework;
using UnityEngine;
using CityX.UI;

namespace CityX.Tests
{
    /// <summary>
    /// IsToonableMaterial's own rule for /celshade -- see that method's own
    /// doc comment for why shader identity replaced a name allowlist, and
    /// why Transparent is excluded specifically to keep glass see-through.
    /// </summary>
    public class DevConsoleViewTests
    {
        private static Material LitMaterial(float surface)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_Surface", surface);
            return material;
        }

        [Test]
        public void AnOpaqueLitMaterialIsToonable()
        {
            Material material = LitMaterial(0.0f);
            Assert.IsTrue(DevConsoleView.IsToonableMaterial(material));
            Object.DestroyImmediate(material);
        }

        /// <summary>
        /// Glass is built on this exact same shader with Surface Type set
        /// to Transparent rather than a different shader asset -- ToonLit
        /// has no Blend state at all, so toon-shading a window would not
        /// restyle it, it would make it suddenly solid.
        /// </summary>
        [Test]
        public void ATransparentLitMaterialIsNotToonable()
        {
            Material material = LitMaterial(1.0f);
            Assert.IsFalse(DevConsoleView.IsToonableMaterial(material));
            Object.DestroyImmediate(material);
        }

        [Test]
        public void ANonLitShaderIsNeverToonable()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            Assert.IsFalse(DevConsoleView.IsToonableMaterial(material));
            Object.DestroyImmediate(material);
        }
    }
}
