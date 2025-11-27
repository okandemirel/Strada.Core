using System;
using System.Collections.Generic;
using System.Reflection;
using Strada.Core.Bridge;
using Strada.Core.MVCS;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Inspectors
{
    /// <summary>
    /// Custom inspector for ViewMediator components that displays:
    /// - All active ComponentBindings
    /// - Binding state (synced/error)
    /// - Force Sync and Force Push buttons
    /// Requirements: 10.1, 10.3, 10.4, 10.5
    /// </summary>
    [CustomEditor(typeof(View), true)]
    public class ViewMediatorInspector : UnityEditor.Editor
    {
        private static readonly Color SyncedColor = new Color(0.2f, 0.8f, 0.3f);
        private static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.3f);
        private static readonly Color NotSyncedColor = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color EntityDestroyedColor = new Color(0.8f, 0.5f, 0.2f);

        private bool _bindingsFoldout = true;
        private object _mediator;
        private IReadOnlyList<IComponentBinding> _bindings;
        private MethodInfo _syncMethod;
        private MethodInfo _pushMethod;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("ViewMediator bindings are only available during Play Mode.", MessageType.Info);
                return;
            }

            RefreshMediatorReference();

            if (_mediator == null)
            {
                return;
            }

            EditorGUILayout.Space();
            DrawMediatorSection();
        }

        private void RefreshMediatorReference()
        {
            var view = target as View;
            if (view == null) return;

            _mediator = FindMediatorForView(view);

            if (_mediator != null)
            {
                var bindingsProperty = _mediator.GetType().GetProperty("Bindings", 
                    BindingFlags.Public | BindingFlags.Instance);
                if (bindingsProperty != null)
                {
                    _bindings = bindingsProperty.GetValue(_mediator) as IReadOnlyList<IComponentBinding>;
                }

                _syncMethod = _mediator.GetType().GetMethod("SyncBindings", 
                    BindingFlags.Public | BindingFlags.Instance);
                _pushMethod = _mediator.GetType().GetMethod("PushBindings", 
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private object FindMediatorForView(View view)
        {
            var registryType = Type.GetType("Strada.Core.Bridge.MediatorRegistry, Strada.Core");
            if (registryType != null)
            {
                var instanceProperty = registryType.GetProperty("Instance", 
                    BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty != null)
                {
                    var registry = instanceProperty.GetValue(null);
                    if (registry != null)
                    {
                        var getMediatorMethod = registryType.GetMethod("GetMediatorForView",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (getMediatorMethod != null)
                        {
                            return getMediatorMethod.Invoke(registry, new object[] { view });
                        }
                    }
                }
            }

            var viewType = view.GetType();
            var fields = viewType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                if (field.FieldType.Name.Contains("Mediator") || 
                    field.FieldType.BaseType?.Name.Contains("ViewMediator") == true)
                {
                    return field.GetValue(view);
                }
            }

            return null;
        }

        private void DrawMediatorSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ViewMediator", EditorStyles.boldLabel);

            var isBoundProperty = _mediator.GetType().GetProperty("IsBound", 
                BindingFlags.Public | BindingFlags.Instance);
            bool isBound = isBoundProperty != null && (bool)isBoundProperty.GetValue(_mediator);

            var statusStyle = new GUIStyle(EditorStyles.miniLabel);
            statusStyle.normal.textColor = isBound ? SyncedColor : NotSyncedColor;
            EditorGUILayout.LabelField(isBound ? "Bound" : "Not Bound", statusStyle, GUILayout.Width(60));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = isBound;
            if (GUILayout.Button(new GUIContent("Force Sync", "Manually trigger SyncBindings to update view from ECS"), 
                GUILayout.Height(24)))
            {
                _syncMethod?.Invoke(_mediator, null);
                Repaint();
            }

            if (GUILayout.Button(new GUIContent("Force Push", "Manually trigger PushBindings to update ECS from view"), 
                GUILayout.Height(24)))
            {
                _pushMethod?.Invoke(_mediator, null);
                Repaint();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            if (_bindings != null && _bindings.Count > 0)
            {
                EditorGUILayout.Space();
                _bindingsFoldout = EditorGUILayout.Foldout(_bindingsFoldout, 
                    $"Component Bindings ({_bindings.Count})", true);

                if (_bindingsFoldout)
                {
                    EditorGUI.indentLevel++;
                    DrawBindingsList();
                    EditorGUI.indentLevel--;
                }
            }
            else if (isBound)
            {
                EditorGUILayout.HelpBox("No component bindings registered.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBindingsList()
        {
            foreach (var binding in _bindings)
            {
                DrawBindingEntry(binding);
            }
        }

        private void DrawBindingEntry(IComponentBinding binding)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var statusRect = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
            statusRect.y += 4;
            var statusColor = GetStatusColor(binding.SyncState);
            EditorGUI.DrawRect(statusRect, statusColor);

            var componentName = binding.ComponentType?.Name ?? "Unknown";
            EditorGUILayout.LabelField(componentName, GUILayout.MinWidth(100));

            var stateStyle = new GUIStyle(EditorStyles.miniLabel);
            stateStyle.normal.textColor = statusColor;
            EditorGUILayout.LabelField(binding.SyncState.ToString(), stateStyle, GUILayout.Width(80));

            if (binding.IsDirty)
            {
                var dirtyStyle = new GUIStyle(EditorStyles.miniLabel);
                dirtyStyle.normal.textColor = new Color(0.9f, 0.7f, 0.2f);
                EditorGUILayout.LabelField("*", dirtyStyle, GUILayout.Width(10));
            }

            EditorGUILayout.EndHorizontal();

            if (binding.SyncState == BindingSyncState.Error && !string.IsNullOrEmpty(binding.LastError))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(binding.LastError, MessageType.Error);
                EditorGUI.indentLevel--;
            }
        }

        private Color GetStatusColor(BindingSyncState state)
        {
            return state switch
            {
                BindingSyncState.Synced => SyncedColor,
                BindingSyncState.Error => ErrorColor,
                BindingSyncState.EntityDestroyed => EntityDestroyedColor,
                _ => NotSyncedColor
            };
        }

        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying && _mediator != null;
        }
    }
}
