using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Templates
{
    public static class ModuleTemplateGenerator
    {
        [MenuItem("Strada/Create New Module")]
        public static void CreateNewModule()
        {
            var window = EditorWindow.GetWindow<ModuleCreatorWindow>(true, "Create Strada Module", true);
            window.ShowPopup();
        }

        public static void GenerateModule(string moduleName, bool createAssemblyDef = true)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                Debug.LogError("[Strada] Module name cannot be empty");
                return;
            }

            if (!moduleName.EndsWith("Module"))
                moduleName += "Module";

            var basePath = Path.Combine(Application.dataPath, "Modules", moduleName, "Scripts");

            var folders = new[]
            {
                "Controllers",
                "Data",
                "Data/UnityObjects",
                "Data/ValueObjects",
                "Interfaces",
                "Systems",
                "Services",
                "Views"
            };

            foreach (var folder in folders)
            {
                var path = Path.Combine(basePath, folder);
                Directory.CreateDirectory(path);
            }

            var prefix = moduleName.Replace("Module", "");
            GenerateModuleInterface(basePath, prefix);
            GenerateModuleInstaller(basePath, prefix);
            GenerateSampleController(basePath, prefix);
            GenerateSampleSystem(basePath, prefix);

            if (createAssemblyDef)
                GenerateAssemblyDefinition(basePath, prefix);

            AssetDatabase.Refresh();
            Debug.Log($"[Strada] Created module '{moduleName}' at Assets/Modules/{moduleName}");
        }

        private static void GenerateModuleInterface(string basePath, string prefix)
        {
            var code = $@"namespace {prefix}.Interfaces
{{
    public interface I{prefix}Service
    {{
        void Initialize();
        void Shutdown();
    }}
}}
";
            File.WriteAllText(Path.Combine(basePath, "Interfaces", $"I{prefix}Service.cs"), code);
        }

        private static void GenerateModuleInstaller(string basePath, string prefix)
        {
            var code = $@"using Strada.Core.DI;
using Strada.Core.Module;
using Strada.Core.Editor.CodeGen;

namespace {prefix}
{{
    [ModulePriority(0)]
    public class {prefix}Module : IModule
    {{
        public string Name => ""{prefix}"";

        public void Install(IContainerBuilder builder)
        {{
            // Register services
            // builder.Register<{prefix}Service>(Lifetime.Singleton);
        }}

        public void Initialize(IContainer container)
        {{
            // Initialize module
        }}

        public void Shutdown()
        {{
            // Cleanup module
        }}
    }}
}}
";
            File.WriteAllText(Path.Combine(basePath, $"{prefix}Module.cs"), code);
        }

        private static void GenerateSampleController(string basePath, string prefix)
        {
            var code = $@"using UnityEngine;

namespace {prefix}.Controllers
{{
    /// <summary>
    /// Sample controller for {prefix} module.
    /// Controllers handle input and coordinate between Views and Services.
    /// </summary>
    public class {prefix}Controller
    {{
        public void Initialize()
        {{
            Debug.Log(""[{prefix}] Controller initialized"");
        }}
    }}
}}
";
            File.WriteAllText(Path.Combine(basePath, "Controllers", $"{prefix}Controller.cs"), code);
        }

        private static void GenerateSampleSystem(string basePath, string prefix)
        {
            var code = $@"using Strada.Core.ECS.Systems;
using Strada.Core.Editor.CodeGen;

namespace {prefix}.Systems
{{
    /// <summary>
    /// Sample ECS system for {prefix} module.
    /// Systems process component data each frame.
    /// </summary>
    [SystemOrder(0)]
    public class {prefix}System : SystemBase
    {{
        protected override void OnUpdate(float deltaTime)
        {{
            // Process entities here
        }}
    }}
}}
";
            File.WriteAllText(Path.Combine(basePath, "Systems", $"{prefix}System.cs"), code);
        }

        private static void GenerateAssemblyDefinition(string basePath, string prefix)
        {
            var asmdef = $@"{{
    ""name"": ""{prefix}"",
    ""rootNamespace"": ""{prefix}"",
    ""references"": [
        ""Strada.Core""
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
            File.WriteAllText(Path.Combine(basePath, $"{prefix}.asmdef"), asmdef);
        }
    }

    public class ModuleCreatorWindow : EditorWindow
    {
        private string _moduleName = "";
        private bool _createAssemblyDef = true;

        private void OnGUI()
        {
            GUILayout.Label("Create New Strada Module", EditorStyles.boldLabel);
            GUILayout.Space(10);

            _moduleName = EditorGUILayout.TextField("Module Name:", _moduleName);
            _createAssemblyDef = EditorGUILayout.Toggle("Create Assembly Definition:", _createAssemblyDef);

            GUILayout.Space(10);

            if (!string.IsNullOrWhiteSpace(_moduleName))
            {
                var displayName = _moduleName.EndsWith("Module") ? _moduleName : _moduleName + "Module";
                EditorGUILayout.HelpBox(
                    $"Will create: Assets/Modules/{displayName}/\n" +
                    $"  Scripts/Controllers/\n" +
                    $"  Scripts/Data/UnityObjects/\n" +
                    $"  Scripts/Data/ValueObjects/\n" +
                    $"  Scripts/Interfaces/\n" +
                    $"  Scripts/Systems/\n" +
                    $"  Scripts/Services/\n" +
                    $"  Scripts/Views/",
                    MessageType.Info);
            }

            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_moduleName));
            if (GUILayout.Button("Create Module", GUILayout.Height(30)))
            {
                ModuleTemplateGenerator.GenerateModule(_moduleName, _createAssemblyDef);
                Close();
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}
