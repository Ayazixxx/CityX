using System.Collections;
using System.Text;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>Not a test: logs the get-up clip's hips and head height against its time, to see when the body is upright. Explicit.</summary>
    [Explicit("diagnostic")]
    public sealed class GetUpClipProbe
    {
        [UnityTest]
        public IEnumerator LogTheClip()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(50, 1, 50);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var player = Object.Instantiate(prefab, new Vector3(0, 0.1f, 0), Quaternion.identity);
            yield return null;
            Animator a = player.GetComponentInChildren<Animator>();
            a.GetComponent<Animator>().enabled = true;
            Transform hips = a.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = a.GetBoneTransform(HumanBodyBones.Head);
            Transform foot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            a.speed = 1.0f;
            a.Play("GettingUp", 0, 0.0f);
            var sb = new StringBuilder("GetUpClip (clip seconds: hips y, head y, foot y)\n");
            for (int i = 0; i <= 38; i++)
            {
                float t = i * 0.2f;
                a.Play("GettingUp", 0, t / IntroCutscene.GettingUpClipSeconds);
                a.Update(0.0f);
                sb.Append($"{t:0.0}s  hips {hips.position.y - player.transform.position.y:0.00}  head {head.position.y - player.transform.position.y:0.00}  foot {foot.position.y - player.transform.position.y:0.00}\n");
            }
            Debug.Log(sb.ToString());
            Object.Destroy(player);
            Object.Destroy(ground);
#else
            yield break;
#endif
        }
    }
}
