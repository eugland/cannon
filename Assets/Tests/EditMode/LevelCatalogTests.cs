using System.Linq;
using Cannon.Game;
using NUnit.Framework;

namespace Cannon.Tests.EditMode
{
    public class LevelCatalogTests
    {
        [Test]
        public void ResourceCatalog_HasPlayableObjectReferences()
        {
            LevelCatalog levels = LevelCatalogLoader.LoadLevels();
            ObjectDefinitionCatalog definitions = LevelCatalogLoader.LoadDefinitions();

            Assert.AreEqual(10, levels.levels.Length);
            Assert.GreaterOrEqual(definitions.definitions.Length, 8);

            foreach (LevelRecord level in levels.levels)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(level.id));
                Assert.Greater(level.timeLimit, 0f);
                Assert.AreEqual(1, level.objects.Count(x => ResolveKind(x, definitions) == "cannon"),
                    $"{level.id} must contain exactly one cannon.");
                Assert.Greater(level.objects.Count(x => ResolveKind(x, definitions) == "target"), 0,
                    $"{level.id} must contain at least one target.");
                Assert.Greater(level.objects.Count(x => IsGravityKind(ResolveKind(x, definitions))), 0,
                    $"{level.id} must contain at least one gravity body.");

                foreach (LevelObjectRecord item in level.objects)
                    Assert.DoesNotThrow(() => LevelCatalogLoader.Resolve(item, definitions));
            }
        }

        [Test]
        public void InstanceMetrics_OverrideDefinitionDefaults()
        {
            var definitions = new ObjectDefinitionCatalog
            {
                definitions = new[]
                {
                    new ObjectDefinitionRecord { id = "planet", kind = "planet", radius = 2f, mass = 4f }
                }
            };
            var instance = new LevelObjectRecord { definitionId = "planet", radius = 5f };

            ResolvedLevelObject resolved = LevelCatalogLoader.Resolve(instance, definitions);

            Assert.AreEqual(5f, resolved.Radius);
            Assert.AreEqual(4f, resolved.Mass);
            Assert.AreEqual("planet", resolved.Kind);
        }

        [Test]
        public void UnknownDefinition_IsRejected()
        {
            var instance = new LevelObjectRecord { definitionId = "missing" };
            var definitions = new ObjectDefinitionCatalog();

            Assert.Throws<System.InvalidOperationException>(() =>
                LevelCatalogLoader.Resolve(instance, definitions));
        }

        private static string ResolveKind(LevelObjectRecord item, ObjectDefinitionCatalog definitions)
            => LevelCatalogLoader.Resolve(item, definitions).Kind;

        private static bool IsGravityKind(string kind)
            => kind == "planet" || kind == "sun" || kind == "blackHole" || kind == "moon";
    }
}
