using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the prefab a fireball spawns where it bursts (docs/FIRE.md, "The fire spells"): its flash of light, decayed by FireBurst.Update
    /// on a cubic (weapons/explosion.gd's OmniLight3D gave the reference for it). The fire, the sparks, the cinders, the smoke and the scorch
    /// are the fire's shared look, which FireBurst.Spawn calls on, not particles of its own.
    /// </summary>
    public static class FireBurstBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/FireBurst.prefab";

        /// <summary>The flash at its brightest and its reach, metres: the street round the burst lit for a moment, softly.</summary>
        public const float FlashIntensity = 12.0f;

        public const float FlashRange = 7.0f;

        [MenuItem("CityX/Rebuild Fire Burst")]
        public static GameObject Build()
        {
            var root = new GameObject("FireBurst");
            try
            {
                var flashObject = new GameObject("Flash");
                flashObject.transform.SetParent(root.transform, false);
                flashObject.transform.localPosition = Vector3.forward * 0.4f;   // off the surface it hit (+Z faces out of it)
                Light flash = flashObject.AddComponent<Light>();
                flash.type = LightType.Point;
                flash.color = new Color(1.0f, 0.6f, 0.25f);
                flash.intensity = FlashIntensity;
                flash.range = FlashRange;
                flash.shadows = LightShadows.None;

                root.AddComponent<FireBurst>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: fire burst written to {PrefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
