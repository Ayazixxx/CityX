using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The light fittings on every room's ceiling and the switch by every room's door (World.RoomLighting), drawn into the building's interior
    /// buffer; and, for the one building in ten with power (World.PowerGrid), each room's glowing faces and real light positions handed out as a
    /// <see cref="RoomGlow"/> for CityStreamer to switch on and off.
    /// </summary>
    public static partial class CityMeshBuilder
    {
        /// <summary>One powered room's lights in a chunk: the glow under its fittings and on its windows (chunk-local), and where its real lights go.</summary>
        public sealed class RoomGlow
        {
            public RoomKey Key;
            public readonly MeshBuffer Glow = new MeshBuffer();

            /// <summary>The room's windows, lit from inside: what a switched-on room looks like from the street.</summary>
            public readonly MeshBuffer Windows = new MeshBuffer();

            /// <summary>The lamps (spots shining down, chunk-local) and their range and brightness (RoomLightPlan.LightPoints).</summary>
            public Vector3[] Lights;
            public float Range;
            public float Intensity;

            /// <summary>The light bounced off the floor under each lamp (spots shining up, chunk-local; RoomLightPlan.BouncePoints).</summary>
            public Vector3[] Bounces;
            public float BounceRange;
            public float BounceIntensity;
            public Color Colour;

            /// <summary>The room's plan (World.RoomLighting), for its bounced light (RoomFill): the space it fills and how bright, in the building's frame.</summary>
            public RoomLightPlan Plan;

            /// <summary>The building's frame, chunk-local: its origin and its x and z axes.</summary>
            public Vector3 FrameOrigin;
            public Vector3 FrameX;
            public Vector3 FrameZ;
        }

        /// <summary>
        /// A powered building's rooms while it is being built: each window pane the builder hangs adds a glowing face just behind its glass to the
        /// room it lights (by the floor it is on, and whether it is in that floor's corner room), and the fittings follow (AddRoomLighting). The only
        /// lit windows in the city are these, and they are lit only while their room's switch is on (owner, 2026-10-09).
        /// </summary>
        private sealed class RoomWindowGlow
        {
            /// <summary>How far behind the glass the glow sits.</summary>
            public const float BehindGlass = 0.05f;

            public readonly List<RoomLightPlan> Plans;
            public readonly Dictionary<RoomKey, RoomGlow> Glows = new Dictionary<RoomKey, RoomGlow>();
            private readonly BuildingSite _site;
            private readonly float _storey;
            private readonly int _floors;
            private readonly float _top;
            private readonly Dictionary<int, Rect> _cornerRooms = new Dictionary<int, Rect>();

            private RoomWindowGlow(BuildingSite site, List<RoomLightPlan> plans, Vector3 offset)
            {
                _site = site;
                Plans = plans;
                float height = site.Params.Height;
                _storey = BuildingInterior.StoreyHeight(height);
                _floors = BuildingInterior.FloorCountFor(height);
                _top = site.BaseHeight + BuildingInterior.FloorTop(_floors - 1, _floors, height, _storey);
                foreach (RoomLightPlan plan in plans)
                {
                    var glow = new RoomGlow
                    {
                        Key = plan.Key, Range = plan.LightRange, Intensity = plan.LightIntensity, Colour = plan.Colour,
                        BounceRange = plan.BounceRange, BounceIntensity = plan.BounceIntensity, Plan = plan,
                    };
                    glow.Glow.SetFrame(offset, site.AxisX, site.AxisZ);
                    glow.Windows.SetFrame(offset, site.AxisX, site.AxisZ);
                    Glows[plan.Key] = glow;
                    if (plan.Key.Room == 1)
                    {
                        _cornerRooms[plan.Key.Floor] = plan.Area;
                    }
                }
            }

            /// <summary>The rooms of a building with power, ready to collect its window glow; null for one without (its windows never light).</summary>
            public static RoomWindowGlow For(BuildingSite site, Vector3 offset)
            {
                if (!PowerGrid.IsOn(site))
                {
                    return null;
                }
                List<RoomLightPlan> plans = RoomLighting.PlansFor(site);
                return plans.Count == 0 ? null : new RoomWindowGlow(site, plans, offset);
            }

            /// <summary>A window pane (in the builder's frame) lit by the room behind it.</summary>
            public void AddPane(in Wall wall, float u0, float u1, float y0, float y1, Vector3 normal)
            {
                Vector3 centre = (wall.At(u0, y0) + wall.At(u1, y1)) * 0.5f;
                if (centre.y < _site.BaseHeight || centre.y > _top)
                {
                    return;   // the podium below the floors, or a sealed top with no rooms in it
                }
                int floor = Mathf.Clamp(Mathf.FloorToInt((centre.y - _site.BaseHeight) / _storey), 0, _floors - 1);
                int room = 0;
                if (_cornerRooms.TryGetValue(floor, out Rect corner))
                {
                    // The pane is in the outer wall, just outside the room's own rectangle.
                    float reach = BuildingInterior.WallThickness + 0.3f;
                    Rect near = Rect.MinMaxRect(corner.xMin - reach, corner.yMin - reach, corner.xMax + reach, corner.yMax + reach);
                    if (near.Contains(new Vector2(centre.x, centre.z)))
                    {
                        room = 1;
                    }
                }
                if (!Glows.TryGetValue(new RoomKey(_site.Id, floor, room), out RoomGlow glow))
                {
                    return;
                }
                // UV'd 0..1 across the pane: the window light (Shaders/CityXWindowLight.shader) is brighter towards the top, where the lit ceiling is.
                glow.Windows.AddQuad(wall.At(u0, y0), wall.At(u1, y0), wall.At(u1, y1), wall.At(u0, y1), normal,
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            }
        }

        private static readonly Color32 FittingLook = SurfaceLook.Pack(new Color(0.86f, 0.87f, 0.89f), (int)WallMaterial.Metal, 0.12f, indoors: true);
        private static readonly Color32 SwitchLook = SurfaceLook.Pack(new Color(0.93f, 0.92f, 0.88f), (int)WallMaterial.Plaster, 0.05f, indoors: true);
        private static readonly Color32 ToggleLook = SurfaceLook.Pack(new Color(0.70f, 0.70f, 0.68f), (int)WallMaterial.Metal, 0.1f, indoors: true);

        /// <summary>
        /// A building's fittings and switches into <paramref name="interior"/> (already framed to the building); for a powered one
        /// (<paramref name="windowGlow"/>, whose window glow is already collected) its rooms' glow into <paramref name="glows"/>.
        /// <paramref name="framed"/> is any buffer carrying the building's frame, to place the lights.
        /// </summary>
        private static void AddRoomLighting(BuildingSite site, MeshBuffer interior, MeshBuffer framed, Vector3 offset, List<RoomGlow> glows,
            RoomWindowGlow windowGlow)
        {
            List<RoomLightPlan> plans = windowGlow != null ? windowGlow.Plans : RoomLighting.PlansFor(site);
            if (plans.Count == 0)
            {
                return;
            }
            Color32 saved = interior.Look;
            foreach (RoomLightPlan plan in plans)
            {
                interior.Look = FittingLook;
                foreach (Fixture f in plan.Fixtures)
                {
                    interior.AddBox(f.HousingMin, f.HousingMax, true);
                    if (f.HasCord)
                    {
                        interior.AddBox(f.CordMin, f.CordMax);
                    }
                }
                AddSwitchPlate(interior, plan.Switch, plan.SwitchFacing);

                if (windowGlow != null && glows != null && plan.Fixtures.Count > 0 && windowGlow.Glows.TryGetValue(plan.Key, out RoomGlow glow))
                {
                    foreach (Fixture f in plan.Fixtures)
                    {
                        glow.Glow.AddBox(f.GlowMin, new Vector3(f.GlowMax.x, f.GlowMin.y + 0.004f, f.GlowMax.z), true);
                    }
                    glow.Glow.ClearFrame();
                    glow.Windows.ClearFrame();
                    glow.Lights = new Vector3[plan.LightPoints.Count];
                    for (int i = 0; i < glow.Lights.Length; i++)
                    {
                        glow.Lights[i] = framed.FramePoint(plan.LightPoints[i]);
                    }
                    glow.Bounces = new Vector3[plan.BouncePoints.Count];
                    for (int i = 0; i < glow.Bounces.Length; i++)
                    {
                        glow.Bounces[i] = framed.FramePoint(plan.BouncePoints[i]);
                    }
                    glow.FrameOrigin = framed.FramePoint(Vector3.zero);
                    glow.FrameX = framed.FrameDirection(Vector3.right);
                    glow.FrameZ = framed.FrameDirection(Vector3.forward);
                    glows.Add(glow);
                }
            }
            interior.Look = saved;
        }

        /// <summary>The switch: a plate on the wall and the rocker on it, facing into the room.</summary>
        private static void AddSwitchPlate(MeshBuffer buffer, Vector3 centre, Vector3 facing)
        {
            float s = RoomLighting.SwitchSize * 0.5f, d = RoomLighting.SwitchDepth * 0.5f;
            bool alongZ = Mathf.Abs(facing.z) > Mathf.Abs(facing.x);
            Vector3 half = alongZ ? new Vector3(s, s, d) : new Vector3(d, s, s);
            buffer.Look = SwitchLook;
            buffer.AddBox(centre - half, centre + half, true);
            Vector3 rocker = alongZ ? new Vector3(0.035f, 0.06f, 0.02f) : new Vector3(0.02f, 0.06f, 0.035f);
            Vector3 at = centre + facing * (d + 0.015f);
            buffer.Look = ToggleLook;
            buffer.AddBox(at - rocker, at + rocker, true);
        }
    }
}
