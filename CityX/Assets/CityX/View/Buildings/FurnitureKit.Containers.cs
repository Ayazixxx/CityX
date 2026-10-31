using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using UnityEngine;
using Kind = CityX.World.OldGuysHouse.FurnitureKind;

namespace CityX.View
{
    /// <summary>
    /// THE FURNITURE THAT OPENS (docs/LOOTING.md): drawers, cupboard doors, the fridge, the wardrobe, chests and the toolbox are moving parts of
    /// their own (LootContainer), so they can be opened and looked into; this draws the carcass they sit in (hollow where a door opens on it, so a
    /// cupboard has an inside) and says where each part is, how it opens and what it is called.
    /// </summary>
    public static partial class FurnitureKit
    {
        /// <summary>One moving part of a piece, in the piece's frame, closed.</summary>
        public struct ContainerPart
        {
            public ContainerKind Kind;
            public Vector3 Min, Max;

            /// <summary>Where it turns (a door's hinge, a lid's back edge, a flap's bottom edge); a drawer's front middle.</summary>
            public Vector3 Pivot;

            public Vector3 HandleMin, HandleMax;
            public float Slide;
            public string Label;
            public Color Tint;

            /// <summary>The middle of its front, where a player looks to open it.</summary>
            public Vector3 Front => new Vector3((Min.x + Max.x) * 0.5f, (Min.y + Max.y) * 0.5f, Max.z);
        }

        /// <summary>The parts of a <paramref name="kind"/> that open, in order (top drawer first, the left door before the right). Empty for the rest.</summary>
        public static List<ContainerPart> PartsOf(Kind kind, Color c)
        {
            var parts = new List<ContainerPart>();
            switch (kind)
            {
                case Kind.Counter:
                    parts.Add(Drawer(-0.78f, 0.87f, 0.45f, 0.78f, 1.05f, 0.95f, 0.42f, "Drawer", c));
                    parts.Add(Door(true, -0.79f, 0.13f, 0.93f, -0.01f, 0.84f, 0.95f, "Cupboard", c));
                    parts.Add(Door(false, 0.01f, 0.13f, 0.93f, 0.79f, 0.84f, 0.95f, "Cupboard", c));
                    break;
                case Kind.Sink:
                    parts.Add(Door(true, -0.79f, 0.13f, 0.93f, -0.01f, 0.81f, 0.95f, "Cupboard under the sink", c));
                    parts.Add(Door(false, 0.01f, 0.13f, 0.93f, 0.79f, 0.81f, 0.95f, "Cupboard under the sink", c));
                    break;
                case Kind.Stove:
                    parts.Add(new ContainerPart
                    {
                        Kind = ContainerKind.Flap, Min = new Vector3(-0.42f, 0.2f, 0.955f), Max = new Vector3(0.42f, 0.85f, 0.98f), Pivot = new Vector3(0, 0.2f, 0.97f),
                        HandleMin = new Vector3(-0.3f, 0.76f, 0.98f), HandleMax = new Vector3(0.3f, 0.8f, 1.02f), Label = "Oven", Tint = Dark,
                    });
                    break;
                case Kind.Fridge:
                    parts.Add(Door(true, -0.45f, 0.0f, 0.85f, 0.45f, 2.4f, 0.9f, "Fridge", c));
                    break;
                case Kind.UpperCabinet:
                    parts.Add(Door(true, -0.79f, 0.01f, 0.45f, -0.01f, 0.89f, 0.47f, "Cupboard", c));
                    parts.Add(Door(false, 0.01f, 0.01f, 0.45f, 0.79f, 0.89f, 0.47f, "Cupboard", c));
                    break;
                case Kind.Sideboard:
                    parts.Add(Door(true, -0.99f, 0.07f, 0.58f, -0.01f, 0.98f, 0.6f, "Sideboard", c));
                    parts.Add(Door(false, 0.01f, 0.07f, 0.58f, 0.99f, 0.98f, 0.6f, "Sideboard", c));
                    break;
                case Kind.Dresser:
                    for (int i = 2; i >= 0; i--)
                        parts.Add(Drawer(-0.67f, 0.06f + i * 0.34f, 0.1f, 0.67f, 0.37f + i * 0.34f, 0.62f, 0.4f, "Drawer", c));
                    break;
                case Kind.Nightstand:
                    parts.Add(Drawer(-0.27f, 0.45f, 0.05f, 0.27f, 0.66f, 0.5f, 0.3f, "Bedside drawer", c));
                    break;
                case Kind.Desk:
                    parts.Add(Drawer(0.42f, 0.52f, 0.1f, 0.86f, 0.9f, 0.8f, 0.4f, "Desk drawer", c));
                    parts.Add(Drawer(0.42f, 0.08f, 0.1f, 0.86f, 0.48f, 0.8f, 0.4f, "Desk drawer", c));
                    break;
                case Kind.Wardrobe:
                    parts.Add(Door(true, -0.89f, 0.02f, 0.7f, -0.005f, 2.58f, 0.73f, "Wardrobe", c));
                    parts.Add(Door(false, 0.005f, 0.02f, 0.7f, 0.89f, 2.58f, 0.73f, "Wardrobe", c));
                    break;
                case Kind.ToyChest:
                    parts.Add(Lid(-0.62f, 0.65f, 0.0f, 0.62f, 0.72f, 0.62f, "Toy chest", Shade(c, 0.25f)));
                    break;
                case Kind.Crate:
                    parts.Add(Lid(-0.47f, 0.9f, -0.47f, 0.47f, 0.97f, 0.47f, "Crate", Shade(c, 0.2f)));
                    break;
                case Kind.Workbench:
                    parts.Add(Lid(0.5f, 1.36f, 0.2f, 0.9f, 1.42f, 0.5f, "Toolbox", new Color(0.65f, 0.15f, 0.12f)));
                    break;
                case Kind.Basin:
                    parts.Add(Door(true, -0.31f, 1.61f, 0.16f, 0.31f, 2.39f, 0.18f, "Bathroom cabinet", new Color(0.75f, 0.85f, 0.90f)));
                    break;
            }
            return parts;
        }

