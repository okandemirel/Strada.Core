using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Graph
{
    /// <summary>
    /// Represents a connection (edge) between two graph nodes.
    /// </summary>
    public class GraphConnection
    {
        public GraphNode FromNode { get; set; }
        public GraphNode ToNode { get; set; }
        public Color Color { get; set; }
        public float Thickness { get; set; }
        public bool IsAnimated { get; set; }
        public string Label { get; set; }

        private float _animationOffset;
        private const float AnimationSpeed = 0.5f;
        private const float DashLength = 5f;

        public GraphConnection(GraphNode fromNode, GraphNode toNode)
        {
            FromNode = fromNode;
            ToNode = toNode;
            Color = StradaEditorStyles.TextColor;
            Thickness = 2f;
            IsAnimated = false;
        }

        /// <summary>
        /// Draws the connection between nodes using Bezier curves.
        /// </summary>
        public void Draw()
        {
            if (FromNode == null || ToNode == null)
                return;

            var startPos = FromNode.GetOutputPoint();
            var endPos = ToNode.GetInputPoint();

            DrawBezierConnection(startPos, endPos);

            if (!string.IsNullOrEmpty(Label))
            {
                DrawLabel(startPos, endPos);
            }

            if (IsAnimated)
            {
                _animationOffset += AnimationSpeed * Time.deltaTime;
                if (_animationOffset > DashLength * 2)
                    _animationOffset = 0;
            }
        }

        /// <summary>
        /// Draws a Bezier curve connection.
        /// </summary>
        private void DrawBezierConnection(Vector2 start, Vector2 end)
        {
            var tangentOffset = Mathf.Abs(end.x - start.x) * 0.5f;
            var startTangent = start + Vector2.right * tangentOffset;
            var endTangent = end + Vector2.left * tangentOffset;

            Handles.BeginGUI();
            Handles.color = Color;

            if (IsAnimated)
            {
                DrawAnimatedBezier(start, startTangent, endTangent, end);
            }
            else
            {
                Handles.DrawBezier(start, end, startTangent, endTangent, Color, null, Thickness);
            }

            Handles.EndGUI();

            DrawArrowhead(end, endTangent);
        }

        /// <summary>
        /// Draws an animated (dashed) Bezier curve.
        /// </summary>
        private void DrawAnimatedBezier(Vector2 start, Vector2 startTangent, Vector2 endTangent, Vector2 end)
        {
            const int segments = 20;
            var dashPattern = new[] { DashLength, DashLength };

            for (int i = 0; i < segments; i++)
            {
                float t1 = i / (float)segments;
                float t2 = (i + 1) / (float)segments;

                var p1 = CalculateBezierPoint(t1, start, startTangent, endTangent, end);
                var p2 = CalculateBezierPoint(t2, start, startTangent, endTangent, end);

                var segmentOffset = (i * DashLength + _animationOffset) % (DashLength * 2);
                if (segmentOffset < DashLength)
                {
                    Handles.DrawLine(p1, p2, Thickness);
                }
            }
        }

        /// <summary>
        /// Calculates a point on a cubic Bezier curve.
        /// </summary>
        private Vector2 CalculateBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 p = uuu * p0;
            p += 3 * uu * t * p1;
            p += 3 * u * tt * p2;
            p += ttt * p3;

            return p;
        }

        /// <summary>
        /// Draws an arrowhead at the end of the connection.
        /// </summary>
        private void DrawArrowhead(Vector2 end, Vector2 tangent)
        {
            var direction = (end - tangent).normalized;
            var perpendicular = new Vector2(-direction.y, direction.x);

            var arrowSize = 10f;
            var arrowWidth = 6f;

            var arrowTip = end;
            var arrowBase = end - direction * arrowSize;
            var arrowLeft = arrowBase + perpendicular * arrowWidth;
            var arrowRight = arrowBase - perpendicular * arrowWidth;

            Handles.BeginGUI();
            Handles.color = Color;

            Vector3[] arrowPoints = new[]
            {
                new Vector3(arrowTip.x, arrowTip.y, 0),
                new Vector3(arrowLeft.x, arrowLeft.y, 0),
                new Vector3(arrowRight.x, arrowRight.y, 0),
                new Vector3(arrowTip.x, arrowTip.y, 0)
            };

            Handles.DrawAAConvexPolygon(arrowPoints);
            Handles.EndGUI();
        }

        /// <summary>
        /// Draws a label in the middle of the connection.
        /// </summary>
        private void DrawLabel(Vector2 start, Vector2 end)
        {
            var midPoint = (start + end) / 2;
            var labelRect = new Rect(
                midPoint.x - 40,
                midPoint.y - 10,
                80,
                20);

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                normal = { textColor = StradaEditorStyles.TextColor }
            };

            GUI.backgroundColor = StradaEditorStyles.BackgroundColor;
            GUI.Box(labelRect, Label, style);
            GUI.backgroundColor = Color.white;
        }

        /// <summary>
        /// Checks if a point is near the connection line.
        /// </summary>
        public bool IsNearPoint(Vector2 point, float threshold = 10f)
        {
            var start = FromNode.GetOutputPoint();
            var end = ToNode.GetInputPoint();

            return DistanceToLine(point, start, end) < threshold;
        }

        /// <summary>
        /// Calculates distance from a point to a line segment.
        /// </summary>
        private float DistanceToLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            var line = lineEnd - lineStart;
            var len = line.magnitude;
            line.Normalize();

            var v = point - lineStart;
            var d = Vector2.Dot(v, line);
            d = Mathf.Clamp(d, 0f, len);

            var closestPoint = lineStart + line * d;
            return Vector2.Distance(point, closestPoint);
        }
    }
}
