using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The machete in the hand (docs/MACHETE.md; owner, 2026-11-23, of the machete held at its butt with its rivets showing: "make the grip further
    /// down the hand, make the second silver dot covered by the grip"): on the real body, with the one-handed hold as tuned, the fist closes on the
    /// middle of the three rivets.
    /// </summary>
    public sealed class MachetePlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        /// <summary>The fist's middle this near the middle rivet, along the handle, metres.</summary>
        private const float OnTheRivet = 0.015f;

        [UnityTest]
        public IEnumerator TheFistClosesOnTheMiddleRivet()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);
            SwordWeapon machete = null;
            try
            {
                body.GetComponent<PlayerInputReader>().enabled = false;
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Machete.prefab");
                Assert.IsNotNull(prefab, "no Machete.prefab -- rebuild the scene");
                machete = Object.Instantiate(prefab, new Vector3(20.0f, 1.0f, 20.0f), Quaternion.identity).GetComponent<SwordWeapon>();
                machete.OnEquip(body, ItemInstanceData.None);
                var locomotion = body.GetComponent<PlayerLocomotion>();
                for (int i = 0; i < 50; i++)
                {
                    locomotion.Tick(new LocomotionInput(), Dt);
                    yield return null;
                }

                Transform hand = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
                float fist = machete.transform.InverseTransformPoint(hand.TransformPoint(SwordWeapon.FistPoint)).y;
                List<float> rivets = RivetHeights(machete.transform.Find("Rivets"));
                TestContext.WriteLine($"the fist's middle at {fist:0.000} m along the machete, its rivets at {string.Join(", ", rivets.ConvertAll(r => r.ToString("0.000")))}");
                Assert.AreEqual(3, rivets.Count, "not three rivets down the handle");
                Assert.AreEqual(rivets[1], fist, OnTheRivet, "the fist is not on the middle rivet");
#else
                Assert.Ignore("needs the editor to load the prefabs.");
                yield break;
#endif
            }
            finally
            {
                Object.Destroy(body);
                Object.Destroy(ground);
                if (machete != null) Object.Destroy(machete.gameObject);
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
        }

        /// <summary>The rivets' middles along the machete (its own space), from the butt up: the rivets' points in bunches down the handle.</summary>
        private static List<float> RivetHeights(Transform rivets)
        {
            var heights = new List<float>();
            foreach (Vector3 v in rivets.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                heights.Add(rivets.localPosition.y + v.y);
            }
            heights.Sort();
            var middles = new List<float>();
            float sum = 0.0f;
            int count = 0;
            for (int i = 0; i < heights.Count; i++)
            {
                if (i > 0 && heights[i] - heights[i - 1] > 0.02f)
                {
                    middles.Add(sum / count);
                    sum = 0.0f;
                    count = 0;
                }
                sum += heights[i];
                count++;
            }
            middles.Add(sum / count);
            return middles;
        }
    }
}
