using CityX.World;
using UnityEngine;

namespace CityX.View
{
    public static partial class CityMeshBuilder
    {
        /// <summary>
        /// What the inside of a building is finished in, and every one of these looks is marked INDOORS (SurfaceLook), so the facade shader dims
        /// the sky's light on it and lets only the lights inside the building reach it.
        ///
        /// WHY NOT BARE CONCRETE ANY MORE (2026-10-09 lighting pass; owner: "the lighting really doesn't look good both indoors and looking from
        /// outdoors during nighttime"). Lighting research for games is unanimous that a lit room reads as lit only if its surfaces are light: a
        /// mid-grey concrete box swallows its lamps and looks murky whatever they do, and from the street a lit window shows mostly ceiling. So:
        /// pale plaster walls in a colour that says what the building is (white and grey offices, warm or pastel homes), a white ceiling, a floor by
        /// what the building is, and bare concrete only for the stairs.
        /// </summary>
        private readonly struct InteriorLooks
        {
            public readonly Color32 Wall;
            public readonly Color32 Ceiling;
            public readonly Color32 Floor;
            public readonly Color32 Stair;

            private InteriorLooks(Color32 wall, Color32 ceiling, Color32 floor, Color32 stair)
            {
                Wall = wall;
                Ceiling = ceiling;
                Floor = floor;
                Stair = stair;
            }

            private const ulong InteriorSalt = 0x494E5445524FUL;   // "INTERO"

            /// <summary>Offices and shops: white, grey-white, cool and warm whites.</summary>
            private static readonly Color[] OfficeWalls =
            {
                new Color(0.88f, 0.88f, 0.86f), new Color(0.84f, 0.86f, 0.88f), new Color(0.90f, 0.87f, 0.82f), new Color(0.80f, 0.81f, 0.80f),
            };

            /// <summary>Homes: cream, sage, pale blue, dusty pink, white.</summary>
            private static readonly Color[] HomeWalls =
            {
                new Color(0.91f, 0.86f, 0.75f), new Color(0.80f, 0.85f, 0.75f), new Color(0.77f, 0.83f, 0.88f), new Color(0.90f, 0.79f, 0.75f),
                new Color(0.90f, 0.89f, 0.86f),
            };

            /// <summary>Works and sheds: painted block, pale grey and a faded institutional green.</summary>
            private static readonly Color[] WorksWalls =
            {
                new Color(0.76f, 0.77f, 0.75f), new Color(0.72f, 0.78f, 0.72f),
            };

            private static readonly Color CeilingWhite = new Color(0.93f, 0.93f, 0.91f);
            private static readonly Color StairConcrete = new Color(0.70f, 0.69f, 0.66f);

            public static InteriorLooks For(BuildingSite site)
            {
                ulong seed = unchecked((ulong)(long)site.Seed);
                double pick = WorldHash.Unit01(site.Id, 1, InteriorSalt, seed);
                double floorPick = WorldHash.Unit01(site.Id, 2, InteriorSalt, seed);

                Color wall;
                int wallSlice = (int)WallMaterial.Plaster;
                Color floor;
                int floorSlice;
                switch (site.Zone)
                {
                    case ZoneKind.Residential:
                    case ZoneKind.OldTown:
                        wall = HomeWalls[Mathf.Min(HomeWalls.Length - 1, (int)(pick * HomeWalls.Length))];
                        // Terracotta or warm tiles, or a pale stone.
                        if (floorPick < 0.4) { floor = new Color(0.66f, 0.45f, 0.33f); floorSlice = (int)WallMaterial.Tile; }
                        else if (floorPick < 0.75) { floor = new Color(0.70f, 0.60f, 0.48f); floorSlice = (int)WallMaterial.Tile; }
                        else { floor = new Color(0.74f, 0.71f, 0.66f); floorSlice = (int)WallMaterial.Stone; }
                        break;
                    case ZoneKind.Industrial:
                        wall = WorksWalls[Mathf.Min(WorksWalls.Length - 1, (int)(pick * WorksWalls.Length))];
                        wallSlice = (int)WallMaterial.Concrete;
                        floor = new Color(0.58f, 0.58f, 0.56f);
                        floorSlice = (int)WallMaterial.Concrete;
                        break;
                    default:
                        wall = OfficeWalls[Mathf.Min(OfficeWalls.Length - 1, (int)(pick * OfficeWalls.Length))];
                        // Grey tiles, pale terrazzo-like stone, or polished concrete.
                        if (floorPick < 0.4) { floor = new Color(0.60f, 0.61f, 0.62f); floorSlice = (int)WallMaterial.Tile; }
                        else if (floorPick < 0.7) { floor = new Color(0.78f, 0.76f, 0.72f); floorSlice = (int)WallMaterial.Stone; }
                        else { floor = new Color(0.66f, 0.65f, 0.62f); floorSlice = (int)WallMaterial.Concrete; }
                        break;
                }
                return new InteriorLooks(
                    SurfaceLook.Pack(wall, wallSlice, 0.0f, indoors: true),
                    SurfaceLook.Pack(CeilingWhite, (int)WallMaterial.Plaster, 0.0f, indoors: true),
                    SurfaceLook.Pack(floor, floorSlice, 0.1f, indoors: true),
                    SurfaceLook.Pack(StairConcrete, (int)WallMaterial.Concrete, 0.1f, indoors: true));
            }
        }
    }
}
