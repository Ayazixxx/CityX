using NUnit.Framework;
using UnityEngine;
using CityX.Character;
using CityX.UI;

namespace CityX.Tests
{
    /// <summary>
    /// IsToonableMaterial's own rule for /celshade -- see that method's own
    /// doc comment for why shader identity replaced a name allowlist, and
    /// why Transparent is excluded specifically to keep glass see-through.
    /// Also the console's own Open/Close handling of the player's Animator
    /// -- explicit bug report: opening the console left whatever animation
    /// was already playing running in place, since disabling InputReader
    /// alone stops new input, not the Animator's own already-running state
    /// machine.
    /// </summary>
    public class DevConsoleViewTests
    {
        private GameObject _consoleObject;
        private GameObject _bodyObject;

        [TearDown]
        public void TearDown()
        {
            if (_consoleObject != null) Object.DestroyImmediate(_consoleObject);
            if (_bodyObject != null) Object.DestroyImmediate(_bodyObject);
        }

        private DevConsoleView MakeConsoleWithAnimator(out Animator animator, out PlayerVitals vitals)
        {
            _bodyObject = new GameObject("Body");
            vitals = _bodyObject.AddComponent<PlayerVitals>();
            vitals.Revive();
            animator = _bodyObject.AddComponent<Animator>();

            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Vitals = vitals;
            view.Bind();
            return view;
        }

        [Test]
        public void OpeningTheConsoleDisablesThePlayerAnimator()
        {
            DevConsoleView view = MakeConsoleWithAnimator(out Animator animator, out _);
            Assert.IsTrue(animator.enabled, "setup failed -- the animator should start enabled");

            view.Open();

            Assert.IsFalse(animator.enabled,
                "opening the console left the animator running -- whatever animation was playing keeps looping");
        }

        [Test]
        public void ClosingTheConsoleReEnablesThePlayerAnimator()
        {
            DevConsoleView view = MakeConsoleWithAnimator(out Animator animator, out _);
            view.Open();

            view.Close();

            Assert.IsTrue(animator.enabled, "closing the console did not hand animation control back");
        }

        /// <summary>
        /// Ragdoll.Activate already disabled this same Animator for a real
        /// reason (a corpse's own bones need to be free for physics) --
        /// closing the console while the player happens to be dead must not
        /// undo that.
        /// </summary>
        [Test]
        public void ClosingTheConsoleDoesNotReEnableTheAnimatorIfThePlayerDied()
        {
            DevConsoleView view = MakeConsoleWithAnimator(out Animator animator, out PlayerVitals vitals);
            view.Open();
            IDamageable target = vitals;
            target.ApplyHit(vitals.MaxHealth * 2.0f, Vector3.zero, 0.0f);
            Assert.IsTrue(vitals.IsDead, "setup failed -- the body should be dead");
            animator.enabled = false; // Ragdoll.Activate's own real job; not present on this bare fixture.

            view.Close();

            Assert.IsFalse(animator.enabled, "closing the console revived the animator on a dead body");
        }

        /// <summary>
        /// GrappleSwing.Attach already disabled this same Animator for a
        /// real reason (its own procedural swing pose owns every bone it
        /// touches) -- closing the console mid-swing must not undo that,
        /// the same guard as the death case above. EXPLICIT BUG REPORT:
        /// opening then closing the console mid-swing sometimes left the
        /// character looking stretched/distorted, because the re-enabled
        /// Animator and GrappleSwing.UpdateSwingPose were both writing the
        /// same bones from then on.
        /// </summary>
        [Test]
        public void ClosingTheConsoleDoesNotReEnableTheAnimatorMidSwing()
        {
            DevConsoleView view = MakeConsoleWithAnimator(out Animator animator, out _);
            GrappleSwing swing = _bodyObject.AddComponent<GrappleSwing>();
            swing.Bind();
            view.Swing = swing;
            swing.Attach(_bodyObject.transform.position + Vector3.up * 5.0f);
            Assert.IsFalse(animator.enabled, "setup failed -- attaching the swing should already have disabled the animator");
            view.Open();

            view.Close();

            Assert.IsFalse(animator.enabled, "closing the console re-enabled the animator while the grapple swing still owned it");
        }

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
