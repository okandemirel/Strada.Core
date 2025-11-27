using System;
using Strada.Core.DI;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Strada.Core.Editor.Graph
{
    /// <summary>
    /// A graph node representing a service registration in the DI container.
    /// Displays service type name, lifetime badge, and implementation type.
    /// Color-coded by lifetime: green=Singleton, orange=Transient, blue=Scoped.
    /// Requirements: 2.3, 2.4
    /// </summary>
    public class ServiceNode : Node
    {
        private static readonly Color SingletonColor = new Color(0.2f, 0.6f, 0.3f);
        private static readonly Color TransientColor = new Color(0.8f, 0.5f, 0.2f);
        private static readonly Color ScopedColor = new Color(0.3f, 0.5f, 0.8f);

        private Port _inputPort;
        private Port _outputPort;
        private Label _lifetimeBadge;
        private Label _implementationLabel;

        /// <summary>
        /// Gets the service type this node represents.
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// Gets the implementation type for this service.
        /// </summary>
        public Type ImplementationType { get; }

        /// <summary>
        /// Gets the lifetime of this service registration.
        /// </summary>
        public Lifetime Lifetime { get; }

        /// <summary>
        /// Gets the input port (dependencies flow in).
        /// </summary>
        public Port InputPort => _inputPort;

        /// <summary>
        /// Gets the output port (this service depends on others).
        /// </summary>
        public Port OutputPort => _outputPort;

        public ServiceNode(Type serviceType, Type implementationType, Lifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType ?? serviceType;
            Lifetime = lifetime;

            title = GetDisplayName(serviceType);
            
            SetupPorts();
            SetupContent();
            SetupContextMenu();
            ApplyLifetimeStyle();

            RefreshExpandedState();
            RefreshPorts();
        }

        private string GetDisplayName(Type type)
        {
            if (type == null) return "Unknown";
            
            var name = type.Name;
            
            // Handle generic types
            if (type.IsGenericType)
            {
                var genericArgs = type.GetGenericArguments();
                var baseName = name.Substring(0, name.IndexOf('`'));
                var argNames = string.Join(", ", Array.ConvertAll(genericArgs, t => t.Name));
                return $"{baseName}<{argNames}>";
            }
            
            return name;
        }

        private void SetupPorts()
        {
            // Input port - other services depend on this
            _inputPort = InstantiatePort(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(object));
            _inputPort.portName = "In";
            _inputPort.portColor = GetLifetimeColor();
            inputContainer.Add(_inputPort);

            // Output port - this service depends on others
            _outputPort = InstantiatePort(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Multi,
                typeof(object));
            _outputPort.portName = "Out";
            _outputPort.portColor = GetLifetimeColor();
            outputContainer.Add(_outputPort);
        }

        private void SetupContent()
        {
            // Lifetime badge
            _lifetimeBadge = new Label(Lifetime.ToString())
            {
                name = "lifetime-badge"
            };
            _lifetimeBadge.style.backgroundColor = GetLifetimeColor();
            _lifetimeBadge.style.color = Color.white;
            _lifetimeBadge.style.borderTopLeftRadius = 4;
            _lifetimeBadge.style.borderTopRightRadius = 4;
            _lifetimeBadge.style.borderBottomLeftRadius = 4;
            _lifetimeBadge.style.borderBottomRightRadius = 4;
            _lifetimeBadge.style.paddingLeft = 6;
            _lifetimeBadge.style.paddingRight = 6;
            _lifetimeBadge.style.paddingTop = 2;
            _lifetimeBadge.style.paddingBottom = 2;
            _lifetimeBadge.style.fontSize = 10;
            _lifetimeBadge.style.marginTop = 4;
            _lifetimeBadge.style.marginBottom = 4;
            _lifetimeBadge.style.alignSelf = Align.FlexStart;

            // Implementation type label
            var implTypeName = ImplementationType != ServiceType 
                ? $"→ {GetDisplayName(ImplementationType)}" 
                : "";
            
            _implementationLabel = new Label(implTypeName)
            {
                name = "implementation-label"
            };
            _implementationLabel.style.fontSize = 10;
            _implementationLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            _implementationLabel.style.marginTop = 2;
            _implementationLabel.style.unityFontStyleAndWeight = FontStyle.Italic;

            // Full type name tooltip
            var fullNameLabel = new Label(ServiceType.FullName ?? ServiceType.Name)
            {
                name = "fullname-label"
            };
            fullNameLabel.style.fontSize = 9;
            fullNameLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            fullNameLabel.style.marginTop = 2;
            fullNameLabel.style.overflow = Overflow.Hidden;
            fullNameLabel.style.textOverflow = TextOverflow.Ellipsis;
            fullNameLabel.style.maxWidth = 200;

            // Add to extension container
            var contentContainer = new VisualElement();
            contentContainer.style.paddingLeft = 8;
            contentContainer.style.paddingRight = 8;
            contentContainer.style.paddingBottom = 8;
            
            contentContainer.Add(_lifetimeBadge);
            if (!string.IsNullOrEmpty(implTypeName))
            {
                contentContainer.Add(_implementationLabel);
            }
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
                
                evt.menu.AppendAction("View Usages", _ => ViewUsages(),
                    DropdownMenuAction.Status.Normal);
                
                evt.menu.AppendAction("Inspect at Runtime", _ => InspectAtRuntime(),
                    _ => Application.isPlaying ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                
                evt.menu.AppendSeparator();
                
                evt.menu.AppendAction($"Copy Type Name", _ => CopyTypeName(),
                    DropdownMenuAction.Status.Normal);
                
                evt.menu.AppendAction($"Copy Full Name", _ => CopyFullName(),
                    DropdownMenuAction.Status.Normal);
            });
            
            this.AddManipulator(contextMenu);
        }

        private void ApplyLifetimeStyle()
        {
            var color = GetLifetimeColor();
            
            // Apply border color based on lifetime
            style.borderTopColor = color;
            style.borderBottomColor = color;
            style.borderLeftColor = color;
            style.borderRightColor = color;
            style.borderTopWidth = 2;
            style.borderBottomWidth = 2;
            style.borderLeftWidth = 2;
            style.borderRightWidth = 2;

            // Add lifetime class for CSS styling
            AddToClassList($"lifetime-{Lifetime.ToString().ToLower()}");
        }

        private Color GetLifetimeColor()
        {
            return Lifetime switch
            {
                Lifetime.Singleton => SingletonColor,
                Lifetime.Transient => TransientColor,
                Lifetime.Scoped => ScopedColor,
                _ => Color.gray
            };
        }

        #region Context Menu Actions

        private bool CanNavigateToSource()
        {
            // Check if we can find the source file for this type
            var type = ImplementationType ?? ServiceType;
            if (type == null) return false;

            var guids = AssetDatabase.FindAssets($"t:Script {type.Name}");
            return guids.Length > 0;
        }

        private void NavigateToSource()
        {
            var type = ImplementationType ?? ServiceType;
            if (type == null) return;

            var guids = AssetDatabase.FindAssets($"t:Script {type.Name}");
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

        private void ViewUsages()
        {
            // Open Unity's search window with type name
            var searchQuery = $"ref:{ServiceType.Name}";
            EditorApplication.ExecuteMenuItem("Edit/Find References In Scene");
        }

        private void InspectAtRuntime()
        {
            if (!Application.isPlaying) return;

            // Try to get the instance from the container
            var container = Bootstrap.GameBootstrapper.Container;
            if (container == null) return;

            try
            {
                var instance = container.Resolve(ServiceType);
                if (instance != null)
                {
                    Debug.Log($"[ServiceNode] Runtime instance of {ServiceType.Name}: {instance}");
                    
                    // If it's a UnityEngine.Object, select it
                    if (instance is UnityEngine.Object unityObj)
                    {
                        Selection.activeObject = unityObj;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ServiceNode] Could not resolve {ServiceType.Name}: {ex.Message}");
            }
        }

        private void CopyTypeName()
        {
            EditorGUIUtility.systemCopyBuffer = ServiceType.Name;
        }

        private void CopyFullName()
        {
            EditorGUIUtility.systemCopyBuffer = ServiceType.FullName ?? ServiceType.Name;
        }

        #endregion
    }
}
