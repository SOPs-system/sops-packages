using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Item inside the SOPs navigation sidebar.
    /// </summary>
    public class SopsSidebarItem : Control
    {
        static SopsSidebarItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SopsSidebarItem), new FrameworkPropertyMetadata(typeof(SopsSidebarItem)));
        }

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(string), typeof(SopsSidebarItem), new PropertyMetadata("Scan"));

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(SopsSidebarItem), new PropertyMetadata(string.Empty));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(SopsSidebarItem), new PropertyMetadata(false));

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public static readonly DependencyProperty BadgeProperty =
            DependencyProperty.Register(nameof(Badge), typeof(string), typeof(SopsSidebarItem), new PropertyMetadata(null));

        public string? Badge
        {
            get => (string?)GetValue(BadgeProperty);
            set => SetValue(BadgeProperty, value);
        }

        public static readonly RoutedEvent ClickEvent =
            EventManager.RegisterRoutedEvent(nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SopsSidebarItem));

        public event RoutedEventHandler Click
        {
            add => AddHandler(ClickEvent, value);
            remove => RemoveHandler(ClickEvent, value);
        }

        protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        }
    }

    /// <summary>
    /// Canonical left side header / navigation rail from SOPs Shell architecture.
    /// Supports smooth 64px (rail) to 224px (expanded) collapse, screen routes, and sign-out.
    /// </summary>
    public class SopsSidebar : HeaderedItemsControl
    {
        static SopsSidebar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SopsSidebar), new FrameworkPropertyMetadata(typeof(SopsSidebar)));
        }

        public static readonly DependencyProperty IsCollapsedProperty =
            DependencyProperty.RegisterAttached(
                "IsCollapsed",
                typeof(bool),
                typeof(SopsSidebar),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        public static bool GetIsCollapsed(DependencyObject obj) => (bool)obj.GetValue(IsCollapsedProperty);
        public static void SetIsCollapsed(DependencyObject obj, bool value) => obj.SetValue(IsCollapsedProperty, value);

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(SopsSidebar),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure, OnIsExpandedChanged));

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public static readonly DependencyProperty SignOutTextProperty =
            DependencyProperty.Register(nameof(SignOutText), typeof(string), typeof(SopsSidebar), new PropertyMetadata("Sign Out"));

        public string SignOutText
        {
            get => (string)GetValue(SignOutTextProperty);
            set => SetValue(SignOutTextProperty, value);
        }

        public static readonly RoutedEvent ToggleCollapseEvent =
            EventManager.RegisterRoutedEvent(nameof(ToggleCollapse), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SopsSidebar));

        public event RoutedEventHandler ToggleCollapse
        {
            add => AddHandler(ToggleCollapseEvent, value);
            remove => RemoveHandler(ToggleCollapseEvent, value);
        }

        public static readonly RoutedEvent SignOutClickEvent =
            EventManager.RegisterRoutedEvent(nameof(SignOutClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SopsSidebar));

        public event RoutedEventHandler SignOutClick
        {
            add => AddHandler(SignOutClickEvent, value);
            remove => RemoveHandler(SignOutClickEvent, value);
        }

        private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SopsSidebar sidebar)
            {
                bool expanded = (bool)e.NewValue;
                sidebar.Width = expanded ? 224.0 : 64.0;
                sidebar.SetValue(IsCollapsedProperty, !expanded);
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            Width = IsExpanded ? 224.0 : 64.0;
            SetValue(IsCollapsedProperty, !IsExpanded);

            if (GetTemplateChild("PART_Toggle") is ButtonBase toggleBtn)
            {
                toggleBtn.Click += (s, e) =>
                {
                    IsExpanded = !IsExpanded;
                    RaiseEvent(new RoutedEventArgs(ToggleCollapseEvent, this));
                };
            }
            if (GetTemplateChild("PART_SignOut") is ButtonBase signOutBtn)
            {
                signOutBtn.Click += (s, e) => RaiseEvent(new RoutedEventArgs(SignOutClickEvent, this));
            }
        }
    }
}
