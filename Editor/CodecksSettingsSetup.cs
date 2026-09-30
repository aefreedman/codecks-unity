using System;
using Codecks.Runtime;
using UnityEditor;
using UnityEngine;

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
