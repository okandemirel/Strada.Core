using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Strada.Core.DI;

namespace Strada.Core.Editor.Windows
{
    /// <summary>
    /// Visual diagnostics window for the Strada DI Container.
    /// Shows all registered services, their lifetimes, and dependencies.
    /// Similar to VContainer's diagnostics but more comprehensive.
    /// </summary>
    public class StradaContainerDiagnosticsWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private string _searchFilter = "";
        private bool _showOnlySingletons = false;
        private bool _showOnlyTransient = false;
        private bool _showOnlyScoped = false;
        private LifetimeFilter _lifetimeFilter = LifetimeFilter.All;

        private GUIStyle _headerStyle;
        private GUIStyle _serviceStyle;
        private GUIStyle _dependencyStyle;
        private bool _stylesInitialized;

        private enum LifetimeFilter
        {
            All,
            Singleton,
            Transient,
            Scoped
        }

        [MenuItem("Strada/Diagnostics/DI Container", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<StradaContainerDiagnosticsWindow>("DI Container Diagnostics");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 5)
            };

            _serviceStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                padding = new RectOffset(20, 0, 2, 2)
            };

            _dependencyStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                padding = new RectOffset(40, 0, 1, 1)
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            DrawToolbar();
            DrawContainerInfo();
            DrawRegistrations();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Search
            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

            GUILayout.Space(10);

            // Lifetime filter
            GUILayout.Label("Filter:", GUILayout.Width(40));
            _lifetimeFilter = (LifetimeFilter)EditorGUILayout.EnumPopup(_lifetimeFilter, EditorStyles.toolbarPopup, GUILayout.Width(100));

            GUILayout.FlexibleSpace();

            // Refresh button
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawContainerInfo()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Container Status", _headerStyle);

            // Check if container exists
            var container = GetActiveContainer();
            
            if (container == null)
            {
                EditorGUILayout.HelpBox(
                    "No active DI container found. Container is created at runtime by GameBootstrapper.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("Status:", "Active");
                EditorGUILayout.LabelField("Registrations:", GetRegistrationCount(container).ToString());
                EditorGUILayout.LabelField("Type:", container.GetType().Name);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void DrawRegistrations()
        {
            EditorGUILayout.LabelField("Registered Services", _headerStyle);

            var container = GetActiveContainer();
            if (container == null)
            {
                EditorGUILayout.HelpBox(
                    "Start Play Mode to see registered services.",
                    MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var registrations = GetRegistrations(container);
            var filteredRegistrations = FilterRegistrations(registrations);

            if (filteredRegistrations.Count == 0)
            {
                EditorGUILayout.HelpBox("No services match the current filter.", MessageType.Info);
            }
            else
            {
                foreach (var registration in filteredRegistrations.OrderBy(r => r.ServiceType.Name))
                {
                    DrawServiceRegistration(registration);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawServiceRegistration(ServiceRegistration registration)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Service header
            EditorGUILayout.BeginHorizontal();
            
            // Lifetime icon
            var lifetimeColor = GetLifetimeColor(registration.Lifetime);
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = lifetimeColor;
            GUILayout.Label(GetLifetimeIcon(registration.Lifetime), GUILayout.Width(20));
            GUI.backgroundColor = previousColor;

            // Service name
            EditorGUILayout.LabelField(registration.ServiceType.Name, EditorStyles.boldLabel);
            
            // Lifetime badge
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = lifetimeColor;
            GUILayout.Label(registration.Lifetime.ToString(), EditorStyles.miniButton, GUILayout.Width(80));
            GUI.backgroundColor = previousColor;

            EditorGUILayout.EndHorizontal();

            // Implementation type
            if (registration.ImplementationType != null && 
                registration.ImplementationType != registration.ServiceType)
            {
                EditorGUILayout.LabelField("→ " + registration.ImplementationType.Name, _dependencyStyle);
            }

            // Dependencies
            if (registration.Dependencies != null && registration.Dependencies.Length > 0)
            {
                EditorGUILayout.LabelField("Dependencies:", EditorStyles.miniLabel);
                foreach (var dep in registration.Dependencies)
                {
                    EditorGUILayout.LabelField("  • " + dep.Name, _dependencyStyle);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private Color GetLifetimeColor(Lifetime lifetime)
        {
            return lifetime switch
            {
                Lifetime.Singleton => new Color(0.3f, 0.8f, 0.3f),
                Lifetime.Transient => new Color(0.8f, 0.6f, 0.2f),
                Lifetime.Scoped => new Color(0.4f, 0.6f, 0.9f),
                _ => Color.gray
            };
        }

        private string GetLifetimeIcon(Lifetime lifetime)
        {
            return lifetime switch
            {
                Lifetime.Singleton => "●",
                Lifetime.Transient => "○",
                Lifetime.Scoped => "◐",
                _ => "?"
            };
        }

        private IContainer GetActiveContainer()
        {
            // In play mode, get from GameBootstrapper
            if (Application.isPlaying)
            {
                return Strada.Core.Bootstrap.GameBootstrapper.Container;
            }
            return null;
        }

        private int GetRegistrationCount(IContainer container)
        {
            // This would be implemented based on container's internal structure
            // For now, return a placeholder
            return 0;
        }

        private List<ServiceRegistration> GetRegistrations(IContainer container)
        {
            // This would extract registrations from container
            // For now, return placeholder data
            return new List<ServiceRegistration>();
        }

        private List<ServiceRegistration> FilterRegistrations(List<ServiceRegistration> registrations)
        {
            var filtered = registrations;

            // Apply lifetime filter
            if (_lifetimeFilter != LifetimeFilter.All)
            {
                filtered = filtered.Where(r => r.Lifetime.ToString() == _lifetimeFilter.ToString()).ToList();
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                filtered = filtered.Where(r => 
                    r.ServiceType.Name.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (r.ImplementationType?.Name.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
            }

            return filtered;
        }

        private class ServiceRegistration
        {
            public System.Type ServiceType;
            public System.Type ImplementationType;
            public Lifetime Lifetime;
            public System.Type[] Dependencies;
        }
    }
}
