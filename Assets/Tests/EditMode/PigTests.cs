using Cannon.Targets;
using NUnit.Framework;
using UnityEngine;

namespace Cannon.Tests.EditMode
{
    public class PigTests
    {
        private GameObject _go;
        private Pig _pig;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Pig");
            _pig = _go.AddComponent<Pig>();
            _pig.HitPoints = 3f;
            _pig.DamageThreshold = 2f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void ImpactBelowThreshold_DoesNoDamage()
        {
            bool killed = _pig.ApplyImpact(1.5f);
            Assert.IsFalse(killed);
            Assert.AreEqual(3f, _pig.HitPoints, 1e-4f);
            Assert.IsFalse(_pig.IsDead);
        }

        [Test]
        public void ImpactAboveThreshold_DamagesByExcess()
        {
            _pig.ApplyImpact(4f); // excess 2 over threshold 2
            Assert.AreEqual(1f, _pig.HitPoints, 1e-4f);
            Assert.IsFalse(_pig.IsDead);
        }

        [Test]
        public void StrongImpact_KillsInOneHit()
        {
            bool killed = _pig.ApplyImpact(10f);
            Assert.IsTrue(killed);
            Assert.IsTrue(_pig.IsDead);
        }

        [Test]
        public void AccumulatedImpacts_EventuallyKill()
        {
            Assert.IsFalse(_pig.ApplyImpact(3f)); // -1 -> 2
            Assert.IsFalse(_pig.ApplyImpact(3f)); // -1 -> 1
            Assert.IsTrue(_pig.ApplyImpact(3f));  // -1 -> 0 => dead
            Assert.IsTrue(_pig.IsDead);
        }

        [Test]
        public void Kill_IsIdempotent_AndRaisesDiedOnce()
        {
            int died = 0;
            _pig.Died += _ => died++;
            _pig.Kill();
            _pig.Kill();
            Assert.AreEqual(1, died);
            Assert.IsTrue(_pig.IsDead);
        }

        [Test]
        public void ImpactAfterDeath_DoesNothing()
        {
            _pig.Kill();
            Assert.IsFalse(_pig.ApplyImpact(100f));
        }
    }
}
