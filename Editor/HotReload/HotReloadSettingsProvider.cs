using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.HotReload
{
    /// <summary>
    /// Settings provider for hot reload configuration in Project Settings.
    /// </summary>
    public class HotReloadSettingsProvider : SettingsProvider
    {
        private const string SettingsPath = "Project/Strada/Hot Reload";
        
        public HotReloadSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }
        
        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Hot Reload Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.HelpBox(
                "Hot Reload allows CD_ config assets to be reloaded during Play Mode without restarting. " +
                "Services that depend on configs will be notified of changes automatically.",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // Enable/Disable toggle
            var enabled = EditorGUILayout.Toggle(
                new GUIContent("Enable Hot Reload", "When enabled, config changes during Play Mode will be automatically applied."),
                HotReloadManager.IsEnabled);
            
            if (enabled != HotReloadManager.IsEnabled)
            {
                HotReloadManager.IsEnabled = enabled;
            }
            
            EditorGUILayout.Space(5);
            
            // Notifications toggle
            var notifications = EditorGUILayout.Toggle(
                new GUIContent("Show Notifications", "Display console messages when configs are reloaded."),
                HotReloadManager.NotificationsEnabled);
            
            if (notifications != HotReloadManager.NotificationsEnabled)
            {
                HotReloadManager.NotificationsEnabled = notifications;
            }
            
            EditorGUILayout.Space(15);
            
            // Status section
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Play Mode Active", Application.isPlaying);
                EditorGUILayout.Toggle("Processing", HotReloadManager.IsProcessing);
                EditorGUILayout.IntField("Pending Changes", HotReloadManager.PendingChangeCount);
            }
            
            // Last reload info
            var lastState = HotReloadManager.LastReloadState;
            if (lastState.HasActivity)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Last Reload", EditorStyles.boldLabel);
                
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Config", lastState.LastConfigPath ?? "None");
                    EditorGUILayout.TextField("Time", lastState.LastReloadTime.ToString("HH:mm:ss"));
                    
                    var statusColor = lastState.WasSuccessful ? Color.green : Color.red;
                    var prevColor = GUI.color;
                    GUI.color = statusColor;
                    EditorGUILayout.TextField("Status", lastState.WasSuccessful ? "Success" : "Failed");
                    GUI.color = prevColor;
                    
                    if (!lastState.WasSuccessful && !string.IsNullOrEmpty(lastState.ErrorMessage))
                    {
                        EditorGUILayout.HelpBox(lastState.ErrorMessage, MessageType.Error);
                    }
                }
            }
            
            EditorGUILayout.Space(15);
            
            // Dependency tracking section
            EditorGUILayout.LabelField("Dependency Tracking", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            var dependencyMap = HotReloadManager.GetDependencyMap();
            if (dependencyMap.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No services have registered for config change notifications yet. " +
                    "Services can register using HotReloadManager.RegisterDependentService<TConfig>().",
                    MessageType.Info);
            }
            else
            {
                foreach (var kvp in dependencyMap)
                {
                    EditorGUILayout.LabelField($"  {kvp.Key.Name}", $"{kvp.Value} service(s)");
                }
            }
            
            EditorGUILayout.Space(15);
            
            // Actions
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = HotReloadManager.PendingChangeCount > 0;
            if (GUILayout.Button("Clear Pending Changes", GUILayout.Width(150)))
            {
                HotReloadManager.ClearPendingChanges();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
        }
        
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new HotReloadSettingsProvider(SettingsPath, SettingsScope.Project)
            {
                keywords = new HashSet<string>(new[] { "Strada", "Hot Reload", "Config", "Live", "Reload" })
            };
        }
    }
}
