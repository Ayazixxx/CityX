using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// HAIR THAT BENDS AT THE NAPE (docs/HAIR.md; owner, 2026-11-04: "when walking around and doing certain animations the hair pokes through the shirt in the back", and of shortening
    /// it, "option 1 changes the fitting"). Hair is rigid on the head, and a style's lowest locks hang level with the shoulders, so a head tipped back drove their tips into the shirt.
    /// A style baked with these weights lets its lowest locks follow the neck, whose joint is level with them, so they stay put on the back while the top of the hair goes with the head.
    /// At rest the two agree exactly, so the style sits where its fit puts it. Pure, so the weights are tested without a scene.
    /// </summary>
    public static class HairBend
    {
        /// <summary>The two "bones": the placement on the head, and its copy on the neck (CharacterHair).</summary>
        public const int BoneCount = 2;

        /// <summary>From this far below the head's centre, metres, the hair starts to follow the neck...</summary>
        public const float StartsBelowHeadCentre = 0.1f;

        /// <summary>...and from this far below it follows the neck alone.</summary>
        public const float FullBelowHeadCentre = 0.26f;

        /// <summary>How much of a point on the hair follows the neck, 0 to 1, by its height in the head's space (the head bone is at the head's centre), as the fit places it.</summary>
        public static float NeckWeight(float heightFromHeadCentre)
        {
            return Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(-StartsBelowHeadCentre, -FullBelowHeadCentre, heightFromHeadCentre));
        }

        /// <summary>Gives a hair mesh its weights, placed by <paramref name="fit"/>. Both bind poses are the identity: both bones stand where the mesh is placed.</summary>
        public static void AddTo(Mesh mesh, in CharacterHair.Fit fit)
        {
            Matrix4x4 worn = Matrix4x4.TRS(fit.Position, Quaternion.Euler(fit.Euler), fit.Scale);
            Vector3[] vertices = mesh.vertices;
            var weights = new BoneWeight[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                float neck = NeckWeight(worn.MultiplyPoint3x4(vertices[i]).y);
                bool neckFirst = neck > 0.5f;
                weights[i] = new BoneWeight
                {
                    boneIndex0 = neckFirst ? 1 : 0,
                    weight0 = neckFirst ? neck : 1.0f - neck,
                    boneIndex1 = neckFirst ? 0 : 1,
                    weight1 = neckFirst ? 1.0f - neck : neck,
                };
            }
            mesh.boneWeights = weights;
            mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity };
        }
    }
}
