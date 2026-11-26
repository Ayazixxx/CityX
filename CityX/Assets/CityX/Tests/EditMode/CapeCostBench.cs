using System.Diagnostics;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: what one cape's swing costs a frame on the processor (CapeSwing's springs and turning its chain's four bones, what
    /// Cape.LateUpdate does each frame); its mesh is bent by the graphics card with the body's. Logged; for docs/CAPE.md. Explicit.
    /// </summary>
    [Explicit("a timing, logged")]
    public sealed class CapeCostBench
    {
        [Test]
        public void TimeOneCapeAFrame()
        {
            var root = new GameObject("Spine").transform;
            var chain = new Transform[4];
            Transform parent = root;
            for (int k = 0; k < chain.Length; k++)
            {
                chain[k] = new GameObject($"Chain{k}").transform;
                chain[k].SetParent(parent, false);
                chain[k].localPosition = Vector3.down * 0.3f;
                parent = chain[k];
            }
            var swing = new CapeSwing();
            const int frames = 100000;
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                swing.Update(new Vector3(0.3f, 0.1f * (i % 7), 4.0f), 30.0f, i / 60.0f, 1.0f / 60.0f);
                Quaternion bend = Quaternion.AngleAxis(swing.Lift / chain.Length, Vector3.right) * Quaternion.AngleAxis(swing.Sway / chain.Length, Vector3.forward);
                foreach (Transform bone in chain)
                {
                    bone.localRotation = bend;
                }
            }
            double perFrame = clock.Elapsed.TotalMilliseconds / frames;
            UnityEngine.Debug.Log($"CAPE COST swing: {perFrame * 1000.0:0.0} microseconds a frame ({perFrame:0.0000} ms)");
            Object.DestroyImmediate(root.gameObject);
            Assert.Pass();
        }
    }
}
