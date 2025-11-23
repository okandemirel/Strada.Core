using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Wizards
{
    /// <summary>
    /// Wizard for creating Strada ScriptableObject configurations (CD_*).
    /// Generates both the ScriptableObject and its value object.
    /// </summary>
    public class ScriptableObjectWizard : EditorWindow
    {
        private string _configName = "MyConfig";
        private string _moduleName = "";
        private string _description = "";

        private bool _addFloatField = false;
        private bool _addIntField = false;
        private bool _addBoolField = false;
        private bool _addStringField = false;
        private bool _addColorField = false;

        [MenuItem("Assets/Create/Strada/New ScriptableObject Config")]
        public static void ShowWizard()
        {
            var window = GetWindow<ScriptableObjectWizard>("New Config");
            window.minSize = new Vector2(500, 450);
            window.Show();
        }

        private void OnGUI()
        {
            StradaEditorGUI.BeginInspectorPanel();
            StradaEditorGUI.DrawHeader("ScriptableObject Config Wizard", StradaEditorIcons.ConfigDataIcon);
            StradaEditorGUI.EndInspectorPanel();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Basic Information", EditorStyles.boldLabel);

            _configName = EditorGUILayout.TextField("Config Name", _configName);
            _moduleName = EditorGUILayout.TextField("Module Name", _moduleName);
            _description = EditorGUILayout.TextField("Description", _description);

            if (string.IsNullOrWhiteSpace(_configName))
            {
                StradaEditorGUI.DrawHelpBox("Config name is required.", MessageType.Error);
            }

            EditorGUILayout.EndVertical();

            StradaEditorGUI.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Fields to Include", EditorStyles.boldLabel);

            _addFloatField = EditorGUILayout.Toggle("Add Float Field", _addFloatField);
            _addIntField = EditorGUILayout.Toggle("Add Int Field", _addIntField);
            _addBoolField = EditorGUILayout.Toggle("Add Bool Field", _addBoolField);
            _addStringField = EditorGUILayout.Toggle("Add String Field", _addStringField);
            _addColorField = EditorGUILayout.Toggle("Add Color Field", _addColorField);

            EditorGUILayout.EndVertical();

            StradaEditorGUI.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Files to Generate:", EditorStyles.boldLabel);
            GUILayout.Label($"  • CD_{_configName}.cs (ScriptableObject)", EditorStyles.miniLabel);
            GUILayout.Label($"  • {_configName}Config.cs (Value Object)", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            StradaEditorGUI.Space();

            GUI.enabled = !string.IsNullOrWhiteSpace(_configName);

            if (StradaEditorGUI.DrawButton("Generate Config", StradaEditorIcons.AddIcon, GUILayout.Height(40)))
            {
                GenerateConfig();
            }

            GUI.enabled = true;
        }

        private void GenerateConfig()
        {
            var basePath = string.IsNullOrWhiteSpace(_moduleName)
                ? "Assets/Configs/"
                : $"Assets/Modules/{_moduleName}/Scripts/Data/";

            Directory.CreateDirectory(basePath + "UnityObjects");
            Directory.CreateDirectory(basePath + "ValueObjects");

            GenerateValueObject(basePath + "ValueObjects/");
            GenerateScriptableObject(basePath + "UnityObjects/");

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Config files generated for {_configName}", "OK");
            Close();
        }

        private void GenerateValueObject(string path)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();

            var ns = string.IsNullOrWhiteSpace(_moduleName) ? "Strada.Configs" : $"Strada.Modules.{_moduleName}";
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine("    [Serializable]");
            sb.AppendLine($"    public class {_configName}Config");
            sb.AppendLine("    {");

            if (_addFloatField)
            {
                sb.AppendLine("        public float FloatValue = 1.0f;");
            }

            if (_addIntField)
            {
                sb.AppendLine("        public int IntValue = 0;");
            }

            if (_addBoolField)
            {
                sb.AppendLine("        public bool BoolValue = false;");
            }

            if (_addStringField)
            {
                sb.AppendLine("        public string StringValue = \"\";");
            }

            if (_addColorField)
            {
                sb.AppendLine("        public Color ColorValue = Color.white;");
            }

            sb.AppendLine();
            sb.AppendLine("        public bool IsValid()");
            sb.AppendLine("        {");
            sb.AppendLine("            // TODO: Add validation logic");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(Path.Combine(path, $"{_configName}Config.cs"), sb.ToString());
        }

        private void GenerateScriptableObject(string path)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();

            var ns = string.IsNullOrWhiteSpace(_moduleName) ? "Strada.Configs" : $"Strada.Modules.{_moduleName}";
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    [CreateAssetMenu(fileName = \"CD_{_configName}\", menuName = \"Strada/Configs/{_configName}\")]");
            sb.AppendLine($"    public class CD_{_configName} : ScriptableObject");
            sb.AppendLine("    {");
            sb.AppendLine($"        public {_configName}Config Config = new {_configName}Config();");
            sb.AppendLine();
            sb.AppendLine("        private void OnValidate()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!Config.IsValid())");
            sb.AppendLine("            {");
            sb.AppendLine($"                Debug.LogWarning($\"[CD_{_configName}] Invalid configuration in {{name}}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static CD_{_configName} CreateDefault()");
            sb.AppendLine("        {");
            sb.AppendLine($"            var config = CreateInstance<CD_{_configName}>();");
            sb.AppendLine($"            config.Config = new {_configName}Config();");
            sb.AppendLine("            return config;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(Path.Combine(path, $"CD_{_configName}.cs"), sb.ToString());
        }
    }
}
