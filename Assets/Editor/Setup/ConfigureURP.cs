using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cannon.EditorTools
{
    /// <summary>
    /// Headless one-shot: create a URP pipeline + renderer asset and make it the
    /// active render pipeline for graphics and every quality level.
    /// Run with: Unity -batchmode -quit -executeMethod Cannon.EditorTools.ConfigureURP.Run
    /// </summary>
    public static class ConfigureURP
    {
        private const string SettingsDir = "Assets/Settings";
        private const string RendererPath = SettingsDir + "/UniversalRenderer.asset";
        private const string PipelinePath = SettingsDir + "/UniversalRenderPipeline.asset";

        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(SettingsDir))
            {
                Directory.CreateDirectory(SettingsDir);
                AssetDatabase.Refresh();
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                AssetDatabase.SaveAssets();
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;

            for (int i = 0; i < QualitySettings.count; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ConfigureURP] URP set as active render pipeline: " + PipelinePath);
        }
    }
}
