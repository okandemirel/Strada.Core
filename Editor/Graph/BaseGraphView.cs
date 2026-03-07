using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Strada.Core.Editor.Graph
{
    public abstract class BaseGraphView<TNode> : GraphView where TNode : Node
    {
        private readonly Dictionary<Type, TNode> _nodeMap = new Dictionary<Type, TNode>();
        private readonly List<TNode> _allNodes = new List<TNode>();
        private readonly List<DependencyEdgeView> _allEdges = new List<DependencyEdgeView>();

        private bool _hasCycle;
        private List<Type> _cyclePath;

        public event Action<TNode> OnNodeSelected;
        public event Action<TNode> OnNodeHovered;
        public event Action OnGraphRefreshed;

        public bool HasCycle => _hasCycle;
        public IReadOnlyList<Type> CyclePath => _cyclePath;

        protected IReadOnlyDictionary<Type, TNode> NodeMap => _nodeMap;
        protected IReadOnlyList<TNode> AllNodes => _allNodes;
        protected IReadOnlyList<DependencyEdgeView> AllEdges => _allEdges;

        protected BaseGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            var gridBackground = new GridBackground();
            Insert(0, gridBackground);
            gridBackground.StretchToParentSize();

            style.flexGrow = 1;
            style.flexShrink = 1;

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());

            var miniMap = new MiniMap { anchored = true };
            miniMap.SetPosition(new Rect(10, 30, 200, 140));
            Add(miniMap);

            styleSheets.Add(Resources.Load<StyleSheet>("DependencyGraphStyles"));

            graphViewChanged += OnGraphViewChanged;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.movedElements != null)
            {
                foreach (var element in change.movedElements)
                {
                    if (element is TNode node)
                    {
                        OnNodeSelected?.Invoke(node);
                    }
                }
            }
            return change;
        }

        protected void InvokeGraphRefreshed() => OnGraphRefreshed?.Invoke();

        protected void AddNodeToGraph(Type key, TNode node)
        {
            _nodeMap[key] = node;
            _allNodes.Add(node);
            AddElement(node);
        }

        protected DependencyEdgeView CreateEdge(Port outputPort, Port inputPort, bool isCircular)
        {
            var edge = new DependencyEdgeView
            {
                output = outputPort,
                input = inputPort,
                IsCircular = isCircular
            };

            outputPort.Connect(edge);
            inputPort.Connect(edge);

            _allEdges.Add(edge);
            AddElement(edge);
            return edge;
        }

        protected void SetCycleInfo(bool hasCycle, List<Type> cyclePath)
        {
            _hasCycle = hasCycle;
            _cyclePath = cyclePath;
        }

        protected void RegisterNodeCallbacks(TNode node)
        {
            node.RegisterCallback<MouseEnterEvent>(evt => OnNodeHovered?.Invoke(node));
            node.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.clickCount == 1)
                {
                    OnNodeSelected?.Invoke(node);
                }
            });
        }

        public void HighlightNodeDependencies(TNode node, Port inputPort, Port outputPort)
        {
            foreach (var n in _allNodes)
            {
                n.RemoveFromClassList("highlighted");
                n.RemoveFromClassList("dependency");
                n.RemoveFromClassList("dependent");
            }

            foreach (var e in _allEdges)
            {
                e.RemoveFromClassList("highlighted");
            }

            if (node == null) return;

            node.AddToClassList("highlighted");

            foreach (var edge in outputPort.connections)
            {
                if (edge.input?.node is TNode depNode)
                {
                    depNode.AddToClassList("dependency");
                    edge.AddToClassList("highlighted");
                }
            }

            foreach (var edge in inputPort.connections)
            {
                if (edge.output?.node is TNode depNode)
                {
                    depNode.AddToClassList("dependent");
                    edge.AddToClassList("highlighted");
                }
            }
        }

        public void FocusOnNode(TNode node)
        {
            if (node == null) return;

            ClearSelection();
            AddToSelection(node);
            FrameSelection();
        }

        protected void ClearGraph()
        {
            foreach (var edge in _allEdges)
            {
                RemoveElement(edge);
            }
            foreach (var node in _allNodes)
            {
                RemoveElement(node);
            }

            _nodeMap.Clear();
            _allNodes.Clear();
            _allEdges.Clear();
            _hasCycle = false;
            _cyclePath = null;

            OnClearGraph();
        }

        protected virtual void OnClearGraph() { }

        protected void ShowMessage(string text)
        {
            var label = new Label(text)
            {
                style =
                {
                    position = Position.Absolute,
                    left = 20,
                    top = 20,
                    fontSize = 14,
                    color = new Color(0.7f, 0.7f, 0.7f)
                }
            };
            Add(label);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(p =>
                p.direction != startPort.direction &&
                p.node != startPort.node).ToList();
        }
    }
}
