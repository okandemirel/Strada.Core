using System.Collections.Generic;
using System.IO;
using System.Linq;
using Strada.Core.Editor.ModuleGenerator.Models;
using UnityEditor;

namespace Strada.Core.Editor.ModuleGenerator.Pipeline.Steps
{
    /// <summary>
    /// Creates assembly definition files.
    /// </summary>
    public class AssemblyDefStep : IGenerationStep
    {
        public string Name => "Assembly Definition";
        public int Order => 20;

        public bool CanExecute(GenerationContext context)
        {
            return context.Definition.Components.AssemblyDefinition &&
                   context.Definition.ModuleType == ModuleType.Main;
        }

        public StepResult Execute(GenerationContext context)
        {
            var basePath = context.Definition.FullPath;
            var name = context.Definition.ModuleName;
            var ns = context.Definition.FullNamespace;

            var references = new List<string> { "Strada.Core" };

            foreach (var dep in context.Definition.Dependencies)
            {
                var depAssembly = ModuleDiscovery.FindAssemblyForModule(dep);
                if (!string.IsNullOrEmpty(depAssembly) && !references.Contains(depAssembly))
                {
                    references.Add(depAssembly);
                }
            }

            var refsJson = string.Join(",\n        ", references.Select(r => $"\"{r}\""));

            var mainAsmdef = $@"{{
    ""name"": ""{ns}"",
    ""rootNamespace"": ""{ns}"",
    ""references"": [
        {refsJson}
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": true,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";

            var mainPath = $"{basePath}/{name}.asmdef";
            File.WriteAllText(mainPath, mainAsmdef);
            context.AddCreatedFile(mainPath);
            context.AssemblyDefPath = mainPath;

            if (context.Definition.Components.EditorScripts)
            {
                var editorAsmdef = $@"{{
    ""name"": ""{ns}.Editor"",
    ""rootNamespace"": ""{ns}.Editor"",
    ""references"": [
        ""{ns}"",
        ""Strada.Core"",
        ""Strada.Core.Editor""
    ],
    ""includePlatforms"": [
        ""Editor""
    ],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": true,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";

                var editorPath = $"{basePath}/Editor/{name}.Editor.asmdef";
                File.WriteAllText(editorPath, editorAsmdef);
                context.AddCreatedFile(editorPath);
            }

            if (context.Definition.Components.RuntimeTests || context.Definition.Components.EditorTests)
            {
                var testAsmdef = $@"{{
    ""name"": ""{ns}.Tests"",
    ""rootNamespace"": ""{ns}.Tests"",
    ""references"": [
        ""{ns}"",
        ""Strada.Core"",
        ""UnityEngine.TestRunner"",
        ""UnityEditor.TestRunner""
    ],
    ""includePlatforms"": [
        ""Editor""
    ],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": true,
    ""overrideReferences"": true,
    ""precompiledReferences"": [
        ""nunit.framework.dll""
    ],
    ""autoReferenced"": false,
    ""defineConstraints"": [
        ""UNITY_INCLUDE_TESTS""
    ],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";

                var testPath = $"{basePath}/Tests/{name}.Tests.asmdef";
                File.WriteAllText(testPath, testAsmdef);
                context.AddCreatedFile(testPath);
            }

            context.RequiresRecompilation = true;

            return StepResult.Ok($"Created assembly definitions");
        }

        public void Rollback(GenerationContext context)
        {
            foreach (var file in context.CreatedFiles)
            {
                if (file.EndsWith(".asmdef") && File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            AssetDatabase.Refresh();
        }
    }
}
