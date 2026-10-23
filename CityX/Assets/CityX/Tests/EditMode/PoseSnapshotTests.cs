using CityX.Character;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The HUD portrait's pose, taken at load, is put on any later body of the same rig (a respawn, the networked body that appears when
    /// hosting), not that body's pose of the moment (owner, 2026-10-23: a hand at the face after taking out the combat sword).
    /// </summary>
    public sealed class PoseSnapshotTests
    {
        [Test]
        public void APoseTakenFromOneBodyIsPutOnAnotherByName()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject first = Object.Instantiate(prefab);
            GameObject second = Object.Instantiate(prefab, new Vector3(10.0f, 0.0f, 0.0f), Quaternion.identity);
            try
            {
                Animator a = first.GetComponentInChildren<Animator>(), b = second.GetComponentInChildren<Animator>();
                PoseSnapshot atLoad = PoseSnapshot.Of(a.transform);
                Vector3 hand = first.transform.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.RightHand).position);

                b.GetBoneTransform(HumanBodyBones.RightLowerArm).localRotation *= Quaternion.Euler(0.0f, 0.0f, 120.0f);   // a hand raised
                atLoad.ApplyTo(b.transform);
                Vector3 secondHand = second.transform.InverseTransformPoint(b.GetBoneTransform(HumanBodyBones.RightHand).position);
                Assert.Less(Vector3.Distance(hand, secondHand), 1e-4f, "the second body does not stand as the first did");
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }
    }
}
