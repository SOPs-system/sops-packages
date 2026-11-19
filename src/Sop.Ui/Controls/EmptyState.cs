using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    [TemplatePart(Name = "PART_ActionButton", Type = typeof(Button))]
    [TemplatePart(Name = "PART_SecondaryButton", Type = typeof(Button))]
    public class EmptyState : Control
    {
        static EmptyState()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(EmptyState), new FrameworkPropertyMetadata(typeof(EmptyState)));
        }

        public static readonly DependencyProperty IconGlyphProperty =
            DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(EmptyState),
                new PropertyMetadata("Package"));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyState),
                new PropertyMetadata("No items found"));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(EmptyState),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ActionTextProperty =
            DependencyProperty.Register(nameof(ActionText), typeof(string), typeof(EmptyState),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ActionCommandProperty =
            DependencyProperty.Register(nameof(ActionCommand), typeof(ICommand), typeof(EmptyState),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SecondaryActionTextProperty =
            DependencyProperty.Register(nameof(SecondaryActionText), typeof(string), typeof(EmptyState),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SecondaryActionCommandProperty =
            DependencyProperty.Register(nameof(SecondaryActionCommand), typeof(ICommand), typeof(EmptyState),
                new PropertyMetadata(null));

        public static readonly RoutedEvent ActionClickEvent =
            EventManager.RegisterRoutedEvent(nameof(ActionClick), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(EmptyState));

        public static readonly RoutedEvent SecondaryActionClickEvent =
            EventManager.RegisterRoutedEvent(nameof(SecondaryActionClick), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(EmptyState));

        public string IconGlyph
        {
            get => (string)GetValue(IconGlyphProperty);
            set => SetValue(IconGlyphProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string? Description
        {
            get => (string?)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public string? ActionText
        {
            get => (string?)GetValue(ActionTextProperty);
            set => SetValue(ActionTextProperty, value);
        }

        public ICommand? ActionCommand
        {
            get => (ICommand?)GetValue(ActionCommandProperty);
            set => SetValue(ActionCommandProperty, value);
        }

        public string? SecondaryActionText
        {
            get => (string?)GetValue(SecondaryActionTextProperty);
            set => SetValue(SecondaryActionTextProperty, value);
        }

        public ICommand? SecondaryActionCommand
        {
            get => (ICommand?)GetValue(SecondaryActionCommandProperty);
            set => SetValue(SecondaryActionCommandProperty, value);
        }

        public event RoutedEventHandler ActionClick
        {
            add => AddHandler(ActionClickEvent, value);
            remove => RemoveHandler(ActionClickEvent, value);
        }

        public event RoutedEventHandler SecondaryActionClick
        {
            add => AddHandler(SecondaryActionClickEvent, value);
            remove => RemoveHandler(SecondaryActionClickEvent, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild("PART_ActionButton") is Button actionBtn)
            {
                actionBtn.Click += (s, e) =>
                {
                    RaiseEvent(new RoutedEventArgs(ActionClickEvent));
                    if (ActionCommand?.CanExecute(null) == true)
                    {
                        ActionCommand.Execute(null);
                    }
                };
            }

            if (GetTemplateChild("PART_SecondaryButton") is Button secBtn)
            {
                secBtn.Click += (s, e) =>
                {
                    RaiseEvent(new RoutedEventArgs(SecondaryActionClickEvent));
                    if (SecondaryActionCommand?.CanExecute(null) == true)
                    {
                        SecondaryActionCommand.Execute(null);
                    }
                };
            }
        }
    }
}
