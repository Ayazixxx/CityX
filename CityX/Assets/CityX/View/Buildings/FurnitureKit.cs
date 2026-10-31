using CityX.World;
using UnityEngine;
using Kind = CityX.World.OldGuysHouse.FurnitureKind;

namespace CityX.View
{
    /// <summary>
    /// LOW-EFFORT FURNITURE (owner, 2026-10-30: "give it some basic furniture as well make low effort models i will replace with better stuff
    /// later"): each piece a few tinted boxes in the house's own material, recognisable at a glance and cheap to replace. A piece's frame: +z its
    /// front, +x its right, y up, at floor level; a piece that stands against a wall has its origin at the middle of its back, a free-standing one
    /// (a table, a chair, the car, a crate, the tree) at its middle. Sizes are the 2.6 m player's (real times 1.3).
    /// </summary>
    public static class FurnitureKit
    {
        private const int Plaster = (int)WallMaterial.Plaster;
        private const int Metal = (int)WallMaterial.Metal;
        private const int Tile = (int)WallMaterial.Tile;
        private const int Brick = (int)WallMaterial.Brick;

        private static readonly Color Dark = new Color(0.12f, 0.12f, 0.13f);
        private static readonly Color Paper = new Color(0.95f, 0.94f, 0.90f);
        private static readonly Color Chrome = new Color(0.70f, 0.72f, 0.74f);

        /// <summary>Adds <paramref name="piece"/> to <paramref name="buffer"/>, turned and moved into the house's frame.</summary>
        public static void Add(MeshBuffer buffer, OldGuysHouse.Furniture piece)
        {
            float yaw = piece.Yaw * Mathf.Deg2Rad;
            buffer.SetFrame(piece.Position, Mathf.Cos(yaw), -Mathf.Sin(yaw));
            Build(buffer, piece.Kind, piece.Tint);
            buffer.ClearFrame();
        }

        private static void B(MeshBuffer b, float x0, float y0, float z0, float x1, float y1, float z1, Color tint, int slice = Plaster)
        {
            b.Look = SurfaceLook.Pack(tint, slice, 0.1f, true);
            b.AddBox(new Vector3(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Min(z0, z1)), new Vector3(Mathf.Max(x0, x1), Mathf.Max(y0, y1), Mathf.Max(z0, z1)), true);
        }

        private static Color Shade(Color c, float by) => Color.Lerp(c, Color.black, by);

        private static Color Tint(Color c, float by) => Color.Lerp(c, Color.white, by);

