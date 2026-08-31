using System.Collections.Generic;
using Cannon.Gravity;
using NUnit.Framework;
using UnityEngine;

namespace Cannon.Tests.EditMode
{
    // Note: OnEnable/OnDisable lifecycle is not reliably driven in EditMode tests,
    // so registration is exercised via the explicit registry API here; the MonoBehaviour
    // auto-register-on-enable path is covered by a PlayMode test.
    public class GravityBodyTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            GravityRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            GravityRegistry.Clear();
        }

        private GravityBody NewBody(Vector3 pos, float mass, float fieldRadius, BodyKind kind = BodyKind.Planet)
        {
            var go = new GameObject("Body");
            go.transform.position = pos;
            var body = go.AddComponent<GravityBody>();
            body.Mass = mass;
            body.FieldRadius = fieldRadius;
            body.Softening = 0.2f;
            body.Kind = kind;
            _spawned.Add(go);
            return body;
        }

        [Test]
        public void Register_AddsBody_Unregister_Removes()
        {
            var body = NewBody(Vector3.zero, 1f, 5f);
            GravityRegistry.Register(body);
            Assert.AreEqual(1, GravityRegistry.ActiveBodies.Count);

            GravityRegistry.Unregister(body);
            Assert.AreEqual(0, GravityRegistry.ActiveBodies.Count);
        }

        [Test]
        public void Register_IsIdempotent()
        {
            var body = NewBody(Vector3.zero, 1f, 5f);
            GravityRegistry.Register(body);
            GravityRegistry.Register(body);
            Assert.AreEqual(1, GravityRegistry.ActiveBodies.Count);
        }

        [Test]
        public void ToWell_ReflectsTransformAndTuning()
        {
            var body = NewBody(new Vector3(3f, 4f, 0f), mass: 12f, fieldRadius: 20f);
            GravityWell well = body.ToWell();
            Assert.AreEqual(new Vector3(3f, 4f, 0f), well.Position);
            Assert.AreEqual(12f, well.Mass, 1e-4f);
            Assert.AreEqual(20f, well.FieldRadius, 1e-4f);
        }

        [Test]
        public void CollectWells_ReturnsAllRegisteredBodies()
        {
            GravityRegistry.Register(NewBody(Vector3.zero, 1f, 5f));
            GravityRegistry.Register(NewBody(new Vector3(10f, 0f, 0f), 8f, 15f));

            var wells = new List<GravityWell>();
            GravityRegistry.CollectWells(wells);
            Assert.AreEqual(2, wells.Count);
        }

        [Test]
        public void SunAndBlackHole_AreLethal_PlanetIsNot()
        {
            Assert.IsTrue(NewBody(Vector3.zero, 10f, 20f, BodyKind.Sun).IsLethal);
            Assert.IsTrue(NewBody(Vector3.zero, 30f, 10f, BodyKind.BlackHole).IsLethal);
            Assert.IsFalse(NewBody(Vector3.zero, 1f, 5f, BodyKind.Planet).IsLethal);
        }
    }
}
