using System.Collections.Generic;
using Cannon.Gravity;
using NUnit.Framework;
using UnityEngine;

namespace Cannon.Tests.EditMode
{
    public class GravityFieldTests
    {
        private const float G = 1f;

        [Test]
        public void NoWells_ProducesZeroAcceleration()
        {
            var accel = GravityField.ComputeAcceleration(Vector3.zero, G, new List<GravityWell>());
            Assert.AreEqual(Vector3.zero, accel);
        }

        [Test]
        public void NullWells_ProducesZeroAcceleration()
        {
            var accel = GravityField.ComputeAcceleration(Vector3.zero, G, null);
            Assert.AreEqual(Vector3.zero, accel);
        }

        [Test]
        public void OutsideFieldRadius_ProducesZeroAcceleration()
        {
            var wells = new List<GravityWell>
            {
                new GravityWell(new Vector3(100f, 0f, 0f), mass: 10f, fieldRadius: 5f, softening: 0.1f)
            };
            var accel = GravityField.ComputeAcceleration(Vector3.zero, G, wells);
            Assert.AreEqual(Vector3.zero, accel, "Well far beyond its field radius must not pull.");
        }

        [Test]
        public void InsideField_AccelerationPointsTowardWell()
        {
            var wells = new List<GravityWell>
            {
                new GravityWell(new Vector3(10f, 0f, 0f), mass: 10f, fieldRadius: 50f, softening: 0.1f)
            };
            var accel = GravityField.ComputeAcceleration(Vector3.zero, G, wells);
            Assert.Greater(accel.x, 0f, "Should accelerate toward the well (+x).");
            Assert.AreEqual(0f, accel.y, 1e-4f);
            Assert.AreEqual(0f, accel.z, 1e-4f);
        }

        [Test]
        public void Acceleration_MatchesClosedFormMagnitude()
        {
            // Single well at distance 4 on +x, mass 8, well within field, tiny softening.
            var well = new GravityWell(new Vector3(4f, 0f, 0f), mass: 8f, fieldRadius: 50f, softening: 0f);
            var accel = GravityField.ComputeAcceleration(Vector3.zero, G, new List<GravityWell> { well });

            float r = 4f;
            float expected = G * 8f / (r * r); // magnitude for softening = 0
            Assert.AreEqual(expected, accel.magnitude, 1e-3f);
        }

        [Test]
        public void Softening_KeepsAccelerationFiniteAtCenter()
        {
            var wells = new List<GravityWell>
            {
                new GravityWell(Vector3.zero, mass: 30f, fieldRadius: 10f, softening: 0.5f)
            };
            var accel = GravityField.ComputeAcceleration(Vector3.zero, G, wells);
            Assert.IsFalse(float.IsNaN(accel.magnitude));
            Assert.IsFalse(float.IsInfinity(accel.magnitude));
            Assert.AreEqual(0f, accel.magnitude, 1e-4f, "At the exact center, symmetric delta is zero → zero accel.");
        }

        [Test]
        public void Step_WithNoWells_MovesInStraightLine()
        {
            var pos = Vector3.zero;
            var vel = new Vector3(2f, 0f, 0f);
            var wells = new List<GravityWell>();
            const float dt = 0.02f;

            for (int i = 0; i < 50; i++)
                GravityField.Step(ref pos, ref vel, G, wells, dt);

            // Zero-G: velocity unchanged, position advanced by v * dt * steps.
            Assert.AreEqual(new Vector3(2f, 0f, 0f), vel);
            Assert.AreEqual(2f * dt * 50, pos.x, 1e-4f);
            Assert.AreEqual(0f, pos.y, 1e-4f);
        }

        [Test]
        public void Step_InsideField_CurvesTowardWell()
        {
            var pos = Vector3.zero;
            var vel = new Vector3(2f, 0f, 0f); // moving along +x
            var wells = new List<GravityWell>
            {
                new GravityWell(new Vector3(5f, -5f, 0f), mass: 20f, fieldRadius: 50f, softening: 0.2f)
            };
            const float dt = 0.02f;

            for (int i = 0; i < 50; i++)
                GravityField.Step(ref pos, ref vel, G, wells, dt);

            // Well is below (−y), so the path must bend downward.
            Assert.Less(pos.y, 0f, "Path should curve toward the well in −y.");
        }
    }
}
