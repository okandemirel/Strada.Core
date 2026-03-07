using System;
using System.IO;
using Strada.Core.Editor.ModuleGenerator.Config;
using Strada.Core.Editor.ModuleGenerator.Models;
using UnityEditor;

namespace Strada.Core.Editor.ModuleGenerator.Pipeline.Steps
{
    /// <summary>
    /// Creates the folder structure for the module.
    /// </summary>
    public class FolderCreationStep : IGenerationStep
    {
        public string Name => "Folder Creation";
        public int Order => 10;

        public bool CanExecute(GenerationContext context)
        {
            return !string.IsNullOrEmpty(context.Definition.FullPath);
        }

        public StepResult Execute(GenerationContext context)
        {
            var basePath = context.Definition.FullPath;

            if (!Directory.Exists(context.Definition.TargetPath))
            {
                Directory.CreateDirectory(context.Definition.TargetPath);
                context.AddCreatedFolder(context.Definition.TargetPath);
            }

            Directory.CreateDirectory(basePath);
            context.AddCreatedFolder(basePath);

            var config = DirectoryStructureConfig.GetOrCreateConfig();
            var folders = config.GetFoldersForModule(context.Definition.Components, context.Definition.ModuleType);

            foreach (var folder in folders)
            {
                var path = $"{basePath}/{folder}";
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    context.AddCreatedFolder(path);
                }
            }

            var components = context.Definition.Components;
            var optionalFolders = new (bool Enabled, string Name)[]
            {
                (components.FolderResources, "Resources"),
                (components.FolderPrefabs, "Prefabs"),
                (components.FolderScenes, "Scenes"),
                (components.FolderSprites, "Sprites"),
                (components.FolderArt, "Art"),
                (components.FolderAudio, "Audio"),
            };

            foreach (var (enabled, folderName) in optionalFolders)
            {
                if (enabled) CreateOptionalFolder(basePath, folderName, context);
            }

            AssetDatabase.Refresh();

            return StepResult.Ok($"Created {context.CreatedFolders.Count} folders");
        }

        public void Rollback(GenerationContext context)
        {
            for (int i = context.CreatedFolders.Count - 1; i >= 0; i--)
            {
                var folder = context.CreatedFolders[i];
                if (Directory.Exists(folder))
                {
                    var files = Directory.GetFiles(folder);
                    var dirs = Directory.GetDirectories(folder);

                    if (files.Length == 0 && dirs.Length == 0)
                    {
                        Directory.Delete(folder);
                    }
                }
            }

            AssetDatabase.Refresh();
        }

        private void CreateOptionalFolder(string basePath, string folderName, GenerationContext context)
        {
            var path = $"{basePath}/{folderName}";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                context.AddCreatedFolder(path);
            }
        }
    }
}
