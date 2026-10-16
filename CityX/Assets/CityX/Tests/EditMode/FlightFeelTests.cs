using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// How flying FEELS: the camera widens and falls back with speed, a boost
    /// punches, the streaks come in when it is fast. Pure rules first, then the
    /// jetpack driving a real camera with them. The jetpack's speeds and lean are
    /// deliberately not in here -- none of this changes them.
    /// </summary>
    public sealed class FlightFeelTests
    {
        private GameObject _jetpackObject;
        private GameObject _cameraObject;
        private Jetpack _jetpack;
        private ThirdPersonCamera _camera;

        // The tunables are static so /flight can change them live; leave them as found.
        private float _fov, _kick, _kickSeconds, _pull, _kickPull, _smooth, _streaks, _start, _full;

        [SetUp]
        public void SetUp()
        {
            _fov = FlightFeel.FovGainDegrees; _kick = FlightFeel.BoostKickFovDegrees;
            _kickSeconds = FlightFeel.BoostKickSeconds; _pull = FlightFeel.PullBackMetres;
            _kickPull = FlightFeel.BoostKickPullBackMetres; _smooth = FlightFeel.SmoothSeconds;
            _streaks = FlightFeel.MaxStreaksPerSecond; _start = FlightFeel.StartSpeed; _full = FlightFeel.FullSpeed;

            _jetpackObject = new GameObject("Jetpack");
            _jetpack = _jetpackObject.AddComponent<Jetpack>();
            _cameraObject = new GameObject("Camera");
            _camera = _cameraObject.AddComponent<ThirdPersonCamera>();
            _jetpack.FeelCamera = _camera;
        }

        [TearDown]
        public void TearDown()
        {
            FlightFeel.FovGainDegrees = _fov; FlightFeel.BoostKickFovDegrees = _kick;
            FlightFeel.BoostKickSeconds = _kickSeconds; FlightFeel.PullBackMetres = _pull;
            FlightFeel.BoostKickPullBackMetres = _kickPull; FlightFeel.SmoothSeconds = _smooth;
            FlightFeel.MaxStreaksPerSecond = _streaks; FlightFeel.StartSpeed = _start; FlightFeel.FullSpeed = _full;
            Object.DestroyImmediate(_jetpackObject);
            Object.DestroyImmediate(_cameraObject);
        }

        private void Fly(Vector3 velocity, bool airborne, bool boosting, float seconds)
        {
            const float step = 1.0f / 60.0f;
            for (float t = 0.0f; t < seconds; t += step)
            {
                _jetpack.TickFlightFeel(velocity, airborne, boosting, step);
            }
        }

        // --- the rules ---

        [Test]
        public void NothingHappensAtWalkingPaceAndItPeaksAtFullSpeed()
        {
            Assert.AreEqual(0.0f, FlightFeel.SpeedFraction(0.0f));
            Assert.AreEqual(0.0f, FlightFeel.SpeedFraction(FlightFeel.StartSpeed));
            Assert.AreEqual(1.0f, FlightFeel.SpeedFraction(FlightFeel.FullSpeed), 1e-5f);
            Assert.AreEqual(1.0f, FlightFeel.SpeedFraction(FlightFeel.FullSpeed * 3.0f), 1e-5f);

            float previous = -1.0f;
            for (float speed = 0.0f; speed <= 30.0f; speed += 1.0f)
            {
                float fraction = FlightFeel.SpeedFraction(speed);
                Assert.GreaterOrEqual(fraction, previous, "faster never feels slower");
                previous = fraction;
            }
        }

        [Test]
        public void TheBoostPunchIsFullAtOnceAndFadesToNothing()
        {
            Assert.AreEqual(1.0f, FlightFeel.KickEnvelope(0.0f), 1e-5f);
            Assert.AreEqual(0.0f, FlightFeel.KickEnvelope(FlightFeel.BoostKickSeconds));
            Assert.AreEqual(0.0f, FlightFeel.KickEnvelope(-1.0f));
            Assert.Less(FlightFeel.KickEnvelope(FlightFeel.BoostKickSeconds * 0.5f), 0.5f, "it eases out quickly");
        }

        [Test]
        public void ViewWidensAndPullsBackWithSpeedAndMoreOnAKick()
        {
            Assert.AreEqual(0.0f, FlightFeel.FovBonus(0.0f, 0.0f));
            Assert.AreEqual(0.0f, FlightFeel.DistanceBonus(0.0f, 0.0f));
            Assert.AreEqual(FlightFeel.FovGainDegrees, FlightFeel.FovBonus(1.0f, 0.0f), 1e-4f);
            Assert.Greater(FlightFeel.FovBonus(1.0f, 1.0f), FlightFeel.FovBonus(1.0f, 0.0f));
            Assert.Greater(FlightFeel.DistanceBonus(1.0f, 1.0f), FlightFeel.DistanceBonus(1.0f, 0.0f));
            Assert.Greater(FlightFeel.FovBonus(0.0f, 1.0f), 0.0f, "even a boost from a near-standstill punches the view");
        }

        [Test]
        public void StreaksOnlyComeInWhenItIsClearlyFast()
        {
            Assert.AreEqual(0.0f, FlightFeel.StreakRate(0.0f));
            Assert.AreEqual(0.0f, FlightFeel.StreakRate(0.25f));
            Assert.Greater(FlightFeel.StreakRate(0.6f), 0.0f);
            Assert.AreEqual(FlightFeel.MaxStreaksPerSecond, FlightFeel.StreakRate(1.0f), 1e-4f);

            FlightFeel.MaxStreaksPerSecond = 0.0f;
            Assert.AreEqual(0.0f, FlightFeel.StreakRate(1.0f), "streaks can be switched off");
        }

        [Test]
        public void TheWindQuickensWithSpeed()
        {
            Assert.Less(FlightFeel.WindPitch(0.0f), FlightFeel.WindPitch(1.0f));
        }

        // --- the jetpack driving a real camera ---

        [Test]
        public void FlyingFastWidensTheRealCameraAndPullsItBack()
        {
            Fly(new Vector3(0.0f, 0.0f, 22.0f), airborne: true, boosting: false, seconds: 3.0f);

            Assert.AreEqual(FlightFeel.FovGainDegrees, _camera.FlightFovBonus, 0.2f);
            Assert.AreEqual(FlightFeel.PullBackMetres, _camera.FlightDistanceBonus, 0.05f);
        }

        [Test]
        public void OnTheGroundTheViewIsNeverWidenedHoweverFast()
        {
            Fly(new Vector3(0.0f, 0.0f, 22.0f), airborne: false, boosting: false, seconds: 2.0f);

            Assert.AreEqual(0.0f, _camera.FlightFovBonus, 0.01f, "sprinting with a jetpack on the back is not flying");
            Assert.AreEqual(0.0f, _camera.FlightDistanceBonus, 0.01f);
        }

        [Test]
        public void ABoostPunchesTheViewBeyondCruisingAndThenSettles()
        {
            Vector3 fast = new Vector3(0.0f, 0.0f, 22.0f);
            Fly(fast, airborne: true, boosting: false, seconds: 3.0f);
            float cruising = _camera.FlightFovBonus;

            Fly(fast, airborne: true, boosting: true, seconds: 0.15f);
            float punched = _camera.FlightFovBonus;
            Assert.Greater(punched, cruising + 1.0f, "the boost should visibly push the view wider");

            Fly(fast, airborne: true, boosting: true, seconds: 3.0f);
            Assert.AreEqual(cruising, _camera.FlightFovBonus, 0.3f, "and it settles back rather than staying punched");
        }

        [Test]
        public void SlowingDownReturnsTheViewToNormal()
        {
            Fly(new Vector3(0.0f, 0.0f, 22.0f), airborne: true, boosting: false, seconds: 3.0f);
            Fly(new Vector3(0.0f, 0.0f, 2.0f), airborne: true, boosting: false, seconds: 3.0f);

            Assert.AreEqual(0.0f, _camera.FlightFovBonus, 0.2f);
            Assert.AreEqual(0.0f, _camera.FlightDistanceBonus, 0.05f);
        }

        [Test]
        public void ACrashOrRagdollEndsTheEffectAtOnce()
        {
            Fly(new Vector3(0.0f, 0.0f, 22.0f), airborne: true, boosting: true, seconds: 2.0f);
            Assert.Greater(_camera.FlightFovBonus, 5.0f, "setup: it should be widened");

            _jetpack.StopThrustEffects();

            Assert.AreEqual(0.0f, _camera.FlightFovBonus, 1.0f);
        }

        [Test]
        public void TheCameraActuallyUsesTheBonusesItIsGiven()
        {
            // The camera adds the widening to its rest view and the pull-back to its distance.
            _camera.Distance = 4.0f;
            _camera.FlightDistanceBonus = 2.0f;
            _camera.FlightFovBonus = 10.0f;

            Assert.AreEqual(2.0f, _camera.FlightDistanceBonus);
            Assert.AreEqual(10.0f, _camera.FlightFovBonus);
        }

        /// <summary>
        /// THE SPEED LINES ARE SUBTLE (owner, 2026-10-15: "make the lines ... a little more subtle. research how other games to it"): few, only at
        /// real speed, at the screen's edges with the middle clear, thin and faint -- faintest nearest the middle. And since the same evening SOFT
        /// ("looks way better when they are mostly invisible instead of just being white lines"): a wisp with no edge, fading in and out along its
        /// length, at under a tenth of full strength.
        /// </summary>
        [Test]
        public void TheSpeedLinesAreFewFaintSoftAndKeptToTheEdges()
        {
            Assert.LessOrEqual(FlightFeel.MaxStreaksPerSecond * CityX.Weapons.JetpackSpeedLines.RateShare, 35.0f, "too many streaks at full speed");
            Assert.AreEqual(0.0f, FlightFeel.StreakRate(0.4f), "streaks at an ordinary flying speed");
            Assert.Less(FlightFeel.StreakRate(0.75f), FlightFeel.MaxStreaksPerSecond * 0.4f, "they should build up only near full speed");
            Assert.GreaterOrEqual(CityX.Weapons.JetpackSpeedLines.MinRing, 4.5f, "the middle of the screen should stay clear");
            Assert.LessOrEqual(CityX.Weapons.JetpackSpeedLines.MaxAlpha, 0.15f, "too bright: a wisp, not a line");
            Assert.LessOrEqual(CityX.Weapons.JetpackSpeedLines.VisibleWidth, 0.1f, "too thick");
            Assert.Less(CityX.Weapons.JetpackSpeedLines.AlphaAt(CityX.Weapons.JetpackSpeedLines.MinRing),
                CityX.Weapons.JetpackSpeedLines.AlphaAt(CityX.Weapons.JetpackSpeedLines.MaxRing) * 0.5f, "the inner streaks should be the faintest");
            Assert.GreaterOrEqual(CityX.Weapons.JetpackSpeedLines.LengthPerSpeed * 60.0f, 4.0f, "a full-speed streak should be metres long");

            // The sprite: full in the middle, nothing at its sides (no hard edge), nothing at its ends (it fades in and out along its length).
            Assert.AreEqual(1.0f, CityX.Weapons.JetpackSpeedLines.SpriteAlpha(0.5f, 0.5f), 1e-3f);
            Assert.Less(CityX.Weapons.JetpackSpeedLines.SpriteAlpha(0.5f, 0.0f), 0.01f, "the sprite has a hard side");
            Assert.Less(CityX.Weapons.JetpackSpeedLines.SpriteAlpha(0.5f, 1.0f), 0.01f, "the sprite has a hard side");
            Assert.AreEqual(0.0f, CityX.Weapons.JetpackSpeedLines.SpriteAlpha(0.0f, 0.5f), 1e-6f, "the sprite starts with a hard end");
            Assert.AreEqual(0.0f, CityX.Weapons.JetpackSpeedLines.SpriteAlpha(1.0f, 0.5f), 1e-6f, "the sprite ends with a hard end");
            Assert.AreEqual(Mathf.Exp(-1.0f), CityX.Weapons.JetpackSpeedLines.SpriteAlpha(0.5f, 0.5f + CityX.Weapons.JetpackSpeedLines.CoreShare), 1e-3f, "a Gaussian across");
        }
    }
}
