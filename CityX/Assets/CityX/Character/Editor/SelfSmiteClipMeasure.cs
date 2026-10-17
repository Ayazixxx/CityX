using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Measures selfsmite.fbx on the real rig: its length, and when its hands are thrown down (the fastest fall of the two hands), which is what
    /// SelfSmiteMove.ClipHandsDownSeconds and ClipSeconds hold so the bolt lands as the hands come down. Run it after replacing the clip
    /// (owner, 2026-10-17: a new SelfSmiteA.fbx) and copy the two numbers in.
    /// </summary>
    public static class SelfSmiteClipMeasure
    {
        [MenuItem("CityX/Measure Self Smite Clip")]
        public static void Measure()
        {
            AnimationClip clip = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/CityX/Character/Animations/selfsmite.fbx"))
            {
                if (asset is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    clip = c;
                    break;
                }
            }
            GameObject rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"));
            try
            {
                Animator animator = rig.GetComponentInChildren<Animator>();
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
                const float step = 1.0f / 120.0f;
                float fastest = 0.0f, fastestAt = 0.0f, last = 0.0f;
                for (float t = 0.0f; t <= clip.length; t += step)
                {
                    clip.SampleAnimation(animator.gameObject, t);
                    float height = 0.5f * (left.position.y + right.position.y) - rig.transform.position.y;
                    if (t > 0.0f)
                    {
                        float fall = (last - height) / step;
                        if (fall > fastest)
                        {
                            fastest = fall;
                            fastestAt = t;
                        }
                    }
                    last = height;
                    if (Mathf.Abs(t / 0.1f - Mathf.Round(t / 0.1f)) < 0.5f * step / 0.1f)
                    {
                        Debug.Log($"CityX self smite clip at {t:0.00} s: hands {height:0.00} m up");
                    }
                }
                Debug.Log($"CityX self smite clip: length {clip.length:0.000} s, hands thrown down fastest at {fastestAt:0.000} s ({fastest:0.0} m/s)");
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
