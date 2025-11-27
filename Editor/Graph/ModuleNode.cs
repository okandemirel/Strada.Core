using System;
using System.Collections.Generic;
using Strada.Core.Editor.DataProviders.Models;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Strada.Core.Editor.Graph
{
    /// <summary>
    /// A graph node representing a module in the module registry.
    /// Displays module name, priority badge, and dependency information.
    /// Requirements: 6.1, 6.2
    /// </summary>
    public class ModuleNode : Node
    {
        private static readonly Color ModuleColor = new Color(0.4f, 0.3f, 0.6f);
        private static readonly Color HighPriorityColor = new Color(0.6f, 0.3f, 0.3f);
        private static readonly Color LowPriorityColor = new Color(0.3f, 0.5f, 0.3f);

        private Port _inputPort;
        private Port _outputPort;
        private Label _priorityBadge;
        private Label _dependencyCountLabel;

        /// <summary>
        /// Gets the module type this node represents.
        /// </summary>
        public Type ModuleType { get; }

        /// <summary>
        /// Gets the module name.
        /// </summary>
        public string ModuleName { get; }

        /// <summary>
        /// Gets the module priority.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Gets the module dependencies.
        /// </summary>
        public IReadOnlyList<Type> Dependencies { get; }

        /// <summary>
        /// Gets whether the module is initialized.
        /// </summary>
        public bool IsInitialized { get; }

        /// <summary>
        /// Gets the input port (dependencies flow in).
        /// </summary>
        public Port InputPort => _inputPort;

        /// <summary>
        /// Gets the output port (this module depends on others).
        /// </summary>
        public Port OutputPort => _outputPort;

        public ModuleNode(ModuleInfoData moduleInfo)
        {
            ModuleType = moduleInfo.ModuleType;
            ModuleName = moduleInfo.Name;
            Priority = moduleInfo.Priority;
            Dependencies = moduleInfo.Dependencies;
            IsInitialized = moduleInfo.IsInitialized;

            title = ModuleName;

            SetupPorts();
            SetupContent();
            SetupContextMenu();
            ApplyStyle();

            RefreshExpandedState();
            RefreshPorts();
        }

        private void SetupPorts()
        {
            // Input port - other modules depend on this
            _inputPort = InstantiatePort(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(object));
            _inputPort.portName = "In";
            _inputPort.portColor = ModuleColor;
            inputContainer.Add(_inputPort);

            // Output port - this module depends on others
            _outputPort = InstantiatePort(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Multi,
                typeof(object));
            _outputPort.portName = "Out";
            _outputPort.portColor = ModuleColor;
            outputContainer.Add(_outputPort);
        }

        private void SetupContent()
        {
            // Priority badge
            _priorityBadge = new Label($"Priority: {Priority}")
            {
                name = "priority-badge"
            };
            _priorityBadge.style.backgroundColor = GetPriorityColor();
            _priorityBadge.style.color = Color.white;
            _priorityBadge.style.borderTopLeftRadius = 4;
            _priorityBadge.style.borderTopRightRadius = 4;
            _priorityBadge.style.borderBottomLeftRadius = 4;
            _priorityBadge.style.borderBottomRightRadius = 4;
            _priorityBadge.style.paddingLeft = 6;
            _priorityBadge.style.paddingRight = 6;
            _priorityBadge.style.paddingTop = 2;
            _priorityBadge.style.paddingBottom = 2;
            _priorityBadge.style.fontSize = 10;
            _priorityBadge.style.marginTop = 4;
            _priorityBadge.style.marginBottom = 4;
            _priorityBadge.style.alignSelf = Align.FlexStart;

            // Dependency count label
            _dependencyCountLabel = new Label($"Dependencies: {Dependencies.Count}")
            {
                name = "dependency-count-label"
            };
            _dependencyCountLabel.style.fontSize = 10;
            _dependencyCountLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            _dependencyCountLabel.style.marginTop = 2;

            // Initialization status
            var statusLabel = new Label(IsInitialized ? "✓ Initialized" : "○ Not Initialized")
            {
                name = "status-label"
            };
            statusLabel.style.fontSize = 10;
            statusLabel.style.color = IsInitialized 
                ? new Color(0.4f, 0.8f, 0.4f) 
                : new Color(0.8f, 0.8f, 0.4f);
            statusLabel.style.marginTop = 2;

            // Full type name tooltip
            var fullNameLabel = new Label(ModuleType.FullName ?? ModuleType.Name)
            {
                name = "fullname-label"
            };
            fullNameLabel.style.fontSize = 9;
            fullNameLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            fullNameLabel.style.marginTop = 4;
            fullNameLabel.style.overflow = Overflow.Hidden;
            fullNameLabel.style.textOverflow = TextOverflow.Ellipsis;
            fullNameLabel.style.maxWidth = 200;

            // Add to extension container
            var contentContainer = new VisualElement();
            contentContainer.style.paddingLeft = 8;
            contentContainer.style.paddingRight = 8;
            contentContainer.style.paddingBottom = 8;

            contentContainer.Add(_priorityBadge);
            contentContainer.Add(_dependencyCountLabel);
            contentContainer.Add(statusLabel);
            contentContainer.Add(fullNameLabel);

            extensionContainer.Add(contentContainer);
            RefreshExpandedState();
        }

        private void SetupContextMenu()
        {
            var contextMenu = new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Navigate to Source", _ => NavigateToSource(),
                    _ => CanNavigateToSource() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

                evt.menu.AppendAction("View Dependencies", _ => ViewDependencies(),
                    DropdownMenuAction.Status.Normal);

                evt.menu.AppendSeparator();

                evt.menu.AppendAction("Copy Module Name", _ => CopyModuleName(),
                    DropdownMenuAction.Status.Normal);

                evt.menu.AppendAction("Copy Type Name", _ => CopyTypeName(),
                    DropdownMenuAction.Status.Normal);
            });

            this.AddManipulator(contextMenu);
        }

        private void ApplyStyle()
        {
            // Apply border color
            style.borderTopColor = ModuleColor;
            style.borderBottomColor = ModuleColor;
            style.borderLeftColor = ModuleColor;
            style.borderRightColor = ModuleColor;
            style.borderTopWidth = 2;
            style.borderBottomWidth = 2;
            style.borderLeftWidth = 2;
            style.borderRightWidth = 2;

            AddToClassList("module-node");
        }

        private Color GetPriorityColor()
        {
            // Higher priority = more red, lower priority = more green
            if (Priority > 100) return HighPriorityColor;
            if (Priority < 0) return LowPriorityColor;
            
            var t = Priority / 100f;
            return Color.Lerp(LowPriorityColor, HighPriorityColor, t);
        }

        #region Context Menu Actions

        private bool CanNavigateToSource()
        {
            if (ModuleType == null) return false;
            var guids = AssetDatabase.FindAssets($"t:Script {ModuleType.Name}");
            return guids.Length > 0;
        }

        private void NavigateToSource()
        {
            if (ModuleType == null) return;

            var guids = AssetDatabase.FindAssets($"t:Script {ModuleType.Name}");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (asset != null)
                {
                    AssetDatabase.OpenAsset(asset);
                }
            }
        }

        private void ViewDependencies()
        {
            var depNames = string.Join("\n", 
                System.Linq.Enumerable.Select(Dependencies, d => $"  - {d.Name}"));
            
            Debug.Log($"[ModuleNode] Dependencies of {ModuleName}:\n{depNames}");
        }

        private void CopyModuleName()
        {
            EditorGUIUtility.systemCopyBuffer = ModuleName;
        }

        private void CopyTypeName()
        {
            EditorGUIUtility.systemCopyBuffer = ModuleType.FullName ?? ModuleType.Name;
        }

        #endregion
    }
}