        private static ContainerPart Drawer(float x0, float y0, float z0, float x1, float y1, float z1, float slide, string label, Color c)
        {
            float mx = (x0 + x1) * 0.5f, my = (y0 + y1) * 0.5f, hw = Mathf.Min(0.18f, (x1 - x0) * 0.3f);
            return new ContainerPart
            {
                Kind = ContainerKind.Drawer, Min = new Vector3(x0, y0, z0), Max = new Vector3(x1, y1, z1), Pivot = new Vector3(mx, my, z1),
                HandleMin = new Vector3(mx - hw, my - 0.02f, z1), HandleMax = new Vector3(mx + hw, my + 0.02f, z1 + 0.04f), Slide = slide, Label = label, Tint = c,
            };
        }

        /// <summary>A door hinged on its left edge (<paramref name="left"/>) or its right, its handle near the other edge.</summary>
        private static ContainerPart Door(bool left, float x0, float y0, float z0, float x1, float y1, float z1, string label, Color c)
        {
            float hx = left ? x1 - 0.08f : x0 + 0.08f, my = Mathf.Min((y0 + y1) * 0.5f, y0 + 1.1f);
            return new ContainerPart
            {
                Kind = left ? ContainerKind.DoorLeft : ContainerKind.DoorRight, Min = new Vector3(x0, y0, z0), Max = new Vector3(x1, y1, z1),
                Pivot = new Vector3(left ? x0 : x1, y0, (z0 + z1) * 0.5f),
                HandleMin = new Vector3(hx - 0.02f, my - 0.12f, z1), HandleMax = new Vector3(hx + 0.02f, my + 0.12f, z1 + 0.04f), Label = label, Tint = c,
            };
        }

        private static ContainerPart Lid(float x0, float y0, float z0, float x1, float y1, float z1, string label, Color c)
        {
            float mx = (x0 + x1) * 0.5f;
            return new ContainerPart
            {
                Kind = ContainerKind.Lid, Min = new Vector3(x0, y0, z0), Max = new Vector3(x1, y1, z1), Pivot = new Vector3(mx, y1, z0),
                HandleMin = new Vector3(mx - 0.1f, y0 + 0.01f, z1), HandleMax = new Vector3(mx + 0.1f, y1 - 0.01f, z1 + 0.03f), Label = label, Tint = c,
            };
        }

        /// <summary>
        /// A box open at its front (+z), or at its top: its back, sides, bottom and top panels, <paramref name="wall"/> thick, so a door or a lid
        /// opens onto an inside. The inside of the back is a shade darker, which reads as depth.
        /// </summary>
        private static void Hollow(MeshBuffer b, float x0, float y0, float z0, float x1, float y1, float z1, float wall, Color c, bool openTop = false)
        {
            B(b, x0, y0, z0, x1, y1, z0 + wall, Shade(c, 0.3f));
            B(b, x0, y0, z0, x0 + wall, y1, z1, c);
            B(b, x1 - wall, y0, z0, x1, y1, z1, c);
            B(b, x0, y0, z0, x1, y0 + wall, z1, Shade(c, 0.15f));
            if (openTop)
            {
                B(b, x0, y0, z1 - wall, x1, y1, z1, c);
            }
            else
            {
                B(b, x0, y1 - wall, z0, x1, y1, z1, c);
            }
        }

