using Cannon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Cannon.Tests.EditMode
{
    public class StarfieldTests
    {
        [Test]
        public void Build_CreatesRequestedNumberOfStars()
        {
            var go = new GameObject("Starfield");
            var sf = go.AddComponent<Starfield>();

            int made = sf.Build(50);

            Assert.AreEqual(50, made);
            Assert.AreEqual(50, sf.transform.childCount);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Build_IsIdempotent_ReplacesPreviousStars()
        {
            var go = new GameObject("Starfield");
            var sf = go.AddComponent<Starfield>();

            sf.Build(30);
            sf.Build(20); // should replace, not add
            Assert.AreEqual(20, sf.transform.childCount);

            Object.DestroyImmediate(go);
        }
    }
}
