using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A tall tower's top (World/TowerTop): a communications mast, a spire on a drum, the stepped crown of an art-deco tower with its spire, a
    /// spire leaning at one corner, or a crown of fins round the roof's edge; rising from the flat roof inside the parapet, built of boxes and
    /// beams (a skyline is read from far off, so near and far get the same, less the small bars). The ground a top's base takes is handed back
    /// so the roof plant keeps off it; where it stands was decided off the stair's hatch by TowerTop. The skyline tier draws a stub of it on
    /// its box, so a far tower keeps its silhouette.
    /// </summary>
    public static partial class CityMeshBuilder
    {
        private static readonly Color32 TopSteel = SurfaceLook.Pack(new Color(0.36f, 0.38f, 0.41f), (int)WallMaterial.Metal, 0.4f);

        private static readonly Color32 TopPale = SurfaceLook.Pack(new Color(0.80f, 0.81f, 0.83f), (int)WallMaterial.Metal, 0.2f);

        /// <summary>The beacon's glow in the lit glass: red, on a powered building at night, nothing by day.</summary>
        private static readonly Color32 BeaconGlow = SurfaceLook.PackFloodlit(new Color(1.0f, 0.10f, 0.06f));

        /// <summary>The skyline tier's look for a top: pale metal, so a far spire reads against the sky as a real one does.</summary>
        private static readonly Color32 SkylineTop = SurfaceLook.Pack(new Color(0.72f, 0.74f, 0.77f), (int)WallMaterial.Metal, 0.3f);

        private const float FinLength = 2.4f;

        private const float FinThickness = 0.22f;

        private const float FinEvery = 6.0f;

        /// <summary>The tower's top on its roof; returns the ground its base takes (the plant keeps off it), in the roof's frame.</summary>
        private static Rect AddTowerTop(MeshBuffer buffer, MeshBuffer lit, in TowerTop top, float x0, float x1, float z0, float z1, float roofY,
            SiteLooks looks, bool far)
        {
            float cx = (x0 + x1) * 0.5f + top.X, cz = (z0 + z1) * 0.5f + top.Z;
            float size = top.Size;
            Color32 saved = buffer.Look;
            Rect ground = size > 0.0f
                ? Rect.MinMaxRect(cx - size * 0.5f - 0.6f, cz - size * 0.5f - 0.6f, cx + size * 0.5f + 0.6f, cz + size * 0.5f + 0.6f)
                : Rect.zero;
            switch (top.Kind)
            {
                case TowerTopKind.Mast:
                    AddMastTop(buffer, lit, cx, cz, roofY, top.Height, size, looks, far);
                    break;
                case TowerTopKind.Spire:
                    AddSpireTop(buffer, cx, cz, roofY, top.Height, size, looks, far, false);
                    break;
                case TowerTopKind.SteppedSpire:
                    AddSpireTop(buffer, cx, cz, roofY, top.Height, size, looks, far, true);
                    break;
                case TowerTopKind.SlantedSpire:
                    // Leaning in over the roof from its corner.
                    AddSlantedSpireTop(buffer, cx, cz, roofY, top.Height, size, -Mathf.Sign(top.X), -Mathf.Sign(top.Z), looks);
                    break;
                case TowerTopKind.Fins:
                    AddFinsTop(buffer, x0, x1, z0, z1, roofY, top.Height, looks);
                    break;
            }
            buffer.Look = saved;
            return ground;
        }

        /// <summary>A communications mast: a platform, a lattice of four legs tapering in over the lower two thirds, a pole, two cross bars and the beacon.</summary>
        private static void AddMastTop(MeshBuffer buffer, MeshBuffer lit, float cx, float cz, float roofY, float h, float size, SiteLooks looks, bool far)
        {
            buffer.Look = looks.Trim;
            AddCentredBox(buffer, cx, cz, roofY, size, 1.0f, size);
            buffer.Look = TopSteel;
            float lattice = h * 0.62f;
            int segments = Mathf.Clamp(Mathf.RoundToInt(lattice / 6.0f), 2, 8);
            float spread = size * 0.36f;
            float yBase = roofY + 1.0f;
            for (int s = 0; s < segments; s++)
            {
                float yA = yBase + lattice * s / segments, yB = yBase + lattice * (s + 1) / segments;
                float sA = spread * (1.0f - 0.85f * s / segments), sB = spread * (1.0f - 0.85f * (s + 1) / segments);
                for (int leg = 0; leg < 4; leg++)
                {
                    float lx = (leg & 1) == 0 ? -1.0f : 1.0f, lz = leg < 2 ? -1.0f : 1.0f;
                    CityProps.AddBeam(buffer, new Vector3(cx + lx * sA, yA, cz + lz * sA), new Vector3(cx + lx * sB, yB, cz + lz * sB), 0.08f);
                }
                if (!far)
                {
                    // A brace ring: two thin bars crossing at the segment's top.
                    AddCentredBox(buffer, cx, cz, yB - 0.1f, sB * 2.0f + 0.16f, 0.12f, 0.12f);
                    AddCentredBox(buffer, cx, cz, yB - 0.1f, 0.12f, 0.12f, sB * 2.0f + 0.16f);
                }
            }
            float poleY = yBase + lattice;
            AddCentredBox(buffer, cx, cz, poleY - 0.2f, 0.26f, roofY + h - poleY + 0.1f, 0.26f);
            AddCentredBox(buffer, cx, cz, roofY + h * 0.78f, 1.6f, 0.12f, 0.12f);
            AddCentredBox(buffer, cx, cz, roofY + h * 0.9f, 0.12f, 0.12f, 1.1f);
            // The beacon at the tip: its housing, and on a powered building a red glow in the lit glass round it.
            AddCentredBox(buffer, cx, cz, roofY + h - 0.1f, 0.5f, 0.45f, 0.5f);
            if (lit != null)
            {
                Color32 litSaved = lit.Look;
                lit.Look = BeaconGlow;
                AddCentredBox(lit, cx, cz, roofY + h - 0.12f, 0.56f, 0.5f, 0.56f);
                lit.Look = litSaved;
            }
        }

        /// <summary>A spire: a drum (or the three setback tiers of a stepped crown) and a needle of eight boxes tapering to the tip.</summary>
        private static void AddSpireTop(MeshBuffer buffer, float cx, float cz, float roofY, float h, float size, SiteLooks looks, bool far, bool stepped)
        {
            float y = roofY;
            float needleBase;
            if (stepped)
            {
                // Three setback tiers in the wall's own look, each narrower and shorter, a band of trim on each: the art-deco crown.
                float tiers = h * 0.4f, s = size;
                for (int tier = 0; tier < 3; tier++)
                {
                    float tall = tiers * (0.45f - 0.1f * tier);
                    buffer.Look = looks.Wall;
                    AddCentredBox(buffer, cx, cz, y, s, tall, s);
                    buffer.Look = looks.Trim;
                    AddCentredBox(buffer, cx, cz, y + tall, s + 0.3f, 0.25f, s + 0.3f);
                    y += tall + 0.25f;
                    s *= 0.72f;
                }
                needleBase = Mathf.Max(0.8f, s * 0.55f);
            }
            else
            {
                buffer.Look = looks.Trim;
                AddCentredBox(buffer, cx, cz, y, size, 2.6f, size);
                y += 2.6f;
                needleBase = Mathf.Max(0.9f, size * 0.5f);
            }
            buffer.Look = TopPale;
            float needle = Mathf.Max(1.0f, roofY + h - y);
            const int parts = 8;
            for (int p = 0; p < parts; p++)
            {
                float s = needleBase * (1.0f - 0.88f * p / parts);
                AddCentredBox(buffer, cx, cz, y + needle * p / parts, s, needle / parts + 0.05f, s);
            }
            if (!far)
            {
                // A collar of cross bars a third of the way up.
                buffer.Look = TopSteel;
                AddCentredBox(buffer, cx, cz, y + needle * 0.35f, needleBase * 1.6f, 0.12f, 0.12f);
                AddCentredBox(buffer, cx, cz, y + needle * 0.35f, 0.12f, 0.12f, needleBase * 1.6f);
            }
        }

        /// <summary>A spire at a corner of the roof, leaning eight degrees toward (<paramref name="sx"/>, <paramref name="sz"/>): each box of the needle a little further over.</summary>
        private static void AddSlantedSpireTop(MeshBuffer buffer, float cx, float cz, float roofY, float h, float size, float sx, float sz, SiteLooks looks)
        {
            buffer.Look = looks.Trim;
            AddCentredBox(buffer, cx, cz, roofY, size, 2.0f, size);
            buffer.Look = TopPale;
            float needle = Mathf.Max(1.0f, h - 2.0f);
            float lean = Mathf.Tan(8.0f * Mathf.Deg2Rad) * 0.7071f;   // the lean split between the two axes, toward the corner's opposite
            const int parts = 10;
            for (int p = 0; p < parts; p++)
            {
                float y = roofY + 2.0f + needle * p / parts;
                float along = needle * (p + 0.5f) / parts * lean;
                float s = Mathf.Max(0.25f, size * 0.5f * (1.0f - 0.9f * p / parts));
                AddCentredBox(buffer, cx + sx * along, cz + sz * along, y, s, needle / parts + 0.05f, s);
            }
        }

        /// <summary>A crown of fins round the roof's edge, inside the parapet, every six metres or so with a pair meeting at each corner, closed by a ring at the top.</summary>
        private static void AddFinsTop(MeshBuffer buffer, float x0, float x1, float z0, float z1, float roofY, float h, SiteLooks looks)
        {
            float ins = ParapetWallThickness + 0.6f;
            float fx0 = x0 + ins, fx1 = x1 - ins, fz0 = z0 + ins, fz1 = z1 - ins;
            float tall = h - 0.4f;
            buffer.Look = looks.Trim;
            int nx = Mathf.Max(1, Mathf.RoundToInt((fx1 - fx0) / FinEvery)), nz = Mathf.Max(1, Mathf.RoundToInt((fz1 - fz0) / FinEvery));
            for (int i = 0; i <= nx; i++)
            {
                float fx = i == 0 ? fx0 + FinLength * 0.5f : (i == nx ? fx1 - FinLength * 0.5f : Mathf.Lerp(fx0, fx1, (float)i / nx));
                AddCentredBox(buffer, fx, fz0, roofY, FinLength, tall, FinThickness);
                AddCentredBox(buffer, fx, fz1, roofY, FinLength, tall, FinThickness);
            }
            for (int j = 0; j <= nz; j++)
            {
                float fz = j == 0 ? fz0 + FinLength * 0.5f : (j == nz ? fz1 - FinLength * 0.5f : Mathf.Lerp(fz0, fz1, (float)j / nz));
                AddCentredBox(buffer, fx0, fz, roofY, FinThickness, tall, FinLength);
                AddCentredBox(buffer, fx1, fz, roofY, FinThickness, tall, FinLength);
            }
            float ringY = roofY + tall;
            buffer.AddBox(new Vector3(fx0 - 0.15f, ringY, fz0 - 0.15f), new Vector3(fx1 + 0.15f, ringY + 0.4f, fz0 + 0.15f));
            buffer.AddBox(new Vector3(fx0 - 0.15f, ringY, fz1 - 0.15f), new Vector3(fx1 + 0.15f, ringY + 0.4f, fz1 + 0.15f));
            buffer.AddBox(new Vector3(fx0 - 0.15f, ringY, fz0 + 0.15f), new Vector3(fx0 + 0.15f, ringY + 0.4f, fz1 - 0.15f));
            buffer.AddBox(new Vector3(fx1 - 0.15f, ringY, fz0 + 0.15f), new Vector3(fx1 + 0.15f, ringY + 0.4f, fz1 - 0.15f));
        }

        /// <summary>
        /// A tower's top in the skyline tier, on its box (whose top is <paramref name="roofY"/>, in the building's centred frame): a mast as a pole,
        /// a spire or crown as a few tapering boxes, the fins as four slabs, so a far tower keeps its silhouette.
        /// </summary>
        private static void AddSkylineTop(MeshBuffer buffer, in TowerTop top, float roofY, float width, float depth)
        {
            Color32 saved = buffer.Look;
            buffer.Look = SkylineTop;
            switch (top.Kind)
            {
                case TowerTopKind.Mast:
                    AddCentredBox(buffer, top.X, top.Z, roofY, 1.2f, top.Height, 1.2f);
                    break;
                case TowerTopKind.SteppedSpire:
                {
                    float y = roofY, s = top.Size, tiers = top.Height * 0.4f;
                    for (int tier = 0; tier < 3; tier++)
                    {
                        float tall = tiers * (0.45f - 0.1f * tier);
                        AddCentredBox(buffer, top.X, top.Z, y, s, tall, s);
                        y += tall + 0.25f;
                        s *= 0.72f;
                    }
                    float needle = Mathf.Max(1.2f, s * 0.55f);
                    AddCentredBox(buffer, top.X, top.Z, y, needle, Mathf.Max(1.0f, roofY + top.Height - y), needle);
                    break;
                }
                case TowerTopKind.Fins:
                {
                    float ins = ParapetWallThickness + 0.6f, tall = top.Height - 0.4f;
                    float hw = width * 0.5f - ins, hd = depth * 0.5f - ins;
                    buffer.AddBox(new Vector3(-hw, roofY, -hd - 0.15f), new Vector3(hw, roofY + tall, -hd + 0.15f));
                    buffer.AddBox(new Vector3(-hw, roofY, hd - 0.15f), new Vector3(hw, roofY + tall, hd + 0.15f));
                    buffer.AddBox(new Vector3(-hw - 0.15f, roofY, -hd), new Vector3(-hw + 0.15f, roofY + tall, hd));
                    buffer.AddBox(new Vector3(hw - 0.15f, roofY, -hd), new Vector3(hw + 0.15f, roofY + tall, hd));
                    break;
                }
                default:
                {
                    // A spire, straight or slanted: its drum and two boxes of needle.
                    float drum = 2.3f, s = Mathf.Max(1.2f, top.Size * 0.5f);
                    AddCentredBox(buffer, top.X, top.Z, roofY, top.Size, drum, top.Size);
                    float needle = Mathf.Max(1.0f, top.Height - drum);
                    AddCentredBox(buffer, top.X, top.Z, roofY + drum, s, needle * 0.5f, s);
                    AddCentredBox(buffer, top.X, top.Z, roofY + drum + needle * 0.5f, s * 0.5f, needle * 0.5f, s * 0.5f);
                    break;
                }
            }
            buffer.Look = saved;
        }
    }
}
