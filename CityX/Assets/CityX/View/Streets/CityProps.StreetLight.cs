using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>The street light (World/StreetLights, docs/AMERICAN_STREETS.md): the American cobra-head on its tapered steel pole.</summary>
    public static partial class CityProps
    {
        /// <summary>A street light's unlit lens: the grey of glass with nothing behind it.</summary>
        private static readonly Color32 DeadLens = SurfaceLook.Pack(new Color(0.52f, 0.55f, 0.56f), (int)WallMaterial.Plaster, 0.1f);

        private static readonly Color32 LampSteel = SurfaceLook.Pack(new Color(0.50f, 0.52f, 0.53f), (int)WallMaterial.Metal, 0.3f);
        private static readonly Color32 LampHousing = SurfaceLook.Pack(new Color(0.34f, 0.36f, 0.37f), (int)WallMaterial.Metal, 0.3f);

        /// <summary>The cobra-head's size: its housing along the arm, across it and its depth, metres.</summary>
        public const float StreetLampHeadLength = 0.95f, StreetLampHeadWidth = 0.36f, StreetLampHeadDepth = 0.16f;

        /// <summary>
        /// A street light standing at <paramref name="foot"/> (on the pavement), its arm reaching <paramref name="arm"/> metres toward
        /// <paramref name="over"/> with the lamp <paramref name="height"/> up: a base, a pole in two tapers, an arm that rises to the lamp, and
        /// the cobra-head with its lens looking down. With power the lens is drawn in <paramref name="lit"/> and glows at night; without, it is
        /// dead glass.
        /// </summary>
        public static void AddStreetLight(MeshBuffer b, MeshBuffer lit, Vector3 foot, Vec2 over, float height, float arm, bool powered)
        {
            Color32 saved = b.Look;
            float cos = (float)over.Y, sin = (float)-over.X;   // the frame's +z is the way the arm reaches
            b.SetFrame(foot, cos, sin);

            b.Look = LampSteel;
            b.AddBox(new Vector3(-0.24f, -0.1f, -0.24f), new Vector3(0.24f, 0.55f, 0.24f));
            float waist = height * 0.5f, shoulder = height - 1.0f;
            b.AddBox(new Vector3(-0.12f, 0.55f, -0.12f), new Vector3(0.12f, waist, 0.12f));
            b.AddBox(new Vector3(-0.09f, waist, -0.09f), new Vector3(0.09f, shoulder, 0.09f));
            // The arm: up and out from the pole's top, then level to the lamp.
            var top = new Vector3(0.0f, shoulder, 0.0f);
            var elbow = new Vector3(0.0f, height + 0.05f, arm * 0.45f);
            var end = new Vector3(0.0f, height + 0.08f, arm - StreetLampHeadLength * 0.3f);
            AddBeam(b, top, elbow, 0.055f);
            AddBeam(b, elbow, end, 0.05f);

            b.Look = LampHousing;
            float z0 = arm - StreetLampHeadLength * 0.35f, z1 = arm + StreetLampHeadLength * 0.65f, half = StreetLampHeadWidth * 0.5f;
            b.AddBox(new Vector3(-half, height, z0), new Vector3(half, height + StreetLampHeadDepth, z1), true);

            // The lens under the head, toward its outer end.
            MeshBuffer lens = lit != null && powered ? lit : b;
            Color32 savedLens = lens.Look;
            if (lens != b)
            {
                lens.SetFrame(foot, cos, sin);
            }
            lens.Look = lens != b ? LampWarm : DeadLens;
            lens.AddBox(new Vector3(-half * 0.72f, height - 0.06f, arm - 0.05f), new Vector3(half * 0.72f, height, z1 - 0.08f), true);
            lens.Look = savedLens;
            if (lens != b)
            {
                lens.ClearFrame();
            }

            b.Look = saved;
            b.ClearFrame();
        }
    }
}
