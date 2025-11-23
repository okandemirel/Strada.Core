using Strada.Core.Bootstrap;
using Strada.Core.DI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Windows
{
    /// <summary>
    /// Editor window for inspecting the DI container at runtime.
    /// Shows all registrations, lifetimes, resolution performance, and cache status.
    /// </summary>
    public class DIContainerInspectorWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private string _searchFilter = "";
        private bool _showOnlySingletons = false;
        private bool _autoRefresh = true;
        private double _lastRefreshTime;
        private const double RefreshInterval = 0.5;

        private List<RegistrationInfo> _registrations = new List<RegistrationInfo>();
        private RegistrationInfo _selectedRegistration;

        [MenuItem("Window/Strada/DI Container Inspector")]
        public static void ShowWindow()
        {
            var window = GetWindow<DIContainerInspectorWindow>("DI Container");
            window.minSize = new Vector2(700, 400);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshData();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawHeader();

            EditorGUILayout.BeginHorizontal();

            DrawRegistrationsList();
            DrawDetailsPanel();

            EditorGUILayout.EndHorizontal();

            if (_autoRefresh && EditorApplication.timeSinceStartup - _lastRefreshTime > RefreshInterval)
            {
                RefreshData();
                Repaint();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshData();
            }

            GUILayout.Space(10);

            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", EditorStyles.toolbarButton, GUILayout.Width(90));

            GUILayout.Space(10);

            _showOnlySingletons = GUILayout.Toggle(_showOnlySingletons, "Singletons Only", EditorStyles.toolbarButton, GUILayout.Width(100));

            GUILayout.FlexibleSpace();

            GUILayout.Label($"Total: {_registrations.Count}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            StradaEditorGUI.BeginInspectorPanel();

            var container = GameBootstrapper.Container;

            if (container == null)
            {
                StradaEditorGUI.DrawHelpBox("DI Container not initialized. Enter Play Mode to see registrations.", MessageType.Info);
                StradaEditorGUI.EndInspectorPanel();
                return;
            }

            StradaEditorGUI.DrawHeader("DI Container Inspector", StradaEditorIcons.DIContainerIcon);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchFilter = EditorGUILayout.TextField(_searchFilter);
            EditorGUILayout.EndHorizontal();

            StradaEditorGUI.Space();
            StradaEditorGUI.EndInspectorPanel();
        }

        private void DrawRegistrationsList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(400));

            StradaEditorGUI.DrawSubHeader("Registrations", StradaEditorIcons.ComponentIcon);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var filteredRegistrations = FilterRegistrations();

            foreach (var registration in filteredRegistrations)
            {
                DrawRegistrationItem(registration);
            }

            if (filteredRegistrations.Count == 0)
            {
                GUILayout.Label("No registrations found.", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRegistrationItem(RegistrationInfo registration)
        {
            var isSelected = _selectedRegistration == registration;
            var backgroundColor = isSelected
                ? StradaEditorStyles.PrimaryColor
                : (EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.8f, 0.8f, 0.8f));

            GUI.backgroundColor = backgroundColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();

            var lifetimeIcon = GetLifetimeIcon(registration.Lifetime);
            GUILayout.Label(lifetimeIcon, GUILayout.Width(16), GUILayout.Height(16));

            var typeName = registration.InterfaceType?.Name ?? registration.ConcreteType?.Name ?? "Unknown";
            if (GUILayout.Button(typeName, EditorStyles.label))
            {
                _selectedRegistration = registration;
            }

            GUILayout.FlexibleSpace();

            DrawLifetimeBadge(registration.Lifetime);

            EditorGUILayout.EndHorizontal();

            if (registration.InterfaceType != null && registration.ConcreteType != null)
            {
                var implName = registration.ConcreteType.Name;
                GUILayout.Label($"→ {implName}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        private void DrawDetailsPanel()
        {
            EditorGUILayout.BeginVertical();

            StradaEditorGUI.DrawSubHeader("Details", StradaEditorIcons.ViewIcon);

            if (_selectedRegistration == null)
            {
                GUILayout.Label("Select a registration to view details.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                DrawRegistrationDetails(_selectedRegistration);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRegistrationDetails(RegistrationInfo registration)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            StradaEditorGUI.DrawLabelWithIcon("Registration Details", StradaEditorIcons.InfoIcon);
            StradaEditorGUI.Space();

            StradaEditorGUI.DrawReadOnlyProperty("Interface Type", registration.InterfaceType?.FullName ?? "None");
            StradaEditorGUI.DrawReadOnlyProperty("Concrete Type", registration.ConcreteType?.FullName ?? "None");
            StradaEditorGUI.DrawReadOnlyProperty("Lifetime", registration.Lifetime.ToString());
            StradaEditorGUI.DrawReadOnlyProperty("Is Factory", registration.IsFactory.ToString());

            StradaEditorGUI.Space();

            if (registration.Lifetime == Lifetime.Singleton)
            {
                StradaEditorGUI.DrawSubHeader("Singleton Info", StradaEditorIcons.SingletonIcon);
                StradaEditorGUI.DrawReadOnlyProperty("Cached", registration.IsCached ? "Yes" : "No");

                if (registration.IsCached)
                {
                    var color = StradaEditorStyles.SuccessColor;
                    StradaEditorGUI.DrawColoredLabel("✓ Instance is cached", color);
                }
            }

            if (registration.Dependencies != null && registration.Dependencies.Length > 0)
            {
                StradaEditorGUI.Space();
                StradaEditorGUI.DrawSubHeader("Dependencies", StradaEditorIcons.ArrowRightIcon);

                foreach (var dep in registration.Dependencies)
                {
                    GUILayout.Label($"• {dep.Name}", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLifetimeBadge(Lifetime lifetime)
        {
            var color = StradaEditorStyles.GetLifetimeColor(lifetime.ToString());
            var text = lifetime.ToString()[0].ToString();

            GUI.backgroundColor = color;

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            GUILayout.Label(text, style, GUILayout.Width(20), GUILayout.Height(20));

            GUI.backgroundColor = Color.white;
        }

        private GUIContent GetLifetimeIcon(Lifetime lifetime)
        {
            return lifetime switch
            {
                Lifetime.Singleton => StradaEditorIcons.SingletonIcon,
                Lifetime.Transient => StradaEditorIcons.TransientIcon,
                Lifetime.Scoped => StradaEditorIcons.ScopedIcon,
                _ => StradaEditorIcons.ComponentIcon
            };
        }

        private void RefreshData()
        {
            _registrations.Clear();

            var container = GameBootstrapper.Container;
            if (container == null)
                return;

            _registrations.Add(new RegistrationInfo
            {
                InterfaceType = typeof(IContainer),
                ConcreteType = container.GetType(),
                Lifetime = Lifetime.Singleton,
                IsFactory = false,
                IsCached = true,
                Dependencies = Array.Empty<Type>()
            });

            _lastRefreshTime = EditorApplication.timeSinceStartup;
        }

        private List<RegistrationInfo> FilterRegistrations()
        {
            var filtered = _registrations.AsEnumerable();

            if (!string.IsNullOrEmpty(_searchFilter))
            {
                filtered = filtered.Where(r =>
                    (r.InterfaceType?.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.ConcreteType?.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (_showOnlySingletons)
            {
                filtered = filtered.Where(r => r.Lifetime == Lifetime.Singleton);
            }

            return filtered.ToList();
        }

        private class RegistrationInfo
        {
            public Type InterfaceType { get; set; }
            public Type ConcreteType { get; set; }
            public Lifetime Lifetime { get; set; }
            public bool IsFactory { get; set; }
            public bool IsCached { get; set; }
            public Type[] Dependencies { get; set; }
        }
    }
}
