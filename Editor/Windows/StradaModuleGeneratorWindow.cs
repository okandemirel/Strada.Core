using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Windows
{
    public class StradaModuleGeneratorWindow : EditorWindow
    {
        private string _moduleName = "";
        private string _namespace = "Game.Modules";
        private bool _createController = true;
        private bool _createService = true;
        private bool _createSystem = true;
        private bool _createConfig = true;
        private bool _createInterfaces = true;
        private bool _createComponents = true;
        private bool _createTests = false;
        private string _targetPath = "Assets/Modules";
        private Vector2 _scrollPosition;

        // Menu item moved to StradaEditorMenus.cs
        public static void ShowWindow()
        {
            var window = GetWindow<StradaModuleGeneratorWindow>("Create Module");
            window.minSize = new Vector2(450, 500);
            window.Show();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(10);
            GUILayout.Label("Strada Module Generator", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 });
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Module Name:", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _moduleName = EditorGUILayout.TextField(_moduleName);
            if (EditorGUI.EndChangeCheck())
                _moduleName = SanitizeModuleName(_moduleName);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Root Namespace:", EditorStyles.boldLabel);
            _namespace = EditorGUILayout.TextField(_namespace);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Target Path:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _targetPath = EditorGUILayout.TextField(_targetPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("Select Module Location", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                    _targetPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Components to Generate:", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            _createInterfaces = EditorGUILayout.Toggle("Service Interface", _createInterfaces);
            _createController = EditorGUILayout.Toggle("Controller", _createController);
            _createService = EditorGUILayout.Toggle("Service", _createService);
            _createSystem = EditorGUILayout.Toggle("ECS System", _createSystem);
            _createComponents = EditorGUILayout.Toggle("ECS Component", _createComponents);
            _createConfig = EditorGUILayout.Toggle("Config Data (CD_*)", _createConfig);
            _createTests = EditorGUILayout.Toggle("Unit Tests", _createTests);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(20);

            bool isValid = IsValidModuleName(_moduleName);
            EditorGUI.BeginDisabledGroup(!isValid);
            if (GUILayout.Button("Generate Module", GUILayout.Height(40)))
                GenerateModule();
            EditorGUI.EndDisabledGroup();

            if (string.IsNullOrEmpty(_moduleName))
                EditorGUILayout.HelpBox("Enter a module name to continue.", MessageType.Info);
            else if (!isValid)
                EditorGUILayout.HelpBox("Module name must start with a letter and contain only letters, numbers.", MessageType.Warning);
            else
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(GetPreview(), MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        private static string SanitizeModuleName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return Regex.Replace(name, @"[^a-zA-Z0-9]", "");
        }

        private static bool IsValidModuleName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9]*$");
        }

        private string GetFullNamespace() => $"{_namespace}.{_moduleName}";

        private string GetPreview()
        {
            var ns = GetFullNamespace();
            var preview = $"{_targetPath}/{_moduleName}Module/\n";
            preview += $"  Namespace: {ns}\n";
            preview += "  Scripts/\n";

            if (_createInterfaces) preview += $"    Interfaces/I{_moduleName}Service.cs\n";
            if (_createController) preview += $"    Controllers/{_moduleName}Controller.cs\n";
            if (_createService) preview += $"    Services/{_moduleName}Service.cs\n";
            if (_createSystem) preview += $"    Systems/{_moduleName}System.cs\n";
            if (_createComponents) preview += $"    Components/{_moduleName}Component.cs\n";
            if (_createConfig)
            {
                preview += $"    Data/UnityObjects/CD_{_moduleName}.cs\n";
                preview += $"    Data/ValueObjects/{_moduleName}Config.cs\n";
            }
            preview += $"  {_moduleName}Module.cs\n";
            preview += $"  {_moduleName}.asmdef\n";

            if (_createTests)
            {
                preview += $"  Tests/\n";
                preview += $"    {_moduleName}Tests.cs\n";
                preview += $"    {_moduleName}.Tests.asmdef\n";
            }

            return preview;
        }

        private void GenerateModule()
        {
            var basePath = $"{_targetPath}/{_moduleName}Module";
            var ns = GetFullNamespace();

            CreateDirectory(basePath);
            CreateDirectory($"{basePath}/Scripts");
            if (_createInterfaces) CreateDirectory($"{basePath}/Scripts/Interfaces");
            if (_createController) CreateDirectory($"{basePath}/Scripts/Controllers");
            if (_createService) CreateDirectory($"{basePath}/Scripts/Services");
            if (_createSystem) CreateDirectory($"{basePath}/Scripts/Systems");
            if (_createComponents) CreateDirectory($"{basePath}/Scripts/Components");
            if (_createConfig)
            {
                CreateDirectory($"{basePath}/Scripts/Data/UnityObjects");
                CreateDirectory($"{basePath}/Scripts/Data/ValueObjects");
            }

            CreateModuleClass(basePath, ns);
            CreateAssemblyDefinition(basePath, ns);

            if (_createInterfaces) CreateInterface(basePath, ns);
            if (_createController) CreateController(basePath, ns);
            if (_createService) CreateService(basePath, ns);
            if (_createSystem) CreateSystem(basePath, ns);
            if (_createComponents) CreateComponent(basePath, ns);
            if (_createConfig) CreateConfig(basePath, ns);
            if (_createTests) CreateTests(basePath, ns);

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Module Created", $"{_moduleName} module created at:\n{basePath}", "OK");
        }

        private void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private void CreateModuleClass(string basePath, string ns)
        {
            var content = $@"using Strada.Core.DI;
using Strada.Core.Modules;

namespace {ns}
{{
    public sealed class {_moduleName}Module : IModuleInstaller
    {{
        public string Name => ""{_moduleName}"";

        public void Install(IContainerBuilder builder)
        {{{(_createService && _createInterfaces ? $@"
            builder.Register<I{_moduleName}Service, {_moduleName}Service>(Lifetime.Singleton);" : "")}{(_createController ? $@"
            builder.Register<{_moduleName}Controller>(Lifetime.Singleton);" : "")}
        }}

        public void Initialize(IContainer container)
        {{
        }}

        public void Shutdown()
        {{
        }}
    }}
}}
";
            File.WriteAllText($"{basePath}/{_moduleName}Module.cs", content);
        }

        private void CreateAssemblyDefinition(string basePath, string ns)
        {
            var content = $@"{{
    ""name"": ""{ns}"",
    ""rootNamespace"": ""{ns}"",
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
            File.WriteAllText($"{basePath}/{_moduleName}.asmdef", content);
        }

        private void CreateInterface(string basePath, string ns)
        {
            var content = $@"namespace {ns}
{{
    public interface I{_moduleName}Service
    {{
        void Initialize();
    }}
}}
";
            File.WriteAllText($"{basePath}/Scripts/Interfaces/I{_moduleName}Service.cs", content);
        }

        private void CreateController(string basePath, string ns)
        {
            var content = $@"using Strada.Core.MVCS;
using Strada.Core.MVCS.Interfaces;

namespace {ns}
{{
    public sealed class {_moduleName}Controller : IController
    {{{(_createService && _createInterfaces ? $@"
        private readonly I{_moduleName}Service _service;

        public {_moduleName}Controller(I{_moduleName}Service service)
        {{
            _service = service;
        }}" : "")}

        public void Initialize()
        {{
        }}

        public void Update(float deltaTime)
        {{
        }}
    }}
}}
";
            File.WriteAllText($"{basePath}/Scripts/Controllers/{_moduleName}Controller.cs", content);
        }

        private void CreateService(string basePath, string ns)
        {
            var content = $@"using Strada.Core.MVCS.Interfaces;

namespace {ns}
{{
    public sealed class {_moduleName}Service : IService{(_createInterfaces ? $", I{_moduleName}Service" : "")}
    {{
        public void Initialize()
        {{
        }}

        public void Update(float deltaTime)
        {{
        }}
    }}
}}
";
            File.WriteAllText($"{basePath}/Scripts/Services/{_moduleName}Service.cs", content);
        }

        private void CreateSystem(string basePath, string ns)
        {
            var content = $@"using Strada.Core.ECS;

namespace {ns}
{{
    public sealed class {_moduleName}System : ISystem
    {{
        public void Initialize()
        {{
        }}

        public void Update(float deltaTime)
        {{
        }}

        public void Dispose()
        {{
        }}
    }}
}}
";
            File.WriteAllText($"{basePath}/Scripts/Systems/{_moduleName}System.cs", content);
        }

        private void CreateComponent(string basePath, string ns)
        {
            var content = $@"using Strada.Core.ECS;

namespace {ns}
{{
    public struct {_moduleName}Component : IComponent
    {{
    }}
}}
";
            File.WriteAllText($"{basePath}/Scripts/Components/{_moduleName}Component.cs", content);
        }

        private void CreateConfig(string basePath, string ns)
        {
            var configContent = $@"using System;

namespace {ns}
{{
    [Serializable]
    public sealed class {_moduleName}Config
    {{
    }}
}}
";
            File.WriteAllText($"{basePath}/Scripts/Data/ValueObjects/{_moduleName}Config.cs", configContent);

            var cdContent = $@"using Strada.Core.Data;
using UnityEngine;

namespace {ns}
{{
    [CreateAssetMenu(fileName = ""CD_{_moduleName}"", menuName = ""Strada/Config/{_moduleName}"")]
    public sealed class CD_{_moduleName} : ConfigData<{_moduleName}Config>
    {{
    }}
}}
";
            File.WriteAllText($"{basePath}/Scripts/Data/UnityObjects/CD_{_moduleName}.cs", cdContent);
        }

        private void CreateTests(string basePath, string ns)
        {
            CreateDirectory($"{basePath}/Tests");

            var testContent = $@"using NUnit.Framework;

namespace {ns}.Tests
{{
    [TestFixture]
    public class {_moduleName}Tests
    {{
        [SetUp]
        public void SetUp()
        {{
        }}

        [TearDown]
        public void TearDown()
        {{
        }}

        [Test]
        public void Example_Test()
        {{
            Assert.Pass();
        }}
    }}
}}
";
            File.WriteAllText($"{basePath}/Tests/{_moduleName}Tests.cs", testContent);

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
            File.WriteAllText($"{basePath}/Tests/{_moduleName}.Tests.asmdef", testAsmdef);
        }
    }
}
