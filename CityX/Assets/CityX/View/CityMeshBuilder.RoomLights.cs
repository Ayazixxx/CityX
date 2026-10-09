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
        /// <summary>One powered room's lights in a chunk: the glow under its fittings (chunk-local) and where its real lights go.</summary>
        public sealed class RoomGlow
        {
            public RoomKey Key;
            public readonly MeshBuffer Glow = new MeshBuffer();
            public Vector3[] Lights;
            public float Range;
            public float Intensity;
            public Color Colour;
        }

        private static readonly Color32 FittingLook = SurfaceLook.Pack(new Color(0.86f, 0.87f, 0.89f), (int)WallMaterial.Metal, 0.12f);
        private static readonly Color32 SwitchLook = SurfaceLook.Pack(new Color(0.93f, 0.92f, 0.88f), (int)WallMaterial.Plaster, 0.05f);
        private static readonly Color32 ToggleLook = SurfaceLook.Pack(new Color(0.70f, 0.70f, 0.68f), (int)WallMaterial.Metal, 0.1f);

        /// <summary>
        /// A building's fittings and switches into <paramref name="interior"/> (already framed to the building), and its glow into
        /// <paramref name="glows"/> when it has power. <paramref name="framed"/> is any buffer carrying the building's frame, to place the lights.
        /// </summary>
        private static void AddRoomLighting(BuildingSite site, MeshBuffer interior, MeshBuffer framed, Vector3 offset, List<RoomGlow> glows)
        {
            List<RoomLightPlan> plans = RoomLighting.PlansFor(site);
            if (plans.Count == 0)
            {
                return;
            }
            bool powered = glows != null && PowerGrid.IsOn(site);
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

                if (powered && plan.Fixtures.Count > 0)
                {
                    var glow = new RoomGlow
                    {
                        Key = plan.Key,
                        Range = plan.LightRange,
                        Intensity = plan.LightIntensity,
                        Colour = plan.Colour,
                    };
                    glow.Glow.SetFrame(offset, site.AxisX, site.AxisZ);
                    foreach (Fixture f in plan.Fixtures)
                    {
                        glow.Glow.AddBox(f.GlowMin, new Vector3(f.GlowMax.x, f.GlowMin.y + 0.004f, f.GlowMax.z), true);
                    }
                    glow.Glow.ClearFrame();
                    glow.Lights = new Vector3[plan.LightPoints.Count];
                    for (int i = 0; i < glow.Lights.Length; i++)
                    {
                        glow.Lights[i] = framed.FramePoint(plan.LightPoints[i]);
                    }
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
