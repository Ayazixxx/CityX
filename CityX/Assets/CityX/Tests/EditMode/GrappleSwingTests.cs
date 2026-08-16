using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The grapple's own rope physics, driven through the real component
    /// with a real CharacterController under it -- same technique
    /// PlayerLocomotionTests already established for testing movement
    /// without a scene.
    ///
    /// Whether a swing FEELS good can only be judged live. What is checked
    /// here is the physics it rests on -- ported from
    /// tests/test_grappling_hook_check.gd: the rope must never stretch past
    /// its own length, it must kill only the outward part of velocity
    /// (keeping the tangential part a pendulum swings on), and letting go
    /// must preserve the momentum built up, since carrying that momentum
    /// out into free flight is the entire mechanic.
    /// </summary>
    public sealed class GrappleSwingTests
    {
        private GameObject _object;
        private GrappleSwing _swing;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Body");
            _object.AddComponent<CharacterController>();
            _swing = _object.AddComponent<GrappleSwing>();
            // EditMode never calls Awake -- see GrappleSwing.Bind's own
            // doc comment.
            _swing.Bind();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        /// <summary>
        /// Thrown sideways hard from well below the anchor, so the
        /// constraint is doing real work every frame -- 60 physics-style
        /// steps under gravity must never let the body end up further from
        /// the anchor than the rope is long.
        /// </summary>
        [Test]
        public void TheRopeNeverStretches()
        {
            _object.transform.position = new Vector3(0.0f, 25.0f, 0.0f);
            // Physics.SyncTransforms is what makes the very first Move() see
            // this position at all -- CharacterController otherwise reads a
            // stale (pre-move) cached position for exactly one Move() call
            // after a raw transform write, which is a real Unity quirk this
            // test would otherwise trip over on frame 0. Real play never
            // hits it: the controller is already being Moved every frame
            // long before a swing ever starts.
            Physics.SyncTransforms();
            var anchor = new Vector3(0.0f, 30.0f, -8.0f);
            _swing.Attach(anchor, new Vector3(18.0f, 0.0f, 0.0f));
            float rope = _swing.RopeLength;

            float worst = 0.0f;
            for (int i = 0; i < 60; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, 1.0f / 60.0f);
                worst = Mathf.Max(worst, Vector3.Distance(_object.transform.position, anchor));
            }

            // A small tolerance: the constraint is solved once per frame, so
            // a fast step can overshoot slightly before being pulled back.
            Assert.LessOrEqual(worst, rope + 0.35f,
                $"the rope stretched to {worst:0.00}m from a {rope:0.00}m rope -- it's behaving like elastic");
        }

        /// <summary>
        /// Purely horizontal velocity, dropped from directly below an
        /// anchor -- entirely tangential to a rope hanging straight up, so
        /// an ideal constraint leaves every bit of it intact rather than
        /// collapsing it to a dead hang.
        /// </summary>
        [Test]
        public void TheConstraintKeepsTangentialSpeed()
        {
            _object.transform.position = new Vector3(0.0f, 20.0f, 0.0f);
            const float startSpeed = 12.0f;
            _swing.Attach(new Vector3(0.0f, 30.0f, 0.0f), new Vector3(startSpeed, 0.0f, 0.0f));

            _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, 1.0f / 60.0f);
            _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, 1.0f / 60.0f);

            float horizontal = new Vector2(_swing.Velocity.x, _swing.Velocity.z).magnitude;
            Assert.GreaterOrEqual(horizontal, startSpeed * 0.8f,
                $"tangential speed collapsed from {startSpeed:0.0} to {horizontal:0.0} in two frames -- the player would hang dead instead of swinging");
        }

        /// <summary>Releasing preserves horizontal momentum and adds the deliberate upward kick.</summary>
        [Test]
        public void ABoostedReleaseKeepsMomentumAndAddsHeight()
        {
            _object.transform.position = new Vector3(0.0f, 20.0f, 0.0f);
            _swing.Attach(new Vector3(0.0f, 30.0f, 0.0f), new Vector3(14.0f, 0.0f, 0.0f));

            _swing.Release(true);

            Assert.IsFalse(_swing.IsActive, "Release left the swing active");
            Assert.AreEqual(14.0f, _swing.LastReleaseVelocity.x, 0.01f,
                "releasing changed horizontal momentum");
            Assert.Greater(_swing.LastReleaseVelocity.y, 0.0f,
                "a boosted release gave no upward kick");
        }

        /// <summary>An unboosted release (death, unequipping) must NOT hand out free height.</summary>
        [Test]
        public void AnUnboostedReleaseAddsNoFreeHeight()
        {
            _object.transform.position = new Vector3(0.0f, 20.0f, 0.0f);
            _swing.Attach(new Vector3(0.0f, 30.0f, 0.0f), new Vector3(14.0f, 0.0f, 0.0f));

            _swing.Release(false);

            Assert.AreEqual(0.0f, _swing.LastReleaseVelocity.y, 0.01f,
                "a forced (unboosted) release still added upward velocity");
        }

        /// <summary>Reeling in shortens the rope -- how you climb to a roof -- and stops at the minimum rather than winding into the wall.</summary>
        [Test]
        public void ReelingInShortensTheRopeAndStopsAtTheMinimum()
        {
            _object.transform.position = new Vector3(0.0f, 5.0f, 0.0f);
            _swing.Attach(new Vector3(0.0f, 30.0f, 0.0f));
            float start = _swing.RopeLength;

            _swing.Tick(Vector2.zero, 0.0f, false, false, true, false, 1.0f);
            Assert.Less(_swing.RopeLength, start, "reeling in didn't shorten the rope");

            for (int i = 0; i < 50; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, true, false, 0.1f);
            }
            Assert.GreaterOrEqual(_swing.RopeLength, GrappleSwing.MinRopeLength - 0.001f,
                "the rope reeled in past its own minimum -- you'd wind into the wall");
        }

        /// <summary>Paying out lengthens the rope and stops at the maximum.</summary>
        [Test]
        public void PayingOutLengthensTheRopeAndStopsAtTheMaximum()
        {
            _object.transform.position = new Vector3(0.0f, 5.0f, 0.0f);
            _swing.Attach(new Vector3(0.0f, 8.0f, 0.0f));
            float start = _swing.RopeLength;

            for (int i = 0; i < 200; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, false, true, 1.0f);
            }
            Assert.Greater(_swing.RopeLength, start, "paying out didn't lengthen the rope");
            Assert.LessOrEqual(_swing.RopeLength, GrappleSwing.MaxRopeLength + 0.001f,
                "the rope paid out past its own maximum");
        }

        /// <summary>A plain release (left-click again) drops the rope without the height boost.</summary>
        [Test]
        public void APlainReleaseInputEndsTheSwingWithoutABoost()
        {
            _object.transform.position = new Vector3(0.0f, 20.0f, 0.0f);
            _swing.Attach(new Vector3(0.0f, 30.0f, 0.0f));

            _swing.Tick(Vector2.zero, 0.0f, false, true, false, false, 1.0f / 60.0f);

            Assert.IsFalse(_swing.IsActive, "the plain release input didn't end the swing");
            // Not exactly zero: gravity already ran once this same Tick
            // before the release was checked, so the only thing this
            // proves is that no BOOST was layered on top of that.
            Assert.LessOrEqual(_swing.LastReleaseVelocity.y, 0.0f,
                "a plain release still added upward velocity");
        }
    }
}
