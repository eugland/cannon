using System.Collections.Generic;
using Cannon.Gravity;
using NUnit.Framework;
using UnityEngine;

namespace Cannon.Tests.EditMode
{
    public class TrajectorySamplerTests
    {
        private const float G = 1f;
        private const float Dt = 0.02f;

        [Test]
        public void NoWells_SamplesStraightLine()
        {
            var pts = new List<Vector3>();
            TrajectorySampler.Sample(Vector3.zero, new Vector3(1f, 0f, 0f), G,
                new List<GravityWell>(), Dt, maxSteps: 100, stride: 10, pts);

            // Start point + one every 10 steps over 100 steps = 11 points.
            Assert.AreEqual(11, pts.Count);
            Assert.AreEqual(Vector3.zero, pts[0]);
            foreach (var p in pts)
                Assert.AreEqual(0f, p.y, 1e-4f); // no vertical drift in zero-G
            Assert.Greater(pts[pts.Count - 1].x, pts[0].x); // moved forward
        }

        [Test]
        public void MatchesManualIntegration()
        {
            var wells = new List<GravityWell>
            {
                new GravityWell(new Vector3(5f, -3f, 0f), mass: 15f, fieldRadius: 100f, softening: 0.2f)
            };

            var pts = new List<Vector3>();
            TrajectorySampler.Sample(Vector3.zero, new Vector3(2f, 0f, 0f), G,
                wells, Dt, maxSteps: 30, stride: 30, pts); // only first and last sample

            // Manually integrate 30 steps and compare to the final sampled point.
            Vector3 pos = Vector3.zero;
            Vector3 vel = new Vector3(2f, 0f, 0f);
            for (int i = 0; i < 30; i++)
                GravityField.Step(ref pos, ref vel, G, wells, Dt);

            Vector3 last = pts[pts.Count - 1];
            Assert.AreEqual(pos.x, last.x, 1e-4f);
            Assert.AreEqual(pos.y, last.y, 1e-4f);
        }

        [Test]
        public void StopAtFieldEntry_TruncatesWhenEnteringField()
        {
            // Well field starts near x=8 (center 10, radius 2). Path along +x from origin.
            var wells = new List<GravityWell>
            {
                new GravityWell(new Vector3(10f, 0f, 0f), mass: 5f, fieldRadius: 2f, softening: 0.1f)
            };

            var full = new List<Vector3>();
            TrajectorySampler.Sample(Vector3.zero, new Vector3(5f, 0f, 0f), G,
                wells, Dt, maxSteps: 1000, stride: 5, full, stopAtFieldEntry: false);

            var truncated = new List<Vector3>();
            TrajectorySampler.Sample(Vector3.zero, new Vector3(5f, 0f, 0f), G,
                wells, Dt, maxSteps: 1000, stride: 5, truncated, stopAtFieldEntry: true);

            Assert.Less(truncated.Count, full.Count, "Truncated path must be shorter.");
            // Final truncated point should be at/just inside the field boundary (x ~ 8).
            Assert.GreaterOrEqual(truncated[truncated.Count - 1].x, 7.5f);
            Assert.LessOrEqual(truncated[truncated.Count - 1].x, 10f);
        }

        [Test]
        public void Stride_ControlsSampleDensity()
        {
            var wells = new List<GravityWell>();
            var dense = new List<Vector3>();
            var sparse = new List<Vector3>();
            TrajectorySampler.Sample(Vector3.zero, Vector3.right, G, wells, Dt, 100, 1, dense);
            TrajectorySampler.Sample(Vector3.zero, Vector3.right, G, wells, Dt, 100, 20, sparse);
            Assert.Greater(dense.Count, sparse.Count);
        }
    }
}
