using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Editor window for displaying asset and module validation results.
    /// Provides detailed reports with categorization, filtering, and quick-fix suggestions.
    /// </summary>
    public class ValidationReportWindow : EditorWindow
    {
        private enum FilterMode
        {
            All,
            Errors,
            Warnings,
            Info
        }

        private ValidationResult _currentResult;
        private Vector2 _scrollPosition;
        private FilterMode _filterMode = FilterMode.All;
        private string _searchFilter = "";
        private string _categoryFilter = "";

        private List<AssetValidator> _validators;
        private bool _isValidating = false;

        [MenuItem("Tools/Strada/Validate All Assets")]
        public static void ValidateAllAssets()
        {
            var window = GetWindow<ValidationReportWindow>("Validation Report");
            window.minSize = new Vector2(700, 500);
            window.Show();
            window.RunFullValidation();
        }

        [MenuItem("Tools/Strada/Validate Modules")]
        public static void ValidateModules()
        {
            var window = GetWindow<ValidationReportWindow>("Validation Report");
            window.minSize = new Vector2(700, 500);
            window.Show();
            window.RunModuleValidation();
        }

        private void OnEnable()
        {
            InitializeValidators();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawSummary();
            DrawFilters();
            DrawResults();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Validate All", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                RunFullValidation();
            }

            if (GUILayout.Button("Modules", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RunModuleValidation();
            }

            if (GUILayout.Button("Configs", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RunConfigValidation();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _currentResult = null;
            }

            GUILayout.FlexibleSpace();

            if (_isValidating)
            {
                GUILayout.Label("Validating...", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            if (_currentResult == null)
            {
                StradaEditorGUI.DrawHelpBox("Click 'Validate All' to run validation checks.", MessageType.Info);
                return;
            }

            StradaEditorGUI.BeginInspectorPanel();

            EditorGUILayout.BeginHorizontal();

            DrawSummaryBox("Errors", _currentResult.ErrorCount, StradaEditorStyles.ErrorColor);
            DrawSummaryBox("Warnings", _currentResult.WarningCount, StradaEditorStyles.WarningColor);
            DrawSummaryBox("Info", _currentResult.InfoCount, StradaEditorStyles.PrimaryColor);

            EditorGUILayout.EndHorizontal();

            StradaEditorGUI.Space();

            if (_currentResult.IsValid)
            {
                StradaEditorGUI.DrawValidationBox(true, "All validation checks passed!", false);
            }
            else
            {
                StradaEditorGUI.DrawValidationBox(false, $"Found {_currentResult.ErrorCount} error(s)", _currentResult.HasWarnings);
            }

            StradaEditorGUI.EndInspectorPanel();
        }

        private void DrawSummaryBox(string label, int count, Color color)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(200));

            GUI.backgroundColor = color;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            GUILayout.Label(count.ToString(), EditorStyles.largeLabel);
            GUILayout.Label(label, EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
        }

        private void DrawFilters()
        {
            if (_currentResult == null || _currentResult.Messages.Count == 0)
                return;

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("Filter:", GUILayout.Width(40));

            if (GUILayout.Toggle(_filterMode == FilterMode.All, "All", EditorStyles.toolbarButton))
                _filterMode = FilterMode.All;

            if (GUILayout.Toggle(_filterMode == FilterMode.Errors, $"Errors ({_currentResult.ErrorCount})", EditorStyles.toolbarButton))
                _filterMode = FilterMode.Errors;

            if (GUILayout.Toggle(_filterMode == FilterMode.Warnings, $"Warnings ({_currentResult.WarningCount})", EditorStyles.toolbarButton))
                _filterMode = FilterMode.Warnings;

            if (GUILayout.Toggle(_filterMode == FilterMode.Info, $"Info ({_currentResult.InfoCount})", EditorStyles.toolbarButton))
                _filterMode = FilterMode.Info;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchFilter = EditorGUILayout.TextField(_searchFilter);
            EditorGUILayout.EndHorizontal();

            StradaEditorGUI.Space();
        }

        private void DrawResults()
        {
            if (_currentResult == null)
                return;

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var filteredMessages = GetFilteredMessages();
            var groupedMessages = filteredMessages.GroupBy(m => m.Category);

            foreach (var group in groupedMessages.OrderBy(g => g.Key))
            {
                DrawCategoryGroup(group.Key, group.ToList());
            }

            if (filteredMessages.Count == 0)
            {
                GUILayout.Label("No messages match the current filter.", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCategoryGroup(string category, List<ValidationResult.ValidationMessage> messages)
        {
            StradaEditorGUI.DrawSubHeader(category, StradaEditorIcons.ModuleIcon);

            foreach (var message in messages)
            {
                DrawMessage(message);
            }

            StradaEditorGUI.Space();
        }

        private void DrawMessage(ValidationResult.ValidationMessage message)
        {
            var backgroundColor = GetMessageColor(message.Severity);

            GUI.backgroundColor = backgroundColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();

            var icon = GetMessageIcon(message.Severity);
            GUILayout.Label(icon, GUILayout.Width(16), GUILayout.Height(16));

            GUILayout.Label(message.Message, EditorStyles.wordWrappedLabel);

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(message.AssetPath))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);

                if (GUILayout.Button(message.AssetPath, EditorStyles.linkLabel))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(message.AssetPath);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(message.FixSuggestion))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);

                GUILayout.Label(StradaEditorIcons.InfoIcon, GUILayout.Width(16), GUILayout.Height(16));
                GUILayout.Label(message.FixSuggestion, EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        private Color GetMessageColor(ValidationResult.Severity severity)
        {
            switch (severity)
            {
                case ValidationResult.Severity.Error:
                    return Color.Lerp(GUI.backgroundColor, StradaEditorStyles.ErrorColor, 0.2f);
                case ValidationResult.Severity.Warning:
                    return Color.Lerp(GUI.backgroundColor, StradaEditorStyles.WarningColor, 0.2f);
                case ValidationResult.Severity.Info:
                    return Color.Lerp(GUI.backgroundColor, StradaEditorStyles.PrimaryColor, 0.15f);
                default:
                    return GUI.backgroundColor;
            }
        }

        private GUIContent GetMessageIcon(ValidationResult.Severity severity)
        {
            switch (severity)
            {
                case ValidationResult.Severity.Error:
                    return StradaEditorIcons.ErrorIcon;
                case ValidationResult.Severity.Warning:
                    return StradaEditorIcons.WarningIcon;
                case ValidationResult.Severity.Info:
                    return StradaEditorIcons.InfoIcon;
                default:
                    return GUIContent.none;
            }
        }

        private List<ValidationResult.ValidationMessage> GetFilteredMessages()
        {
            if (_currentResult == null)
                return new List<ValidationResult.ValidationMessage>();

            var messages = _currentResult.Messages;

            if (_filterMode != FilterMode.All)
            {
                var targetSeverity = _filterMode switch
                {
                    FilterMode.Errors => ValidationResult.Severity.Error,
                    FilterMode.Warnings => ValidationResult.Severity.Warning,
                    FilterMode.Info => ValidationResult.Severity.Info,
                    _ => ValidationResult.Severity.Info
                };

                messages = messages.Where(m => m.Severity == targetSeverity).ToList();
            }

            if (!string.IsNullOrEmpty(_searchFilter))
            {
                messages = messages.Where(m =>
                    m.Message.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (m.AssetPath != null && m.AssetPath.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
            }

            if (!string.IsNullOrEmpty(_categoryFilter))
            {
                messages = messages.Where(m => m.Category == _categoryFilter).ToList();
            }

            return messages;
        }

        private void InitializeValidators()
        {
            _validators = new List<AssetValidator>
            {
                new ScriptableObjectValidator()
            };
        }

        private void RunFullValidation()
        {
            _isValidating = true;
            _currentResult = new ValidationResult();

            RunModuleValidation();
            RunConfigValidation();

            _isValidating = false;
            Repaint();
        }

        private void RunModuleValidation()
        {
            _isValidating = true;

            if (_currentResult == null)
                _currentResult = new ValidationResult();

            var moduleResult = ModuleValidator.ValidateAllModules();
            _currentResult.Merge(moduleResult);

            _isValidating = false;
            Repaint();
        }

        private void RunConfigValidation()
        {
            _isValidating = true;

            if (_currentResult == null)
                _currentResult = new ValidationResult();

            var guids = AssetDatabase.FindAssets("t:ScriptableObject");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (asset == null)
                    continue;

                foreach (var validator in _validators)
                {
                    if (validator.CanValidate(asset))
                    {
                        var result = validator.Validate(asset);
                        _currentResult.Merge(result);
                    }
                }
            }

            _isValidating = false;
            Repaint();
        }
    }
}