        private static void Build(MeshBuffer b, Kind kind, Color c)
        {
            switch (kind)
            {
                case Kind.Car:
                    B(b, -1.1f, 0.45f, -2.8f, 1.1f, 1.45f, 2.8f, c, Metal);
                    B(b, -0.95f, 1.45f, -1.4f, 0.95f, 2.25f, 1.2f, Shade(c, 0.25f), Metal);
                    B(b, -0.9f, 1.55f, 1.15f, 0.9f, 2.15f, 1.25f, Dark, Metal);             // windscreen
                    foreach (float x in new[] { -1.15f, 0.85f })
                    foreach (float z in new[] { -1.9f, 1.6f })
                        B(b, x, 0, z, x + 0.3f, 0.9f, z + 0.9f, Dark, Metal);
                    B(b, -1.0f, 0.75f, 2.8f, -0.6f, 1.0f, 2.82f, Paper, Metal);              // headlamps
                    B(b, 0.6f, 0.75f, 2.8f, 1.0f, 1.0f, 2.82f, Paper, Metal);
                    break;
                case Kind.Workbench:
                    B(b, -1.2f, 1.1f, 0, 1.2f, 1.2f, 0.9f, c);
                    foreach (float x in new[] { -1.15f, 1.05f })
                    foreach (float z in new[] { 0.05f, 0.75f })
                        B(b, x, 0, z, x + 0.1f, 1.1f, z + 0.1f, Shade(c, 0.2f));
                    B(b, -1.2f, 1.4f, 0, 1.2f, 2.4f, 0.04f, Tint(c, 0.4f));                  // pegboard
                    B(b, -0.8f, 1.7f, 0.04f, -0.2f, 1.78f, 0.1f, Chrome, Metal);             // tools on it
                    B(b, 0.1f, 1.6f, 0.04f, 0.18f, 2.1f, 0.1f, Chrome, Metal);
                    B(b, 0.5f, 1.21f, 0.2f, 0.9f, 1.4f, 0.5f, new Color(0.65f, 0.15f, 0.12f), Metal);   // a toolbox
                    break;
                case Kind.Shelves:
                    B(b, -1.1f, 0, 0, -1.05f, 2.4f, 0.6f, c, Metal);
                    B(b, 1.05f, 0, 0, 1.1f, 2.4f, 0.6f, c, Metal);
                    for (int i = 0; i < 4; i++)
                    {
                        float y = 0.15f + i * 0.72f;
                        B(b, -1.05f, y, 0, 1.05f, y + 0.04f, 0.6f, c, Metal);
                        B(b, -0.9f + i * 0.3f, y + 0.04f, 0.1f, -0.4f + i * 0.3f, y + 0.4f, 0.5f, new Color(0.62f, 0.50f, 0.34f));   // boxes
                    }
                    break;
                case Kind.OilDrum:
                    B(b, -0.4f, 0, -0.28f, 0.4f, 1.2f, 0.28f, c, Metal);
                    B(b, -0.28f, 0, -0.4f, 0.28f, 1.2f, 0.4f, c, Metal);
                    break;
                case Kind.Manhole:
                    B(b, -0.55f, 0, -0.38f, 0.55f, 0.025f, 0.38f, c, Metal);
                    B(b, -0.38f, 0, -0.55f, 0.38f, 0.025f, 0.55f, c, Metal);
                    break;
                case Kind.FuseBox:
                    B(b, -0.4f, 1.3f, 0, 0.4f, 2.4f, 0.25f, c, Metal);
                    B(b, 0.18f, 1.6f, 0.25f, 0.28f, 1.95f, 0.33f, new Color(0.75f, 0.15f, 0.10f), Metal);   // the lever
                    B(b, -0.05f, 0, 0.02f, 0.05f, 1.3f, 0.08f, Dark, Metal);                // the cable down the wall
                    break;
                case Kind.Boiler:
                    B(b, -0.45f, 0.6f, 0, 0.45f, 2.2f, 0.7f, c, Metal);
                    B(b, -0.3f, 0, 0.2f, -0.22f, 0.6f, 0.28f, Chrome, Metal);
                    B(b, 0.22f, 0, 0.2f, 0.3f, 0.6f, 0.28f, Chrome, Metal);
                    break;
                case Kind.Crate:
                    B(b, -0.45f, 0, -0.45f, 0.45f, 0.9f, 0.45f, c);
                    B(b, -0.47f, 0.4f, -0.47f, 0.47f, 0.5f, 0.47f, Shade(c, 0.25f));
                    break;
                case Kind.Sofa:
                    B(b, -1.3f, 0, 0, 1.3f, 0.55f, 1.15f, Shade(c, 0.15f));
                    B(b, -1.3f, 0.55f, 0, 1.3f, 1.25f, 0.35f, c);
                    B(b, -1.3f, 0.55f, 0.35f, -1.05f, 0.85f, 1.15f, c);
                    B(b, 1.05f, 0.55f, 0.35f, 1.3f, 0.85f, 1.15f, c);
                    B(b, -1.03f, 0.55f, 0.37f, -0.02f, 0.7f, 1.12f, Tint(c, 0.12f));       // cushions
                    B(b, 0.02f, 0.55f, 0.37f, 1.03f, 0.7f, 1.12f, Tint(c, 0.12f));
                    break;
                case Kind.Armchair:
                    B(b, -0.6f, 0, 0, 0.6f, 0.55f, 1.1f, Shade(c, 0.15f));
                    B(b, -0.6f, 0.55f, 0, 0.6f, 1.3f, 0.32f, c);
                    B(b, -0.6f, 0.55f, 0.32f, -0.42f, 0.85f, 1.1f, c);
                    B(b, 0.42f, 0.55f, 0.32f, 0.6f, 0.85f, 1.1f, c);
                    break;
                case Kind.CoffeeTable:
                    B(b, -0.8f, 0.55f, -0.45f, 0.8f, 0.62f, 0.45f, c);
                    foreach (float x in new[] { -0.72f, 0.65f })
                    foreach (float z in new[] { -0.38f, 0.31f })
                        B(b, x, 0, z, x + 0.07f, 0.55f, z + 0.07f, Shade(c, 0.2f));
                    break;
                case Kind.Fireplace:
                    B(b, -1.1f, 0, 0, 1.1f, 1.5f, 0.5f, c, Brick);
                    B(b, -0.6f, 0.12f, 0.49f, 0.6f, 1.0f, 0.52f, Dark, Brick);              // the hearth's mouth
                    B(b, -1.3f, 1.5f, 0, 1.3f, 1.6f, 0.6f, new Color(0.40f, 0.27f, 0.16f));   // the mantel
                    B(b, -0.9f, 1.6f, 0, 0.9f, 7.0f, 0.35f, Shade(c, 0.08f), Brick);         // the chimney breast
                    B(b, -1.2f, 0, 0.5f, 1.2f, 0.08f, 1.1f, Shade(c, 0.2f), Brick);          // the hearth
                    break;
                case Kind.Rug:
                    B(b, -1.5f, 0.005f, -1.1f, 1.5f, 0.025f, 1.1f, c);
                    B(b, -1.3f, 0.025f, -0.9f, 1.3f, 0.03f, 0.9f, Tint(c, 0.25f));
                    break;
                case Kind.Bottle:
                    B(b, -0.06f, 0, -0.06f, 0.06f, 0.3f, 0.06f, c);
                    B(b, -0.025f, 0.3f, -0.025f, 0.025f, 0.42f, 0.025f, c);
                    break;
                case Kind.Counter:
                case Kind.Sink:
                case Kind.Stove:
                    float half = kind == Kind.Stove ? 0.5f : 0.8f;
                    B(b, -half, 0, 0, half, 1.07f, 0.95f, c);
                    B(b, -half, 0, 0.92f, half, 0.12f, 0.96f, Dark);                          // kick plate
                    B(b, -0.01f, 0.15f, 0.95f, 0.01f, 1.0f, 0.97f, Shade(c, 0.3f));          // between the doors
                    if (kind == Kind.Sink)
                    {
                        B(b, -half, 1.07f, 0, half, OldGuysHouse.KitchenCounterHeight, 0.15f, Shade(c, 0.45f), Tile);
                        B(b, -half, 1.07f, 0.8f, half, OldGuysHouse.KitchenCounterHeight, 1.0f, Shade(c, 0.45f), Tile);
                        B(b, -half, 1.07f, 0.15f, -0.45f, OldGuysHouse.KitchenCounterHeight, 0.8f, Shade(c, 0.45f), Tile);
                        B(b, 0.45f, 1.07f, 0.15f, half, OldGuysHouse.KitchenCounterHeight, 0.8f, Shade(c, 0.45f), Tile);
                        B(b, -0.45f, 0.85f, 0.15f, 0.45f, 0.87f, 0.8f, Chrome, Metal);       // the basin's floor
                        B(b, -0.04f, OldGuysHouse.KitchenCounterHeight, 0.05f, 0.04f, OldGuysHouse.KitchenCounterHeight + 0.4f, 0.13f, Chrome, Metal);
                        B(b, -0.04f, OldGuysHouse.KitchenCounterHeight + 0.32f, 0.05f, 0.04f, OldGuysHouse.KitchenCounterHeight + 0.4f, 0.4f, Chrome, Metal);
                    }
                    else
                    {
                        B(b, -half, 1.07f, 0, half, OldGuysHouse.KitchenCounterHeight, 1.0f, kind == Kind.Stove ? Dark : Shade(c, 0.45f), Tile);
                    }
                    if (kind == Kind.Stove)
                    {
                        foreach (float x in new[] { -0.25f, 0.25f })
                        foreach (float z in new[] { 0.3f, 0.7f })
                            B(b, x - 0.12f, OldGuysHouse.KitchenCounterHeight, z - 0.12f, x + 0.12f, OldGuysHouse.KitchenCounterHeight + 0.02f, z + 0.12f, Chrome, Metal);
                        B(b, -0.42f, 0.2f, 0.96f, 0.42f, 0.85f, 0.98f, Dark, Metal);          // the oven door
                    }
                    break;
                case Kind.Fridge:
                    B(b, -0.45f, 0, 0, 0.45f, 2.4f, 0.85f, c, Metal);
                    B(b, -0.45f, 1.55f, 0.85f, 0.45f, 1.58f, 0.87f, Shade(c, 0.3f), Metal);
                    B(b, 0.32f, 0.9f, 0.85f, 0.37f, 1.45f, 0.92f, Chrome, Metal);
                    B(b, 0.32f, 1.7f, 0.85f, 0.37f, 2.1f, 0.92f, Chrome, Metal);
                    break;
                case Kind.KnifeBlock:
                    // The kitchen knife itself is an item standing in the front slot (OldGuysHouse.Items); two more handles behind it.
                    B(b, -0.09f, 0, -0.12f, 0.09f, 0.30f, 0.12f, c);
                    B(b, -0.045f, 0.30f, -0.09f, -0.015f, 0.42f, -0.06f, Dark);
                    B(b, 0.015f, 0.30f, -0.09f, 0.045f, 0.40f, -0.06f, Dark);
                    break;
                case Kind.UpperCabinet:
                    B(b, -0.8f, 0, 0, 0.8f, 0.9f, 0.45f, c);
                    B(b, -0.01f, 0.05f, 0.45f, 0.01f, 0.85f, 0.47f, Shade(c, 0.3f));
                    break;
                case Kind.DiningTable:
                    B(b, -0.6f, 0.9f, -1.1f, 0.6f, 0.98f, 1.1f, c);
                    foreach (float x in new[] { -0.55f, 0.47f })
                    foreach (float z in new[] { -1.05f, 0.97f })
                        B(b, x, 0, z, x + 0.08f, 0.9f, z + 0.08f, Shade(c, 0.15f));
                    break;
                case Kind.Chair:
                    B(b, -0.28f, 0.58f, -0.28f, 0.28f, 0.64f, 0.28f, c);
                    foreach (float x in new[] { -0.26f, 0.21f })
                    foreach (float z in new[] { -0.26f, 0.21f })
                        B(b, x, 0, z, x + 0.05f, 0.58f, z + 0.05f, Shade(c, 0.15f));
                    B(b, -0.28f, 0.64f, -0.28f, 0.28f, 1.35f, -0.23f, c);
                    break;
                case Kind.Sideboard:
                    B(b, -1.0f, 0, 0, 1.0f, 1.0f, 0.6f, c);
                    B(b, -0.34f, 0.1f, 0.6f, -0.32f, 0.9f, 0.62f, Shade(c, 0.3f));
                    B(b, 0.32f, 0.1f, 0.6f, 0.34f, 0.9f, 0.62f, Shade(c, 0.3f));
                    B(b, -0.6f, 1.0f, 0.15f, -0.3f, 1.35f, 0.4f, new Color(0.70f, 0.70f, 0.80f));   // a vase and a photo frame
                    B(b, 0.3f, 1.0f, 0.2f, 0.62f, 1.4f, 0.24f, Dark);
                    break;
                case Kind.Television:
                    B(b, -0.7f, 0, 0, 0.7f, 0.7f, 0.6f, new Color(0.40f, 0.27f, 0.16f));
                    B(b, -0.6f, 0.7f, 0.02f, 0.6f, 1.55f, 0.55f, c);
                    B(b, -0.48f, 0.8f, 0.55f, 0.48f, 1.45f, 0.57f, new Color(0.20f, 0.24f, 0.26f));   // the screen
                    break;
                case Kind.Radiator:
                    for (int i = 0; i < 10; i++)
                    {
                        float x = -0.7f + i * 0.15f;
                        B(b, x, 0.15f, 0.02f, x + 0.1f, 0.95f, 0.14f, c, Metal);
                    }
                    break;
                case Kind.Toilet:
                    B(b, -0.3f, 0.55f, 0, 0.3f, 1.05f, 0.25f, c, Tile);
                    B(b, -0.25f, 0, 0.2f, 0.25f, 0.55f, 0.95f, c, Tile);
                    B(b, -0.27f, 0.55f, 0.2f, 0.27f, 0.6f, 0.97f, Shade(c, 0.1f), Tile);
                    break;
                case Kind.Basin:
                    B(b, -0.35f, 1.0f, 0, 0.35f, 1.25f, 0.55f, c, Tile);
                    B(b, -0.1f, 0, 0.1f, 0.1f, 1.0f, 0.3f, c, Tile);
                    B(b, -0.04f, 1.25f, 0.03f, 0.04f, 1.45f, 0.18f, Chrome, Metal);
                    B(b, -0.3f, 1.6f, 0, 0.3f, 2.4f, 0.03f, new Color(0.75f, 0.85f, 0.90f), Metal);   // the mirror
                    break;
                case Kind.Bathtub:
                    B(b, -1.1f, 0, 0, 1.1f, 0.72f, 0.95f, c, Tile);
                    B(b, -0.95f, 0.5f, 0.12f, 0.95f, 0.73f, 0.83f, Tint(new Color(0.55f, 0.75f, 0.85f), 0.3f), Tile);
                    B(b, 0.85f, 0.72f, 0.02f, 0.93f, 1.2f, 0.12f, Chrome, Metal);
                    break;
                case Kind.CoatRack:
                    B(b, -0.04f, 0, -0.04f, 0.04f, 2.3f, 0.04f, c);
                    B(b, -0.3f, 0, -0.3f, 0.3f, 0.05f, 0.3f, c);
                    B(b, -0.3f, 1.2f, -0.12f, 0.1f, 2.15f, 0.12f, new Color(0.30f, 0.32f, 0.25f));   // a coat
                    break;
                case Kind.Plant:
                    B(b, -0.25f, 0, -0.25f, 0.25f, 0.5f, 0.25f, new Color(0.72f, 0.42f, 0.30f));
                    B(b, -0.45f, 0.5f, -0.4f, 0.4f, 1.3f, 0.45f, c);
                    B(b, -0.25f, 1.2f, -0.25f, 0.3f, 1.7f, 0.2f, Tint(c, 0.15f));
                    break;
                case Kind.SmallTable:
                    B(b, -0.5f, 0.85f, 0, 0.5f, 0.9f, 0.5f, c);
                    foreach (float x in new[] { -0.46f, 0.4f })
                    foreach (float z in new[] { 0.04f, 0.4f })
                        B(b, x, 0, z, x + 0.06f, 0.85f, z + 0.06f, Shade(c, 0.15f));
                    break;
                case Kind.Bed:
                case Kind.DoubleBed:
                    float w = kind == Kind.Bed ? 0.8f : 1.1f, length = kind == Kind.Bed ? 2.6f : 2.7f;
                    B(b, -w, 0, 0, w, 0.45f, length, new Color(0.40f, 0.27f, 0.16f));
                    B(b, -w + 0.05f, 0.45f, 0.05f, w - 0.05f, 0.7f, length - 0.05f, Paper);
                    B(b, -w + 0.04f, 0.7f, 0.8f, w - 0.04f, 0.78f, length - 0.04f, c);        // the blanket
                    B(b, -w + 0.15f, 0.7f, 0.15f, w - 0.15f, 0.85f, 0.65f, Paper);             // the pillow
                    B(b, -w, 0, 0, w, 1.3f, 0.1f, new Color(0.40f, 0.27f, 0.16f));           // the headboard
                    break;
                case Kind.Desk:
                    B(b, -0.9f, 0.94f, 0, 0.9f, 1.0f, 0.8f, c);
                    B(b, 0.4f, 0, 0.05f, 0.88f, 0.94f, 0.78f, Shade(c, 0.1f));
                    B(b, -0.88f, 0, 0.05f, -0.8f, 0.94f, 0.75f, Shade(c, 0.15f));
                    B(b, -0.6f, 1.0f, 0.1f, -0.2f, 1.06f, 0.4f, Paper);                        // papers
                    break;
                case Kind.Bookcase:
                    B(b, -0.7f, 0, 0, 0.7f, 2.6f, 0.06f, c);
                    B(b, -0.7f, 0, 0, -0.65f, 2.6f, 0.45f, c);
                    B(b, 0.65f, 0, 0, 0.7f, 2.6f, 0.45f, c);
                    for (int i = 0; i < 5; i++)
                    {
                        float y = 0.1f + i * 0.5f;
                        B(b, -0.65f, y, 0, 0.65f, y + 0.04f, 0.45f, c);
                        float x = -0.62f;
                        for (int k = 0; k < 6; k++)
                        {
                            float bw = 0.12f + ((i * 7 + k * 3) % 4) * 0.03f;
                            Color book = Color.HSVToRGB(((i * 5 + k * 3) % 11) / 11.0f, 0.45f, 0.55f);
                            B(b, x, y + 0.04f, 0.06f, x + bw, y + 0.34f + (k % 3) * 0.04f, 0.4f, book);
                            x += bw + 0.02f;
                            if (x > 0.5f) break;
                        }
                    }
                    break;
                case Kind.GlassCase:
                    // A pedestal and a cushion; the wand lies on it (OldGuysHouse.Items). Its glass is drawn by the house builder, with nothing solid.
                    B(b, -0.35f, 0, -0.35f, 0.35f, 1.05f, 0.35f, c);
                    B(b, -0.4f, 1.05f, -0.4f, 0.4f, 1.1f, 0.4f, Shade(c, 0.2f));
                    B(b, -0.28f, 1.1f, -0.16f, 0.28f, 1.14f, 0.16f, new Color(0.55f, 0.12f, 0.15f));
                    break;
                case Kind.Wardrobe:
                    B(b, -0.9f, 0, 0, 0.9f, 2.6f, 0.7f, c);
                    B(b, -0.01f, 0.1f, 0.7f, 0.01f, 2.5f, 0.72f, Shade(c, 0.35f));
                    B(b, -0.12f, 1.2f, 0.7f, -0.08f, 1.5f, 0.76f, Chrome, Metal);
                    B(b, 0.08f, 1.2f, 0.7f, 0.12f, 1.5f, 0.76f, Chrome, Metal);
                    break;
                case Kind.Nightstand:
                    B(b, -0.3f, 0, 0, 0.3f, 0.7f, 0.5f, c);
                    B(b, -0.12f, 0.7f, 0.15f, 0.12f, 1.05f, 0.35f, new Color(0.95f, 0.88f, 0.60f));   // a lamp
                    break;
                case Kind.Dresser:
                    B(b, -0.7f, 0, 0, 0.7f, 1.1f, 0.6f, c);
                    for (int i = 0; i < 3; i++)
                        B(b, -0.65f, 0.3f + i * 0.3f, 0.6f, 0.65f, 0.32f + i * 0.3f, 0.62f, Shade(c, 0.35f));
                    break;
                case Kind.ToyChest:
                    B(b, -0.6f, 0, 0, 0.6f, 0.65f, 0.6f, c);
                    B(b, -0.62f, 0.65f, -0.02f, 0.62f, 0.72f, 0.62f, Shade(c, 0.25f));
                    B(b, -0.3f, 0.72f, 0.15f, 0.0f, 0.95f, 0.4f, new Color(0.95f, 0.80f, 0.20f));   // a toy on its lid
                    break;
                case Kind.Poster:
                    B(b, -0.45f, -0.6f, 0, 0.45f, 0.6f, 0.02f, c);
                    B(b, -0.3f, -0.3f, 0.02f, 0.3f, 0.35f, 0.025f, new Color(0.85f, 0.25f, 0.15f));
                    break;
                case Kind.Drawing:
                    // The grandson's drawing: the tree, the tree house on it and a bridge to the house -- the hint to the hidden bat.
                    B(b, -0.4f, -0.3f, 0, 0.4f, 0.3f, 0.015f, c);
                    B(b, -0.25f, -0.25f, 0.015f, -0.2f, 0.1f, 0.02f, new Color(0.40f, 0.25f, 0.12f));
                    B(b, -0.35f, 0.05f, 0.015f, -0.08f, 0.25f, 0.02f, new Color(0.30f, 0.60f, 0.25f));
                    B(b, -0.3f, -0.05f, 0.015f, -0.14f, 0.06f, 0.02f, new Color(0.60f, 0.40f, 0.20f));
                    B(b, -0.14f, -0.02f, 0.015f, 0.2f, 0.0f, 0.02f, new Color(0.45f, 0.30f, 0.15f));
                    B(b, 0.2f, -0.25f, 0.015f, 0.35f, 0.05f, 0.02f, new Color(0.85f, 0.85f, 0.85f));
                    break;
                case Kind.Tree:
                    B(b, -0.6f, 0, -0.6f, 0.6f, 9.0f, 0.6f, new Color(0.40f, 0.28f, 0.18f));
                    B(b, -3.2f, 8.4f, -3.2f, 3.2f, 11.6f, 3.2f, c);
                    B(b, -2.2f, 11.6f, -2.2f, 2.2f, 13.2f, 2.2f, Tint(c, 0.1f));
                    B(b, 2.7f, 7.6f, -1.2f, 5.4f, 10.2f, 2.4f, Shade(c, 0.1f));
                    B(b, -5.4f, 7.9f, -2.6f, -2.7f, 10.4f, 0.4f, Shade(c, 0.05f));
                    break;
                case Kind.Bench:
                    B(b, -1.0f, 0.55f, 0.1f, 1.0f, 0.62f, 0.6f, c);
                    B(b, -1.0f, 0.62f, 0.05f, 1.0f, 1.2f, 0.12f, c);
                    B(b, -0.95f, 0, 0.15f, -0.85f, 0.55f, 0.55f, Shade(c, 0.3f), Metal);
                    B(b, 0.85f, 0, 0.15f, 0.95f, 0.55f, 0.55f, Shade(c, 0.3f), Metal);
                    break;
                case Kind.CeilingLamp:
                    B(b, -0.02f, -0.6f, -0.02f, 0.02f, 0, 0.02f, Dark);
                    B(b, -0.3f, -0.9f, -0.3f, 0.3f, -0.6f, 0.3f, c);
                    break;
            }
        }
    }
}
