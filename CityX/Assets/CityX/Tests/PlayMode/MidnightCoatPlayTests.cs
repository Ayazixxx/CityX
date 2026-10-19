using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The Midnight Coat worn on the real player through a sprint (owner, 2026-10-19: "make it so the knee doesnt clip through the coat",
    /// "make it so the face and all parts of the body arent pocking through"): the body under it is hidden and given back, the head never
    /// sinks into the collar, and a knee coming forward stays behind the coat's front panel (the skirt bones, GarmentHelperBones).
    /// </summary>
    public sealed class MidnightCoatPlayTests
    {
        private const int StandFrames = 20;
        private const int SprintFrames = 170;

        /// <summary>How much deeper than standing the head may go into the collar's soft fur and still read as resting on it.</summary>
        private const float CollarSink = 0.02f;

        /// <summary>The knee's skin round its joint: the leg tapers from the hip's radius to the ankle's.</summary>
        private const float KneeRadius = (FusedBodyShape.LegRadius + FusedBodyShape.AnkleRadius) / 2.0f;

        /// <summary>How far the coat's soft lining may touch into the knee and still read as resting on it.</summary>
        private const float KneeTouch = 0.02f;

        /// <summary>How far round toward the body's front a knee must be for the panel in front of it to be checked.</summary>
        private const float KneeForward = 0.25f;

        private GameObject _player;
        private GameObject _coat;
        private PlayerLocomotion _locomotion;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "TestGround";
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            _coat = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/MidnightCoat.prefab"),
                new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
#endif
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Object.Destroy(_player);
            if (_coat != null)
            {
                Object.Destroy(_coat);
            }
            Object.Destroy(GameObject.Find("TestGround"));
        }

        [UnityTest]
        public IEnumerator TheCoatHidesTheBodyUnderItAndGivesItBackWhenTakenOff()
        {
            var skin = TransformSearch.FindDeep(_player.transform, "Skin").GetComponent<SkinnedMeshRenderer>();
            Mesh whole = skin.sharedMesh;
            var coat = _coat.GetComponent<MidnightCoat>();
            coat.OnEquip(_player, ItemInstanceData.None);
            yield return null;
            Assert.AreNotSame(whole, skin.sharedMesh, "the body under the coat is still drawn whole");
            Assert.Less(skin.sharedMesh.triangles.Length, whole.triangles.Length);
            Assert.Greater(skin.sharedMesh.triangles.Length, whole.triangles.Length / 2, "the coat hides more of the body than it covers");
            coat.OnUnequip(_player);
            _coat = null;
            yield return null;
            Assert.AreSame(whole, skin.sharedMesh, "taking the coat off did not give the whole body back");
        }

        [UnityTest]
        public IEnumerator TheHeadNeverSinksIntoTheCollarAndAKneeNeverComesThroughTheCoatInASprint()
        {
            _coat.GetComponent<MidnightCoat>().OnEquip(_player, ItemInstanceData.None);
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            var knees = new[] { (animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg)),
                (animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg)) };
            var worn = TransformSearch.FindDeep(_player.transform, "CombinedGarmentSkin").GetComponent<SkinnedMeshRenderer>();
            var baked = new Mesh();
            float[] standing = null;
            float sank = 0.0f;
            Vector3 where = Vector3.zero;
            int kneesForward = 0;
            float closestToKnee = float.MaxValue;
            var through = new List<string>();
            try
            {
                for (int frame = 0; frame < SprintFrames; frame++)
                {
                    _locomotion.Tick(new LocomotionInput { Move = frame > StandFrames ? Vector2.up : Vector2.zero, Sprint = frame > StandFrames },
                        1.0f / 60.0f);
                    yield return null;
                    worn.BakeMesh(baked, true);
                    Matrix4x4 toWorld = Matrix4x4.TRS(worn.transform.position, worn.transform.rotation, Vector3.one);
                    Vector3[] vertices = baked.vertices;
                    // The fur collar wraps up round the head's lower half as fitted: what counts is the head going deeper than standing.
                    if (frame == StandFrames)
                    {
                        standing = new float[vertices.Length];
                    }
                    for (int v = 0; standing != null && v < vertices.Length; v++)
                    {
                        float off = Vector3.Distance(toWorld.MultiplyPoint3x4(vertices[v]), head.position) - FusedBodyShape.HeadRadius;
                        if (frame == StandFrames)
                        {
                            standing[v] = off;
                        }
                        else
                        {
                            // How much deeper inside the head than standing (a point outside it is not in it at all).
                            float deeper = Mathf.Max(0.0f, -off) - Mathf.Max(0.0f, -standing[v]);
                            if (deeper > sank)
                            {
                                sank = deeper;
                                where = worn.sharedMesh.vertices[v];
                            }
                        }
                    }
                    // A knee coming forward against the coat: no part of the coat inside the knee (the open front may show it, that is
                    // the coat's design; cutting through a panel is what the skirt bones are for).
                    foreach ((Transform hip, Transform knee) in knees)
                    {
                        if (Vector3.Dot((knee.position - hip.position).normalized, _player.transform.forward) < KneeForward)
                        {
                            continue;
                        }
                        kneesForward++;
                        float nearest = float.MaxValue;
                        int at = 0;
                        for (int v = 0; v < vertices.Length; v++)
                        {
                            float distance = Vector3.Distance(toWorld.MultiplyPoint3x4(vertices[v]), knee.position);
                            if (distance < nearest)
                            {
                                nearest = distance;
                                at = v;
                            }
                        }
                        closestToKnee = Mathf.Min(closestToKnee, nearest);
                        if (nearest < KneeRadius - KneeTouch)
                        {
                            BoneWeight w = worn.sharedMesh.boneWeights[at];
                            through.Add($"frame {frame} {knee.name} {nearest * 100.0f:0.0} cm at {worn.sharedMesh.vertices[at]:F2} " +
                                $"({w.boneIndex0}:{w.weight0:F2} {w.boneIndex1}:{w.weight1:F2} {w.boneIndex2}:{w.weight2:F2})");
                        }
                    }
                }
            }
            finally
            {
                Object.Destroy(baked);
            }
            Debug.Log($"MidnightCoat sprint: the head went {sank * 100.0f:0.0} cm deeper into the collar than standing; a knee forward " +
                $"{kneesForward} times, the coat {closestToKnee * 100.0f:0.0} cm from its joint at the nearest");
            Assert.Less(sank, CollarSink, $"the head sank {sank * 100.0f:0.0} cm deeper into the coat than standing, at {where:F3} (bind pose)");
            Assert.Greater(kneesForward, 10, "the sprint never brought a knee forward: nothing was checked");
            Assert.IsEmpty(through, "a knee came through the coat: " + string.Join(", ", through));
        }
    }
}
