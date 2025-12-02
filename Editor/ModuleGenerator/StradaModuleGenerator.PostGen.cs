using System;
using System.IO;
using System.Linq;
using Strada.Core.Bootstrap;
using Strada.Core.Modules;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.ModuleGenerator
{
    /// <summary>
    /// Handles post-generation operations after script recompilation.
    /// </summary>
    [InitializeOnLoad]
    public static class ModuleGeneratorPostProcessor
    {
        static ModuleGeneratorPostProcessor()
        {
            EditorApplication.delayCall += ProcessPendingOperations;
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            EditorApplication.delayCall += ProcessPendingOperations;
        }

        private static void ProcessPendingOperations()
        {
            ProcessPendingModuleConfigAsset();
            ProcessPendingBootstrapperRegistration();
        }

        private static void ProcessPendingModuleConfigAsset()
        {
            var json = EditorPrefs.GetString("Strada_PendingModuleConfigAsset", "");
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<PendingModuleData>(json);
            if (data == null) return;

            EditorPrefs.DeleteKey("Strada_PendingModuleConfigAsset");

            var configType = FindType(data.ConfigClassName, data.Namespace);
            if (configType == null)
            {
                Debug.LogWarning($"[StradaGenerator] Could not find ModuleConfig type: {data.ConfigClassName}");
                return;
            }

            var configPath = $"{data.ModulePath}/Resources/Configs/{data.ModuleName}ModuleConfig.asset";

            var existingConfig = AssetDatabase.LoadAssetAtPath<ScriptableObject>(configPath);
            if (existingConfig != null)
            {
                Debug.Log($"[StradaGenerator] ModuleConfig asset already exists: {configPath}");
                return;
            }

            var configDir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            var instance = ScriptableObject.CreateInstance(configType);
            AssetDatabase.CreateAsset(instance, configPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[StradaGenerator] Created ModuleConfig asset: {configPath}");

            EditorGUIUtility.PingObject(instance);
        }

        private static void ProcessPendingBootstrapperRegistration()
        {
            var json = EditorPrefs.GetString("Strada_PendingModuleRegistration", "");
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<PendingModuleData>(json);
            if (data == null) return;

            EditorPrefs.DeleteKey("Strada_PendingModuleRegistration");

            var bootstrapperGuids = AssetDatabase.FindAssets("t:GameBootstrapperConfig");
            if (bootstrapperGuids.Length == 0)
            {
                Debug.LogWarning("[StradaGenerator] No GameBootstrapperConfig found in project.");
                return;
            }

            var moduleConfigPath = $"{data.ModulePath}/Resources/Configs/{data.ModuleName}ModuleConfig.asset";
            var moduleConfig = AssetDatabase.LoadAssetAtPath<ModuleConfig>(moduleConfigPath);

            if (moduleConfig == null)
            {
                Debug.LogWarning($"[StradaGenerator] ModuleConfig not found at: {moduleConfigPath}");
                return;
            }

            foreach (var guid in bootstrapperGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var bootstrapperConfig = AssetDatabase.LoadAssetAtPath<GameBootstrapperConfig>(path);

                if (bootstrapperConfig == null) continue;

                var serializedObj = new SerializedObject(bootstrapperConfig);
                var modulesProperty = serializedObj.FindProperty("_modules");

                if (modulesProperty == null)
                {
                    Debug.LogWarning("[StradaGenerator] Could not find _modules property on GameBootstrapperConfig");
                    continue;
                }

                bool alreadyRegistered = false;
                for (int i = 0; i < modulesProperty.arraySize; i++)
                {
                    var element = modulesProperty.GetArrayElementAtIndex(i);
                    var configProp = element.FindPropertyRelative("_moduleConfig");
                    if (configProp != null && configProp.objectReferenceValue == moduleConfig)
                    {
                        alreadyRegistered = true;
                        break;
                    }
                }

                if (alreadyRegistered)
                {
                    Debug.Log($"[StradaGenerator] Module already registered in: {path}");
                    continue;
                }

                modulesProperty.arraySize++;
                var newElement = modulesProperty.GetArrayElementAtIndex(modulesProperty.arraySize - 1);

                var newConfigProp = newElement.FindPropertyRelative("_moduleConfig");
                if (newConfigProp != null)
                {
                    newConfigProp.objectReferenceValue = moduleConfig;
                }

                var enabledProp = newElement.FindPropertyRelative("_enabled");
                if (enabledProp != null)
                {
                    enabledProp.boolValue = true;
                }

                var priorityProp = newElement.FindPropertyRelative("_priority");
                if (priorityProp != null)
                {
                    priorityProp.intValue = modulesProperty.arraySize * 10;
                }

                serializedObj.ApplyModifiedProperties();

                Debug.Log($"[StradaGenerator] Registered {data.ModuleName} module in: {path}");
            }

            AssetDatabase.SaveAssets();
        }

        private static Type FindType(string typeName, string ns)
        {
            var fullName = $"{ns}.{typeName}";

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var type = assembly.GetType(fullName);
                    if (type != null)
                        return type;

                    type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                    if (type != null)
                        return type;
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
