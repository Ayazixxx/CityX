using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The C-menu's camera goes round the player to the front (PlayerMenuCameraMath.Spin): clockwise seen from above, along a circle that
    /// never comes closer than it started or ends, facing the player, smoothly, and exactly onto the menu's shot. And its icons are drawn.
    /// </summary>
    public sealed class PlayerMenuSpinTests
    {
        private static readonly Vector3 Player = new Vector3(10.0f, 2.0f, -4.0f);

        /// <summary>Behind the player and off to the right, looking past the shoulder, as the third-person camera stands.</summary>
        private static void Behind(Vector3 forward, out Vector3 position, out Quaternion rotation)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            position = Player - forward * 5.0f + right * 0.87f + Vector3.up * 2.3f;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        [Test]
        public void FromBehindTheCameraGoesRoundClockwiseAndEndsOnTheMenusShot()
        {
            foreach (float facing in new[] { 0.0f, 40.0f, 135.0f, 290.0f })
            {
                Vector3 forward = Quaternion.Euler(0.0f, facing, 0.0f) * Vector3.forward;
                Behind(forward, out Vector3 from, out Quaternion fromRotation);
                Vector3 to = PlayerMenuCameraMath.CameraPosition(Player, forward);
                Quaternion toRotation = PlayerMenuCameraMath.CameraRotation(forward);
                float degrees = PlayerMenuCameraMath.ClockwiseDegrees(Player, from, to);
                Assert.That(degrees, Is.InRange(150.0f, 200.0f), $"facing {facing}: from behind it is not about a half turn to the front");

                float travelled = 0.0f, previous = PlayerMenuCameraMath.Bearing(Player, from);
                float nearest = float.MaxValue;
                for (int step = 1; step <= 100; step++)
                {
                    float share = PlayerMenuCameraMath.Smooth(step / 100.0f);
                    PlayerMenuCameraMath.Spin(Player, from, fromRotation, to, toRotation, share, out Vector3 position, out Quaternion rotation);
                    float bearing = PlayerMenuCameraMath.Bearing(Player, position);
                    float turned = Mathf.DeltaAngle(previous, bearing);
                    Assert.GreaterOrEqual(turned, -1e-3f, $"facing {facing}: the camera went back anticlockwise at step {step}");
                    travelled += turned;
                    previous = bearing;
                    Vector3 flat = position - Player;
                    flat.y = 0.0f;
                    nearest = Mathf.Min(nearest, flat.magnitude);
                    if (step >= 50)
                    {
                        // Looking at the player (its own aim past the shoulder has faded by now): within a few degrees, across the flat.
                        Vector3 look = rotation * Vector3.forward;
                        look.y = 0.0f;
                        Assert.Less(Vector3.Angle(look, -flat), 6.0f, $"facing {facing}: the camera does not face the player at step {step}");
                    }
                    if (step == 100)
                    {
                        Assert.AreEqual(to, position, $"facing {facing}: the spin does not end on the menu's shot");
                        Assert.AreEqual(toRotation, rotation);
                    }
                }
                Assert.AreEqual(degrees, travelled, 0.5f, $"facing {facing}: the camera did not go the whole way round clockwise");
                Assert.GreaterOrEqual(nearest, PlayerMenuCameraMath.Distance - 1e-3f, $"facing {facing}: the camera cut in towards the body");
            }
        }

        /// <summary>
        /// Already looking at the player's front, a little the other side of the menu's shot, the camera slides the short way to it rather than
        /// going nearly all the way round (owner, 2026-11-26: "make it so the c menu doesnt spin around if you are already looking at the front
        /// of your character ... no unnecessary spins"); on the shot already, it does not turn at all.
        /// </summary>
        [Test]
        public void AlreadyInFrontTheCameraGoesTheShortWayAndOnTheShotItDoesNotTurn()
        {
            foreach (float facing in new[] { 0.0f, 120.0f, 250.0f })
            {
                Vector3 forward = Quaternion.Euler(0.0f, facing, 0.0f) * Vector3.forward;
                Vector3 to = PlayerMenuCameraMath.CameraPosition(Player, forward);
                Quaternion toRotation = PlayerMenuCameraMath.CameraRotation(forward);
                foreach (float off in new[] { 25.0f, 80.0f })
                {
                    // In front, `off` degrees past the shot clockwise (seen from above): clockwise it is most of a turn round. A little nearer,
                    // looking at the player.
                    Vector3 from = Player + Quaternion.Euler(0.0f, off, 0.0f) * (to - Player) * 0.9f;
                    Quaternion fromRotation = Quaternion.LookRotation(Player + Vector3.up * 1.6f - from, Vector3.up);
                    float turn = PlayerMenuCameraMath.TurnDegrees(Player, from, to);
                    Assert.AreEqual(off, -turn, 0.5f, $"facing {facing}, {off} degrees past the shot: it does not go the short way back");
                    float travelled = 0.0f, previous = PlayerMenuCameraMath.Bearing(Player, from);
                    for (int step = 1; step <= 100; step++)
                    {
                        PlayerMenuCameraMath.Spin(Player, from, fromRotation, to, toRotation, PlayerMenuCameraMath.Smooth(step / 100.0f),
                            out Vector3 position, out Quaternion _);
                        float bearing = PlayerMenuCameraMath.Bearing(Player, position);
                        travelled += Mathf.DeltaAngle(previous, bearing);
                        previous = bearing;
                    }
                    Assert.AreEqual(-off, travelled, 0.5f, $"facing {facing}, {off} degrees past the shot: the camera spun round");
                    Assert.Less(PlayerMenuCameraMath.SpinSeconds(turn), 0.6f, "a short slide takes as long as a spin");
                }
                Assert.AreEqual(0.0f, PlayerMenuCameraMath.TurnDegrees(Player, to, to), 1e-3f, "on the shot already, the camera turns");
            }
        }

        [Test]
        public void TheSpinStartsAndEndsWithoutAJoltAndTakesLongerTheFurtherItGoes()
        {
            Assert.AreEqual(0.0f, PlayerMenuCameraMath.Smooth(0.0f), 1e-6f);
            Assert.AreEqual(1.0f, PlayerMenuCameraMath.Smooth(1.0f), 1e-6f);
            const float dt = 1e-3f;
            Assert.Less(PlayerMenuCameraMath.Smooth(dt) / dt, 0.01f, "the spin sets off with a jolt");
            Assert.Less((1.0f - PlayerMenuCameraMath.Smooth(1.0f - dt)) / dt, 0.01f, "the spin stops with a jolt");
            Assert.Less(PlayerMenuCameraMath.SpinSeconds(30.0f), PlayerMenuCameraMath.SpinSeconds(180.0f));
            Assert.Less(PlayerMenuCameraMath.SpinSeconds(180.0f), PlayerMenuCameraMath.SpinSeconds(330.0f));
            Assert.LessOrEqual(PlayerMenuCameraMath.SpinSeconds(360.0f), PlayerMenuCamera.EaseSeconds + 1e-6f);
        }

        [Test]
        public void EveryMenuIconIsDrawnCutOutWhereItShouldBe()
        {
            foreach (string kind in new[] { "exclamation", "person", "people", "pin", "vest", "chart", "star", "bag" })
            {
                Texture2D icon = MenuIcons.Build(kind);
                try
                {
                    int covered = 0;
                    foreach (Color pixel in icon.GetPixels())
                    {
                        if (pixel.a > 0.5f) covered++;
                    }
                    float share = covered / (float)(MenuIcons.Size * MenuIcons.Size);
                    Assert.That(share, Is.InRange(0.08f, 0.6f), $"{kind}: {share:P0} of the icon is drawn");
                    Assert.Less(icon.GetPixel(1, 1).a, 0.01f, $"{kind}: the corner is not clear");
                }
                finally
                {
                    Object.DestroyImmediate(icon);
                }
            }

            Texture2D pin = MenuIcons.Build("pin");
            Texture2D person = MenuIcons.Build("person");
            try
            {
                // Y up in a texture: the pin's hole is at (64, 48) drawn y-down, and the person's gap between head and shoulders at (64, 63).
                Assert.Less(pin.GetPixel(64, MenuIcons.Size - 1 - 48).a, 0.05f, "the pin has no hole");
                Assert.Greater(pin.GetPixel(64, MenuIcons.Size - 1 - 24).a, 0.95f, "the pin's head is not drawn round its hole");
                Assert.Less(person.GetPixel(64, MenuIcons.Size - 1 - 63).a, 0.05f, "no gap between the head and the shoulders");
            }
            finally
            {
                Object.DestroyImmediate(pin);
                Object.DestroyImmediate(person);
            }
        }
    }
}
