using System.Collections;
using System.IO;
using System.Text;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: a body on stairs of the real size (0.36 m treads, 0.227 m risers), side on: walking up, walking down, sprinting up, sprinting down, a
    /// row each, to Logs/CityShots/stairs-sheet.png. What the eye cannot tell from a picture is logged as "CityX stairs ...": how far each part
    /// jolts off a steady climb in one frame, how far a foot jumps, the frames a foot is in the stairs, off the ground, and the frame-by-frame heights to stairs-*.csv.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.StairWalkCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class StairWalkCapture
    {
        private const int Cell = 300;
        private const int Columns = 7;
        private const float Step = 1.0f / 60.0f;
        private const float Tread = 0.36f;
        private const float Riser = 0.227f;
        private const int Steps = 22;
        private const float FirstStepZ = 2.0f;

        private static float TopZ => FirstStepZ + Tread * Steps;
        private static float TopY => Riser * Steps;

        [UnityTest]
        public IEnumerator RenderAndMeasureTheStairs()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var made = new System.Collections.Generic.List<GameObject>();
            GameObject Box(Vector3 centre, Vector3 size)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = centre;
                go.transform.localScale = size;
                go.GetComponent<Renderer>().material.color = new Color(0.62f, 0.6f, 0.56f);
                made.Add(go);
                return go;
            }
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(300.0f, 1.0f, 300.0f));
            for (int i = 0; i < Steps; i++)
            {
                float height = Riser * (i + 1);
                Box(new Vector3(0.0f, height * 0.5f, FirstStepZ + Tread * (i + 0.5f)), new Vector3(8.0f, height, Tread));
            }
            Box(new Vector3(0.0f, TopY * 0.5f, TopZ + 10.0f), new Vector3(8.0f, TopY, 20.0f));
            Physics.SyncTransforms();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            var rows = new (string name, bool up, bool sprint)[] { ("walk-up", true, false), ("walk-down", false, false), ("sprint-up", true, true), ("sprint-down", false, true) };
            var sheet = new Texture2D(Cell * Columns, Cell * rows.Length, TextureFormat.RGB24, false);
            var gameSheet = new Texture2D(Cell * Columns, Cell * rows.Length, TextureFormat.RGB24, false);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            for (int row = 0; row < rows.Length; row++)
            {
                (string name, bool up, bool sprint) = rows[row];
                Vector3 start = up ? new Vector3(0.0f, 0.05f, FirstStepZ - 3.0f) : new Vector3(0.0f, TopY + 0.05f, TopZ + 3.0f);
                float yaw = up ? 0.0f : 180.0f;
                GameObject player = Object.Instantiate(prefab, start, Quaternion.Euler(0.0f, yaw, 0.0f));
                player.GetComponent<PlayerInputReader>().enabled = false;
                player.GetComponent<PlayerVitals>().InfiniteStamina = true;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                Animator animator = player.GetComponentInChildren<Animator>();
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform[] feet = { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
                for (int i = 0; i < 20; i++)
                {
                    locomotion.Tick(new LocomotionInput { YawDegrees = yaw }, Step);
                    yield return null;
                }

                var csv = new StringBuilder("frame,z,capsule,hips,head,left,right,leftGround,rightGround,grounded,drop,slope,offset\n");
                var capsuleY = new System.Collections.Generic.List<float>();
                var hipsY = new System.Collections.Generic.List<float>();
                var headY = new System.Collections.Generic.List<float>();
                var footY = new[] { new System.Collections.Generic.List<float>(), new System.Collections.Generic.List<float>() };
                int inStairs = 0, ungrounded = 0, floating = 0, buried = 0, frames = 0, column = 0, stepperFrames = 0, shutKnees = 0, backKnees = 0;
                float worstMiss = 0.0f, tightestKnee = 180.0f, planeOff = 0.0f;
                int stepsAtStart = -1;
                Transform[] shins = { animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg) };
                Transform[] thighs = { animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightUpperLeg) };
                // Past where the body has slowed to the stair's pace.
                float firstZ = FirstStepZ + 2.5f, lastZ = TopZ - 2.5f;
                int farFromPlant = 0;
                var input = new LocomotionInput { Move = Vector2.up, YawDegrees = yaw, Sprint = sprint };
                for (int frame = 0; frame < 900; frame++)
                {
                    locomotion.Tick(input, Step);
                    yield return null;
                    Vector3 p = player.transform.position;
                    if (up ? p.z > TopZ + 1.0f : p.z < FirstStepZ - 1.0f) break;
                    if (p.z < firstZ || p.z > lastZ) continue;

                    frames++;
                    capsuleY.Add(p.y);
                    hipsY.Add(hips.position.y);
                    headY.Add(head.position.y);
                    var line = new StringBuilder($"{frames},{p.z:0.000},{p.y:0.000},{hips.position.y:0.000},{head.position.y:0.000}");
                    for (int f = 0; f < 2; f++)
                    {
                        footY[f].Add(feet[f].position.y);
                    }
                    line.Append($",{feet[0].position.y:0.000},{feet[1].position.y:0.000}");
                    bool anyPlanted = false;
                    for (int f = 0; f < 2; f++)
                    {
                        Vector3 lowest = LowestSole(feet[f]);
                        float ground = GroundUnder(lowest, player.transform);
                        line.Append($",{ground:0.000}");
                        float clearance = lowest.y - ground;
                        anyPlanted |= locomotion.FootPlanting.IsOnFloor(f);
                        if (locomotion.StairSteps.Weight >= 1.0f)
                        {
                            stepperFrames++;
                            // A planted foot the leg cannot reach has left its tread. (One in the air at the end of the leg is a trailing foot.)
                            if (!locomotion.StairSteps.Of(f).Stepping)
                            {
                                float miss = Vector3.Distance(feet[f].position, locomotion.StairSteps.Of(f).Ankle);
                                worstMiss = Mathf.Max(worstMiss, miss);
                                if (miss > 0.05f) farFromPlant++;
                            }
                            // The leg as the eye sees it: the knee's angle, whether it bends forward, whether the shin stays in the leg's plane.
                            Vector3 thighBone = thighs[f].position - shins[f].position, shinBone = feet[f].position - shins[f].position;
                            float knee = Vector3.Angle(thighBone, shinBone);
                            tightestKnee = Mathf.Min(tightestKnee, knee);
                            if (knee < 70.0f) shutKnees++;
                            Vector3 mid = 0.5f * (thighs[f].position + feet[f].position);
                            if (Vector3.Dot(shins[f].position - mid, player.transform.forward) < 0.0f) backKnees++;
                            planeOff = Mathf.Max(planeOff, Mathf.Abs(Vector3.Dot(feet[f].position - thighs[f].position, player.transform.right)));
                        }
                        if (!locomotion.StairSteps.Of(f).Stepping && clearance > 0.05f) floating++;
                        if (clearance < -0.03f) buried++;
                        if (InTheStairs(ToeOf(feet[f]), player.transform) || InTheStairs(HeelOf(feet[f]), player.transform)) inStairs++;
                    }
                    if (stepsAtStart < 0) stepsAtStart = locomotion.StairSteps.StepsTaken;
                    if (!locomotion.IsGrounded) ungrounded++;
                    line.Append($",{(locomotion.IsGrounded ? 1 : 0)},{locomotion.FootPlanting.TotalDrop:0.000},{locomotion.GroundSlope:0.00},{locomotion.GlideOffset:0.000}");
                    csv.AppendLine(line.ToString());

                    if (column < Columns && frames > 20 && frames % (sprint ? 3 : 6) == 0)
                    {
                        Texture2D cell = Render(camera, p + new Vector3(8.0f, 1.0f, 0.0f), p + Vector3.up * 1.0f);
                        sheet.SetPixels(column * Cell, (rows.Length - 1 - row) * Cell, Cell, Cell, cell.GetPixels());
                        Object.Destroy(cell);
                        // And as the game's camera sees it: FOV 50, 5 m back, over the shoulder.
                        camera.orthographic = false;
                        camera.fieldOfView = 50.0f;
                        Vector3 pivot = p + Vector3.up * 1.6f;
                        Vector3 back = Quaternion.Euler(12.0f, yaw, 0.0f) * Vector3.forward;
                        Texture2D game = Render(camera, pivot - back * 5.0f + Vector3.up * 1.0f + player.transform.right * 0.87f, pivot + back * 2.0f);
                        gameSheet.SetPixels(column * Cell, (rows.Length - 1 - row) * Cell, Cell, Cell, game.GetPixels());
                        Object.Destroy(game);
                        camera.orthographic = true;
                        column++;
                    }
                }
                File.WriteAllText(Path.Combine(BuildingShapeCapture.ShotsFolder, $"stairs-{name}.csv"), csv.ToString());
                Debug.Log($"CityX stairs {name}: {frames} frames; jolt off a steady climb, cm in a frame: capsule {Jolt(capsuleY):0.0}, hips {Jolt(hipsY):0.0}, head {Jolt(headY):0.0}; " +
                    $"head wobble {Wobble(headY):0.0} cm rms; foot's biggest jump {Mathf.Max(BiggestJump(footY[0]), BiggestJump(footY[1])):0.0} cm; " +
                    $"foot frames in the stairs {inStairs}, buried {buried}, floating {floating}; not grounded {ungrounded}; stepper {stepperFrames / 2}/{frames} frames, {locomotion.StairSteps.StepsTaken - stepsAtStart} steps; " +
                    $"worst IK miss {worstMiss * 100.0f:0.0} cm, over 5 cm on {farFromPlant} foot frames; knee tightest {tightestKnee:0} deg, under 70: {shutKnees}, bent backward: {backKnees}, foot sideways of hip {planeOff * 100.0f:0} cm");
                Object.Destroy(player);
                yield return null;
            }

            sheet.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(BuildingShapeCapture.ShotsFolder, "stairs-sheet.png"), (byte[])encode.Invoke(null, new object[] { sheet }));
            Object.Destroy(sheet);
            gameSheet.Apply();
            File.WriteAllBytes(Path.Combine(BuildingShapeCapture.ShotsFolder, "stairs-game-camera.png"), (byte[])encode.Invoke(null, new object[] { gameSheet }));
            Object.Destroy(gameSheet);
            foreach (GameObject go in made) Object.Destroy(go);
            Object.Destroy(sun.gameObject);
            Object.Destroy(camera.gameObject);
