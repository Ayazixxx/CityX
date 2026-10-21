using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A step street drawn (World/StepStreets: a street too steep to drive, built as steps as San Francisco's are): from the junction plate at one
    /// end to the plate at the other, the carriageway's width in concrete treads following the ground (each a block down past the ground, which
    /// the road's cut has already taken away), a row of bollards across the level pad at each end, and -- where the street is wide -- a stepped
    /// concrete planter down the middle splitting it into two flights (the Lyon Street Steps), each flight with a handrail on posts. Its sides
    /// close against the sloping sidewalks beside it: a retaining face up to the paving where that stands over a tread, a parapet on the
    /// sidewalk's edge where the drop is a fall; the kerb is not drawn along the steps (BuildPavement). No asphalt, no paint, no barricades.
    /// Each tread is built by the chunk its middle is in.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>The planter down a wide step street's middle stands this high over each tread, metres.</summary>
        private const float StepStreetPlanterHeight = 0.5f;

        private static void BuildStepStreet(Context ctx, EdgeGeometry g, MeshBuffer pavement, MeshBuffer structures, bool far)
        {
            List<StairTread> treads = StepStreets.Profile(ctx.Plan, g, out bool fromB);
            double t0 = g.TrimA, t1 = g.Length - g.TrimB, length = t1 - t0;
            if (treads.Count == 0 || length < 1.0) return;
            Vec2 dir = fromB ? -g.Dir : g.Dir, low = g.A + g.Dir * (fromB ? t1 : t0);
            Vec2 Point(double s, double across) => low + dir * s + dir.Perp * across;
            double Ground(Vec2 p) => ctx.Plan.GroundHeight(p.X, p.Y);
            double half = g.HalfWidth;
            bool median = half >= StepStreets.MedianFrom;
            double m = StepStreets.MedianWidth * 0.5;

            for (int i = 0; i < treads.Count; i++)
            {
                StairTread t = treads[i];
                Vec2 middle = Point((t.S0 + t.S1) * 0.5, 0.0);
                if (!ctx.Contains(middle.X, middle.Y)) continue;
                Vec2 a = Point(t.S0, -half), b = Point(t.S1, -half), c = Point(t.S1, half), d = Point(t.S0, half);
                double bottom = Math.Min(t.Top, Math.Min(Math.Min(Ground(a), Ground(b)), Math.Min(Ground(c), Ground(d)))) - StairLaneFooting;
                AddTreadBlock(ctx, pavement, a, b, c, d, t.Top, bottom, StairLaneLook, StairLaneLook);
                // Its sides, where the sloping sidewalk beside stands over the tread: a retaining face up to the paving (the kerb is not drawn
                // along the steps), and a parapet on the sidewalk's edge where the drop is a fall.
                foreach (double across in new[] { -half, half })
                {
                    Vec2 p = Point(t.S0, across), q = Point(t.S1, across);
                    double pp = Ground(p) + PlanBuildings.PavingRise, pq = Ground(q) + PlanBuildings.PavingRise;
                    if (pp <= t.Top + 0.02 && pq <= t.Top + 0.02) continue;
                    Vec2 inward = dir.Perp * -Math.Sign(across);
                    pavement.Look = StairLaneRetaining;
                    float len = (float)(t.S1 - t.S0);
                    pavement.AddQuad(ctx.Local(p.X, t.Top, p.Y), ctx.Local(q.X, t.Top, q.Y), ctx.Local(q.X, Math.Max(pq, t.Top), q.Y), ctx.Local(p.X, Math.Max(pp, t.Top), p.Y),
                        new Vector3((float)inward.X, 0.0f, (float)inward.Y), new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 1), new Vector2(0, 1));
                    if (!far && pp > t.Top + StairLaneParapetFrom && pq > t.Top + StairLaneParapetFrom)
                    {
                        AddParapet(ctx, pavement, p, q, pp, pq, -inward);
                    }
                }
                // The planter down the middle, a box on each tread between the end pads: terraced like the steps.
                if (median && !far && i > 0 && i < treads.Count - 1)
                {
                    AddTreadBlock(ctx, pavement, Point(t.S0, -m), Point(t.S1, -m), Point(t.S1, m), Point(t.S0, m), t.Top + StepStreetPlanterHeight, t.Top,
                        StairLanePlanter, StairLaneEarth);
                }
            }
            if (far) return;

            // A handrail down each flight (down the middle where there is one flight), from pad to pad.
            double from = StepStreets.EndPad, to = length - StepStreets.EndPad;
            if (median)
            {
                double flight = m + (half - m) * 0.5;
                AddHandrail(ctx, pavement, treads, from, to, s => Point(s, -flight));
                AddHandrail(ctx, pavement, treads, from, to, s => Point(s, flight));
            }
            else
            {
                AddHandrail(ctx, pavement, treads, from, to, s => Point(s, 0.0));
            }

            // The bollards across each end's pad, closing it to cars.
            foreach (double s in new[] { StepStreets.BollardInset, length - StepStreets.BollardInset })
            {
                double top = TreadTopAt(treads, s);
                for (double w = -half + StepStreets.BollardSpacing * 0.5; w <= half - StepStreets.BollardSpacing * 0.5 + 1e-6; w += StepStreets.BollardSpacing)
                {
                    Vec2 at = Point(s, w);
                    if (!ctx.Contains(at.X, at.Y)) continue;
                    Vector3 local = ctx.Local(at.X, 0.0, at.Y);
                    CityProps.AddBollard(structures, local.x, local.z, ctx.LocalY(top));
                }
            }
            structures.Look = SurfaceLook.Default;
        }
    }
}
