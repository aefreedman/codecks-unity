using System;
using System.IO;
using System.Linq;
using Codecks.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Codecks.Editor
{
    public static class CodecksSettingsSetup
    {
        internal const string DefaultAssetPath = "Assets/Resources/Codecks/CodecksSettings.asset";

        [MenuItem("Tools/Codecks/Create or Select Report Settings")]
        public static void CreateOrSelectReportSettings()
        {
            var settings = GetOrCreateDefaultAsset();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Tools/Codecks/Set Up Imported Samples")]
        public static void SetUpImportedSamples()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before setting up imported samples.");
            var settings = GetOrCreateDefaultAsset();
            var paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(IsImportedSampleScene).OrderBy(p => p).ToArray();
            int updated = 0, skipped = 0;
            foreach (string path in paths)
            {
                var result = WireClosedSampleScene(path, settings);
                if (result == WiringResult.Updated) updated++;
                if (result == WiringResult.OpenScene)
                {
                    skipped++;
                    Debug.LogWarning("Codecks setup skipped an open sample scene. Close it without discarding your changes, then run setup again: " + path);
                }
            }
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
            Debug.Log($"Codecks sample setup: {updated} scene(s) wired, {skipped} open scene(s) skipped. Existing settings references preserved. Configure the selected settings asset. Import samples first if none were found.");
        }

        internal enum WiringResult { Updated, AlreadyConfigured, OpenScene }

        private static bool IsImportedSampleScene(string path)
        {
            string name = Path.GetFileName(path);
            return path.StartsWith("Assets/Samples/", StringComparison.Ordinal) &&
                (name == "CodecksSampleScene.unity" || name == "CodecksUIToolkitFeedbackReporterScene.unity");
        }

        internal static WiringResult WireClosedSampleScene(string path, CodecksSettings settings)
        {
            if (!IsImportedSampleScene(path))
                throw new InvalidOperationException("Codecks sample setup only edits imported sample scenes under Assets/Samples.");
            if (settings == null || !AssetDatabase.Contains(settings))
                throw new InvalidOperationException("Sample setup requires a saved CodecksSettings asset.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before setting up imported samples.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == path)
                    return WiringResult.OpenScene;

            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var creators = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CodecksCardCreator>(true)).ToArray();
                if (creators.Length == 0)
                    throw new InvalidOperationException("Imported sample scene has no CodecksCardCreator: " + path);
                bool changed = false;
                foreach (var creator in creators)
                {
                    if (creator.settings != null) continue;
                    creator.settings = settings;
                    EditorUtility.SetDirty(creator);
                    changed = true;
                }
                if (!changed) return WiringResult.AlreadyConfigured;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Could not save the closed imported sample scene: " + path);
                return WiringResult.Updated;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }
        }

        internal static CodecksSettings GetOrCreateDefaultAsset()
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(DefaultAssetPath);
            if (existing != null)
            {
                if (existing is CodecksSettings settings)
                    return settings; // Never overwrite or reset an existing token/configuration.
                throw new InvalidOperationException("The default Codecks settings path is occupied by another asset. Move it before creating report settings.");
            }
            if (System.IO.File.Exists(DefaultAssetPath))
                throw new InvalidOperationException("The default Codecks settings path already exists but is not imported. Refresh assets before creating report settings.");

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Codecks"))
                AssetDatabase.CreateFolder("Assets/Resources", "Codecks");
            var created = ScriptableObject.CreateInstance<CodecksSettings>();
            AssetDatabase.CreateAsset(created, DefaultAssetPath);
            return created;
        }
    }
}
