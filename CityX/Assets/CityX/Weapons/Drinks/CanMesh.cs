using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A DRINKS CAN (docs/PHIZZ_CAN.md): a real 12 oz can standing on its base, in metres, its outline in three turned parts (the bare metal
    /// foot, the printed body, the bare metal neck and lid) and its pull tab, a flat plate with a finger hole riveted to the lid's middle.
    /// Pure: <see cref="BottleMesh"/> turns the outlines; the builder colours, prints and saves them.
    /// </summary>
    public static class CanMesh
    {
        /// <summary>The can's radius and height (a standard 12 oz can: 66 mm across, 122 mm tall).</summary>
        public const float Radius = 0.033f;
        public const float Height = 0.1222f;

        /// <summary>Where the printed body begins and ends, above the base.</summary>
        public const float BodyFrom = 0.012f;
        public const float BodyTo = 0.106f;

        /// <summary>The lid's height (sunk below the rolled rim) and the rim's inner radius.</summary>
        public const float LidHeight = 0.1185f;
        public const float LidRadius = 0.0256f;

        /// <summary>The foot: the domed base out to the ring it stands on, then up the taper to the body.</summary>
        public static readonly Vector2[] Foot =
        {
            new Vector2(0.000f, 0.0100f), new Vector2(0.012f, 0.0090f), new Vector2(0.0215f, 0.0050f), new Vector2(0.0245f, 0.0004f),
            new Vector2(0.0262f, 0.0000f), new Vector2(0.0280f, 0.0012f), new Vector2(0.0315f, 0.0065f), new Vector2(Radius, 0.0105f),
            new Vector2(Radius, BodyFrom),
        };

        /// <summary>The body, straight, where the print goes.</summary>
        public static readonly Vector2[] Body =
        {
            new Vector2(Radius, BodyFrom), new Vector2(Radius, BodyFrom + 0.001f), new Vector2(Radius, BodyTo - 0.001f), new Vector2(Radius, BodyTo),
        };

        /// <summary>The neck tapering in, the rolled rim, its inside wall down to the lid and the lid in to the middle.</summary>
        public static readonly Vector2[] Top =
        {
            new Vector2(Radius, BodyTo), new Vector2(Radius, BodyTo + 0.0015f), new Vector2(0.0312f, 0.1115f), new Vector2(0.0285f, 0.1165f),
            new Vector2(0.0274f, 0.1195f), new Vector2(0.0276f, 0.1212f), new Vector2(0.0270f, Height), new Vector2(0.0261f, 0.1216f),
            new Vector2(LidRadius, 0.1196f), new Vector2(0.0248f, LidHeight), new Vector2(0.0200f, LidHeight), new Vector2(0.000f, LidHeight),
        };

        /// <summary>The whole outline, foot, body and neck, bottom to top: what a picture printed past the body's ends follows.</summary>
        public static Vector2[] Outline
        {
            get
            {
                var outline = new System.Collections.Generic.List<Vector2>(Foot);
                outline.AddRange(Body);
                outline.AddRange(Top);
                return outline.ToArray();
            }
        }

        /// <summary>The rivet holding the tab: a low dome on the lid's middle.</summary>
        public static readonly Vector2[] Rivet =
        {
            new Vector2(0.0032f, LidHeight), new Vector2(0.0030f, LidHeight + 0.0009f), new Vector2(0.0018f, LidHeight + 0.0022f), new Vector2(0.000f, LidHeight + 0.0024f),
        };

        /// <summary>The tab: its length from the rivet's end to the finger end, its width, thickness, and how far it rides above the lid.</summary>
        public const float TabLength = 0.0205f;
        public const float TabWidth = 0.0125f;
        public const float TabThickness = 0.0007f;
        public const float TabLift = 0.0010f;

        /// <summary>How far past the rivet the tab's near end reaches (the tab lies across the lid's middle, its finger end toward the rim).</summary>
        public const float TabBehindRivet = 0.0045f;

        /// <summary>The finger hole: its centre along the tab from the rivet, and its half length and half width.</summary>
        public const float HoleAlong = 0.0095f;
        public const float HoleHalfLength = 0.0045f;
        public const float HoleHalfWidth = 0.0038f;

        /// <summary>The tab's free end from its hinge at the rivet (on top of it): where a finger pinches it.</summary>
        public static Vector3 TabEndFromHinge => new Vector3(TabLength - TabBehindRivet, TabLift + TabThickness, 0.0f);

        /// <summary>Points round the tab's outline and its hole.</summary>
        public const int TabSteps = 32;

        /// <summary>
        /// The pull tab, lying on the lid along +X from the rivet: a rounded plate (a stadium) with a rounded finger hole toward its far end, a top,
        /// an underside and the walls round its edge and its hole. Each outline point is where a ray from the hole's middle crosses that outline,
        /// so the plate between them is one band that never folds over.
        /// </summary>
        public static BottleMesh.Model Tab()
        {
            float half = TabWidth * 0.5f;
            return Ring(new Vector2(HoleAlong, 0.0f), -TabBehindRivet + half, TabLength - TabBehindRivet - half, half,
                HoleAlong - HoleHalfLength + HoleHalfWidth, HoleAlong + HoleHalfLength - HoleHalfWidth, HoleHalfWidth, LidHeight + TabLift, TabThickness);
        }

        /// <summary>The opening's scored outline on the lid, on the other side of the rivet from the tab's finger end: where it would open.</summary>
        public const float OpeningNear = -0.0085f;
        public const float OpeningFar = -0.0140f;
        public const float OpeningHalfWidth = 0.0050f;

        /// <summary>The scored line's width and how far it stands up off the lid.</summary>
        public const float ScoreWidth = 0.0008f;
        public const float ScoreHeight = 0.0004f;

        /// <summary>The opening's outline, a thin raised loop on the lid (a can's mouth is pressed into its lid before it is ever opened).</summary>
        public static BottleMesh.Model Opening()
        {
            float halfScore = ScoreWidth * 0.5f;
            return Ring(new Vector2((OpeningNear + OpeningFar) * 0.5f, 0.0f), OpeningFar, OpeningNear, OpeningHalfWidth + halfScore,
                OpeningFar, OpeningNear, OpeningHalfWidth - halfScore, LidHeight, ScoreHeight);
        }

        /// <summary>How far the opened hole's black stands off the lid, inside the scored line.</summary>
        public const float HoleProud = 0.0002f;

        /// <summary>The opened hole: the black inside the scored line, a plate the shape of the opening (shown once the tab has cracked the lid).</summary>
        public static BottleMesh.Model Hole()
        {
            float middle = (OpeningNear + OpeningFar) * 0.5f;
            return Ring(new Vector2(middle, 0.0f), OpeningFar, OpeningNear, OpeningHalfWidth - ScoreWidth * 0.5f, middle, middle, 0.00005f, LidHeight, HoleProud);
        }

        /// <summary>
        /// A flat ring lying on the lid between two stadiums (each the segment from (a, 0) to (b, 0) with a radius round it), from
        /// <paramref name="bottom"/> up <paramref name="thickness"/>: a top, an underside and the walls round both edges. Each outline point is where a ray
        /// from <paramref name="middle"/> crosses that outline, so the plate between them is one band that never folds over.
        /// </summary>
        private static BottleMesh.Model Ring(Vector2 middle, float outerA, float outerB, float outerRadius, float innerA, float innerB, float innerRadius,
            float bottom, float thickness)
        {
            var model = new BottleMesh.Model();
            var outer = new Vector2[TabSteps];
            var inner = new Vector2[TabSteps];
            for (int i = 0; i < TabSteps; i++)
            {
                float angle = i * Mathf.PI * 2.0f / TabSteps;
                var way = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                outer[i] = middle + way * ReachInside(middle, way, outerA, outerB, outerRadius);
                inner[i] = middle + way * ReachInside(middle, way, innerA, innerB, innerRadius);
            }
            float top = bottom + thickness;
            Band(model, outer, inner, top, Vector3.up);
            Band(model, inner, outer, bottom, Vector3.down);
            Wall(model, outer, bottom, top, 1.0f, middle);
            Wall(model, inner, bottom, top, -1.0f, middle);
            return model;
        }

        /// <summary>How far from <paramref name="from"/> along <paramref name="way"/> a stadium (the segment from (a, 0) to (b, 0), <paramref name="radius"/> round it) ends.</summary>
        public static float ReachInside(Vector2 from, Vector2 way, float a, float b, float radius)
        {
            float lo = 0.0f, hi = 0.1f;
            for (int i = 0; i < 40; i++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector2 p = from + way * mid;
                float along = Mathf.Clamp(p.x, a, b);
                bool inside = (p - new Vector2(along, 0.0f)).magnitude <= radius;
                if (inside)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }
            return lo;
        }

        /// <summary>A flat band between two outlines at <paramref name="height"/>, facing <paramref name="facing"/>.</summary>
        private static void Band(BottleMesh.Model model, Vector2[] a, Vector2[] b, float height, Vector3 facing)
        {
            int start = model.Vertices.Count;
            for (int i = 0; i < a.Length; i++)
            {
                model.Vertices.Add(new Vector3(a[i].x, height, a[i].y));
                model.Normals.Add(facing);
                model.Vertices.Add(new Vector3(b[i].x, height, b[i].y));
                model.Normals.Add(facing);
            }
            for (int i = 0; i < a.Length; i++)
            {
                int j = (i + 1) % a.Length;
                Facing(model, start + i * 2, start + j * 2, start + j * 2 + 1, facing);
                Facing(model, start + i * 2, start + j * 2 + 1, start + i * 2 + 1, facing);
            }
        }

        /// <summary>An upright wall round an outline, facing away from <paramref name="middle"/> (<paramref name="outward"/> 1) or toward it (-1).</summary>
        private static void Wall(BottleMesh.Model model, Vector2[] outline, float bottom, float top, float outward, Vector2 middle)
        {
            int start = model.Vertices.Count;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 away = (outline[i] - middle).normalized * outward;
                var normal = new Vector3(away.x, 0.0f, away.y);
                model.Vertices.Add(new Vector3(outline[i].x, bottom, outline[i].y));
                model.Normals.Add(normal);
                model.Vertices.Add(new Vector3(outline[i].x, top, outline[i].y));
                model.Normals.Add(normal);
            }
            for (int i = 0; i < outline.Length; i++)
            {
                int j = (i + 1) % outline.Length;
                Vector3 normal = model.Normals[start + i * 2];
                Facing(model, start + i * 2, start + j * 2, start + j * 2 + 1, normal);
                Facing(model, start + i * 2, start + j * 2 + 1, start + i * 2 + 1, normal);
            }
        }

        /// <summary>A triangle wound to face <paramref name="facing"/>.</summary>
        private static void Facing(BottleMesh.Model model, int a, int b, int c, Vector3 facing)
        {
            Vector3 face = Vector3.Cross(model.Vertices[b] - model.Vertices[a], model.Vertices[c] - model.Vertices[a]);
            if (face.sqrMagnitude < 1e-16f)
            {
                return;
            }
            bool along = Vector3.Dot(face, facing) >= 0.0f;
            model.Triangles.Add(a);
            model.Triangles.Add(along ? b : c);
            model.Triangles.Add(along ? c : b);
        }
    }
}
