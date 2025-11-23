using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strada.Core.Editor.Graph
{
    /// <summary>
    /// Base class for graph nodes in visual graph editors.
    /// Represents a visual element with position, size, connections, and rendering logic.
    /// </summary>
    public abstract class GraphNode
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public Rect Position { get; set; }
        public Color Color { get; set; }
        public bool IsSelected { get; set; }
        public bool IsDragging { get; set; }

        public List<GraphConnection> IncomingConnections { get; }
        public List<GraphConnection> OutgoingConnections { get; }

        protected const float MinWidth = 120f;
        protected const float MinHeight = 60f;
        protected const float HeaderHeight = 25f;
        protected const float Padding = 8f;

        protected GraphNode()
        {
            Id = Guid.NewGuid().ToString();
            Position = new Rect(0, 0, MinWidth, MinHeight);
            Color = StradaEditorStyles.PrimaryColor;
            IncomingConnections = new List<GraphConnection>();
            OutgoingConnections = new List<GraphConnection>();
        }

        /// <summary>
        /// Draws the node at its position.
        /// </summary>
        public virtual void Draw()
        {
            DrawBackground();
            DrawHeader();
            DrawContent();
            DrawBorder();
        }

        /// <summary>
        /// Draws the node background.
        /// </summary>
        protected virtual void DrawBackground()
        {
            var bgColor = IsSelected
                ? StradaEditorStyles.Lighten(Color, 0.3f)
                : StradaEditorStyles.Darken(Color, 0.1f);

            GUI.backgroundColor = bgColor;
            GUI.Box(Position, "", GetNodeStyle());
            GUI.backgroundColor = Color.white;
        }

        /// <summary>
        /// Draws the node header with title.
        /// </summary>
        protected virtual void DrawHeader()
        {
            var headerRect = new Rect(
                Position.x,
                Position.y,
                Position.width,
                HeaderHeight);

            GUI.backgroundColor = Color;
            GUI.Box(headerRect, "", GetHeaderStyle());
            GUI.backgroundColor = Color.white;

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            GUI.Label(headerRect, Title, labelStyle);
        }

        /// <summary>
        /// Draws the node content area. Override in derived classes.
        /// </summary>
        protected abstract void DrawContent();

        /// <summary>
        /// Draws a border around the node if selected.
        /// </summary>
        protected virtual void DrawBorder()
        {
            if (!IsSelected) return;

            var borderRect = new Rect(
                Position.x - 2,
                Position.y - 2,
                Position.width + 4,
                Position.height + 4);

            DrawRect(borderRect, StradaEditorStyles.PrimaryColor, 2f);
        }

        /// <summary>
        /// Checks if a point is within the node bounds.
        /// </summary>
        public virtual bool Contains(Vector2 point)
        {
            return Position.Contains(point);
        }

        /// <summary>
        /// Handles mouse down event.
        /// </summary>
        public virtual void OnMouseDown(Vector2 mousePosition)
        {
            if (Contains(mousePosition))
            {
                IsDragging = true;
            }
        }

        /// <summary>
        /// Handles mouse drag event.
        /// </summary>
        public virtual void OnMouseDrag(Vector2 delta)
        {
            if (IsDragging)
            {
                Position = new Rect(
                    Position.x + delta.x,
                    Position.y + delta.y,
                    Position.width,
                    Position.height);
            }
        }

        /// <summary>
        /// Handles mouse up event.
        /// </summary>
        public virtual void OnMouseUp()
        {
            IsDragging = false;
        }

        /// <summary>
        /// Called when the node is clicked.
        /// </summary>
        public virtual void OnClick()
        {
        }

        /// <summary>
        /// Called when the node is double-clicked.
        /// </summary>
        public virtual void OnDoubleClick()
        {
        }

        /// <summary>
        /// Shows a context menu for the node.
        /// </summary>
        public virtual void ShowContextMenu()
        {
        }

        /// <summary>
        /// Gets the input connection point (left side).
        /// </summary>
        public virtual Vector2 GetInputPoint()
        {
            return new Vector2(Position.x, Position.y + Position.height / 2);
        }

        /// <summary>
        /// Gets the output connection point (right side).
        /// </summary>
        public virtual Vector2 GetOutputPoint()
        {
            return new Vector2(Position.xMax, Position.y + Position.height / 2);
        }

        protected virtual GUIStyle GetNodeStyle()
        {
            return GUI.skin.box;
        }

        protected virtual GUIStyle GetHeaderStyle()
        {
            return GUI.skin.box;
        }

        protected void DrawRect(Rect rect, Color color, float thickness = 1f)
        {
            UnityEditor.Handles.BeginGUI();
            UnityEditor.Handles.color = color;

            Vector3[] points = new[]
            {
                new Vector3(rect.xMin, rect.yMin),
                new Vector3(rect.xMax, rect.yMin),
                new Vector3(rect.xMax, rect.yMax),
                new Vector3(rect.xMin, rect.yMax),
                new Vector3(rect.xMin, rect.yMin)
            };

            UnityEditor.Handles.DrawAAPolyLine(thickness, points);
            UnityEditor.Handles.EndGUI();
        }
    }
}
