using System;
using UnityEngine;

namespace Cannon.Game
{
    [Serializable]
    public sealed class LevelCatalog
    {
        public LevelRecord[] levels = Array.Empty<LevelRecord>();
    }

    [Serializable]
    public sealed class ObjectDefinitionCatalog
    {
        public ObjectDefinitionRecord[] definitions = Array.Empty<ObjectDefinitionRecord>();
    }

    [Serializable]
    public sealed class ObjectDefinitionRecord
    {
        public string id;
        public string name;
        public string kind;
        public string color;
        public float radius;
        public float width = 1f;
        public float height = 1f;
        public float mass;
        public float fieldRadius;
        public float softening = 0.4f;
        public float hitPoints = 1f;
        public float damageThreshold = 1f;
    }

    [Serializable]
    public sealed class LevelRecord
    {
        public string id;
        public string name;
        public int par = 3;
        public float timeLimit = 180f;
        public LevelObjectRecord[] objects = Array.Empty<LevelObjectRecord>();
    }

    [Serializable]
    public sealed class LevelObjectRecord
    {
        public string id;
        public string definitionId;
        public string kind;
        public string color;
        public float x;
        public float y;
        public float z;
        public float rotation;
        public float scale = 1f;
        public float radius;
        public float width;
        public float height;
        public float mass;
        public float fieldRadius;
        public float softening;
        public float hitPoints;
        public float damageThreshold;
        public float surfaceCenterX;
        public float surfaceCenterY;
        public float orbitRadius;
        public float orbitSpeed;
        public float startAngle;
    }

    public sealed class ResolvedLevelObject
    {
        public LevelObjectRecord Instance { get; }
        public ObjectDefinitionRecord Definition { get; }

        public string Kind => Value(Instance.kind, Definition.kind);
        public string Color => Value(Instance.color, Definition.color);
        public float Radius => Positive(Instance.radius, Definition.radius);
        public float Width => Positive(Instance.width, Definition.width);
        public float Height => Positive(Instance.height, Definition.height);
        public float Mass => Positive(Instance.mass, Definition.mass);
        public float FieldRadius => Positive(Instance.fieldRadius, Definition.fieldRadius);
        public float Softening => Positive(Instance.softening, Definition.softening);
        public float HitPoints => Positive(Instance.hitPoints, Definition.hitPoints);
        public float DamageThreshold => Positive(Instance.damageThreshold, Definition.damageThreshold);
        public float Scale => Instance.scale > 0f ? Instance.scale : 1f;

        public ResolvedLevelObject(LevelObjectRecord instance, ObjectDefinitionRecord definition)
        {
            Instance = instance;
            Definition = definition;
        }

        private static string Value(string instanceValue, string definitionValue)
            => string.IsNullOrWhiteSpace(instanceValue) ? definitionValue : instanceValue;

        private static float Positive(float instanceValue, float definitionValue)
            => instanceValue > 0f ? instanceValue : definitionValue;
    }

    public static class LevelCatalogLoader
    {
        public const string LevelsResourcePath = "LevelEditor/levels";
        public const string DefinitionsResourcePath = "LevelEditor/objects";

        public static LevelCatalog LoadLevels()
        {
            TextAsset asset = Resources.Load<TextAsset>(LevelsResourcePath);
            if (asset == null)
                throw new InvalidOperationException($"Missing Resources/{LevelsResourcePath}.json");
            return ParseLevels(asset.text);
        }

        public static ObjectDefinitionCatalog LoadDefinitions()
        {
            TextAsset asset = Resources.Load<TextAsset>(DefinitionsResourcePath);
            if (asset == null)
                throw new InvalidOperationException($"Missing Resources/{DefinitionsResourcePath}.json");
            return ParseDefinitions(asset.text);
        }

        public static LevelCatalog ParseLevels(string json)
        {
            LevelCatalog catalog = JsonUtility.FromJson<LevelCatalog>(json);
            if (catalog == null || catalog.levels == null || catalog.levels.Length == 0)
                throw new InvalidOperationException("Level catalog contains no levels.");
            return catalog;
        }

        public static ObjectDefinitionCatalog ParseDefinitions(string json)
        {
            ObjectDefinitionCatalog catalog = JsonUtility.FromJson<ObjectDefinitionCatalog>(json);
            if (catalog == null || catalog.definitions == null || catalog.definitions.Length == 0)
                throw new InvalidOperationException("Object catalog contains no definitions.");
            return catalog;
        }

        public static ResolvedLevelObject Resolve(LevelObjectRecord instance, ObjectDefinitionCatalog catalog)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            ObjectDefinitionRecord definition = null;
            if (catalog?.definitions != null)
            {
                foreach (ObjectDefinitionRecord candidate in catalog.definitions)
                {
                    if (candidate != null && candidate.id == instance.definitionId)
                    {
                        definition = candidate;
                        break;
                    }
                }
            }

            if (definition == null)
                throw new InvalidOperationException($"Unknown object definition '{instance.definitionId}'.");
            return new ResolvedLevelObject(instance, definition);
        }
    }
}
