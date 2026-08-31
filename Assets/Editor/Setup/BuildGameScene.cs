using System.IO;
using Cannon.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cannon.EditorTools
{
    /// <summary>
    /// Writes the playable game scene (camera, light, GameManager) to Main.unity.
    /// GameManager builds the actual level contents at runtime.
    /// Run: Unity -batchmode -quit -executeMethod Cannon.EditorTools.BuildGameScene.Run
    /// </summary>
    public static class BuildGameScene
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string MainScenePath = ScenesDir + "/Main.unity";

        public static void Run()
        {
            Directory.CreateDirectory(ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            camGo.transform.position = new Vector3(0f, -1f, -20f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.09f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[BuildGameScene] Playable game scene written to " + MainScenePath);
        }
    }
}
