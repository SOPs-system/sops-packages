using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Canonical right side header / secondary drawer panel from SOPs Shell architecture (sops-backup/wpf-beta).
    /// Used for alerts, cart summaries, patient details, and conversational help.
    /// Supports interactive mouse drag-to-resize on its leading edge.
    /// </summary>
    public class SopsSidePanel : HeaderedContentControl
    {
        static SopsSidePanel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SopsSidePanel), new FrameworkPropertyMetadata(typeof(SopsSidePanel)));
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(SopsSidePanel), new PropertyMetadata("Notifications"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(SopsSidePanel),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public static readonly DependencyProperty BadgeProperty =
            DependencyProperty.Register(nameof(Badge), typeof(string), typeof(SopsSidePanel), new PropertyMetadata(null));

        public string? Badge
        {
            get => (string?)GetValue(BadgeProperty);
            set => SetValue(BadgeProperty, value);
        }

        public static readonly DependencyProperty IsResizableProperty =
            DependencyProperty.Register(nameof(IsResizable), typeof(bool), typeof(SopsSidePanel), new PropertyMetadata(true));

        public bool IsResizable
        {
            get => (bool)GetValue(IsResizableProperty);
            set => SetValue(IsResizableProperty, value);
        }

        public static readonly DependencyProperty MinPanelWidthProperty =
            DependencyProperty.Register(nameof(MinPanelWidth), typeof(double), typeof(SopsSidePanel), new PropertyMetadata(220.0));

        public double MinPanelWidth
        {
            get => (double)GetValue(MinPanelWidthProperty);
            set => SetValue(MinPanelWidthProperty, value);
        }

        public static readonly DependencyProperty MaxPanelWidthProperty =
            DependencyProperty.Register(nameof(MaxPanelWidth), typeof(double), typeof(SopsSidePanel), new PropertyMetadata(600.0));

        public double MaxPanelWidth
        {
            get => (double)GetValue(MaxPanelWidthProperty);
            set => SetValue(MaxPanelWidthProperty, value);
        }

        private Point _dragStartPoint;
        private double _dragStartWidth;
        private bool _isDragging;

        public static readonly RoutedEvent CloseClickEvent =
            EventManager.RegisterRoutedEvent(nameof(CloseClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SopsSidePanel));

        public event RoutedEventHandler CloseClick
        {
            add => AddHandler(CloseClickEvent, value);
            remove => RemoveHandler(CloseClickEvent, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("PART_CloseButton") is ButtonBase closeBtn)
            {
                closeBtn.Click += (s, e) =>
                {
                    IsOpen = false;
                    RaiseEvent(new RoutedEventArgs(CloseClickEvent, this));
                };
            }

            if (GetTemplateChild("PART_ResizeGrip") is Thumb resizeGrip)
            {
                resizeGrip.DragStarted += (s, e) =>
                {
                    UIElement reference = (UIElement)Window.GetWindow(this) ?? (Parent as UIElement) ?? this;
                    _dragStartPoint = Mouse.GetPosition(reference);
                    _dragStartWidth = Width;
                    if (double.IsNaN(_dragStartWidth) || _dragStartWidth <= 0)
                    {
                        _dragStartWidth = ActualWidth > 0 ? ActualWidth : 384.0;
                    }
                    _isDragging = true;
                };

                resizeGrip.DragDelta += (s, e) =>
                {
                    if (!_isDragging) return;

                    UIElement reference = (UIElement)Window.GetWindow(this) ?? (Parent as UIElement) ?? this;
                    Point currentPoint = Mouse.GetPosition(reference);
                    double deltaX = currentPoint.X - _dragStartPoint.X;

                    // Absolute delta from stationary Window reference eliminates Thumb self-motion feedback glitch
                    double widthChange = FlowDirection == FlowDirection.RightToLeft ? deltaX : -deltaX;
                    double newWidth = Math.Max(MinPanelWidth, Math.Min(MaxPanelWidth, _dragStartWidth + widthChange));
                    Width = newWidth;
                };

                resizeGrip.DragCompleted += (s, e) =>
                {
                    _isDragging = false;
                };
            }
        }
    }
}