#else
            yield break;
#endif
        }

        private static float SteadyRate(System.Collections.Generic.List<float> y) => y.Count > 1 ? (y[y.Count - 1] - y[0]) / (y.Count - 1) : 0.0f;

        /// <summary>The most a height moved in one frame beyond what a steady climb would, centimetres.</summary>
        private static float Jolt(System.Collections.Generic.List<float> y)
        {
            float rate = SteadyRate(y), worst = 0.0f;
            for (int i = 1; i < y.Count; i++) worst = Mathf.Max(worst, Mathf.Abs(y[i] - y[i - 1] - rate));
            return worst * 100.0f;
        }

        /// <summary>How far a height wanders either side of a steady climb, centimetres (root mean square).</summary>
        private static float Wobble(System.Collections.Generic.List<float> y)
        {
            float rate = SteadyRate(y), sum = 0.0f, mean = 0.0f;
            for (int i = 0; i < y.Count; i++) mean += y[i] - rate * i;
            mean /= Mathf.Max(1, y.Count);
            for (int i = 0; i < y.Count; i++)
            {
                float off = y[i] - rate * i - mean;
                sum += off * off;
            }
            return Mathf.Sqrt(sum / Mathf.Max(1, y.Count)) * 100.0f;
        }

        private static float BiggestJump(System.Collections.Generic.List<float> y)
        {
            float worst = 0.0f;
            for (int i = 1; i < y.Count; i++) worst = Mathf.Max(worst, Mathf.Abs(y[i] - y[i - 1]));
            return worst * 100.0f;
        }

        private static Vector3 ToeOf(Transform foot) => foot.position + foot.rotation * (new Vector3(0.0f, -BodyRig.FootY, FusedBodyShape.FootToeCentreForward + FusedBodyShape.FootToeRadius) * foot.lossyScale.y);

        private static Vector3 HeelOf(Transform foot) => foot.position + foot.rotation * (new Vector3(0.0f, -BodyRig.FootY, FusedBodyShape.FootHeelCentreForward - FusedBodyShape.FootHeelRadius) * foot.lossyScale.y);

        private static Vector3 LowestSole(Transform foot)
        {
            Vector3 toe = ToeOf(foot), heel = HeelOf(foot);
            return toe.y < heel.y ? toe : heel;
        }

        private static float GroundUnder(Vector3 at, Transform self)
        {
            float best = float.MinValue;
            foreach (RaycastHit hit in Physics.RaycastAll(at + Vector3.up * 1.0f, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.transform.IsChildOf(self)) best = Mathf.Max(best, hit.point.y);
            }
            return best;
        }

        private static bool InTheStairs(Vector3 point, Transform self)
        {
            foreach (Collider hit in Physics.OverlapSphere(point, 0.01f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(self) && point.y < hit.bounds.max.y - 0.03f) return true;
            }
            return false;
        }

        private static Texture2D Render(Camera camera, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            RenderTexture rt = RenderTexture.GetTemporary(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
            image.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return image;
        }
    }
}
