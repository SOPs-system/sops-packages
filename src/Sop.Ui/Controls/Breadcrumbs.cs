using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// A single navigation crumb within a <see cref="Breadcrumbs"/> trail.
    /// Non-current items are interactive links; the final (current) item is non-interactive
    /// and highlighted to show the current page.
    /// </summary>
    public class BreadcrumbItem : System.Windows.Controls.Button
    {
        static BreadcrumbItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BreadcrumbItem), new FrameworkPropertyMetadata(typeof(BreadcrumbItem)));
        }

        public static readonly DependencyProperty IsCurrentProperty = DependencyProperty.Register(
            nameof(IsCurrent), typeof(bool), typeof(BreadcrumbItem), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HasSeparatorProperty = DependencyProperty.Register(
            nameof(HasSeparator), typeof(bool), typeof(BreadcrumbItem), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(object), typeof(BreadcrumbItem), new FrameworkPropertyMetadata(null));

        public bool IsCurrent
        {
            get => (bool)GetValue(IsCurrentProperty);
            set => SetValue(IsCurrentProperty, value);
        }

        public bool HasSeparator
        {
            get => (bool)GetValue(HasSeparatorProperty);
            set => SetValue(HasSeparatorProperty, value);
        }

        public object? Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
    }

    /// <summary>
    /// Breadcrumbs control for hierarchical navigation through nested screens, categories, and records.
    /// Automatically manages active states and directional separators.
    /// </summary>
    public class Breadcrumbs : ItemsControl
    {
        static Breadcrumbs()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Breadcrumbs), new FrameworkPropertyMetadata(typeof(Breadcrumbs)));
        }

        public static readonly DependencyProperty ItemClickCommandProperty = DependencyProperty.Register(
            nameof(ItemClickCommand), typeof(ICommand), typeof(Breadcrumbs), new FrameworkPropertyMetadata(null));

        public static readonly RoutedEvent ItemClickEvent = EventManager.RegisterRoutedEvent(
            nameof(ItemClick), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<object?>), typeof(Breadcrumbs));

        public ICommand? ItemClickCommand
        {
            get => (ICommand?)GetValue(ItemClickCommandProperty);
            set => SetValue(ItemClickCommandProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<object?> ItemClick
        {
            add => AddHandler(ItemClickEvent, value);
            remove => RemoveHandler(ItemClickEvent, value);
        }

        protected override bool IsItemItsOwnContainerOverride(object item) => item is BreadcrumbItem;

        protected override DependencyObject GetContainerForItemOverride() => new BreadcrumbItem();

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            if (element is BreadcrumbItem breadcrumbItem)
            {
                if (!(item is BreadcrumbItem) && breadcrumbItem.Content == null)
                {
                    breadcrumbItem.Content = item;
                }

                UpdateItemStates();
                breadcrumbItem.Click -= OnBreadcrumbItemClicked;
                breadcrumbItem.Click += OnBreadcrumbItemClicked;
            }
        }

        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            base.ClearContainerForItemOverride(element, item);
            if (element is BreadcrumbItem breadcrumbItem)
            {
                breadcrumbItem.Click -= OnBreadcrumbItemClicked;
            }
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);
            UpdateItemStates();
        }

        public void UpdateItemStates()
        {
            var count = Items.Count;
            for (var i = 0; i < count; i++)
            {
                if (ItemContainerGenerator.ContainerFromIndex(i) is BreadcrumbItem container)
                {
                    var isLast = i == count - 1;
                    container.IsCurrent = isLast;
                    container.HasSeparator = !isLast;
                    container.IsEnabled = !isLast;
                }
            }
        }

        private void OnBreadcrumbItemClicked(object sender, RoutedEventArgs e)
        {
            if (sender is BreadcrumbItem container)
            {
                var item = ItemContainerGenerator.ItemFromContainer(container);
                ItemClickCommand?.Execute(item);
                RaiseEvent(new RoutedPropertyChangedEventArgs<object?>(null, item, ItemClickEvent));
            }
        }
    }
}
