using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Wizards
{
    /// <summary>
    /// Wizard for generating test files for Strada components.
    /// Creates AAA pattern test stubs with proper structure.
    /// </summary>
    public class TestGeneratorWizard : EditorWindow
    {
        private MonoScript _targetScript;
        private string _className = "";
        private bool _generateEditModeTests = true;
        private bool _generatePlayModeTests = false;
        private bool _generatePerformanceTests = false;

        [MenuItem("Assets/Create/Strada/Generate Tests")]
        public static void ShowWizard()
        {
            var window = GetWindow<TestGeneratorWizard>("Test Generator");
            window.minSize = new Vector2(500, 350);
            window.Show();
        }

        private void OnGUI()
        {
            StradaEditorGUI.BeginInspectorPanel();
            StradaEditorGUI.DrawHeader("Test Generator", StradaEditorIcons.ComponentIcon);
            StradaEditorGUI.EndInspectorPanel();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _targetScript = (MonoScript)EditorGUILayout.ObjectField("Target Script", _targetScript, typeof(MonoScript), false);

            if (_targetScript != null)
            {
                var scriptClass = _targetScript.GetClass();
                if (scriptClass != null)
                {
                    _className = scriptClass.Name;
                    EditorGUILayout.LabelField("Class Name", _className);
                    EditorGUILayout.LabelField("Namespace", scriptClass.Namespace ?? "(none)");
                }
            }

            EditorGUILayout.EndVertical();

            StradaEditorGUI.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Test Types to Generate", EditorStyles.boldLabel);

            _generateEditModeTests = EditorGUILayout.Toggle("Edit Mode Tests", _generateEditModeTests);
            _generatePlayModeTests = EditorGUILayout.Toggle("Play Mode Tests", _generatePlayModeTests);
            _generatePerformanceTests = EditorGUILayout.Toggle("Performance Tests", _generatePerformanceTests);

            EditorGUILayout.EndVertical();

            StradaEditorGUI.Space();

            GUI.enabled = _targetScript != null && !string.IsNullOrEmpty(_className);

            if (StradaEditorGUI.DrawButton("Generate Tests", StradaEditorIcons.AddIcon, GUILayout.Height(40)))
            {
                GenerateTests();
            }

            GUI.enabled = true;
        }

        private void GenerateTests()
        {
            var scriptPath = AssetDatabase.GetAssetPath(_targetScript);
            var directory = Path.GetDirectoryName(scriptPath);
            var projectRoot = directory;

            while (!Directory.Exists(Path.Combine(projectRoot, "Tests")) && projectRoot.Contains("Assets"))
            {
                projectRoot = Directory.GetParent(projectRoot)?.FullName;
                if (projectRoot == null) break;
            }

            if (projectRoot == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not find Tests directory.", "OK");
                return;
            }

            var testsPath = Path.Combine(projectRoot, "Tests");

            if (!Directory.Exists(testsPath))
            {
                Directory.CreateDirectory(testsPath);
            }

            if (_generateEditModeTests)
            {
                var editModePath = Path.Combine(testsPath, "EditMode");
                Directory.CreateDirectory(editModePath);
                GenerateEditModeTests(editModePath);
            }

            if (_generatePlayModeTests)
            {
                var playModePath = Path.Combine(testsPath, "PlayMode");
                Directory.CreateDirectory(playModePath);
                GeneratePlayModeTests(playModePath);
            }

            if (_generatePerformanceTests)
            {
                var perfPath = Path.Combine(testsPath, "Performance");
                Directory.CreateDirectory(perfPath);
                GeneratePerformanceTests(perfPath);
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Tests generated for {_className}", "OK");
            Close();
        }

        private void GenerateEditModeTests(string path)
        {
            var sb = new StringBuilder();
            var scriptClass = _targetScript.GetClass();
            var ns = scriptClass?.Namespace;

            sb.AppendLine("using NUnit.Framework;");
            if (!string.IsNullOrEmpty(ns))
            {
                sb.AppendLine($"using {ns};");
            }
            sb.AppendLine();

            var testNamespace = !string.IsNullOrEmpty(ns) ? $"{ns}.Tests" : "Tests";
            sb.AppendLine($"namespace {testNamespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    [TestFixture]");
            sb.AppendLine($"    public class {_className}Tests");
            sb.AppendLine("    {");
            sb.AppendLine("        [SetUp]");
            sb.AppendLine("        public void Setup()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        [TearDown]");
            sb.AppendLine("        public void TearDown()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        [Test]");
            sb.AppendLine($"        public void {_className}_WhenCreated_ShouldNotBeNull()");
            sb.AppendLine("        {");
            sb.AppendLine($"            // Arrange");
            sb.AppendLine();
            sb.AppendLine($"            // Act");
            sb.AppendLine($"            // var instance = new {_className}();");
            sb.AppendLine();
            sb.AppendLine($"            // Assert");
            sb.AppendLine($"            // Assert.IsNotNull(instance);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(Path.Combine(path, $"{_className}Tests.cs"), sb.ToString());
        }

        private void GeneratePlayModeTests(string path)
        {
            var sb = new StringBuilder();
            var scriptClass = _targetScript.GetClass();
            var ns = scriptClass?.Namespace;

            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using NUnit.Framework;");
            sb.AppendLine("using UnityEngine.TestTools;");
            if (!string.IsNullOrEmpty(ns))
            {
                sb.AppendLine($"using {ns};");
            }
            sb.AppendLine();

            var testNamespace = !string.IsNullOrEmpty(ns) ? $"{ns}.Tests.PlayMode" : "Tests.PlayMode";
            sb.AppendLine($"namespace {testNamespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    [TestFixture]");
            sb.AppendLine($"    public class {_className}PlayModeTests");
            sb.AppendLine("    {");
            sb.AppendLine("        [UnityTest]");
            sb.AppendLine($"        public IEnumerator {_className}_PlayModeTest()");
            sb.AppendLine("        {");
            sb.AppendLine($"            // Arrange");
            sb.AppendLine();
            sb.AppendLine($"            // Act");
            sb.AppendLine("            yield return null;");
            sb.AppendLine();
            sb.AppendLine($"            // Assert");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(Path.Combine(path, $"{_className}PlayModeTests.cs"), sb.ToString());
        }

        private void GeneratePerformanceTests(string path)
        {
            var sb = new StringBuilder();
            var scriptClass = _targetScript.GetClass();
            var ns = scriptClass?.Namespace;

            sb.AppendLine("using NUnit.Framework;");
            sb.AppendLine("using Unity.PerformanceTesting;");
            if (!string.IsNullOrEmpty(ns))
            {
                sb.AppendLine($"using {ns};");
            }
            sb.AppendLine();

            var testNamespace = !string.IsNullOrEmpty(ns) ? $"{ns}.Tests.Performance" : "Tests.Performance";
            sb.AppendLine($"namespace {testNamespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    [TestFixture]");
            sb.AppendLine($"    public class {_className}PerformanceTests");
            sb.AppendLine("    {");
            sb.AppendLine("        [Test, Performance]");
            sb.AppendLine($"        public void {_className}_Performance_Benchmark()");
            sb.AppendLine("        {");
            sb.AppendLine("            Measure.Method(() =>");
            sb.AppendLine("            {");
            sb.AppendLine("                // Code to benchmark");
            sb.AppendLine("            })");
            sb.AppendLine("            .WarmupCount(10)");
            sb.AppendLine("            .MeasurementCount(100)");
            sb.AppendLine("            .Run();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(Path.Combine(path, $"{_className}PerformanceTests.cs"), sb.ToString());
        }
    }
}
