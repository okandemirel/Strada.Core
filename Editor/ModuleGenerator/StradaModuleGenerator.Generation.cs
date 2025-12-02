using Strada.Core.Editor.ModuleGenerator.Models;
using Strada.Core.Editor.ModuleGenerator.Pipeline;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.ModuleGenerator
{
    public partial class StradaModuleGenerator
    {
        private void StartGeneration()
        {
            if (!CanGenerate())
            {
                Debug.LogWarning("[StradaGenerator] Cannot generate - validation failed.");
                return;
            }

            _generationState = GenerationState.InProgress;

            var context = new GenerationContext(_moduleDefinition);
            var result = _pipeline.Execute(context);

            if (result.Success)
            {
                _generationState = GenerationState.Completed;
                _lastGeneratedModulePath = _moduleDefinition.FullPath;

                Debug.Log($"[StradaGenerator] Successfully created module: {_moduleDefinition.ModuleName}");
                Debug.Log($"[StradaGenerator] Created {result.CreatedFiles.Count} files in {result.CreatedFolders.Count} folders");

                foreach (var step in result.StepResults)
                {
                    Debug.Log($"[StradaGenerator]   - {step.Step}: {step.Message}");
                }

                if (_moduleDefinition.RegisterInBootstrapper && context.RequiresRecompilation)
                {
                    SavePendingRegistration();
                }

                if (_moduleDefinition.CreateModuleConfigAsset && context.RequiresRecompilation)
                {
                    SavePendingAssetCreation();
                }

                if (_moduleDefinition.OpenFolderAfterCreate)
                {
                    OpenGeneratedModule();
                }

                ShowCompletionDialog(result);

                _moduleDefinition.Reset();
                _generationState = GenerationState.Idle;
            }
            else
            {
                _generationState = GenerationState.Failed;
                Debug.LogError($"[StradaGenerator] Generation failed: {result.ErrorMessage}");
                EditorUtility.DisplayDialog("Generation Failed", result.ErrorMessage, "OK");

                EditorApplication.delayCall += () => _generationState = GenerationState.Idle;
            }
        }

        private void OpenGeneratedModule()
        {
            if (string.IsNullOrEmpty(_lastGeneratedModulePath)) return;

            var asset = AssetDatabase.LoadAssetAtPath<Object>(_lastGeneratedModulePath);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
        }

        private void ShowCompletionDialog(GenerationResult result)
        {
            var message = $"Successfully created {_moduleDefinition.ModuleName} module!\n\n" +
                         $"Location: {_moduleDefinition.FullPath}\n" +
                         $"Files: {result.CreatedFiles.Count}\n" +
                         $"Folders: {result.CreatedFolders.Count}";

            if (_moduleDefinition.RegisterInBootstrapper)
            {
                message += "\n\nNote: Module will be registered in GameBootstrapper after script recompilation.";
            }

            if (_moduleDefinition.CreateModuleConfigAsset)
            {
                message += "\nModuleConfig asset will be created after script recompilation.";
            }

            EditorUtility.DisplayDialog("Module Created", message, "OK");
        }

        private void SavePendingRegistration()
        {
            var data = new PendingModuleData
            {
                ModuleName = _moduleDefinition.ModuleName,
                ModulePath = _moduleDefinition.FullPath,
                Namespace = _moduleDefinition.FullNamespace,
                ConfigClassName = $"{_moduleDefinition.ModuleName}ModuleConfig"
            };

            var json = JsonUtility.ToJson(data);
            EditorPrefs.SetString("Strada_PendingModuleRegistration", json);
        }

        private void SavePendingAssetCreation()
        {
            var data = new PendingModuleData
            {
                ModuleName = _moduleDefinition.ModuleName,
                ModulePath = _moduleDefinition.FullPath,
                Namespace = _moduleDefinition.FullNamespace,
                ConfigClassName = $"{_moduleDefinition.ModuleName}ModuleConfig"
            };

            var json = JsonUtility.ToJson(data);
            EditorPrefs.SetString("Strada_PendingModuleConfigAsset", json);
        }
    }

    [System.Serializable]
    public class PendingModuleData
    {
        public string ModuleName;
        public string ModulePath;
        public string Namespace;
        public string ConfigClassName;
    }
}
