using Strada.Core.Editor.Graph;
using Strada.Core.Modules;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Windows
{
    /// <summary>
    /// Graph node representing a Strada module with its registrations and dependencies.
    /// </summary>
    public class ModuleNode : GraphNode
    {
        public Type ModuleType { get; set; }
        public int RegistrationCount { get; set; }
        public bool HasECS { get; set; }
        public bool IsLoaded { get; set; }
        public string[] Dependencies { get; set; }

        private bool _showDetails;
        private const float CollapsedHeight = 80f;
        private const float ExpandedHeight = 140f;

        public ModuleNode(Type moduleType, string title)
        {
            ModuleType = moduleType;
            Title = title;
            Position = new Rect(0, 0, 200, CollapsedHeight);
            Dependencies = Array.Empty<string>();

            Color = HasECS
                ? StradaEditorStyles.PrimaryColor
                : StradaEditorStyles.SuccessColor;
        }

        protected override void DrawContent()
        {
            var contentRect = new Rect(
                Position.x + Padding,
                Position.y + HeaderHeight + Padding,
                Position.width - Padding * 2,
                Position.height - HeaderHeight - Padding * 2);

            GUILayout.BeginArea(contentRect);

            DrawModuleInfo();

            if (_showDetails)
            {
                GUILayout.Space(4);
                DrawDetailedInfo();
            }

            GUILayout.EndArea();

            DrawStatusIndicator();
        }

        /// <summary>
        /// Draws basic module information.
        /// </summary>
        private void DrawModuleInfo()
        {
            var iconContent = HasECS
                ? StradaEditorIcons.WorldIcon
                : StradaEditorIcons.ModuleIcon;

            GUILayout.BeginHorizontal();
            GUILayout.Label(iconContent, GUILayout.Width(16), GUILayout.Height(16));
            GUILayout.Label(HasECS ? "Hybrid Module" : "MVCS Module", GetSmallLabelStyle());
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            var registrationLabel = $"{RegistrationCount} Registration{(RegistrationCount != 1 ? "s" : "")}";
            GUILayout.Label(registrationLabel, GetSmallLabelStyle());

            if (Dependencies.Length > 0)
            {
                var depLabel = $"{Dependencies.Length} Dependenc{(Dependencies.Length != 1 ? "ies" : "y")}";
                GUILayout.Label(depLabel, GetSmallLabelStyle());
            }
        }

        /// <summary>
        /// Draws detailed module information when expanded.
        /// </summary>
        private void DrawDetailedInfo()
        {
            GUILayout.Label("Dependencies:", GetBoldSmallLabelStyle());

            if (Dependencies.Length > 0)
            {
                foreach (var dep in Dependencies)
                {
                    GUILayout.Label($"• {dep}", GetTinyLabelStyle());
                }
            }
            else
            {
                GUILayout.Label("(none)", GetTinyLabelStyle());
            }
        }

        /// <summary>
        /// Draws a status indicator in the corner.
        /// </summary>
        private void DrawStatusIndicator()
        {
            var indicatorSize = 12f;
            var indicatorRect = new Rect(
                Position.xMax - indicatorSize - 4,
                Position.yMax - indicatorSize - 4,
                indicatorSize,
                indicatorSize);

            var statusColor = IsLoaded
                ? StradaEditorStyles.SuccessColor
                : StradaEditorStyles.SubtleTextColor;

            EditorGUI.DrawRect(indicatorRect, statusColor);

            var borderRect = new Rect(
                indicatorRect.x - 1,
                indicatorRect.y - 1,
                indicatorRect.width + 2,
                indicatorRect.height + 2);

            DrawRect(borderRect, Color.white, 1f);
        }

        public override void OnClick()
        {
            base.OnClick();
            _showDetails = !_showDetails;

            Position = new Rect(
                Position.x,
                Position.y,
                Position.width,
                _showDetails ? ExpandedHeight : CollapsedHeight);
        }

        public override void OnDoubleClick()
        {
            base.OnDoubleClick();

            if (ModuleType != null)
            {
                var script = AssetDatabase.FindAssets($"t:Script {ModuleType.Name}")
                    .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(script))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(script);
                    AssetDatabase.OpenAsset(asset);
                }
            }
        }

        public override void ShowContextMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Toggle Details"), _showDetails, () => OnClick());
            menu.AddItem(new GUIContent("Open Script"), false, () => OnDoubleClick());
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("View Registrations"), false, () =>
            {
                Debug.Log($"[{Title}] Has {RegistrationCount} registrations");
            });

            if (Dependencies.Length > 0)
            {
                menu.AddItem(new GUIContent("View Dependencies"), false, () =>
                {
                    var deps = string.Join("\n", Dependencies.Select(d => $"  • {d}"));
                    Debug.Log($"[{Title}] Dependencies:\n{deps}");
                });
            }

            menu.ShowAsContext();
        }

        private GUIStyle GetSmallLabelStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = StradaEditorStyles.TextColor }
            };
        }

        private GUIStyle GetBoldSmallLabelStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = StradaEditorStyles.TextColor }
            };
        }

        private GUIStyle GetTinyLabelStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                normal = { textColor = StradaEditorStyles.SubtleTextColor }
            };
        }
    }
}
