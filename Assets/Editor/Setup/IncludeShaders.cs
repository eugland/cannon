using UnityEditor;
using UnityEngine;

namespace Cannon.EditorTools
{
    /// <summary>
    /// Ships the URP shaders used at runtime without a variant explosion:
    ///  1) removes URP Lit/Unlit from Always-Included (which forces ALL variants), and
    ///  2) creates base material assets in a Resources folder so the build includes the
    ///     shaders with only the variants those materials use.
    /// Run: Unity -batchmode -quit -executeMethod Cannon.EditorTools.IncludeShaders.Run
    /// </summary>
    public static class IncludeShaders
    {
        private const string ResDir = "Assets/Resources";

        public static void Run()
        {
            RemoveHeavyAlwaysIncluded();
            CreateBaseMaterials();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[IncludeShaders] shaders fixed (Resources materials created, heavy always-included removed).");
        }

        private static void RemoveHeavyAlwaysIncluded()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;

            var so = new SerializedObject(assets[0]);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            for (int i = arr.arraySize - 1; i >= 0; i--)
            {
                var shader = arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader != null &&
                    (shader.name == "Universal Render Pipeline/Lit" ||
                     shader.name == "Universal Render Pipeline/Unlit"))
                {
                    arr.DeleteArrayElementAtIndex(i);
                    Debug.Log("[IncludeShaders] removed heavy always-included: " + shader.name);
                }
            }
            so.ApplyModifiedProperties();
        }

        private static void CreateBaseMaterials()
        {
            if (!AssetDatabase.IsValidFolder(ResDir))
                AssetDatabase.CreateFolder("Assets", "Resources");

            CreateMat("LitWhite", "Universal Render Pipeline/Lit");
            CreateMat("UnlitWhite", "Universal Render Pipeline/Unlit");
        }

        private static void CreateMat(string name, string shaderName)
        {
            string path = ResDir + "/" + name + ".mat";
            var shader = Shader.Find(shaderName) ?? Shader.Find("Standard");
            if (shader == null) { Debug.LogWarning("[IncludeShaders] shader not found: " + shaderName); return; }

            var mat = new Material(shader) { color = Color.white };
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log("[IncludeShaders] created " + path + " (" + shaderName + ")");
        }
    }
}
