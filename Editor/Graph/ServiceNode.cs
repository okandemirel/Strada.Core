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
            _inputPort = InstantiatePort(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(object));
            _inputPort.portName = "In";
            _inputPort.portColor = GetLifetimeColor();
            inputContainer.Add(_inputPort);

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
            var lifetimeBadge = new Label(Lifetime.ToString())
            {
                name = "lifetime-badge"
            };
            lifetimeBadge.style.backgroundColor = GetLifetimeColor();
            lifetimeBadge.style.color = Color.white;
            lifetimeBadge.style.borderTopLeftRadius = 4;
            lifetimeBadge.style.borderTopRightRadius = 4;
            lifetimeBadge.style.borderBottomLeftRadius = 4;
            lifetimeBadge.style.borderBottomRightRadius = 4;
            lifetimeBadge.style.paddingLeft = 6;
            lifetimeBadge.style.paddingRight = 6;
            lifetimeBadge.style.paddingTop = 2;
            lifetimeBadge.style.paddingBottom = 2;
            lifetimeBadge.style.fontSize = 10;
            lifetimeBadge.style.marginTop = 4;
            lifetimeBadge.style.marginBottom = 4;
            lifetimeBadge.style.alignSelf = Align.FlexStart;

            var implTypeName = ImplementationType != ServiceType
                ? $"→ {GetDisplayName(ImplementationType)}" 
                : "";
            
            var implementationLabel = new Label(implTypeName)
            {
                name = "implementation-label"
            };
            implementationLabel.style.fontSize = 10;
            implementationLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            implementationLabel.style.marginTop = 2;
            implementationLabel.style.unityFontStyleAndWeight = FontStyle.Italic;

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

            var contentContainer = new VisualElement();
            contentContainer.style.paddingLeft = 8;
            contentContainer.style.paddingRight = 8;
            contentContainer.style.paddingBottom = 8;
            
            contentContainer.Add(lifetimeBadge);
            if (!string.IsNullOrEmpty(implTypeName))
            {
                contentContainer.Add(implementationLabel);
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
                
                evt.menu.AppendAction("Copy Type Name", _ => CopyTypeName(),
                    DropdownMenuAction.Status.Normal);

                evt.menu.AppendAction("Copy Full Name", _ => CopyFullName(),
                    DropdownMenuAction.Status.Normal);
            });
            
            this.AddManipulator(contextMenu);
        }

        private void ApplyLifetimeStyle()
        {
            var color = GetLifetimeColor();

            style.borderTopColor = color;
            style.borderBottomColor = color;
            style.borderLeftColor = color;
            style.borderRightColor = color;
            style.borderTopWidth = 2;
            style.borderBottomWidth = 2;
            style.borderLeftWidth = 2;
            style.borderRightWidth = 2;

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

        private bool CanNavigateToSource()
        {
            return NodeHelper.CanNavigateToSource(ImplementationType ?? ServiceType);
        }

        private void NavigateToSource()
        {
            NodeHelper.NavigateToSource(ImplementationType ?? ServiceType);
        }

        private void ViewUsages()
        {
            EditorApplication.ExecuteMenuItem("Edit/Find References In Scene");
        }

        private void InspectAtRuntime()
        {
            if (!Application.isPlaying) return;

            var container = Bootstrap.GameBootstrapper.Container;
            if (container == null) return;

            try
            {
                var instance = container.Resolve(ServiceType);
                if (instance != null)
                {
                    Debug.Log($"[ServiceNode] Runtime instance of {ServiceType.Name}: {instance}");

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
    }
}
