using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders dismemberment (docs/DISMEMBERMENT.md) to Logs/CityShots/dismember-*.png -- a sword through the neck, a sword
    /// through an arm and a leg, a blunt blow bursting a head, a forearm and a whole body, each a moment after and once it has all landed (a
    /// burst's pieces close up too, and how long the blow took is logged); and a corpse hacked at (head burst, arm cut, shin shot off).
    /// Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.DismembermentCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class DismembermentCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderDismemberment()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(2000.0f, 1.0f, 2000.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 40.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            Physics.SyncTransforms();

            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            (string name, HumanBodyBones from, HumanBodyBones to, float share, bool edged)[] cases =
            {
                ("decapitated", HumanBodyBones.Neck, HumanBodyBones.Head, 0.5f, true),
                ("arm-cut", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.5f, true),
                ("leg-cut", HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.4f, true),
                ("head-burst", HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f, false),
                ("forearm-burst", HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.5f, false),
                ("body-burst", HumanBodyBones.Hips, HumanBodyBones.Neck, 0.6f, false),
            };
            float x = 0.0f;
            foreach ((string name, HumanBodyBones from, HumanBodyBones to, float share, bool edged) in cases)
            {
                GameObject body = Object.Instantiate(prefab, new Vector3(x, 0.05f, 0.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
                body.GetComponent<PlayerInputReader>().enabled = false;
                var locomotion = body.GetComponent<PlayerLocomotion>();
                var vitals = body.GetComponent<PlayerVitals>();
                vitals.Revive();
                for (int i = 0; i < 20; i++)
                {
                    locomotion.Tick(new LocomotionInput { YawDegrees = 180.0f }, Step);
                    yield return null;
                }
                Animator animator = body.GetComponentInChildren<Animator>();
                Vector3 at = Vector3.Lerp(animator.GetBoneTransform(from).position, animator.GetBoneTransform(to).position, share);
                vitals.TakeDamage(vitals.Health - 10.0f);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                using (DamageContext.Cutting(edged))
                {
                    DamageContext.Strike(vitals, 90.0f, at + new Vector3(1.0f, 0.0f, -1.5f), 0.0f, DamageKind.Physical, 1.0f, at);
                }
                Debug.Log($"CityX dismember capture: {name}: the blow took {watch.Elapsed.TotalMilliseconds:F1} ms");
                for (int i = 0; i < 9; i++)
                {
                    yield return null;
                }
                Vector3 look = body.transform.position + Vector3.up * 1.4f;
                yield return ArcaneOrbCapture.ShootFacing(camera, $"dismember-{name}", look + new Vector3(3.2f, 0.6f, -4.0f), look);
                for (float t = 0.0f; t < 2.5f; t += Step)
                {
                    yield return null;
                }
                yield return ArcaneOrbCapture.ShootFacing(camera, $"dismember-{name}-landed", look + new Vector3(2.5f, 2.5f, -3.5f), look + Vector3.down * 1.0f);
                if (!edged)
                {
                    Vector3 pieces = PiecesAround(body.transform.position);
                    yield return ArcaneOrbCapture.ShootFacing(camera, $"dismember-{name}-pieces", pieces + new Vector3(1.0f, 1.3f, -1.4f), pieces);
                }
                Debug.Log($"CityX dismember capture: {name}: {body.GetComponent<Dismemberer>().Last} {body.GetComponent<Dismemberer>().LastPart}");
                x += 12.0f;
            }

            // A corpse hacked at: killed bare, then two club blows on the head, a sword through the right arm, a shot at the left shin.
            GameObject corpse = Object.Instantiate(prefab, new Vector3(x, 0.05f, 0.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            corpse.GetComponent<PlayerInputReader>().enabled = false;
            var corpseVitals = corpse.GetComponent<PlayerVitals>();
            corpseVitals.Revive();
            for (int i = 0; i < 20; i++)
            {
                corpse.GetComponent<PlayerLocomotion>().Tick(new LocomotionInput { YawDegrees = 180.0f }, Step);
                yield return null;
            }
            corpseVitals.TakeDamage(corpseVitals.Health);
            for (float t = 0.0f; t < 1.5f; t += Step)
            {
                yield return null;
            }
            Animator corpseAnimator = corpse.GetComponentInChildren<Animator>();
            (HumanBodyBones from, HumanBodyBones to, float share, float damage, bool edged)[] blows =
            {
                (HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f, 12.0f, false),
                (HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f, 12.0f, false),
                (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.5f, 20.0f, true),
                (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.5f, 20.0f, false),
            };
            foreach ((HumanBodyBones from, HumanBodyBones to, float share, float damage, bool edged) in blows)
            {
                Vector3 at = Vector3.Lerp(corpseAnimator.GetBoneTransform(from).position, corpseAnimator.GetBoneTransform(to).position, share);
                using (DamageContext.Cutting(edged))
                {
                    DamageContext.Strike(corpseVitals, damage, at + Vector3.up * 1.5f, 0.0f, DamageKind.Physical, 1.0f, at);
                }
                for (float t = 0.0f; t < 0.3f; t += Step)
                {
                    yield return null;
                }
            }
            for (float t = 0.0f; t < 2.0f; t += Step)
            {
                yield return null;
            }
            Vector3 lying = corpseAnimator.GetBoneTransform(HumanBodyBones.Hips).position;
            yield return ArcaneOrbCapture.ShootFacing(camera, "dismember-corpse-hacked", lying + new Vector3(2.5f, 3.0f, -3.0f), lying);
            Debug.Log($"CityX dismember capture: corpse: head {corpse.GetComponent<Dismemberer>().Has(BodyPart.Head)}, " +
                      $"right arm {corpse.GetComponent<Dismemberer>().Has(BodyPart.RightUpperArm)}, left shin {corpse.GetComponent<Dismemberer>().Has(BodyPart.LeftLowerLeg)}");

            foreach (Debris debris in Object.FindObjectsByType<Debris>(FindObjectsSortMode.None))
            {
                Object.Destroy(debris.gameObject);
            }
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        /// <summary>The middle of the gore lying within a few metres of a body (each case's body stands 12 m from the next).</summary>
        private static Vector3 PiecesAround(Vector3 body)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (GoreChunk piece in Object.FindObjectsByType<GoreChunk>(FindObjectsSortMode.None))
            {
                Vector3 at = piece.transform.position;
                if (Mathf.Abs(at.x - body.x) < 5.5f && Mathf.Abs(at.z - body.z) < 5.5f)
                {
                    sum += at;
                    count++;
                }
            }
            return count > 0 ? sum / count : body;
        }
    }
}