        /// <summary>Draws the carcass of a piece that opens; false for every other kind (Build draws those).</summary>
        private static bool BuildCarcass(MeshBuffer b, Kind kind, Color c)
        {
            float top = OldGuysHouse.KitchenCounterHeight;
            switch (kind)
            {
                case Kind.Counter:
                    B(b, -0.8f, 0, 0, 0.8f, 0.12f, 0.93f, Dark);
                    Hollow(b, -0.8f, 0.12f, 0, 0.8f, 0.85f, 0.93f, 0.03f, c);
                    B(b, -0.77f, 0.47f, 0.03f, 0.77f, 0.5f, 0.9f, Shade(c, 0.1f));
                    B(b, -0.8f, 0.85f, 0, 0.8f, 1.07f, 0.93f, c);
                    B(b, -0.8f, 1.07f, 0, 0.8f, top, 1.0f, Shade(c, 0.45f), Tile);
                    return true;
                case Kind.Sink:
                    B(b, -0.8f, 0, 0, 0.8f, 0.12f, 0.93f, Dark);
                    Hollow(b, -0.8f, 0.12f, 0, 0.8f, 0.82f, 0.93f, 0.03f, c);
                    B(b, -0.8f, 0.82f, 0.85f, 0.8f, 1.07f, 0.93f, c);
                    B(b, -0.8f, 1.07f, 0, 0.8f, top, 0.15f, Shade(c, 0.45f), Tile);
                    B(b, -0.8f, 1.07f, 0.8f, 0.8f, top, 1.0f, Shade(c, 0.45f), Tile);
                    B(b, -0.8f, 1.07f, 0.15f, -0.45f, top, 0.8f, Shade(c, 0.45f), Tile);
                    B(b, 0.45f, 1.07f, 0.15f, 0.8f, top, 0.8f, Shade(c, 0.45f), Tile);
                    B(b, -0.45f, 0.85f, 0.15f, 0.45f, 0.87f, 0.8f, Chrome, Metal);
                    B(b, -0.04f, top, 0.05f, 0.04f, top + 0.4f, 0.13f, Chrome, Metal);
                    B(b, -0.04f, top + 0.32f, 0.05f, 0.04f, top + 0.4f, 0.4f, Chrome, Metal);
                    return true;
                case Kind.Stove:
                    B(b, -0.5f, 0, 0, 0.5f, 1.07f, 0.95f, c);
                    B(b, -0.42f, 0.2f, 0.94f, 0.42f, 0.85f, 0.955f, Dark, Metal);         // the oven's dark inside, behind its door
                    B(b, -0.5f, 1.07f, 0, 0.5f, top, 1.0f, Dark, Tile);
                    foreach (float x in new[] { -0.25f, 0.25f })
                    foreach (float z in new[] { 0.3f, 0.7f })
                        B(b, x - 0.12f, top, z - 0.12f, x + 0.12f, top + 0.02f, z + 0.12f, Chrome, Metal);
                    return true;
                case Kind.Fridge:
                    Hollow(b, -0.45f, 0, 0, 0.45f, 2.4f, 0.85f, 0.05f, c);
                    for (int i = 1; i <= 3; i++)
                        B(b, -0.4f, i * 0.55f, 0.05f, 0.4f, i * 0.55f + 0.03f, 0.8f, Tint(new Color(0.75f, 0.85f, 0.95f), 0.3f), Metal);
                    return true;
                case Kind.UpperCabinet:
                    Hollow(b, -0.8f, 0, 0, 0.8f, 0.9f, 0.45f, 0.03f, c);
                    B(b, -0.77f, 0.44f, 0.03f, 0.77f, 0.47f, 0.42f, Shade(c, 0.1f));
                    return true;
                case Kind.Sideboard:
                    B(b, -1.0f, 0, 0, 1.0f, 0.06f, 0.58f, Shade(c, 0.2f));
                    Hollow(b, -1.0f, 0.06f, 0, 1.0f, 1.0f, 0.58f, 0.03f, c);
                    B(b, -1.02f, 1.0f, 0, 1.02f, 1.04f, 0.62f, Shade(c, 0.1f));
                    B(b, -0.97f, 0.5f, 0.03f, 0.97f, 0.53f, 0.55f, Shade(c, 0.1f));
                    B(b, -0.6f, 1.04f, 0.15f, -0.3f, 1.39f, 0.4f, new Color(0.70f, 0.70f, 0.80f));   // a vase and a photo frame
                    B(b, 0.3f, 1.04f, 0.2f, 0.62f, 1.44f, 0.24f, Dark);
                    return true;
                case Kind.Dresser:
                    B(b, -0.7f, 0, 0, 0.7f, 1.1f, 0.58f, c);
                    B(b, -0.72f, 1.1f, -0.0f, 0.72f, 1.14f, 0.62f, Shade(c, 0.1f));
                    return true;
                case Kind.Nightstand:
                    B(b, -0.3f, 0, 0, 0.3f, 0.7f, 0.48f, c);
                    B(b, -0.12f, 0.7f, 0.15f, 0.12f, 1.05f, 0.35f, new Color(0.95f, 0.88f, 0.60f));   // a lamp
                    return true;
                case Kind.Desk:
                    B(b, -0.9f, 0.94f, 0, 0.9f, 1.0f, 0.8f, c);
                    B(b, 0.4f, 0, 0.05f, 0.88f, 0.94f, 0.78f, Shade(c, 0.1f));
                    B(b, -0.88f, 0, 0.05f, -0.8f, 0.94f, 0.75f, Shade(c, 0.15f));
                    B(b, -0.6f, 1.0f, 0.1f, -0.2f, 1.06f, 0.4f, Paper);                        // papers
                    return true;
                case Kind.Wardrobe:
                    Hollow(b, -0.9f, 0, 0, 0.9f, 2.6f, 0.7f, 0.05f, c);
                    B(b, -0.85f, 2.2f, 0.33f, 0.85f, 2.24f, 0.37f, Chrome, Metal);           // the hanging rail
                    B(b, -0.7f, 1.0f, 0.12f, -0.35f, 2.2f, 0.58f, new Color(0.30f, 0.32f, 0.40f));   // coats on it
                    B(b, -0.25f, 1.2f, 0.14f, 0.05f, 2.2f, 0.56f, new Color(0.55f, 0.45f, 0.30f));
                    B(b, 0.2f, 1.4f, 0.16f, 0.45f, 2.2f, 0.54f, new Color(0.45f, 0.20f, 0.18f));
                    return true;
                case Kind.ToyChest:
                    Hollow(b, -0.6f, 0, 0, 0.6f, 0.65f, 0.6f, 0.05f, c, true);
                    return true;
                case Kind.Crate:
                    Hollow(b, -0.45f, 0, -0.45f, 0.45f, 0.9f, 0.45f, 0.06f, c, true);
                    B(b, -0.47f, 0.4f, -0.47f, 0.47f, 0.5f, 0.47f, Shade(c, 0.25f));
                    return true;
                case Kind.Workbench:
                    B(b, -1.2f, 1.1f, 0, 1.2f, 1.2f, 0.9f, c);
                    foreach (float x in new[] { -1.15f, 1.05f })
                    foreach (float z in new[] { 0.05f, 0.75f })
                        B(b, x, 0, z, x + 0.1f, 1.1f, z + 0.1f, Shade(c, 0.2f));
                    B(b, -1.2f, 1.4f, 0, 1.2f, 2.4f, 0.04f, Tint(c, 0.4f));                  // pegboard
                    B(b, -0.8f, 1.7f, 0.04f, -0.2f, 1.78f, 0.1f, Chrome, Metal);             // tools on it
                    B(b, 0.1f, 1.6f, 0.04f, 0.18f, 2.1f, 0.1f, Chrome, Metal);
                    B(b, 0.5f, 1.2f, 0.2f, 0.9f, 1.36f, 0.5f, new Color(0.65f, 0.15f, 0.12f), Metal);   // the toolbox's body
                    return true;
                case Kind.Basin:
                    B(b, -0.35f, 1.0f, 0, 0.35f, 1.25f, 0.55f, c, Tile);
                    B(b, -0.1f, 0, 0.1f, 0.1f, 1.0f, 0.3f, c, Tile);
                    B(b, -0.04f, 1.25f, 0.03f, 0.04f, 1.45f, 0.18f, Chrome, Metal);
                    Hollow(b, -0.32f, 1.6f, 0, 0.32f, 2.4f, 0.16f, 0.02f, Shade(c, 0.05f));
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>A part's own mesh, in a frame on its pivot (as LootContainer turns it): its box and its handle.</summary>
        public static void BuildPart(MeshBuffer b, ContainerPart part)
        {
            B(b, part.Min.x - part.Pivot.x, part.Min.y - part.Pivot.y, part.Min.z - part.Pivot.z,
                part.Max.x - part.Pivot.x, part.Max.y - part.Pivot.y, part.Max.z - part.Pivot.z, part.Tint);
            B(b, part.HandleMin.x - part.Pivot.x, part.HandleMin.y - part.Pivot.y, part.HandleMin.z - part.Pivot.z,
                part.HandleMax.x - part.Pivot.x, part.HandleMax.y - part.Pivot.y, part.HandleMax.z - part.Pivot.z, Chrome, Metal);
        }
    }
}
