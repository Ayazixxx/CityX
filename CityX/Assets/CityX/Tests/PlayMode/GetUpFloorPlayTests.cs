using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The get-up (owner, 2026-10-20: "make sure the hands arent clipping through the ground or any body parts for that matter"): through the
    /// whole GettingUp clip, as the ragdoll plays it (LimbFloorClamp after the Animator), no part of the body's skin goes under the floor.
    /// </summary>
    public sealed class GetUpFloorPlayTests
    {
        /// <summary>How far under the floor the skin may reach: the flat of a hand or a sole resting on it.</summary>
        private const float Tolerance = 0.02f;

        private const int Samples = 40;

        [UnityTest]
        public IEnumerator NoPartOfTheBodyGoesUnderTheFloorWhileGettingUp()
        {
#if UNITY_EDITOR
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                Vector3.zero, Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.GetComponent<PlayerLocomotion>().enabled = false;
            var baked = new Mesh();
            try
            {
                yield return null;
                Animator animator = player.GetComponentInChildren<Animator>();
                var skin = player.GetComponentInChildren<SkinnedMeshRenderer>();
                float worst = float.MaxValue;
                string where = "";
                for (int i = 0; i <= Samples; i++)
                {
                    animator.Play("GettingUp", 0, i / (float)Samples * 0.999f);
                    animator.Update(0.0f);
                    yield return null;
                    LimbFloorClamp.Apply(animator, 0.0f);
                    skin.BakeMesh(baked, true);
                    Vector3[] vertices = baked.vertices;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        float y = skin.transform.TransformPoint(vertices[v]).y;
                        if (y < worst)
                        {
                            worst = y;
                            where = $"{skin.bones[skin.sharedMesh.boneWeights[v].boneIndex0].name} at {i / (float)Samples:0.00} of the clip";
                        }
                    }
                }
                Assert.GreaterOrEqual(worst, -Tolerance, $"the skin went {-worst * 100.0f:0.0} cm under the floor: {where}");
            }
            finally
            {
                Object.Destroy(baked);
                Object.Destroy(player);
            }
#else
            yield break;
#endif
        }
    }
}
