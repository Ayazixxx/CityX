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
    /// The hands on the wood plank (docs/WOOD_PLANK.md; the owner's rule, 2026-11-20: hands that hold, no fingers through a weapon): carried in both
    /// hands, each palm on the board and not in it, the fingers and the thumb in it no more than a fingertip's sinking, measured on the body's own
    /// skin as it is drawn against the board's box (WoodPlankShape).
    /// </summary>
    public sealed class WoodPlankGripPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        /// <summary>The fingertips may sink this far into the wood and still read as holding it (the can's allowance).</summary>
        private const float FingersMostSunk = 0.01f;

        /// <summary>The palm: on the wood, not in it.</summary>
        private const float PalmMostSunk = 0.006f;

        private struct Hand
        {
            public float Fingers, Thumb, Palm;
            public string Where;
        }

        [UnityTest]
        public IEnumerator CarriedBothHandsLieOnTheBoardAndNotInIt()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);
            SwordWeapon plank = null;
            try
            {
                body.GetComponent<PlayerInputReader>().enabled = false;
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/WoodPlank.prefab");
                Assert.IsNotNull(prefab, "no WoodPlank.prefab -- rebuild the scene");
                plank = Object.Instantiate(prefab, new Vector3(20.0f, 1.0f, 20.0f), Quaternion.identity).GetComponent<SwordWeapon>();
                plank.OnEquip(body, ItemInstanceData.None);
                var locomotion = body.GetComponent<PlayerLocomotion>();
                for (int i = 0; i < 60; i++)
                {
                    locomotion.Tick(new LocomotionInput(), Dt);
                    yield return null;
                }
                Hand right = Measure(body, plank.transform, "_r"), left = Measure(body, plank.transform, "_l");
                Animator animator = body.GetComponentInChildren<Animator>();
                Vector3 Palm(HumanBodyBones bone) => plank.transform.InverseTransformDirection(-animator.GetBoneTransform(bone).up);
                Vector3 At(HumanBodyBones bone) => plank.transform.InverseTransformPoint(animator.GetBoneTransform(bone).position);
                right.Where += $", palm facing {Palm(HumanBodyBones.RightHand):F2}, wrist at {At(HumanBodyBones.RightHand):F3}";
                left.Where += $", palm facing {Palm(HumanBodyBones.LeftHand):F2}, wrist at {At(HumanBodyBones.LeftHand):F3}";
                string report = $"right: fingers {right.Fingers * 100.0f:F1} cm in, thumb {right.Thumb * 100.0f:F1}, palm {right.Palm * 100.0f:F1}{right.Where}; "
                    + $"left: fingers {left.Fingers * 100.0f:F1} cm in, thumb {left.Thumb * 100.0f:F1}, palm {left.Palm * 100.0f:F1}{left.Where}";
                Debug.Log("PLANK GRIP " + report);
                TestContext.WriteLine(report);
                foreach (Hand hand in new[] { right, left })
                {
                    Assert.Less(hand.Fingers, FingersMostSunk, "fingers go into the plank. " + report);
                    Assert.Less(hand.Thumb, FingersMostSunk, "a thumb goes into the plank. " + report);
                    Assert.Less(hand.Palm, PalmMostSunk, "a palm goes into the plank. " + report);
                    Assert.Greater(hand.Palm, -0.02f, "a palm is not on the plank. " + report);
                }
#else
                Assert.Ignore("needs the editor to load the prefabs.");
                yield break;
#endif
            }
            finally
            {
                Object.Destroy(body);
                Object.Destroy(ground);
                if (plank != null) Object.Destroy(plank.gameObject);
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
        }

        /// <summary>How deep the hand's skin goes into the board (negative: how far off it), its fingers, its thumb and its palm.</summary>
        private static Hand Measure(GameObject body, Transform plank, string side)
        {
            var skin = TransformSearch.FindDeep(body.transform, BodySurface.SkinName).GetComponent<SkinnedMeshRenderer>();
            var posed = new Mesh();
            skin.BakeMesh(posed, true);
            Vector3[] points = posed.vertices;
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            Transform[] bones = skin.bones;
            var hand = new Hand { Fingers = -1.0f, Thumb = -1.0f, Palm = -1.0f, Where = string.Empty };
            for (int i = 0; i < points.Length; i++)
            {
                Transform bone = bones[weights[i].boneIndex0];
                string name = bone != null ? bone.name : string.Empty;
                if (!name.EndsWith(side))
                {
                    continue;
                }
                bool thumb = name.StartsWith("thumb");
                bool finger = name.StartsWith("index") || name.StartsWith("middle") || name.StartsWith("ring") || name.StartsWith("little");
                bool palm = name.StartsWith("hand");
                if (!thumb && !finger && !palm)
                {
                    continue;
                }
                Vector3 p = plank.InverseTransformPoint(skin.transform.TransformPoint(points[i]));
                float sunk = Mathf.Min(WoodPlankShape.Width * 0.5f - Mathf.Abs(p.x), WoodPlankShape.Thickness * 0.5f - Mathf.Abs(p.z),
                    p.y - WoodPlankShape.Bottom, WoodPlankShape.Top - p.y);
                if (finger && sunk > hand.Fingers)
                {
                    hand.Fingers = sunk;
                    hand.Where = $" (fingers deepest at x {p.x * 100.0f:F1} z {p.z * 100.0f:F1} cm, {name})";
                }
                if (thumb) hand.Thumb = Mathf.Max(hand.Thumb, sunk);
                if (palm) hand.Palm = Mathf.Max(hand.Palm, sunk);
            }
            Object.Destroy(posed);
            return hand;
        }
    }
}
