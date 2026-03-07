using UnityEngine;
using UnityEngine.UIElements;

namespace Strada.Core.Editor.Graph
{
    internal static class GraphWindowHelper
    {
        public static VisualElement CreateCycleWarningBanner(string warningText)
        {
            var banner = new VisualElement();
            banner.style.backgroundColor = new Color(0.3f, 0.15f, 0.15f);
            banner.style.borderTopWidth = 2;
            banner.style.borderBottomWidth = 2;
            banner.style.borderLeftWidth = 2;
            banner.style.borderRightWidth = 2;
            var borderColor = new Color(0.8f, 0.2f, 0.2f);
            banner.style.borderTopColor = borderColor;
            banner.style.borderBottomColor = borderColor;
            banner.style.borderLeftColor = borderColor;
            banner.style.borderRightColor = borderColor;
            banner.style.paddingLeft = 12;
            banner.style.paddingRight = 12;
            banner.style.paddingTop = 8;
            banner.style.paddingBottom = 8;
            banner.style.display = DisplayStyle.None;

            var warningIcon = new Label("\u26a0");
            warningIcon.style.fontSize = 16;
            warningIcon.style.color = new Color(1f, 0.4f, 0.4f);
            warningIcon.style.marginRight = 8;

            var warningLabel = new Label(warningText);
            warningLabel.name = "cycle-warning-text";
            warningLabel.style.color = new Color(1f, 0.6f, 0.6f);
            warningLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var cyclePathLabel = new Label();
            cyclePathLabel.name = "cycle-path-label";
            cyclePathLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            cyclePathLabel.style.marginTop = 4;
            cyclePathLabel.style.fontSize = 11;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.Add(warningIcon);
            headerRow.Add(warningLabel);

            banner.Add(headerRow);
            banner.Add(cyclePathLabel);

            return banner;
        }

        public static (VisualElement statusBar, Label statusLabel, Label countLabel) CreateStatusBar(string initialCountText)
        {
            var statusBar = new VisualElement();
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.justifyContent = Justify.SpaceBetween;
            statusBar.style.paddingLeft = 8;
            statusBar.style.paddingRight = 8;
            statusBar.style.paddingTop = 4;
            statusBar.style.paddingBottom = 4;
            statusBar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            statusBar.style.borderTopWidth = 1;
            statusBar.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f);

            var statusLabel = new Label("Ready");
            statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            statusLabel.style.fontSize = 11;

            var countLabel = new Label(initialCountText);
            countLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            countLabel.style.fontSize = 11;

            statusBar.Add(statusLabel);
            statusBar.Add(countLabel);

            return (statusBar, statusLabel, countLabel);
        }

        public static VisualElement CreateToolbarBase()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.paddingTop = 4;
            toolbar.style.paddingBottom = 4;
            toolbar.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            return toolbar;
        }

        public static (VisualElement container, TextField searchField) CreateSearchSection()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var searchLabel = new Label("Search:");
            searchLabel.style.marginRight = 4;
            container.Add(searchLabel);

            var searchField = new TextField();
            searchField.style.minWidth = 180;
            container.Add(searchField);

            return (container, searchField);
        }

        public static VisualElement CreateToolbarSpacer()
        {
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            return spacer;
        }

        public static Button CreateToolbarButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.marginRight = 8;
            return button;
        }
    }
}
