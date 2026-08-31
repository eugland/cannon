using System.IO;
using Cannon.Demo;
using Cannon.Gravity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cannon.EditorTools
{
    /// <summary>
    /// Populates Main.unity with a non-interactive gravity demo (planet + looping
    /// projectile + framed camera) so the build shows the core physics visually.
    /// Run: Unity -batchmode -quit -executeMethod Cannon.EditorTools.BuildDemoScene.Run
    /// </summary>
    public static class BuildDemoScene
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string MainScenePath = ScenesDir + "/Main.unity";

        public static void Run()
        {
            Directory.CreateDirectory(ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera framing the XY playfield, dark space background.
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 0f, -22f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.08f);
            cam.fieldOfView = 60f;

            // Directional light so the lit materials read.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Planet (blue) with a gravity field.
            var planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Planet";
            planet.transform.position = new Vector3(2f, -2f, 0f);
            planet.transform.localScale = Vector3.one * 4f;
            Paint(planet, new Color(0.2f, 0.45f, 0.9f));
            var body = planet.AddComponent<GravityBody>();
            body.Kind = BodyKind.Planet;
            body.Mass = 45f;
            body.FieldRadius = 200f;
            body.Softening = 0.5f;

            // Projectile (orange) that auto-launches on a loop.
            var proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proj.name = "Projectile";
            proj.transform.localScale = Vector3.one * 0.6f;
            Paint(proj, new Color(1f, 0.55f, 0.1f));
            var op = proj.AddComponent<OrbitalProjectile>();
            op.G = 1f;
            op.MaxSpeed = 40f;
            op.MaxLifetime = 6f;
            var demo = proj.AddComponent<DemoLauncher>();
            demo.StartPosition = new Vector3(-7f, 4f, 0f);
            demo.LaunchVelocity = new Vector3(4.5f, -0.5f, 0f);
            demo.ResetAfter = 5f;

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[BuildDemoScene] Demo scene written to " + MainScenePath);
        }

        private static void Paint(GameObject go, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
