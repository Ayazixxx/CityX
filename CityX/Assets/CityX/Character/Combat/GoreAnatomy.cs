using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What is inside a body, for the gore a burst throws out with the pieces of its skin (docs/DISMEMBERMENT.md): broken long bones with a knob
    /// at one end and red marrow at the other, a side of ribs on a bit of spine, vertebrae, a jaw with its teeth, eyes, the heart, a lung, the
    /// liver, brain, a coil of gut, pieces of skull, teeth and lumps of meat. Games whose gore reads as people throw these (Half-Life's skull,
    /// ribs, heart and gut; Fallout's eyeballs; Left 4 Dead's bone out of a limb). Sizes are for the player's 2.6 m body and scale with it; eyes and
    /// teeth are drawn bigger than life so they read from a few metres. Each shape is built once, closed all round, its submeshes the tissues
    /// in <see cref="TissuesOf"/>.
    /// </summary>
    public static class GoreAnatomy
    {
        public enum Bit
        {
            Femur,
            Shin,
            Humerus,
            Forearm,
            RibSide,
            Spine,
            Jaw,
            Eye,
            Heart,
            Lung,
            Liver,
            Brain,
            Guts,
            SkullShard,
            Tooth,
            Meat,
        }

        /// <summary>What a submesh is made of: its colour and how wet it shines (<see cref="Gore"/> makes a material for each).</summary>
        public enum Tissue
        {
            Meat,
            DarkMeat,
            Fat,
            Bone,
            Marrow,
            Tooth,
            Lung,
            Heart,
            Vessel,
            Liver,
            Brain,
            Guts,
            EyeWhite,
            Iris,
            Pupil,
        }

        /// <summary>How many shapes of the bits that come in more than one (meat, gut, brain, skull).</summary>
        public const int Variants = 3;

        /// <summary>The long bones: length, shaft radius and knob radius, m (the thigh's, the shin's, the upper arm's, the forearm's).</summary>
        public const float FemurLength = 0.34f, FemurShaft = 0.026f, FemurKnob = 0.05f;

        public const float ShinLength = 0.3f, ShinShaft = 0.024f, ShinKnob = 0.045f;
        public const float HumerusLength = 0.26f, HumerusShaft = 0.02f, HumerusKnob = 0.04f;
        public const float ForearmLength = 0.22f, ForearmShaft = 0.016f, ForearmKnob = 0.031f;

        /// <summary>An eye's radius, m: twice life size, so it reads from a few metres.</summary>
        public const float EyeRadius = 0.032f;

        private static readonly Dictionary<(Bit, int), Mesh> Made = new Dictionary<(Bit, int), Mesh>();

        /// <summary>The tissue of each of a bit's submeshes, in order.</summary>
        public static Tissue[] TissuesOf(Bit bit)
        {
            switch (bit)
            {
                case Bit.Femur:
                case Bit.Shin:
                case Bit.Humerus:
                case Bit.Forearm:
                    return new[] { Tissue.Bone, Tissue.Marrow };
                case Bit.RibSide:
                case Bit.Spine:
                    return new[] { Tissue.Bone, Tissue.Meat };
                case Bit.Jaw:
                    return new[] { Tissue.Bone, Tissue.Tooth };
                case Bit.Eye:
                    return new[] { Tissue.EyeWhite, Tissue.Iris, Tissue.Pupil, Tissue.Meat };
                case Bit.Heart:
                    return new[] { Tissue.Heart, Tissue.Vessel };
                case Bit.Lung:
                    return new[] { Tissue.Lung, Tissue.Fat };
                case Bit.Liver:
                    return new[] { Tissue.Liver };
                case Bit.Brain:
                    return new[] { Tissue.Brain };
                case Bit.Guts:
                    return new[] { Tissue.Guts };
                case Bit.SkullShard:
                    return new[] { Tissue.Bone, Tissue.DarkMeat };
                case Bit.Tooth:
                    return new[] { Tissue.Tooth };
                default:
                    return new[] { Tissue.Meat };
            }
        }

        /// <summary>The long bone of a limb part: the thigh's femur, the shin's, the upper arm's humerus, the forearm's.</summary>
        public static Bit BoneOf(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.LeftUpperLeg:
                case BodyPart.RightUpperLeg:
                    return Bit.Femur;
                case BodyPart.LeftLowerLeg:
                case BodyPart.RightLowerLeg:
                    return Bit.Shin;
                case BodyPart.LeftUpperArm:
                case BodyPart.RightUpperArm:
                    return Bit.Humerus;
                default:
                    return Bit.Forearm;
            }
        }

        /// <summary>
        /// What a burst of <paramref name="part"/> throws besides the pieces of its skin: a head its jaw, both eyes, brain, skull and teeth; a
        /// limb part its broken bone and a lump of meat; the torso (the whole body) a side of ribs, the spine, the heart, both lungs, the liver,
        /// gut and meat.
        /// </summary>
        public static List<Bit> BitsOf(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head:
                    return new List<Bit> { Bit.Jaw, Bit.Eye, Bit.Eye, Bit.Brain, Bit.Brain, Bit.SkullShard, Bit.SkullShard, Bit.Tooth, Bit.Tooth, Bit.Tooth };
                case BodyPart.Torso:
                    return new List<Bit> { Bit.RibSide, Bit.RibSide, Bit.Spine, Bit.Heart, Bit.Lung, Bit.Lung, Bit.Liver, Bit.Guts, Bit.Guts, Bit.Meat, Bit.Meat, Bit.Meat };
                default:
                    return new List<Bit> { BoneOf(part), Bit.Meat };
            }
        }

        /// <summary>A bit's shape (built the first time, then shared: never destroy it), its middle at its origin.</summary>
        public static Mesh MeshOf(Bit bit, int variant = 0)
        {
            variant = HasVariants(bit) ? Mathf.Abs(variant) % Variants : 0;
            if (Made.TryGetValue((bit, variant), out Mesh mesh) && mesh != null)
            {
                return mesh;
            }
            var random = new System.Random(1000 * (int)bit + variant + 17);
            var into = new GoreMeshes.Builder(TissuesOf(bit).Length);
            switch (bit)
            {
                case Bit.Femur:
                    LongBone(into, FemurLength, FemurShaft, FemurKnob, random);
                    break;
                case Bit.Shin:
                    LongBone(into, ShinLength, ShinShaft, ShinKnob, random);
                    break;
                case Bit.Humerus:
                    LongBone(into, HumerusLength, HumerusShaft, HumerusKnob, random);
                    break;
                case Bit.Forearm:
                    LongBone(into, ForearmLength, ForearmShaft, ForearmKnob, random);
                    break;
                case Bit.RibSide:
                    RibSide(into);
                    break;
                case Bit.Spine:
                    Spine(into);
                    break;
                case Bit.Jaw:
                    Jaw(into);
                    break;
                case Bit.Eye:
                    Eye(into);
                    break;
                case Bit.Heart:
                    Heart(into);
                    break;
                case Bit.Lung:
                    GoreMeshes.Ball(into, 0, Vector3.zero, new Vector3(0.08f, 0.13f, 0.055f), 0.15f, 3.5f, 4.0f, 10, 14);
                    GoreMeshes.Tube(into, 1, new[] { new Vector3(0.0f, 0.11f, 0.0f), new Vector3(-0.02f, 0.17f, 0.0f) }, 0.014f, 0.8f, 6);
                    break;
                case Bit.Liver:
                    GoreMeshes.Ball(into, 0, Vector3.zero, new Vector3(0.12f, 0.05f, 0.08f), 0.1f, 1.5f, 9.0f, 8, 14);
                    break;
                case Bit.Brain:
                    GoreMeshes.Ball(into, 0, Vector3.zero, new Vector3(0.07f, 0.045f, 0.06f), 0.18f, 7.0f, 3.0f + variant * 5.0f, 12, 18);
                    break;
                case Bit.Guts:
                    Guts(into, variant);
                    break;
                case Bit.SkullShard:
                    SkullShard(into, random);
                    break;
                case Bit.Tooth:
                    GoreMeshes.Box(into, 0, Vector3.zero, new Vector3(0.016f, 0.024f, 0.014f), Quaternion.identity);
                    break;
                default:
                    GoreMeshes.Ball(into, 0, Vector3.zero, new Vector3(0.07f, 0.055f, 0.06f), 0.3f, 2.3f, 7.0f + variant * 11.0f, 8, 12);
                    break;
            }
            into.Recentre();
            mesh = into.ToMesh($"Gore{bit}{variant}");
            Made[(bit, variant)] = mesh;
            return mesh;
        }

        private static bool HasVariants(Bit bit) => bit == Bit.Meat || bit == Bit.Guts || bit == Bit.Brain || bit == Bit.SkullShard;

        /// <summary>A broken long bone along Y: a round knob at the bottom, the shaft, a jagged break at the top showing the marrow.</summary>
        private static void LongBone(GoreMeshes.Builder into, float length, float shaft, float knob, System.Random random)
        {
            var rings = new List<(float, float, float)>
            {
                (0.0f, knob * 0.55f, 0.0f),
                (length * 0.04f, knob, 0.0f),
                (length * 0.09f, knob * 1.05f, 0.0f),
                (length * 0.15f, knob * 0.75f, 0.0f),
                (length * 0.24f, shaft * 1.25f, 0.0f),
                (length * 0.35f, shaft, 0.0f),
                (length * 0.8f, shaft, 0.0f),
                (length * 0.93f, shaft * 1.05f, 0.0f),
                (length, shaft * 1.1f, 0.5f),
            };
            GoreMeshes.Lathe(into, 0, rings, 8, 0, 1, random);
        }

        /// <summary>One side of a rib cage: four ribs curving out from a bit of spine and broken off at the front, a little meat between them.</summary>
        private static void RibSide(GoreMeshes.Builder into)
        {
            const float height = 0.3f;
            GoreMeshes.Tube(into, 0, new[] { Vector3.zero, new Vector3(0.0f, height, 0.0f) }, 0.022f, 1.0f, 6);
            float[] radii = { 0.13f, 0.16f, 0.17f, 0.16f };
            for (int r = 0; r < radii.Length; r++)
            {
                float y = 0.04f + r * 0.07f;
                GoreMeshes.Ball(into, 0, new Vector3(0.0f, y, 0.0f), new Vector3(0.03f, 0.025f, 0.03f), 0.0f, 1.0f, 0.0f, 5, 8);
                var arc = new List<Vector3>();
                for (int i = 0; i <= 8; i++)
                {
                    float angle = 0.75f * Mathf.PI * i / 8.0f;
                    arc.Add(new Vector3(radii[r] * Mathf.Sin(angle), y - 0.05f * angle / Mathf.PI, radii[r] * (1.0f - Mathf.Cos(angle))));
                }
                GoreMeshes.Tube(into, 0, arc, 0.012f, 0.8f, 5);
            }
            GoreMeshes.Ball(into, 1, new Vector3(0.05f, 0.08f, 0.02f), new Vector3(0.04f, 0.045f, 0.025f), 0.25f, 3.0f, 2.0f, 6, 10);
            GoreMeshes.Ball(into, 1, new Vector3(0.06f, 0.2f, 0.03f), new Vector3(0.035f, 0.04f, 0.025f), 0.25f, 3.0f, 5.0f, 6, 10);
        }

        /// <summary>Four vertebrae, each a round body with a spike behind and a wing either side, red discs between them.</summary>
        private static void Spine(GoreMeshes.Builder into)
        {
            for (int i = 0; i < 4; i++)
            {
                float y = i * 0.055f;
                GoreMeshes.Tube(into, 0, new[] { new Vector3(0.0f, y, 0.0f), new Vector3(0.0f, y + 0.04f, 0.0f) }, 0.032f, 1.0f, 8);
                GoreMeshes.Box(into, 0, new Vector3(0.0f, y + 0.02f, -0.05f), new Vector3(0.018f, 0.022f, 0.05f), Quaternion.Euler(-20.0f, 0.0f, 0.0f));
                GoreMeshes.Box(into, 0, new Vector3(0.042f, y + 0.02f, -0.012f), new Vector3(0.045f, 0.012f, 0.015f), Quaternion.identity);
                GoreMeshes.Box(into, 0, new Vector3(-0.042f, y + 0.02f, -0.012f), new Vector3(0.045f, 0.012f, 0.015f), Quaternion.identity);
                if (i < 3)
                {
                    GoreMeshes.Tube(into, 1, new[] { new Vector3(0.0f, y + 0.04f, 0.0f), new Vector3(0.0f, y + 0.055f, 0.0f) }, 0.029f, 1.0f, 8);
                }
            }
        }

        /// <summary>A lower jaw: a U of bone opening backwards, rising at both ends, with a row of teeth along the front.</summary>
        private static void Jaw(GoreMeshes.Builder into)
        {
            var path = new List<Vector3>
            {
                new Vector3(-0.075f, 0.06f, -0.015f),
                new Vector3(-0.078f, 0.03f, -0.01f),
            };
            for (int i = 0; i <= 10; i++)
            {
                float t = -1.0f + i * 0.2f;
                path.Add(new Vector3(0.075f * t, 0.0f, 0.065f * (1.0f - t * t)));
            }
            path.Add(new Vector3(0.078f, 0.03f, -0.01f));
            path.Add(new Vector3(0.075f, 0.06f, -0.015f));
            GoreMeshes.Tube(into, 0, path, 0.016f, 1.0f, 6);
            for (int i = 0; i < 8; i++)
            {
                float t = -0.7f + i * 0.2f;
                var at = new Vector3(0.075f * t, 0.02f, 0.065f * (1.0f - t * t));
                GoreMeshes.Box(into, 1, at, new Vector3(0.013f, 0.018f, 0.011f), Quaternion.Euler(0.0f, Mathf.Atan2(at.x, at.z) * Mathf.Rad2Deg, 0.0f));
            }
        }

        /// <summary>An eye looking along +Z: white, a ring of iris, the pupil, and the red stalk out of the back.</summary>
        private static void Eye(GoreMeshes.Builder into)
        {
            const float radius = EyeRadius;
            GoreMeshes.Ball(into, 0, Vector3.zero, Vector3.one * radius, 0.0f, 1.0f, 0.0f, 10, 16, d => d.z > 0.95f ? 2 : d.z > 0.78f ? 1 : 0);
            GoreMeshes.Tube(into, 3, new[] { new Vector3(0.0f, 0.0f, -radius * 0.8f), new Vector3(0.0f, -0.01f, -radius - 0.03f), new Vector3(0.006f, -0.02f, -radius - 0.05f) },
                0.008f, 0.7f, 6);
        }

        /// <summary>The heart: a dark lump, its big vessel arching over the top.</summary>
        private static void Heart(GoreMeshes.Builder into)
        {
            GoreMeshes.Ball(into, 0, Vector3.zero, new Vector3(0.07f, 0.085f, 0.06f), 0.12f, 2.5f, 1.0f, 10, 14);
            GoreMeshes.Tube(into, 1, new[]
            {
                new Vector3(0.0f, 0.06f, 0.0f), new Vector3(0.0f, 0.11f, 0.01f), new Vector3(0.03f, 0.135f, 0.0f), new Vector3(0.06f, 0.12f, -0.01f),
                new Vector3(0.07f, 0.09f, -0.01f),
            }, 0.018f, 0.85f, 7);
        }

        /// <summary>A coil of gut, heaped on itself.</summary>
        private static void Guts(GoreMeshes.Builder into, int variant)
        {
            var path = new List<Vector3>();
            float seed = variant * 1.7f;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * 0.55f;
                float reach = 0.05f + 0.03f * Mathf.Sin(i * 0.9f + seed);
                path.Add(new Vector3(Mathf.Cos(angle) * reach, 0.012f * i * 0.5f + 0.02f * Mathf.Sin(i * 1.3f + seed), Mathf.Sin(angle) * reach));
            }
            GoreMeshes.Tube(into, 0, path, 0.024f, 0.9f, 6);
        }

        /// <summary>A piece of skull: a curved plate of bone with a jagged edge, dark meat on its inner face.</summary>
        private static void SkullShard(GoreMeshes.Builder into, System.Random random)
        {
            const float radius = 0.2f, thickness = 0.014f;
            const int sides = 10, rings = 4;
            var reach = new float[sides];
            for (int i = 0; i < sides; i++)
            {
                reach[i] = 0.32f * (0.7f + 0.3f * (float)random.NextDouble());
            }
            var outer = new int[rings + 1, sides];
            var inner = new int[rings + 1, sides];
            var outerPoints = new Vector3[sides];
            var innerPoints = new Vector3[sides];
            for (int j = 0; j <= rings; j++)
            {
                for (int i = 0; i < sides; i++)
                {
                    float polar = reach[i] * j / rings, azimuth = Mathf.PI * 2.0f * i / sides;
                    var d = new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    outer[j, i] = into.Add(d * radius, d, Vector2.zero);
                    inner[j, i] = into.Add(d * (radius - thickness), -d, Vector2.zero);
                    if (j == rings)
                    {
                        outerPoints[i] = d * radius;
                        innerPoints[i] = d * (radius - thickness);
                    }
                }
            }
            for (int j = 0; j < rings; j++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int k = (i + 1) % sides;
                    if (j > 0)
                    {
                        into.Triangle(0, outer[j, i], outer[j, k], outer[j + 1, k]);
                        into.Triangle(1, inner[j, i], inner[j + 1, k], inner[j, k]);
                    }
                    into.Triangle(0, outer[j, i], outer[j + 1, k], outer[j + 1, i]);
                    into.Triangle(1, inner[j, i], inner[j + 1, i], inner[j + 1, k]);
                }
            }
            for (int i = 0; i < sides; i++)
            {
                int k = (i + 1) % sides;
                Vector3 normal = Vector3.Cross(outerPoints[k] - outerPoints[i], innerPoints[k] - outerPoints[i]).normalized;
                int a = into.Add(outerPoints[i], normal, Vector2.zero), b = into.Add(outerPoints[k], normal, Vector2.zero);
                int c = into.Add(innerPoints[k], normal, Vector2.zero), d = into.Add(innerPoints[i], normal, Vector2.zero);
                into.Triangle(0, a, b, c);
                into.Triangle(0, a, c, d);
            }
        }
    }
}
