using System.Collections;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The hand round a can of PHIZZ (owner, 2026-11-23: "thumb wrapped around for grip no not clipping into the can, maybe a very slight amount of the
    /// finger tips can clip through as long as it reads as him gripping it"): carried, the palm on the can, the thumb lying on it and not in it, the
    /// fingers in it no more than a centimetre, measured on the body's own skin as it is drawn against the can's outline.
    /// </summary>
    public sealed class CanGripPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        /// <summary>The fingertips may sink this far into the can and still read as gripping it.</summary>
        private const float FingersMostSunk = 0.01f;

        /// <summary>The thumb and the palm: on the can, not in it.</summary>
        private const float ThumbMostSunk = 0.004f;

        /// <summary>The thumb lies on the can: its tip no further off it than this.</summary>
        private const float ThumbMostOff = 0.012f;

        [UnityTest]
        public IEnumerator CarriedTheHandHoldsTheCanWithTheThumbOnItAndOnlyTheFingertipsIn()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);
            try
            {
                body.GetComponent<PlayerInputReader>().enabled = false;
                var can = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Cans/phizz_can.prefab"),
                    new Vector3(20.0f, 1.0f, 20.0f), Quaternion.identity).GetComponent<PhizzCan>();
                can.OnEquip(body, ItemInstanceData.None);
                var locomotion = body.GetComponent<PlayerLocomotion>();
                for (int i = 0; i < 50; i++)
                {
                    locomotion.Tick(new LocomotionInput(), Dt);
                    yield return null;
                }
                Assert.Greater(locomotion.RightGrip, 0.9f, "the hand has not closed on the can yet");
                Assert.Greater(locomotion.RaisedCarryWeight, 0.9f, "the can is not carried raised");

                var skin = TransformSearch.FindDeep(body.transform, BodySurface.SkinName).GetComponent<SkinnedMeshRenderer>();
                var posed = new Mesh();
                skin.BakeMesh(posed, true);
                Vector3[] points = posed.vertices;
                BoneWeight[] weights = skin.sharedMesh.boneWeights;
                Transform[] bones = skin.bones;
                float thumbIn = -1.0f, fingersIn = -1.0f, palmIn = -1.0f, thumbTipOff = 1.0f;
                for (int i = 0; i < points.Length; i++)
                {
                    Transform bone = bones[weights[i].boneIndex0];
                    string name = bone != null ? bone.name : string.Empty;
                    if (!name.EndsWith("_r"))
                    {
                        continue;
                    }
                    bool thumb = name.StartsWith("thumb");
                    bool finger = name.StartsWith("index") || name.StartsWith("middle") || name.StartsWith("ring") || name.StartsWith("little");
                    bool palm = name == "hand_r";
                    if (!thumb && !finger && !palm)
                    {
                        continue;
                    }
                    Vector3 inCan = can.transform.InverseTransformPoint(skin.transform.TransformPoint(points[i]));
                    float height = inCan.y / BottleShape.LifeScale;
                    float radius = height < 0.0f || height > CanMesh.Height ? 0.0f : BottleMesh.RadiusAt(CanMesh.Outline, height, 0.004f) * BottleShape.LifeScale;
                    float sunk = radius - new Vector2(inCan.x, inCan.z).magnitude;
                    if (thumb)
                    {
                        thumbIn = Mathf.Max(thumbIn, sunk);
                        if (name.StartsWith("thumb2"))
                        {
                            thumbTipOff = Mathf.Min(thumbTipOff, -sunk);
                        }
                    }
                    fingersIn = finger ? Mathf.Max(fingersIn, sunk) : fingersIn;
                    palmIn = palm ? Mathf.Max(palmIn, sunk) : palmIn;
                }
                Object.Destroy(posed);
                string report = $"thumb {thumbIn * 100.0f:F1} cm in (its tip {thumbTipOff * 100.0f:F1} cm off), fingers {fingersIn * 100.0f:F1} cm in, palm {palmIn * 100.0f:F1} cm in";
                Debug.Log("CAN GRIP: " + report);
                Assert.Less(thumbIn, ThumbMostSunk, "the thumb goes into the can. " + report);
                Assert.Less(thumbTipOff, ThumbMostOff, "the thumb is not round the can. " + report);
                Assert.Less(palmIn, ThumbMostSunk, "the palm goes into the can. " + report);
                Assert.Less(fingersIn, FingersMostSunk, "the fingers go into the can. " + report);
                Assert.Greater(palmIn, -0.01f, "the palm is not on the can. " + report);
#else
                Assert.Ignore("needs the editor to load the prefabs.");
                yield break;
#endif
            }
            finally
            {
                Object.Destroy(body);
                Object.Destroy(ground);
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
        }
    }
}
