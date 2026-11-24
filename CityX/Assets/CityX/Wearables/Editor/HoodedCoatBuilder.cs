using CityX.Items;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the hooded coats (docs/BROWN_COAT.md; owner, 2026-11-24, with Meshy's two models of their coat, hood down and hood up: "put it into
    /// the game as a functional item called brown coat"): each colour is the coat twice, hood down (Models/&lt;Models&gt;.json) and hood up
    /// (Models/&lt;Models&gt;HoodUp.json), cut off Meshy's mannequin, reduced and skinned by SourceArt/BrownCoat/brown_coat_build.py, and its paint
    /// for each (&lt;Name&gt;.jpg, &lt;Name&gt;HoodUp.jpg). The chat's /hoodon swaps the one for the other (HoodedCoat). A colour of the same coat
    /// reuses its models and has its paint made here from theirs (<see cref="Colourway.Grey"/>: the Black Coat, from the Brown Coat's); a new
    /// colour is a line in <see cref="Colourways"/>, an item, and its lines where the Brown Coat is listed.
    /// </summary>
    public static class HoodedCoatBuilder
    {
        /// <summary>Plain woven cloth: matt.</summary>
        private const float FabricSmoothness = 0.1f;

        private const string Folder = "Assets/CityX/Wearables";

        /// <summary>A colour of the coat: the name its prefab and paint go by, its item, and whose models (and paint, to make its own from) it wears.</summary>
        public readonly struct Colourway
        {
            public readonly string Name;
            public readonly string ItemId;

            /// <summary>Whose models it is: its own name, or another colour's.</summary>
            public readonly string Models;

            /// <summary>
            /// Above 0, its paint is the models' own made grey and dark: each texel's lightness times this (the Black Coat: "just get rid of the
            /// color and make it darker"); 0, its own paint as Meshy made it.
            /// </summary>
            public readonly float Grey;

            public Colourway(string name, string itemId, string models = null, float grey = 0.0f)
            {
                Name = name;
                ItemId = itemId;
                Models = models ?? name;
                Grey = grey;
            }

            public string PrefabPath => $"{Folder}/{Name}.prefab";
        }

        public static readonly Colourway Brown = new Colourway("BrownCoat", ItemRegistry.BrownCoat);

        /// <summary>
        /// The Black Coat (owner, 2026-11-24: "make a black version of the brown coat. just get rid of the color and make it darker"): the Brown
        /// Coat's models, its paint without its colour, darkened to a very dark grey (the brown's lightness, about 0.34, to about 0.09; pure black
        /// loses the coat's folds to the outline).
        /// </summary>
        public static readonly Colourway Black = new Colourway("BlackCoat", ItemRegistry.BlackCoat, Brown.Name, 0.27f);

        public static readonly Colourway[] Colourways = { Brown, Black };

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
            string name = colourway.Name, models = colourway.Models;
            Mesh down = BlenderGarmentMesh.Bake($"{Folder}/Models/{models}.json", models + "Mesh", $"{Folder}/{models}Mesh.asset");
            Mesh up = BlenderGarmentMesh.Bake($"{Folder}/Models/{models}HoodUp.json", models + "HoodUpMesh", $"{Folder}/{models}HoodUpMesh.asset");
            if (down == null || up == null)
            {
                Debug.LogError($"CityX: no {models} model -- run SourceArt/BrownCoat/brown_coat_build.py.");
                return null;
            }
            if (colourway.Grey > 0.0f)
            {
                Greyed($"{Folder}/{models}.jpg", $"{Folder}/{name}.jpg", colourway.Grey);
                Greyed($"{Folder}/{models}HoodUp.jpg", $"{Folder}/{name}HoodUp.jpg", colourway.Grey);
            }
            Material material = BlenderGarmentMesh.TexturedMaterial($"{Folder}/{name}.mat", $"{Folder}/{name}.jpg", FabricSmoothness);
            Material hoodUp = BlenderGarmentMesh.TexturedMaterial($"{Folder}/{name}HoodUp.mat", $"{Folder}/{name}HoodUp.jpg", FabricSmoothness);
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

        /// <summary>The paint at <paramref name="from"/> without its colour, each texel's lightness times <paramref name="grey"/>, written to <paramref name="to"/>.</summary>
        private static void Greyed(string from, string to, float grey)
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(System.IO.File.ReadAllBytes(from));
            Color[] texels = source.GetPixels();
            for (int i = 0; i < texels.Length; i++)
            {
                float light = (0.2126f * texels[i].r + 0.7152f * texels[i].g + 0.0722f * texels[i].b) * grey;
                texels[i] = new Color(light, light, light, 1.0f);
            }
            source.SetPixels(texels);
            System.IO.File.WriteAllBytes(to, source.EncodeToJPG(90));
            Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(to);
        }
    }
}
