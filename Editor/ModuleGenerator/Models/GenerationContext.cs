using System;
using System.Collections.Generic;

namespace Strada.Core.Editor.ModuleGenerator.Models
{
    /// <summary>
    /// Context passed through the generation pipeline.
    /// Contains all state needed for generation and tracks created artifacts.
    /// </summary>
    public class GenerationContext
    {
        public ModuleDefinition Definition { get; }
        public Dictionary<string, string> TemplateValues { get; }
        public List<string> CreatedFolders { get; }
        public List<string> CreatedFiles { get; }
        public List<string> Errors { get; }
        public List<string> Warnings { get; }

        public string ModuleConfigAssetPath { get; set; }
        public string AssemblyDefPath { get; set; }
        public bool RequiresRecompilation { get; set; }

        public GenerationContext(ModuleDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            TemplateValues = new Dictionary<string, string>();
            CreatedFolders = new List<string>();
            CreatedFiles = new List<string>();
            Errors = new List<string>();
            Warnings = new List<string>();

            BuildTemplateValues();
        }

        private void BuildTemplateValues()
        {
            var name = Definition.ModuleName;
            var nameLower = char.ToLowerInvariant(name[0]) + name.Substring(1);
            var now = DateTime.Now;

            TemplateValues["Name"] = name;
            TemplateValues["NameLower"] = nameLower;
            TemplateValues["NameUpper"] = name.ToUpperInvariant();
            TemplateValues["Namespace"] = Definition.FullNamespace;
            TemplateValues["RootNamespace"] = Definition.Namespace;
            TemplateValues["ModuleName"] = name;
            TemplateValues["ModuleFolderName"] = Definition.ModuleFolderName;
            TemplateValues["Date"] = now.ToString("yyyy-MM-dd");
            TemplateValues["Year"] = now.Year.ToString();
            TemplateValues["Time"] = now.ToString("HH:mm:ss");
        }

        public void AddError(string message)
        {
            Errors.Add(message);
        }

        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }

        public void AddCreatedFolder(string path)
        {
            CreatedFolders.Add(path);
        }

        public void AddCreatedFile(string path)
        {
            CreatedFiles.Add(path);
        }

        public bool HasErrors => Errors.Count > 0;
        public bool HasWarnings => Warnings.Count > 0;
    }
}
