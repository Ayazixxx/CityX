using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// SMALL OBSTACLES ARE RUN OVER (owner, 2026-10-14, a screenshot of the body stopped dead at the foot of a shop's floor-length opening: "im
    /// getting stuck when running into small obstacles like this when i should just be running right over this"). The controller steps 0.4 m on
    /// its own; anything up to a quarter of the body's height (0.65 m) is stepped onto in the stride by PlayerLocomotion's step up
    /// (StepUpProbe), walking or running, smoothly rather than in one frame, and the body counts as on its feet throughout. Taller is a vault or a
    /// climb; a sill with a whole pane over it, or an opening too low to stand in, is not stepped into; a staircase is still taken a riser at a time.
    /// </summary>
    public sealed class SmallObstaclePlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        private GameObject Box(Vector3 centre, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = centre;
            box.transform.localScale = size;
            _made.Add(box);
            return box;
        }

        private void Street()
        {
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(40.0f, 1.0f, 80.0f));
        }

        private sealed class Run
        {
            public Vector3 End;
            public float MostRiseInAFrame;
            public bool SteppedUp;
            public bool EverOffItsFeet;
            public bool AnimatorEverOffItsFeet;
        }

        /// <summary>
        /// A fresh body set down 2 m before z = 0 (the obstacles start at z = 2), held on W (and Shift when <paramref name="sprint"/>) for
        /// <paramref name="seconds"/>. From its first step up to the end it is watched for leaving its feet.
        /// </summary>
        private IEnumerator RunAt(bool sprint, float seconds, Run run)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, -2.0f), Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var body = player.GetComponent<PlayerLocomotion>();
            Animator animator = player.GetComponentInChildren<Animator>();
            for (int i = 0; i < 20; i++)
            {
                body.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            var forward = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = 0.0f, Sprint = sprint };
            float lastY = player.transform.position.y;
            for (float t = 0.0f; t < seconds; t += 1.0f / 60.0f)
            {
                body.Tick(forward, 1.0f / 60.0f);
                float y = player.transform.position.y;
                run.MostRiseInAFrame = Mathf.Max(run.MostRiseInAFrame, y - lastY);
                lastY = y;
                run.SteppedUp |= body.IsSteppingUp;
                if (run.SteppedUp)
                {
                    run.EverOffItsFeet |= !body.IsGrounded;
                    run.AnimatorEverOffItsFeet |= animator != null && !animator.GetBool("Grounded");
                }
                yield return null;
            }
            run.End = player.transform.position;
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ARaisedFloorUpToAQuarterOfTheBodysHeightIsWalkedAndRunUp([Values(0.3f, 0.44f, 0.55f, 0.64f)] float height,
            [Values(false, true)] bool sprint)
        {
            Time.captureFramerate = 60;
            Street();
            Box(new Vector3(0.0f, height * 0.5f, 17.0f), new Vector3(6.0f, height, 30.0f));   // the floor through a doorway, from z = 2
            Box(new Vector3(-1.9f, 2.0f, 2.2f), new Vector3(1.6f, 4.0f, 0.4f));              // the jambs: a doorway 2.2 m wide
            Box(new Vector3(1.9f, 2.0f, 2.2f), new Vector3(1.6f, 4.0f, 0.4f));
            Physics.SyncTransforms();
            var run = new Run();
            yield return RunAt(sprint, 2.0f, run);
            string how = sprint ? "running" : "walking";
            Assert.Greater(run.End.z, 3.0f, $"stuck at a {height:0.00} m step ({how}): ended at {run.End}");
            Assert.AreEqual(height, run.End.y, 0.06f, $"not standing on the {height:0.00} m floor ({how}): {run.End}");
            if (height > 0.4f)
            {
                Assert.IsTrue(run.SteppedUp, "taller than the controller steps, so the step up took it");
                // At 60 frames a second a pop up the whole step in one frame is 0.44 m and more; the step up rises at most 7.5 m/s (0.125 m a frame).
                Assert.Less(run.MostRiseInAFrame, 0.14f, $"the body popped {run.MostRiseInAFrame:0.00} m in one frame onto a {height:0.00} m step ({how})");
                Assert.IsFalse(run.EverOffItsFeet, $"the body left its feet stepping up ({how}): it would lose its jump and its footsteps");
                Assert.IsFalse(run.AnimatorEverOffItsFeet, $"the animator's Grounded went false stepping up ({how}): other players would see a fall");
            }
        }

        [UnityTest]
        public IEnumerator ALowBarOnTheGroundIsSteppedOver([Values(0.35f, 0.5f, 0.62f)] float height, [Values(false, true)] bool sprint)
        {
            Time.captureFramerate = 60;
            Street();
            Box(new Vector3(0.0f, height * 0.5f, 2.0f), new Vector3(6.0f, height, 0.25f));
            Physics.SyncTransforms();
            var run = new Run();
            yield return RunAt(sprint, 2.0f, run);
            Assert.Greater(run.End.z, 3.5f, $"stuck at a {height:0.00} m bar ({(sprint ? "running" : "walking")}): ended at {run.End}");
            Assert.AreEqual(0.0f, run.End.y, 0.06f, "back on the street past the bar");
        }

        [UnityTest]
        public IEnumerator AStepTallerThanAQuarterOfTheBodyIsNotWalkedUp()
        {
            Time.captureFramerate = 60;
            Street();
            Box(new Vector3(0.0f, 0.4f, 17.0f), new Vector3(6.0f, 0.8f, 30.0f));
            Physics.SyncTransforms();
            var run = new Run();
            yield return RunAt(false, 1.5f, run);
            Assert.IsFalse(run.SteppedUp, "a 0.8 m step was taken as a step");
            Assert.Less(run.End.z, 2.0f, $"walked up a 0.8 m step: {run.End}");
            Assert.Less(run.End.y, 0.1f, "still on the street");
        }

        /// <summary>A sill with a whole pane of glass over it is a window, not a step: the body stays in front of it.</summary>
        [UnityTest]
        public IEnumerator ALowSillWithAWholePaneOverItIsNotSteppedOnto()
        {
            Time.captureFramerate = 60;
            Street();
            Box(new Vector3(0.0f, 0.25f, 2.15f), new Vector3(6.0f, 0.5f, 0.3f));    // the sill: a wall 0.5 m high, 0.3 m thick
            Box(new Vector3(0.0f, 1.8f, 2.12f), new Vector3(6.0f, 2.6f, 0.02f));    // the pane, in the middle of the wall's thickness
            Physics.SyncTransforms();
            var run = new Run();
            yield return RunAt(true, 1.5f, run);
            Assert.IsFalse(run.SteppedUp, "stepped up onto a sill with the glass still in");
            Assert.Less(run.End.y, 0.1f, $"climbed onto the sill of a whole window: {run.End}");
        }

        /// <summary>A shattered window lower than the body is gone through with a dive (the vault), not stepped into: the body would not fit.</summary>
        [UnityTest]
        public IEnumerator AnOpeningTooLowToStandInIsNotSteppedInto()
        {
            Time.captureFramerate = 60;
            Street();
            Box(new Vector3(0.0f, 0.25f, 2.15f), new Vector3(6.0f, 0.5f, 0.3f));    // the sill
            Box(new Vector3(0.0f, 3.5f, 2.15f), new Vector3(6.0f, 2.0f, 0.3f));     // the wall over a 2 m opening (from 2.5 m up)
            Box(new Vector3(0.0f, 0.2f, 6.3f), new Vector3(6.0f, 0.4f, 8.0f));      // the room's floor, a little under the sill
            Physics.SyncTransforms();
            var run = new Run();
            yield return RunAt(true, 1.5f, run);
            Assert.IsFalse(run.SteppedUp, "stepped up into an opening lower than the body");
            Assert.Less(run.End.z, 2.0f, $"went through an opening lower than the body without a dive: {run.End}");
        }

        /// <summary>
        /// Under a ceiling only a little over the head (a low passage, the top of a shop window) the body still walks and runs on. The controller
        /// lifts the body by its whole step (0.4 m) before every sideways move, and a ceiling closer than that over the head used to stop it dead.
        /// </summary>
        [UnityTest]
        public IEnumerator ACeilingJustOverTheHeadDoesNotStopTheBody([Values(0.12f, 0.25f)] float headroom, [Values(false, true)] bool sprint)
        {
            Time.captureFramerate = 60;
            Street();
            Box(new Vector3(0.0f, BodyRig.StandingHeight + headroom + 0.5f, 6.0f), new Vector3(6.0f, 1.0f, 8.0f));   // a ceiling from z = 2 to 10
            Physics.SyncTransforms();
            var run = new Run();
            yield return RunAt(sprint, 2.0f, run);
            Assert.Greater(run.End.z, 3.0f, $"stopped under a ceiling {headroom:0.00} m over the head ({(sprint ? "running" : "walking")}): {run.End}");
        }

        /// <summary>
        /// The city's floor-length window, shattered: a 0.4 m sill (a wall 0.3 m thick) with the top of the opening 2.86 m over it and the room's
        /// floor 0.16 m under it. The body steps onto the sill and goes on in (the owner's screenshot).
        /// </summary>
        [UnityTest]
        public IEnumerator AShatteredFloorLengthWindowIsRunInto([Values(0.4f, 0.55f)] float sill, [Values(false, true)] bool sprint)
        {
            Time.captureFramerate = 60;
            Street();
            Box(new Vector3(0.0f, sill * 0.5f, 2.15f), new Vector3(6.0f, sill, 0.3f));                                 // the wall under the opening
            Box(new Vector3(0.0f, sill + 2.86f + 0.5f, 2.15f), new Vector3(6.0f, 1.0f, 0.3f));                          // the wall over it
            Box(new Vector3(-2.2f, 2.0f, 2.15f), new Vector3(1.6f, 4.0f, 0.3f));                                         // jambs: 2.8 m wide
            Box(new Vector3(2.2f, 2.0f, 2.15f), new Vector3(1.6f, 4.0f, 0.3f));
            Box(new Vector3(0.0f, (sill - 0.16f) * 0.5f, 8.3f), new Vector3(6.0f, sill - 0.16f, 12.0f));                // the room's floor, from z = 2.3
            Physics.SyncTransforms();
            var run = new Run();
            yield return RunAt(sprint, 2.0f, run);
            Assert.Greater(run.End.z, 3.5f, $"held at a {sill:0.00} m sill under the top of the opening ({(sprint ? "running" : "walking")}): {run.End}");
            Assert.AreEqual(sill - 0.16f, run.End.y, 0.06f, "standing on the room's floor");
        }

        /// <summary>A flight of stairs is still the controller's, a riser at a time: the step up never takes two risers as one step.</summary>
        [UnityTest]
        public IEnumerator AFlightOfStairsIsNotTakenAsOneStep([Values(false, true)] bool sprint)
        {
            Time.captureFramerate = 60;
            Street();
            for (int i = 0; i < 6; i++)
            {
                float top = 0.22f * (i + 1);
                Box(new Vector3(0.0f, top * 0.5f, 2.0f + i * 0.32f + 15.0f), new Vector3(3.0f, top, 30.0f));   // riser i at z = 2 + 0.32 i
            }
            Physics.SyncTransforms();
            var run = new Run();
            yield return RunAt(sprint, 2.5f, run);
            Assert.IsFalse(run.SteppedUp, "a staircase's risers were taken as one tall step");
            Assert.AreEqual(1.32f, run.End.y, 0.06f, $"did not climb the stairs: {run.End}");
        }
    }
}
