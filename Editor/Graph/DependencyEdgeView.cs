using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Strada.Core.Editor.Graph
{
    /// <summary>
    /// A graph edge representing a dependency between services.
    /// Circular dependencies are highlighted in red.
    /// Requirements: 2.2
    /// </summary>
    public class DependencyEdgeView : Edge
    {
        private static readonly Color NormalColor = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color CircularColor = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color HighlightColor = new Color(0.3f, 0.7f, 1f);

        private bool _isCircular;

        /// <summary>
        /// Gets or sets whether this edge is part of a circular dependency.
        /// </summary>
        public bool IsCircular
        {
            get => _isCircular;
            set
            {
                _isCircular = value;
                UpdateEdgeStyle();
            }
        }

        public DependencyEdgeView()
        {
            UpdateEdgeStyle();
        }

        private void UpdateEdgeStyle()
        {
            if (_isCircular)
            {
                edgeControl.inputColor = CircularColor;
                edgeControl.outputColor = CircularColor;
                AddToClassList("circular");
                RemoveFromClassList("normal");
            }
            else
            {
                edgeControl.inputColor = NormalColor;
                edgeControl.outputColor = NormalColor;
                AddToClassList("normal");
                RemoveFromClassList("circular");
            }
        }

        /// <summary>
        /// Highlights this edge.
        /// </summary>
        public void Highlight()
        {
            if (!_isCircular)
            {
                edgeControl.inputColor = HighlightColor;
                edgeControl.outputColor = HighlightColor;
            }
            AddToClassList("highlighted");
        }

        /// <summary>
        /// Removes highlight from this edge.
        /// </summary>
        public void RemoveHighlight()
        {
            UpdateEdgeStyle();
            RemoveFromClassList("highlighted");
        }
    }
}
