using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Graph
{
    /// <summary>
    /// Base class for graph-based editor windows with zoom, pan, and node management.
    /// </summary>
    public abstract class GraphWindow : EditorWindow
    {
        protected List<GraphNode> Nodes { get; } = new List<GraphNode>();
        protected List<GraphConnection> Connections { get; } = new List<GraphConnection>();

        private Vector2 _pan = Vector2.zero;
        private float _zoom = 1f;
        private Vector2 _lastMousePosition;
        private bool _isPanning;
        private GraphNode _selectedNode;
        private GraphNode _draggingNode;

        private const float MinZoom = 0.5f;
        private const float MaxZoom = 2f;
        private const float ZoomSpeed = 0.1f;
        private const float GridSpacing = 20f;

        protected virtual void OnGUI()
        {
            DrawGrid();

            BeginZoomArea();

            DrawConnections();
            DrawNodes();

            EndZoomArea();

            HandleEvents();
            DrawToolbar();

            if (GUI.changed)
                Repaint();
        }

        /// <summary>
        /// Draws a grid background.
        /// </summary>
        private void DrawGrid()
        {
            var gridColor = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.18f, 0.18f)
                : new Color(0.85f, 0.85f, 0.85f);

            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), gridColor);

            Handles.BeginGUI();
            Handles.color = StradaEditorStyles.SubtleTextColor;

            var spacing = GridSpacing * _zoom;
            var offsetX = _pan.x % spacing;
            var offsetY = _pan.y % spacing;

            for (float x = offsetX; x < position.width; x += spacing)
            {
                Handles.DrawLine(new Vector3(x, 0), new Vector3(x, position.height));
            }

            for (float y = offsetY; y < position.height; y += spacing)
            {
                Handles.DrawLine(new Vector3(0, y), new Vector3(position.width, y));
            }

            Handles.EndGUI();
        }

        /// <summary>
        /// Begins the zoom area with current zoom and pan.
        /// </summary>
        private void BeginZoomArea()
        {
            GUI.EndClip();

            var scale = Vector3.one * _zoom;
            var translation = Matrix4x4.Translate(_pan);
            var scaleMatrix = Matrix4x4.Scale(scale);

            GUI.matrix = translation * scaleMatrix;

            var clipRect = new Rect(
                -_pan.x / _zoom,
                -_pan.y / _zoom,
                position.width / _zoom,
                position.height / _zoom);

            GUI.BeginClip(clipRect);
        }

        /// <summary>
        /// Ends the zoom area and restores GUI matrix.
        /// </summary>
        private void EndZoomArea()
        {
            GUI.EndClip();
            GUI.matrix = Matrix4x4.identity;

            var toolbarHeight = 20f;
            GUI.BeginClip(new Rect(0, toolbarHeight, position.width, position.height - toolbarHeight));
        }

        /// <summary>
        /// Draws all connections between nodes.
        /// </summary>
        private void DrawConnections()
        {
            foreach (var connection in Connections)
            {
                connection?.Draw();
            }
        }

        /// <summary>
        /// Draws all nodes.
        /// </summary>
        private void DrawNodes()
        {
            foreach (var node in Nodes)
            {
                node?.Draw();
            }
        }

        /// <summary>
        /// Draws the toolbar at the top.
        /// </summary>
        private void DrawToolbar()
        {
            var toolbarRect = new Rect(0, 0, position.width, 20);
            GUI.backgroundColor = StradaEditorStyles.HeaderColor;
            GUI.Box(toolbarRect, "", EditorStyles.toolbar);
            GUI.backgroundColor = Color.white;

            GUILayout.BeginArea(toolbarRect);
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            DrawToolbarContent();

            GUILayout.FlexibleSpace();

            GUILayout.Label($"Zoom: {_zoom:F2}x", EditorStyles.miniLabel, GUILayout.Width(80));
            GUILayout.Label($"Nodes: {Nodes.Count}", EditorStyles.miniLabel, GUILayout.Width(80));

            if (GUILayout.Button("Reset View", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                ResetView();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Override to draw custom toolbar content.
        /// </summary>
        protected virtual void DrawToolbarContent()
        {
        }

        /// <summary>
        /// Handles input events (mouse, keyboard).
        /// </summary>
        private void HandleEvents()
        {
            var e = Event.current;
            var mousePos = e.mousePosition;

            switch (e.type)
            {
                case EventType.MouseDown:
                    HandleMouseDown(mousePos, e.button);
                    break;

                case EventType.MouseDrag:
                    HandleMouseDrag(mousePos, e.button);
                    break;

                case EventType.MouseUp:
                    HandleMouseUp();
                    break;

                case EventType.ScrollWheel:
                    HandleScroll(mousePos, e.delta);
                    break;

                case EventType.KeyDown:
                    HandleKeyDown(e.keyCode);
                    break;
            }

            _lastMousePosition = mousePos;
        }

        /// <summary>
        /// Handles mouse down events.
        /// </summary>
        private void HandleMouseDown(Vector2 mousePos, int button)
        {
            var worldPos = ScreenToWorld(mousePos);

            if (button == 0)
            {
                var clickedNode = GetNodeAtPosition(worldPos);

                if (clickedNode != null)
                {
                    SelectNode(clickedNode);
                    _draggingNode = clickedNode;
                    _draggingNode.OnMouseDown(worldPos);
                }
                else
                {
                    DeselectAll();
                }

                Repaint();
            }
            else if (button == 2)
            {
                _isPanning = true;
            }
        }

        /// <summary>
        /// Handles mouse drag events.
        /// </summary>
        private void HandleMouseDrag(Vector2 mousePos, int button)
        {
            var delta = mousePos - _lastMousePosition;

            if (button == 2 || _isPanning)
            {
                _pan += delta;
                Repaint();
            }
            else if (_draggingNode != null)
            {
                var worldDelta = delta / _zoom;
                _draggingNode.OnMouseDrag(worldDelta);
                Repaint();
            }
        }

        /// <summary>
        /// Handles mouse up events.
        /// </summary>
        private void HandleMouseUp()
        {
            _isPanning = false;

            if (_draggingNode != null)
            {
                _draggingNode.OnMouseUp();
                _draggingNode = null;
            }
        }

        /// <summary>
        /// Handles scroll wheel events for zooming.
        /// </summary>
        private void HandleScroll(Vector2 mousePos, Vector2 delta)
        {
            var zoomDelta = -delta.y * ZoomSpeed;
            var oldZoom = _zoom;

            _zoom = Mathf.Clamp(_zoom + zoomDelta, MinZoom, MaxZoom);

            var worldPos = ScreenToWorld(mousePos);
            _pan += (worldPos - ScreenToWorld(mousePos)) * (oldZoom - _zoom);

            Repaint();
        }

        /// <summary>
        /// Handles keyboard events.
        /// </summary>
        private void HandleKeyDown(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.F:
                    FrameAll();
                    break;

                case KeyCode.Delete:
                    if (_selectedNode != null)
                    {
                        DeleteNode(_selectedNode);
                    }
                    break;
            }
        }

        /// <summary>
        /// Converts screen position to world position.
        /// </summary>
        private Vector2 ScreenToWorld(Vector2 screenPos)
        {
            return (screenPos - _pan) / _zoom;
        }

        /// <summary>
        /// Gets the node at a given world position.
        /// </summary>
        private GraphNode GetNodeAtPosition(Vector2 worldPos)
        {
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                if (Nodes[i].Contains(worldPos))
                    return Nodes[i];
            }

            return null;
        }

        /// <summary>
        /// Selects a node.
        /// </summary>
        protected void SelectNode(GraphNode node)
        {
            DeselectAll();
            node.IsSelected = true;
            _selectedNode = node;
            OnNodeSelected(node);
        }

        /// <summary>
        /// Deselects all nodes.
        /// </summary>
        protected void DeselectAll()
        {
            foreach (var node in Nodes)
            {
                node.IsSelected = false;
            }

            _selectedNode = null;
        }

        /// <summary>
        /// Deletes a node and its connections.
        /// </summary>
        protected void DeleteNode(GraphNode node)
        {
            Connections.RemoveAll(c => c.FromNode == node || c.ToNode == node);
            Nodes.Remove(node);
            Repaint();
        }

        /// <summary>
        /// Frames all nodes in the view.
        /// </summary>
        protected void FrameAll()
        {
            if (Nodes.Count == 0) return;

            var minX = Nodes.Min(n => n.Position.xMin);
            var maxX = Nodes.Max(n => n.Position.xMax);
            var minY = Nodes.Min(n => n.Position.yMin);
            var maxY = Nodes.Max(n => n.Position.yMax);

            var center = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);
            var size = new Vector2(maxX - minX, maxY - minY);

            var zoomX = position.width / (size.x + 100);
            var zoomY = position.height / (size.y + 100);
            _zoom = Mathf.Clamp(Mathf.Min(zoomX, zoomY), MinZoom, MaxZoom);

            _pan = -center * _zoom + new Vector2(position.width, position.height) / 2;

            Repaint();
        }

        /// <summary>
        /// Resets the view to default zoom and pan.
        /// </summary>
        protected void ResetView()
        {
            _zoom = 1f;
            _pan = Vector2.zero;
            Repaint();
        }

        /// <summary>
        /// Called when a node is selected. Override for custom behavior.
        /// </summary>
        protected virtual void OnNodeSelected(GraphNode node)
        {
        }

        /// <summary>
        /// Refreshes the graph data. Override to implement data loading.
        /// </summary>
        protected abstract void RefreshGraph();
    }
}
