using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The get-up (owner, 2026-10-20: "make sure the hands arent clipping through the ground or any body parts for that matter"; 2026-11-10,
    /// "the feet are clipping through the sidewalk"): from lying on its back and on its front to standing, as the ragdoll plays it on level
    /// ground, no part of the body's skin goes under the floor, and once up its feet stand on it.
    /// </summary>
    public sealed class GetUpFloorPlayTests
    {
        /// <summary>How far under the floor the skin may reach: the flat of a hand or a sole resting on it.</summary>
        private const float Tolerance = 0.03f;

        /// <summary>The skin is measured every this many frames.</summary>
        private const int Every = 3;

        [UnityTest]
        public IEnumerator NoPartOfTheBodyGoesUnderTheFloorWhileGettingUp([Values(true, false)] bool onItsBack)
        {
#if UNITY_EDITOR
            GameObject ground = LyingBody.BuildGround("level");
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.3f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var baked = new Mesh();
            try
            {
                Ragdoll ragdoll = player.GetComponent<Ragdoll>();
                Animator animator = player.GetComponentInChildren<Animator>();
                var skin = player.GetComponentInChildren<SkinnedMeshRenderer>();
                yield return null;
                ragdoll.Activate(Vector3.zero, allowImmediateStandUp: true);
                yield return new WaitForFixedUpdate();
                LyingBody.LayDown(ragdoll, animator, onItsBack ? Vector3.up : Vector3.down);
                yield return new WaitForSeconds(2.5f);
                float floor = LyingBody.GroundUnder(animator.GetBoneTransform(HumanBodyBones.Hips).position, player.transform);

                ragdoll.BeginStandingUp(force: true);
                float worst = float.MaxValue, elapsed = 0.0f;
                string where = "";
                for (int frame = 0; ragdoll.IsLimp && elapsed < Ragdoll.StandUpRecoverySeconds + 1.0f; frame++)
                {
                    ragdoll.Tick(Time.deltaTime);
                    elapsed += Time.deltaTime;
                    yield return null;
                    if (elapsed < Ragdoll.SettleSeconds || frame % Every != 0)
                    {
                        continue;   // still easing out of the pose the ragdoll lay in
                    }
                    skin.BakeMesh(baked, true);
                    Vector3[] vertices = baked.vertices;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        float y = skin.transform.TransformPoint(vertices[v]).y - floor;
                        if (y < worst)
                        {
                            worst = y;
                            where = $"{skin.bones[skin.sharedMesh.boneWeights[v].boneIndex0].name} at {ragdoll.GetUpClipSeconds:0.00} s of {ragdoll.GetUpSegment}";
                        }
                    }
                }
                Assert.IsFalse(ragdoll.IsLimp, "it never got up");
                Assert.GreaterOrEqual(worst, -Tolerance, $"the skin went {-worst * 100.0f:0.0} cm under the floor: {where}");

                // Up, and handed back: the soles on the floor, not in it.
                for (int i = 0; i < 20; i++)
                {
                    yield return null;
                }
                skin.BakeMesh(baked, true);
                float lowest = float.MaxValue;
                foreach (Vector3 vertex in baked.vertices)
                {
                    lowest = Mathf.Min(lowest, skin.transform.TransformPoint(vertex).y - floor);
                }
                Assert.That(lowest, Is.InRange(-Tolerance, 0.06f), "standing, the feet are in the floor or over it");
            }
            finally
            {
                Object.Destroy(baked);
                Object.Destroy(player);
                Object.Destroy(ground);
            }
#else
            yield break;
#endif
        }
    }
}
