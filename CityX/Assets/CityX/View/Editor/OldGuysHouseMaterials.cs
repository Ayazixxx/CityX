using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// The tutorial house's window glass (docs/OLD_GUYS_HOUSE.md): a faint blue that the room and the garden show through, so the daylight and
    /// the view come in. The city's glass is dark and solid from outside; a house the player walks through needs to be seen out of. An asset, so a
    /// build keeps the shader variant.
    /// </summary>
    public static class OldGuysHouseMaterials
    {
        public const string GlassPath = "Assets/CityX/View/Materials/HouseGlass.mat";

        public static readonly Color GlassColour = new Color(0.78f, 0.90f, 0.96f, 0.22f);

        public static Material EnsureGlass()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    Debug.LogWarning("CityX: no URP particle shader for the house's glass.");
                    return null;
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, GlassPath);
            }
            SeeThroughMaterial.Apply(material, false);
            material.SetColor("_BaseColor", GlassColour);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
