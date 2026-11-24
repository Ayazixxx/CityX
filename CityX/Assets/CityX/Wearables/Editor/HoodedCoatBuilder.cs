using CityX.Items;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the hooded coats (docs/BROWN_COAT.md; owner, 2026-11-24, with Meshy's two models of their coat, hood down and hood up: "put it into
    /// the game as a functional item called brown coat"): each colour is the coat twice, hood down (Models/&lt;Name&gt;.json, &lt;Name&gt;.jpg) and hood
    /// up (Models/&lt;Name&gt;HoodUp.json, &lt;Name&gt;HoodUp.jpg), cut off Meshy's mannequin, reduced and skinned by
    /// SourceArt/BrownCoat/brown_coat_build.py. The chat's /hoodon swaps the one for the other (HoodedCoat). A new colour is its two models and
    /// maps, a line in <see cref="Colourways"/>, an item, and its lines where the Brown Coat is listed.
    /// </summary>
    public static class HoodedCoatBuilder
    {
        /// <summary>Plain woven cloth: matt.</summary>
        private const float FabricSmoothness = 0.1f;

        /// <summary>A colour of the coat: the name its prefab, models and maps go by, and its item.</summary>
        public readonly struct Colourway
        {
            public readonly string Name;
            public readonly string ItemId;

            public Colourway(string name, string itemId)
            {
                Name = name;
                ItemId = itemId;
            }

            public string PrefabPath => $"Assets/CityX/Wearables/{Name}.prefab";
        }

        public static readonly Colourway Brown = new Colourway("BrownCoat", ItemRegistry.BrownCoat);

        public static readonly Colourway[] Colourways = { Brown };

        [MenuItem("CityX/Rebuild Hooded Coats")]
        public static void BuildAll()
        {
            foreach (Colourway colourway in Colourways)
            {
                Build(colourway);
            }
        }

        public static GameObject Build(Colourway colourway)
        {
            string name = colourway.Name, folder = "Assets/CityX/Wearables";
            Mesh down = BlenderGarmentMesh.Bake($"{folder}/Models/{name}.json", name + "Mesh", $"{folder}/{name}Mesh.asset");
            Mesh up = BlenderGarmentMesh.Bake($"{folder}/Models/{name}HoodUp.json", name + "HoodUpMesh", $"{folder}/{name}HoodUpMesh.asset");
            if (down == null || up == null)
            {
                Debug.LogError($"CityX: no {name} model -- run SourceArt/BrownCoat/brown_coat_build.py.");
                return null;
            }
            Material material = BlenderGarmentMesh.TexturedMaterial($"{folder}/{name}.mat", $"{folder}/{name}.jpg", FabricSmoothness);
            Material hoodUp = BlenderGarmentMesh.TexturedMaterial($"{folder}/{name}HoodUp.mat", $"{folder}/{name}HoodUp.jpg", FabricSmoothness);
            byte[] hidden = LongCoatBody.Hidden(down, out int bodyTriangles);
            GameObject saved = SkinnedGarmentBuilder.BuildPrefabFromMesh<HoodedCoat>(
                down, material, colourway.PrefabPath, name, LongCoatBody.GroundScale, LongCoatBody.GroundPitchDegrees, LongCoatBody.GroundLift,
                coat =>
                {
                    coat.SetItemId(colourway.ItemId);
                    coat.HoodUpMesh = up;
                    coat.HoodUpMaterial = hoodUp;
                    coat.HiddenBodyTriangles = hidden;
                    coat.HiddenFromBodyTriangles = bodyTriangles;
                });
            if (saved != null)
            {
                Debug.Log($"CityX: {name} written to {colourway.PrefabPath} -- {down.triangles.Length / 3} triangles hood down, {up.triangles.Length / 3} hood up");
            }
            return saved;
        }
    }
}
