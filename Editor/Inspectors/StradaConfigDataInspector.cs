using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor
{
    /// <summary>
    /// Base class for all Strada ConfigData (CD_*) inspectors.
    /// Provides consistent UI, validation display, and action buttons.
    /// </summary>
    public abstract class StradaConfigDataInspector<T> : UnityEditor.Editor where T : ScriptableObject
    {
        protected T Target => target as T;

        private bool _showAdvanced = false;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            StradaEditorGUI.BeginInspectorPanel();

            DrawHeader();
            StradaEditorGUI.Space();

            DrawValidationStatus();
            StradaEditorGUI.Space();

            DrawConfigSection();
            StradaEditorGUI.Space();

            DrawComputedPropertiesSection();
            StradaEditorGUI.Space();

            DrawActionButtons();

            StradaEditorGUI.EndInspectorPanel();

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }

        /// <summary>
        /// Draws the header with icon and title.
        /// </summary>
        protected virtual void DrawHeader()
        {
            StradaEditorGUI.DrawHeader(GetHeaderTitle(), StradaEditorIcons.ConfigDataIcon);
        }

        /// <summary>
        /// Draws the validation status box.
        /// </summary>
        protected virtual void DrawValidationStatus()
        {
            var isValid = IsConfigValid(out var errorMessage);

            if (isValid)
            {
                StradaEditorGUI.DrawValidationBox(true, "✓ Configuration is valid");
            }
            else
            {
                StradaEditorGUI.DrawValidationBox(false, $"✗ {errorMessage}");
            }
        }

        /// <summary>
        /// Draws the main configuration section.
        /// </summary>
        protected virtual void DrawConfigSection()
        {
            _showAdvanced = StradaEditorGUI.BeginFoldoutSection("Configuration", _showAdvanced, StradaEditorIcons.SettingsIcon);

            if (_showAdvanced)
            {
                StradaEditorGUI.BeginIndent();
                DrawConfigProperties();
                StradaEditorGUI.EndIndent();
            }

            StradaEditorGUI.EndFoldoutSection();
        }

        /// <summary>
        /// Draws the computed properties section (read-only).
        /// </summary>
        protected virtual void DrawComputedPropertiesSection()
        {
            var computedProps = GetComputedProperties();
            if (computedProps == null || computedProps.Length == 0)
                return;

            var foldout = StradaEditorGUI.BeginFoldoutSection("Computed Properties (Read-Only)", false, StradaEditorIcons.ViewIcon);

            if (foldout)
            {
                StradaEditorGUI.BeginIndent();

                foreach (var (label, value) in computedProps)
                {
                    StradaEditorGUI.DrawReadOnlyProperty(label, value);
                }

                StradaEditorGUI.EndIndent();
            }

            StradaEditorGUI.EndFoldoutSection();
        }

        /// <summary>
        /// Draws action buttons at the bottom.
        /// </summary>
        protected virtual void DrawActionButtons()
        {
            StradaEditorGUI.DrawActionButtons(
                ("Reset to Default", OnResetToDefault),
                ("Clone Config", OnCloneConfig),
                ("Validate", OnValidate)
            );
        }

        /// <summary>
        /// Override this to provide custom header title.
        /// </summary>
        protected abstract string GetHeaderTitle();

        /// <summary>
        /// Override this to draw config properties.
        /// </summary>
        protected abstract void DrawConfigProperties();

        /// <summary>
        /// Override this to validate the configuration.
        /// </summary>
        protected abstract bool IsConfigValid(out string errorMessage);

        /// <summary>
        /// Override this to provide computed properties for display.
        /// </summary>
        protected virtual (string label, string value)[] GetComputedProperties()
        {
            return null;
        }

        /// <summary>
        /// Override this to implement reset to default behavior.
        /// </summary>
        protected virtual void OnResetToDefault()
        {
            if (EditorUtility.DisplayDialog(
                "Reset to Default",
                "Are you sure you want to reset this configuration to default values?",
                "Yes", "Cancel"))
            {
                Undo.RecordObject(target, "Reset Config to Default");
                ResetToDefaultImplementation();
                EditorUtility.SetDirty(target);
            }
        }

        /// <summary>
        /// Override this to implement the actual reset logic.
        /// </summary>
        protected abstract void ResetToDefaultImplementation();

        /// <summary>
        /// Clones the current configuration as a new asset.
        /// </summary>
        protected virtual void OnCloneConfig()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Clone Configuration",
                $"{target.name}_Clone",
                "asset",
                "Choose where to save the cloned configuration");

            if (string.IsNullOrEmpty(path))
                return;

            var clone = Instantiate(target);
            AssetDatabase.CreateAsset(clone, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(clone);
            Selection.activeObject = clone;
        }

        /// <summary>
        /// Validates the configuration and shows the result.
        /// </summary>
        protected virtual void OnValidate()
        {
            var isValid = IsConfigValid(out var errorMessage);

            if (isValid)
            {
                EditorUtility.DisplayDialog("Validation", "✓ Configuration is valid!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Validation Error", $"✗ {errorMessage}", "OK");
            }
        }
    }
}
