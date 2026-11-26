using System.Collections.Generic;
using System.Diagnostics;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: what one cape's cloth costs a frame at each level of detail (CapeDetail), with the body's eleven capsules, at 60 frames a
    /// second (one and a half steps a frame). Logged; for docs/CAPE.md. Explicit.
    /// </summary>
    [Explicit("a timing, logged")]
    public sealed class CapeCostBench
    {
        [Test]
        public void TimeOneCapeAFrame()
        {
            Vector3[] rest = CapeShape.RestPoints();
            var capsules = new List<CapeCloth.Capsule>();
            for (int i = 0; i < 11; i++)
            {
                capsules.Add(new CapeCloth.Capsule(new Vector3(0.3f * (i % 3 - 1), 0.4f + 0.15f * i, 0.0f), new Vector3(0.3f * (i % 3 - 1), 0.6f + 0.15f * i, 0.0f), 0.1f));
            }
            var pins = new List<(int, Vector3)>();
            foreach (CapeLevel level in new[] { new CapeLevel("full", CapeDetail.IterationsAt(CapeDetailLevel.Full)), new CapeLevel("light", CapeDetail.LightIterations) })
            {
                CapeCloth cloth = CapeShape.MakeCloth(rest);
                pins.Clear();
                for (int i = 0; i < rest.Length; i++)
                {
                    if (cloth.Pinned[i]) pins.Add((i, rest[i]));
                }
                const int steps = 3000;
                for (int i = 0; i < 100; i++) cloth.Step(pins, rest, capsules, Vector3.right, -10.0f, level.Iterations);   // warmed up
                var clock = Stopwatch.StartNew();
                for (int i = 0; i < steps; i++) cloth.Step(pins, rest, capsules, Vector3.right, -10.0f, level.Iterations);
                double perStep = clock.Elapsed.TotalMilliseconds / steps;
                UnityEngine.Debug.Log($"CAPE COST {level.Name}: {perStep:0.000} ms a step, {perStep * 1.5:0.000} ms a frame at 60 fps ({cloth.LinkCount} links, {rest.Length} points)");
            }
            Assert.Pass();
        }

        private readonly struct CapeLevel
        {
            public readonly string Name;
            public readonly int Iterations;

            public CapeLevel(string name, int iterations)
            {
                Name = name;
                Iterations = iterations;
            }
        }
    }
}
