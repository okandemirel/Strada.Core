using System.IO;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.ModuleGenerator.Config
{
    /// <summary>
    /// Global settings for the Strada Module Generator.
    /// </summary>
    public class StradaGeneratorSettings : ScriptableObject
    {
        private const string SettingsPath = "Assets/Editor/StradaGeneratorSettings.asset";

        [Header("Defaults")]
        [SerializeField] private string _defaultNamespace = "Game.Modules";
        [SerializeField] private string _defaultTargetPath = "Assets/Modules";

        [Header("Generation Options")]
        [SerializeField] private bool _generateFileHeaders = false;
        [SerializeField] private bool _generateSummaries = true;
        [SerializeField] private bool _autoRegisterInBootstrapper = true;
        [SerializeField] private bool _autoCreateModuleConfigAsset = true;
        [SerializeField] private bool _openFolderAfterGeneration = true;

        [Header("Template Settings")]
        [SerializeField] private string _customTemplatesPath = "";

        public string DefaultNamespace => _defaultNamespace;
        public string DefaultTargetPath => _defaultTargetPath;
        public bool GenerateFileHeaders => _generateFileHeaders;
        public bool GenerateSummaries => _generateSummaries;
        public bool AutoRegisterInBootstrapper => _autoRegisterInBootstrapper;
        public bool AutoCreateModuleConfigAsset => _autoCreateModuleConfigAsset;
        public bool OpenFolderAfterGeneration => _openFolderAfterGeneration;
        public string CustomTemplatesPath => _customTemplatesPath;

        private static StradaGeneratorSettings _instance;

        public static StradaGeneratorSettings GetOrCreateSettings()
        {
            if (_instance != null)
                return _instance;

            _instance = AssetDatabase.LoadAssetAtPath<StradaGeneratorSettings>(SettingsPath);

            if (_instance == null)
            {
                var guids = AssetDatabase.FindAssets("t:StradaGeneratorSettings");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = AssetDatabase.LoadAssetAtPath<StradaGeneratorSettings>(path);
                }
            }

            if (_instance == null)
            {
                _instance = CreateInstance<StradaGeneratorSettings>();

                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                AssetDatabase.CreateAsset(_instance, SettingsPath);
                AssetDatabase.SaveAssets();
            }

            return _instance;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }

    internal static class StradaGeneratorSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Project/Strada/Module Generator", SettingsScope.Project)
            {
                label = "Module Generator",
                guiHandler = searchContext =>
                {
                    var settings = StradaGeneratorSettings.GetSerializedSettings();

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Default Settings", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(settings.FindProperty("_defaultNamespace"));
                    EditorGUILayout.PropertyField(settings.FindProperty("_defaultTargetPath"));

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Generation Options", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(settings.FindProperty("_generateFileHeaders"));
                    EditorGUILayout.PropertyField(settings.FindProperty("_generateSummaries"));
                    EditorGUILayout.PropertyField(settings.FindProperty("_autoRegisterInBootstrapper"));
                    EditorGUILayout.PropertyField(settings.FindProperty("_autoCreateModuleConfigAsset"));
                    EditorGUILayout.PropertyField(settings.FindProperty("_openFolderAfterGeneration"));

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Templates", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(settings.FindProperty("_customTemplatesPath"));

                    settings.ApplyModifiedProperties();
                },
                keywords = new[] { "Strada", "Module", "Generator", "Template" }
            };

            return provider;
        }
    }
}
