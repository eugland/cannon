using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using Cannon.Game;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Cannon.EditorTools
{
    public static class LevelEditorLauncher
    {
        private const string EditorUrl = "http://127.0.0.1:4173";
        private const string LevelsPath = "Assets/Resources/LevelEditor/levels.json";
        private const string DefinitionsPath = "Assets/Resources/LevelEditor/objects.json";
        private static double _serverDeadline;

        [MenuItem("Cannon/Level Editor/Open Web Editor")]
        public static void OpenWebEditor()
        {
            if (IsServerRunning())
            {
                Application.OpenURL(EditorUrl);
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            string toolRoot = Path.Combine(projectRoot ?? string.Empty, "tools", "level-editor");
            string server = Path.Combine(toolRoot, "server.js");
            if (!File.Exists(server))
            {
                EditorUtility.DisplayDialog("Cannon Level Editor", $"Missing {server}", "OK");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "server.js",
                    WorkingDirectory = toolRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Cannon Level Editor",
                    $"Could not start Node.js server.\n\n{exception.Message}", "OK");
                return;
            }

            _serverDeadline = EditorApplication.timeSinceStartup + 3d;
            EditorApplication.update -= OpenWhenServerReady;
            EditorApplication.update += OpenWhenServerReady;
        }

        [MenuItem("Cannon/Level Editor/Select JSON Records")]
        public static void SelectJsonRecords()
        {
            var levels = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelsPath);
            var definitions = AssetDatabase.LoadAssetAtPath<TextAsset>(DefinitionsPath);
            Selection.objects = new UnityEngine.Object[] { levels, definitions };
            EditorGUIUtility.PingObject(levels);
        }

        [MenuItem("Cannon/Level Editor/Validate JSON Records")]
        public static void ValidateJsonRecords()
        {
            try
            {
                LevelCatalog levels = LevelCatalogLoader.LoadLevels();
                ObjectDefinitionCatalog definitions = LevelCatalogLoader.LoadDefinitions();
                foreach (LevelRecord level in levels.levels)
                foreach (LevelObjectRecord item in level.objects)
                    LevelCatalogLoader.Resolve(item, definitions);

                Debug.Log($"[LevelEditor] Validated {levels.levels.Length} levels and " +
                          $"{definitions.definitions.Length} reusable assets.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Cannon Level Editor", exception.Message, "OK");
            }
        }

        private static bool IsServerRunning()
        {
            try
            {
                using var client = new TcpClient();
                IAsyncResult connection = client.BeginConnect("127.0.0.1", 4173, null, null);
                if (!connection.AsyncWaitHandle.WaitOne(200)) return false;
                client.EndConnect(connection);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void OpenWhenServerReady()
        {
            if (!IsServerRunning() && EditorApplication.timeSinceStartup < _serverDeadline) return;
            EditorApplication.update -= OpenWhenServerReady;
            if (IsServerRunning()) Application.OpenURL(EditorUrl);
            else EditorUtility.DisplayDialog("Cannon Level Editor",
                "Node.js server did not start on 127.0.0.1:4173.", "OK");
        }
    }
}
