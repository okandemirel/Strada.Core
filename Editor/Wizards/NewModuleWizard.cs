using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Wizards
{
    /// <summary>
    /// Wizard for creating new Strada modules with all necessary files and structure.
    /// Accessible via Assets > Create > Strada > New Module menu.
    /// </summary>
    public class NewModuleWizard : EditorWindow
    {
        private enum WizardStep
        {
            BasicInfo,
            Components,
            Dependencies,
            Generate
        }

        private WizardStep _currentStep = WizardStep.BasicInfo;

        private string _moduleName = "MyModule";
        private string _moduleDescription = "";
        private string _authorName = "";

        private bool _includeModel = true;
        private bool _includeView = true;
        private bool _includeController = true;
        private bool _includeService = true;

        private bool _includeECS = false;
        private bool _includeComponents = false;
        private bool _includeSystems = false;
        private bool _includeBaker = false;

        private bool _includeScriptableObject = true;
        private bool _generateInterfaces = true;
        private bool _generateTests = true;

        private Vector2 _scrollPosition;

        [MenuItem("Assets/Create/Strada/New Module")]
        public static void ShowWizard()
        {
            var window = GetWindow<NewModuleWizard>("New Module Wizard");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawProgressBar();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_currentStep)
            {
                case WizardStep.BasicInfo:
                    DrawBasicInfoStep();
                    break;

                case WizardStep.Components:
                    DrawComponentsStep();
                    break;

                case WizardStep.Dependencies:
                    DrawDependenciesStep();
                    break;

                case WizardStep.Generate:
                    DrawGenerateStep();
                    break;
            }

            EditorGUILayout.EndScrollView();

            DrawNavigationButtons();
        }

        private void DrawHeader()
        {
            StradaEditorGUI.BeginInspectorPanel();
            StradaEditorGUI.DrawHeader("New Module Wizard", StradaEditorIcons.ModuleIcon);
            StradaEditorGUI.EndInspectorPanel();
        }

        private void DrawProgressBar()
        {
            var stepNames = new[] { "Basic Info", "Components", "Dependencies", "Generate" };
            var stepIndex = (int)_currentStep;

            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < stepNames.Length; i++)
            {
                var isActive = i == stepIndex;
                var isCompleted = i < stepIndex;

                var color = isCompleted ? StradaEditorStyles.SuccessColor :
                           isActive ? StradaEditorStyles.PrimaryColor :
                           StradaEditorStyles.SubtleTextColor;

                GUI.backgroundColor = color;
                GUILayout.Label($"{i + 1}. {stepNames[i]}", EditorStyles.toolbarButton);
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();
            StradaEditorGUI.Space();
        }

        private void DrawBasicInfoStep()
        {
            StradaEditorGUI.DrawSubHeader("Step 1: Basic Information", StradaEditorIcons.InfoIcon);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _moduleName = EditorGUILayout.TextField("Module Name", _moduleName);
            _moduleDescription = EditorGUILayout.TextField("Description", _moduleDescription);
            _authorName = EditorGUILayout.TextField("Author", _authorName);

            if (string.IsNullOrWhiteSpace(_moduleName))
            {
                StradaEditorGUI.DrawHelpBox("Module name is required.", MessageType.Error);
            }
            else if (!IsValidModuleName(_moduleName))
            {
                StradaEditorGUI.DrawHelpBox("Module name must be a valid C# identifier (letters, numbers, underscore).", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();

            StradaEditorGUI.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("The module will be created at:", EditorStyles.boldLabel);
            GUILayout.Label(GetModulePath(), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawComponentsStep()
        {
            StradaEditorGUI.DrawSubHeader("Step 2: Choose Components", StradaEditorIcons.ComponentIcon);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("MVCS Components", EditorStyles.boldLabel);

            _includeModel = EditorGUILayout.Toggle("Model", _includeModel);
            _includeView = EditorGUILayout.Toggle("View", _includeView);
            _includeController = EditorGUILayout.Toggle("Controller", _includeController);
            _includeService = EditorGUILayout.Toggle("Service", _includeService);

            EditorGUILayout.EndVertical();

            StradaEditorGUI.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("ECS Components", EditorStyles.boldLabel);

            _includeECS = EditorGUILayout.Toggle("Include ECS Support", _includeECS);

            if (_includeECS)
            {
                EditorGUI.indentLevel++;
                _includeComponents = EditorGUILayout.Toggle("Components", _includeComponents);
                _includeSystems = EditorGUILayout.Toggle("Systems", _includeSystems);
                _includeBaker = EditorGUILayout.Toggle("Baker", _includeBaker);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();

            StradaEditorGUI.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Additional Options", EditorStyles.boldLabel);

            _includeScriptableObject = EditorGUILayout.Toggle("ScriptableObject Config", _includeScriptableObject);
            _generateInterfaces = EditorGUILayout.Toggle("Generate Interfaces", _generateInterfaces);
            _generateTests = EditorGUILayout.Toggle("Generate Tests", _generateTests);

            EditorGUILayout.EndVertical();
        }

        private void DrawDependenciesStep()
        {
            StradaEditorGUI.DrawSubHeader("Step 3: Dependencies (Optional)", StradaEditorIcons.ArrowRightIcon);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Module dependencies can be added later in the module installer.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawGenerateStep()
        {
            StradaEditorGUI.DrawSubHeader("Step 4: Ready to Generate", StradaEditorIcons.SuccessIcon);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("Module Summary:", EditorStyles.boldLabel);
            GUILayout.Label($"Name: {_moduleName}", EditorStyles.label);
            GUILayout.Label($"Path: {GetModulePath()}", EditorStyles.wordWrappedMiniLabel);

            StradaEditorGUI.Space();

            GUILayout.Label("Files to Generate:", EditorStyles.boldLabel);

            var fileCount = CountFilesToGenerate();
            GUILayout.Label($"• {fileCount} files will be created", EditorStyles.label);

            if (_includeModel) GUILayout.Label($"  - I{_moduleName}Model.cs", EditorStyles.miniLabel);
            if (_includeView) GUILayout.Label($"  - {_moduleName}View.cs", EditorStyles.miniLabel);
            if (_includeController) GUILayout.Label($"  - I{_moduleName}Controller.cs", EditorStyles.miniLabel);
            if (_includeService) GUILayout.Label($"  - I{_moduleName}Service.cs", EditorStyles.miniLabel);
            if (_includeScriptableObject) GUILayout.Label($"  - CD_{_moduleName}.cs", EditorStyles.miniLabel);

            GUILayout.Label($"  - {_moduleName}ModuleInstaller.cs", EditorStyles.miniLabel);
            GUILayout.Label($"  - Assembly definitions (2)", EditorStyles.miniLabel);

            if (_generateTests)
            {
                GUILayout.Label($"  - Test files", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            StradaEditorGUI.Space();

            if (StradaEditorGUI.DrawButton("Generate Module", StradaEditorIcons.AddIcon, GUILayout.Height(40)))
            {
                GenerateModule();
            }
        }

        private void DrawNavigationButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = _currentStep > WizardStep.BasicInfo;
            if (GUILayout.Button("← Previous", GUILayout.Height(30)))
            {
                _currentStep--;
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (_currentStep < WizardStep.Generate)
            {
                var canProceed = CanProceedToNextStep();
                GUI.enabled = canProceed;

                if (GUILayout.Button("Next →", GUILayout.Height(30)))
                {
                    _currentStep++;
                }

                GUI.enabled = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool CanProceedToNextStep()
        {
            switch (_currentStep)
            {
                case WizardStep.BasicInfo:
                    return !string.IsNullOrWhiteSpace(_moduleName) && IsValidModuleName(_moduleName);

                case WizardStep.Components:
                    return _includeModel || _includeView || _includeController || _includeService || _includeECS;

                default:
                    return true;
            }
        }

        private bool IsValidModuleName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (char.IsDigit(name[0]))
                return false;

            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return true;
        }

        private string GetModulePath()
        {
            return $"Assets/Modules/{_moduleName}/";
        }

        private int CountFilesToGenerate()
        {
            int count = 1;

            if (_includeModel) count += 2;
            if (_includeView) count += 1;
            if (_includeController) count += 2;
            if (_includeService) count += 2;
            if (_includeScriptableObject) count += 2;
            if (_includeECS && _includeComponents) count += 1;
            if (_includeECS && _includeSystems) count += 1;
            if (_includeECS && _includeBaker) count += 1;

            count += 2;

            if (_generateTests) count += 2;

            return count;
        }

        private void GenerateModule()
        {
            try
            {
                var basePath = GetModulePath();

                Directory.CreateDirectory(basePath + "Scripts");
                Directory.CreateDirectory(basePath + "Scripts/Data/ValueObjects");
                Directory.CreateDirectory(basePath + "Scripts/Data/UnityObjects");

                if (_generateInterfaces)
                    Directory.CreateDirectory(basePath + "Scripts/Interfaces");

                if (_includeModel)
                    Directory.CreateDirectory(basePath + "Scripts/Models");

                if (_includeView)
                    Directory.CreateDirectory(basePath + "Scripts/Views");

                if (_includeController)
                    Directory.CreateDirectory(basePath + "Scripts/Controllers");

                if (_includeService)
                    Directory.CreateDirectory(basePath + "Scripts/Services");

                if (_includeECS)
                {
                    Directory.CreateDirectory(basePath + "Scripts/ECS");
                    if (_includeComponents) Directory.CreateDirectory(basePath + "Scripts/ECS/Components");
                    if (_includeSystems) Directory.CreateDirectory(basePath + "Scripts/ECS/Systems");
                    if (_includeBaker) Directory.CreateDirectory(basePath + "Scripts/ECS/Bakers");
                }

                if (_generateTests)
                {
                    Directory.CreateDirectory(basePath + "Tests");
                    Directory.CreateDirectory(basePath + "Tests/EditMode");
                }

                GenerateModuleInstaller(basePath);
                GenerateAssemblyDefinitions(basePath);

                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Success!", $"Module '{_moduleName}' created successfully at {basePath}", "OK");

                Close();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to create module: {ex.Message}", "OK");
            }
        }

        private void GenerateModuleInstaller(string basePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"using Strada.Core.DI;");
            sb.AppendLine($"using Strada.Core.Modules;");
            sb.AppendLine($"using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"namespace Strada.Modules.{_moduleName}");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {_moduleName}ModuleInstaller : IModuleInstaller");
            sb.AppendLine("    {");
            sb.AppendLine("        public void Install(IContainerBuilder builder)");
            sb.AppendLine("        {");
            sb.AppendLine($"            Debug.Log(\"[{_moduleName}] Installing module...\");");
            sb.AppendLine();
            sb.AppendLine("            // TODO: Register dependencies here");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void Initialize(IContainer container)");
            sb.AppendLine("        {");
            sb.AppendLine($"            Debug.Log(\"[{_moduleName}] Initializing module...\");");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void Shutdown()");
            sb.AppendLine("        {");
            sb.AppendLine($"            Debug.Log(\"[{_moduleName}] Shutting down module...\");");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(basePath + $"Scripts/{_moduleName}ModuleInstaller.cs", sb.ToString());
        }

        private void GenerateAssemblyDefinitions(string basePath)
        {
            var runtimeAsmdef = $@"{{
    ""name"": ""Strada.Modules.{_moduleName}"",
    ""rootNamespace"": ""Strada.Modules.{_moduleName}"",
    ""references"": [
        ""Strada.Core""
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";

            File.WriteAllText(basePath + $"Strada.Modules.{_moduleName}.asmdef", runtimeAsmdef);
        }
    }
}
