using System.Collections;
using Cannon.Gravity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cannon.Tests.PlayMode
{
    public class OrbitalProjectilePlayTests
    {
        [SetUp]
        public void SetUp()
        {
            GravityRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            GravityRegistry.Clear();
        }

        [UnityTest]
        public IEnumerator GravityBody_RegistersOnEnable_UnregistersOnDisable()
        {
            var go = new GameObject("Planet");
            var body = go.AddComponent<GravityBody>();
            yield return null; // let OnEnable run
            Assert.AreEqual(1, GravityRegistry.ActiveBodies.Count);

            body.enabled = false;
            go.SetActive(false);
            yield return null;
            Assert.AreEqual(0, GravityRegistry.ActiveBodies.Count);

            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Projectile_CurvesTowardBodyInFlight()
        {
            // Planet below the flight path so the shot should bend downward (−y).
            var planet = new GameObject("Planet");
            planet.transform.position = new Vector3(5f, -4f, 0f);
            var body = planet.AddComponent<GravityBody>();
            body.Mass = 30f;
            body.FieldRadius = 100f;
            body.Softening = 0.3f;

            var projGo = new GameObject("Projectile");
            projGo.transform.position = Vector3.zero;
            var proj = projGo.AddComponent<OrbitalProjectile>();
            proj.G = 1f;
            proj.MaxLifetime = 100f;
            proj.Launch(new Vector3(3f, 0f, 0f)); // straight along +x

            yield return null; // allow OnEnable registration

            for (int i = 0; i < 60; i++)
                yield return new WaitForFixedUpdate();

            Assert.Less(projGo.transform.position.y, -0.05f,
                "Projectile should curve toward the planet below (−y).");
            Assert.Greater(projGo.transform.position.x, 0f, "Should still travel forward (+x).");

            Object.Destroy(projGo);
            Object.Destroy(planet);
        }

        [UnityTest]
        public IEnumerator Projectile_TimesOutAfterMaxLifetime()
        {
            var projGo = new GameObject("Projectile");
            var proj = projGo.AddComponent<OrbitalProjectile>();
            proj.MaxLifetime = 0.1f; // times out almost immediately
            bool ended = false;
            proj.Ended += _ => ended = true;
            proj.Launch(new Vector3(1f, 0f, 0f));

            float t = 0f;
            while (!ended && t < 2f)
            {
                t += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(ended, "Projectile should raise Ended after its lifetime.");
            Assert.IsTrue(proj.HasEnded);

            Object.Destroy(projGo);
        }
    }
}
