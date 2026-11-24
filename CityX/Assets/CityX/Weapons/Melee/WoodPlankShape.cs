using System.Collections.Generic;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE WOOD PLANK'S SHAPE (docs/WOOD_PLANK.md; owner, 2026-11-23: "a wood plank weapon, just a wood plank like a 2x4 (maybe not that exact length
    /// ingame but one that looks like it) ... make it about the length of the metal pole maybe a tad less", and of a cartoon plank: "add the chip at
    /// the top of it"). A long board, flat faced, a V-shaped chip cut into its top end through its thickness, in the greatsword's frame: the root
    /// where the upper hand holds, the board up +Y, its width across X (the swing leads with a broad face: -X) and its thickness along Z, the way
    /// the palm lies on it. Its faces are mapped onto WoodPlank.png: the broad faces on its upper board, the narrow sides, the ends and the chip's
    /// faces on its lower one, the grain running along the plank at the texture's own scale.
    /// </summary>
    public static class WoodPlankShape
    {
        /// <summary>End to end, metres on the 2.6 m body: a tad short of the bo staff's 2.2 m pole (StaffRules), as asked.</summary>
        public const float Length = 2.0f;

        /// <summary>Across its broad faces and through them: a 2x4's look (3.5 by 1.5 inches, 2.3 to 1) at a size the fists close on.</summary>
        public const float Width = 0.10f;
        public const float Thickness = 0.045f;

        /// <summary>The bottom end, below the root: the lower hand just above it (as the baseball bat's knob), the upper hand the support grip's drop above that.</summary>
        public const float BottomHandAboveEnd = 0.11f;
        public static float Bottom => -(BottomHandAboveEnd + SwordWeapon.DefaultSupportGripDown);
        public static float Top => Bottom + Length;

        /// <summary>
        /// The left palm on the board, off the shared support point (SwordWeapon.SupportPalmOffset): the left hand comes at it from the far side and
        /// lies turned onto its -X edge (measured: WoodPlankGripPlayTests), so its palm's middle stands that far out past the edge.
        /// </summary>
        public const float LeftPalmStandOff = 0.048f;
        public static Vector3 SupportPalmOffset => new Vector3(-(Width * 0.5f + LeftPalmStandOff), 0.0f, 0.0f);

        /// <summary>The chip: a V into the top end, its middle this share of the width across from -X, this wide at the end and this deep.</summary>
        public const float ChipAcross = 0.62f;
        public const float ChipOpening = 0.016f;
        public const float ChipDepth = 0.035f;

        /// <summary>The texture's boards are 16 times as long as they are wide (1024 by 64); a face takes the board across its width.</summary>
        public const float TextureBoardAspect = 16.0f;

        /// <summary>The texture's two boards, as V ranges, kept half a texel off where they meet so neither bleeds into the other.</summary>
        public const float HalfTexel = 0.5f / 128.0f;
        public static readonly Vector2 BroadBoard = new Vector2(0.5f + HalfTexel, 1.0f - HalfTexel);
        public static readonly Vector2 NarrowBoard = new Vector2(HalfTexel, 0.5f - HalfTexel);

        /// <summary>The outline in the board's plane (X across, Y along), going round anticlockwise seen from +Z, the chip in its top edge.</summary>
        public static Vector2[] Outline()
        {
            float half = Width * 0.5f, chip = -half + Width * ChipAcross;
            return new[]
            {
                new Vector2(-half, Bottom), new Vector2(half, Bottom), new Vector2(half, Top), new Vector2(chip + ChipOpening * 0.5f, Top),
                new Vector2(chip, Top - ChipDepth), new Vector2(chip - ChipOpening * 0.5f, Top), new Vector2(-half, Top),
            };
        }

        /// <summary>The board's mesh: flat faces (each its own vertices), normals out, mapped onto the texture.</summary>
        public static void Build(out Vector3[] vertices, out Vector3[] normals, out Vector2[] uv, out int[] triangles)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<Vector2>();
            var tri = new List<int>();
            Vector2[] outline = Outline();
            float halfT = Thickness * 0.5f;

            // The broad faces: the outline below the chip's point is one piece, either side of the chip two more.
            Vector2 chipPoint = outline[4];
            var below = new[] { outline[0], outline[1], new Vector2(outline[1].x, chipPoint.y), chipPoint, new Vector2(outline[0].x, chipPoint.y) };
            var left = new[] { new Vector2(outline[0].x, chipPoint.y), chipPoint, outline[5], outline[6] };
            var right = new[] { chipPoint, new Vector2(outline[1].x, chipPoint.y), outline[2], outline[3] };
            foreach (float side in new[] { 1.0f, -1.0f })
            {
                var normal = new Vector3(0.0f, 0.0f, side);
                foreach (Vector2[] piece in new[] { below, left, right })
                {
                    int first = v.Count;
                    foreach (Vector2 p in piece)
                    {
                        v.Add(new Vector3(p.x, p.y, side * halfT));
                        n.Add(normal);
                        t.Add(Mapped(p.y, p.x + Width * 0.5f, Width, BroadBoard));
                    }
                    for (int i = 1; i + 1 < piece.Length; i++)
                    {
                        AddTriangle(v, tri, first, first + i, first + i + 1, normal);
                    }
                }
            }

            // Round the outline: the narrow sides, the ends and the chip's two faces, each a band through the thickness.
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                Vector2 along = (b - a).normalized;
                var normal = new Vector3(along.y, -along.x, 0.0f);
                int first = v.Count;
                bool lengthwise = Mathf.Abs(along.y) > 0.9f;
                foreach ((Vector2 p, float z) in new[] { (a, -halfT), (b, -halfT), (b, halfT), (a, halfT) })
                {
                    v.Add(new Vector3(p.x, p.y, z));
                    n.Add(normal);
                    // The grain runs along the plank: a lengthwise face takes it along its length, an end or a chip face across.
                    float run = lengthwise ? p.y : Vector2.Dot(p, along);
                    t.Add(Mapped(run, z + halfT, Thickness, NarrowBoard));
                }
                AddTriangle(v, tri, first, first + 1, first + 2, normal);
                AddTriangle(v, tri, first, first + 2, first + 3, normal);
            }
            vertices = v.ToArray();
            normals = n.ToArray();
            uv = t.ToArray();
            triangles = tri.ToArray();
        }

        /// <summary>A point on a face <paramref name="across"/> metres wide onto one of the texture's boards: along it at its own scale, across it the board's height.</summary>
        private static Vector2 Mapped(float along, float acrossFromEdge, float across, Vector2 board) =>
            new Vector2(along / (across * TextureBoardAspect), Mathf.Lerp(board.x, board.y, acrossFromEdge / across));

        /// <summary>A triangle wound so its front faces <paramref name="normal"/> (Unity: clockwise seen from the front).</summary>
        private static void AddTriangle(List<Vector3> v, List<int> tri, int a, int b, int c, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), normal) < 0.0f)
            {
                (b, c) = (c, b);
            }
            tri.Add(a);
            tri.Add(b);
            tri.Add(c);
        }
    }
}
